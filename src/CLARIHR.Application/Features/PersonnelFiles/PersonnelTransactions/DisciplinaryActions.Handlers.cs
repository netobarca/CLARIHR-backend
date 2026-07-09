using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles.PersonnelTransactions;

/// <summary>
/// Shared write-validation glue for disciplinary actions: the incident date ≤ today (RN-10), the suspension
/// block against the type flag (RN-05) and the deduction block against the amount (RN-06). The overlap under a
/// lock (RN-18) and the concept snapshot (aclaración №5) are decision-time concerns and live in the handler.
/// </summary>
internal static class DisciplinaryActionWriteSupport
{
    public const string DisciplinaryActionTypeCode = "AMONESTACION";
    public const string SuspensionActionTypeCode = "SUSPENSION";
    public const string AppliedActionStatusCode = "APLICADA";

    /// <summary>
    /// Validates the declarative blocks against the clock (incident date), the type's suspension flag and the
    /// deduction amount. <paramref name="typeAppliesSuspension"/> is the resolved master flag (RN-05).
    /// </summary>
    public static Error? ValidateDeclarative(DisciplinaryActionInput item, bool typeAppliesSuspension, DateOnly today)
    {
        if (item.IncidentDate > today)
        {
            return DisciplinaryActionErrors.IncidentDateInFuture;
        }

        switch (PersonnelTransactionRules.ValidateSuspensionBlock(typeAppliesSuspension, item.SuspensionStartDate, item.SuspensionEndDate))
        {
            case SuspensionBlockValidation.NotAllowedForType:
                return DisciplinaryActionErrors.SuspensionNotAllowedForType;
            case SuspensionBlockValidation.DatesRequired:
                return DisciplinaryActionErrors.SuspensionDatesRequired;
            case SuspensionBlockValidation.RangeInvalid:
                return DisciplinaryActionErrors.SuspensionRangeInvalid;
        }

        if (PersonnelTransactionRules.ValidateDeduction(item.HasPayrollDeduction, item.DeductionAmount)
            == DeductionValidation.AmountRequired)
        {
            return DisciplinaryActionErrors.DeductionAmountRequired;
        }

        return null;
    }
}

internal sealed class AddPersonnelFileDisciplinaryActionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelTransactionRepository transactionRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFileDisciplinaryActionCommand, PersonnelFileDisciplinaryActionResponse>
{
    public async Task<Result<PersonnelFileDisciplinaryActionResponse>> Handle(
        AddPersonnelFileDisciplinaryActionCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageDisciplinaryActionsAsync<PersonnelFileDisciplinaryActionResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileDisciplinaryActionResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var tenantId = personnelFile.TenantId;
        if (await transactionRepository.IsProfileRetiredAsync(personnelFile.Id, cancellationToken))
        {
            return Result<PersonnelFileDisciplinaryActionResponse>.Failure(PersonnelFileErrors.ProfileRetiredLocked);
        }

        var item = command.Item;

        var typeRef = await transactionRepository.ResolveActiveDisciplinaryActionTypeAsync(tenantId, item.DisciplinaryActionTypePublicId, cancellationToken);
        if (typeRef is null)
        {
            return Result<PersonnelFileDisciplinaryActionResponse>.Failure(DisciplinaryActionErrors.TypeInvalid);
        }

        var causeRef = await transactionRepository.ResolveActiveDisciplinaryActionCauseAsync(tenantId, item.DisciplinaryActionCausePublicId, cancellationToken);
        if (causeRef is null)
        {
            return Result<PersonnelFileDisciplinaryActionResponse>.Failure(DisciplinaryActionErrors.CauseInvalid);
        }

        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow);
        if (DisciplinaryActionWriteSupport.ValidateDeclarative(item, typeRef.AppliesSuspension, today) is { } declarativeError)
        {
            return Result<PersonnelFileDisciplinaryActionResponse>.Failure(declarativeError);
        }

        // The optional deduction-concept override is an editable reference (default from the cause); when it
        // travels it is validated as an active egreso (aclaración №5). The authoritative concept is frozen from
        // the cause default at Apply.
        if (await ValidateInputConceptAsync(transactionRepository, tenantId, item.DeductionConceptTypeCode, cancellationToken) is { } conceptError)
        {
            return Result<PersonnelFileDisciplinaryActionResponse>.Failure(conceptError);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var currentUserId);
        var entity = PersonnelFileDisciplinaryAction.Create(
            typeRef.InternalId,
            typeRef.Name,
            typeRef.AppliesSuspension,
            causeRef.InternalId,
            causeRef.Name,
            item.IncidentDate,
            item.FactsDetail,
            item.HasPayrollDeduction,
            item.DeductionAmount,
            item.CurrencyCode,
            item.SuspensionStartDate,
            item.SuspensionEndDate,
            item.AssignedPositionPublicId,
            currentUserId,
            item.Notes);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(tenantId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            transactionRepository.AddDisciplinaryAction(entity);
            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await transactionRepository.GetDisciplinaryActionResponseAsync(personnelFile.PublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Disciplinary-action response could not be resolved after creation.");

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Registered a disciplinary action for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileDisciplinaryActionResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    internal static async Task<Error?> ValidateInputConceptAsync(
        IPersonnelTransactionRepository transactionRepository,
        Guid tenantId,
        string? conceptCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(conceptCode))
        {
            return null;
        }

        var resolved = await transactionRepository.ResolveActiveEgressConceptAsync(tenantId, conceptCode, cancellationToken);
        return resolved is null ? DisciplinaryActionErrors.DeductionConceptInvalid : null;
    }
}

internal sealed class UpdatePersonnelFileDisciplinaryActionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelTransactionRepository transactionRepository,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileDisciplinaryActionCommand, PersonnelFileDisciplinaryActionResponse>
{
    public async Task<Result<PersonnelFileDisciplinaryActionResponse>> Handle(
        UpdatePersonnelFileDisciplinaryActionCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageDisciplinaryActionsAsync<PersonnelFileDisciplinaryActionResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entity = await transactionRepository.GetDisciplinaryActionEntityAsync(personnelFile!.PublicId, command.DisciplinaryActionPublicId, cancellationToken);
        if (entity is null)
        {
            return Result<PersonnelFileDisciplinaryActionResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileDisciplinaryActionResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (!PersonnelTransactionStatuses.IsEditable(entity.StatusCode))
        {
            return Result<PersonnelFileDisciplinaryActionResponse>.Failure(DisciplinaryActionErrors.StateRuleViolation);
        }

        var tenantId = personnelFile.TenantId;
        if (await transactionRepository.IsProfileRetiredAsync(personnelFile.Id, cancellationToken))
        {
            return Result<PersonnelFileDisciplinaryActionResponse>.Failure(PersonnelFileErrors.ProfileRetiredLocked);
        }

        var item = command.Item;

        var typeRef = await transactionRepository.ResolveActiveDisciplinaryActionTypeAsync(tenantId, item.DisciplinaryActionTypePublicId, cancellationToken);
        if (typeRef is null)
        {
            return Result<PersonnelFileDisciplinaryActionResponse>.Failure(DisciplinaryActionErrors.TypeInvalid);
        }

        var causeRef = await transactionRepository.ResolveActiveDisciplinaryActionCauseAsync(tenantId, item.DisciplinaryActionCausePublicId, cancellationToken);
        if (causeRef is null)
        {
            return Result<PersonnelFileDisciplinaryActionResponse>.Failure(DisciplinaryActionErrors.CauseInvalid);
        }

        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow);
        if (DisciplinaryActionWriteSupport.ValidateDeclarative(item, typeRef.AppliesSuspension, today) is { } declarativeError)
        {
            return Result<PersonnelFileDisciplinaryActionResponse>.Failure(declarativeError);
        }

        if (await AddPersonnelFileDisciplinaryActionCommandHandler.ValidateInputConceptAsync(
                transactionRepository, tenantId, item.DeductionConceptTypeCode, cancellationToken) is { } conceptError)
        {
            return Result<PersonnelFileDisciplinaryActionResponse>.Failure(conceptError);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            entity.Update(
                typeRef.InternalId,
                typeRef.Name,
                typeRef.AppliesSuspension,
                causeRef.InternalId,
                causeRef.Name,
                item.IncidentDate,
                item.FactsDetail,
                item.HasPayrollDeduction,
                item.DeductionAmount,
                item.CurrencyCode,
                item.SuspensionStartDate,
                item.SuspensionEndDate,
                item.AssignedPositionPublicId,
                item.Notes);
            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await transactionRepository.GetDisciplinaryActionResponseAsync(personnelFile.PublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Disciplinary-action response could not be resolved after update.");

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Updated a disciplinary action for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileDisciplinaryActionResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DecidePersonnelFileDisciplinaryActionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelTransactionRepository transactionRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DecidePersonnelFileDisciplinaryActionCommand, PersonnelFileDisciplinaryActionResponse>
{
    public async Task<Result<PersonnelFileDisciplinaryActionResponse>> Handle(
        DecidePersonnelFileDisciplinaryActionCommand command,
        CancellationToken cancellationToken)
    {
        // The single decision requires the dedicated AuthorizeDisciplinaryActions grant (Admin excluded, D-05).
        var (failure, personnelFile) = await LoadForAuthorizeDisciplinaryActionsAsync<PersonnelFileDisciplinaryActionResponse>(
            command.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var isApply = string.Equals(command.Decision, DisciplinaryActionDecisions.Apply, StringComparison.OrdinalIgnoreCase);
        if (!isApply && string.IsNullOrWhiteSpace(command.Note))
        {
            return Result<PersonnelFileDisciplinaryActionResponse>.Failure(DisciplinaryActionErrors.DecisionNoteRequired);
        }

        var entity = await transactionRepository.GetDisciplinaryActionEntityAsync(personnelFile!.PublicId, command.DisciplinaryActionPublicId, cancellationToken);
        if (entity is null)
        {
            return Result<PersonnelFileDisciplinaryActionResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileDisciplinaryActionResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // Double anti-self-approval (RN-02): neither the subject employee nor the registrar may decide.
        if (PersonnelTransactionRules.IsSelfDecision(
                personnelFile.LinkedUserPublicId?.ToString(), entity.RegisteredByUserId.ToString(), currentUserService.UserId ?? string.Empty))
        {
            return Result<PersonnelFileDisciplinaryActionResponse>.Failure(DisciplinaryActionErrors.SelfApprovalForbidden);
        }

        // Re-verify the record is still EN_REVISION (a concurrent decision would already have moved it — 422).
        if (!PersonnelTransactionStatuses.IsEditable(entity.StatusCode))
        {
            return Result<PersonnelFileDisciplinaryActionResponse>.Failure(DisciplinaryActionErrors.StateRuleViolation);
        }

        var tenantId = personnelFile.TenantId;

        // On a retired profile only RECHAZAR is allowed; applying (which stamps the file) is blocked (aclaración №10).
        if (isApply && await transactionRepository.IsProfileRetiredAsync(personnelFile.Id, cancellationToken))
        {
            return Result<PersonnelFileDisciplinaryActionResponse>.Failure(PersonnelFileErrors.ProfileRetiredLocked);
        }

        // Freeze the deduction concept from the cause default and re-validate it is still an active egreso
        // (aclaración №5). Resolved before the transaction (a read); the freeze happens under the same tx as
        // the journal entries.
        string? conceptCode = null;
        string? conceptName = null;
        if (isApply && entity.HasPayrollDeduction)
        {
            var causeConceptCode = await transactionRepository.GetDisciplinaryActionCauseConceptCodeAsync(entity.DisciplinaryActionCauseId, cancellationToken);
            if (!string.IsNullOrWhiteSpace(causeConceptCode))
            {
                var resolved = await transactionRepository.ResolveActiveEgressConceptAsync(tenantId, causeConceptCode, cancellationToken);
                if (resolved is null)
                {
                    return Result<PersonnelFileDisciplinaryActionResponse>.Failure(DisciplinaryActionErrors.DeductionConceptInvalid);
                }

                conceptCode = resolved.Code;
                conceptName = resolved.Name;
            }
        }

        var nowUtc = dateTimeProvider.UtcNow;
        _ = Guid.TryParse(currentUserService.UserId, out var currentUserId);
        var hasSuspension = isApply && entity is { SuspensionStartDate: { } start, SuspensionEndDate: { } end };

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            if (isApply)
            {
                if (hasSuspension)
                {
                    // Serialize the apply-with-suspension race per (tenant, employee) and re-verify the overlap
                    // under the advisory lock (RN-18 / aclaración №3).
                    await transactionRepository.AcquireEmployeeRelationsLockAsync(tenantId, personnelFile.Id, cancellationToken);
                    if (await transactionRepository.HasOverlappingSuspensionAsync(
                            personnelFile.Id, entity.SuspensionStartDate!.Value, entity.SuspensionEndDate!.Value, entity.Id, cancellationToken))
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<PersonnelFileDisciplinaryActionResponse>.Failure(DisciplinaryActionErrors.SuspensionOverlap);
                    }
                }

                var amonestacion = PersonnelFilePersonnelAction.Create(
                    DisciplinaryActionWriteSupport.DisciplinaryActionTypeCode,
                    DisciplinaryActionWriteSupport.AppliedActionStatusCode,
                    actionDateUtc: entity.IncidentDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                    effectiveFromUtc: null,
                    effectiveToUtc: null,
                    description: $"AMONESTACION ({entity.TypeNameSnapshot} / {entity.CauseNameSnapshot}) {entity.IncidentDate:yyyy-MM-dd}.",
                    reference: null,
                    amount: entity.HasPayrollDeduction ? entity.DeductionAmount : null,
                    currencyCode: entity.HasPayrollDeduction ? entity.CurrencyCode : null,
                    isSystemGenerated: true);
                amonestacion.BindToPersonnelFile(personnelFile.Id);
                amonestacion.SetTenantId(tenantId);
                _ = await employeeRepository.AddPersonnelActionAsync(amonestacion, cancellationToken);

                Guid? suspensionPublicId = null;
                if (hasSuspension)
                {
                    var suspension = PersonnelFilePersonnelAction.Create(
                        DisciplinaryActionWriteSupport.SuspensionActionTypeCode,
                        DisciplinaryActionWriteSupport.AppliedActionStatusCode,
                        actionDateUtc: nowUtc,
                        effectiveFromUtc: entity.SuspensionStartDate!.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                        effectiveToUtc: entity.SuspensionEndDate!.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                        description: $"SUSPENSION SIN GOCE ({entity.SuspensionDays} días) {entity.SuspensionStartDate:yyyy-MM-dd}–{entity.SuspensionEndDate:yyyy-MM-dd}.",
                        reference: null,
                        amount: null,
                        currencyCode: null,
                        isSystemGenerated: true);
                    suspension.BindToPersonnelFile(personnelFile.Id);
                    suspension.SetTenantId(tenantId);
                    _ = await employeeRepository.AddPersonnelActionAsync(suspension, cancellationToken);
                    suspensionPublicId = suspension.PublicId;
                }

                entity.Apply(currentUserId, nowUtc, amonestacion.PublicId, suspensionPublicId, conceptCode, conceptName);
            }
            else
            {
                entity.Reject(currentUserId, nowUtc, command.Note!);
            }

            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await transactionRepository.GetDisciplinaryActionResponseAsync(personnelFile.PublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Disciplinary-action response could not be resolved after the decision.");

            var verb = isApply ? "Applied" : "Rejected";
            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"{verb} a disciplinary action for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileDisciplinaryActionResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class AnnulPersonnelFileDisciplinaryActionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelTransactionRepository transactionRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AnnulPersonnelFileDisciplinaryActionCommand, PersonnelFileDisciplinaryActionResponse>
{
    public async Task<Result<PersonnelFileDisciplinaryActionResponse>> Handle(
        AnnulPersonnelFileDisciplinaryActionCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileDisciplinaryActionResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return Result<PersonnelFileDisciplinaryActionResponse>.Failure(DisciplinaryActionErrors.AnnulmentReasonRequired);
        }

        var tenantId = tenantContext.TenantId.Value;
        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(command.PersonnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileDisciplinaryActionResponse>.Failure(
                await personnelFileRepository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        var entity = await transactionRepository.GetDisciplinaryActionEntityAsync(personnelFile.PublicId, command.DisciplinaryActionPublicId, cancellationToken);
        if (entity is null)
        {
            return Result<PersonnelFileDisciplinaryActionResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileDisciplinaryActionResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var wasApplied = entity.StatusCode == PersonnelTransactionStatuses.Aplicada;
        if (entity.StatusCode == PersonnelTransactionStatuses.EnRevision)
        {
            // Trámite withdrawal — the manage permission suffices.
            var manage = await authorizationService.EnsureCanManageDisciplinaryActionsAsync(tenantId, cancellationToken);
            if (manage.IsFailure)
            {
                return Result<PersonnelFileDisciplinaryActionResponse>.Failure(manage.Error);
            }
        }
        else if (wasApplied)
        {
            // Revocation — the dedicated AuthorizeDisciplinaryActions grant + the double anti-self check.
            var authorize = await authorizationService.EnsureCanAuthorizeDisciplinaryActionsAsync(tenantId, cancellationToken);
            if (authorize.IsFailure)
            {
                return Result<PersonnelFileDisciplinaryActionResponse>.Failure(authorize.Error);
            }

            if (PersonnelTransactionRules.IsSelfDecision(
                    personnelFile.LinkedUserPublicId?.ToString(), entity.RegisteredByUserId.ToString(), currentUserService.UserId ?? string.Empty))
            {
                return Result<PersonnelFileDisciplinaryActionResponse>.Failure(DisciplinaryActionErrors.SelfApprovalForbidden);
            }
        }
        else
        {
            return Result<PersonnelFileDisciplinaryActionResponse>.Failure(DisciplinaryActionErrors.StateRuleViolation);
        }

        var nowUtc = dateTimeProvider.UtcNow;
        _ = Guid.TryParse(currentUserService.UserId, out var currentUserId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // Revocation of an applied disciplinary action annuls BOTH linked entries (amonestación + suspensión)
            // in the same tx (aclaración №4).
            if (wasApplied)
            {
                if (entity.PersonnelActionPublicId is { } actionPublicId)
                {
                    var action = await transactionRepository.GetPersonnelActionEntityAsync(personnelFile.Id, actionPublicId, cancellationToken);
                    action?.Annul();
                }

                if (entity.SuspensionActionPublicId is { } suspensionActionPublicId)
                {
                    var suspensionAction = await transactionRepository.GetPersonnelActionEntityAsync(personnelFile.Id, suspensionActionPublicId, cancellationToken);
                    suspensionAction?.Annul();
                }
            }

            entity.Annul(command.Reason, currentUserId, nowUtc);
            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await transactionRepository.GetDisciplinaryActionResponseAsync(personnelFile.PublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Disciplinary-action response could not be resolved after annulment.");

            var verb = wasApplied ? "Revoked" : "Annulled";
            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"{verb} a disciplinary action for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileDisciplinaryActionResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class GetPersonnelFileDisciplinaryActionsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelTransactionRepository transactionRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileDisciplinaryActionsQuery, IReadOnlyCollection<PersonnelFileDisciplinaryActionResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileDisciplinaryActionResponse>>> Handle(
        GetPersonnelFileDisciplinaryActionsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile, restrictToApplied) = await LoadCompletedEmployeeForDisciplinaryActionReadAsync<IReadOnlyCollection<PersonnelFileDisciplinaryActionResponse>>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await transactionRepository.GetDisciplinaryActionResponsesAsync(personnelFile!.PublicId, restrictToApplied, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileDisciplinaryActionResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileDisciplinaryActionByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelTransactionRepository transactionRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileDisciplinaryActionByIdQuery, PersonnelFileDisciplinaryActionResponse>
{
    public async Task<Result<PersonnelFileDisciplinaryActionResponse>> Handle(
        GetPersonnelFileDisciplinaryActionByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile, restrictToApplied) = await LoadCompletedEmployeeForDisciplinaryActionReadAsync<PersonnelFileDisciplinaryActionResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await transactionRepository.GetDisciplinaryActionResponseAsync(personnelFile!.PublicId, query.DisciplinaryActionPublicId, cancellationToken);

        // The self-service employee only ever sees their APLICADA disciplinary actions (D-13) — a non-applied
        // record is masked as not found rather than leaked.
        if (response is null || (restrictToApplied && response.StatusCode != PersonnelTransactionStatuses.Aplicada))
        {
            return Result<PersonnelFileDisciplinaryActionResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        return Result<PersonnelFileDisciplinaryActionResponse>.Success(response);
    }
}

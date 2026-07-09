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

/// <summary>Shared write-validation glue for recognitions (event date ≤ today, RN-17 amount coherence).</summary>
internal static class RecognitionWriteSupport
{
    public const string ActionTypeCode = "RECONOCIMIENTO";
    public const string AppliedActionStatusCode = "APLICADA";

    /// <summary>Validates the declarative block against the clock (event date) and RN-17 (amount/currency).</summary>
    public static Error? ValidateDeclarative(RecognitionInput item, DateOnly today)
    {
        if (item.EventDate > today)
        {
            return RecognitionErrors.EventDateInFuture;
        }

        // RN-17: an amount is informational but, when it travels, it must be positive and carry a currency.
        if (item.Amount is { } amount && (amount <= 0m || string.IsNullOrWhiteSpace(item.CurrencyCode)))
        {
            return RecognitionErrors.AmountInvalid;
        }

        return null;
    }
}

internal sealed class AddPersonnelFileRecognitionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelTransactionRepository transactionRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFileRecognitionCommand, PersonnelFileRecognitionResponse>
{
    public async Task<Result<PersonnelFileRecognitionResponse>> Handle(
        AddPersonnelFileRecognitionCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageRecognitionsAsync<PersonnelFileRecognitionResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileRecognitionResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var tenantId = personnelFile.TenantId;
        if (await transactionRepository.IsProfileRetiredAsync(personnelFile.Id, cancellationToken))
        {
            return Result<PersonnelFileRecognitionResponse>.Failure(PersonnelFileErrors.ProfileRetiredLocked);
        }

        var item = command.Item;
        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow);
        if (RecognitionWriteSupport.ValidateDeclarative(item, today) is { } declarativeError)
        {
            return Result<PersonnelFileRecognitionResponse>.Failure(declarativeError);
        }

        var typeRef = await transactionRepository.ResolveActiveRecognitionTypeAsync(tenantId, item.RecognitionTypePublicId, cancellationToken);
        if (typeRef is null)
        {
            return Result<PersonnelFileRecognitionResponse>.Failure(RecognitionErrors.TypeInvalid);
        }

        var entity = PersonnelFileRecognition.Create(
            typeRef.InternalId,
            typeRef.Name,
            item.EventDate,
            item.Detail,
            item.Amount,
            item.CurrencyCode,
            item.AssignedPositionPublicId,
            currentUserService.UserId ?? string.Empty,
            item.Notes);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(tenantId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            transactionRepository.AddRecognition(entity);
            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await transactionRepository.GetRecognitionResponseAsync(personnelFile.PublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Recognition response could not be resolved after creation.");

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Registered a recognition for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileRecognitionResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdatePersonnelFileRecognitionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelTransactionRepository transactionRepository,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileRecognitionCommand, PersonnelFileRecognitionResponse>
{
    public async Task<Result<PersonnelFileRecognitionResponse>> Handle(
        UpdatePersonnelFileRecognitionCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageRecognitionsAsync<PersonnelFileRecognitionResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entity = await transactionRepository.GetRecognitionEntityAsync(personnelFile!.PublicId, command.RecognitionPublicId, cancellationToken);
        if (entity is null)
        {
            return Result<PersonnelFileRecognitionResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileRecognitionResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (!PersonnelTransactionStatuses.IsEditable(entity.StatusCode))
        {
            return Result<PersonnelFileRecognitionResponse>.Failure(RecognitionErrors.StateRuleViolation);
        }

        var tenantId = personnelFile.TenantId;
        if (await transactionRepository.IsProfileRetiredAsync(personnelFile.Id, cancellationToken))
        {
            return Result<PersonnelFileRecognitionResponse>.Failure(PersonnelFileErrors.ProfileRetiredLocked);
        }

        var item = command.Item;
        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow);
        if (RecognitionWriteSupport.ValidateDeclarative(item, today) is { } declarativeError)
        {
            return Result<PersonnelFileRecognitionResponse>.Failure(declarativeError);
        }

        var typeRef = await transactionRepository.ResolveActiveRecognitionTypeAsync(tenantId, item.RecognitionTypePublicId, cancellationToken);
        if (typeRef is null)
        {
            return Result<PersonnelFileRecognitionResponse>.Failure(RecognitionErrors.TypeInvalid);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            entity.Update(
                typeRef.InternalId,
                typeRef.Name,
                item.EventDate,
                item.Detail,
                item.Amount,
                item.CurrencyCode,
                item.AssignedPositionPublicId,
                item.Notes);
            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await transactionRepository.GetRecognitionResponseAsync(personnelFile.PublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Recognition response could not be resolved after update.");

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Updated a recognition for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileRecognitionResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DecidePersonnelFileRecognitionCommandHandler(
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
      ICommandHandler<DecidePersonnelFileRecognitionCommand, PersonnelFileRecognitionResponse>
{
    public async Task<Result<PersonnelFileRecognitionResponse>> Handle(
        DecidePersonnelFileRecognitionCommand command,
        CancellationToken cancellationToken)
    {
        // The single decision requires the dedicated AuthorizeRecognitions grant (Admin excluded, D-05).
        var (failure, personnelFile) = await LoadForAuthorizeRecognitionsAsync<PersonnelFileRecognitionResponse>(
            command.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var isApply = string.Equals(command.Decision, RecognitionDecisions.Apply, StringComparison.OrdinalIgnoreCase);
        if (!isApply && string.IsNullOrWhiteSpace(command.Note))
        {
            return Result<PersonnelFileRecognitionResponse>.Failure(RecognitionErrors.DecisionNoteRequired);
        }

        var entity = await transactionRepository.GetRecognitionEntityAsync(personnelFile!.PublicId, command.RecognitionPublicId, cancellationToken);
        if (entity is null)
        {
            return Result<PersonnelFileRecognitionResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileRecognitionResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // Double anti-self-approval (RN-02): neither the subject employee nor the registrar may decide.
        if (PersonnelTransactionRules.IsSelfDecision(
                personnelFile.LinkedUserPublicId?.ToString(), entity.RegisteredByUserId, currentUserService.UserId ?? string.Empty))
        {
            return Result<PersonnelFileRecognitionResponse>.Failure(RecognitionErrors.SelfApprovalForbidden);
        }

        // Re-verify the record is still EN_REVISION (a concurrent decision would already have moved it — 422).
        if (!PersonnelTransactionStatuses.IsEditable(entity.StatusCode))
        {
            return Result<PersonnelFileRecognitionResponse>.Failure(RecognitionErrors.StateRuleViolation);
        }

        var tenantId = personnelFile.TenantId;

        // On a retired profile only RECHAZAR is allowed; applying (which stamps the file) is blocked (aclaración №10).
        if (isApply && await transactionRepository.IsProfileRetiredAsync(personnelFile.Id, cancellationToken))
        {
            return Result<PersonnelFileRecognitionResponse>.Failure(PersonnelFileErrors.ProfileRetiredLocked);
        }

        var nowUtc = dateTimeProvider.UtcNow;

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            if (isApply)
            {
                var action = PersonnelFilePersonnelAction.Create(
                    RecognitionWriteSupport.ActionTypeCode,
                    RecognitionWriteSupport.AppliedActionStatusCode,
                    actionDateUtc: nowUtc,
                    effectiveFromUtc: entity.EventDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                    effectiveToUtc: null,
                    description: $"RECONOCIMIENTO ({entity.TypeNameSnapshot}) {entity.EventDate:yyyy-MM-dd}.",
                    reference: null,
                    amount: entity.Amount,
                    currencyCode: entity.CurrencyCode,
                    isSystemGenerated: true);
                action.BindToPersonnelFile(personnelFile.Id);
                action.SetTenantId(tenantId);
                _ = await employeeRepository.AddPersonnelActionAsync(action, cancellationToken);

                entity.Apply(currentUserService.UserId ?? string.Empty, nowUtc, action.PublicId);
            }
            else
            {
                entity.Reject(currentUserService.UserId ?? string.Empty, nowUtc, command.Note!);
            }

            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await transactionRepository.GetRecognitionResponseAsync(personnelFile.PublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Recognition response could not be resolved after the decision.");

            var verb = isApply ? "Applied" : "Rejected";
            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"{verb} a recognition for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileRecognitionResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class AnnulPersonnelFileRecognitionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelTransactionRepository transactionRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AnnulPersonnelFileRecognitionCommand, PersonnelFileRecognitionResponse>
{
    public async Task<Result<PersonnelFileRecognitionResponse>> Handle(
        AnnulPersonnelFileRecognitionCommand command,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<PersonnelFileRecognitionResponse>.Failure(AuthorizationErrors.Unauthenticated);
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return Result<PersonnelFileRecognitionResponse>.Failure(RecognitionErrors.AnnulmentReasonRequired);
        }

        var tenantId = tenantContext.TenantId.Value;
        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(command.PersonnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return Result<PersonnelFileRecognitionResponse>.Failure(
                await personnelFileRepository.ExistsOutsideTenantAsync(command.PersonnelFileId, cancellationToken)
                    ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                    : PersonnelFileErrors.NotFound);
        }

        var entity = await transactionRepository.GetRecognitionEntityAsync(personnelFile.PublicId, command.RecognitionPublicId, cancellationToken);
        if (entity is null)
        {
            return Result<PersonnelFileRecognitionResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileRecognitionResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var wasApplied = entity.StatusCode == PersonnelTransactionStatuses.Aplicada;
        if (entity.StatusCode == PersonnelTransactionStatuses.EnRevision)
        {
            // Trámite withdrawal — the manage permission suffices.
            var manage = await authorizationService.EnsureCanManageRecognitionsAsync(tenantId, cancellationToken);
            if (manage.IsFailure)
            {
                return Result<PersonnelFileRecognitionResponse>.Failure(manage.Error);
            }
        }
        else if (wasApplied)
        {
            // Revocation — the dedicated AuthorizeRecognitions grant + the double anti-self check.
            var authorize = await authorizationService.EnsureCanAuthorizeRecognitionsAsync(tenantId, cancellationToken);
            if (authorize.IsFailure)
            {
                return Result<PersonnelFileRecognitionResponse>.Failure(authorize.Error);
            }

            if (PersonnelTransactionRules.IsSelfDecision(
                    personnelFile.LinkedUserPublicId?.ToString(), entity.RegisteredByUserId, currentUserService.UserId ?? string.Empty))
            {
                return Result<PersonnelFileRecognitionResponse>.Failure(RecognitionErrors.SelfApprovalForbidden);
            }
        }
        else
        {
            return Result<PersonnelFileRecognitionResponse>.Failure(RecognitionErrors.StateRuleViolation);
        }

        var nowUtc = dateTimeProvider.UtcNow;

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // Revocation of an applied recognition annuls the linked RECONOCIMIENTO entry in the same tx.
            if (wasApplied && entity.PersonnelActionPublicId is { } actionPublicId)
            {
                var action = await transactionRepository.GetPersonnelActionEntityAsync(personnelFile.Id, actionPublicId, cancellationToken);
                action?.Annul();
            }

            entity.Annul(command.Reason, currentUserService.UserId ?? string.Empty, nowUtc);
            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await transactionRepository.GetRecognitionResponseAsync(personnelFile.PublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Recognition response could not be resolved after annulment.");

            var verb = wasApplied ? "Revoked" : "Annulled";
            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"{verb} a recognition for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileRecognitionResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class GetPersonnelFileRecognitionsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelTransactionRepository transactionRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileRecognitionsQuery, IReadOnlyCollection<PersonnelFileRecognitionResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileRecognitionResponse>>> Handle(
        GetPersonnelFileRecognitionsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile, restrictToApplied) = await LoadCompletedEmployeeForRecognitionReadAsync<IReadOnlyCollection<PersonnelFileRecognitionResponse>>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await transactionRepository.GetRecognitionResponsesAsync(personnelFile!.PublicId, restrictToApplied, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileRecognitionResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileRecognitionByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelTransactionRepository transactionRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileRecognitionByIdQuery, PersonnelFileRecognitionResponse>
{
    public async Task<Result<PersonnelFileRecognitionResponse>> Handle(
        GetPersonnelFileRecognitionByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile, restrictToApplied) = await LoadCompletedEmployeeForRecognitionReadAsync<PersonnelFileRecognitionResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await transactionRepository.GetRecognitionResponseAsync(personnelFile!.PublicId, query.RecognitionPublicId, cancellationToken);

        // The self-service employee only ever sees their APLICADA recognitions (D-13) — a non-applied record
        // is masked as not found rather than leaked.
        if (response is null || (restrictToApplied && response.StatusCode != PersonnelTransactionStatuses.Aplicada))
        {
            return Result<PersonnelFileRecognitionResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        return Result<PersonnelFileRecognitionResponse>.Success(response);
    }
}

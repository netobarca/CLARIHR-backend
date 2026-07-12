using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Application.Features.PersonnelFiles.Compensation;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>Maps a one-time-deduction aggregate to its API response (user ids null-safe).</summary>
public static class OneTimeDeductionMapping
{
    public static OneTimeDeductionResponse ToResponse(PersonnelFileOneTimeDeduction entity) =>
        new(
            entity.PublicId,
            entity.DeductionDate,
            entity.Reference,
            entity.ConceptTypeCode,
            entity.ConceptNameSnapshot,
            entity.Observations,
            entity.IsFixedValue,
            entity.CalculationMethod,
            entity.Quantity,
            entity.UnitValue,
            entity.Multiplier,
            entity.Percentage,
            entity.BaseAmount,
            entity.Amount,
            entity.CurrencyCode,
            entity.AssignedPositionPublicId,
            entity.RequesterFilePublicId,
            entity.RequesterNameSnapshot,
            entity.PayrollTypeCode,
            entity.PayrollPeriodPublicId,
            entity.PayrollPeriodLabel,
            entity.PayrollPeriodEndDate,
            entity.StatusCode,
            NullIfEmpty(entity.RequestedByUserId),
            entity.DecidedByUserId,
            entity.DecidedUtc,
            entity.DecisionNote,
            entity.AnnulledByUserId,
            entity.AnnulledUtc,
            entity.AnnulmentReason,
            entity.AppliedBySettlementPublicId,
            entity.IsActive,
            entity.ConcurrencyToken);

    private static Guid? NullIfEmpty(Guid value) => value == Guid.Empty ? null : value;
}

/// <summary>
/// Cross-aggregate validation + snapshot resolution shared by the one-time-deduction write handlers: the concept
/// is an ACTIVE, NON-STATUTORY <c>Egreso</c> concept (RN-04 — reusing the resolver REQ-008 already built); the
/// VALUE is validated and its amount RECOMPUTED by the pure rules (a declared amount that does not follow from
/// its components is rejected with the expected figure); the payroll type is an active catalog code; the plaza
/// resolves (no cost center — P-08); the requester is a real personnel file of the company (its name is
/// snapshotted).
/// </summary>
internal sealed record OneTimeDeductionResolved(
    string ConceptName,
    decimal Amount,
    string CurrencyCode,
    Guid AssignedPositionPublicId,
    Guid RequesterFilePublicId,
    string RequesterName);

internal static class OneTimeDeductionWriteSupport
{
    public static async Task<Result<OneTimeDeductionResolved>> ResolveAndValidateAsync(
        OneTimeDeductionInput input,
        PersonnelFile personnelFile,
        IPersonnelFileRepository personnelFileRepository,
        IPersonnelFileEmployeeRepository employeeRepository,
        CancellationToken cancellationToken)
    {
        // 1) Concept — ACTIVE, NON-STATUTORY Egreso (RN-04). The resolver is the one REQ-008 built: ISSS/AFP/Renta
        // are payroll law, not one-off charges, and can never back a manual deduction.
        var concept = await personnelFileRepository.GetActiveDeductionConceptAsync(
            personnelFile.TenantId, input.ConceptTypeCode, cancellationToken);
        if (concept is null || string.IsNullOrWhiteSpace(concept.Name))
        {
            return Result<OneTimeDeductionResolved>.Failure(OneTimeDeductionErrors.ConceptInvalid);
        }

        // 2) Value — the SERVER owns the amount of a computed value.
        var valueValidation = OneTimeDeductionRules.ValidateValue(
            input.IsFixedValue,
            input.CalculationMethod,
            input.Quantity,
            input.UnitValue,
            input.Multiplier,
            input.Percentage,
            input.BaseAmount,
            input.Amount);
        if (!valueValidation.IsValid)
        {
            var detail = valueValidation.ExpectedAmount is { } expected
                ? $"The one-time deduction value is not coherent; the expected amount is {expected:0.00}."
                : "The one-time deduction value is not coherent.";
            return Result<OneTimeDeductionResolved>.Failure(
                new Error(valueValidation.ErrorCode!, detail, ErrorType.UnprocessableEntity));
        }

        // 3) Payroll type.
        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
                personnelFile.TenantId, PersonnelCurriculumCatalogCategories.PayrollType, input.PayrollTypeCode, cancellationToken))
        {
            return Result<OneTimeDeductionResolved>.Failure(OneTimeDeductionErrors.PayrollTypeInvalid);
        }

        // 4) Plaza (default the principal one). NO cost center (P-08), unlike the one-time income.
        var plaza = await employeeRepository.ResolveOneTimeDeductionPlazaAsync(
            personnelFile.Id, input.AssignedPositionPublicId, cancellationToken);
        if (!plaza.Found)
        {
            return Result<OneTimeDeductionResolved>.Failure(OneTimeDeductionErrors.AssignedPositionInvalid);
        }

        // 5) Requester trío: resolve the requester file's display name snapshot.
        var requester = await employeeRepository.GetOneTimeDeductionRequesterLookupAsync(
            input.RequesterFilePublicId, personnelFile.TenantId, cancellationToken);
        if (requester is null)
        {
            return Result<OneTimeDeductionResolved>.Failure(OneTimeDeductionErrors.RequesterInvalid);
        }

        // 6) Currency — default the company preference when the request omits it.
        var currencyCode = !string.IsNullOrWhiteSpace(input.CurrencyCode)
            ? input.CurrencyCode!.Trim().ToUpperInvariant()
            : (await personnelFileRepository.GetCompanyDefaultCurrencyCodeAsync(personnelFile.TenantId, cancellationToken))?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            currencyCode = "USD";
        }

        return Result<OneTimeDeductionResolved>.Success(new OneTimeDeductionResolved(
            concept.Name,
            valueValidation.Amount!.Value,
            currencyCode,
            plaza.AssignedPositionPublicId,
            requester.FilePublicId,
            requester.FullName));
    }
}

/// <summary>
/// Separation-of-duties checks for the authorizer actions (TRIPLE anti-self): neither the SUBJECT employee (the
/// file's linked login), the REGISTRAR (who created the deduction) nor the REQUESTER (the trío file's linked
/// login) may decide or revoke it. The requester leg needs a database read, so the guard is async; a requester
/// file without a linked login cannot trip it (documented behavior).
/// </summary>
internal static class OneTimeDeductionAuthorizerGuards
{
    public static async Task<Error?> CheckAsync(
        PersonnelFile personnelFile,
        PersonnelFileOneTimeDeduction deduction,
        Guid actingUserId,
        IPersonnelFileEmployeeRepository employeeRepository,
        CancellationToken cancellationToken)
    {
        if (actingUserId == Guid.Empty)
        {
            return null;
        }

        // (a) The subject employee never decides/revokes their own deduction.
        if (personnelFile.LinkedUserPublicId is { } subjectUserId && subjectUserId == actingUserId)
        {
            return OneTimeDeductionErrors.SelfApprovalForbidden;
        }

        // (b) The registrar never decides/revokes the deduction they registered.
        if (deduction.RequestedByUserId == actingUserId)
        {
            return OneTimeDeductionErrors.SelfApprovalForbidden;
        }

        // (c) The requester (the trío file's linked login) never decides/revokes what they asked for.
        var requester = await employeeRepository.GetOneTimeDeductionRequesterLookupAsync(
            deduction.RequesterFilePublicId, personnelFile.TenantId, cancellationToken);
        if (requester?.LinkedUserPublicId is { } requesterUserId && requesterUserId == actingUserId)
        {
            return OneTimeDeductionErrors.SelfApprovalForbidden;
        }

        return null;
    }
}

// ── CRUD (Manage) ───────────────────────────────────────────────────────────────────────────────────

internal sealed class AddPersonnelFileOneTimeDeductionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFileOneTimeDeductionCommand, OneTimeDeductionResponse>
{
    public async Task<Result<OneTimeDeductionResponse>> Handle(
        AddPersonnelFileOneTimeDeductionCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageOneTimeDeductionsAsync<OneTimeDeductionResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<OneTimeDeductionResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        if (await employeeRepository.IsRecurringIncomeProfileRetiredAsync(personnelFile.Id, cancellationToken))
        {
            return Result<OneTimeDeductionResponse>.Failure(PersonnelFileErrors.ProfileRetiredLocked);
        }

        var resolution = await OneTimeDeductionWriteSupport.ResolveAndValidateAsync(
            command.Item, personnelFile, personnelFileRepository, employeeRepository, cancellationToken);
        if (resolution.IsFailure)
        {
            return Result<OneTimeDeductionResponse>.Failure(resolution.Error);
        }

        var resolved = resolution.Value;
        _ = Guid.TryParse(currentUserService.UserId, out var requestedByUserId);

        var entity = PersonnelFileOneTimeDeduction.Create(
            command.Item.DeductionDate,
            command.Item.Reference,
            command.Item.ConceptTypeCode,
            resolved.ConceptName,
            command.Item.Observations,
            command.Item.IsFixedValue,
            command.Item.CalculationMethod,
            command.Item.Quantity,
            command.Item.UnitValue,
            command.Item.Multiplier,
            command.Item.Percentage,
            command.Item.BaseAmount,
            resolved.Amount,
            resolved.CurrencyCode,
            resolved.AssignedPositionPublicId,
            resolved.RequesterFilePublicId,
            resolved.RequesterName,
            command.Item.PayrollTypeCode,
            payrollPeriodId: null,
            command.Item.PayrollPeriodPublicId,
            command.Item.PayrollPeriodLabel,
            command.Item.PayrollPeriodEndDate,
            requestedByUserId);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        var all = await employeeRepository.AddOneTimeDeductionAsync(personnelFile.Id, personnelFile.TenantId, entity, cancellationToken);
        var response = all.SingleOrDefault(item => item.Id == entity.PublicId)
            ?? throw new InvalidOperationException("One-time-deduction response could not be resolved after creation.");
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Registered one-time deduction for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<OneTimeDeductionResponse>.Success(response);
    }
}

internal sealed class UpdatePersonnelFileOneTimeDeductionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileOneTimeDeductionCommand, OneTimeDeductionResponse>
{
    public async Task<Result<OneTimeDeductionResponse>> Handle(
        UpdatePersonnelFileOneTimeDeductionCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageOneTimeDeductionsAsync<OneTimeDeductionResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entity = await employeeRepository.GetOneTimeDeductionEntityAsync(
            personnelFile!.PublicId, command.OneTimeDeductionPublicId, personnelFile.TenantId, cancellationToken);
        if (entity is null)
        {
            return Result<OneTimeDeductionResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OneTimeDeductionResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // Only an EN_REVISION deduction can be edited; pre-check to avoid a domain exception → 500.
        if (entity.StatusCode != OneTimeDeductionStatuses.EnRevision)
        {
            return Result<OneTimeDeductionResponse>.Failure(
                new Error(OneTimeDeductionRules.StateRuleViolationCode, "Only an EN_REVISION one-time deduction can be edited.", ErrorType.UnprocessableEntity));
        }

        var resolution = await OneTimeDeductionWriteSupport.ResolveAndValidateAsync(
            command.Item, personnelFile, personnelFileRepository, employeeRepository, cancellationToken);
        if (resolution.IsFailure)
        {
            return Result<OneTimeDeductionResponse>.Failure(resolution.Error);
        }

        var resolved = resolution.Value;
        entity.Update(
            command.Item.DeductionDate,
            command.Item.Reference,
            command.Item.ConceptTypeCode,
            resolved.ConceptName,
            command.Item.Observations,
            command.Item.IsFixedValue,
            command.Item.CalculationMethod,
            command.Item.Quantity,
            command.Item.UnitValue,
            command.Item.Multiplier,
            command.Item.Percentage,
            command.Item.BaseAmount,
            resolved.Amount,
            resolved.CurrencyCode,
            resolved.AssignedPositionPublicId,
            resolved.RequesterFilePublicId,
            resolved.RequesterName,
            command.Item.PayrollTypeCode,
            payrollPeriodId: null,
            command.Item.PayrollPeriodPublicId,
            command.Item.PayrollPeriodLabel,
            command.Item.PayrollPeriodEndDate);

        var response = OneTimeDeductionMapping.ToResponse(entity);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated one-time deduction for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<OneTimeDeductionResponse>.Success(response);
    }
}

internal sealed class DeletePersonnelFileOneTimeDeductionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeletePersonnelFileOneTimeDeductionCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeletePersonnelFileOneTimeDeductionCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageOneTimeDeductionsAsync<PersonnelFileParentConcurrencyResult>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entity = await employeeRepository.GetOneTimeDeductionEntityAsync(
            personnelFile!.PublicId, command.OneTimeDeductionPublicId, personnelFile.TenantId, cancellationToken);
        if (entity is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (entity.StatusCode != OneTimeDeductionStatuses.EnRevision)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(
                new Error(OneTimeDeductionRules.StateRuleViolationCode, "Only an EN_REVISION draft can be discarded.", ErrorType.UnprocessableEntity));
        }

        entity.Deactivate();
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Deleted one-time deduction draft for {personnelFile.FullName}.", new { command.OneTimeDeductionPublicId }, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileParentConcurrencyResult>.Success(
            new PersonnelFileParentConcurrencyResult(personnelFile.ConcurrencyToken));
    }
}

internal sealed class AnnulPersonnelFileOneTimeDeductionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AnnulPersonnelFileOneTimeDeductionCommand, OneTimeDeductionResponse>
{
    public async Task<Result<OneTimeDeductionResponse>> Handle(
        AnnulPersonnelFileOneTimeDeductionCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageOneTimeDeductionsAsync<OneTimeDeductionResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return Result<OneTimeDeductionResponse>.Failure(OneTimeDeductionErrors.AnnulmentReasonRequired);
        }

        var entity = await employeeRepository.GetOneTimeDeductionEntityAsync(
            personnelFile!.PublicId, command.OneTimeDeductionPublicId, personnelFile.TenantId, cancellationToken);
        if (entity is null)
        {
            return Result<OneTimeDeductionResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OneTimeDeductionResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // The HR (Manage) branch only annuls a draft: revoking an AUTHORIZED deduction is the authorizer's job.
        if (entity.StatusCode != OneTimeDeductionStatuses.EnRevision)
        {
            return Result<OneTimeDeductionResponse>.Failure(
                new Error(OneTimeDeductionRules.StateRuleViolationCode, "Only an EN_REVISION one-time deduction can be annulled here.", ErrorType.UnprocessableEntity));
        }

        _ = Guid.TryParse(currentUserService.UserId, out var byUserId);
        entity.Annul(command.Reason, byUserId, dateTimeProvider.UtcNow);

        var response = OneTimeDeductionMapping.ToResponse(entity);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Annulled one-time deduction for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<OneTimeDeductionResponse>.Success(response);
    }
}

internal sealed class RetargetPersonnelFileOneTimeDeductionPeriodCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<RetargetPersonnelFileOneTimeDeductionPeriodCommand, OneTimeDeductionResponse>
{
    public async Task<Result<OneTimeDeductionResponse>> Handle(
        RetargetPersonnelFileOneTimeDeductionPeriodCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageOneTimeDeductionsAsync<OneTimeDeductionResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entity = await employeeRepository.GetOneTimeDeductionEntityAsync(
            personnelFile!.PublicId, command.OneTimeDeductionPublicId, personnelFile.TenantId, cancellationToken);
        if (entity is null)
        {
            return Result<OneTimeDeductionResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OneTimeDeductionResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // Re-imputation is only legal while AUTORIZADO: an applied deduction is already in a payroll.
        var rule = OneTimeDeductionRules.CanRetarget(entity.StatusCode);
        if (!rule.IsValid)
        {
            return Result<OneTimeDeductionResponse>.Failure(
                new Error(rule.ErrorCode!, "The one-time deduction cannot be re-targeted in its current state.", ErrorType.UnprocessableEntity));
        }

        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
                personnelFile.TenantId, PersonnelCurriculumCatalogCategories.PayrollType, command.Item.PayrollTypeCode, cancellationToken))
        {
            return Result<OneTimeDeductionResponse>.Failure(OneTimeDeductionErrors.PayrollTypeInvalid);
        }

        entity.RetargetPeriod(
            command.Item.PayrollTypeCode,
            payrollPeriodId: null,
            command.Item.PayrollPeriodPublicId,
            command.Item.PayrollPeriodLabel,
            command.Item.PayrollPeriodEndDate,
            dateTimeProvider.UtcNow);

        var response = OneTimeDeductionMapping.ToResponse(entity);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Re-targeted one-time deduction for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<OneTimeDeductionResponse>.Success(response);
    }
}

// ── Resolution (Authorize — TRIPLE anti-self) ────────────────────────────────────────────────────────

internal sealed class ResolvePersonnelFileOneTimeDeductionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<ResolvePersonnelFileOneTimeDeductionCommand, OneTimeDeductionResponse>
{
    public async Task<Result<OneTimeDeductionResponse>> Handle(
        ResolvePersonnelFileOneTimeDeductionCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForAuthorizeOneTimeDeductionsAsync<OneTimeDeductionResponse>(
            command.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var targetStatus = command.TargetStatusCode.Trim().ToUpperInvariant();
        if (targetStatus is not (OneTimeDeductionStatuses.Autorizado or OneTimeDeductionStatuses.Rechazado))
        {
            return Result<OneTimeDeductionResponse>.Failure(OneTimeDeductionErrors.StatusInvalid);
        }

        if (targetStatus == OneTimeDeductionStatuses.Rechazado && string.IsNullOrWhiteSpace(command.Note))
        {
            return Result<OneTimeDeductionResponse>.Failure(OneTimeDeductionErrors.DecisionNoteRequired);
        }

        var entity = await employeeRepository.GetOneTimeDeductionEntityAsync(
            personnelFile!.PublicId, command.OneTimeDeductionPublicId, personnelFile.TenantId, cancellationToken);
        if (entity is null)
        {
            return Result<OneTimeDeductionResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OneTimeDeductionResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // Re-verify EN_REVISION inside the request (the decision is one-shot).
        if (entity.StatusCode != OneTimeDeductionStatuses.EnRevision)
        {
            return Result<OneTimeDeductionResponse>.Failure(
                new Error(OneTimeDeductionRules.StateRuleViolationCode, "Only an EN_REVISION one-time deduction can be resolved.", ErrorType.UnprocessableEntity));
        }

        _ = Guid.TryParse(currentUserService.UserId, out var actingUserId);
        if (await OneTimeDeductionAuthorizerGuards.CheckAsync(personnelFile, entity, actingUserId, employeeRepository, cancellationToken) is { } selfApproval)
        {
            return Result<OneTimeDeductionResponse>.Failure(selfApproval);
        }

        var now = dateTimeProvider.UtcNow;
        if (targetStatus == OneTimeDeductionStatuses.Autorizado)
        {
            entity.Approve(actingUserId, now);
        }
        else
        {
            entity.Reject(actingUserId, now, command.Note!);
        }

        var response = OneTimeDeductionMapping.ToResponse(entity);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Resolved one-time deduction ({targetStatus}) for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<OneTimeDeductionResponse>.Success(response);
    }
}

internal sealed class RevokePersonnelFileOneTimeDeductionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<RevokePersonnelFileOneTimeDeductionCommand, OneTimeDeductionResponse>
{
    public async Task<Result<OneTimeDeductionResponse>> Handle(
        RevokePersonnelFileOneTimeDeductionCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForAuthorizeOneTimeDeductionsAsync<OneTimeDeductionResponse>(
            command.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return Result<OneTimeDeductionResponse>.Failure(OneTimeDeductionErrors.AnnulmentReasonRequired);
        }

        var entity = await employeeRepository.GetOneTimeDeductionEntityAsync(
            personnelFile!.PublicId, command.OneTimeDeductionPublicId, personnelFile.TenantId, cancellationToken);
        if (entity is null)
        {
            return Result<OneTimeDeductionResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OneTimeDeductionResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // Revocation targets an AUTHORIZED deduction (the draft branch is the HR annulment). An APLICADO one must
        // have its application reverted first.
        if (entity.StatusCode != OneTimeDeductionStatuses.Autorizado)
        {
            return Result<OneTimeDeductionResponse>.Failure(
                new Error(OneTimeDeductionRules.StateRuleViolationCode, "Only an AUTORIZADO one-time deduction can be revoked.", ErrorType.UnprocessableEntity));
        }

        _ = Guid.TryParse(currentUserService.UserId, out var actingUserId);
        if (await OneTimeDeductionAuthorizerGuards.CheckAsync(personnelFile, entity, actingUserId, employeeRepository, cancellationToken) is { } selfApproval)
        {
            return Result<OneTimeDeductionResponse>.Failure(selfApproval);
        }

        entity.Annul(command.Reason, actingUserId, dateTimeProvider.UtcNow);

        var response = OneTimeDeductionMapping.ToResponse(entity);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Revoked one-time deduction for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<OneTimeDeductionResponse>.Success(response);
    }
}

// ── Queries (View) ──────────────────────────────────────────────────────────────────────────────────

internal sealed class GetPersonnelFileOneTimeDeductionsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeCommandHandlerBase,
      IQueryHandler<GetPersonnelFileOneTimeDeductionsQuery, IReadOnlyCollection<OneTimeDeductionResponse>>
{
    public async Task<Result<IReadOnlyCollection<OneTimeDeductionResponse>>> Handle(
        GetPersonnelFileOneTimeDeductionsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForOneTimeDeductionReadAsync<IReadOnlyCollection<OneTimeDeductionResponse>>(
            query.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var items = await employeeRepository.GetOneTimeDeductionsAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<OneTimeDeductionResponse>>.Success(items);
    }
}

internal sealed class GetPersonnelFileOneTimeDeductionByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeCommandHandlerBase,
      IQueryHandler<GetPersonnelFileOneTimeDeductionByIdQuery, OneTimeDeductionResponse>
{
    public async Task<Result<OneTimeDeductionResponse>> Handle(
        GetPersonnelFileOneTimeDeductionByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForOneTimeDeductionReadAsync<OneTimeDeductionResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var item = await employeeRepository.GetOneTimeDeductionAsync(
            personnelFile!.PublicId, query.OneTimeDeductionPublicId, cancellationToken);

        return item is null
            ? Result<OneTimeDeductionResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<OneTimeDeductionResponse>.Success(item);
    }
}

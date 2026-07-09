using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Application.Features.PersonnelFiles.Compensation;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>Maps a one-time-income aggregate to its API response (user ids null-safe — a non-Guid principal → null).</summary>
public static class OneTimeIncomeMapping
{
    public static OneTimeIncomeResponse ToResponse(PersonnelFileOneTimeIncome entity) =>
        new(
            entity.PublicId,
            entity.IncomeDate,
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
            entity.CostCenterPublicId,
            entity.CostCenterNameSnapshot,
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
/// Cross-aggregate validation + snapshot resolution shared by the one-time-income write handlers (database-backed,
/// so it lives outside the pure <c>OneTimeIncomeRules</c>): the income date ≤ today (RN-09); the compensation
/// concept is an active income concept (Nature = Ingreso, not base salary — D-03) whose name is snapshotted; the
/// value coherence is checked + the amount resolved (fixed or computed — №11); the payroll type is an active
/// catalog code; the plaza resolves (P-15 — its cost center is derived and snapshotted, plaza without a cost
/// center → 422); the requester file resolves (the trío name snapshot, №10); the currency defaults to the company
/// preference when omitted.
/// </summary>
internal sealed record OneTimeIncomeResolved(
    string ConceptName,
    decimal Amount,
    string CurrencyCode,
    Guid AssignedPositionPublicId,
    Guid CostCenterPublicId,
    string CostCenterName,
    Guid RequesterFilePublicId,
    string RequesterName);

internal static class OneTimeIncomeWriteSupport
{
    public static async Task<Result<OneTimeIncomeResolved>> ResolveAndValidateAsync(
        OneTimeIncomeInput input,
        PersonnelFile personnelFile,
        IPersonnelFileRepository personnelFileRepository,
        IPersonnelFileEmployeeRepository employeeRepository,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        if (input.IncomeDate > today)
        {
            return Result<OneTimeIncomeResolved>.Failure(OneTimeIncomeErrors.IncomeDateInFuture);
        }

        // 1) Compensation concept — active income concept (Nature = Ingreso, not base salary); snapshot its name.
        var concept = await personnelFileRepository.GetOneTimeIncomeConceptAsync(
            personnelFile.TenantId, input.ConceptTypeCode, cancellationToken);
        if (concept is null || !concept.IsActive)
        {
            return Result<OneTimeIncomeResolved>.Failure(OneTimeIncomeErrors.ConceptInvalid);
        }

        var conceptRule = OneTimeIncomeRules.ValidateConcept(concept.Nature, concept.IsBaseSalary);
        if (!conceptRule.IsValid)
        {
            return Result<OneTimeIncomeResolved>.Failure(
                new Error(conceptRule.ErrorCode!, "The compensation concept is not valid for a one-time income.", ErrorType.UnprocessableEntity));
        }

        // 2) Value coherence (D-07, №11): fixed vs computed; resolve the amount (server is the source of truth).
        var valueValidation = OneTimeIncomeRules.ValidateValue(
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
            return Result<OneTimeIncomeResolved>.Failure(
                new Error(valueValidation.ErrorCode!, "The one-time income value is not coherent.", ErrorType.UnprocessableEntity));
        }

        // 3) Payroll type (REQ-004 catalog).
        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
                personnelFile.TenantId, PersonnelCurriculumCatalogCategories.PayrollType, input.PayrollTypeCode, cancellationToken))
        {
            return Result<OneTimeIncomeResolved>.Failure(OneTimeIncomeErrors.PayrollTypeInvalid);
        }

        // 4) Plaza + cost center (P-15): default the principal plaza; derive + snapshot the cost center.
        var plaza = await employeeRepository.ResolveOneTimeIncomePlazaAsync(
            personnelFile.Id, input.AssignedPositionPublicId, cancellationToken);
        if (!plaza.Found)
        {
            return Result<OneTimeIncomeResolved>.Failure(OneTimeIncomeErrors.AssignedPositionInvalid);
        }

        if (plaza.CostCenterPublicId is not { } costCenterPublicId || costCenterPublicId == Guid.Empty)
        {
            return Result<OneTimeIncomeResolved>.Failure(OneTimeIncomeErrors.CostCenterMissing);
        }

        // 5) Requester trío (№10): resolve the requester file's display name snapshot.
        var requester = await employeeRepository.GetOneTimeIncomeRequesterLookupAsync(
            input.RequesterFilePublicId, personnelFile.TenantId, cancellationToken);
        if (requester is null)
        {
            return Result<OneTimeIncomeResolved>.Failure(OneTimeIncomeErrors.RequesterInvalid);
        }

        // 6) Currency — default the company preference when the request omits it.
        var currencyCode = !string.IsNullOrWhiteSpace(input.CurrencyCode)
            ? input.CurrencyCode!.Trim().ToUpperInvariant()
            : (await personnelFileRepository.GetCompanyDefaultCurrencyCodeAsync(personnelFile.TenantId, cancellationToken))?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(currencyCode))
        {
            currencyCode = "USD";
        }

        return Result<OneTimeIncomeResolved>.Success(new OneTimeIncomeResolved(
            concept.Name,
            valueValidation.ExpectedAmount!.Value,
            currencyCode,
            plaza.AssignedPositionPublicId,
            costCenterPublicId,
            plaza.CostCenterName ?? string.Empty,
            requester.FilePublicId,
            requester.FullName));
    }
}

/// <summary>
/// Separation-of-duties checks for the authorizer actions (aclaración №6, TRIPLE anti-self): neither the SUBJECT
/// employee (the file's linked login), the REGISTRAR (who created the income) nor the REQUESTER (the trío file's
/// linked login) may decide or revoke it. The requester pata (c) needs a database read, so it is async; a
/// requester file without a linked login cannot trip it (documented behavior).
/// </summary>
internal static class OneTimeIncomeAuthorizerGuards
{
    public static async Task<Error?> CheckAsync(
        PersonnelFile personnelFile,
        PersonnelFileOneTimeIncome income,
        Guid actingUserId,
        IPersonnelFileEmployeeRepository employeeRepository,
        CancellationToken cancellationToken)
    {
        if (actingUserId == Guid.Empty)
        {
            return null;
        }

        // (a) The subject employee never decides/revokes their own income.
        if (personnelFile.LinkedUserPublicId is { } subjectUserId && subjectUserId == actingUserId)
        {
            return OneTimeIncomeErrors.SelfApprovalForbidden;
        }

        // (b) The registrar never decides/revokes the income they registered.
        if (income.RequestedByUserId == actingUserId)
        {
            return OneTimeIncomeErrors.SelfApprovalForbidden;
        }

        // (c) The requester (the trío file's linked login) never decides/revokes the income they asked for.
        var requester = await employeeRepository.GetOneTimeIncomeRequesterLookupAsync(
            income.RequesterFilePublicId, personnelFile.TenantId, cancellationToken);
        if (requester?.LinkedUserPublicId is { } requesterUserId && requesterUserId == actingUserId)
        {
            return OneTimeIncomeErrors.SelfApprovalForbidden;
        }

        return null;
    }
}

// ── CRUD (Manage) ───────────────────────────────────────────────────────────────────────────────────

internal sealed class AddPersonnelFileOneTimeIncomeCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFileOneTimeIncomeCommand, OneTimeIncomeResponse>
{
    public async Task<Result<OneTimeIncomeResponse>> Handle(
        AddPersonnelFileOneTimeIncomeCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageOneTimeIncomesAsync<OneTimeIncomeResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<OneTimeIncomeResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        if (await employeeRepository.IsOneTimeIncomeProfileRetiredAsync(personnelFile.Id, cancellationToken))
        {
            return Result<OneTimeIncomeResponse>.Failure(PersonnelFileErrors.ProfileRetiredLocked);
        }

        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow);
        var resolution = await OneTimeIncomeWriteSupport.ResolveAndValidateAsync(
            command.Item, personnelFile, personnelFileRepository, employeeRepository, today, cancellationToken);
        if (resolution.IsFailure)
        {
            return Result<OneTimeIncomeResponse>.Failure(resolution.Error);
        }

        var resolved = resolution.Value;
        _ = Guid.TryParse(currentUserService.UserId, out var requestedByUserId);

        var entity = PersonnelFileOneTimeIncome.Create(
            command.Item.IncomeDate,
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
            resolved.CostCenterPublicId,
            resolved.CostCenterName,
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

        var all = await employeeRepository.AddOneTimeIncomeAsync(personnelFile.Id, personnelFile.TenantId, entity, cancellationToken);
        var response = all.SingleOrDefault(item => item.Id == entity.PublicId)
            ?? throw new InvalidOperationException("One-time-income response could not be resolved after creation.");
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Registered one-time income for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<OneTimeIncomeResponse>.Success(response);
    }
}

internal sealed class UpdatePersonnelFileOneTimeIncomeCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileOneTimeIncomeCommand, OneTimeIncomeResponse>
{
    public async Task<Result<OneTimeIncomeResponse>> Handle(
        UpdatePersonnelFileOneTimeIncomeCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageOneTimeIncomesAsync<OneTimeIncomeResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<OneTimeIncomeResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        if (await employeeRepository.IsOneTimeIncomeProfileRetiredAsync(personnelFile.Id, cancellationToken))
        {
            return Result<OneTimeIncomeResponse>.Failure(PersonnelFileErrors.ProfileRetiredLocked);
        }

        var entity = await employeeRepository.GetOneTimeIncomeEntityAsync(
            personnelFile.PublicId, command.OneTimeIncomePublicId, personnelFile.TenantId, cancellationToken);
        if (entity is null)
        {
            return Result<OneTimeIncomeResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OneTimeIncomeResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // Only an EN_REVISION income can be edited (RN-02); pre-check to avoid a domain exception → 500.
        if (entity.StatusCode != OneTimeIncomeStatuses.EnRevision)
        {
            return Result<OneTimeIncomeResponse>.Failure(OneTimeIncomeErrors.StateRuleViolation);
        }

        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow);
        var resolution = await OneTimeIncomeWriteSupport.ResolveAndValidateAsync(
            command.Item, personnelFile, personnelFileRepository, employeeRepository, today, cancellationToken);
        if (resolution.IsFailure)
        {
            return Result<OneTimeIncomeResponse>.Failure(resolution.Error);
        }

        var resolved = resolution.Value;
        entity.Update(
            command.Item.IncomeDate,
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
            resolved.CostCenterPublicId,
            resolved.CostCenterName,
            resolved.RequesterFilePublicId,
            resolved.RequesterName,
            command.Item.PayrollTypeCode,
            payrollPeriodId: null,
            command.Item.PayrollPeriodPublicId,
            command.Item.PayrollPeriodLabel,
            command.Item.PayrollPeriodEndDate);

        var response = OneTimeIncomeMapping.ToResponse(entity);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated one-time income for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<OneTimeIncomeResponse>.Success(response);
    }
}

internal sealed class DeletePersonnelFileOneTimeIncomeCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeletePersonnelFileOneTimeIncomeCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeletePersonnelFileOneTimeIncomeCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageOneTimeIncomesAsync<PersonnelFileParentConcurrencyResult>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var entity = await employeeRepository.GetOneTimeIncomeEntityAsync(
            personnelFile.PublicId, command.OneTimeIncomePublicId, personnelFile.TenantId, cancellationToken);
        if (entity is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // Only an EN_REVISION draft can be discarded (soft delete); an authorized income is revoked/annulled.
        if (entity.StatusCode != OneTimeIncomeStatuses.EnRevision)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(OneTimeIncomeErrors.StateRuleViolation);
        }

        entity.Deactivate();
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Deactivated one-time income draft for {personnelFile.FullName}.", OneTimeIncomeMapping.ToResponse(entity), cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileParentConcurrencyResult>.Success(new PersonnelFileParentConcurrencyResult(personnelFile.ConcurrencyToken));
    }
}

// ── Lifecycle (Manage): annulment / re-imputation ──────────────────────────────────────────────────────

internal sealed class AnnulPersonnelFileOneTimeIncomeCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AnnulPersonnelFileOneTimeIncomeCommand, OneTimeIncomeResponse>
{
    public async Task<Result<OneTimeIncomeResponse>> Handle(
        AnnulPersonnelFileOneTimeIncomeCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, entity, personnelFile) = await OneTimeIncomeManageLoad.LoadAsync(
            authorizationService, personnelFileRepository, employeeRepository, tenantContext,
            command.PersonnelFileId, command.OneTimeIncomePublicId, command.ConcurrencyToken, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return Result<OneTimeIncomeResponse>.Failure(OneTimeIncomeErrors.AnnulmentReasonRequired);
        }

        // Manage annulment (retiro) operates on an EN_REVISION income; an AUTORIZADO one is revoked by the authorizer.
        if (entity!.StatusCode != OneTimeIncomeStatuses.EnRevision)
        {
            return Result<OneTimeIncomeResponse>.Failure(OneTimeIncomeErrors.StateRuleViolation);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var byUserId);
        entity.Annul(command.Reason, byUserId, dateTimeProvider.UtcNow);
        TouchPersonnelFile(personnelFile!);

        return await OneTimeIncomeManageLoad.FinishAsync(entity, personnelFile!, auditService, unitOfWork, "Annulled", cancellationToken);
    }
}

internal sealed class RetargetPersonnelFileOneTimeIncomePeriodCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<RetargetPersonnelFileOneTimeIncomePeriodCommand, OneTimeIncomeResponse>
{
    public async Task<Result<OneTimeIncomeResponse>> Handle(
        RetargetPersonnelFileOneTimeIncomePeriodCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, entity, personnelFile) = await OneTimeIncomeManageLoad.LoadAsync(
            authorizationService, personnelFileRepository, employeeRepository, tenantContext,
            command.PersonnelFileId, command.OneTimeIncomePublicId, command.ConcurrencyToken, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        // Re-imputation ("enviar a otro periodo", RF-005) only from AUTORIZADO; pre-check to avoid a 500.
        if (entity!.StatusCode != OneTimeIncomeStatuses.Autorizado)
        {
            return Result<OneTimeIncomeResponse>.Failure(OneTimeIncomeErrors.NotRetargetable);
        }

        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
                personnelFile!.TenantId, PersonnelCurriculumCatalogCategories.PayrollType, command.Period.PayrollTypeCode, cancellationToken))
        {
            return Result<OneTimeIncomeResponse>.Failure(OneTimeIncomeErrors.PayrollTypeInvalid);
        }

        // The payroll-period FK resolution is deferred to PR-4 (§0.13); PR-3 re-targets in the degraded mode
        // (label + optional public id + optional end date, no hard FK).
        entity.RetargetPeriod(
            command.Period.PayrollTypeCode,
            payrollPeriodId: null,
            command.Period.PayrollPeriodPublicId,
            command.Period.PayrollPeriodLabel,
            command.Period.PayrollPeriodEndDate,
            dateTimeProvider.UtcNow);
        TouchPersonnelFile(personnelFile);

        return await OneTimeIncomeManageLoad.FinishAsync(entity, personnelFile, auditService, unitOfWork, "Re-targeted", cancellationToken);
    }
}

/// <summary>Shared load + finish glue for the Manage lifecycle handlers (annulment / re-imputation).</summary>
internal static class OneTimeIncomeManageLoad
{
    public static async Task<(Result<OneTimeIncomeResponse>? Failure, PersonnelFileOneTimeIncome? Entity, PersonnelFile? File)> LoadAsync(
        IPersonnelFileAuthorizationService authorizationService,
        IPersonnelFileRepository personnelFileRepository,
        IPersonnelFileEmployeeRepository employeeRepository,
        ITenantContext tenantContext,
        Guid personnelFileId,
        Guid oneTimeIncomePublicId,
        Guid concurrencyToken,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<OneTimeIncomeResponse>.Failure(AuthorizationErrors.Unauthenticated), null, null);
        }

        var authorizationResult = await authorizationService.EnsureCanManageOneTimeIncomesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return (Result<OneTimeIncomeResponse>.Failure(authorizationResult.Error), null, null);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<OneTimeIncomeResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                        : PersonnelFileErrors.NotFound),
                null, null);
        }

        var entity = await employeeRepository.GetOneTimeIncomeEntityAsync(
            personnelFile.PublicId, oneTimeIncomePublicId, personnelFile.TenantId, cancellationToken);
        if (entity is null)
        {
            return (Result<OneTimeIncomeResponse>.Failure(PersonnelFileErrors.ItemNotFound), null, null);
        }

        if (entity.ConcurrencyToken != concurrencyToken)
        {
            return (Result<OneTimeIncomeResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict), null, null);
        }

        return (null, entity, personnelFile);
    }

    // The caller (a command-handler subclass) touches the personnel file before calling this so its concurrency
    // token rotates with the sub-record write; this shared glue only persists + audits inside the transaction.
    public static async Task<Result<OneTimeIncomeResponse>> FinishAsync(
        PersonnelFileOneTimeIncome entity,
        PersonnelFile personnelFile,
        IAuditService auditService,
        IUnitOfWork unitOfWork,
        string verb,
        CancellationToken cancellationToken)
    {
        var response = OneTimeIncomeMapping.ToResponse(entity);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"{verb} one-time income for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<OneTimeIncomeResponse>.Success(response);
    }
}

// ── Resolution / revocation (Authorize — TRIPLE anti-self) ─────────────────────────────────────────────

internal sealed class ResolvePersonnelFileOneTimeIncomeCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<ResolvePersonnelFileOneTimeIncomeCommand, OneTimeIncomeResponse>
{
    public async Task<Result<OneTimeIncomeResponse>> Handle(
        ResolvePersonnelFileOneTimeIncomeCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForAuthorizeOneTimeIncomesAsync<OneTimeIncomeResponse>(
            command.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entity = await employeeRepository.GetOneTimeIncomeEntityAsync(
            personnelFile!.PublicId, command.OneTimeIncomePublicId, personnelFile.TenantId, cancellationToken);
        if (entity is null)
        {
            return Result<OneTimeIncomeResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OneTimeIncomeResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // Re-verify the state inside the request (a second concurrent decision holds a stale token → 409 above,
        // or a non-EN_REVISION state here → 422).
        if (entity.StatusCode != OneTimeIncomeStatuses.EnRevision)
        {
            return Result<OneTimeIncomeResponse>.Failure(OneTimeIncomeErrors.StateRuleViolation);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var decidedByUserId);
        if (await OneTimeIncomeAuthorizerGuards.CheckAsync(personnelFile, entity, decidedByUserId, employeeRepository, cancellationToken) is { } selfError)
        {
            return Result<OneTimeIncomeResponse>.Failure(selfError);
        }

        var target = command.TargetStatusCode.Trim().ToUpperInvariant();
        var isAuthorize = target == OneTimeIncomeStatuses.Autorizado;
        var isReject = target == OneTimeIncomeStatuses.Rechazado;
        if ((!isAuthorize && !isReject)
            || !await personnelFileRepository.CatalogCodeIsActiveAsync(
                personnelFile.TenantId, PersonnelCurriculumCatalogCategories.OneTimeIncomeStatus, target, cancellationToken))
        {
            return Result<OneTimeIncomeResponse>.Failure(OneTimeIncomeErrors.StatusInvalid);
        }

        var now = dateTimeProvider.UtcNow;
        string verb;
        if (isAuthorize)
        {
            entity.Approve(decidedByUserId, now);
            verb = "Authorized";
        }
        else
        {
            if (string.IsNullOrWhiteSpace(command.Note))
            {
                return Result<OneTimeIncomeResponse>.Failure(OneTimeIncomeErrors.DecisionNoteRequired);
            }

            entity.Reject(decidedByUserId, now, command.Note);
            verb = "Rejected";
        }

        TouchPersonnelFile(personnelFile);
        return await OneTimeIncomeManageLoad.FinishAsync(entity, personnelFile, auditService, unitOfWork, verb, cancellationToken);
    }
}

internal sealed class RevokePersonnelFileOneTimeIncomeCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<RevokePersonnelFileOneTimeIncomeCommand, OneTimeIncomeResponse>
{
    public async Task<Result<OneTimeIncomeResponse>> Handle(
        RevokePersonnelFileOneTimeIncomeCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForAuthorizeOneTimeIncomesAsync<OneTimeIncomeResponse>(
            command.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entity = await employeeRepository.GetOneTimeIncomeEntityAsync(
            personnelFile!.PublicId, command.OneTimeIncomePublicId, personnelFile.TenantId, cancellationToken);
        if (entity is null)
        {
            return Result<OneTimeIncomeResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OneTimeIncomeResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // Revocation applies to an AUTORIZADO income only.
        if (entity.StatusCode != OneTimeIncomeStatuses.Autorizado)
        {
            return Result<OneTimeIncomeResponse>.Failure(OneTimeIncomeErrors.StateRuleViolation);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var byUserId);
        if (await OneTimeIncomeAuthorizerGuards.CheckAsync(personnelFile, entity, byUserId, employeeRepository, cancellationToken) is { } selfError)
        {
            return Result<OneTimeIncomeResponse>.Failure(selfError);
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return Result<OneTimeIncomeResponse>.Failure(OneTimeIncomeErrors.AnnulmentReasonRequired);
        }

        entity.Annul(command.Reason, byUserId, dateTimeProvider.UtcNow);
        TouchPersonnelFile(personnelFile);

        return await OneTimeIncomeManageLoad.FinishAsync(entity, personnelFile, auditService, unitOfWork, "Revoked", cancellationToken);
    }
}

// ── Reads (View — no self-service, P-10) ───────────────────────────────────────────────────────────────

internal sealed class GetPersonnelFileOneTimeIncomesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileOneTimeIncomesQuery, IReadOnlyCollection<OneTimeIncomeResponse>>
{
    public async Task<Result<IReadOnlyCollection<OneTimeIncomeResponse>>> Handle(
        GetPersonnelFileOneTimeIncomesQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForOneTimeIncomeReadAsync<IReadOnlyCollection<OneTimeIncomeResponse>>(
            query.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetOneTimeIncomesAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<OneTimeIncomeResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileOneTimeIncomeByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileOneTimeIncomeByIdQuery, OneTimeIncomeResponse>
{
    public async Task<Result<OneTimeIncomeResponse>> Handle(
        GetPersonnelFileOneTimeIncomeByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForOneTimeIncomeReadAsync<OneTimeIncomeResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetOneTimeIncomeAsync(personnelFile!.PublicId, query.OneTimeIncomePublicId, cancellationToken);
        return response is null
            ? Result<OneTimeIncomeResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<OneTimeIncomeResponse>.Success(response);
    }
}

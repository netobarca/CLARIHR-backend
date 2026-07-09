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

/// <summary>Maps a recurring-income aggregate to its API response (user ids null-safe — a non-Guid principal → null).</summary>
public static class RecurringIncomeMapping
{
    public static RecurringIncomeResponse ToResponse(PersonnelFileRecurringIncome entity) =>
        new(
            entity.PublicId,
            entity.RegistrationDate,
            entity.Reference,
            entity.RecurringIncomeTypeCode,
            entity.ConceptTypeCode,
            entity.ConceptNameSnapshot,
            entity.Observations,
            entity.AssignedPositionPublicId,
            entity.CostCenterPublicId,
            entity.CostCenterNameSnapshot,
            entity.InstallmentStartDate,
            entity.CurrencyCode,
            entity.PayrollTypeCode,
            entity.InstallmentFrequencyCode,
            entity.IsIndefinite,
            entity.InstallmentValue,
            entity.InstallmentCount,
            entity.TotalAmount,
            entity.SettlementActionCode,
            entity.StatusCode,
            NullIfEmpty(entity.RegisteredByUserId),
            entity.DecidedByUserId,
            entity.DecidedUtc,
            entity.DecisionNote,
            entity.SuspendedUtc,
            entity.SuspensionNote,
            entity.ClosedUtc,
            entity.ClosureReason,
            entity.ClosedByUserId,
            entity.IsActive,
            entity.ConcurrencyToken);

    private static Guid? NullIfEmpty(Guid value) => value == Guid.Empty ? null : value;
}

/// <summary>
/// Cross-aggregate validation + snapshot resolution shared by the recurring-income write handlers (database-backed,
/// so it lives outside the pure <c>RecurringIncomeRules</c>): the registration date ≤ today; the income type,
/// payroll type and installment frequency are active catalog codes; the concept is an active income concept
/// (Nature = Ingreso) whose name is snapshotted; the plaza resolves (P-15 — its cost center is derived and
/// snapshotted, plaza without a cost center → 422); the plan is normalized; the settlement action is coherent
/// with the plan (PAGAR_SALDO × indefinite → 422).
/// </summary>
internal sealed record RecurringIncomeResolved(
    string ConceptName,
    Guid AssignedPositionPublicId,
    Guid CostCenterPublicId,
    string CostCenterName,
    RecurringIncomePlan Plan,
    string CurrencyCode);

internal static class RecurringIncomeWriteSupport
{
    public static async Task<Result<RecurringIncomeResolved>> ResolveAndValidateAsync(
        RecurringIncomeInput input,
        PersonnelFile personnelFile,
        IPersonnelFileRepository personnelFileRepository,
        IPersonnelFileEmployeeRepository employeeRepository,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        if (input.RegistrationDate > today)
        {
            return Result<RecurringIncomeResolved>.Failure(RecurringIncomeErrors.RegistrationDateInFuture);
        }

        // 1) Income type (P-02 catalog).
        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
                personnelFile.TenantId, PersonnelCurriculumCatalogCategories.RecurringIncomeType, input.RecurringIncomeTypeCode, cancellationToken))
        {
            return Result<RecurringIncomeResolved>.Failure(RecurringIncomeErrors.TypeInvalid);
        }

        // 2) Compensation concept — active income concept (Nature = Ingreso); snapshot its name.
        var conceptName = await personnelFileRepository.GetActiveIncomeConceptNameAsync(
            personnelFile.TenantId, input.ConceptTypeCode, cancellationToken);
        if (string.IsNullOrWhiteSpace(conceptName))
        {
            return Result<RecurringIncomeResolved>.Failure(RecurringIncomeErrors.ConceptInvalid);
        }

        // 3) Payroll type (REQ-004 catalog).
        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
                personnelFile.TenantId, PersonnelCurriculumCatalogCategories.PayrollType, input.PayrollTypeCode, cancellationToken))
        {
            return Result<RecurringIncomeResolved>.Failure(RecurringIncomeErrors.PayrollTypeInvalid);
        }

        // 4) Installment frequency (PAY_PERIOD_CATALOG).
        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
                personnelFile.TenantId, PersonnelCurriculumCatalogCategories.PayPeriod, input.InstallmentFrequencyCode, cancellationToken))
        {
            return Result<RecurringIncomeResolved>.Failure(RecurringIncomeErrors.FrequencyInvalid);
        }

        // 5) Plaza + cost center (P-15): default the principal plaza; derive + snapshot the cost center.
        var plaza = await employeeRepository.ResolveRecurringIncomePlazaAsync(
            personnelFile.Id, input.AssignedPositionPublicId, cancellationToken);
        if (!plaza.Found)
        {
            return Result<RecurringIncomeResolved>.Failure(RecurringIncomeErrors.AssignedPositionInvalid);
        }

        if (plaza.CostCenterPublicId is not { } costCenterPublicId || costCenterPublicId == Guid.Empty)
        {
            return Result<RecurringIncomeResolved>.Failure(RecurringIncomeErrors.CostCenterMissing);
        }

        // 6) Plan coherence (RN-05) — the granular plan code is bilingual (PR-2).
        var normalization = RecurringIncomeRules.NormalizePlan(
            input.InstallmentValue, input.InstallmentCount, input.TotalAmount, input.IsIndefinite);
        if (!normalization.IsValid || normalization.Plan is null)
        {
            return Result<RecurringIncomeResolved>.Failure(
                new Error(normalization.ErrorCode!, "The recurring-income plan is not coherent.", ErrorType.UnprocessableEntity));
        }

        // 7) Settlement action coherence (P-06): PAGAR_SALDO is meaningless for an indefinite plan.
        var settlementRule = RecurringIncomeRules.ValidateSettlementAction(input.SettlementActionCode, input.IsIndefinite);
        if (!settlementRule.IsValid)
        {
            return Result<RecurringIncomeResolved>.Failure(
                new Error(settlementRule.ErrorCode!, "The settlement action is not coherent with the plan.", ErrorType.UnprocessableEntity));
        }

        return Result<RecurringIncomeResolved>.Success(new RecurringIncomeResolved(
            conceptName,
            plaza.AssignedPositionPublicId,
            costCenterPublicId,
            plaza.CostCenterName ?? string.Empty,
            normalization.Plan,
            input.CurrencyCode.Trim().ToUpperInvariant()));
    }
}

/// <summary>
/// Separation-of-duties checks for the authorizer actions (aclaración №6, double anti-self): neither the SUBJECT
/// employee (the file's linked login) nor the REGISTRAR (who created the income) may decide or revoke it.
/// </summary>
internal static class RecurringIncomeAuthorizerGuards
{
    public static Error? Check(PersonnelFile personnelFile, PersonnelFileRecurringIncome income, Guid actingUserId)
    {
        if (actingUserId != Guid.Empty
            && ((personnelFile.LinkedUserPublicId is { } subjectUserId && subjectUserId == actingUserId)
                || income.RegisteredByUserId == actingUserId))
        {
            return RecurringIncomeErrors.SelfApprovalForbidden;
        }

        return null;
    }
}

// ── CRUD (Manage) ───────────────────────────────────────────────────────────────────────────────────

internal sealed class AddPersonnelFileRecurringIncomeCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFileRecurringIncomeCommand, RecurringIncomeResponse>
{
    public async Task<Result<RecurringIncomeResponse>> Handle(
        AddPersonnelFileRecurringIncomeCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageRecurringIncomesAsync<RecurringIncomeResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<RecurringIncomeResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        if (await employeeRepository.IsRecurringIncomeProfileRetiredAsync(personnelFile.Id, cancellationToken))
        {
            return Result<RecurringIncomeResponse>.Failure(PersonnelFileErrors.ProfileRetiredLocked);
        }

        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow);
        var resolution = await RecurringIncomeWriteSupport.ResolveAndValidateAsync(
            command.Item, personnelFile, personnelFileRepository, employeeRepository, today, cancellationToken);
        if (resolution.IsFailure)
        {
            return Result<RecurringIncomeResponse>.Failure(resolution.Error);
        }

        var resolved = resolution.Value;
        _ = Guid.TryParse(currentUserService.UserId, out var registeredByUserId);

        var entity = PersonnelFileRecurringIncome.Create(
            command.Item.RegistrationDate,
            command.Item.Reference,
            command.Item.RecurringIncomeTypeCode,
            command.Item.ConceptTypeCode,
            resolved.ConceptName,
            command.Item.Observations,
            resolved.AssignedPositionPublicId,
            resolved.CostCenterPublicId,
            resolved.CostCenterName,
            command.Item.InstallmentStartDate,
            resolved.CurrencyCode,
            command.Item.PayrollTypeCode,
            command.Item.InstallmentFrequencyCode,
            resolved.Plan.IsIndefinite,
            resolved.Plan.InstallmentValue,
            resolved.Plan.InstallmentCount,
            resolved.Plan.TotalAmount,
            command.Item.SettlementActionCode,
            registeredByUserId);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        var all = await employeeRepository.AddRecurringIncomeAsync(personnelFile.Id, personnelFile.TenantId, entity, cancellationToken);
        var response = all.SingleOrDefault(item => item.Id == entity.PublicId)
            ?? throw new InvalidOperationException("Recurring-income response could not be resolved after creation.");
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Registered recurring income for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<RecurringIncomeResponse>.Success(response);
    }
}

internal sealed class UpdatePersonnelFileRecurringIncomeCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileRecurringIncomeCommand, RecurringIncomeResponse>
{
    public async Task<Result<RecurringIncomeResponse>> Handle(
        UpdatePersonnelFileRecurringIncomeCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageRecurringIncomesAsync<RecurringIncomeResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<RecurringIncomeResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        if (await employeeRepository.IsRecurringIncomeProfileRetiredAsync(personnelFile.Id, cancellationToken))
        {
            return Result<RecurringIncomeResponse>.Failure(PersonnelFileErrors.ProfileRetiredLocked);
        }

        var entity = await employeeRepository.GetRecurringIncomeEntityAsync(
            personnelFile.PublicId, command.RecurringIncomePublicId, personnelFile.TenantId, cancellationToken);
        if (entity is null)
        {
            return Result<RecurringIncomeResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<RecurringIncomeResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // Only an EN_REVISION income can be edited (RN-02); pre-check to avoid a domain exception → 500.
        if (entity.StatusCode != RecurringIncomeStatuses.EnRevision)
        {
            return Result<RecurringIncomeResponse>.Failure(RecurringIncomeErrors.StateRuleViolation);
        }

        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow);
        var resolution = await RecurringIncomeWriteSupport.ResolveAndValidateAsync(
            command.Item, personnelFile, personnelFileRepository, employeeRepository, today, cancellationToken);
        if (resolution.IsFailure)
        {
            return Result<RecurringIncomeResponse>.Failure(resolution.Error);
        }

        var resolved = resolution.Value;
        entity.Update(
            command.Item.RegistrationDate,
            command.Item.Reference,
            command.Item.RecurringIncomeTypeCode,
            command.Item.ConceptTypeCode,
            resolved.ConceptName,
            command.Item.Observations,
            resolved.AssignedPositionPublicId,
            resolved.CostCenterPublicId,
            resolved.CostCenterName,
            command.Item.InstallmentStartDate,
            resolved.CurrencyCode,
            command.Item.PayrollTypeCode,
            command.Item.InstallmentFrequencyCode,
            resolved.Plan.IsIndefinite,
            resolved.Plan.InstallmentValue,
            resolved.Plan.InstallmentCount,
            resolved.Plan.TotalAmount,
            command.Item.SettlementActionCode);

        var response = RecurringIncomeMapping.ToResponse(entity);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated recurring income for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<RecurringIncomeResponse>.Success(response);
    }
}

internal sealed class DeletePersonnelFileRecurringIncomeCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeletePersonnelFileRecurringIncomeCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeletePersonnelFileRecurringIncomeCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageRecurringIncomesAsync<PersonnelFileParentConcurrencyResult>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var entity = await employeeRepository.GetRecurringIncomeEntityAsync(
            personnelFile.PublicId, command.RecurringIncomePublicId, personnelFile.TenantId, cancellationToken);
        if (entity is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // Only an EN_REVISION draft can be discarded (soft delete); an authorized income is revoked/closed.
        if (entity.StatusCode != RecurringIncomeStatuses.EnRevision)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(RecurringIncomeErrors.StateRuleViolation);
        }

        entity.Deactivate();
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Deactivated recurring income draft for {personnelFile.FullName}.", RecurringIncomeMapping.ToResponse(entity), cancellationToken);
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

// ── Lifecycle (Manage): suspension / closure / annulment ──────────────────────────────────────────────

internal sealed class SetPersonnelFileRecurringIncomeSuspensionCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<SetPersonnelFileRecurringIncomeSuspensionCommand, RecurringIncomeResponse>
{
    public async Task<Result<RecurringIncomeResponse>> Handle(
        SetPersonnelFileRecurringIncomeSuspensionCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, entity, personnelFile) = await RecurringIncomeManageLoad.LoadAsync(
            authorizationService, personnelFileRepository, employeeRepository, tenantContext,
            command.PersonnelFileId, command.RecurringIncomePublicId, command.ConcurrencyToken, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var now = dateTimeProvider.UtcNow;
        if (command.Suspend)
        {
            if (entity!.StatusCode != RecurringIncomeStatuses.Vigente)
            {
                return Result<RecurringIncomeResponse>.Failure(RecurringIncomeErrors.StateRuleViolation);
            }

            entity.Suspend(command.Note, now);
        }
        else
        {
            if (entity!.StatusCode != RecurringIncomeStatuses.Suspendido)
            {
                return Result<RecurringIncomeResponse>.Failure(RecurringIncomeErrors.StateRuleViolation);
            }

            entity.Resume(now);
        }

        TouchPersonnelFile(personnelFile!);
        return await RecurringIncomeManageLoad.FinishAsync(entity, personnelFile!, auditService, unitOfWork,
            command.Suspend ? "Suspended" : "Resumed", cancellationToken);
    }
}

internal sealed class ClosePersonnelFileRecurringIncomeCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<ClosePersonnelFileRecurringIncomeCommand, RecurringIncomeResponse>
{
    public async Task<Result<RecurringIncomeResponse>> Handle(
        ClosePersonnelFileRecurringIncomeCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, entity, personnelFile) = await RecurringIncomeManageLoad.LoadAsync(
            authorizationService, personnelFileRepository, employeeRepository, tenantContext,
            command.PersonnelFileId, command.RecurringIncomePublicId, command.ConcurrencyToken, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return Result<RecurringIncomeResponse>.Failure(RecurringIncomeErrors.ClosureReasonRequired);
        }

        // Only an INDEFINITE VIGENTE income can be closed manually (P-06); pre-check to avoid a 500.
        if (entity!.StatusCode != RecurringIncomeStatuses.Vigente || !entity.IsIndefinite)
        {
            return Result<RecurringIncomeResponse>.Failure(RecurringIncomeErrors.StateRuleViolation);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var byUserId);
        entity.CloseManually(command.Reason, byUserId, dateTimeProvider.UtcNow);
        TouchPersonnelFile(personnelFile!);

        return await RecurringIncomeManageLoad.FinishAsync(entity, personnelFile!, auditService, unitOfWork, "Closed", cancellationToken);
    }
}

internal sealed class AnnulPersonnelFileRecurringIncomeCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AnnulPersonnelFileRecurringIncomeCommand, RecurringIncomeResponse>
{
    public async Task<Result<RecurringIncomeResponse>> Handle(
        AnnulPersonnelFileRecurringIncomeCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, entity, personnelFile) = await RecurringIncomeManageLoad.LoadAsync(
            authorizationService, personnelFileRepository, employeeRepository, tenantContext,
            command.PersonnelFileId, command.RecurringIncomePublicId, command.ConcurrencyToken, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return Result<RecurringIncomeResponse>.Failure(RecurringIncomeErrors.AnnulmentReasonRequired);
        }

        // Manage annulment operates on an EN_REVISION income; a VIGENTE one is revoked by the authorizer.
        if (entity!.StatusCode != RecurringIncomeStatuses.EnRevision)
        {
            return Result<RecurringIncomeResponse>.Failure(RecurringIncomeErrors.StateRuleViolation);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var byUserId);
        entity.Annul(command.Reason, byUserId, dateTimeProvider.UtcNow);
        TouchPersonnelFile(personnelFile!);

        return await RecurringIncomeManageLoad.FinishAsync(entity, personnelFile!, auditService, unitOfWork, "Annulled", cancellationToken);
    }
}

/// <summary>Shared load + finish glue for the Manage lifecycle handlers (suspension / closure / annulment).</summary>
internal static class RecurringIncomeManageLoad
{
    public static async Task<(Result<RecurringIncomeResponse>? Failure, PersonnelFileRecurringIncome? Entity, PersonnelFile? File)> LoadAsync(
        IPersonnelFileAuthorizationService authorizationService,
        IPersonnelFileRepository personnelFileRepository,
        IPersonnelFileEmployeeRepository employeeRepository,
        ITenantContext tenantContext,
        Guid personnelFileId,
        Guid recurringIncomePublicId,
        Guid concurrencyToken,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return (Result<RecurringIncomeResponse>.Failure(AuthorizationErrors.Unauthenticated), null, null);
        }

        var authorizationResult = await authorizationService.EnsureCanManageRecurringIncomesAsync(tenantContext.TenantId.Value, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return (Result<RecurringIncomeResponse>.Failure(authorizationResult.Error), null, null);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFileId, cancellationToken);
        if (personnelFile is null)
        {
            return (
                Result<RecurringIncomeResponse>.Failure(
                    await personnelFileRepository.ExistsOutsideTenantAsync(personnelFileId, cancellationToken)
                        ? authorizationService.TenantMismatch(RbacPermissionAction.Update)
                        : PersonnelFileErrors.NotFound),
                null, null);
        }

        var entity = await employeeRepository.GetRecurringIncomeEntityAsync(
            personnelFile.PublicId, recurringIncomePublicId, personnelFile.TenantId, cancellationToken);
        if (entity is null)
        {
            return (Result<RecurringIncomeResponse>.Failure(PersonnelFileErrors.ItemNotFound), null, null);
        }

        if (entity.ConcurrencyToken != concurrencyToken)
        {
            return (Result<RecurringIncomeResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict), null, null);
        }

        return (null, entity, personnelFile);
    }

    // The caller (a command-handler subclass) touches the personnel file before calling this so its concurrency
    // token rotates with the sub-record write; this shared glue only persists + audits inside the transaction.
    public static async Task<Result<RecurringIncomeResponse>> FinishAsync(
        PersonnelFileRecurringIncome entity,
        PersonnelFile personnelFile,
        IAuditService auditService,
        IUnitOfWork unitOfWork,
        string verb,
        CancellationToken cancellationToken)
    {
        var response = RecurringIncomeMapping.ToResponse(entity);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"{verb} recurring income for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<RecurringIncomeResponse>.Success(response);
    }
}

// ── Resolution / revocation (Authorize — double anti-self) ─────────────────────────────────────────────

internal sealed class ResolvePersonnelFileRecurringIncomeCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<ResolvePersonnelFileRecurringIncomeCommand, RecurringIncomeResponse>
{
    public async Task<Result<RecurringIncomeResponse>> Handle(
        ResolvePersonnelFileRecurringIncomeCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForAuthorizeRecurringIncomesAsync<RecurringIncomeResponse>(
            command.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entity = await employeeRepository.GetRecurringIncomeEntityAsync(
            personnelFile!.PublicId, command.RecurringIncomePublicId, personnelFile.TenantId, cancellationToken);
        if (entity is null)
        {
            return Result<RecurringIncomeResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<RecurringIncomeResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // Re-verify the state inside the request (a second concurrent decision holds a stale token → 409 above,
        // or a non-EN_REVISION state here → 422).
        if (entity.StatusCode != RecurringIncomeStatuses.EnRevision)
        {
            return Result<RecurringIncomeResponse>.Failure(RecurringIncomeErrors.StateRuleViolation);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var decidedByUserId);
        if (RecurringIncomeAuthorizerGuards.Check(personnelFile, entity, decidedByUserId) is { } selfError)
        {
            return Result<RecurringIncomeResponse>.Failure(selfError);
        }

        var target = command.TargetStatusCode.Trim().ToUpperInvariant();
        var isAuthorize = target == RecurringIncomeStatuses.Vigente;
        var isReject = target == RecurringIncomeStatuses.Rechazado;
        if ((!isAuthorize && !isReject)
            || !await personnelFileRepository.CatalogCodeIsActiveAsync(
                personnelFile.TenantId, PersonnelCurriculumCatalogCategories.RecurringIncomeStatus, target, cancellationToken))
        {
            return Result<RecurringIncomeResponse>.Failure(RecurringIncomeErrors.StatusInvalid);
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
                return Result<RecurringIncomeResponse>.Failure(RecurringIncomeErrors.DecisionNoteRequired);
            }

            entity.Reject(decidedByUserId, now, command.Note);
            verb = "Rejected";
        }

        TouchPersonnelFile(personnelFile);
        return await RecurringIncomeManageLoad.FinishAsync(entity, personnelFile, auditService, unitOfWork, verb, cancellationToken);
    }
}

internal sealed class RevokePersonnelFileRecurringIncomeCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<RevokePersonnelFileRecurringIncomeCommand, RecurringIncomeResponse>
{
    public async Task<Result<RecurringIncomeResponse>> Handle(
        RevokePersonnelFileRecurringIncomeCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForAuthorizeRecurringIncomesAsync<RecurringIncomeResponse>(
            command.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entity = await employeeRepository.GetRecurringIncomeEntityAsync(
            personnelFile!.PublicId, command.RecurringIncomePublicId, personnelFile.TenantId, cancellationToken);
        if (entity is null)
        {
            return Result<RecurringIncomeResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<RecurringIncomeResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // Revocation applies to a VIGENTE income only.
        if (entity.StatusCode != RecurringIncomeStatuses.Vigente)
        {
            return Result<RecurringIncomeResponse>.Failure(RecurringIncomeErrors.StateRuleViolation);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var byUserId);
        if (RecurringIncomeAuthorizerGuards.Check(personnelFile, entity, byUserId) is { } selfError)
        {
            return Result<RecurringIncomeResponse>.Failure(selfError);
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return Result<RecurringIncomeResponse>.Failure(RecurringIncomeErrors.AnnulmentReasonRequired);
        }

        entity.Annul(command.Reason, byUserId, dateTimeProvider.UtcNow);
        TouchPersonnelFile(personnelFile);

        return await RecurringIncomeManageLoad.FinishAsync(entity, personnelFile, auditService, unitOfWork, "Revoked", cancellationToken);
    }
}

// ── Reads (View — no self-service, P-11) ───────────────────────────────────────────────────────────────

internal sealed class GetPersonnelFileRecurringIncomesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileRecurringIncomesQuery, IReadOnlyCollection<RecurringIncomeResponse>>
{
    public async Task<Result<IReadOnlyCollection<RecurringIncomeResponse>>> Handle(
        GetPersonnelFileRecurringIncomesQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForRecurringIncomeReadAsync<IReadOnlyCollection<RecurringIncomeResponse>>(
            query.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetRecurringIncomesAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<RecurringIncomeResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileRecurringIncomeByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileRecurringIncomeByIdQuery, RecurringIncomeResponse>
{
    public async Task<Result<RecurringIncomeResponse>> Handle(
        GetPersonnelFileRecurringIncomeByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForRecurringIncomeReadAsync<RecurringIncomeResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetRecurringIncomeAsync(personnelFile!.PublicId, query.RecurringIncomePublicId, cancellationToken);
        return response is null
            ? Result<RecurringIncomeResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<RecurringIncomeResponse>.Success(response);
    }
}

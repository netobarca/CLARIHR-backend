using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Application.Features.PersonnelFiles.Compensation;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>Maps an application entity to its API response (user ids null-safe).</summary>
public static class OneTimeDeductionApplicationMapping
{
    public static OneTimeDeductionApplicationResponse ToResponse(PersonnelFileOneTimeDeductionApplication entity) =>
        new(
            entity.PublicId,
            entity.AppliedDate,
            entity.PayrollTypeCode,
            entity.PayrollPeriodPublicId,
            entity.PayrollPeriodLabel,
            entity.OriginCode,
            entity.StatusCode,
            NullIfEmpty(entity.AppliedByUserId),
            entity.SettlementPublicId,
            entity.AnnulmentReason,
            entity.AnnulledByUserId,
            entity.AnnulledUtc,
            entity.Notes,
            entity.ConcurrencyToken);

    private static Guid? NullIfEmpty(Guid value) => value == Guid.Empty ? null : value;
}

// ── Read (View — the application history) ───────────────────────────────────────────────────────────

internal sealed class GetOneTimeDeductionApplicationsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeCommandHandlerBase,
      IQueryHandler<GetOneTimeDeductionApplicationsQuery, IReadOnlyCollection<OneTimeDeductionApplicationResponse>>
{
    public async Task<Result<IReadOnlyCollection<OneTimeDeductionApplicationResponse>>> Handle(
        GetOneTimeDeductionApplicationsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForOneTimeDeductionReadAsync<IReadOnlyCollection<OneTimeDeductionApplicationResponse>>(
            query.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var items = await employeeRepository.GetOneTimeDeductionApplicationsAsync(
            personnelFile!.PublicId, query.OneTimeDeductionPublicId, cancellationToken);
        return Result<IReadOnlyCollection<OneTimeDeductionApplicationResponse>>.Success(items);
    }
}

// ── Unitary application (Manage — lock) ──────────────────────────────────────────────────────────────

internal sealed class ApplyOneTimeDeductionApplicationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<ApplyOneTimeDeductionApplicationCommand, OneTimeDeductionApplicationResult>
{
    public async Task<Result<OneTimeDeductionApplicationResult>> Handle(
        ApplyOneTimeDeductionApplicationCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageOneTimeDeductionsAsync<OneTimeDeductionApplicationResult>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        // Pre-check existence + If-Match (AsNoTracking) so we never take the lock for a bad request.
        var snapshot = await employeeRepository.GetOneTimeDeductionAsync(
            personnelFile!.PublicId, command.OneTimeDeductionPublicId, cancellationToken);
        if (snapshot is null)
        {
            return Result<OneTimeDeductionApplicationResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (snapshot.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OneTimeDeductionApplicationResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var period = await ResolvePeriodAsync(
            employeeRepository, personnelFile.TenantId, command.PayrollPeriodPublicId, cancellationToken);
        if (period.Failed)
        {
            return Result<OneTimeDeductionApplicationResult>.Failure(OneTimeDeductionErrors.PayrollPeriodInvalid);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var appliedByUserId);
        var now = dateTimeProvider.UtcNow;
        var appliedDate = command.AppliedDate ?? DateOnly.FromDateTime(now);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // Serialize the at-most-one-active-application rule: take the lock, then load fresh (the first
            // tracking load on this context, so it reflects any committed concurrent application).
            await employeeRepository.AcquireOneTimeDeductionMutationLockAsync(command.OneTimeDeductionPublicId, cancellationToken);
            var deduction = await employeeRepository.GetTrackedOneTimeDeductionWithApplicationsAsync(
                command.OneTimeDeductionPublicId, personnelFile.TenantId, cancellationToken);
            if (deduction is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<OneTimeDeductionApplicationResult>.Failure(PersonnelFileErrors.ItemNotFound);
            }

            if (deduction.ConcurrencyToken != command.ConcurrencyToken)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<OneTimeDeductionApplicationResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
            }

            var canApply = OneTimeDeductionRules.CanApply(deduction.StatusCode, deduction.HasActiveApplication);
            if (!canApply.IsValid)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<OneTimeDeductionApplicationResult>.Failure(
                    new Error(canApply.ErrorCode!, "The one-time deduction cannot be applied.", ErrorType.UnprocessableEntity));
            }

            // The period defaults to the deduction's own target when the request does not override it.
            var periodPublicId = command.PayrollPeriodPublicId ?? deduction.PayrollPeriodPublicId;
            var periodLabel = period.Label ?? deduction.PayrollPeriodLabel;

            var application = deduction.Apply(
                appliedDate,
                deduction.PayrollTypeCode,
                period.InternalId,
                periodPublicId,
                periodLabel,
                OneTimeDeductionApplicationOrigins.Manual,
                appliedByUserId,
                settlementPublicId: null,
                command.Notes);

            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var result = new OneTimeDeductionApplicationResult(
                OneTimeDeductionApplicationMapping.ToResponse(application),
                deduction.StatusCode,
                deduction.ConcurrencyToken);

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile,
                $"Applied one-time deduction for {personnelFile.FullName}.", result, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<OneTimeDeductionApplicationResult>.Success(result);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    internal static async Task<(bool Failed, long? InternalId, string? Label)> ResolvePeriodAsync(
        IPersonnelFileEmployeeRepository employeeRepository,
        Guid tenantId,
        Guid? payrollPeriodPublicId,
        CancellationToken cancellationToken)
    {
        if (payrollPeriodPublicId is not { } publicId || publicId == Guid.Empty)
        {
            return (false, null, null);
        }

        var period = await employeeRepository.ResolveOneTimeDeductionPayrollPeriodAsync(tenantId, publicId, cancellationToken);
        return period is null || !period.IsActive
            ? (true, null, null)
            : (false, period.InternalId, period.Label);
    }
}

// ── The REVERSAL (Manage — lock; the deduction returns to AUTORIZADO) ────────────────────────────────

internal sealed class AnnulOneTimeDeductionApplicationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AnnulOneTimeDeductionApplicationCommand, OneTimeDeductionApplicationResult>
{
    public async Task<Result<OneTimeDeductionApplicationResult>> Handle(
        AnnulOneTimeDeductionApplicationCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageOneTimeDeductionsAsync<OneTimeDeductionApplicationResult>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return Result<OneTimeDeductionApplicationResult>.Failure(OneTimeDeductionApplicationErrors.AnnulmentReasonRequired);
        }

        var snapshot = await employeeRepository.GetOneTimeDeductionAsync(
            personnelFile!.PublicId, command.OneTimeDeductionPublicId, cancellationToken);
        if (snapshot is null)
        {
            return Result<OneTimeDeductionApplicationResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (snapshot.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OneTimeDeductionApplicationResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        var now = dateTimeProvider.UtcNow;
        _ = Guid.TryParse(currentUserService.UserId, out var byUserId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await employeeRepository.AcquireOneTimeDeductionMutationLockAsync(command.OneTimeDeductionPublicId, cancellationToken);
            var deduction = await employeeRepository.GetTrackedOneTimeDeductionWithApplicationsAsync(
                command.OneTimeDeductionPublicId, personnelFile.TenantId, cancellationToken);
            if (deduction is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<OneTimeDeductionApplicationResult>.Failure(PersonnelFileErrors.ItemNotFound);
            }

            if (deduction.ConcurrencyToken != command.ConcurrencyToken)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<OneTimeDeductionApplicationResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
            }

            var canRevert = OneTimeDeductionRules.CanRevertApplication(deduction.StatusCode);
            if (!canRevert.IsValid)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<OneTimeDeductionApplicationResult>.Failure(
                    new Error(canRevert.ErrorCode!, "The application cannot be reverted in the current state.", ErrorType.UnprocessableEntity));
            }

            var target = deduction.Applications.SingleOrDefault(item =>
                item.PublicId == command.ApplicationPublicId
                && item.StatusCode == OneTimeDeductionApplicationStatuses.Aplicada);
            if (target is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<OneTimeDeductionApplicationResult>.Failure(OneTimeDeductionApplicationErrors.ApplicationNotFound);
            }

            // The reversal: the deduction goes back to AUTORIZADO and the filtered-unique index frees the slot,
            // so it can be charged again in another payroll.
            deduction.AnnulApplication(command.ApplicationPublicId, command.Reason, byUserId, now);

            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var result = new OneTimeDeductionApplicationResult(
                OneTimeDeductionApplicationMapping.ToResponse(target),
                deduction.StatusCode,
                deduction.ConcurrencyToken);

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile,
                $"Reverted the one-time-deduction application for {personnelFile.FullName}.", result, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<OneTimeDeductionApplicationResult>.Success(result);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

// ── Pending work list (View — gate per handler) ──────────────────────────────────────────────────────

internal sealed class QueryOneTimeDeductionPendingQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileEmployeeRepository employeeRepository,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<QueryOneTimeDeductionPendingQuery, OneTimeDeductionPendingResponse>
{
    public async Task<Result<OneTimeDeductionPendingResponse>> Handle(
        QueryOneTimeDeductionPendingQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewOneTimeDeductionsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OneTimeDeductionPendingResponse>.Failure(authorizationResult.Error);
        }

        var rows = await employeeRepository.GetOneTimeDeductionPendingAsync(
            query.CompanyId, query.PayrollTypeCode, query.PayrollPeriodPublicId, query.EmployeeId, cancellationToken);

        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow);
        var items = rows
            .Select(row => new OneTimeDeductionPendingRow(
                row.OneTimeDeductionPublicId,
                row.PersonnelFilePublicId,
                row.EmployeeName,
                row.DeductionDate,
                row.ConceptTypeCode,
                row.ConceptNameSnapshot,
                row.Amount,
                row.CurrencyCode,
                row.PayrollTypeCode,
                row.PayrollPeriodPublicId,
                row.PayrollPeriodLabel,
                row.PayrollPeriodEndDate,
                // A deduction whose target period already closed is OVERDUE work: it should have been charged.
                IsOverdue: row.PayrollPeriodEndDate is { } endDate && endDate < today,
                row.ConcurrencyToken))
            .OrderBy(row => row.EmployeeName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.DeductionDate)
            .ToArray();

        return Result<OneTimeDeductionPendingResponse>.Success(
            new OneTimeDeductionPendingResponse(items, items.Length, items.Count(row => row.IsOverdue)));
    }
}

// ── Apply-period batch (Manage — atomic, ordered locks) ──────────────────────────────────────────────

internal sealed class ApplyOneTimeDeductionPeriodCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ApplyOneTimeDeductionPeriodCommand, OneTimeDeductionApplyPeriodResult>
{
    public async Task<Result<OneTimeDeductionApplyPeriodResult>> Handle(
        ApplyOneTimeDeductionPeriodCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageOneTimeDeductionsAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OneTimeDeductionApplyPeriodResult>.Failure(authorizationResult.Error);
        }

        var tenantId = command.CompanyId;
        var payrollTypeCode = command.PayrollTypeCode.Trim().ToUpperInvariant();

        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
                tenantId, PersonnelCurriculumCatalogCategories.PayrollType, payrollTypeCode, cancellationToken))
        {
            return Result<OneTimeDeductionApplyPeriodResult>.Failure(OneTimeDeductionErrors.PayrollTypeInvalid);
        }

        long? payrollPeriodInternalId = null;
        if (command.PayrollPeriodPublicId is { } periodPublicId && periodPublicId != Guid.Empty)
        {
            var period = await employeeRepository.ResolveOneTimeDeductionPayrollPeriodAsync(tenantId, periodPublicId, cancellationToken);
            if (period is null || !period.IsActive)
            {
                return Result<OneTimeDeductionApplyPeriodResult>.Failure(OneTimeDeductionErrors.PayrollPeriodInvalid);
            }

            payrollPeriodInternalId = period.InternalId;
        }

        var excluded = command.ExcludedDeductionPublicIds?.ToHashSet() ?? [];
        var candidates = await employeeRepository.GetOneTimeDeductionBatchScanAsync(
            tenantId, payrollTypeCode, command.PayrollPeriodPublicId, cancellationToken);

        var targets = candidates.Where(item => !excluded.Contains(item.PublicId)).ToArray();
        var pospuestos = candidates.Count - targets.Length;

        if (targets.Length == 0)
        {
            return Result<OneTimeDeductionApplyPeriodResult>.Success(new OneTimeDeductionApplyPeriodResult(0, pospuestos));
        }

        _ = Guid.TryParse(currentUserService.UserId, out var appliedByUserId);
        var now = dateTimeProvider.UtcNow;
        var appliedDate = DateOnly.FromDateTime(now);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var aplicados = 0;

            // The scan is id-ordered, so the locks are taken in a stable order (anti-deadlock). The batch is
            // ATOMIC: any conflict rolls the whole run back with the offender in the detail.
            foreach (var target in targets)
            {
                await employeeRepository.AcquireOneTimeDeductionMutationLockAsync(target.PublicId, cancellationToken);
                var deduction = await employeeRepository.GetTrackedOneTimeDeductionWithApplicationsAsync(target.PublicId, tenantId, cancellationToken);
                if (deduction is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<OneTimeDeductionApplyPeriodResult>.Failure(
                        OneTimeDeductionApplicationErrors.ApplyPeriodConflict($"deduction {target.PublicId} disappeared"));
                }

                var canApply = OneTimeDeductionRules.CanApply(deduction.StatusCode, deduction.HasActiveApplication);
                if (!canApply.IsValid)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<OneTimeDeductionApplyPeriodResult>.Failure(
                        OneTimeDeductionApplicationErrors.ApplyPeriodConflict($"deduction {target.PublicId} is no longer applicable"));
                }

                _ = deduction.Apply(
                    appliedDate,
                    deduction.PayrollTypeCode,
                    payrollPeriodInternalId ?? deduction.PayrollPeriodId,
                    command.PayrollPeriodPublicId ?? deduction.PayrollPeriodPublicId,
                    deduction.PayrollPeriodLabel,
                    OneTimeDeductionApplicationOrigins.Manual,
                    appliedByUserId,
                    settlementPublicId: null,
                    notes: null);
                aplicados++;
            }

            var summary = new OneTimeDeductionApplyPeriodResult(aplicados, pospuestos);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.OneTimeDeductionApplicationsApplied,
                    AuditEntityTypes.PersonnelFile,
                    command.CompanyId,
                    $"ONE_TIME_DEDUCTION_APPLY_PERIOD_{payrollTypeCode}",
                    AuditActions.Update,
                    $"Applied {aplicados} one-time deduction(s) for payroll type {payrollTypeCode} ({pospuestos} postponed).",
                    After: summary),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<OneTimeDeductionApplyPeriodResult>.Success(summary);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

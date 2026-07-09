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

/// <summary>Maps a one-time-income application entity to its API response (user ids null-safe — a non-Guid principal → null).</summary>
internal static class OneTimeIncomeApplicationMapping
{
    public static OneTimeIncomeApplicationResponse ToResponse(PersonnelFileOneTimeIncomeApplication entity) =>
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

/// <summary>
/// Shared application snapshot logic so the unitary application and the apply-period batch cuadran by
/// construction. An application's payroll destination defaults to the income's OWN declared destination (degraded:
/// public id + label, no FK — mirroring the income header, PR-3) unless a resolved payroll-period override is
/// supplied (FK real, §0.13). The payroll type is always the income's declared type (snapshot; not editable).
/// </summary>
internal static class OneTimeIncomeApplicationSupport
{
    public readonly record struct PeriodSnapshot(long? InternalId, Guid? PublicId, string? Label);

    /// <summary>The snapshot for one income given an optional resolved period override (null → income's own destination).</summary>
    public static PeriodSnapshot SnapshotFor(PersonnelFileOneTimeIncome income, PeriodSnapshot? overridePeriod) =>
        overridePeriod ?? new PeriodSnapshot(null, income.PayrollPeriodPublicId, income.PayrollPeriodLabel);

    /// <summary>Applies the income (RN-06) with the resolved snapshot; the caller commits through the unit of work.</summary>
    public static PersonnelFileOneTimeIncomeApplication Apply(
        PersonnelFileOneTimeIncome income,
        DateOnly appliedDate,
        PeriodSnapshot snapshot,
        Guid appliedByUserId,
        string? notes) =>
        income.Apply(
            appliedDate,
            income.PayrollTypeCode,
            snapshot.InternalId,
            snapshot.PublicId,
            snapshot.Label,
            OneTimeIncomeApplicationOrigins.Manual,
            appliedByUserId,
            settlementPublicId: null,
            notes);
}

// ── Reads (View — application history) ─────────────────────────────────────────────────────────────────

internal sealed class GetOneTimeIncomeApplicationsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetOneTimeIncomeApplicationsQuery, IReadOnlyCollection<OneTimeIncomeApplicationResponse>>
{
    public async Task<Result<IReadOnlyCollection<OneTimeIncomeApplicationResponse>>> Handle(
        GetOneTimeIncomeApplicationsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForOneTimeIncomeReadAsync<IReadOnlyCollection<OneTimeIncomeApplicationResponse>>(
            query.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetOneTimeIncomeApplicationsAsync(
            personnelFile!.PublicId, query.OneTimeIncomePublicId, cancellationToken);
        return response is null
            ? Result<IReadOnlyCollection<OneTimeIncomeApplicationResponse>>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<IReadOnlyCollection<OneTimeIncomeApplicationResponse>>.Success(response);
    }
}

// ── Unitary application (Manage — lock) ─────────────────────────────────────────────────────────────────

internal sealed class ApplyOneTimeIncomeApplicationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<ApplyOneTimeIncomeApplicationCommand, OneTimeIncomeApplicationResult>
{
    public async Task<Result<OneTimeIncomeApplicationResult>> Handle(
        ApplyOneTimeIncomeApplicationCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageOneTimeIncomesAsync<OneTimeIncomeApplicationResult>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<OneTimeIncomeApplicationResult>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        if (await employeeRepository.IsOneTimeIncomeProfileRetiredAsync(personnelFile.Id, cancellationToken))
        {
            return Result<OneTimeIncomeApplicationResult>.Failure(PersonnelFileErrors.ProfileRetiredLocked);
        }

        // Pre-check existence + If-Match (AsNoTracking) so we never take the lock for a bad request.
        var snapshot = await employeeRepository.GetOneTimeIncomeAsync(personnelFile.PublicId, command.OneTimeIncomePublicId, cancellationToken);
        if (snapshot is null)
        {
            return Result<OneTimeIncomeApplicationResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (snapshot.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OneTimeIncomeApplicationResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // Resolve the (optional) payroll-period override to a FK real before opening the transaction (§0.13). When
        // omitted, the application defaults to the income's own declared destination (degraded, mirrors the header).
        OneTimeIncomeApplicationSupport.PeriodSnapshot? overridePeriod = null;
        if (command.PayrollPeriodPublicId is { } payrollPeriodPublicId && payrollPeriodPublicId != Guid.Empty)
        {
            var period = await employeeRepository.ResolveOneTimeIncomePayrollPeriodAsync(
                personnelFile.TenantId, payrollPeriodPublicId, cancellationToken);
            if (period is null || !period.IsActive)
            {
                return Result<OneTimeIncomeApplicationResult>.Failure(OneTimeIncomeApplicationErrors.PayrollPeriodInvalid);
            }

            overridePeriod = new OneTimeIncomeApplicationSupport.PeriodSnapshot(period.InternalId, payrollPeriodPublicId, period.Label);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var appliedByUserId);
        var now = dateTimeProvider.UtcNow;
        var appliedDate = command.AppliedDate ?? DateOnly.FromDateTime(now);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // Serialize the at-most-one-active-application rule: acquire the lock, then load fresh (the first
            // tracking load of this income on the context, so it reflects any committed concurrent application).
            await employeeRepository.AcquireOneTimeIncomeMutationLockAsync(command.OneTimeIncomePublicId, cancellationToken);
            var income = await employeeRepository.GetTrackedOneTimeIncomeWithApplicationsAsync(
                command.OneTimeIncomePublicId, personnelFile.TenantId, cancellationToken);
            if (income is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<OneTimeIncomeApplicationResult>.Failure(PersonnelFileErrors.ItemNotFound);
            }

            if (income.ConcurrencyToken != command.ConcurrencyToken)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<OneTimeIncomeApplicationResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
            }

            var canApply = OneTimeIncomeRules.CanApply(income.StatusCode, income.HasActiveApplication);
            if (!canApply.IsValid)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<OneTimeIncomeApplicationResult>.Failure(
                    new Error(canApply.ErrorCode!, "The one-time income cannot be applied.", ErrorType.UnprocessableEntity));
            }

            var application = OneTimeIncomeApplicationSupport.Apply(
                income,
                appliedDate,
                OneTimeIncomeApplicationSupport.SnapshotFor(income, overridePeriod),
                appliedByUserId,
                command.Notes);

            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var result = new OneTimeIncomeApplicationResult(
                OneTimeIncomeApplicationMapping.ToResponse(application),
                income.StatusCode,
                income.ConcurrencyToken);

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile,
                $"Applied one-time income for {personnelFile.FullName}.", result, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<OneTimeIncomeApplicationResult>.Success(result);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

// ── Unitary annulment / reversal (Manage — lock) ────────────────────────────────────────────────────────

internal sealed class AnnulOneTimeIncomeApplicationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AnnulOneTimeIncomeApplicationCommand, OneTimeIncomeApplicationResult>
{
    public async Task<Result<OneTimeIncomeApplicationResult>> Handle(
        AnnulOneTimeIncomeApplicationCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageOneTimeIncomesAsync<OneTimeIncomeApplicationResult>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<OneTimeIncomeApplicationResult>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return Result<OneTimeIncomeApplicationResult>.Failure(OneTimeIncomeErrors.AnnulmentReasonRequired);
        }

        var snapshot = await employeeRepository.GetOneTimeIncomeAsync(personnelFile.PublicId, command.OneTimeIncomePublicId, cancellationToken);
        if (snapshot is null)
        {
            return Result<OneTimeIncomeApplicationResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (snapshot.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OneTimeIncomeApplicationResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var byUserId);
        var now = dateTimeProvider.UtcNow;

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await employeeRepository.AcquireOneTimeIncomeMutationLockAsync(command.OneTimeIncomePublicId, cancellationToken);
            var income = await employeeRepository.GetTrackedOneTimeIncomeWithApplicationsAsync(
                command.OneTimeIncomePublicId, personnelFile.TenantId, cancellationToken);
            if (income is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<OneTimeIncomeApplicationResult>.Failure(PersonnelFileErrors.ItemNotFound);
            }

            if (income.ConcurrencyToken != command.ConcurrencyToken)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<OneTimeIncomeApplicationResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
            }

            var application = income.Applications.FirstOrDefault(item =>
                item.PublicId == command.ApplicationPublicId
                && item.StatusCode == OneTimeIncomeApplicationStatuses.Aplicada);
            if (application is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<OneTimeIncomeApplicationResult>.Failure(OneTimeIncomeApplicationErrors.ApplicationNotFound);
            }

            // Annuls the active application (is_active=false + ANULADA) and reopens the income APLICADO → AUTORIZADO
            // in the SAME transaction, so it is immediately re-applicable.
            income.AnnulApplication(command.ApplicationPublicId, command.Reason, byUserId, now);

            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var result = new OneTimeIncomeApplicationResult(
                OneTimeIncomeApplicationMapping.ToResponse(application),
                income.StatusCode,
                income.ConcurrencyToken);

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile,
                $"Annulled one-time income application for {personnelFile.FullName}.", result, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<OneTimeIncomeApplicationResult>.Success(result);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

// ── Apply-period batch (company-scoped, Manage per-handler, ordered locks, atomic) ─────────────────────

internal sealed class ApplyOneTimeIncomePeriodCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ApplyOneTimeIncomePeriodCommand, OneTimeIncomeApplyPeriodResult>
{
    public async Task<Result<OneTimeIncomeApplyPeriodResult>> Handle(
        ApplyOneTimeIncomePeriodCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageOneTimeIncomesAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OneTimeIncomeApplyPeriodResult>.Failure(authorizationResult.Error);
        }

        var tenantId = command.CompanyId;
        var payrollTypeCode = command.PayrollTypeCode.Trim().ToUpperInvariant();

        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
                tenantId, PersonnelCurriculumCatalogCategories.PayrollType, payrollTypeCode, cancellationToken))
        {
            return Result<OneTimeIncomeApplyPeriodResult>.Failure(OneTimeIncomeErrors.PayrollTypeInvalid);
        }

        // Resolve the (optional) destination override ONCE for the whole batch (FK real when a public id resolves;
        // a bare label is a degraded override; none → each application defaults to its income's own destination).
        OneTimeIncomeApplicationSupport.PeriodSnapshot? overridePeriod = null;
        if (command.PayrollPeriodPublicId is { } payrollPeriodPublicId && payrollPeriodPublicId != Guid.Empty)
        {
            var period = await employeeRepository.ResolveOneTimeIncomePayrollPeriodAsync(tenantId, payrollPeriodPublicId, cancellationToken);
            if (period is null || !period.IsActive)
            {
                return Result<OneTimeIncomeApplyPeriodResult>.Failure(OneTimeIncomeApplicationErrors.PayrollPeriodInvalid);
            }

            overridePeriod = new OneTimeIncomeApplicationSupport.PeriodSnapshot(period.InternalId, payrollPeriodPublicId, period.Label);
        }
        else if (!string.IsNullOrWhiteSpace(command.PayrollPeriodLabel))
        {
            overridePeriod = new OneTimeIncomeApplicationSupport.PeriodSnapshot(null, null, command.PayrollPeriodLabel.Trim());
        }

        var excluded = command.ExcludedIncomePublicIds?.ToHashSet() ?? [];
        var candidates = await employeeRepository.GetOneTimeIncomeApplyPeriodCandidatesAsync(tenantId, payrollTypeCode, cancellationToken);

        var targets = new List<OneTimeIncomeBatchCandidate>();
        var pospuestos = 0;
        foreach (var candidate in candidates)
        {
            if (excluded.Contains(candidate.PublicId))
            {
                pospuestos++;
                continue;
            }

            targets.Add(candidate);
        }

        if (targets.Count == 0)
        {
            return Result<OneTimeIncomeApplyPeriodResult>.Success(new OneTimeIncomeApplyPeriodResult(0, pospuestos));
        }

        _ = Guid.TryParse(currentUserService.UserId, out var appliedByUserId);
        var now = dateTimeProvider.UtcNow;
        var appliedDate = DateOnly.FromDateTime(now);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var aplicados = 0;

            // Ordered by internal id (targets already come from an id-ordered scan) → anti-deadlock. The batch is
            // atomic: any conflict rolls the whole transaction back with the offending income in the detail.
            foreach (var target in targets)
            {
                await employeeRepository.AcquireOneTimeIncomeMutationLockAsync(target.PublicId, cancellationToken);
                var income = await employeeRepository.GetTrackedOneTimeIncomeWithApplicationsAsync(target.PublicId, tenantId, cancellationToken);
                if (income is null
                    || income.StatusCode != OneTimeIncomeStatuses.Autorizado
                    || income.HasActiveApplication)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<OneTimeIncomeApplyPeriodResult>.Failure(
                        OneTimeIncomeApplicationErrors.ApplyPeriodConflict($"income {target.PublicId} is no longer an applicable AUTORIZADO"));
                }

                _ = OneTimeIncomeApplicationSupport.Apply(
                    income,
                    appliedDate,
                    OneTimeIncomeApplicationSupport.SnapshotFor(income, overridePeriod),
                    appliedByUserId,
                    notes: null);
                aplicados++;
            }

            var summary = new OneTimeIncomeApplyPeriodResult(aplicados, pospuestos);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.OneTimeIncomeApplicationsApplied,
                    AuditEntityTypes.PersonnelFile,
                    command.CompanyId,
                    $"ONE_TIME_INCOME_APPLY_PERIOD_{payrollTypeCode}",
                    AuditActions.Update,
                    $"Applied {aplicados} one-time income(s) for payroll type {payrollTypeCode} ({pospuestos} postponed).",
                    After: summary),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<OneTimeIncomeApplyPeriodResult>.Success(summary);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

// ── Pending / overdue tray (company-scoped, View per-handler) ──────────────────────────────────────────

internal sealed class QueryOneTimeIncomePendingQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileEmployeeRepository employeeRepository,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<QueryOneTimeIncomePendingQuery, OneTimeIncomePendingResponse>
{
    public async Task<Result<OneTimeIncomePendingResponse>> Handle(
        QueryOneTimeIncomePendingQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewOneTimeIncomesAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OneTimeIncomePendingResponse>.Failure(authorizationResult.Error);
        }

        var payrollTypeCode = string.IsNullOrWhiteSpace(query.PayrollTypeCode)
            ? null
            : query.PayrollTypeCode.Trim().ToUpperInvariant();

        var rows = await employeeRepository.GetOneTimeIncomePendingRowsAsync(query.CompanyId, payrollTypeCode, cancellationToken);
        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow);

        var items = rows
            .Select(row => new OneTimeIncomePendingRow(
                row.OneTimeIncomePublicId,
                row.PersonnelFilePublicId,
                row.EmployeeName,
                row.IncomeDate,
                row.ConceptTypeCode,
                row.ConceptNameSnapshot,
                row.Amount,
                row.CurrencyCode,
                row.PayrollTypeCode,
                row.PayrollPeriodPublicId,
                row.PayrollPeriodLabel,
                row.PayrollPeriodEndDate,
                OneTimeIncomeRules.IsOverdue(row.PayrollPeriodEndDate, today),
                row.ConcurrencyToken))
            .Where(row => !query.OnlyOverdue || row.IsOverdue)
            .ToArray();

        var overdueCount = items.Count(row => row.IsOverdue);
        return Result<OneTimeIncomePendingResponse>.Success(
            new OneTimeIncomePendingResponse(items, items.Length, overdueCount));
    }
}

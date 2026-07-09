using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles.Compensation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Shared pure projection of a VIGENTE recurring income's PENDING theoretical installments (RF-011). Reuses the
/// same <c>RecurringIncomeInstallmentSupport.PendingInstallmentsUpTo</c> the apply-period batch uses, so the
/// pending bandeja and the batch cuadran by construction; the amount / due date come from the pure rules.
/// </summary>
internal static class RecurringIncomePendingProjection
{
    public static IReadOnlyList<RecurringIncomePendingInstallmentResponse> Build(
        IReadOnlyList<RecurringIncomePendingScanItem> scan,
        DateOnly cutoff,
        DateOnly today,
        DateOnly? startDate)
    {
        var rows = new List<RecurringIncomePendingInstallmentResponse>();
        foreach (var item in scan)
        {
            var batchScan = new RecurringIncomeBatchScanItem(
                item.InternalId,
                item.PublicId,
                item.IsIndefinite,
                item.InstallmentFrequencyCode,
                item.InstallmentStartDate,
                item.InstallmentValue,
                item.InstallmentCount,
                item.TotalAmount,
                item.AppliedInstallmentNumbers);

            var pending = RecurringIncomeInstallmentSupport.PendingInstallmentsUpTo(batchScan, cutoff);
            if (pending.Count == 0)
            {
                continue;
            }

            var plan = new RecurringIncomePlan(item.InstallmentValue, item.InstallmentCount, item.TotalAmount, item.IsIndefinite);
            foreach (var number in pending)
            {
                var dueDate = RecurringIncomeRules.TheoreticalDueDateFor(item.InstallmentFrequencyCode, item.InstallmentStartDate, number);
                if (startDate is { } from && dueDate < from)
                {
                    continue;
                }

                var amount = RecurringIncomeRules.InstallmentAmountFor(number, plan);
                rows.Add(new RecurringIncomePendingInstallmentResponse(
                    item.PublicId,
                    item.PersonnelFilePublicId,
                    item.EmployeeFullName,
                    item.EmployeeCode,
                    item.RecurringIncomeTypeCode,
                    item.ConceptNameSnapshot,
                    item.AssignedPositionPublicId,
                    item.CostCenterNameSnapshot,
                    item.PayrollTypeCode,
                    item.CurrencyCode,
                    number,
                    dueDate,
                    amount,
                    IsOverdue: dueDate < today));
            }
        }

        return rows
            .OrderBy(row => row.EmployeeFullName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.RecurringIncomePublicId)
            .ThenBy(row => row.InstallmentNumber)
            .ToList();
    }
}

// ── Bandeja de ingresos (View — gate per handler) ──────────────────────────────────────────────────────

internal sealed class QueryRecurringIncomesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileEmployeeRepository employeeRepository)
    : IQueryHandler<QueryRecurringIncomesQuery, RecurringIncomeBandejaResponse>
{
    public async Task<Result<RecurringIncomeBandejaResponse>> Handle(
        QueryRecurringIncomesQuery query,
        CancellationToken cancellationToken)
    {
        // View gate enforced per handler (NOT a policy-set on the POST, which would treat this READ as a manage
        // action and 403 view-only users). ViewRecurringIncomes covers the company bandeja and its exports.
        var authorizationResult = await authorizationService.EnsureCanViewRecurringIncomesAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<RecurringIncomeBandejaResponse>.Failure(authorizationResult.Error);
        }

        var page = await employeeRepository.QueryRecurringIncomesAsync(query, cancellationToken);
        return Result<RecurringIncomeBandejaResponse>.Success(page);
    }
}

internal sealed class ExportRecurringIncomesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileEmployeeRepository employeeRepository)
    : IQueryHandler<ExportRecurringIncomesQuery, IReadOnlyCollection<IngresoCiclicoExportRow>>
{
    public async Task<Result<IReadOnlyCollection<IngresoCiclicoExportRow>>> Handle(
        ExportRecurringIncomesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewRecurringIncomesAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<IngresoCiclicoExportRow>>.Failure(authorizationResult.Error);
        }

        var rows = await employeeRepository.GetRecurringIncomeExportRowsAsync(query, cancellationToken);
        return Result<IReadOnlyCollection<IngresoCiclicoExportRow>>.Success(rows);
    }
}

// ── Bandeja de cuotas pendientes / vencidas (View — gate per handler) ──────────────────────────────────

internal sealed class QueryPendingRecurringIncomeInstallmentsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileEmployeeRepository employeeRepository,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<QueryPendingRecurringIncomeInstallmentsQuery, RecurringIncomePendingInstallmentsResponse>
{
    public async Task<Result<RecurringIncomePendingInstallmentsResponse>> Handle(
        QueryPendingRecurringIncomeInstallmentsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewRecurringIncomesAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<RecurringIncomePendingInstallmentsResponse>.Failure(authorizationResult.Error);
        }

        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow);
        var (cutoffFailure, cutoff) = await ResolveCutoffAsync(
            employeeRepository, query.CompanyId, query.PayrollPeriodPublicId, query.CutoffDate, today, cancellationToken);
        if (cutoffFailure is not null)
        {
            return Result<RecurringIncomePendingInstallmentsResponse>.Failure(cutoffFailure);
        }

        var scan = await employeeRepository.GetRecurringIncomePendingScanAsync(
            query.CompanyId, query.PayrollTypeCode, query.EmployeeId, cancellationToken);

        var all = RecurringIncomePendingProjection.Build(scan, cutoff, today, query.StartDate);
        var page = all
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArray();

        return Result<RecurringIncomePendingInstallmentsResponse>.Success(
            new RecurringIncomePendingInstallmentsResponse(page, query.PageNumber, query.PageSize, all.Count));
    }

    internal static async Task<(Error? Failure, DateOnly Cutoff)> ResolveCutoffAsync(
        IPersonnelFileEmployeeRepository employeeRepository,
        Guid tenantId,
        Guid? payrollPeriodPublicId,
        DateOnly? cutoffDate,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        if (payrollPeriodPublicId is { } periodPublicId && periodPublicId != Guid.Empty)
        {
            var period = await employeeRepository.ResolveRecurringIncomePayrollPeriodAsync(tenantId, periodPublicId, cancellationToken);
            if (period is null || !period.IsActive)
            {
                return (RecurringIncomeInstallmentErrors.PayrollPeriodInvalid, default);
            }

            return (null, period.EndDate);
        }

        return (null, cutoffDate ?? today);
    }
}

internal sealed class ExportPendingRecurringIncomeInstallmentsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileEmployeeRepository employeeRepository,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<ExportPendingRecurringIncomeInstallmentsQuery, IReadOnlyCollection<CuotaPendienteCiclicaExportRow>>
{
    public async Task<Result<IReadOnlyCollection<CuotaPendienteCiclicaExportRow>>> Handle(
        ExportPendingRecurringIncomeInstallmentsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewRecurringIncomesAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<CuotaPendienteCiclicaExportRow>>.Failure(authorizationResult.Error);
        }

        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow);
        var (cutoffFailure, cutoff) = await QueryPendingRecurringIncomeInstallmentsQueryHandler.ResolveCutoffAsync(
            employeeRepository, query.CompanyId, query.PayrollPeriodPublicId, query.CutoffDate, today, cancellationToken);
        if (cutoffFailure is not null)
        {
            return Result<IReadOnlyCollection<CuotaPendienteCiclicaExportRow>>.Failure(cutoffFailure);
        }

        var scan = await employeeRepository.GetRecurringIncomePendingScanAsync(
            query.CompanyId, query.PayrollTypeCode, query.EmployeeId, cancellationToken);

        IEnumerable<RecurringIncomePendingInstallmentResponse> rows = RecurringIncomePendingProjection.Build(scan, cutoff, today, query.StartDate);
        if (query.MaxRows is { } maxRows)
        {
            rows = rows.Take(maxRows + 1);
        }

        var exportRows = rows
            .Select(row => new CuotaPendienteCiclicaExportRow(
                row.EmployeeFullName,
                row.EmployeeCode,
                row.RecurringIncomeTypeCode,
                row.ConceptNameSnapshot,
                row.AssignedPositionPublicId.ToString(),
                row.CostCenterNameSnapshot,
                row.PayrollTypeCode,
                row.InstallmentNumber,
                row.TheoreticalDueDate,
                row.Amount,
                row.CurrencyCode,
                row.IsOverdue))
            .ToArray();

        return Result<IReadOnlyCollection<CuotaPendienteCiclicaExportRow>>.Success(exportRows);
    }
}

// ── Insumo de planilla (View — gate per handler; mandatory range → 422) ────────────────────────────────

internal sealed class ExportRecurringIncomePayrollInputQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileEmployeeRepository employeeRepository)
    : IQueryHandler<ExportRecurringIncomePayrollInputQuery, IReadOnlyCollection<InsumoPlanillaCiclicoExportRow>>
{
    public async Task<Result<IReadOnlyCollection<InsumoPlanillaCiclicoExportRow>>> Handle(
        ExportRecurringIncomePayrollInputQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewRecurringIncomesAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<InsumoPlanillaCiclicoExportRow>>.Failure(authorizationResult.Error);
        }

        // The date range is MANDATORY for the payroll input (§5) — a missing bound is a domain rule (422), not a
        // shape error, so the operator gets an actionable code instead of a raw validation failure.
        if (query.StartDate is not { } startDate || query.EndDate is not { } endDate)
        {
            return Result<IReadOnlyCollection<InsumoPlanillaCiclicoExportRow>>.Failure(RecurringIncomeErrors.PayrollInputRangeRequired);
        }

        var rows = await employeeRepository.GetRecurringIncomePayrollInputRowsAsync(
            query.CompanyId, query.PayrollTypeCode, startDate, endDate, query.MaxRows, cancellationToken);
        return Result<IReadOnlyCollection<InsumoPlanillaCiclicoExportRow>>.Success(rows);
    }
}

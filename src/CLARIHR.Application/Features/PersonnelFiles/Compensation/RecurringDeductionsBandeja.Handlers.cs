using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles.Compensation;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Shared pure projection of a VIGENTE credit's PENDING theoretical charges (RF-011). It reuses the very same
/// <c>RecurringDeductionInstallmentSupport.PendingChargesUpTo</c> the apply-period batch uses — so the pending
/// bandeja, the payroll input and the batch cuadran by construction — and the amount / split / due date all come
/// from the pure rules.
/// </summary>
internal static class RecurringDeductionPendingProjection
{
    public static IReadOnlyList<RecurringDeductionPendingInstallmentResponse> Build(
        IReadOnlyList<RecurringDeductionPendingScanItem> scan,
        DateOnly cutoff,
        DateOnly today,
        DateOnly? startDate)
    {
        var rows = new List<RecurringDeductionPendingInstallmentResponse>();
        foreach (var item in scan)
        {
            // A credit whose effective date has not been reached cannot be charged yet (D-04) — the same rule the
            // batch applies, so the bandeja never shows work the batch would refuse.
            if (item.Scan.EffectiveDate > today)
            {
                continue;
            }

            var pending = RecurringDeductionInstallmentSupport.PendingChargesUpTo(item.Scan, cutoff);
            if (pending.Count == 0)
            {
                continue;
            }

            var plan = RecurringDeductionInstallmentSupport.ToPlan(item.Scan);
            var exceptionMonths = item.Scan.ExceptionMonths.ToHashSet();

            foreach (var number in pending)
            {
                var dueDate = RecurringDeductionRules.ChargeDueDateFor(
                    item.Scan.ApplicationFrequencyCode, item.Scan.InstallmentStartDate, exceptionMonths, number);
                if (startDate is { } from && dueDate < from)
                {
                    continue;
                }

                var (amount, capital, interest) = RecurringDeductionRules.ChargeSplitFor(
                    number, plan, item.Scan.ApplicationFrequencyCode);

                rows.Add(new RecurringDeductionPendingInstallmentResponse(
                    item.Scan.PublicId,
                    item.PersonnelFilePublicId,
                    item.EmployeeFullName,
                    item.EmployeeCode,
                    item.Reference,
                    item.RecurringDeductionTypeCode,
                    item.ConceptNameSnapshot,
                    item.FinancialInstitution,
                    item.AssignedPositionPublicId,
                    item.PayrollTypeCode,
                    item.CurrencyCode,
                    number,
                    dueDate,
                    amount,
                    capital,
                    interest,
                    IsOverdue: dueDate < today));
            }
        }

        return rows
            .OrderBy(row => row.EmployeeFullName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.RecurringDeductionPublicId)
            .ThenBy(row => row.InstallmentNumber)
            .ToList();
    }
}

// ── Bandeja de descuentos (View — gate per handler) ────────────────────────────────────────────────────

internal sealed class QueryRecurringDeductionsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileEmployeeRepository employeeRepository)
    : IQueryHandler<QueryRecurringDeductionsQuery, RecurringDeductionBandejaResponse>
{
    public async Task<Result<RecurringDeductionBandejaResponse>> Handle(
        QueryRecurringDeductionsQuery query,
        CancellationToken cancellationToken)
    {
        // View gate enforced per handler (NOT a policy-set on the POST, which would treat this READ as a manage
        // action and 403 view-only users). ViewRecurringDeductions covers the company bandeja and its exports.
        var authorizationResult = await authorizationService.EnsureCanViewRecurringDeductionsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<RecurringDeductionBandejaResponse>.Failure(authorizationResult.Error);
        }

        var page = await employeeRepository.QueryRecurringDeductionsAsync(query, cancellationToken);
        return Result<RecurringDeductionBandejaResponse>.Success(page);
    }
}

internal sealed class ExportRecurringDeductionsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileEmployeeRepository employeeRepository)
    : IQueryHandler<ExportRecurringDeductionsQuery, IReadOnlyCollection<DescuentoCiclicoExportRow>>
{
    public async Task<Result<IReadOnlyCollection<DescuentoCiclicoExportRow>>> Handle(
        ExportRecurringDeductionsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewRecurringDeductionsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<DescuentoCiclicoExportRow>>.Failure(authorizationResult.Error);
        }

        var rows = await employeeRepository.GetRecurringDeductionExportRowsAsync(query, cancellationToken);
        return Result<IReadOnlyCollection<DescuentoCiclicoExportRow>>.Success(rows);
    }
}

// ── Bandeja de cobros pendientes / vencidos (View — gate per handler) ──────────────────────────────────

internal sealed class QueryPendingRecurringDeductionInstallmentsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileEmployeeRepository employeeRepository,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<QueryPendingRecurringDeductionInstallmentsQuery, RecurringDeductionPendingInstallmentsResponse>
{
    public async Task<Result<RecurringDeductionPendingInstallmentsResponse>> Handle(
        QueryPendingRecurringDeductionInstallmentsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewRecurringDeductionsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<RecurringDeductionPendingInstallmentsResponse>.Failure(authorizationResult.Error);
        }

        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow);
        var (cutoffFailure, cutoff) = await ResolveCutoffAsync(
            employeeRepository, query.CompanyId, query.PayrollPeriodPublicId, query.CutoffDate, today, cancellationToken);
        if (cutoffFailure is not null)
        {
            return Result<RecurringDeductionPendingInstallmentsResponse>.Failure(cutoffFailure);
        }

        var scan = await employeeRepository.GetRecurringDeductionPendingScanAsync(
            query.CompanyId, query.PayrollTypeCode, query.EmployeeId, cancellationToken);

        var all = RecurringDeductionPendingProjection.Build(scan, cutoff, today, query.StartDate);
        var page = all
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArray();

        return Result<RecurringDeductionPendingInstallmentsResponse>.Success(
            new RecurringDeductionPendingInstallmentsResponse(page, query.PageNumber, query.PageSize, all.Count));
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
            var period = await employeeRepository.ResolveRecurringDeductionPayrollPeriodAsync(tenantId, periodPublicId, cancellationToken);
            if (period is null || !period.IsActive)
            {
                return (RecurringDeductionInstallmentErrors.PayrollPeriodInvalid, default);
            }

            return (null, period.EndDate);
        }

        return (null, cutoffDate ?? today);
    }
}

internal sealed class ExportPendingRecurringDeductionInstallmentsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileEmployeeRepository employeeRepository,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<ExportPendingRecurringDeductionInstallmentsQuery, IReadOnlyCollection<CuotaPendienteDescuentoExportRow>>
{
    public async Task<Result<IReadOnlyCollection<CuotaPendienteDescuentoExportRow>>> Handle(
        ExportPendingRecurringDeductionInstallmentsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewRecurringDeductionsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<CuotaPendienteDescuentoExportRow>>.Failure(authorizationResult.Error);
        }

        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow);
        var (cutoffFailure, cutoff) = await QueryPendingRecurringDeductionInstallmentsQueryHandler.ResolveCutoffAsync(
            employeeRepository, query.CompanyId, query.PayrollPeriodPublicId, query.CutoffDate, today, cancellationToken);
        if (cutoffFailure is not null)
        {
            return Result<IReadOnlyCollection<CuotaPendienteDescuentoExportRow>>.Failure(cutoffFailure);
        }

        var scan = await employeeRepository.GetRecurringDeductionPendingScanAsync(
            query.CompanyId, query.PayrollTypeCode, query.EmployeeId, cancellationToken);

        IEnumerable<RecurringDeductionPendingInstallmentResponse> rows =
            RecurringDeductionPendingProjection.Build(scan, cutoff, today, query.StartDate);
        if (query.MaxRows is { } maxRows)
        {
            rows = rows.Take(maxRows + 1);
        }

        var exportRows = rows
            .Select(row => new CuotaPendienteDescuentoExportRow(
                row.EmployeeFullName,
                row.EmployeeCode,
                row.Reference,
                row.RecurringDeductionTypeCode,
                row.ConceptNameSnapshot,
                row.FinancialInstitution,
                row.AssignedPositionPublicId.ToString(),
                row.PayrollTypeCode,
                row.InstallmentNumber,
                row.TheoreticalDueDate,
                row.Amount,
                row.CapitalAmount,
                row.InterestAmount,
                row.CurrencyCode,
                row.IsOverdue))
            .ToArray();

        return Result<IReadOnlyCollection<CuotaPendienteDescuentoExportRow>>.Success(exportRows);
    }
}

// ── Insumo de planilla (View — gate per handler; mandatory range → 422) ────────────────────────────────

internal sealed class ExportRecurringDeductionPayrollInputQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileEmployeeRepository employeeRepository)
    : IQueryHandler<ExportRecurringDeductionPayrollInputQuery, IReadOnlyCollection<InsumoPlanillaDescuentoExportRow>>
{
    public async Task<Result<IReadOnlyCollection<InsumoPlanillaDescuentoExportRow>>> Handle(
        ExportRecurringDeductionPayrollInputQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewRecurringDeductionsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<InsumoPlanillaDescuentoExportRow>>.Failure(authorizationResult.Error);
        }

        // The date range is MANDATORY for the payroll input — a missing bound is a domain rule (422), not a shape
        // error, so the operator gets an actionable code instead of a raw validation failure.
        if (query.StartDate is not { } startDate || query.EndDate is not { } endDate)
        {
            return Result<IReadOnlyCollection<InsumoPlanillaDescuentoExportRow>>.Failure(RecurringDeductionErrors.PayrollInputRangeRequired);
        }

        var rows = await employeeRepository.GetRecurringDeductionPayrollInputRowsAsync(
            query.CompanyId, query.PayrollTypeCode, startDate, endDate, query.MaxRows, cancellationToken);
        return Result<IReadOnlyCollection<InsumoPlanillaDescuentoExportRow>>.Success(rows);
    }
}

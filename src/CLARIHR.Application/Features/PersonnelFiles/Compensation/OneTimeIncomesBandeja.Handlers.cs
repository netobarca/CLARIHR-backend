using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.PersonnelFiles;

// ── Bandeja de ingresos (View — gate per handler) ────────────────────────────────────────────────────────

/// <summary>
/// The company-wide one-time-income advanced search + aggregation (RF-008 / №14). The View gate is enforced per
/// handler (NOT a policy-set on the POST, which the governance would map to a Manage action → false 403s for
/// view-only users; precedent: settlements / recurring-incomes reporting). An invalid <c>groupBy</c> token is a
/// 400 (parsed here, before the repository); a valid one travels to the aggregation which respects EVERY filter.
/// </summary>
internal sealed class QueryOneTimeIncomesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileEmployeeRepository employeeRepository)
    : IQueryHandler<QueryOneTimeIncomesQuery, OneTimeIncomeBandejaResponse>
{
    public async Task<Result<OneTimeIncomeBandejaResponse>> Handle(
        QueryOneTimeIncomesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewOneTimeIncomesAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OneTimeIncomeBandejaResponse>.Failure(authorizationResult.Error);
        }

        OneTimeIncomeGroupDimension? dimension = null;
        if (!string.IsNullOrWhiteSpace(query.GroupBy))
        {
            if (!OneTimeIncomeGroupDimensions.TryParse(query.GroupBy, out var parsed))
            {
                return Result<OneTimeIncomeBandejaResponse>.Failure(OneTimeIncomeBandejaErrors.GroupDimensionInvalid());
            }

            dimension = parsed;
        }

        var page = await employeeRepository.QueryOneTimeIncomesAsync(query, dimension, cancellationToken);
        return Result<OneTimeIncomeBandejaResponse>.Success(page);
    }
}

internal sealed class ExportOneTimeIncomesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileEmployeeRepository employeeRepository)
    : IQueryHandler<ExportOneTimeIncomesQuery, IReadOnlyCollection<IngresoEventualExportRow>>
{
    public async Task<Result<IReadOnlyCollection<IngresoEventualExportRow>>> Handle(
        ExportOneTimeIncomesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewOneTimeIncomesAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<IngresoEventualExportRow>>.Failure(authorizationResult.Error);
        }

        var rows = await employeeRepository.GetOneTimeIncomeExportRowsAsync(query, cancellationToken);
        return Result<IReadOnlyCollection<IngresoEventualExportRow>>.Success(rows);
    }
}

// ── Bandeja de pendientes + insumo de planilla (View — gate per handler) ─────────────────────────────────

internal sealed class ExportOneTimeIncomePendingQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileEmployeeRepository employeeRepository,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<ExportOneTimeIncomePendingQuery, IReadOnlyCollection<IngresoEventualPendienteExportRow>>
{
    public async Task<Result<IReadOnlyCollection<IngresoEventualPendienteExportRow>>> Handle(
        ExportOneTimeIncomePendingQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewOneTimeIncomesAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<IngresoEventualPendienteExportRow>>.Failure(authorizationResult.Error);
        }

        var payrollTypeCode = string.IsNullOrWhiteSpace(query.PayrollTypeCode)
            ? null
            : query.PayrollTypeCode.Trim().ToUpperInvariant();

        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow);
        var rows = await employeeRepository.GetOneTimeIncomePendingExportRowsAsync(
            query.CompanyId, payrollTypeCode, query.OnlyOverdue, today, query.MaxRows, cancellationToken);
        return Result<IReadOnlyCollection<IngresoEventualPendienteExportRow>>.Success(rows);
    }
}

internal sealed class ExportOneTimeIncomePayrollInputQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileEmployeeRepository employeeRepository)
    : IQueryHandler<ExportOneTimeIncomePayrollInputQuery, IReadOnlyCollection<InsumoPlanillaEventualExportRow>>
{
    public async Task<Result<IReadOnlyCollection<InsumoPlanillaEventualExportRow>>> Handle(
        ExportOneTimeIncomePayrollInputQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewOneTimeIncomesAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<InsumoPlanillaEventualExportRow>>.Failure(authorizationResult.Error);
        }

        // The payroll type + period are MANDATORY for the payroll input (§5) — a missing bound is a 400 with an
        // actionable code, so the export always maps a bounded destination.
        if (string.IsNullOrWhiteSpace(query.PayrollTypeCode) || string.IsNullOrWhiteSpace(query.PayrollPeriod))
        {
            return Result<IReadOnlyCollection<InsumoPlanillaEventualExportRow>>.Failure(OneTimeIncomeBandejaErrors.PayrollInputFilterRequired);
        }

        var rows = await employeeRepository.GetOneTimeIncomePayrollInputRowsAsync(
            query.CompanyId,
            query.PayrollTypeCode.Trim().ToUpperInvariant(),
            query.PayrollPeriod.Trim(),
            query.MaxRows,
            cancellationToken);
        return Result<IReadOnlyCollection<InsumoPlanillaEventualExportRow>>.Success(rows);
    }
}

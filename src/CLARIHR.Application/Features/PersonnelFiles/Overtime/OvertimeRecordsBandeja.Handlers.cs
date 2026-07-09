using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.PersonnelFiles;

// ── Bandeja de horas extras (View — gate per handler) ────────────────────────────────────────────────────

/// <summary>
/// The company-wide overtime advanced search (RF-011 / §0.16). The View gate is enforced per handler (NOT a
/// policy-set on the POST, which the governance would map to a Manage action → false 403s for view-only users;
/// precedent: settlements / recurring-incomes / one-time-incomes reporting). The response carries the paginated
/// items, the per-status counts (span every status), the global total HOURS and the totals-by-type buckets.
/// </summary>
internal sealed class QueryOvertimeRecordsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileEmployeeRepository employeeRepository)
    : IQueryHandler<QueryOvertimeRecordsQuery, OvertimeRecordBandejaResponse>
{
    public async Task<Result<OvertimeRecordBandejaResponse>> Handle(
        QueryOvertimeRecordsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewOvertimeRecordsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OvertimeRecordBandejaResponse>.Failure(authorizationResult.Error);
        }

        var page = await employeeRepository.QueryOvertimeRecordsAsync(query, cancellationToken);
        return Result<OvertimeRecordBandejaResponse>.Success(page);
    }
}

internal sealed class ExportOvertimeRecordsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileEmployeeRepository employeeRepository)
    : IQueryHandler<ExportOvertimeRecordsQuery, IReadOnlyCollection<HoraExtraExportRow>>
{
    public async Task<Result<IReadOnlyCollection<HoraExtraExportRow>>> Handle(
        ExportOvertimeRecordsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewOvertimeRecordsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<HoraExtraExportRow>>.Failure(authorizationResult.Error);
        }

        var rows = await employeeRepository.GetOvertimeRecordExportRowsAsync(query, cancellationToken);
        return Result<IReadOnlyCollection<HoraExtraExportRow>>.Success(rows);
    }
}

// ── Bandeja de pendientes + insumo de planilla (View — gate per handler) ─────────────────────────────────

internal sealed class ExportOvertimeRecordPendingQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileEmployeeRepository employeeRepository,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<ExportOvertimeRecordPendingQuery, IReadOnlyCollection<HoraExtraPendienteExportRow>>
{
    public async Task<Result<IReadOnlyCollection<HoraExtraPendienteExportRow>>> Handle(
        ExportOvertimeRecordPendingQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewOvertimeRecordsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<HoraExtraPendienteExportRow>>.Failure(authorizationResult.Error);
        }

        var payrollTypeCode = string.IsNullOrWhiteSpace(query.PayrollTypeCode)
            ? null
            : query.PayrollTypeCode.Trim().ToUpperInvariant();

        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow);
        var rows = await employeeRepository.GetOvertimeRecordPendingExportRowsAsync(
            query.CompanyId, payrollTypeCode, query.OnlyOverdue, today, query.MaxRows, cancellationToken);
        return Result<IReadOnlyCollection<HoraExtraPendienteExportRow>>.Success(rows);
    }
}

internal sealed class ExportOvertimeRecordPayrollInputQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileEmployeeRepository employeeRepository,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<ExportOvertimeRecordPayrollInputQuery, IReadOnlyCollection<InsumoPlanillaHoraExtraExportRow>>
{
    public async Task<Result<IReadOnlyCollection<InsumoPlanillaHoraExtraExportRow>>> Handle(
        ExportOvertimeRecordPayrollInputQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewOvertimeRecordsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<InsumoPlanillaHoraExtraExportRow>>.Failure(authorizationResult.Error);
        }

        // The payroll type + period are MANDATORY for the payroll input (§0.16) — a missing bound is a 400 with an
        // actionable code, so the export always maps a bounded destination.
        if (string.IsNullOrWhiteSpace(query.PayrollTypeCode) || string.IsNullOrWhiteSpace(query.PayrollPeriod))
        {
            return Result<IReadOnlyCollection<InsumoPlanillaHoraExtraExportRow>>.Failure(OvertimeRecordBandejaErrors.PayrollInputFilterRequired);
        }

        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow);
        var rows = await employeeRepository.GetOvertimeRecordPayrollInputRowsAsync(
            query.CompanyId,
            query.PayrollTypeCode.Trim().ToUpperInvariant(),
            query.PayrollPeriod.Trim(),
            today,
            query.MaxRows,
            cancellationToken);
        return Result<IReadOnlyCollection<InsumoPlanillaHoraExtraExportRow>>.Success(rows);
    }
}

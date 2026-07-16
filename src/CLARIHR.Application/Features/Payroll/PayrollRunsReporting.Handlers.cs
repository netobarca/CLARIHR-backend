using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Payroll;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.Payroll;

namespace CLARIHR.Application.Features.Payroll;

internal sealed class QueryPayrollRunsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPayrollRunRepository runRepository)
    : IQueryHandler<QueryPayrollRunsQuery, PayrollRunBandejaResponse>
{
    public async Task<Result<PayrollRunBandejaResponse>> Handle(
        QueryPayrollRunsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewPayrollRunsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PayrollRunBandejaResponse>.Failure(authorizationResult.Error);
        }

        var page = await runRepository.QueryRunsAsync(query, cancellationToken);
        return Result<PayrollRunBandejaResponse>.Success(page);
    }
}

internal sealed class ExportPayrollRunsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPayrollRunRepository runRepository)
    : IQueryHandler<ExportPayrollRunsQuery, IReadOnlyCollection<CorridaPlanillaExportRow>>
{
    public async Task<Result<IReadOnlyCollection<CorridaPlanillaExportRow>>> Handle(
        ExportPayrollRunsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewPayrollRunsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<CorridaPlanillaExportRow>>.Failure(authorizationResult.Error);
        }

        var rows = await runRepository.GetRunExportRowsAsync(query, cancellationToken);
        return Result<IReadOnlyCollection<CorridaPlanillaExportRow>>.Success(rows);
    }
}

internal sealed class ExportPayrollRunLinesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPayrollRunRepository runRepository)
    : IQueryHandler<ExportPayrollRunLinesQuery, IReadOnlyCollection<ImpresionPlanillaExportRow>>
{
    public async Task<Result<IReadOnlyCollection<ImpresionPlanillaExportRow>>> Handle(
        ExportPayrollRunLinesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewPayrollRunsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<ImpresionPlanillaExportRow>>.Failure(authorizationResult.Error);
        }

        var rows = await runRepository.GetRunLineExportRowsAsync(
            query.CompanyId, query.PayrollRunId, query.MaxRows, cancellationToken);
        return rows is null
            ? Result<IReadOnlyCollection<ImpresionPlanillaExportRow>>.Failure(PayrollRunErrors.PayrollRunNotFound)
            : Result<IReadOnlyCollection<ImpresionPlanillaExportRow>>.Success(rows);
    }
}

internal sealed class ExportPayrollRunBankReconciliationQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPayrollRunRepository runRepository)
    : IQueryHandler<ExportPayrollRunBankReconciliationQuery, IReadOnlyCollection<ConciliacionBancariaExportRow>>
{
    public async Task<Result<IReadOnlyCollection<ConciliacionBancariaExportRow>>> Handle(
        ExportPayrollRunBankReconciliationQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewPayrollRunsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<ConciliacionBancariaExportRow>>.Failure(authorizationResult.Error);
        }

        var rows = await runRepository.GetBankReconciliationRowsAsync(
            query.CompanyId, query.PayrollRunId, query.MaxRows, cancellationToken);
        return rows is null
            ? Result<IReadOnlyCollection<ConciliacionBancariaExportRow>>.Failure(PayrollRunErrors.PayrollRunNotFound)
            : Result<IReadOnlyCollection<ConciliacionBancariaExportRow>>.Success(rows);
    }
}

internal sealed class QueryPayrollRunEmployeeHistoryQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPayrollRunRepository runRepository)
    : IQueryHandler<QueryPayrollRunEmployeeHistoryQuery, PayrollRunEmployeeHistoryResponse>
{
    public async Task<Result<PayrollRunEmployeeHistoryResponse>> Handle(
        QueryPayrollRunEmployeeHistoryQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewPayrollRunsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PayrollRunEmployeeHistoryResponse>.Failure(authorizationResult.Error);
        }

        // Default = history (CERRADA+AUTORIZADA — REQ-015 P-01); GENERADA/ANULADA only explicitly.
        var statuses = query.StatusCodes is { Count: > 0 }
            ? query.StatusCodes.Select(code => code.Trim().ToUpperInvariant()).Distinct().ToArray()
            : PayrollRunReportingConstants.DefaultHistoryStatuses;

        var page = await runRepository.QueryEmployeeHistoryAsync(
            query.CompanyId, query.PersonnelFilePublicId, query.Year, query.PayrollDefinitionPublicId,
            query.PayrollTypeCode, statuses, query.From, query.To, query.PageNumber, query.PageSize,
            cancellationToken);
        return Result<PayrollRunEmployeeHistoryResponse>.Success(page);
    }
}

internal sealed class GetMyPayrollHistoryQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPayrollRunRepository runRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : IQueryHandler<GetMyPayrollHistoryQuery, PayrollRunEmployeeHistoryResponse>
{
    public async Task<Result<PayrollRunEmployeeHistoryResponse>> Handle(
        GetMyPayrollHistoryQuery query,
        CancellationToken cancellationToken)
    {
        var gate = await PayrollRunSelfOrViewGate.EnsureAsync(
            authorizationService, personnelFileRepository, currentUserService, tenantContext,
            query.PersonnelFilePublicId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<PayrollRunEmployeeHistoryResponse>.Failure(gate.Error);
        }

        // FIXED states (REQ-015 RF-005): the employee's own surface never sees drafts or annulments.
        var page = await runRepository.QueryEmployeeHistoryAsync(
            gate.Value, query.PersonnelFilePublicId, query.Year, payrollDefinitionPublicId: null,
            payrollTypeCode: null, PayrollRunReportingConstants.DefaultHistoryStatuses,
            from: null, to: null, query.PageNumber, query.PageSize, cancellationToken);
        return Result<PayrollRunEmployeeHistoryResponse>.Success(page);
    }
}

internal sealed class GetMyPayrollHistoryRunLinesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPayrollRunRepository runRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : IQueryHandler<GetMyPayrollHistoryRunLinesQuery, PayrollRunEmployeeLinesResponse>
{
    public async Task<Result<PayrollRunEmployeeLinesResponse>> Handle(
        GetMyPayrollHistoryRunLinesQuery query,
        CancellationToken cancellationToken)
    {
        var gate = await PayrollRunSelfOrViewGate.EnsureAsync(
            authorizationService, personnelFileRepository, currentUserService, tenantContext,
            query.PersonnelFilePublicId, cancellationToken);
        if (gate.IsFailure)
        {
            return Result<PayrollRunEmployeeLinesResponse>.Failure(gate.Error);
        }

        var run = await runRepository.GetByIdAsync(query.PayrollRunId, cancellationToken);
        if (run is null || run.TenantId != gate.Value ||
            !PayrollRunReportingConstants.DefaultHistoryStatuses.Contains(run.StatusCode))
        {
            // A GENERADA/ANULADA run does NOT exist on the employee's surface (fixed states — RF-005).
            return Result<PayrollRunEmployeeLinesResponse>.Failure(PayrollRunErrors.PayrollRunNotFound);
        }

        var lines = run.Lines
            .Where(line => line.EmployeePublicId == query.PersonnelFilePublicId)
            .OrderBy(line => line.SortOrder)
            .ThenBy(line => line.PublicId)
            .ToArray();
        if (lines.Length == 0)
        {
            return Result<PayrollRunEmployeeLinesResponse>.Failure(PayrollRunReviewErrors.LineNotFound);
        }

        var income = lines.Where(line => line is { LineClass: PayrollLineClasses.Ingreso, IsIncluded: true }).Sum(line => line.FinalAmount);
        var deductions = lines.Where(line => line is { LineClass: PayrollLineClasses.Descuento, IsIncluded: true }).Sum(line => line.FinalAmount);

        return Result<PayrollRunEmployeeLinesResponse>.Success(new PayrollRunEmployeeLinesResponse(
            run.PublicId,
            query.PersonnelFilePublicId,
            lines[0].EmployeeName,
            income,
            deductions,
            income - deductions,
            lines.Select(PayrollRunReviewSupport.ToLineResponse).ToArray()));
    }
}

/// <summary>
/// The REQ-015 read gate (its P-03, molde medical-claims/D-13): the caller passes with the corporate
/// ViewPayrollRuns grant OR by being the employee LINKED to the file. Returns the file's tenant id.
/// Cross-tenant/unknown files answer NOT FOUND — the surface never reveals foreign expedientes.
/// </summary>
internal static class PayrollRunSelfOrViewGate
{
    public static async Task<Result<Guid>> EnsureAsync(
        IPersonnelFileAuthorizationService authorizationService,
        IPersonnelFileRepository personnelFileRepository,
        ICurrentUserService currentUserService,
        ITenantContext tenantContext,
        Guid personnelFilePublicId,
        CancellationToken cancellationToken)
    {
        if (!tenantContext.TenantId.HasValue)
        {
            return Result<Guid>.Failure(AuthorizationErrors.Unauthenticated);
        }

        var personnelFile = await personnelFileRepository.GetForAccessCheckAsync(personnelFilePublicId, cancellationToken);
        if (personnelFile is null || personnelFile.TenantId != tenantContext.TenantId.Value)
        {
            return Result<Guid>.Failure(PersonnelFileErrors.NotFound);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var currentUserId);
        if (personnelFile.LinkedUserPublicId is { } linkedUser && linkedUser == currentUserId)
        {
            return Result<Guid>.Success(personnelFile.TenantId);
        }

        var viewResult = await authorizationService.EnsureCanViewPayrollRunsAsync(personnelFile.TenantId, cancellationToken);
        return viewResult.IsFailure
            ? Result<Guid>.Failure(viewResult.Error)
            : Result<Guid>.Success(personnelFile.TenantId);
    }
}

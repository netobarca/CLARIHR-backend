using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.Payroll;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

/// <summary>
/// The payroll-run bandeja, its exports (bandeja · payroll print · bank reconciliation) and the corporate
/// employee-history query (REQ-012 §3.7 — REQ-013 RF-001/002/003 · REQ-015 RF-001). Deliberately NOT
/// annotated with [AuthorizationPolicySet]: the query POSTs are reads — everything gates per handler on
/// <c>ViewPayrollRuns</c> (payroll data exposes salaries; corporate reads are HR-only).
/// </summary>
[ApiController]
[Authorize]
[Tags("Payroll Runs")]
public sealed class PayrollRunsReportingController(
    IQueryDispatcher queryDispatcher,
    ReportExportDeliveryService reportExportDeliveryService) : ControllerBase
{
    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
    [HttpPost("api/v1/companies/{companyId:guid}/payroll-runs/query")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PayrollRunBandejaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "Query the payroll-run bandeja",
        Description = """
            Paginated and filterable (Nómina, period, status, year) over the PERSISTED run header — the
            totals are what the review froze, never recomputed (REQ-013 P-03). **`statusCounts` always
            spans EVERY status** — they are the numbers of the tabs, so do not recompute them from
            `items`.
            """)]
    public async Task<ActionResult<PayrollRunBandejaResponse>> Query(
        Guid companyId,
        [FromBody] QueryPayrollRunsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new QueryPayrollRunsQuery(
                companyId,
                request.PayrollDefinitionPublicId,
                request.PayrollPeriodPublicId,
                request.StatusCode,
                request.Year,
                request.PageNumber ?? 1,
                request.PageSize ?? 25),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
    [HttpGet("api/v1/companies/{companyId:guid}/payroll-runs/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [SwaggerOperation(Summary = "Export the payroll-run bandeja to Excel/CSV/JSON")]
    public async Task<IActionResult> Export(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] Guid? payrollDefinitionPublicId = null,
        [FromQuery] Guid? payrollPeriodPublicId = null,
        [FromQuery] string? statusCode = null,
        [FromQuery] int? year = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportPayrollRunsQuery(
                companyId, payrollDefinitionPublicId, payrollPeriodPublicId, statusCode, year,
                reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(
                Result<IReadOnlyCollection<CorridaPlanillaExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "payroll-runs",
            "PayrollRuns",
            AuditEntityTypes.PayrollRun,
            "PAYROLL_RUNS",
            "Exported the payroll-run bandeja.",
            new { payrollDefinitionPublicId, payrollPeriodPublicId, statusCode, year },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
    [HttpGet("api/v1/companies/{companyId:guid}/payroll-runs/{payrollRunId:guid}/lines/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [SwaggerOperation(
        Summary = "Export the payroll print (run lines)",
        Description = """
            The payroll print (REQ-013 RF-003): every line — employee, cost center, concept, class, units,
            base, calculated amount, audited override and final amount, inclusion flag and source module —
            as `DETALLE` rows, followed by `TOTAL_POR_CONCEPTO` and `TOTAL_POR_CENTRO_COSTO` summary rows
            computed over the INCLUDED lines.
            """)]
    public async Task<IActionResult> ExportLines(
        Guid companyId,
        Guid payrollRunId,
        [FromQuery] string format = "xlsx",
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportPayrollRunLinesQuery(companyId, payrollRunId, reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(
                Result<IReadOnlyCollection<ImpresionPlanillaExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "payroll-run-lines",
            "PayrollRunLines",
            AuditEntityTypes.PayrollRun,
            "PAYROLL_RUN_LINES",
            "Exported the payroll print (run lines).",
            new { payrollRunId },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
    [HttpGet("api/v1/companies/{companyId:guid}/payroll-runs/{payrollRunId:guid}/bank-reconciliation/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [SwaggerOperation(
        Summary = "Export the bank reconciliation of a payroll run",
        Description = """
            One row per employee of the run: payment method → bank → account (the profile's designated
            payment account, else the PRIMARY one) with the employee's net over the INCLUDED lines. An
            employee without a resolvable account still travels — the row carries the
            `PAYROLL_WARNING_NO_BANK_ACCOUNT` warning instead of blocking the export.
            """)]
    public async Task<IActionResult> ExportBankReconciliation(
        Guid companyId,
        Guid payrollRunId,
        [FromQuery] string format = "xlsx",
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportPayrollRunBankReconciliationQuery(
                companyId, payrollRunId, reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(
                Result<IReadOnlyCollection<ConciliacionBancariaExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "payroll-run-bank-reconciliation",
            "BankReconciliation",
            AuditEntityTypes.PayrollRun,
            "PAYROLL_RUN_BANK_RECONCILIATION",
            "Exported the payroll-run bank reconciliation.",
            new { payrollRunId },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
    [HttpPost("api/v1/companies/{companyId:guid}/payroll-runs/employee-history/query")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PayrollRunEmployeeHistoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "Query an employee's payroll history across runs",
        Description = """
            The EMPLOYEE axis of the motor (REQ-015 RF-001): one row per run where the employee has
            INCLUDED lines, with THEIR sums (income/deductions/net), newest first. Default statuses are
            `CERRADA`+`AUTORIZADA` (the payment history); passing `statusCodes: ["GENERADA"]` turns the
            SAME endpoint into the open-period actions/events view. The per-run drill is
            `GET …/payroll-runs/{id}/employees/{personnelFilePublicId}`.
            """)]
    public async Task<ActionResult<PayrollRunEmployeeHistoryResponse>> QueryEmployeeHistory(
        Guid companyId,
        [FromBody] QueryPayrollRunEmployeeHistoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new QueryPayrollRunEmployeeHistoryQuery(
                companyId,
                request.PersonnelFilePublicId,
                request.Year,
                request.PayrollDefinitionPublicId,
                request.PayrollTypeCode,
                request.StatusCodes,
                request.From,
                request.To,
                request.PageNumber ?? 1,
                request.PageSize ?? 25),
            cancellationToken);

        return this.ToActionResult(result);
    }

    public sealed record QueryPayrollRunsRequest(
        Guid? PayrollDefinitionPublicId,
        Guid? PayrollPeriodPublicId,
        string? StatusCode,
        int? Year,
        int? PageNumber,
        int? PageSize);

    public sealed record QueryPayrollRunEmployeeHistoryRequest(
        Guid PersonnelFilePublicId,
        int? Year,
        Guid? PayrollDefinitionPublicId,
        string? PayrollTypeCode,
        IReadOnlyCollection<string>? StatusCodes,
        DateOnly? From,
        DateOnly? To,
        int? PageNumber,
        int? PageSize);
}

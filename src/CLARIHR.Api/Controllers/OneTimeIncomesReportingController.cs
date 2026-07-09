using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

/// <summary>
/// Company-wide one-time-income operations ("planilla — ingresos eventuales", REQ-006): the atomic apply-period
/// batch (RF-012), the pending/overdue tray, the advanced search + aggregation (RF-008 / №14), and the tabular
/// exports (bandeja, pending, payroll input, §5). Intentionally NOT annotated with [AuthorizationPolicySet] (the
/// convention would assign the Manage policy to the POST reads — producing false 403s for view-only users);
/// authorization is enforced per handler (Manage for the batch, View for the reads). Precedent: settlements /
/// recurring-incomes reporting controllers.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
public sealed class OneTimeIncomesReportingController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    ReportExportDeliveryService reportExportDeliveryService) : ControllerBase
{
    [HttpPost("api/v1/companies/{companyId:guid}/one-time-incomes/apply-period")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<OneTimeIncomeApplyPeriodResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Apply the AUTORIZADO one-time incomes of a payroll period",
        Description = """
            Applies, ATOMICALLY, every `AUTORIZADO` one-time income of the company of the given `payrollTypeCode`
            (RF-012) — including the "atrasados" whose declared period already passed. Provide a
            `payrollPeriodPublicId` (FK real; its id/label are snapshotted onto the applications) or a bare
            `payrollPeriodLabel` to override the destination for every applied income; omit both to default each
            application to its income's declared destination. `excludedIncomePublicIds` postpones incomes (they stay
            AUTORIZADO for the next run). Any conflict rolls the whole batch back (422). HR-only
            (`ManageOneTimeIncomes`). Returns the count of applied and postponed incomes.
            """)]
    public async Task<ActionResult<OneTimeIncomeApplyPeriodResult>> ApplyPeriod(
        Guid companyId,
        [FromBody] ApplyOneTimeIncomePeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ApplyOneTimeIncomePeriodCommand(
                companyId,
                request.PayrollTypeCode,
                request.PayrollPeriodPublicId,
                request.PayrollPeriodLabel,
                request.ExcludedIncomePublicIds ?? []),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
    [HttpPost("api/v1/companies/{companyId:guid}/one-time-incomes/pending/query")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<OneTimeIncomePendingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "Query the pending/overdue one-time incomes tray",
        Description = """
            Returns the company's `AUTORIZADO` one-time incomes without an active application (RF-012) — the ones
            still to be applied — each marked `isOverdue` when its declared payroll-period end date already passed.
            Optionally filter by `payrollTypeCode` and/or `onlyOverdue`. HR-only (`ViewOneTimeIncomes`).
            """)]
    public async Task<ActionResult<OneTimeIncomePendingResponse>> QueryPending(
        Guid companyId,
        [FromBody] QueryOneTimeIncomePendingRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new QueryOneTimeIncomePendingQuery(companyId, request.PayrollTypeCode, request.OnlyOverdue ?? false),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
    [HttpPost("api/v1/companies/{companyId:guid}/one-time-incomes/query")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<OneTimeIncomeBandejaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "Query the company-wide one-time-income bandeja (advanced search + aggregation)",
        Description = """
            Returns a paginated, filterable list of the company's one-time incomes (RF-008: by `statusCodes[]`,
            `employeeId`, `conceptTypeCode`, `fromDate`/`toDate`, `isFixedValue`, `payrollTypeCode`, `payrollPeriod`,
            `costCenterPublicId`, `currencyCode`, `requesterFilePublicId`, `search`), plus per-status counts (span
            every status), the amount totals BY CURRENCY (RN-13). When `groupBy` is present the response also carries
            the aggregation buckets (composite key (dimension, currency); allowed dimensions: `estado`, `tipo`,
            `empleado`, `tipoPlanilla`, `periodo`, `centroCosto`, `moneda`, `mes` — an invalid dimension → 400). The
            groups CUADRAN against the flat totals of the same filter. HR-only (`ViewOneTimeIncomes`).
            """)]
    public async Task<ActionResult<OneTimeIncomeBandejaResponse>> Query(
        Guid companyId,
        [FromBody] QueryOneTimeIncomesRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new QueryOneTimeIncomesQuery(
                companyId,
                request.StatusCodes,
                request.EmployeeId,
                request.ConceptTypeCode,
                request.FromDate,
                request.ToDate,
                request.IsFixedValue,
                request.PayrollTypeCode,
                request.PayrollPeriod,
                request.CostCenterPublicId,
                request.CurrencyCode,
                request.RequesterFilePublicId,
                request.Search,
                request.GroupBy,
                request.PageNumber ?? 1,
                request.PageSize ?? 25),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
    [HttpGet("api/v1/companies/{companyId:guid}/one-time-incomes/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [SwaggerOperation(
        Summary = "Export the one-time-income bandeja to Excel/CSV/JSON",
        Description = """
            Exports the filtered one-time incomes (same filters as the bandeja) to `xlsx`, `csv` or `json` with the
            header, value, destination, requester, status and the registrar / decider ids. HR-only. Synchronous
            download capped at the configured row limit (`413` if exceeded).
            """)]
    public async Task<IActionResult> Export(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] string[]? statusCodes = null,
        [FromQuery] Guid? employeeId = null,
        [FromQuery] string? conceptTypeCode = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] bool? isFixedValue = null,
        [FromQuery] string? payrollTypeCode = null,
        [FromQuery] string? payrollPeriod = null,
        [FromQuery] Guid? costCenterPublicId = null,
        [FromQuery] string? currencyCode = null,
        [FromQuery] Guid? requesterFilePublicId = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportOneTimeIncomesQuery(
                companyId,
                statusCodes,
                employeeId,
                conceptTypeCode,
                fromDate,
                toDate,
                isFixedValue,
                payrollTypeCode,
                payrollPeriod,
                costCenterPublicId,
                currencyCode,
                requesterFilePublicId,
                search,
                reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<IngresoEventualExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "one-time-incomes",
            "OneTimeIncomes",
            AuditEntityTypes.PersonnelFile,
            "ONE_TIME_INCOMES",
            "Exported one-time incomes report.",
            new
            {
                statusCodes,
                employeeId,
                conceptTypeCode,
                fromDate,
                toDate,
                isFixedValue,
                payrollTypeCode,
                payrollPeriod,
                costCenterPublicId,
                currencyCode,
                requesterFilePublicId,
                search
            },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
    [HttpGet("api/v1/companies/{companyId:guid}/one-time-incomes/pending/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [SwaggerOperation(
        Summary = "Export the pending / overdue one-time incomes to Excel/CSV/JSON",
        Description = """
            Exports the pending / overdue tray (the AUTORIZADO one-time incomes without an active application, RF-012)
            to `xlsx`, `csv` or `json`, each marked `Vencido` when its declared payroll-period end date already passed.
            Optionally filter by `payrollTypeCode` and/or `onlyOverdue`. HR-only. Synchronous download capped (`413`).
            """)]
    public async Task<IActionResult> ExportPending(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] string? payrollTypeCode = null,
        [FromQuery] bool onlyOverdue = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportOneTimeIncomePendingQuery(
                companyId,
                payrollTypeCode,
                onlyOverdue,
                reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<IngresoEventualPendienteExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "one-time-income-pending",
            "PendingOneTimeIncomes",
            AuditEntityTypes.PersonnelFile,
            "ONE_TIME_INCOME_PENDING",
            "Exported one-time-income pending report.",
            new
            {
                payrollTypeCode,
                onlyOverdue
            },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
    [HttpGet("api/v1/companies/{companyId:guid}/one-time-incomes/payroll-input/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [SwaggerOperation(
        Summary = "Export the one-time-income payroll input for an external payroll system",
        Description = """
            Exports the pending (AUTORIZADO, not yet applied) one-time incomes of a MANDATORY `payrollTypeCode` +
            `payrollPeriod` (matched against the declared period label) — one row per income with the employee,
            concept, payroll type, period, amount, currency and cost center (§5). This is the bridge with the external
            payroll while the internal engine does not exist; it cuadra EXACTLY against the pending tray of the same
            filter (excludes annulled and applied). A missing payroll type or period yields `400`. HR-only.
            Synchronous download capped (`413`).
            """)]
    public async Task<IActionResult> ExportPayrollInput(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] string? payrollTypeCode = null,
        [FromQuery] string? payrollPeriod = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportOneTimeIncomePayrollInputQuery(
                companyId,
                payrollTypeCode,
                payrollPeriod,
                reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<InsumoPlanillaEventualExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "one-time-income-payroll-input",
            "PayrollInput",
            AuditEntityTypes.PersonnelFile,
            "ONE_TIME_INCOME_PAYROLL_INPUT",
            "Exported one-time-income payroll input.",
            new
            {
                payrollTypeCode,
                payrollPeriod
            },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }
}

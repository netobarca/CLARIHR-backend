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
/// Company-wide overtime-record operations ("horas extras del empleado", REQ-007): the atomic apply-period batch
/// (RF-012), the pending/overdue tray, the advanced search with per-status counts + totals EN HORAS (RF-011 /
/// §0.16), and the tabular exports (bandeja, pending, payroll input). Intentionally NOT annotated with
/// [AuthorizationPolicySet] (the convention would assign the Manage policy to the POST reads — producing false
/// 403s for view-only users); authorization is enforced per handler (Manage for the batch, View for the reads).
/// Precedent: settlements / recurring-incomes / one-time-incomes reporting controllers.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
public sealed class OvertimeRecordsReportingController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    ReportExportDeliveryService reportExportDeliveryService) : ControllerBase
{
    [HttpPost("api/v1/companies/{companyId:guid}/overtime-records/apply-period")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<OvertimeApplyPeriodResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Apply the AUTORIZADA overtime records of a payroll period",
        Description = """
            Applies, ATOMICALLY, every `AUTORIZADA` overtime record of the company of the given `payrollTypeCode`
            (RF-012) whose work date has elapsed — including the "atrasados" whose declared period already passed;
            future organized shifts are excluded. Provide a `payrollPeriodPublicId` (FK real; its id/label are
            snapshotted onto the applications) or a bare `payrollPeriodLabel` to override the destination for every
            applied record; omit both to default each application to its record's declared destination.
            `excludedRecordPublicIds` postpones records (they stay AUTORIZADA for the next run). Any conflict rolls
            the whole batch back (422). HR-only (`ManageOvertimeRecords`). Returns the count of applied and postponed
            records.
            """)]
    public async Task<ActionResult<OvertimeApplyPeriodResult>> ApplyPeriod(
        Guid companyId,
        [FromBody] ApplyOvertimePeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ApplyOvertimePeriodCommand(
                companyId,
                request.PayrollTypeCode,
                request.PayrollPeriodPublicId,
                request.PayrollPeriodLabel,
                request.ExcludedRecordPublicIds ?? []),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
    [HttpPost("api/v1/companies/{companyId:guid}/overtime-records/pending/query")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<OvertimeRecordPendingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "Query the pending/overdue overtime records tray",
        Description = """
            Returns the company's `AUTORIZADA` overtime records without an active application (RF-012) — the ones
            still to be applied — each marked `isOverdue` when its declared payroll-period end date already passed.
            Optionally filter by `payrollTypeCode` and/or `onlyOverdue`. HR-only (`ViewOvertimeRecords`).
            """)]
    public async Task<ActionResult<OvertimeRecordPendingResponse>> QueryPending(
        Guid companyId,
        [FromBody] QueryOvertimeRecordPendingRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new QueryOvertimeRecordPendingQuery(companyId, request.PayrollTypeCode, request.OnlyOverdue ?? false),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
    [HttpPost("api/v1/companies/{companyId:guid}/overtime-records/query")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<OvertimeRecordBandejaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "Query the company-wide overtime bandeja (advanced search)",
        Description = """
            Returns a paginated, filterable list of the company's overtime records (RF-011: by `statusCodes[]`,
            `employeeId`, `overtimeTypePublicId`, `justificationTypePublicId`, `fromWorkDate`/`toWorkDate`,
            `payrollTypeCode`, `payrollPeriod`, `requesterFilePublicId`, `originChannel` (RRHH/PORTAL),
            `assignedPositionPublicId`, `search`), plus per-status counts (span every status), the global total HOURS
            of the filtered set and the totals-by-type buckets (`{overtimeTypeCode, overtimeTypeName, count,
            totalHours}` — a GroupBy over the decimal hours; Σ totalHours == TotalHours). Totals are EN HORAS — the
            module carries no money (unlike the REQ-006 groupBy); there is NO dimensional groupBy. HR-only
            (`ViewOvertimeRecords`).
            """)]
    public async Task<ActionResult<OvertimeRecordBandejaResponse>> Query(
        Guid companyId,
        [FromBody] QueryOvertimeRecordsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new QueryOvertimeRecordsQuery(
                companyId,
                request.StatusCodes,
                request.EmployeeId,
                request.OvertimeTypePublicId,
                request.JustificationTypePublicId,
                request.FromWorkDate,
                request.ToWorkDate,
                request.PayrollTypeCode,
                request.PayrollPeriod,
                request.RequesterFilePublicId,
                request.OriginChannel,
                request.AssignedPositionPublicId,
                request.Search,
                request.PageNumber ?? 1,
                request.PageSize ?? 25),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
    [HttpGet("api/v1/companies/{companyId:guid}/overtime-records/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [SwaggerOperation(
        Summary = "Export the overtime bandeja to Excel/CSV/JSON",
        Description = """
            Exports the filtered overtime records (same filters as the bandeja) to `xlsx`, `csv` or `json` with the
            shift, type, factor, decimal hours, channel, destination, requester, status and the registrar / decider
            ids. HR-only. Synchronous download capped at the configured row limit (`413` if exceeded).
            """)]
    public async Task<IActionResult> Export(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] string[]? statusCodes = null,
        [FromQuery] Guid? employeeId = null,
        [FromQuery] Guid? overtimeTypePublicId = null,
        [FromQuery] Guid? justificationTypePublicId = null,
        [FromQuery] DateOnly? fromWorkDate = null,
        [FromQuery] DateOnly? toWorkDate = null,
        [FromQuery] string? payrollTypeCode = null,
        [FromQuery] string? payrollPeriod = null,
        [FromQuery] Guid? requesterFilePublicId = null,
        [FromQuery] string? originChannel = null,
        [FromQuery] Guid? assignedPositionPublicId = null,
        [FromQuery] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportOvertimeRecordsQuery(
                companyId,
                statusCodes,
                employeeId,
                overtimeTypePublicId,
                justificationTypePublicId,
                fromWorkDate,
                toWorkDate,
                payrollTypeCode,
                payrollPeriod,
                requesterFilePublicId,
                originChannel,
                assignedPositionPublicId,
                search,
                reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<HoraExtraExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "overtime-records",
            "OvertimeRecords",
            AuditEntityTypes.PersonnelFile,
            "OVERTIME_RECORDS",
            "Exported overtime records report.",
            new
            {
                statusCodes,
                employeeId,
                overtimeTypePublicId,
                justificationTypePublicId,
                fromWorkDate,
                toWorkDate,
                payrollTypeCode,
                payrollPeriod,
                requesterFilePublicId,
                originChannel,
                assignedPositionPublicId,
                search
            },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
    [HttpGet("api/v1/companies/{companyId:guid}/overtime-records/pending/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [SwaggerOperation(
        Summary = "Export the pending / overdue overtime records to Excel/CSV/JSON",
        Description = """
            Exports the pending / overdue tray (the AUTORIZADA overtime records without an active application, RF-012)
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
            new ExportOvertimeRecordPendingQuery(
                companyId,
                payrollTypeCode,
                onlyOverdue,
                reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<HoraExtraPendienteExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "overtime-records-pending",
            "PendingOvertimeRecords",
            AuditEntityTypes.PersonnelFile,
            "OVERTIME_RECORDS_PENDING",
            "Exported overtime-record pending report.",
            new
            {
                payrollTypeCode,
                onlyOverdue
            },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
    [HttpGet("api/v1/companies/{companyId:guid}/overtime-records/payroll-input/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [SwaggerOperation(
        Summary = "Export the overtime payroll input for an external payroll system",
        Description = """
            Exports the pending (AUTORIZADA, not yet applied, elapsed, NOT compensated) overtime records of a
            MANDATORY `payrollTypeCode` + `payrollPeriod` (matched against the declared period label) — one row per
            record with the employee, overtime type, factor, decimal hours, payroll type, period and the cost center
            derived from the plaza (D-12; §0.16). This is the bridge with the external payroll while the internal
            engine does not exist; it cuadra against the pending tray of the same filter (excludes annulled + applied
            + compensated + future). A missing payroll type or period yields `400`. HR-only. Synchronous download
            capped (`413`).
            """)]
    public async Task<IActionResult> ExportPayrollInput(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] string? payrollTypeCode = null,
        [FromQuery] string? payrollPeriod = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportOvertimeRecordPayrollInputQuery(
                companyId,
                payrollTypeCode,
                payrollPeriod,
                reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<InsumoPlanillaHoraExtraExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "overtime-payroll-input",
            "OvertimePayrollInput",
            AuditEntityTypes.PersonnelFile,
            "OVERTIME_PAYROLL_INPUT",
            "Exported overtime payroll input.",
            new
            {
                payrollTypeCode,
                payrollPeriod
            },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }
}

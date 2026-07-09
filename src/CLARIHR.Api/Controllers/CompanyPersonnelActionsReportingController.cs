using CLARIHR.Api.Common;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Application.Features.PersonnelFiles.Reporting;
using CLARIHR.Application.Features.Reports.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

/// <summary>
/// Company-wide personnel-actions bandeja and export (RF-016/RF-017) — the drill of the documentary-actions
/// journal to the row level at the tenant scope. Read-only under the dashboard reader permission
/// (<c>ViewReports</c> ∨ <c>Read</c> ∨ <c>Admin</c> — D-16). Intentionally NOT annotated with
/// <c>[AuthorizationPolicySet]</c>: the convention would assign the Manage policy to the POST query (a READ),
/// producing false 403s for view-only users (aclaración №11); authorization is enforced per handler via
/// <c>EnsureCanViewReportsAsync</c>, mirroring <see cref="SettlementsReportingController"/> and
/// <see cref="PersonnelFileReportingController"/>. SIN MONTOS (aclaración №8): no amount/currency is exposed.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
public sealed class CompanyPersonnelActionsReportingController(
    IQueryDispatcher queryDispatcher,
    ReportExportDeliveryService reportExportDeliveryService) : ControllerBase
{
    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
    [HttpPost("api/v1/companies/{companyId:guid}/personnel-actions/query")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelActionBandejaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "Query the company-wide personnel-actions bandeja (journal drill)",
        Description = """
            Returns a paginated, filterable list of the tenant's personnel-action journal entries (the drill of
            the documentary-actions dashboard section to the row level) plus per-status counts. Filters: action
            type, status, origin (`isSystemGenerated` — false=manual, true=automático), a date window
            (`year`/`month`, or a `fromUtc`/`toUtc` range — defaults to the current year), employee
            (`employeePublicId`) and the common dimension filters (`functionalAreaPublicId`, `orgUnitPublicId`,
            `positionCategoryPublicId`, `jobProfilePublicId`, `workCenterPublicId`, `payrollTypeCode`,
            `costCenterPublicId`, scoped by the employee's CURRENT active-primary assignment — D-07). The
            `statusCounts` span EVERY status in the filtered set (they ignore the status filter). Each row carries
            the employee, code, type, status, origin, action date, effective-from/to dates and
            description/reference — NO monetary fields (aclaración №8). Requires the ViewReports or Read
            permission (gated in the handler).
            """)]
    public async Task<ActionResult<PersonnelActionBandejaResponse>> Query(
        Guid companyId,
        [FromBody] QueryCompanyPersonnelActionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new QueryCompanyPersonnelActionsQuery(
                companyId,
                request.ActionTypeCode,
                request.ActionStatusCode,
                request.IsSystemGenerated,
                request.Year,
                request.Month,
                request.FromUtc,
                request.ToUtc,
                request.EmployeeId,
                request.FunctionalAreaId,
                request.OrgUnitId,
                request.PositionCategoryId,
                request.JobProfileId,
                request.WorkCenterId,
                request.PayrollTypeCode,
                request.CostCenterId,
                request.PageNumber ?? 1,
                request.PageSize ?? 25),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
    [HttpGet("api/v1/companies/{companyId:guid}/personnel-actions/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [SwaggerOperation(
        Summary = "Export the company-wide personnel-actions bandeja to Excel/CSV/JSON (synchronous)",
        Description = """
            Streams the filtered personnel-actions bandeja as a file (default `xlsx`, also `csv`/`json`) with the
            documentary columns in Spanish (empleado, código, tipo, estado, origen, fecha del asiento, vigencias,
            descripción, referencia) — NO monetary column (aclaración №8). Same filters as the bandeja query.
            This synchronous path is capped at the configured synchronous row limit; when the filtered result
            would exceed it the endpoint responds `413 Payload Too Large`. For larger exports submit an
            asynchronous report export job instead (`POST /api/v1/companies/{companyId}/report-export-jobs` with
            `resourceKey=COMPANY_PERSONNEL_ACTIONS`), then poll `GET /api/v1/report-export-jobs/{jobId}` and
            download its artifact. Requires the ViewReports or Read permission (gated in the handler).
            """)]
    public async Task<IActionResult> Export(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] string? actionTypeCode = null,
        [FromQuery] string? actionStatusCode = null,
        [FromQuery] bool? isSystemGenerated = null,
        [FromQuery] int? year = null,
        [FromQuery] int? month = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] Guid? employeeId = null,
        [FromQuery] Guid? functionalAreaId = null,
        [FromQuery] Guid? orgUnitId = null,
        [FromQuery] Guid? positionCategoryId = null,
        [FromQuery] Guid? jobProfileId = null,
        [FromQuery] Guid? workCenterId = null,
        [FromQuery] string? payrollTypeCode = null,
        [FromQuery] Guid? costCenterId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportCompanyPersonnelActionsQuery(
                companyId,
                actionTypeCode,
                actionStatusCode,
                isSystemGenerated,
                year,
                month,
                fromUtc,
                toUtc,
                employeeId,
                functionalAreaId,
                orgUnitId,
                positionCategoryId,
                jobProfileId,
                workCenterId,
                payrollTypeCode,
                costCenterId,
                reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<AsientoPersonalExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "personnel-actions",
            "PersonnelActions",
            AuditEntityTypes.PersonnelFile,
            ReportExportResources.CompanyPersonnelActions,
            "Exported company personnel-actions bandeja.",
            new
            {
                actionTypeCode,
                actionStatusCode,
                isSystemGenerated,
                year,
                month,
                fromUtc,
                toUtc,
                employeeId,
                functionalAreaId,
                orgUnitId,
                positionCategoryId,
                jobProfileId,
                workCenterId,
                payrollTypeCode,
                costCenterId
            },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }
}

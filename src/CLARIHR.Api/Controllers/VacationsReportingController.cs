using CLARIHR.Api.Common;
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
/// Company-wide vacation-requests bandeja + export and the yearly vacation calendar (leave module §3.7/§3.9).
/// Intentionally NOT annotated with [AuthorizationPolicySet]: the convention would assign the Manage policy to
/// the POST `query` (a READ), producing false 403s for view-only users; authorization is enforced per handler
/// via <c>EnsureCanViewVacationsAsync</c> (like the incapacities reporting controller).
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
public sealed class VacationsReportingController(
    IQueryDispatcher queryDispatcher,
    ReportExportDeliveryService reportExportDeliveryService) : ControllerBase
{
    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
    [HttpPost("api/v1/companies/{companyId:guid}/vacation-requests/query")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<VacationRequestBandejaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "Query the company-wide vacation-requests bandeja",
        Description = """
            Returns a paginated, filterable list of the company's vacation requests (by employee, status and
            start-date range) with the requested / consumed / returned / net days, plus per-status counts. The
            StatusCounts cover every status. HR-only (`ViewVacations`).
            """)]
    public async Task<ActionResult<VacationRequestBandejaResponse>> QueryVacationRequests(
        Guid companyId,
        [FromBody] QueryVacationRequestsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new QueryVacationRequestsQuery(
                companyId,
                request.EmployeeId,
                request.StatusCode,
                request.StartFromUtc,
                request.StartToUtc,
                request.PageNumber ?? 1,
                request.PageSize ?? 25),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
    [HttpGet("api/v1/companies/{companyId:guid}/vacation-requests/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [SwaggerOperation(
        Summary = "Export the enjoyed vacations (goces) to Excel/CSV/JSON",
        Description = """
            Exports the enjoyed vacations — approved / partially-returned / returned requests
            (APROBADA / DEVUELTA_PARCIAL / DEVUELTA) — with the enjoyment window, the requested / consumed /
            returned / net days and the periods of origin (year: days). Optionally filtered by employee and
            start-date range. HR-only (`ViewVacations`). Synchronous download capped at the configured row limit
            (`413` if exceeded).
            """)]
    public async Task<IActionResult> ExportVacationRequests(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] Guid? employeeId = null,
        [FromQuery] DateTime? startFromUtc = null,
        [FromQuery] DateTime? startToUtc = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportVacationRequestsQuery(
                companyId,
                employeeId,
                startFromUtc,
                startToUtc,
                reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<GoceVacacionesExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "vacation-enjoyments",
            "VacationEnjoyments",
            AuditEntityTypes.PersonnelFile,
            "VACATION_ENJOYMENTS",
            "Exported vacation enjoyments report.",
            new { employeeId, startFromUtc, startToUtc },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }

    [HttpGet("api/v1/companies/{companyId:guid}/vacations/calendar")]
    [Produces("application/json")]
    [ProducesResponseType<VacationCalendarResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "Get the company vacation calendar of a year",
        Description = """
            Returns the company's vacation calendar for `year`: the enjoyed windows (approved / partially-returned
            / returned requests overlapping the year) and the planned windows (lines of the VIGENTE plans of the
            year), each with the employee and the dates/days. HR-only (`ViewVacations`).
            """)]
    public async Task<ActionResult<VacationCalendarResponse>> GetVacationsCalendar(
        Guid companyId,
        [FromQuery] int year,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetVacationCalendarQuery(companyId, year), cancellationToken);
        return this.ToActionResult(result);
    }
}

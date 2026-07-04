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
/// Company-wide retirement-request bandeja and export (RF-002) — HR-only (RN-002.1, ratified: exclusive to
/// HR, no manager/team visibility in Fase 1). Intentionally NOT annotated with [AuthorizationPolicySet]: the
/// convention would assign the Manage policy to the POST query (a READ), producing false 403s for view-only
/// users; authorization is enforced per handler via <c>EnsureCanViewRetirementsAsync</c> (and the dual
/// <c>EnsureCanViewRetirementInterviewTrayAsync</c> gate for the interview tray).
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
public sealed class RetirementRequestsReportingController(
    IQueryDispatcher queryDispatcher,
    ReportExportDeliveryService reportExportDeliveryService) : ControllerBase
{
    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
    [HttpPost("api/v1/companies/{companyId:guid}/retirement-requests/query")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<RetirementRequestBandejaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "Query the company-wide retirement-request bandeja",
        Description = """
            Returns a paginated, filterable list of all retirement requests of the company (by status,
            category, reason, employee, request-date range, retirement-date range and free text over the
            employee/requester names), plus per-status counts. HR-only (`ViewRetirements`, D-12).
            """)]
    public async Task<ActionResult<RetirementRequestBandejaResponse>> QueryRetirementRequests(
        Guid companyId,
        [FromBody] QueryRetirementRequestsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new QueryRetirementRequestsQuery(
                companyId,
                request.StatusCode,
                request.CategoryCode,
                request.ReasonCode,
                request.EmployeeId,
                request.RequestFromUtc,
                request.RequestToUtc,
                request.RetirementFromUtc,
                request.RetirementToUtc,
                request.Search,
                request.PageNumber ?? 1,
                request.PageSize ?? 25),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
    [HttpGet("api/v1/companies/{companyId:guid}/retirement-requests/interview-tray")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<RetirementInterviewTrayItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "List employees authorized for retirement with their exit-interview state (interview tray)",
        Description = """
            The entry point of the exit interview (RF-008, D-07): employees whose retirement request is
            `AUTORIZADA` or `EJECUTADA`, each with the derived state of their interview — `SIN_FORMULARIO`
            (no active published form for the reason), `PENDIENTE`, `BORRADOR` or `ENVIADA` — and the
            submission id to navigate to. `REVERTIDA`/`ANULADA`/`RECHAZADA` never appear. Access with
            `ViewExitInterviews` OR `ViewRetirements` (RN-008.1); reading the interview ANSWERS remains
            governed by the exit-interview module. The interview stays OPTIONAL and never blocks the
            execution (RN-008.3).
            """)]
    public async Task<ActionResult<IReadOnlyCollection<RetirementInterviewTrayItemResponse>>> GetInterviewTray(
        Guid companyId,
        [FromQuery] string? interviewStatus = null,
        [FromQuery] string? categoryCode = null,
        [FromQuery] string? reasonCode = null,
        [FromQuery] DateTime? retirementFromUtc = null,
        [FromQuery] DateTime? retirementToUtc = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetRetirementInterviewTrayQuery(companyId, interviewStatus, categoryCode, reasonCode, retirementFromUtc, retirementToUtc),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
    [HttpGet("api/v1/companies/{companyId:guid}/retirement-requests/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [SwaggerOperation(
        Summary = "Export the retirement-request bandeja to Excel/CSV",
        Description = """
            Exports the filtered retirement-request list (same filters as the bandeja) to `xlsx`, `csv` or
            `json`. HR-only. Synchronous download capped at the configured row limit (`413` if exceeded).
            """)]
    public async Task<IActionResult> Export(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] string? statusCode = null,
        [FromQuery] string? categoryCode = null,
        [FromQuery] string? reasonCode = null,
        [FromQuery] Guid? employeeId = null,
        [FromQuery] DateTime? requestFromUtc = null,
        [FromQuery] DateTime? requestToUtc = null,
        [FromQuery] DateTime? retirementFromUtc = null,
        [FromQuery] DateTime? retirementToUtc = null,
        [FromQuery(Name = "q")] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportRetirementRequestsQuery(
                companyId,
                statusCode,
                categoryCode,
                reasonCode,
                employeeId,
                requestFromUtc,
                requestToUtc,
                retirementFromUtc,
                retirementToUtc,
                search,
                reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<RetirementRequestExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "retirement-requests",
            "RetirementRequests",
            AuditEntityTypes.PersonnelFile,
            "RETIREMENT_REQUESTS",
            "Exported retirement requests report.",
            new
            {
                statusCode,
                categoryCode,
                reasonCode,
                employeeId,
                requestFromUtc,
                requestToUtc,
                retirementFromUtc,
                retirementToUtc,
                q = search
            },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }
}

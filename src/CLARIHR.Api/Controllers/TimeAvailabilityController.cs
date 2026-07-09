using CLARIHR.Api.Common;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Application.Features.PersonnelFiles.PersonnelTransactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

/// <summary>
/// Company-wide time-availability query and export (REQ-003 §3.11 / RF-013) — the planning view that unifies
/// heterogeneous "who is unavailable, when and why" sources under one stable contract with <c>activeSources[]</c>
/// (aclaración №6). F1 wires two families: unpaid SUSPENSIONS (from the applied disciplinary actions) and the
/// end of TEMPORARY CONTRACTS (derived from active assignments whose contract type is temporary). Authorization
/// is the dedicated <c>ViewTimeAvailability</c> permission, enforced per handler
/// (<c>EnsureCanViewTimeAvailabilityAsync</c>): a corporate read with NO self-service branch, so the POST query
/// gates like the family bandejas rather than through [AuthorizationPolicySet] (which would treat the POST as a
/// manage action). The payload is minimal (P-10): no cause/facts/amounts travel.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
public sealed class TimeAvailabilityController(
    IQueryDispatcher queryDispatcher,
    ReportExportDeliveryService reportExportDeliveryService) : ControllerBase
{
    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
    [HttpPost("api/v1/companies/{companyId:guid}/time-availability/query")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<TimeAvailabilityQueryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [SwaggerOperation(
        Summary = "Query the company-wide time-availability planning view",
        Description = """
            Returns a paginated list of the company's availability rows for a MANDATORY date range: unpaid
            SUSPENSIONS (from the applied disciplinary actions, real dates) and the end of TEMPORARY CONTRACTS
            (active assignments whose contract type is temporary and whose end date falls in the range). Each row
            carries a minimal payload (no cause/facts/amounts — P-10) plus the source module and a back-reference.
            The response includes per-category counts and `activeSources[]` (the two F1 families). A missing range
            → 422 `TIME_AVAILABILITY_RANGE_REQUIRED`; an incoherent range → 422 `TIME_AVAILABILITY_RANGE_INVALID`.
            Ordered by start date ascending, employee as tie-break. Requires `ViewTimeAvailability`.
            """)]
    public async Task<ActionResult<TimeAvailabilityQueryResponse>> Query(
        Guid companyId,
        [FromBody] TimeAvailabilityQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new TimeAvailabilityQuery(
                companyId,
                request.StartDate,
                request.EndDate,
                request.PersonnelFilePublicId,
                request.CategoryCodes,
                request.OrgUnitPublicId,
                request.PageNumber ?? 1,
                request.PageSize ?? 50),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
    [HttpGet("api/v1/companies/{companyId:guid}/time-availability/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [SwaggerOperation(
        Summary = "Export the time-availability planning view to Excel/CSV/JSON",
        Description = """
            Exports the time-availability rows (same MANDATORY range and filters as the query) to `xlsx`, `csv` or
            `json` with the employee, position, category, dates, days, status and source. A missing/incoherent
            range → 422. Requires `ViewTimeAvailability`. Synchronous download capped at the configured row limit
            (413 if exceeded).
            """)]
    public async Task<IActionResult> Export(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null,
        [FromQuery] Guid? personnelFilePublicId = null,
        [FromQuery] string[]? categoryCodes = null,
        [FromQuery] Guid? orgUnitPublicId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportTimeAvailabilityQuery(
                companyId,
                startDate,
                endDate,
                personnelFilePublicId,
                categoryCodes,
                orgUnitPublicId,
                reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<DisponibilidadTiempoExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "time-availability",
            "TimeAvailability",
            AuditEntityTypes.PersonnelFile,
            "TIME_AVAILABILITY",
            "Exported time availability report.",
            new { startDate, endDate, personnelFilePublicId, categoryCodes, orgUnitPublicId },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }
}

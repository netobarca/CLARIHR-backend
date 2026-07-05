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
/// Company-wide settlements bandeja and export (RF-006/RF-007c) — HR-only (D-20; settlements expose salary
/// data, no self-service in Fase 1). Intentionally NOT annotated with [AuthorizationPolicySet]: the
/// convention would assign the Manage policy to the POST query (a READ), producing false 403s for view-only
/// users; authorization is enforced per handler via <c>EnsureCanViewSettlementsAsync</c>.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
public sealed class SettlementsReportingController(
    IQueryDispatcher queryDispatcher,
    ReportExportDeliveryService reportExportDeliveryService) : ControllerBase
{
    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
    [HttpPost("api/v1/companies/{companyId:guid}/settlements/query")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<SettlementBandejaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "Query the company-wide settlements bandeja",
        Description = """
            Returns a paginated, filterable list of the company's settlements and scenarios (by kind
            `Liquidacion`/`Escenario`, status, category, reason, employee, request/retirement date ranges
            and free text over employee/requester/plaza names) plus per-status counts (scenarios roll up
            under the `ESCENARIO` key). Scenarios are always visibly marked — suggest defaulting the UI
            filter to `kind=Liquidacion` (R-10). HR-only (`ViewSettlements`, D-20).
            """)]
    public async Task<ActionResult<SettlementBandejaResponse>> QuerySettlements(
        Guid companyId,
        [FromBody] QuerySettlementsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new QuerySettlementsQuery(
                companyId,
                request.Kind,
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

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
    [HttpGet("api/v1/companies/{companyId:guid}/settlements/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [SwaggerOperation(
        Summary = "Export the settlements bandeja to Excel/CSV",
        Description = """
            Exports the filtered settlements list (same filters as the bandeja) to `xlsx`, `csv` or `json`
            with the five-section totals per row; scenario rows carry the `SIMULACIÓN — SIN EFECTOS` mark.
            HR-only. Synchronous download capped at the configured row limit (`413` if exceeded).
            """)]
    public async Task<IActionResult> Export(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] string? kind = null,
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
            new ExportSettlementsQuery(
                companyId,
                kind,
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
            return this.ToActionResult(Result<IReadOnlyCollection<SettlementExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "settlements",
            "Settlements",
            AuditEntityTypes.PersonnelFile,
            "SETTLEMENTS",
            "Exported settlements report.",
            new
            {
                kind,
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

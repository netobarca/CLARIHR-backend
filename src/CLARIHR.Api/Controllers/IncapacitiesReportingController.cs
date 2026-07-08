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
/// Company-wide incapacities bandeja and export (RF-013). Intentionally NOT annotated with
/// [AuthorizationPolicySet]: the convention would assign the Manage policy to the POST query (a READ),
/// producing false 403s for view-only users; authorization is enforced per handler via
/// <c>EnsureCanViewIncapacitiesAsync</c>. The bandeja/export default the status filter to REGISTRADA so the
/// EN_REVISION self-registrations are excluded from the payroll input (R-T6) — the StatusCounts still cover
/// every status.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
public sealed class IncapacitiesReportingController(
    IQueryDispatcher queryDispatcher,
    ReportExportDeliveryService reportExportDeliveryService) : ControllerBase
{
    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
    [HttpPost("api/v1/companies/{companyId:guid}/incapacities/query")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<IncapacityBandejaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "Query the company-wide incapacities bandeja",
        Description = """
            Returns a paginated, filterable list of the company's incapacities (by employee, risk code, type
            code, status, payroll type and start-date range) with days and referential amounts, plus per-status
            counts. The StatusCounts cover every status; the items DEFAULT to `status=REGISTRADA` when no status
            is supplied — this excludes the EN_REVISION self-registrations from the payroll input (R-T6).
            HR-only (`ViewIncapacities`).
            """)]
    public async Task<ActionResult<IncapacityBandejaResponse>> QueryIncapacities(
        Guid companyId,
        [FromBody] QueryIncapacitiesRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new QueryIncapacitiesQuery(
                companyId,
                request.EmployeeId,
                request.RiskCode,
                request.IncapacityTypeCode,
                request.StatusCode,
                request.PayrollTypeCode,
                request.StartFromUtc,
                request.StartToUtc,
                request.PageNumber ?? 1,
                request.PageSize ?? 25),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
    [HttpGet("api/v1/companies/{companyId:guid}/incapacities/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [SwaggerOperation(
        Summary = "Export the incapacities bandeja to Excel/CSV/JSON",
        Description = """
            Exports the filtered incapacities (same filters as the bandeja) to `xlsx`, `csv` or `json` with days
            and amounts per section, the flattened per-tranche percentages, the monthly/daily base salary and the
            payroll type + period. DEFAULTS to `status=REGISTRADA` (the payroll input excludes EN_REVISION).
            HR-only. Synchronous download capped at the configured row limit (`413` if exceeded).
            """)]
    public async Task<IActionResult> Export(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] Guid? employeeId = null,
        [FromQuery] string? riskCode = null,
        [FromQuery] string? incapacityTypeCode = null,
        [FromQuery] string? statusCode = null,
        [FromQuery] string? payrollTypeCode = null,
        [FromQuery] DateTime? startFromUtc = null,
        [FromQuery] DateTime? startToUtc = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportIncapacitiesQuery(
                companyId,
                employeeId,
                riskCode,
                incapacityTypeCode,
                statusCode,
                payrollTypeCode,
                startFromUtc,
                startToUtc,
                reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<IncapacidadExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "incapacities",
            "Incapacities",
            AuditEntityTypes.PersonnelFile,
            "INCAPACITIES",
            "Exported incapacities report.",
            new
            {
                employeeId,
                riskCode,
                incapacityTypeCode,
                statusCode,
                payrollTypeCode,
                startFromUtc,
                startToUtc
            },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }
}

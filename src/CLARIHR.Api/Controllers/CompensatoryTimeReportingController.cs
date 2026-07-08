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
/// Company-wide compensatory-time bandeja and exports (REQ-002 §3.9): the unified movements stream (credits +
/// absences) and the per-employee fund balances. Intentionally NOT annotated with [AuthorizationPolicySet]: the
/// convention would assign the Manage policy to the POST query (a READ), producing false 403s for view-only
/// users; authorization is enforced per handler via <c>EnsureCanViewCompensatoryTimeAsync</c> (precedent:
/// settlements / incapacities reporting). The bandeja/export exclude the ANULADA movements by default (the
/// caller opts in with <c>includeAnnulled</c> or an explicit <c>statusCode</c>).
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
public sealed class CompensatoryTimeReportingController(
    IQueryDispatcher queryDispatcher,
    ReportExportDeliveryService reportExportDeliveryService) : ControllerBase
{
    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
    [HttpPost("api/v1/companies/{companyId:guid}/compensatory-time-movements/query")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<CompensatoryTimeMovementBandejaResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "Query the company-wide compensatory-time movements bandeja",
        Description = """
            Returns a paginated, filterable list of the company's compensatory-time movements — credits
            (`ACREDITACION`, hours +) and absences (`AUSENCIA`, hours −) projected into one stream — by employee,
            type, operation, status and movement-date range, plus per-status counts. The StatusCounts cover
            every status; the items EXCLUDE the ANULADA movements by default (opt in with `includeAnnulled` or
            an explicit `statusCode`). HR-only (`ViewCompensatoryTime`).
            """)]
    public async Task<ActionResult<CompensatoryTimeMovementBandejaResponse>> QueryMovements(
        Guid companyId,
        [FromBody] QueryCompensatoryTimeMovementsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new QueryCompensatoryTimeMovementsQuery(
                companyId,
                request.EmployeeId,
                request.CompensatoryTimeTypePublicId,
                request.OperationCode,
                request.StatusCode,
                request.FromDate,
                request.ToDate,
                request.IncludeAnnulled ?? false,
                request.PageNumber ?? 1,
                request.PageSize ?? 25),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
    [HttpGet("api/v1/companies/{companyId:guid}/compensatory-time-movements/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [SwaggerOperation(
        Summary = "Export the compensatory-time movements bandeja to Excel/CSV/JSON",
        Description = """
            Exports the filtered movements (same filters as the bandeja) to `xlsx`, `csv` or `json` with the
            worked/date-range, hours worked, factor, signed hours, detail, authorizer, status and the imputed
            payroll period. The ANULADA movements are EXCLUDED by default (opt in with `includeAnnulled` or an
            explicit `statusCode`). HR-only. Synchronous download capped at the configured row limit (413 if
            exceeded).
            """)]
    public async Task<IActionResult> ExportMovements(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] Guid? employeeId = null,
        [FromQuery] Guid? compensatoryTimeTypePublicId = null,
        [FromQuery] string? operationCode = null,
        [FromQuery] string? statusCode = null,
        [FromQuery] DateOnly? fromDate = null,
        [FromQuery] DateOnly? toDate = null,
        [FromQuery] bool includeAnnulled = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportCompensatoryTimeMovementsQuery(
                companyId,
                employeeId,
                compensatoryTimeTypePublicId,
                operationCode,
                statusCode,
                fromDate,
                toDate,
                includeAnnulled,
                reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<MovimientoTiempoCompensatorioExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "compensatory-time-movements",
            "CompensatoryTimeMovements",
            AuditEntityTypes.PersonnelFile,
            "COMPENSATORY_TIME_MOVEMENTS",
            "Exported compensatory-time movements report.",
            new
            {
                employeeId,
                compensatoryTimeTypePublicId,
                operationCode,
                statusCode,
                fromDate,
                toDate,
                includeAnnulled
            },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
    [HttpGet("api/v1/companies/{companyId:guid}/compensatory-time-balances/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [SwaggerOperation(
        Summary = "Export the per-employee compensatory-time balances to Excel/CSV/JSON",
        Description = """
            Exports the fund balances (total credited / debited / available + last movement date) of every
            employee with at least one VIGENTE (REGISTRADA) movement to `xlsx`, `csv` or `json`. HR-only.
            Synchronous download capped at the configured row limit (413 if exceeded).
            """)]
    public async Task<IActionResult> ExportBalances(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] Guid? employeeId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportCompensatoryTimeBalancesQuery(
                companyId,
                employeeId,
                reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<SaldoTiempoCompensatorioExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "compensatory-time-balances",
            "CompensatoryTimeBalances",
            AuditEntityTypes.PersonnelFile,
            "COMPENSATORY_TIME_BALANCES",
            "Exported compensatory-time balances report.",
            new { employeeId },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }
}

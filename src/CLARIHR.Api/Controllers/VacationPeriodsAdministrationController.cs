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
/// Company-wide vacation fund administration (leave module PR-7): the idempotent mass generation of yearly
/// periods and the Finanzas provision export (D-25). Intentionally NOT annotated with [AuthorizationPolicySet]
/// (its name is outside the PersonnelFile governed family): authorization is enforced per handler
/// (<c>EnsureCanManageVacationsAsync</c> for the generation write, <c>EnsureCanViewVacationsAsync</c> for the
/// export read) — like the reporting controllers.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
public sealed class VacationPeriodsAdministrationController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    ReportExportDeliveryService reportExportDeliveryService) : ControllerBase
{
    [HttpPost("api/v1/companies/{companyId:guid}/vacation-periods/generate")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<VacationPeriodGenerationSummary>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "Generate the yearly vacation fund for active employees",
        Description = """
            Creates one active vacation fund period per active employee for the year (idempotent by
            employee-year: a re-run creates nothing for employees that already have an active period).
            Art. 177-ineligible employees (less than one year of service at the start of the period) are
            reported per row (`errors`). The grants and anniversary flag default to the company preference; an
            optional `employeeIds` filter restricts the run. Returns a `{created, skipped, errors[]}` summary.
            Manager-only (`ManageVacations`).
            """)]
    public async Task<ActionResult<VacationPeriodGenerationSummary>> Generate(
        Guid companyId,
        [FromBody] GenerateVacationPeriodsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new GenerateVacationPeriodsCommand(
                companyId,
                new VacationPeriodGenerationInput(
                    request.Year,
                    request.UseAnniversary,
                    request.LegalDaysGranted,
                    request.BenefitDaysGranted,
                    request.GeneratesEnjoymentDays,
                    request.EmployeeIds)),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
    [HttpGet("api/v1/companies/{companyId:guid}/vacation-fund/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [SwaggerOperation(
        Summary = "Export the vacation fund provision to Excel/CSV/JSON",
        Description = """
            Exports the Finanzas provision (D-25): one active period per employee with the granted days split
            into legal/benefit, the enjoyed and pending days, the daily salary (salary/30) and the provision
            (pending × daily × 1.30). Optionally filtered by year. HR-only (`ViewVacations`). Synchronous
            download capped at the configured row limit (`413` if exceeded).
            """)]
    public async Task<IActionResult> ExportFund(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] int? year = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportVacationFundQuery(companyId, year, reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<FondoProvisionExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "vacation-fund-provision",
            "VacationFundProvision",
            AuditEntityTypes.PersonnelFile,
            "VACATION_FUND_PROVISION",
            "Exported vacation fund provision report.",
            new { year },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }
}

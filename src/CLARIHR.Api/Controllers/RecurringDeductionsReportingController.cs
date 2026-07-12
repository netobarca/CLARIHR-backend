using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.PersonnelFiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

/// <summary>
/// Company-wide recurring-deduction operations ("planilla — descuentos cíclicos", REQ-008): the apply-period batch
/// (PR-4) and — added in PR-5 — the bandeja + exports, the pending-charges bandeja and the payroll-input export.
/// Intentionally NOT annotated with [AuthorizationPolicySet] (the convention would assign a policy to every
/// action); the apply-period write enforces the <c>ManageRecurringDeductions</c> grant per handler (precedent: the
/// recurring-income reporting controller).
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
public sealed class RecurringDeductionsReportingController(
    ICommandDispatcher commandDispatcher) : ControllerBase
{
    [HttpPost("api/v1/companies/{companyId:guid}/recurring-deductions/apply-period")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<RecurringDeductionApplyPeriodResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Apply the due recurring-deduction charges of a payroll period",
        Description = """
            Applies, ATOMICALLY, every due charge of the company's `VIGENTE` credits of the given `payrollTypeCode`
            up to the period cutoff (RF-007) — including overdue charges carried over from previous periods. The
            charges advance by each credit's APPLICATION cadence (a monthly quota charged fortnightly yields two
            half charges) and skip its exception months; a credit whose `effectiveDate` has not been reached is not
            a candidate. Provide a `payrollPeriodPublicId` (its end date is the cutoff and its id/label are
            snapshotted onto the charges) or a bare `cutoffDate`. `excludedDeductionPublicIds` postpones credits
            (they stay due for the next run). Credits whose plan completes in the run are FINALIZED. Any conflict
            rolls the WHOLE batch back (422). HR-only (`ManageRecurringDeductions`).
            """)]
    public async Task<ActionResult<RecurringDeductionApplyPeriodResult>> ApplyRecurringDeductionPeriod(
        Guid companyId,
        [FromBody] ApplyRecurringDeductionPeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ApplyRecurringDeductionPeriodCommand(
                companyId,
                request.PayrollTypeCode,
                request.PayrollPeriodPublicId,
                request.CutoffDate,
                request.ExcludedDeductionPublicIds ?? []),
            cancellationToken);

        return this.ToActionResult(result);
    }
}

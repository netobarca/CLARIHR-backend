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
/// Company-wide recurring-income operations ("planilla — ingresos cíclicos", REQ-005): the apply-period batch that
/// posts every due installment of a payroll type in one atomic transaction. Intentionally NOT annotated with
/// [AuthorizationPolicySet] (the convention would assign a policy to every action); the apply-period write enforces
/// the Manage grant per handler (precedent: settlements / compensatory-time reporting). The bandeja + exports land
/// on this controller in PR-5.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
public sealed class RecurringIncomesReportingController(
    ICommandDispatcher commandDispatcher) : ControllerBase
{
    [HttpPost("api/v1/companies/{companyId:guid}/recurring-incomes/apply-period")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<RecurringIncomeApplyPeriodResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Apply the due recurring-income installments of a payroll period",
        Description = """
            Applies, ATOMICALLY, every due installment of the company's `VIGENTE` recurring incomes of the given
            `payrollTypeCode` up to the period cutoff (RF-007) — including overdue installments carried over from
            previous periods. Provide a `payrollPeriodPublicId` (its end date is the cutoff and its id/label are
            snapshotted onto the installments) or a bare `cutoffDate`. `excludedIncomePublicIds` postpones incomes
            (they stay due for the next run). Any conflict rolls the whole batch back (422). HR-only
            (`ManageRecurringIncomes`). Returns the count of applied installments, finalized incomes and postponed
            incomes.
            """)]
    public async Task<ActionResult<RecurringIncomeApplyPeriodResult>> ApplyPeriod(
        Guid companyId,
        [FromBody] ApplyRecurringIncomePeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ApplyRecurringIncomePeriodCommand(
                companyId,
                request.PayrollTypeCode,
                request.PayrollPeriodPublicId,
                request.CutoffDate,
                request.ExcludedIncomePublicIds ?? []),
            cancellationToken);

        return this.ToActionResult(result);
    }
}

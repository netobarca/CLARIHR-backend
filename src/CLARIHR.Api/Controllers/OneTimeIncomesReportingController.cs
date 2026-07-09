using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

/// <summary>
/// Company-wide one-time-income operations ("planilla — ingresos eventuales", REQ-006): the atomic apply-period
/// batch (RF-012) and the pending/overdue tray. Intentionally NOT annotated with [AuthorizationPolicySet] (the
/// convention would assign the Manage policy to the POST pending query — a READ — producing false 403s for
/// view-only users); authorization is enforced per handler (Manage for the batch, View for the tray). Precedent:
/// settlements / recurring-incomes reporting controllers.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
public sealed class OneTimeIncomesReportingController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpPost("api/v1/companies/{companyId:guid}/one-time-incomes/apply-period")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<OneTimeIncomeApplyPeriodResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Apply the AUTORIZADO one-time incomes of a payroll period",
        Description = """
            Applies, ATOMICALLY, every `AUTORIZADO` one-time income of the company of the given `payrollTypeCode`
            (RF-012) — including the "atrasados" whose declared period already passed. Provide a
            `payrollPeriodPublicId` (FK real; its id/label are snapshotted onto the applications) or a bare
            `payrollPeriodLabel` to override the destination for every applied income; omit both to default each
            application to its income's declared destination. `excludedIncomePublicIds` postpones incomes (they stay
            AUTORIZADO for the next run). Any conflict rolls the whole batch back (422). HR-only
            (`ManageOneTimeIncomes`). Returns the count of applied and postponed incomes.
            """)]
    public async Task<ActionResult<OneTimeIncomeApplyPeriodResult>> ApplyPeriod(
        Guid companyId,
        [FromBody] ApplyOneTimeIncomePeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ApplyOneTimeIncomePeriodCommand(
                companyId,
                request.PayrollTypeCode,
                request.PayrollPeriodPublicId,
                request.PayrollPeriodLabel,
                request.ExcludedIncomePublicIds ?? []),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
    [HttpPost("api/v1/companies/{companyId:guid}/one-time-incomes/pending/query")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<OneTimeIncomePendingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "Query the pending/overdue one-time incomes tray",
        Description = """
            Returns the company's `AUTORIZADO` one-time incomes without an active application (RF-012) — the ones
            still to be applied — each marked `isOverdue` when its declared payroll-period end date already passed.
            Optionally filter by `payrollTypeCode` and/or `onlyOverdue`. HR-only (`ViewOneTimeIncomes`).
            """)]
    public async Task<ActionResult<OneTimeIncomePendingResponse>> QueryPending(
        Guid companyId,
        [FromBody] QueryOneTimeIncomePendingRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new QueryOneTimeIncomePendingQuery(companyId, request.PayrollTypeCode, request.OnlyOverdue ?? false),
            cancellationToken);
        return this.ToActionResult(result);
    }
}

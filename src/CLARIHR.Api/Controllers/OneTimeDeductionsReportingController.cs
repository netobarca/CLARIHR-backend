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
/// Company-wide one-time-deduction operations (REQ-009): the pending work list and the apply-period batch (PR-4);
/// the bandeja + exports arrive in PR-5. Intentionally NOT annotated with [AuthorizationPolicySet] (the convention
/// would assign a Manage policy to the POST that is really a READ); the reads enforce
/// <c>ViewOneTimeDeductions</c> per handler and the batch enforces <c>ManageOneTimeDeductions</c> per handler.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
public sealed class OneTimeDeductionsReportingController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
    [HttpPost("api/v1/companies/{companyId:guid}/one-time-deductions/pending/query")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<OneTimeDeductionPendingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "Query the one-time deductions still waiting to be charged",
        Description = """
            The payroll operator's work list: every `AUTORIZADO` deduction of the company that has not been charged
            yet, optionally narrowed by payroll type, payroll period or employee. A deduction whose target period
            has already CLOSED is flagged `isOverdue` — it should have been charged and was not. HR-only
            (`ViewOneTimeDeductions`).
            """)]
    public async Task<ActionResult<OneTimeDeductionPendingResponse>> QueryOneTimeDeductionPending(
        Guid companyId,
        [FromBody] QueryOneTimeDeductionPendingRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new QueryOneTimeDeductionPendingQuery(
                companyId,
                request.PayrollTypeCode,
                request.PayrollPeriodPublicId,
                request.EmployeeId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/companies/{companyId:guid}/one-time-deductions/apply-period")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<OneTimeDeductionApplyPeriodResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Charge the pending one-time deductions of a payroll period",
        Description = """
            Charges, ATOMICALLY, every `AUTORIZADO` deduction of the company for the given `payrollTypeCode`
            (optionally narrowed to a payroll period). `excludedDeductionPublicIds` postpones deductions — they
            stay pending for the next run. Any conflict rolls the WHOLE batch back (422); there are no partial
            successes. HR-only (`ManageOneTimeDeductions`).
            """)]
    public async Task<ActionResult<OneTimeDeductionApplyPeriodResult>> ApplyOneTimeDeductionPeriod(
        Guid companyId,
        [FromBody] ApplyOneTimeDeductionPeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ApplyOneTimeDeductionPeriodCommand(
                companyId,
                request.PayrollTypeCode,
                request.PayrollPeriodPublicId,
                request.ExcludedDeductionPublicIds ?? []),
            cancellationToken);

        return this.ToActionResult(result);
    }
}

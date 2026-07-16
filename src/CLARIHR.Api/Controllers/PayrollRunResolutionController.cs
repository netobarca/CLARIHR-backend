using Asp.Versioning;
using CLARIHR.Api.Authorization;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.Payroll;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

/// <summary>
/// AUTHORIZER decisions on payroll runs (REQ-012 §3.6 — REQ-013 P-01/P-02): authorization (freeze) and
/// return-with-reason (the ONLY pre-closure reopening). Kept in a dedicated controller because the
/// class-level policy set must map its writes to <see cref="PersonnelFilePolicies.AuthorizePayrollRuns"/> —
/// a grant that <c>PersonnelFiles.Admin</c> deliberately does NOT imply (separation of duties, mirrors
/// AuthorizeRetirement). The handler adds the double anti-self: the run's generator never authorizes it.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}")]
[Tags("Payroll Runs")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewPayrollRuns, PersonnelFilePolicies.AuthorizePayrollRuns)]
public sealed class PayrollRunResolutionController(ICommandDispatcher commandDispatcher) : ControllerBase
{
    [HttpPatch("companies/{companyId:guid}/payroll-runs/{payrollRunId:guid}/authorization")]
    [Produces("application/json")]
    [ProducesResponseType<PayrollRunResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Authorize a payroll run",
        Description = """
            `GENERADA` → `AUTORIZADA` (REQ-013 P-01 first step): the calculations freeze — no adjustment,
            recalculation or regeneration is possible until a return. Requires the dedicated
            `AuthorizePayrollRuns` grant (`PersonnelFiles.Admin` does NOT imply it). Separation of duties
            (double anti-self): the user who GENERATED the run cannot authorize it
            (`403 PAYROLL_RUN_SELF_AUTHORIZATION_FORBIDDEN`). Requires `If-Match` with the run's current
            `concurrencyToken`.
            """)]
    public async Task<ActionResult<PayrollRunResponse>> Authorize(
        Guid companyId,
        Guid payrollRunId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AuthorizePayrollRunCommand(companyId, payrollRunId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("companies/{companyId:guid}/payroll-runs/{payrollRunId:guid}/return")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PayrollRunResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Return a payroll run to review",
        Description = """
            `AUTORIZADA` → `GENERADA` with a MANDATORY reason (REQ-013 P-02 — the ONLY pre-closure
            reopening): the run becomes editable again (adjustments, recalculation, regeneration) and must
            be re-authorized to close. Requires the dedicated `AuthorizePayrollRuns` grant and `If-Match`
            with the run's current `concurrencyToken`.
            """)]
    public async Task<ActionResult<PayrollRunResponse>> Return(
        Guid companyId,
        Guid payrollRunId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] ReturnPayrollRunRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReturnPayrollRunCommand(companyId, payrollRunId, request.Reason, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    public sealed record ReturnPayrollRunRequest(string Reason);
}

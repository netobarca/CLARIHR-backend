using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.Compensation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

/// <summary>
/// The company's per-type indebtedness ceilings (REQ-010 D-16). Deliberately NOT annotated with
/// [AuthorizationPolicySet]: the read and the write carry different, dedicated permissions, and both gate per
/// handler (<c>ViewIndebtedness</c> / <c>ManageIndebtednessParameters</c>).
/// </summary>
[ApiController]
[Authorize]
[Tags("Compensation")]
public sealed class IndebtednessParametersController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/companies/{companyId:guid}/indebtedness-limits")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<IndebtednessLimitResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "Get the company's per-type indebtedness ceilings",
        Description = """
            The maximum share of an employee's income that a recurring deduction of each type may consume. A type
            WITHOUT a row here falls back to the company-wide `maxIndebtednessPercent` preference; if neither is
            configured, the company has **no indebtedness control at all** and deductions are registered without
            any warning (that is the intended opt-in-by-configuration behaviour, not a bug).
            """)]
    public async Task<ActionResult<IReadOnlyCollection<IndebtednessLimitResponse>>> GetLimits(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetIndebtednessLimitsQuery(companyId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/companies/{companyId:guid}/indebtedness-limits")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<IndebtednessLimitResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace the company's per-type indebtedness ceilings",
        Description = """
            **Replace-all**: the body is the complete set of ceilings; whatever is not sent is removed. Each
            `recurringDeductionTypeCode` must exist and be ACTIVE in the deduction-type catalog
            (`422 INDEBTEDNESS_LIMIT_TYPE_INVALID`), and no type may appear twice
            (`422 INDEBTEDNESS_LIMIT_TYPE_DUPLICATED`). The per-type ceiling PREVAILS over the company-wide
            preference for deductions of its type — **even when it is more permissive**.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<IndebtednessLimitResponse>>> ReplaceLimits(
        Guid companyId,
        [FromBody] ReplaceIndebtednessLimitsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplaceIndebtednessLimitsCommand(
                companyId,
                request.Limits ?? []),
            cancellationToken);

        return this.ToActionResult(result);
    }
}

public sealed record ReplaceIndebtednessLimitsRequest(IReadOnlyCollection<IndebtednessLimitInput>? Limits);

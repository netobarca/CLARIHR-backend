using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
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
/// The employee's indebtedness level and its simulation (REQ-010 D-17). Both endpoints are READS gated per handler
/// by <c>ViewIndebtedness</c> — an aggregated, sensitive figure that does not ride on the generic file-read
/// permission. Deliberately NOT annotated with [AuthorizationPolicySet]: the convention would treat the simulation's
/// POST as a write, and it is not one.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
public sealed class IndebtednessController(IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/personnel-files/{publicId:guid}/indebtedness")]
    [Produces("application/json")]
    [ProducesResponseType<IndebtednessResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get the employee's indebtedness level",
        Description = """
            The income base (broken down per plaza, each monthly-ized on its own pay period), the debt load (broken
            down per recurring deduction, each with the ceiling that governs IT), the current percentage, and the
            history of confirmed overrides.

            `status` is `DENTRO`, `EXCEDIDO` or **`SIN_CONTROL`** — the last one meaning the company configured no
            ceiling at all. That is a legitimate state, not an error: the whole feature is opt-in by configuration.
            An employee with no salary configured reports `0%` and is never flagged as exceeding.
            """)]
    public async Task<ActionResult<IndebtednessResponse>> Get(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetIndebtednessQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
    [HttpPost("api/v1/personnel-files/{publicId:guid}/indebtedness/simulation")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<IndebtednessSimulationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [SwaggerOperation(
        Summary = "Simulate an additional deduction against the employee's indebtedness",
        Description = """
            **Read-only. It writes NOTHING** — "solo simulación y no debe afectar la planilla" is a literal
            requirement, so the handler is a query with no unit of work. The verb is POST only because the input
            travels in a body.

            `baseIncomeOverride` is the "ingreso digitado": omit it and the derived base is used.
            `additionalDeduction.typeCode` selects the ceiling that would govern the hypothetical deduction (the
            per-type one prevails over the company-wide one, even when it is more permissive).
            """)]
    public async Task<ActionResult<IndebtednessSimulationResponse>> Simulate(
        Guid publicId,
        [FromBody] SimulateIndebtednessRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SimulateIndebtednessQuery(
                publicId,
                request.BaseIncomeOverride,
                new SimulatedDeductionInput(
                    request.AdditionalDeduction.Amount,
                    request.AdditionalDeduction.PayPeriodCode,
                    request.AdditionalDeduction.TypeCode)),
            cancellationToken);

        return this.ToActionResult(result);
    }
}

public sealed record SimulateIndebtednessRequest(
    decimal? BaseIncomeOverride,
    SimulatedDeductionRequest AdditionalDeduction);

public sealed record SimulatedDeductionRequest(
    decimal Amount,
    string PayPeriodCode,
    string? TypeCode);

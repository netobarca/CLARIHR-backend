using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.Compensation;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

// Tenant-scoped configurable income-tax (Renta/ISR) withholding table (D-14). Read with the personnel
// Read policy; replace (per period) with the Manage policy. The table is editable at any time (D-19);
// the actual retention is computed by the future payroll module, not here.
[ApiController]
[Authorize]
[Tags("Compensation")]
[AuthorizationPolicySet(PersonnelFilePolicies.Read, PersonnelFilePolicies.Manage)]
public sealed class IncomeTaxBracketsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/income-tax-brackets")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<IncomeTaxBracketResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List the income-tax (Renta) withholding brackets",
        Description = """
            Returns the configured income-tax withholding brackets of the tenant, optionally filtered by
            `payPeriodCode` (e.g. `MENSUAL`, `QUINCENAL`, `SEMANAL`). Ordered by period then bracket order.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<IncomeTaxBracketResponse>>> GetIncomeTaxBrackets(
        [FromQuery] string? payPeriodCode,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetIncomeTaxBracketsQuery(payPeriodCode), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/income-tax-brackets")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<IncomeTaxBracketResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace the income-tax (Renta) withholding brackets of a pay period",
        Description = """
            Replaces the whole set of brackets for the given `payPeriodCode` (the table is edited as a
            set, not row by row). Returns the resulting brackets for that period.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<IncomeTaxBracketResponse>>> ReplaceIncomeTaxBrackets(
        [FromBody] ReplaceIncomeTaxBracketsRequest request,
        CancellationToken cancellationToken = default)
    {
        var brackets = request.Brackets
            .Select(item => new IncomeTaxBracketInput(
                item.BracketOrder,
                item.LowerBound,
                item.UpperBound,
                item.FixedFee,
                item.RatePercent,
                item.ExcessOver,
                item.EffectiveFromUtc,
                item.EffectiveToUtc,
                item.IsActive))
            .ToArray();

        var result = await commandDispatcher.SendAsync(
            new ReplaceIncomeTaxBracketsCommand(request.PayPeriodCode, brackets),
            cancellationToken);
        return this.ToActionResult(result);
    }

    public sealed record ReplaceIncomeTaxBracketsRequest(
        string PayPeriodCode,
        IReadOnlyCollection<IncomeTaxBracketRequestItem> Brackets);

    public sealed record IncomeTaxBracketRequestItem(
        int BracketOrder,
        decimal LowerBound,
        decimal? UpperBound,
        decimal FixedFee,
        decimal RatePercent,
        decimal ExcessOver,
        DateTime EffectiveFromUtc,
        DateTime? EffectiveToUtc,
        bool IsActive);
}

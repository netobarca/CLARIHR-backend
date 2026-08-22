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

// La porción del aguinaldo exenta de Renta, por año. Mismo régimen que la tabla de Renta
// (`IncomeTaxBracketsController`): parámetro legal tenant-scoped, se lee con la política Read y se edita
// como CONJUNTO con la política Manage. No se siembra: la empresa registra el valor que publica la ley.
[ApiController]
[Authorize]
[Tags("Compensation")]
[AuthorizationPolicySet(PersonnelFilePolicies.Read, PersonnelFilePolicies.Manage)]
public sealed class AguinaldoExemptionsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/aguinaldo-exemptions")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<AguinaldoExemptionResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List the aguinaldo income-tax exemptions by year",
        Description = """
            Returns the tenant's configured aguinaldo exemptions — the ABSOLUTE amount of the Christmas bonus
            that is exempt from income tax (Renta), one row per calendar year. Optionally filtered by `year`.
            Newest year first.

            When an aguinaldo payroll run finds no ACTIVE row for its year, the whole aguinaldo is taxed and
            the run reports the `PAYROLL_WARNING_AGUINALDO_NO_EXEMPTION` warning: withholding too much is
            visible and correctable, not withholding is a silent liability.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<AguinaldoExemptionResponse>>> GetAguinaldoExemptions(
        [FromQuery] int? year,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetAguinaldoExemptionsQuery(year), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/aguinaldo-exemptions")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<AguinaldoExemptionResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace the aguinaldo income-tax exemptions",
        Description = """
            Replaces the whole set of yearly exemptions (the parameter is edited as a set, like the Renta
            table). At most one row per `year` — a duplicate year fails with
            `AGUINALDO_EXEMPTION_DUPLICATE_YEAR`. Years omitted from the payload are deleted.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<AguinaldoExemptionResponse>>> ReplaceAguinaldoExemptions(
        [FromBody] ReplaceAguinaldoExemptionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var exemptions = request.Exemptions
            .Select(item => new AguinaldoExemptionInput(item.Year, item.ExemptAmount, item.IsActive))
            .ToArray();

        var result = await commandDispatcher.SendAsync(
            new ReplaceAguinaldoExemptionsCommand(exemptions),
            cancellationToken);
        return this.ToActionResult(result);
    }

    public sealed record ReplaceAguinaldoExemptionsRequest(
        IReadOnlyCollection<AguinaldoExemptionRequestItem> Exemptions);

    public sealed record AguinaldoExemptionRequestItem(
        int Year,
        decimal ExemptAmount,
        bool IsActive);
}

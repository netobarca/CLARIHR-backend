using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.PersonnelFiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

// Country-scoped enriched catalog of settlement ("liquidación") concepts. Authn-only, like
// GeneralCatalogsController: reference data consumed by the settlement detail to group lines per
// section and by the pickers that re-add or append manual lines (class, affectation matrix,
// exemption rule, employer rate). Mirrors CompensationConceptTypesController.
[ApiController]
[Authorize]
[Tags("General Catalogs")]
public sealed class SettlementConceptsController(IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/settlement-concepts")]
    [ProducesResponseType<IReadOnlyCollection<SettlementConceptResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [SwaggerOperation(
        Summary = "List the settlement concepts",
        Description = """
            Returns the country-scoped catalog of settlement ("liquidación") concepts with the
            attributes the calculation consumes: the section (`Ingreso`, `Descuento`,
            `PagoPatronal`), the ISSS/AFP/Renta affectation matrix, the income-tax exemption rule
            with its optional multiplier, whether the engine computes the line (vs manual entry),
            and the employer rate for the pagos-patronales section. The `countryCode` query
            parameter (a 2–3 letter ISO-style code) selects the country and returns no items when
            missing or unknown. Optionally filter by `conceptClass`. Items are ordered by
            `sortOrder`.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<SettlementConceptResponse>>> GetSettlementConcepts(
        [FromQuery] string? countryCode,
        [FromQuery] SettlementConceptClass? conceptClass,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetSettlementConceptsQuery(countryCode, conceptClass),
            cancellationToken);
        return this.ToActionResult(result);
    }
}

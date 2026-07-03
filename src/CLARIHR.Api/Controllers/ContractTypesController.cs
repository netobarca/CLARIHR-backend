using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.PersonnelFiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

// Country-scoped enriched catalog of contract types (RF-011). Authn-only, like
// GeneralCatalogsController: reference data consumed on the contract-history form. The generic
// `general-catalogs/contract-types` read stays available for code/name combos; this endpoint adds the
// abbreviation and the IsTemporary flag (DP-03: extra columns are seed-delivered).
[ApiController]
[Authorize]
[Tags("General Catalogs")]
public sealed class ContractTypesController(IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/contract-types")]
    [ProducesResponseType<IReadOnlyCollection<ContractTypeResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [SwaggerOperation(
        Summary = "List the contract types (enriched)",
        Description = """
            Returns the country-scoped catalog of employment contract types with their enriched
            attributes: the optional short `abbreviation` and the `isTemporary` flag that marks
            fixed-term/temporary modalities (plazo fijo, obra, eventual…). The `countryCode` query
            parameter (a 2–3 letter ISO-style code) selects the country and returns no items when
            missing or unknown. Items are ordered by `sortOrder`.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<ContractTypeResponse>>> GetContractTypes(
        [FromQuery] string? countryCode,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetContractTypesQuery(countryCode), cancellationToken);
        return this.ToActionResult(result);
    }
}

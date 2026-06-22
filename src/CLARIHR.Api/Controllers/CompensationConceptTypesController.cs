using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

// Country-scoped enriched catalog of compensation concept types (income/deduction). Authn-only, like
// GeneralCatalogsController: reference data consumed on the compensation form to pre-fill defaults
// (nature, deduction class, calculation type/base, statutory employee/employer rates and cap).
[ApiController]
[Authorize]
[Tags("General Catalogs")]
public sealed class CompensationConceptTypesController(IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/compensation-concept-types")]
    [ProducesResponseType<IReadOnlyCollection<CompensationConceptTypeResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [SwaggerOperation(
        Summary = "List the compensation concept types",
        Description = """
            Returns the country-scoped catalog of compensation concept types with their default
            attributes (nature `Ingreso`/`Egreso`, statutory flag, default deduction class, default
            calculation type/base, and the statutory employee/employer rates and contribution cap).
            The `countryCode` query parameter (a 2–3 letter ISO-style code) selects the country and
            returns no items when missing or unknown. Optionally filter by `nature`
            (`Ingreso` or `Egreso`). Items are ordered by `sortOrder`.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<CompensationConceptTypeResponse>>> GetCompensationConceptTypes(
        [FromQuery] string? countryCode,
        [FromQuery] CompensationNature? nature,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetCompensationConceptTypesQuery(countryCode, nature),
            cancellationToken);
        return this.ToActionResult(result);
    }
}

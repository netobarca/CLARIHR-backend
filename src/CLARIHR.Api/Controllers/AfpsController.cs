using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.PersonnelFiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

// Country-scoped enriched master catalog of pension fund administrators (AFP, RF-007). Authn-only,
// like GeneralCatalogsController: reference data consumed on the employee affiliation form
// (PersonnelFile.afpCode). The generic `general-catalogs/afps` read stays available for code/name
// combos; this endpoint adds the identity/contact columns (DP-03: extra columns are seed-delivered).
[ApiController]
[Authorize]
[Tags("General Catalogs")]
public sealed class AfpsController(IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/afps")]
    [ProducesResponseType<IReadOnlyCollection<AfpResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [SwaggerOperation(
        Summary = "List the AFP master catalog (enriched)",
        Description = """
            Returns the country-scoped master catalog of pension fund administrators (AFP) with their
            identity/contact attributes: `abbreviation`, `address`, `phone`, `fax` and `contactName`
            (nullable — completed by administration). The `countryCode` query parameter (a 2–3 letter
            ISO-style code) selects the country and returns no items when missing or unknown. Items
            are ordered by `sortOrder`. The employee affiliation is stored on the person as `afpCode`.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<AfpResponse>>> GetAfps(
        [FromQuery] string? countryCode,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetAfpsQuery(countryCode), cancellationToken);
        return this.ToActionResult(result);
    }
}

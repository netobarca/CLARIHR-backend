using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.AccountCompanies;
using CLARIHR.Application.Features.LegalRepresentatives;
using CLARIHR.Application.Features.Locations.Countries;
using CLARIHR.Application.Features.OrgStructureCatalogs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

// Read-only onboarding catalogs used to populate the "create company" form: the supported countries, the
// company types available for a country, and the legal-representative position-title and representation-type
// lookups. Extracted from AccountCompaniesController so that controller administers only the Company entity
// (canonical points 1/6/13); the routes are preserved verbatim under `api/v1/account/companies/...` — moving
// them here changed no URL.
//
// These are pre-company reference lookups (no companyPublicId — consumed before a company exists), so authz is
// authn-only ([Authorize], no ownership/RBAC): the family is intentionally excluded from
// [AuthorizationPolicySet]/GovernedFamilyRegex (there is no permission/policy to declare). It IS enrolled in
// the OpenAPI guardrail ("Account Companies Catalogs") so a dropped [Tags]/[SwaggerOperation] fails CI.
[ApiController]
[Authorize]
[Route("api/v1/account/companies")]
[Tags("Account Companies Catalogs")]
public sealed class AccountCompanyCatalogsController(
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("countries")]
    [ProducesResponseType<IReadOnlyCollection<CountryCatalogItemResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Unauthorized)]
    [SwaggerOperation(
        Summary = "List supported countries",
        Description = """
            Returns the catalog of countries available when provisioning a company (ISO country code plus
            display metadata). Authenticated read; no company context is required — this lookup feeds the
            company-creation form before any company exists.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<CountryCatalogItemResponse>>> GetCountries(
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetCountryCatalogItemsQuery(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("company-types")]
    [ProducesResponseType<IReadOnlyCollection<CompanyTypeCatalogItemResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.BadRequest | StandardErrorSet.Unauthorized)]
    [SwaggerOperation(
        Summary = "List company types for a country",
        Description = """
            Returns the active company types available for the given ISO country code, used when provisioning
            a company. The `countryCode` query parameter is required and must be a 2–3 letter ISO-style code;
            a missing or malformed code yields `400`.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<CompanyTypeCatalogItemResponse>>> GetCompanyTypes(
        [FromQuery] string countryCode,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetAvailableCompanyTypesQuery(countryCode), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("legal-representative-position-titles")]
    [ProducesResponseType<IReadOnlyCollection<LegalRepresentativePositionTitleCatalogItemResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Unauthorized)]
    [SwaggerOperation(
        Summary = "List legal-representative position titles",
        Description = """
            Returns the catalog of position titles for a company's initial legal representative, used in the
            company-creation form. Authenticated read; no company context is required.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<LegalRepresentativePositionTitleCatalogItemResponse>>> GetLegalRepresentativePositionTitles(
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetLegalRepresentativePositionTitlesQuery(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("legal-representative-representation-types")]
    [ProducesResponseType<IReadOnlyCollection<LegalRepresentativeRepresentationTypeCatalogItemResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Unauthorized)]
    [SwaggerOperation(
        Summary = "List legal-representative representation types",
        Description = """
            Returns the catalog of representation types for a company's initial legal representative, used in
            the company-creation form. Authenticated read; no company context is required.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<LegalRepresentativeRepresentationTypeCatalogItemResponse>>> GetLegalRepresentativeRepresentationTypes(
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetLegalRepresentativeRepresentationTypesQuery(), cancellationToken);
        return this.ToActionResult(result);
    }
}

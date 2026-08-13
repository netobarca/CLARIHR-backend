using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

// Company-less catalog surface. These are global system catalogs (document types, education-*) and
// country reference catalogs (professions, identification types, banks…) — reference data, not tenant
// data — consumed before a company exists (onboarding) and on every form load. Authz is authn-only
// ([Authorize], no companyId / ownership / RBAC), mirroring AccountCompanyCatalogsController; the family
// is intentionally OUT of GovernedFamilyRegex (no policy to declare) but enrolled in the OpenAPI
// guardrail ("General Catalogs"). Country-scoped catalogs take the country via the `countryCode` query
// parameter and fall back to the CURRENT TENANT's country when it is omitted (H-21): the parameter stays for
// the company-less caller and for cross-country reads, but its absence is no longer an empty list.
[ApiController]
[Authorize]
[Tags("General Catalogs")]
public sealed class GeneralCatalogsController(IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/general-catalogs/{catalogKey}")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelCatalogItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [SwaggerOperation(
        Summary = "List a general catalog",
        Description = """
            Returns the active items of the catalog identified by `catalogKey` (a closed whitelist —
            e.g. `countries`, `currencies`, `banks`, `languages`, `education-careers`,
            `file-document-types`; an unsupported key yields `400`). Authenticated read; no company
            context is required. System-scoped catalogs (education statuses/study types/levels/
            shifts/modalities, document types, `countries`) are global and ignore `countryCode` — they answer
            with no country context at all, which is what the onboarding caller needs. Every other catalog is
            country-scoped: The `countryCode` query parameter (a 2–3
            letter ISO-style code) is OPTIONAL: when omitted the country of the CURRENT TENANT is used, and a
            code that matches no active country is rejected with `400 CATALOG_COUNTRY_UNKNOWN` — an empty list
            now means only that this country's catalog has no rows (H-21). A caller with no tenant (the
            company-less onboarding surface) must send it, or gets `400 CATALOG_COUNTRY_REQUIRED`. Careers are country-scoped despite sitting among the education catalogs. Items are ordered by `sortOrder`.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelCatalogItemResponse>>> GetGeneralCatalogItems(
        string catalogKey,
        [FromQuery] string? countryCode,
        CancellationToken cancellationToken = default)
    {
        if (!GeneralCatalogKeyMap.TryResolveCatalogCategory(catalogKey, out var category))
        {
            return this.ToActionResult(Result<IReadOnlyCollection<PersonnelCatalogItemResponse>>.Failure(
                ErrorCatalog.Validation(new Dictionary<string, string[]>
                {
                    ["catalogKey"] = [$"Catalog key '{catalogKey}' is not supported."]
                })));
        }

        var result = await queryDispatcher.SendAsync(
            new GetPersonnelCatalogItemsQuery(category, countryCode),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/reference-catalogs/{catalogKey}")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelReferenceCatalogItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [SwaggerOperation(
        Summary = "List a reference catalog",
        Description = """
            Returns the active items of the country-scoped reference catalog identified by `catalogKey`
            (a closed whitelist — `professions`, `marital-statuses`, `identification-types`, `kinships`,
            `departments`, `municipalities`; an unsupported key yields `400`). Authenticated read; no
            company context is required. The `countryCode` query parameter (a 2–3
            letter ISO-style code) is OPTIONAL: when omitted the country of the CURRENT TENANT is used, and a
            code that matches no active country is rejected with `400 CATALOG_COUNTRY_UNKNOWN` — an empty list
            now means only that this country's catalog has no rows (H-21). A caller with no tenant (the
            company-less onboarding surface) must send it, or gets `400 CATALOG_COUNTRY_REQUIRED`. Items are ordered by `sortOrder`. For
            hierarchical catalogs, the optional `parentCode` narrows children
            (e.g. `municipalities?countryCode=SV&parentCode={departmentCode}`).
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelReferenceCatalogItemResponse>>> GetReferenceCatalogItems(
        string catalogKey,
        [FromQuery] string? countryCode,
        [FromQuery] string? parentCode,
        CancellationToken cancellationToken = default)
    {
        if (!GeneralCatalogKeyMap.TryResolveReferenceCategory(catalogKey, out var category))
        {
            return this.ToActionResult(Result<IReadOnlyCollection<PersonnelReferenceCatalogItemResponse>>.Failure(
                ErrorCatalog.Validation(new Dictionary<string, string[]>
                {
                    ["catalogKey"] = [$"Reference catalog key '{catalogKey}' is not supported."]
                })));
        }

        var result = await queryDispatcher.SendAsync(
            new GetPersonnelReferenceCatalogItemsQuery(category, countryCode, parentCode),
            cancellationToken);
        return this.ToActionResult(result);
    }
}


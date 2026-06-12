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
// parameter instead of resolving it from a company.
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
            context is required. System-scoped catalogs (education statuses/study types/shifts/
            modalities/careers, document types) are global and ignore `countryCode`; country-scoped
            catalogs (languages, currencies, banks…) require the `countryCode` query parameter (a 2–3
            letter ISO-style code) to select the country and return no items when it is missing or
            unknown. Items are ordered by `sortOrder`.
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
            company context is required. The `countryCode` query parameter (a 2–3 letter ISO-style code)
            is required and scopes the items to that country; items are ordered by `sortOrder`. For
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
            new GetPersonnelReferenceCatalogItemsQuery(category, countryCode ?? string.Empty, parentCode),
            cancellationToken);
        return this.ToActionResult(result);
    }
}


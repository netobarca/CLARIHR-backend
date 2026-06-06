using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
[Tags("General Catalogs")]
public sealed class GeneralCatalogsController(IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/companies/{companyId:guid}/general-catalogs/{catalogKey}")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelCatalogItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "List a general catalog",
        Description = """
            Returns the active items of the catalog identified by `catalogKey` (a closed whitelist —
            e.g. `countries`, `currencies`, `banks`, `languages`, `education-careers`,
            `file-document-types`; an unsupported key yields `400`). System-scoped catalogs (education
            statuses/study types/shifts/modalities/careers, document types) are global; country-scoped
            catalogs (languages, currencies, banks…) are filtered by the authorized company's country.
            Items are ordered by `sortOrder`. Read access is gated by the company's personnel-files
            module and read permission (`403` otherwise).
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelCatalogItemResponse>>> GetGeneralCatalogItems(
        Guid companyId,
        string catalogKey,
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
            new GetPersonnelCatalogItemsQuery(companyId, category),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/companies/{companyId:guid}/reference-catalogs/{catalogKey}")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelReferenceCatalogItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "List a reference catalog",
        Description = """
            Returns the active items of the country-scoped reference catalog identified by `catalogKey`
            (a closed whitelist — `professions`, `marital-statuses`, `identification-types`, `kinships`,
            `departments`, `municipalities`; an unsupported key yields `400`). Items are scoped to the
            authorized company's country and ordered by `sortOrder`. For hierarchical catalogs, the
            optional `parentCode` narrows children (e.g. `municipalities?parentCode={departmentCode}`).
            Read access is gated by the company's personnel-files module and read permission (`403`
            otherwise).
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelReferenceCatalogItemResponse>>> GetReferenceCatalogItems(
        Guid companyId,
        string catalogKey,
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
            new GetPersonnelReferenceCatalogItemsQuery(companyId, category, parentCode),
            cancellationToken);
        return this.ToActionResult(result);
    }
}


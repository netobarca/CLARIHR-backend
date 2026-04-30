using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.EducationCatalogs;
using CLARIHR.Application.Features.EducationCatalogs.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

/// <summary>
/// Read-only access to education catalog items for CLARIHR Core users.
/// Administration of these catalogs is performed exclusively via Backoffice.
/// </summary>
[ApiController]
[Route("api/v1/education-catalogs")]
[Authorize]
[Tags("Education Catalogs")]
public sealed class EducationCatalogsController(IQueryDispatcher queryDispatcher) : ControllerBase
{
    private static readonly IReadOnlyDictionary<string, EducationCatalogType> KeyMap =
        new Dictionary<string, EducationCatalogType>(StringComparer.OrdinalIgnoreCase)
        {
            ["education-statuses"]   = EducationCatalogType.EducationStatus,
            ["study-types"]          = EducationCatalogType.StudyType,
            ["careers"]              = EducationCatalogType.Career,
            ["shifts"]               = EducationCatalogType.Shift,
            ["modalities"]           = EducationCatalogType.Modality
        };

    /// <summary>
    /// Returns a paged list of active education catalog items for use in forms.
    /// </summary>
    // GET api/v1/education-catalogs/{catalogKey}
    [HttpGet("{catalogKey}")]
    [ProducesResponseType(typeof(PagedResponse<EducationCatalogLookup>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<EducationCatalogLookup>>> Search(
        string catalogKey,
        [FromQuery] bool? isActive,
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = EducationCatalogValidationRules.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        if (!TryMapKey(catalogKey, out var catalogType))
        {
            return NotFound();
        }

        var query = new SearchEducationCatalogLookupQuery(catalogType, isActive, search, pageNumber, pageSize);
        var result = await queryDispatcher.SendAsync(query, cancellationToken);
        return this.ToActionResult(result);
    }

    /// <summary>
    /// Returns a single active education catalog item by its public Id.
    /// </summary>
    // GET api/v1/education-catalogs/{catalogKey}/{id}
    [HttpGet("{catalogKey}/{id:guid}")]
    [ProducesResponseType(typeof(EducationCatalogLookup), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EducationCatalogLookup>> GetById(
        string catalogKey,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!TryMapKey(catalogKey, out var catalogType))
        {
            return NotFound();
        }

        var query = new GetEducationCatalogActiveLookupByIdQuery(catalogType, id);
        var result = await queryDispatcher.SendAsync(query, cancellationToken);
        return this.ToActionResult(result);
    }

    private static bool TryMapKey(string catalogKey, out EducationCatalogType catalogType) =>
        KeyMap.TryGetValue(catalogKey, out catalogType);
}

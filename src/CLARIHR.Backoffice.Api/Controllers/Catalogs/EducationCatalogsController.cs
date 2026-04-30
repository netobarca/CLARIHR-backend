using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.EducationCatalogs;
using CLARIHR.Application.Features.EducationCatalogs.Common;
using CLARIHR.Backoffice.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Backoffice.Api.Controllers.Catalogs;

[ApiController]
[Route("api/platform/education-catalogs")]
[Authorize(Policy = "PlatformOperator")]
[Tags("Education Catalogs")]
public sealed class EducationCatalogsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher)
    : ControllerBase
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

    // GET api/platform/education-catalogs/{catalogKey}
    [HttpGet("{catalogKey}")]
    [ProducesResponseType(typeof(PagedResponse<EducationCatalogItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResponse<EducationCatalogItemResponse>>> Search(
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

        var query = new SearchEducationCatalogItemsQuery(catalogType, isActive, search, pageNumber, pageSize);
        var result = await queryDispatcher.SendAsync(query, cancellationToken);
        return this.ToActionResult(result);
    }

    // GET api/platform/education-catalogs/{catalogKey}/{id}
    [HttpGet("{catalogKey}/{id:guid}")]
    [ProducesResponseType(typeof(EducationCatalogItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EducationCatalogItemResponse>> GetById(
        string catalogKey,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        if (!TryMapKey(catalogKey, out var catalogType))
        {
            return NotFound();
        }

        var query = new GetEducationCatalogItemByIdQuery(catalogType, id);
        var result = await queryDispatcher.SendAsync(query, cancellationToken);
        return this.ToActionResult(result);
    }

    // POST api/platform/education-catalogs/{catalogKey}
    [HttpPost("{catalogKey}")]
    [ProducesResponseType(typeof(EducationCatalogItemResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EducationCatalogItemResponse>> Create(
        string catalogKey,
        [FromBody] CreateEducationCatalogItemRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryMapKey(catalogKey, out var catalogType))
        {
            return NotFound();
        }

        var command = new CreateEducationCatalogItemCommand(catalogType, request.Code, request.Name, request.SortOrder);
        var result = await commandDispatcher.SendAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetById), new { catalogKey, id = result.Value!.Id }, result.Value);
    }

    // PUT api/platform/education-catalogs/{catalogKey}/{id}
    [HttpPut("{catalogKey}/{id:guid}")]
    [ProducesResponseType(typeof(EducationCatalogItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EducationCatalogItemResponse>> Update(
        string catalogKey,
        Guid id,
        [FromBody] UpdateEducationCatalogItemRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryMapKey(catalogKey, out var catalogType))
        {
            return NotFound();
        }

        var command = new UpdateEducationCatalogItemCommand(
            catalogType, id, request.Code, request.Name, request.SortOrder, request.ConcurrencyToken);
        var result = await commandDispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }

    // PATCH api/platform/education-catalogs/{catalogKey}/{id}/activate
    [HttpPatch("{catalogKey}/{id:guid}/activate")]
    [ProducesResponseType(typeof(EducationCatalogItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EducationCatalogItemResponse>> Activate(
        string catalogKey,
        Guid id,
        [FromBody] ConcurrencyTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryMapKey(catalogKey, out var catalogType))
        {
            return NotFound();
        }

        var command = new ActivateEducationCatalogItemCommand(catalogType, id, request.ConcurrencyToken);
        var result = await commandDispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }

    // PATCH api/platform/education-catalogs/{catalogKey}/{id}/inactivate
    [HttpPatch("{catalogKey}/{id:guid}/inactivate")]
    [ProducesResponseType(typeof(EducationCatalogItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EducationCatalogItemResponse>> Inactivate(
        string catalogKey,
        Guid id,
        [FromBody] ConcurrencyTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryMapKey(catalogKey, out var catalogType))
        {
            return NotFound();
        }

        var command = new InactivateEducationCatalogItemCommand(catalogType, id, request.ConcurrencyToken);
        var result = await commandDispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }

    private static bool TryMapKey(string catalogKey, out EducationCatalogType catalogType) =>
        KeyMap.TryGetValue(catalogKey, out catalogType);
}

// ─── Request contracts ────────────────────────────────────────────────────────

public sealed record CreateEducationCatalogItemRequest(string Code, string Name, int SortOrder);

public sealed record UpdateEducationCatalogItemRequest(
    string Code,
    string Name,
    int SortOrder,
    Guid ConcurrencyToken);

public sealed record ConcurrencyTokenRequest(Guid ConcurrencyToken);

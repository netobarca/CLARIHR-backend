using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.EducationCatalogs;
using CLARIHR.Application.Features.EducationCatalogs.Common;
using CLARIHR.Backoffice.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

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
    // Career CRUD is DEFERRED since the catalog became country-scoped + enriched (RF-009, DP-03/DP-06):
    // this thin global contract cannot carry country/FK/extra columns, so careers are seed-administered
    // and the "careers" key resolves to 404 here. Reads stay on general-catalogs/education-careers.
    private static readonly IReadOnlyDictionary<string, EducationCatalogType> KeyMap =
        new Dictionary<string, EducationCatalogType>(StringComparer.OrdinalIgnoreCase)
        {
            ["education-statuses"]   = EducationCatalogType.EducationStatus,
            ["study-types"]          = EducationCatalogType.StudyType,
            ["shifts"]               = EducationCatalogType.Shift,
            ["modalities"]           = EducationCatalogType.Modality,
            ["levels"]               = EducationCatalogType.Level
        };

    // GET api/platform/education-catalogs/{catalogKey}
    [HttpGet("{catalogKey}")]
    [ProducesResponseType(typeof(PagedResponse<EducationCatalogItemResponse>), StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query | StandardErrorSet.NotFound)]
    [SwaggerOperation(
        Summary = "Search education catalog items",
        Description = "Returns a paged list of the system-wide catalog identified by `catalogKey` (e.g. `careers`, `study-types`). An unknown `catalogKey` yields `404`.")]
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
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get an education catalog item",
        Description = "Returns a single catalog item. The current `concurrencyToken` is included in the body for use in the `If-Match` header of a subsequent update.")]
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
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Create an education catalog item",
        Description = "Creates a catalog item in the catalog identified by `catalogKey`. Returns `201`; the current `concurrencyToken` is included in the body and the `ETag` header.")]
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

        // The GetById `id` (Guid) route parameter is rewritten to `publicId` by
        // PublicContractRouteConvention; the (string) `catalogKey` parameter is unchanged.
        return this.ToCreatedAtActionResult(
            result,
            nameof(GetById),
            value => new { catalogKey, publicId = value.Id },
            value => value.ConcurrencyToken);
    }

    // PUT api/platform/education-catalogs/{catalogKey}/{id}
    [HttpPut("{catalogKey}/{id:guid}")]
    [ProducesResponseType(typeof(EducationCatalogItemResponse), StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Update an education catalog item",
        Description = "Replaces the editable fields (code, name, sort order). Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in the body and the `ETag` header.")]
    public async Task<ActionResult<EducationCatalogItemResponse>> Update(
        string catalogKey,
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateEducationCatalogItemRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!TryMapKey(catalogKey, out var catalogType))
        {
            return NotFound();
        }

        var command = new UpdateEducationCatalogItemCommand(
            catalogType, id, request.Code, request.Name, request.SortOrder, concurrencyToken);
        var result = await commandDispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    // PATCH api/platform/education-catalogs/{catalogKey}/{id}
    [HttpPatch("{catalogKey}/{id:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType(typeof(EducationCatalogItemResponse), StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Patch an education catalog item (RFC 6902 JSON Patch)",
        Description = "Applies a partial update using JSON Patch (RFC 6902), media type `application/json-patch+json`. Patchable paths: `/code`, `/name`, `/sortOrder`. Activation state changes use the `/activate` and `/inactivate` actions. Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in the body and the `ETag` header.")]
    public async Task<ActionResult<EducationCatalogItemResponse>> Patch(
        string catalogKey,
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchEducationCatalogItemRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        if (!TryMapKey(catalogKey, out var catalogType))
        {
            return NotFound();
        }

        var command = new PatchEducationCatalogItemCommand(
            catalogType,
            id,
            concurrencyToken,
            JsonPatchOperationMapper.Map(
                patchDoc,
                static (op, path, from, value) => new EducationCatalogItemPatchOperation(op, path, from, value)));
        var result = await commandDispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    // PATCH api/platform/education-catalogs/{catalogKey}/{id}/activate
    [HttpPatch("{catalogKey}/{id:guid}/activate")]
    [ProducesResponseType(typeof(EducationCatalogItemResponse), StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Activate an education catalog item",
        Description = "Activates the item. Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in the body and the `ETag` header.")]
    public async Task<ActionResult<EducationCatalogItemResponse>> Activate(
        string catalogKey,
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        if (!TryMapKey(catalogKey, out var catalogType))
        {
            return NotFound();
        }

        var command = new ActivateEducationCatalogItemCommand(catalogType, id, concurrencyToken);
        var result = await commandDispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    // PATCH api/platform/education-catalogs/{catalogKey}/{id}/inactivate
    [HttpPatch("{catalogKey}/{id:guid}/inactivate")]
    [ProducesResponseType(typeof(EducationCatalogItemResponse), StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Inactivate an education catalog item",
        Description = "Inactivates the item (rejected if it is in use). Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in the body and the `ETag` header.")]
    public async Task<ActionResult<EducationCatalogItemResponse>> Inactivate(
        string catalogKey,
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        if (!TryMapKey(catalogKey, out var catalogType))
        {
            return NotFound();
        }

        var command = new InactivateEducationCatalogItemCommand(catalogType, id, concurrencyToken);
        var result = await commandDispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    private static bool TryMapKey(string catalogKey, out EducationCatalogType catalogType) =>
        KeyMap.TryGetValue(catalogKey, out catalogType);
}

// ─── Request contracts ────────────────────────────────────────────────────────

public sealed record CreateEducationCatalogItemRequest(string Code, string Name, int SortOrder);

public sealed record UpdateEducationCatalogItemRequest(
    string Code,
    string Name,
    int SortOrder);

public sealed class PatchEducationCatalogItemRequest
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}

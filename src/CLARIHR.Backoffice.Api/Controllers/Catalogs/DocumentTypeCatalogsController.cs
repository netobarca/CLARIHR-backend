using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.DocumentTypeCatalogs;
using CLARIHR.Application.Features.DocumentTypeCatalogs.Common;
using CLARIHR.Backoffice.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Backoffice.Api.Controllers.Catalogs;

[ApiController]
[Route("api/platform/document-type-catalogs")]
[Authorize(Policy = "PlatformOperator")]
[Tags("Document Type Catalogs")]
public sealed class DocumentTypeCatalogsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher)
    : ControllerBase
{
    // GET api/platform/document-type-catalogs
    [HttpGet]
    [ProducesResponseType(typeof(PagedResponse<DocumentTypeCatalogItemResponse>), StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "Search document type catalog items",
        Description = "Returns a paged list of system-wide document type catalog items.")]
    public async Task<ActionResult<PagedResponse<DocumentTypeCatalogItemResponse>>> Search(
        [FromQuery] bool? isActive,
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = DocumentTypeCatalogValidationRules.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var query = new SearchDocumentTypeCatalogItemsQuery(isActive, search, pageNumber, pageSize);
        var result = await queryDispatcher.SendAsync(query, cancellationToken);
        return this.ToActionResult(result);
    }

    // GET api/platform/document-type-catalogs/{id}
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DocumentTypeCatalogItemResponse), StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a document type catalog item",
        Description = "Returns a single document type catalog item. The current `concurrencyToken` is included in the body for use in the `If-Match` header of a subsequent update.")]
    public async Task<ActionResult<DocumentTypeCatalogItemResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var query = new GetDocumentTypeCatalogItemByIdQuery(id);
        var result = await queryDispatcher.SendAsync(query, cancellationToken);
        return this.ToActionResult(result);
    }

    // POST api/platform/document-type-catalogs
    [HttpPost]
    [ProducesResponseType(typeof(DocumentTypeCatalogItemResponse), StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Query | StandardErrorSet.Conflict)]
    [SwaggerOperation(
        Summary = "Create a document type catalog item",
        Description = "Creates a document type catalog item. Returns `201`; the current `concurrencyToken` is included in the body and the `ETag` header.")]
    public async Task<ActionResult<DocumentTypeCatalogItemResponse>> Create(
        [FromBody] CreateDocumentTypeCatalogItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new CreateDocumentTypeCatalogItemCommand(request.Code, request.Name, request.SortOrder);
        var result = await commandDispatcher.SendAsync(command, cancellationToken);

        // The GetById route parameter `id` (Guid) is rewritten to `publicId` by
        // PublicContractRouteConvention, so the generated-URL route value must use that external name.
        return this.ToCreatedAtActionResult(
            result,
            nameof(GetById),
            value => new { publicId = value.Id },
            value => value.ConcurrencyToken);
    }

    // PUT api/platform/document-type-catalogs/{id}
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(DocumentTypeCatalogItemResponse), StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Update a document type catalog item",
        Description = "Replaces the editable fields (code, name, sort order). Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in the body and the `ETag` header.")]
    public async Task<ActionResult<DocumentTypeCatalogItemResponse>> Update(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateDocumentTypeCatalogItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateDocumentTypeCatalogItemCommand(
            id, request.Code, request.Name, request.SortOrder, concurrencyToken);
        var result = await commandDispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    // PATCH api/platform/document-type-catalogs/{id}
    [HttpPatch("{id:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType(typeof(DocumentTypeCatalogItemResponse), StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Patch a document type catalog item (RFC 6902 JSON Patch)",
        Description = "Applies a partial update using JSON Patch (RFC 6902), media type `application/json-patch+json`. Patchable paths: `/code`, `/name`, `/sortOrder`. Activation state changes use the `/activate` and `/inactivate` actions. Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in the body and the `ETag` header.")]
    public async Task<ActionResult<DocumentTypeCatalogItemResponse>> Patch(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchDocumentTypeCatalogItemRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var command = new PatchDocumentTypeCatalogItemCommand(
            id,
            concurrencyToken,
            JsonPatchOperationMapper.Map(
                patchDoc,
                static (op, path, from, value) => new DocumentTypeCatalogItemPatchOperation(op, path, from, value)));
        var result = await commandDispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    // PATCH api/platform/document-type-catalogs/{id}/activate
    [HttpPatch("{id:guid}/activate")]
    [ProducesResponseType(typeof(DocumentTypeCatalogItemResponse), StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Activate a document type catalog item",
        Description = "Activates the item. Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in the body and the `ETag` header.")]
    public async Task<ActionResult<DocumentTypeCatalogItemResponse>> Activate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var command = new ActivateDocumentTypeCatalogItemCommand(id, concurrencyToken);
        var result = await commandDispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    // PATCH api/platform/document-type-catalogs/{id}/inactivate
    [HttpPatch("{id:guid}/inactivate")]
    [ProducesResponseType(typeof(DocumentTypeCatalogItemResponse), StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Inactivate a document type catalog item",
        Description = "Inactivates the item (rejected if it is in use). Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in the body and the `ETag` header.")]
    public async Task<ActionResult<DocumentTypeCatalogItemResponse>> Inactivate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var command = new InactivateDocumentTypeCatalogItemCommand(id, concurrencyToken);
        var result = await commandDispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }
}

// ─── Request contracts ────────────────────────────────────────────────────────

public sealed record CreateDocumentTypeCatalogItemRequest(string Code, string Name, int SortOrder);

public sealed record UpdateDocumentTypeCatalogItemRequest(
    string Code,
    string Name,
    int SortOrder);

public sealed class PatchDocumentTypeCatalogItemRequest
{
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }
}

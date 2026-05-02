using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.DocumentTypeCatalogs;
using CLARIHR.Application.Features.DocumentTypeCatalogs.Common;
using CLARIHR.Backoffice.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
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
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DocumentTypeCatalogItemResponse>> Create(
        [FromBody] CreateDocumentTypeCatalogItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new CreateDocumentTypeCatalogItemCommand(request.Code, request.Name, request.SortOrder);
        var result = await commandDispatcher.SendAsync(command, cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value);
    }

    // PUT api/platform/document-type-catalogs/{id}
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(DocumentTypeCatalogItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DocumentTypeCatalogItemResponse>> Update(
        Guid id,
        [FromBody] UpdateDocumentTypeCatalogItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new UpdateDocumentTypeCatalogItemCommand(
            id, request.Code, request.Name, request.SortOrder, request.ConcurrencyToken);
        var result = await commandDispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }

    // PATCH api/platform/document-type-catalogs/{id}/activate
    [HttpPatch("{id:guid}/activate")]
    [ProducesResponseType(typeof(DocumentTypeCatalogItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DocumentTypeCatalogItemResponse>> Activate(
        Guid id,
        [FromBody] ConcurrencyTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new ActivateDocumentTypeCatalogItemCommand(id, request.ConcurrencyToken);
        var result = await commandDispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }

    // PATCH api/platform/document-type-catalogs/{id}/inactivate
    [HttpPatch("{id:guid}/inactivate")]
    [ProducesResponseType(typeof(DocumentTypeCatalogItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DocumentTypeCatalogItemResponse>> Inactivate(
        Guid id,
        [FromBody] ConcurrencyTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var command = new InactivateDocumentTypeCatalogItemCommand(id, request.ConcurrencyToken);
        var result = await commandDispatcher.SendAsync(command, cancellationToken);
        return this.ToActionResult(result);
    }
}

// ─── Request contracts ────────────────────────────────────────────────────────

public sealed record CreateDocumentTypeCatalogItemRequest(string Code, string Name, int SortOrder);

public sealed record UpdateDocumentTypeCatalogItemRequest(
    string Code,
    string Name,
    int SortOrder,
    Guid ConcurrencyToken);

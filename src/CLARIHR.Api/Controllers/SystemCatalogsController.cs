using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.DocumentTypeCatalogs;
using CLARIHR.Application.Features.DocumentTypeCatalogs.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

/// <summary>
/// Exposes system-scoped catalogs (not country-scoped) for read-only access.
/// These catalogs are managed globally by platform operators via Backoffice.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/system-catalogs")]
public sealed class SystemCatalogsController(IQueryDispatcher queryDispatcher) : ControllerBase
{
    /// <summary>
    /// Lists active document type catalog items.
    /// </summary>
    [HttpGet("document-types")]
    [ProducesResponseType(typeof(PagedResponse<DocumentTypeCatalogItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<DocumentTypeCatalogItemResponse>>> GetDocumentTypes(
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = DocumentTypeCatalogValidationRules.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var query = new SearchDocumentTypeCatalogItemsQuery(
            IsActive: true,
            search,
            pageNumber,
            pageSize);
        var result = await queryDispatcher.SendAsync(query, cancellationToken);
        return this.ToActionResult(result);
    }
}

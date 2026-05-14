using System.Text.Json;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PositionDescriptionCatalogs;
using CLARIHR.Application.Features.PositionDescriptionCatalogs.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
[Consumes("application/json")]
[Produces("application/json")]
public sealed class PositionDescriptionCatalogItemsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher)
    : ControllerBase
{
    [HttpGet("companies/{companyPublicId:guid}/position-description-catalogs/{catalogType}/items")]
    [ProducesResponseType<PagedResponse<PositionDescriptionCatalogItemResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    public async Task<ActionResult<PagedResponse<PositionDescriptionCatalogItemResponse>>> Get(
        Guid companyPublicId,
        string catalogType,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PositionDescriptionCatalogValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        if (!PositionDescriptionCatalogRouteMap.TryResolve(catalogType, out var resolvedCatalogType))
        {
            return this.ToActionResult(Result<PagedResponse<PositionDescriptionCatalogItemResponse>>.Failure(
                PositionDescriptionCatalogErrors.InvalidCatalogType));
        }

        var result = await queryDispatcher.SendAsync(
            new SearchPositionDescriptionCatalogItemsQuery(
                companyPublicId,
                resolvedCatalogType,
                isActive,
                search,
                page,
                pageSize,
                includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("position-description-catalogs/{catalogType}/items/{positionDescriptionCatalogItemPublicId:guid}")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    public async Task<ActionResult<PositionDescriptionCatalogItemResponse>> GetById(
        string catalogType,
        Guid positionDescriptionCatalogItemPublicId,
        CancellationToken cancellationToken = default)
    {
        if (!PositionDescriptionCatalogRouteMap.TryResolve(catalogType, out var resolvedCatalogType))
        {
            return this.ToActionResult(Result<PositionDescriptionCatalogItemResponse>.Failure(
                PositionDescriptionCatalogErrors.InvalidCatalogType));
        }

        var result = await queryDispatcher.SendAsync(
            new GetPositionDescriptionCatalogItemByIdQuery(positionDescriptionCatalogItemPublicId),
            cancellationToken);

        if (result.IsFailure || result.Value.CatalogType == resolvedCatalogType)
        {
            return this.ToActionResult(result);
        }

        return this.ToActionResult(Result<PositionDescriptionCatalogItemResponse>.Failure(
            PositionDescriptionCatalogErrors.CatalogItemNotFound));
    }

    [HttpPost("companies/{companyPublicId:guid}/position-description-catalogs/{catalogType}/items")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<PositionDescriptionCatalogItemResponse>> Add(
        Guid companyPublicId,
        string catalogType,
        [FromBody] UpsertPositionDescriptionCatalogItemRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!PositionDescriptionCatalogRouteMap.TryResolve(catalogType, out var resolvedCatalogType))
        {
            return this.ToActionResult(Result<PositionDescriptionCatalogItemResponse>.Failure(
                PositionDescriptionCatalogErrors.InvalidCatalogType));
        }

        var result = await commandDispatcher.SendAsync(
            new CreatePositionDescriptionCatalogItemCommand(
                companyPublicId,
                resolvedCatalogType,
                request.Code,
                request.Name,
                request.Description,
                request.SortOrder),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(result).Result!;
        }

        return Created($"/api/v1/position-description-catalogs/{catalogType}/items/{result.Value.Id:D}", result.Value);
    }

    [HttpPatch("position-description-catalogs/{catalogType}/items/{positionDescriptionCatalogItemPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [ProducesResponseType<PositionDescriptionCatalogItemResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<PositionDescriptionCatalogItemResponse>> Patch(
        string catalogType,
        Guid positionDescriptionCatalogItemPublicId,
        [FromBody] JsonPatchDocument<PatchPositionDescriptionCatalogItemRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        if (patchDoc is null)
        {
            return BadRequest(ProblemDetailsFactory.CreateProblemDetails(
                HttpContext,
                statusCode: StatusCodes.Status400BadRequest,
                detail: "Invalid patch document."));
        }

        if (!PositionDescriptionCatalogRouteMap.TryResolve(catalogType, out var resolvedCatalogType))
        {
            return this.ToActionResult(Result<PositionDescriptionCatalogItemResponse>.Failure(
                PositionDescriptionCatalogErrors.InvalidCatalogType));
        }

        var result = await commandDispatcher.SendAsync(
            new PatchPositionDescriptionCatalogItemCommand(
                positionDescriptionCatalogItemPublicId,
                resolvedCatalogType,
                MapPatchOperations(patchDoc)),
            cancellationToken);

        return this.ToActionResult(result);
    }

    private static IReadOnlyCollection<PositionDescriptionCatalogPatchOperation> MapPatchOperations(
        JsonPatchDocument<PatchPositionDescriptionCatalogItemRequest> patchDoc) =>
        patchDoc.Operations
            .Select(operation => new PositionDescriptionCatalogPatchOperation(
                operation.op,
                operation.path,
                operation.from,
                MapPatchValue(operation.value)))
            .ToArray();

    private static JsonElement? MapPatchValue(object? value)
    {
        if (value is null)
        {
            return JsonSerializer.SerializeToElement<object?>(null);
        }

        if (value is JToken token)
        {
            using var document = JsonDocument.Parse(token.ToString(Newtonsoft.Json.Formatting.None));
            return document.RootElement.Clone();
        }

        return JsonSerializer.SerializeToElement(value, value.GetType());
    }

    public sealed class UpsertPositionDescriptionCatalogItemRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int SortOrder { get; set; }
    }

    public sealed class PatchPositionDescriptionCatalogItemRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public Guid ConcurrencyToken { get; set; }
    }
}

using System.Text.Json;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
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
public sealed class PositionCategoriesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher)
    : ControllerBase
{
    [HttpGet("companies/{companyPublicId:guid}/position-categories")]
    [ProducesResponseType<PagedResponse<PositionCategoryResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    public async Task<ActionResult<PagedResponse<PositionCategoryResponse>>> Get(
        Guid companyPublicId,
        [FromQuery] Guid? classificationPublicId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PositionDescriptionCatalogValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchPositionCategoriesQuery(
                companyPublicId,
                classificationPublicId,
                isActive,
                search,
                page,
                pageSize,
                includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("position-categories/{positionCategoryPublicId:guid}")]
    [ProducesResponseType<PositionCategoryResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    public async Task<ActionResult<PositionCategoryResponse>> GetById(
        Guid positionCategoryPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPositionCategoryByIdQuery(positionCategoryPublicId),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost("companies/{companyPublicId:guid}/position-categories")]
    [ProducesResponseType<PositionCategoryResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<PositionCategoryResponse>> Add(
        Guid companyPublicId,
        [FromBody] UpsertPositionCategoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreatePositionCategoryCommand(
                companyPublicId,
                request.Code,
                request.Name,
                request.Description,
                request.ClassificationPublicId,
                request.SortOrder),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(result).Result!;
        }

        return Created($"/api/v1/position-categories/{result.Value.Id:D}", result.Value);
    }

    [HttpPatch("position-categories/{positionCategoryPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [ProducesResponseType<PositionCategoryResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<PositionCategoryResponse>> Patch(
        Guid positionCategoryPublicId,
        [FromBody] JsonPatchDocument<PatchPositionCategoryRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        if (patchDoc is null)
        {
            return BadRequest(ProblemDetailsFactory.CreateProblemDetails(
                HttpContext,
                statusCode: StatusCodes.Status400BadRequest,
                detail: "Invalid patch document."));
        }

        var result = await commandDispatcher.SendAsync(
            new PatchPositionCategoryCommand(
                positionCategoryPublicId,
                MapPatchOperations(patchDoc)),
            cancellationToken);

        return this.ToActionResult(result);
    }

    private static IReadOnlyCollection<PositionDescriptionCatalogPatchOperation> MapPatchOperations(
        JsonPatchDocument<PatchPositionCategoryRequest> patchDoc) =>
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

    public sealed class UpsertPositionCategoryRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid ClassificationPublicId { get; set; }
        public int SortOrder { get; set; }
    }

    public sealed class PatchPositionCategoryRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid ClassificationPublicId { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public Guid ConcurrencyToken { get; set; }
    }
}

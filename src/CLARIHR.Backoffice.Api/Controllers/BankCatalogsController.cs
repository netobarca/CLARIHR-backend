using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Banks;
using CLARIHR.Application.Features.Banks.Common;
using CLARIHR.Backoffice.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Backoffice.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/platform/bank-catalogs")]
[Tags("Bank Catalogs")]
public sealed class BankCatalogsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<BankCatalogItemResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "Search bank catalog items by country",
        Description = "Returns a paged list of global bank catalog items for the given `countryCode`.")]
    public async Task<ActionResult<PagedResponse<BankCatalogItemResponse>>> Search(
        [FromQuery] string countryCode,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = BankCatalogValidationRules.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchBankCatalogItemsQuery(countryCode, isActive, search, page, pageSize),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("{publicId:guid}")]
    [ProducesResponseType<BankCatalogItemResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a bank catalog item",
        Description = "Returns a single bank catalog item. The current `concurrencyToken` is included in the body for use in the `If-Match` header of a subsequent update.")]
    public async Task<ActionResult<BankCatalogItemResponse>> GetById(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetBankCatalogItemByIdQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType<BankCatalogItemResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Query | StandardErrorSet.NotFound | StandardErrorSet.Conflict)]
    [SwaggerOperation(
        Summary = "Create a bank catalog item",
        Description = "Creates a global bank catalog item for a country. Returns `201`; the current `concurrencyToken` is included in the body and the `ETag` header.")]
    public async Task<ActionResult<BankCatalogItemResponse>> Create(
        [FromBody] UpsertBankCatalogItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateBankCatalogItemCommand(
                request.CountryCode,
                request.Code,
                request.Name,
                request.Alias,
                request.SwiftCode,
                request.RoutingCode,
                request.SortOrder),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetById),
            value => new { publicId = value.PublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("{publicId:guid}")]
    [ProducesResponseType<BankCatalogItemResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Update a bank catalog item",
        Description = "Replaces the editable fields. The country cannot be changed. Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in the body and the `ETag` header.")]
    public async Task<ActionResult<BankCatalogItemResponse>> Update(
        Guid publicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateBankCatalogItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateBankCatalogItemCommand(
                publicId,
                request.CountryCode,
                request.Code,
                request.Name,
                request.Alias,
                request.SwiftCode,
                request.RoutingCode,
                request.SortOrder,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("{publicId:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<BankCatalogItemResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Patch a bank catalog item (RFC 6902 JSON Patch)",
        Description = "Applies a partial update using JSON Patch (RFC 6902), media type `application/json-patch+json`. Patchable paths: `/code`, `/name`, `/alias`, `/swiftCode`, `/routingCode`, `/sortOrder` (the optional string fields accept `null` or remove to clear them). The country is immutable and activation state uses the `/activate` and `/inactivate` actions. Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in the body and the `ETag` header.")]
    public async Task<ActionResult<BankCatalogItemResponse>> Patch(
        Guid publicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchBankCatalogItemRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchBankCatalogItemCommand(
                publicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(
                    patchDoc,
                    static (op, path, from, value) => new BankCatalogItemPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("{publicId:guid}/activate")]
    [ProducesResponseType<BankCatalogItemResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Activate a bank catalog item",
        Description = "Activates the item. Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in the body and the `ETag` header.")]
    public async Task<ActionResult<BankCatalogItemResponse>> Activate(
        Guid publicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateBankCatalogItemCommand(publicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("{publicId:guid}/inactivate")]
    [ProducesResponseType<BankCatalogItemResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Inactivate a bank catalog item",
        Description = "Inactivates the item. Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in the body and the `ETag` header.")]
    public async Task<ActionResult<BankCatalogItemResponse>> Inactivate(
        Guid publicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateBankCatalogItemCommand(publicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    public sealed record UpsertBankCatalogItemRequest(
        string CountryCode,
        string Code,
        string Name,
        string? Alias,
        string? SwiftCode,
        string? RoutingCode,
        int SortOrder);

    public sealed record UpdateBankCatalogItemRequest(
        string CountryCode,
        string Code,
        string Name,
        string? Alias,
        string? SwiftCode,
        string? RoutingCode,
        int SortOrder);

    public sealed class PatchBankCatalogItemRequest
    {
        public string Code { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string? Alias { get; set; }

        public string? SwiftCode { get; set; }

        public string? RoutingCode { get; set; }

        public int SortOrder { get; set; }
    }
}

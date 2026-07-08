using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using CLARIHR.Api.Authorization;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.EmployeeRelations;
using CLARIHR.Application.Features.EmployeeRelations.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}")]
[Tags("Recognition Types")]
[AuthorizationPolicySet(EmployeeRelationsConfigurationPolicies.Read, EmployeeRelationsConfigurationPolicies.Manage)]
[ResourceActions(EmployeeRelationsConfigurationPermissionCodes.RecognitionTypesResourceKey)]
public sealed class RecognitionTypesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("companies/{companyId:guid}/recognition-types")]
    [ProducesResponseType<PagedResponse<RecognitionTypeListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "List recognition types for a company",
        Description = """
            Returns a paginated list of the company's recognition types ("tipos de reconocimiento",
            REQ-003 D-06 — the master ships with a seeded template), filterable by `isActive` and
            free-text `q` over the code and name. The owning company is validated against the
            authenticated tenant. Set `includeAllowedActions=true` to receive per-item read/manage flags.
            """)]
    public async Task<ActionResult<PagedResponse<RecognitionTypeListItemResponse>>> Search(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery, Range(1, EmployeeRelationsConfigurationValidationRules.MaxPageSize)] int pageSize = EmployeeRelationsConfigurationValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchRecognitionTypesQuery(companyId, isActive, search, page, pageSize, includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("recognition-types/{id:guid}")]
    [ProducesResponseType<RecognitionTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a recognition type by id",
        Description = """
            Returns a single recognition type by its public id. The owning company is resolved from the
            authenticated tenant; a non-existent id yields `404`, while an id that belongs to another
            tenant yields `403 TENANT_MISMATCH`. The current `concurrencyToken` is emitted as the `ETag`
            header on mutations.
            """)]
    public async Task<ActionResult<RecognitionTypeResponse>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetRecognitionTypeByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("companies/{companyId:guid}/recognition-types")]
    [ProducesResponseType<RecognitionTypeResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Create a recognition type",
        Description = """
            Creates a recognition type under the company and returns `201 Created` with the `Location`
            header pointing to the new resource and the `ETag` header carrying its initial
            `concurrencyToken`. A duplicate active code yields `409`.
            """)]
    public async Task<ActionResult<RecognitionTypeResponse>> Create(
        Guid companyId,
        [FromBody] CreateRecognitionTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateRecognitionTypeCommand(companyId, request.Code, request.Name, request.SortOrder),
            cancellationToken);

        // The PublicContractRouteConvention rewrites the GetById route token `{id}` to `{publicId}`,
        // so the Location route value MUST be keyed `publicId` (not `id`).
        return this.ToCreatedAtActionResult(
            result,
            nameof(GetById),
            value => new { publicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPut("recognition-types/{id:guid}")]
    [ProducesResponseType<RecognitionTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Update a recognition type",
        Description = """
            Replaces the editable fields of a recognition type (code, name, sort order). Requires the
            current `concurrencyToken` in the `If-Match` header; a missing/malformed header yields `400`
            and a stale token yields `409 CONCURRENCY_CONFLICT`. A duplicate active code yields `409`.
            """)]
    public async Task<ActionResult<RecognitionTypeResponse>> Update(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateRecognitionTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateRecognitionTypeCommand(id, request.Code, request.Name, request.SortOrder, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("recognition-types/{id:guid}/activate")]
    [ProducesResponseType<RecognitionTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Activate a recognition type",
        Description = """
            Reactivates an inactive recognition type. Requires the current `concurrencyToken` in the
            `If-Match` header (missing → `400`, stale → `409`). If another active type already uses the
            same code, activation yields `409`.
            """)]
    public async Task<ActionResult<RecognitionTypeResponse>> Activate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateRecognitionTypeCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("recognition-types/{id:guid}/inactivate")]
    [ProducesResponseType<RecognitionTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Inactivate a recognition type",
        Description = """
            Deactivates (soft-delete) a recognition type. A type referenced by an active record yields
            `422 RECOGNITION_TYPE_IN_USE`. Requires the current `concurrencyToken` in the `If-Match`
            header (missing → `400`, stale → `409`).
            """)]
    public async Task<ActionResult<RecognitionTypeResponse>> Inactivate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateRecognitionTypeCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    public sealed record CreateRecognitionTypeRequest(string Code, string Name, int SortOrder = 0);

    public sealed record UpdateRecognitionTypeRequest(string Code, string Name, int SortOrder = 0);
}

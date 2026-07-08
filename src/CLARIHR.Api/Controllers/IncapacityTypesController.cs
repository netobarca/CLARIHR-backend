using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using CLARIHR.Api.Authorization;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Leave;
using CLARIHR.Application.Features.Leave.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}")]
[Tags("Incapacity Types")]
[AuthorizationPolicySet(LeaveConfigurationPolicies.Read, LeaveConfigurationPolicies.Manage)]
[ResourceActions(LeaveConfigurationPermissionCodes.IncapacityTypesResourceKey)]
public sealed class IncapacityTypesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("companies/{companyId:guid}/incapacity-types")]
    [ProducesResponseType<PagedResponse<IncapacityTypeListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "List incapacity types for a company",
        Description = """
            Returns a paginated list of the company's incapacity types, filterable by
            `isActive` and free-text `q` over code and name. The owning company is validated
            against the authenticated tenant. Set `includeAllowedActions=true` to receive
            per-item read/manage flags.
            """)]
    public async Task<ActionResult<PagedResponse<IncapacityTypeListItemResponse>>> Search(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery, Range(1, LeaveConfigurationValidationRules.MaxPageSize)] int pageSize = LeaveConfigurationValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchIncapacityTypesQuery(companyId, isActive, search, page, pageSize, includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("incapacity-types/{id:guid}")]
    [ProducesResponseType<IncapacityTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get an incapacity type by id",
        Description = """
            Returns a single incapacity type by its public id. The owning company is resolved
            from the authenticated tenant; a non-existent id yields `404`, while an id that
            belongs to another tenant yields `403 TENANT_MISMATCH`. The current
            `concurrencyToken` is emitted as the `ETag` header on mutations.
            """)]
    public async Task<ActionResult<IncapacityTypeResponse>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetIncapacityTypeByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("companies/{companyId:guid}/incapacity-types")]
    [ProducesResponseType<IncapacityTypeResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Create an incapacity type",
        Description = """
            Creates an incapacity type under the company and returns `201 Created` with the
            `Location` header pointing to the new resource and the `ETag` header carrying its
            initial `concurrencyToken`. A duplicate code yields `409`.
            """)]
    public async Task<ActionResult<IncapacityTypeResponse>> Create(
        Guid companyId,
        [FromBody] CreateIncapacityTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateIncapacityTypeCommand(
                companyId,
                request.Code,
                request.Name,
                request.DeductionTypeText,
                request.IncomeTypeText,
                request.AppliesToWorkAccident),
            cancellationToken);

        // The PublicContractRouteConvention rewrites the GetById route token `{id}` to
        // `{publicId}`, so the Location route value MUST be keyed `publicId` (not `id`) or
        // link generation fails. Mirrors CostCentersController's POST.
        return this.ToCreatedAtActionResult(
            result,
            nameof(GetById),
            value => new { publicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPut("incapacity-types/{id:guid}")]
    [ProducesResponseType<IncapacityTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Update an incapacity type",
        Description = """
            Replaces the editable fields of an incapacity type (code, name, deduction/income
            type texts, work-accident flag). Requires the current `concurrencyToken` in the
            `If-Match` header; a missing/malformed header yields `400` and a stale token
            yields `409 CONCURRENCY_CONFLICT`. A duplicate code yields `409`. The refreshed
            token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<IncapacityTypeResponse>> Update(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateIncapacityTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateIncapacityTypeCommand(
                id,
                request.Code,
                request.Name,
                request.DeductionTypeText,
                request.IncomeTypeText,
                request.AppliesToWorkAccident,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("incapacity-types/{id:guid}/activate")]
    [ProducesResponseType<IncapacityTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Activate an incapacity type",
        Description = """
            Reactivates an inactive incapacity type. Requires the current `concurrencyToken`
            in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is
            returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<IncapacityTypeResponse>> Activate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateIncapacityTypeCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("incapacity-types/{id:guid}/inactivate")]
    [ProducesResponseType<IncapacityTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Inactivate an incapacity type",
        Description = """
            Deactivates (soft-delete) an incapacity type. Requires the current
            `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The
            refreshed token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<IncapacityTypeResponse>> Inactivate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateIncapacityTypeCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    public sealed record CreateIncapacityTypeRequest(
        string Code,
        string Name,
        string? DeductionTypeText,
        string? IncomeTypeText,
        bool AppliesToWorkAccident);

    public sealed record UpdateIncapacityTypeRequest(
        string Code,
        string Name,
        string? DeductionTypeText,
        string? IncomeTypeText,
        bool AppliesToWorkAccident);
}

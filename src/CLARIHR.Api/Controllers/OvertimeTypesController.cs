using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using CLARIHR.Api.Authorization;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PersonnelFiles.Overtime;
using CLARIHR.Application.Features.PersonnelFiles.Overtime.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}")]
[Tags("Overtime Types")]
[AuthorizationPolicySet(OvertimeConfigurationPolicies.Read, OvertimeConfigurationPolicies.Manage)]
[ResourceActions(OvertimeConfigurationResourceKeys.OvertimeTypes)]
public sealed class OvertimeTypesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("companies/{companyId:guid}/overtime-types")]
    [ProducesResponseType<PagedResponse<OvertimeTypeListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "List overtime types for a company",
        Description = """
            Returns a paginated list of the company's overtime types ("tipos de hora extra", REQ-007 — the
            master ships with a seeded template with reference factors), filterable by `isActive` and
            free-text `q` over the code and name. Each type carries its `defaultFactor` (the reference
            multiplier applied to the base hour) and an optional `payrollEffectDescription`. The owning
            company is validated against the authenticated tenant. Set `includeAllowedActions=true` to
            receive per-item read/manage flags.
            """)]
    public async Task<ActionResult<PagedResponse<OvertimeTypeListItemResponse>>> Search(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery, Range(1, OvertimeConfigurationValidationRules.MaxPageSize)] int pageSize = OvertimeConfigurationValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchOvertimeTypesQuery(companyId, isActive, search, page, pageSize, includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("overtime-types/{id:guid}")]
    [ProducesResponseType<OvertimeTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get an overtime type by id",
        Description = """
            Returns a single overtime type by its public id. The owning company is resolved from the
            authenticated tenant; a non-existent id yields `404`, while an id that belongs to another tenant
            yields `403 TENANT_MISMATCH`. The current `concurrencyToken` is emitted as the `ETag` header on
            mutations.
            """)]
    public async Task<ActionResult<OvertimeTypeResponse>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetOvertimeTypeByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("companies/{companyId:guid}/overtime-types")]
    [ProducesResponseType<OvertimeTypeResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Create an overtime type",
        Description = """
            Creates an overtime type under the company and returns `201 Created` with the `Location` header
            pointing to the new resource and the `ETag` header carrying its initial `concurrencyToken`. The
            `defaultFactor` (the reference multiplier applied to the base hour) must be greater than zero. A
            duplicate active code yields `409`.
            """)]
    public async Task<ActionResult<OvertimeTypeResponse>> Create(
        Guid companyId,
        [FromBody] CreateOvertimeTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateOvertimeTypeCommand(
                companyId,
                request.Code,
                request.Name,
                request.DefaultFactor,
                request.PayrollEffectDescription,
                request.SortOrder),
            cancellationToken);

        // The PublicContractRouteConvention rewrites the GetById route token `{id}` to `{publicId}`,
        // so the Location route value MUST be keyed `publicId` (not `id`).
        return this.ToCreatedAtActionResult(
            result,
            nameof(GetById),
            value => new { publicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPut("overtime-types/{id:guid}")]
    [ProducesResponseType<OvertimeTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Update an overtime type",
        Description = """
            Replaces the editable fields of an overtime type (code, name, default factor, payroll-effect
            description, sort order). Requires the current `concurrencyToken` in the `If-Match` header; a
            missing/malformed header yields `400` and a stale token yields `409 CONCURRENCY_CONFLICT`. A
            duplicate active code yields `409`.
            """)]
    public async Task<ActionResult<OvertimeTypeResponse>> Update(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateOvertimeTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateOvertimeTypeCommand(
                id,
                request.Code,
                request.Name,
                request.DefaultFactor,
                request.PayrollEffectDescription,
                request.SortOrder,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("overtime-types/{id:guid}/activation")]
    [ProducesResponseType<OvertimeTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Activate an overtime type",
        Description = """
            Reactivates an inactive overtime type. Requires the current `concurrencyToken` in the `If-Match`
            header (missing → `400`, stale → `409`). If another active type already uses the same code,
            activation yields `409`.
            """)]
    public async Task<ActionResult<OvertimeTypeResponse>> Activate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateOvertimeTypeCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("overtime-types/{id:guid}/inactivation")]
    [ProducesResponseType<OvertimeTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Inactivate an overtime type",
        Description = """
            Deactivates (soft-delete) an overtime type. A type referenced by an active record yields
            `422 OVERTIME_TYPE_IN_USE`. Requires the current `concurrencyToken` in the `If-Match` header
            (missing → `400`, stale → `409`).
            """)]
    public async Task<ActionResult<OvertimeTypeResponse>> Inactivate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateOvertimeTypeCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    public sealed record CreateOvertimeTypeRequest(
        string Code,
        string Name,
        decimal DefaultFactor,
        string? PayrollEffectDescription = null,
        int SortOrder = 0);

    public sealed record UpdateOvertimeTypeRequest(
        string Code,
        string Name,
        decimal DefaultFactor,
        string? PayrollEffectDescription = null,
        int SortOrder = 0);
}

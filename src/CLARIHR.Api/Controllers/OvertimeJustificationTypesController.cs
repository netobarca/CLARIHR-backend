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
[Tags("Overtime Justification Types")]
[AuthorizationPolicySet(OvertimeConfigurationPolicies.Read, OvertimeConfigurationPolicies.Manage)]
[ResourceActions(OvertimeConfigurationResourceKeys.OvertimeJustificationTypes)]
public sealed class OvertimeJustificationTypesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("companies/{companyId:guid}/overtime-justification-types")]
    [ProducesResponseType<PagedResponse<OvertimeJustificationTypeListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "List overtime justification types for a company",
        Description = """
            Returns a paginated list of the company's overtime justification types ("tipos de justificación
            de hora extra", REQ-007 — the master ships with a seeded template), filterable by `isActive` and
            free-text `q` over the code and name. The owning company is validated against the authenticated
            tenant. Set `includeAllowedActions=true` to receive per-item read/manage flags.
            """)]
    public async Task<ActionResult<PagedResponse<OvertimeJustificationTypeListItemResponse>>> Search(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery, Range(1, OvertimeConfigurationValidationRules.MaxPageSize)] int pageSize = OvertimeConfigurationValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchOvertimeJustificationTypesQuery(companyId, isActive, search, page, pageSize, includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("overtime-justification-types/{id:guid}")]
    [ProducesResponseType<OvertimeJustificationTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get an overtime justification type by id",
        Description = """
            Returns a single overtime justification type by its public id. The owning company is resolved
            from the authenticated tenant; a non-existent id yields `404`, while an id that belongs to
            another tenant yields `403 TENANT_MISMATCH`. The current `concurrencyToken` is emitted as the
            `ETag` header on mutations.
            """)]
    public async Task<ActionResult<OvertimeJustificationTypeResponse>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetOvertimeJustificationTypeByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("companies/{companyId:guid}/overtime-justification-types")]
    [ProducesResponseType<OvertimeJustificationTypeResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Create an overtime justification type",
        Description = """
            Creates an overtime justification type under the company and returns `201 Created` with the
            `Location` header pointing to the new resource and the `ETag` header carrying its initial
            `concurrencyToken`. A duplicate active code yields `409`.
            """)]
    public async Task<ActionResult<OvertimeJustificationTypeResponse>> Create(
        Guid companyId,
        [FromBody] CreateOvertimeJustificationTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateOvertimeJustificationTypeCommand(
                companyId,
                request.Code,
                request.Name,
                request.Description,
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

    [HttpPut("overtime-justification-types/{id:guid}")]
    [ProducesResponseType<OvertimeJustificationTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Update an overtime justification type",
        Description = """
            Replaces the editable fields of an overtime justification type (code, name, description, sort
            order). Requires the current `concurrencyToken` in the `If-Match` header; a missing/malformed
            header yields `400` and a stale token yields `409 CONCURRENCY_CONFLICT`. A duplicate active code
            yields `409`.
            """)]
    public async Task<ActionResult<OvertimeJustificationTypeResponse>> Update(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateOvertimeJustificationTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateOvertimeJustificationTypeCommand(
                id,
                request.Code,
                request.Name,
                request.Description,
                request.SortOrder,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("overtime-justification-types/{id:guid}/activation")]
    [ProducesResponseType<OvertimeJustificationTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Activate an overtime justification type",
        Description = """
            Reactivates an inactive overtime justification type. Requires the current `concurrencyToken` in
            the `If-Match` header (missing → `400`, stale → `409`). If another active type already uses the
            same code, activation yields `409`.
            """)]
    public async Task<ActionResult<OvertimeJustificationTypeResponse>> Activate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateOvertimeJustificationTypeCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("overtime-justification-types/{id:guid}/inactivation")]
    [ProducesResponseType<OvertimeJustificationTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Inactivate an overtime justification type",
        Description = """
            Deactivates (soft-delete) an overtime justification type. A type referenced by an active record
            yields `422 OVERTIME_JUSTIFICATION_TYPE_IN_USE`. Requires the current `concurrencyToken` in the
            `If-Match` header (missing → `400`, stale → `409`).
            """)]
    public async Task<ActionResult<OvertimeJustificationTypeResponse>> Inactivate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateOvertimeJustificationTypeCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    public sealed record CreateOvertimeJustificationTypeRequest(
        string Code,
        string Name,
        string? Description = null,
        int SortOrder = 0);

    public sealed record UpdateOvertimeJustificationTypeRequest(
        string Code,
        string Name,
        string? Description = null,
        int SortOrder = 0);
}

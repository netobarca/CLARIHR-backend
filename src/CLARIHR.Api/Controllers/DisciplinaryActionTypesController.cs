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
[Tags("Disciplinary Action Types")]
[AuthorizationPolicySet(EmployeeRelationsConfigurationPolicies.Read, EmployeeRelationsConfigurationPolicies.Manage)]
[ResourceActions(EmployeeRelationsConfigurationPermissionCodes.DisciplinaryActionTypesResourceKey)]
public sealed class DisciplinaryActionTypesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("companies/{companyId:guid}/disciplinary-action-types")]
    [ProducesResponseType<PagedResponse<DisciplinaryActionTypeListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "List disciplinary-action types for a company",
        Description = """
            Returns a paginated list of the company's disciplinary-action types ("tipos de
            amonestación", REQ-003 D-06 — the master ships with a seeded template), filterable by
            `isActive`, `appliesSuspension` (whether the type carries a suspension block) and free-text
            `q` over the code and name. Set `includeAllowedActions=true` for per-item read/manage flags.
            """)]
    public async Task<ActionResult<PagedResponse<DisciplinaryActionTypeListItemResponse>>> Search(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery] bool? appliesSuspension,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery, Range(1, EmployeeRelationsConfigurationValidationRules.MaxPageSize)] int pageSize = EmployeeRelationsConfigurationValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchDisciplinaryActionTypesQuery(companyId, isActive, appliesSuspension, search, page, pageSize, includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("disciplinary-action-types/{id:guid}")]
    [ProducesResponseType<DisciplinaryActionTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a disciplinary-action type by id",
        Description = """
            Returns a single disciplinary-action type by its public id. A non-existent id yields `404`,
            an id that belongs to another tenant yields `403 TENANT_MISMATCH`.
            """)]
    public async Task<ActionResult<DisciplinaryActionTypeResponse>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetDisciplinaryActionTypeByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("companies/{companyId:guid}/disciplinary-action-types")]
    [ProducesResponseType<DisciplinaryActionTypeResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Create a disciplinary-action type",
        Description = """
            Creates a disciplinary-action type under the company. `appliesSuspension` declares whether a
            disciplinary action of this type carries a suspension block (default false). Returns
            `201 Created` with `Location` and `ETag`; a duplicate active code yields `409`.
            """)]
    public async Task<ActionResult<DisciplinaryActionTypeResponse>> Create(
        Guid companyId,
        [FromBody] CreateDisciplinaryActionTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateDisciplinaryActionTypeCommand(companyId, request.Code, request.Name, request.AppliesSuspension, request.SortOrder),
            cancellationToken);

        // The PublicContractRouteConvention rewrites the GetById route token `{id}` to `{publicId}`.
        return this.ToCreatedAtActionResult(
            result,
            nameof(GetById),
            value => new { publicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPut("disciplinary-action-types/{id:guid}")]
    [ProducesResponseType<DisciplinaryActionTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Update a disciplinary-action type",
        Description = """
            Replaces the editable fields of a disciplinary-action type (code, name, appliesSuspension,
            sort order). Changing `appliesSuspension` does NOT rewrite existing records (the record
            snapshots the flag at creation). Requires `If-Match` (missing → `400`, stale → `409`); a
            duplicate active code yields `409`.
            """)]
    public async Task<ActionResult<DisciplinaryActionTypeResponse>> Update(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateDisciplinaryActionTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateDisciplinaryActionTypeCommand(id, request.Code, request.Name, request.AppliesSuspension, request.SortOrder, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("disciplinary-action-types/{id:guid}/activate")]
    [ProducesResponseType<DisciplinaryActionTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Activate a disciplinary-action type",
        Description = """
            Reactivates an inactive disciplinary-action type. Requires `If-Match` (missing → `400`,
            stale → `409`). If another active type already uses the same code, activation yields `409`.
            """)]
    public async Task<ActionResult<DisciplinaryActionTypeResponse>> Activate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateDisciplinaryActionTypeCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("disciplinary-action-types/{id:guid}/inactivate")]
    [ProducesResponseType<DisciplinaryActionTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Inactivate a disciplinary-action type",
        Description = """
            Deactivates (soft-delete) a disciplinary-action type. A type referenced by an active record
            yields `422 DISCIPLINARY_ACTION_TYPE_IN_USE`. Requires `If-Match` (missing → `400`, stale → `409`).
            """)]
    public async Task<ActionResult<DisciplinaryActionTypeResponse>> Inactivate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateDisciplinaryActionTypeCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    public sealed record CreateDisciplinaryActionTypeRequest(
        string Code,
        string Name,
        bool AppliesSuspension = false,
        int SortOrder = 0);

    public sealed record UpdateDisciplinaryActionTypeRequest(
        string Code,
        string Name,
        bool AppliesSuspension = false,
        int SortOrder = 0);
}

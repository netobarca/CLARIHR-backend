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
[Tags("Disciplinary Action Causes")]
[AuthorizationPolicySet(EmployeeRelationsConfigurationPolicies.Read, EmployeeRelationsConfigurationPolicies.Manage)]
[ResourceActions(EmployeeRelationsConfigurationPermissionCodes.DisciplinaryActionCausesResourceKey)]
public sealed class DisciplinaryActionCausesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("companies/{companyId:guid}/disciplinary-action-causes")]
    [ProducesResponseType<PagedResponse<DisciplinaryActionCauseListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "List disciplinary-action causes for a company",
        Description = """
            Returns a paginated list of the company's disciplinary-action causes ("causas de
            amonestación", REQ-003 D-06 — the master ships with a seeded template, all causes without a
            deduction concept), filterable by `isActive` and free-text `q` over the code and name. Set
            `includeAllowedActions=true` for per-item read/manage flags.
            """)]
    public async Task<ActionResult<PagedResponse<DisciplinaryActionCauseListItemResponse>>> Search(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery, Range(1, EmployeeRelationsConfigurationValidationRules.MaxPageSize)] int pageSize = EmployeeRelationsConfigurationValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchDisciplinaryActionCausesQuery(companyId, isActive, search, page, pageSize, includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("disciplinary-action-causes/{id:guid}")]
    [ProducesResponseType<DisciplinaryActionCauseResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a disciplinary-action cause by id",
        Description = """
            Returns a single disciplinary-action cause by its public id. A non-existent id yields `404`,
            an id that belongs to another tenant yields `403 TENANT_MISMATCH`.
            """)]
    public async Task<ActionResult<DisciplinaryActionCauseResponse>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetDisciplinaryActionCauseByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("companies/{companyId:guid}/disciplinary-action-causes")]
    [ProducesResponseType<DisciplinaryActionCauseResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Create a disciplinary-action cause",
        Description = """
            Creates a disciplinary-action cause under the company. The optional
            `deductionConceptTypeCode` is the default egreso concept for a payroll deduction; it is
            validated against the tenant's country `compensation-concept-types` catalog (active,
            egreso) and yields `422 DEDUCTION_CONCEPT_INVALID` when invalid. Returns `201 Created` with
            `Location` and `ETag`; a duplicate active code yields `409`.
            """)]
    public async Task<ActionResult<DisciplinaryActionCauseResponse>> Create(
        Guid companyId,
        [FromBody] CreateDisciplinaryActionCauseRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateDisciplinaryActionCauseCommand(companyId, request.Code, request.Name, request.DeductionConceptTypeCode, request.SortOrder),
            cancellationToken);

        // The PublicContractRouteConvention rewrites the GetById route token `{id}` to `{publicId}`.
        return this.ToCreatedAtActionResult(
            result,
            nameof(GetById),
            value => new { publicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPut("disciplinary-action-causes/{id:guid}")]
    [ProducesResponseType<DisciplinaryActionCauseResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Update a disciplinary-action cause",
        Description = """
            Replaces the editable fields of a disciplinary-action cause (code, name,
            deductionConceptTypeCode, sort order). A travelling invalid `deductionConceptTypeCode` yields
            `422 DEDUCTION_CONCEPT_INVALID`. Requires `If-Match` (missing → `400`, stale → `409`); a
            duplicate active code yields `409`.
            """)]
    public async Task<ActionResult<DisciplinaryActionCauseResponse>> Update(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateDisciplinaryActionCauseRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateDisciplinaryActionCauseCommand(id, request.Code, request.Name, request.DeductionConceptTypeCode, request.SortOrder, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("disciplinary-action-causes/{id:guid}/activate")]
    [ProducesResponseType<DisciplinaryActionCauseResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Activate a disciplinary-action cause",
        Description = """
            Reactivates an inactive disciplinary-action cause. Requires `If-Match` (missing → `400`,
            stale → `409`). If another active cause already uses the same code, activation yields `409`.
            """)]
    public async Task<ActionResult<DisciplinaryActionCauseResponse>> Activate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateDisciplinaryActionCauseCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("disciplinary-action-causes/{id:guid}/inactivate")]
    [ProducesResponseType<DisciplinaryActionCauseResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Inactivate a disciplinary-action cause",
        Description = """
            Deactivates (soft-delete) a disciplinary-action cause. A cause referenced by an active record
            yields `422 DISCIPLINARY_ACTION_CAUSE_IN_USE`. Requires `If-Match` (missing → `400`, stale → `409`).
            """)]
    public async Task<ActionResult<DisciplinaryActionCauseResponse>> Inactivate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateDisciplinaryActionCauseCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    public sealed record CreateDisciplinaryActionCauseRequest(
        string Code,
        string Name,
        string? DeductionConceptTypeCode = null,
        int SortOrder = 0);

    public sealed record UpdateDisciplinaryActionCauseRequest(
        string Code,
        string Name,
        string? DeductionConceptTypeCode = null,
        int SortOrder = 0);
}

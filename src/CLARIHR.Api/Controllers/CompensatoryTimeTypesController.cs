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
[Tags("Compensatory Time Types")]
[AuthorizationPolicySet(LeaveConfigurationPolicies.Read, LeaveConfigurationPolicies.Manage)]
[ResourceActions(LeaveConfigurationPermissionCodes.CompensatoryTimeTypesResourceKey)]
public sealed class CompensatoryTimeTypesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("companies/{companyId:guid}/compensatory-time-types")]
    [ProducesResponseType<PagedResponse<CompensatoryTimeTypeListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "List compensatory-time types for a company",
        Description = """
            Returns a paginated list of the company's compensatory-time types (REQ-002 D-05 —
            the master starts empty; the administrator creates the types), filterable by
            `isActive`, `operationCode` (ACREDITA / DEBITA / AMBAS) and free-text `q` over the
            code and name. The owning company is validated against the authenticated tenant.
            Set `includeAllowedActions=true` to receive per-item read/manage flags.
            """)]
    public async Task<ActionResult<PagedResponse<CompensatoryTimeTypeListItemResponse>>> Search(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery] string? operationCode,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery, Range(1, LeaveConfigurationValidationRules.MaxPageSize)] int pageSize = LeaveConfigurationValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchCompensatoryTimeTypesQuery(companyId, isActive, operationCode, search, page, pageSize, includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("compensatory-time-types/{id:guid}")]
    [ProducesResponseType<CompensatoryTimeTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a compensatory-time type by id",
        Description = """
            Returns a single compensatory-time type by its public id. The owning company is
            resolved from the authenticated tenant; a non-existent id yields `404`, while an id
            that belongs to another tenant yields `403 TENANT_MISMATCH`. The current
            `concurrencyToken` is emitted as the `ETag` header on mutations.
            """)]
    public async Task<ActionResult<CompensatoryTimeTypeResponse>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetCompensatoryTimeTypeByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("companies/{companyId:guid}/compensatory-time-types")]
    [ProducesResponseType<CompensatoryTimeTypeResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Create a compensatory-time type",
        Description = """
            Creates a compensatory-time type under the company and returns `201 Created` with the
            `Location` header pointing to the new resource and the `ETag` header carrying its
            initial `concurrencyToken`. `operationCode` must be one of ACREDITA / DEBITA / AMBAS,
            `creditFactor` must be greater than zero (default 1.00, snapshotted per credit).
            A duplicate active code yields `409`.
            """)]
    public async Task<ActionResult<CompensatoryTimeTypeResponse>> Create(
        Guid companyId,
        [FromBody] CreateCompensatoryTimeTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateCompensatoryTimeTypeCommand(
                companyId,
                request.Code,
                request.Name,
                request.OperationCode,
                request.CreditFactor,
                request.SortOrder),
            cancellationToken);

        // The PublicContractRouteConvention rewrites the GetById route token `{id}` to `{publicId}`,
        // so the Location route value MUST be keyed `publicId` (not `id`). Mirrors MedicalClinicsController.
        return this.ToCreatedAtActionResult(
            result,
            nameof(GetById),
            value => new { publicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPut("compensatory-time-types/{id:guid}")]
    [ProducesResponseType<CompensatoryTimeTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Update a compensatory-time type",
        Description = """
            Replaces the editable fields of a compensatory-time type (code, name, operation code,
            credit factor, sort order). Editing `creditFactor` does NOT recompute historical
            credits (the factor is snapshotted per credit, RN-02). Requires the current
            `concurrencyToken` in the `If-Match` header; a missing/malformed header yields `400`
            and a stale token yields `409 CONCURRENCY_CONFLICT`. A duplicate active code yields
            `409`. The refreshed token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<CompensatoryTimeTypeResponse>> Update(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateCompensatoryTimeTypeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateCompensatoryTimeTypeCommand(
                id,
                request.Code,
                request.Name,
                request.OperationCode,
                request.CreditFactor,
                request.SortOrder,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("compensatory-time-types/{id:guid}/activate")]
    [ProducesResponseType<CompensatoryTimeTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Activate a compensatory-time type",
        Description = """
            Reactivates an inactive compensatory-time type. Requires the current `concurrencyToken`
            in the `If-Match` header (missing → `400`, stale → `409`). If another active type
            already uses the same code, activation yields `409`. The refreshed token is returned in
            the body and the `ETag` header.
            """)]
    public async Task<ActionResult<CompensatoryTimeTypeResponse>> Activate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateCompensatoryTimeTypeCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("compensatory-time-types/{id:guid}/inactivate")]
    [ProducesResponseType<CompensatoryTimeTypeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Inactivate a compensatory-time type",
        Description = """
            Deactivates (soft-delete) a compensatory-time type. A type referenced by an active
            record yields `422 COMPENSATORY_TIME_TYPE_IN_USE`. Requires the current
            `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The
            refreshed token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<CompensatoryTimeTypeResponse>> Inactivate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateCompensatoryTimeTypeCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    public sealed record CreateCompensatoryTimeTypeRequest(
        string Code,
        string Name,
        string OperationCode,
        decimal CreditFactor = 1.00m,
        int SortOrder = 0);

    public sealed record UpdateCompensatoryTimeTypeRequest(
        string Code,
        string Name,
        string OperationCode,
        decimal CreditFactor = 1.00m,
        int SortOrder = 0);
}

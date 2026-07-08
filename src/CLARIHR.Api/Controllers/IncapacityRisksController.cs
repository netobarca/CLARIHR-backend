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
[Tags("Incapacity Risks")]
[AuthorizationPolicySet(LeaveConfigurationPolicies.Read, LeaveConfigurationPolicies.Manage)]
[ResourceActions(LeaveConfigurationPermissionCodes.IncapacityRisksResourceKey)]
public sealed class IncapacityRisksController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("companies/{companyId:guid}/incapacity-risks")]
    [ProducesResponseType<PagedResponse<IncapacityRiskListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "List incapacity risks for a company",
        Description = """
            Returns a paginated list of the company's incapacity risks, filterable by
            `isActive` and free-text `q` over code and name. Each item carries the risk's
            day-counting flags and the count of its subsidy parameters (`parameterCount`);
            the full tranche set travels only on the by-id read. The owning company is
            validated against the authenticated tenant. Set `includeAllowedActions=true` to
            receive per-item read/manage flags.
            """)]
    public async Task<ActionResult<PagedResponse<IncapacityRiskListItemResponse>>> Search(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery, Range(1, LeaveConfigurationValidationRules.MaxPageSize)] int pageSize = LeaveConfigurationValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchIncapacityRisksQuery(companyId, isActive, search, page, pageSize, includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("incapacity-risks/{id:guid}")]
    [ProducesResponseType<IncapacityRiskResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get an incapacity risk by id",
        Description = """
            Returns a single incapacity risk by its public id, including its subsidy
            parameters ordered by `sortOrder`. The owning company is resolved from the
            authenticated tenant; a non-existent id yields `404`, while an id that belongs to
            another tenant yields `403 TENANT_MISMATCH`. The current `concurrencyToken` is
            emitted as the `ETag` header on mutations.
            """)]
    public async Task<ActionResult<IncapacityRiskResponse>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetIncapacityRiskByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("companies/{companyId:guid}/incapacity-risks")]
    [ProducesResponseType<IncapacityRiskResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Create an incapacity risk",
        Description = """
            Creates an incapacity risk under the company and returns `201 Created` with the
            `Location` header pointing to the new resource and the `ETag` header carrying its
            initial `concurrencyToken`. The subsidy `parameters` are optional in the POST:
            when they travel, the tranche set must start at day 1, be contiguous and only the
            last tranche may be open-ended (`dayTo = null`), with payer codes `ISSS`,
            `EMPRESA` or `SIN_PAGO` (`422 RISK_PARAMETERS_INVALID` otherwise; a risk without
            `hasSubsidy` cannot define tranches). A duplicate code yields `409`.
            """)]
    public async Task<ActionResult<IncapacityRiskResponse>> Create(
        Guid companyId,
        [FromBody] CreateIncapacityRiskRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateIncapacityRiskCommand(
                companyId,
                request.Code,
                request.Name,
                request.CountsSeventhDay,
                request.CountsSaturday,
                request.CountsHoliday,
                request.UsesWorkSchedule,
                request.AllowsIndefinite,
                request.AllowsExtension,
                request.UsesFund,
                request.HasSubsidy,
                request.Parameters ?? []),
            cancellationToken);

        // The PublicContractRouteConvention rewrites the GetById route token `{id}` to
        // `{publicId}`, so the Location route value MUST be keyed `publicId` (not `id`) or
        // link generation fails. Mirrors MedicalClinicsController's POST.
        return this.ToCreatedAtActionResult(
            result,
            nameof(GetById),
            value => new { publicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPut("incapacity-risks/{id:guid}")]
    [ProducesResponseType<IncapacityRiskResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Update an incapacity risk",
        Description = """
            Replaces the scalar fields of an incapacity risk (code, name and the
            day-counting/behavior flags). The subsidy parameters are NOT touched here — use
            `PUT /incapacity-risks/{id}/parameters` to replace the tranche set. Turning off
            `hasSubsidy` while tranches still exist yields
            `422 INCAPACITY_RISK_RULE_VIOLATION`. Requires the current `concurrencyToken` in
            the `If-Match` header; a missing/malformed header yields `400` and a stale token
            yields `409 CONCURRENCY_CONFLICT`. A duplicate code yields `409`. The refreshed
            token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<IncapacityRiskResponse>> Update(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateIncapacityRiskRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateIncapacityRiskCommand(
                id,
                request.Code,
                request.Name,
                request.CountsSeventhDay,
                request.CountsSaturday,
                request.CountsHoliday,
                request.UsesWorkSchedule,
                request.AllowsIndefinite,
                request.AllowsExtension,
                request.UsesFund,
                request.HasSubsidy,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPut("incapacity-risks/{id:guid}/parameters")]
    [ProducesResponseType<IncapacityRiskResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace the subsidy parameters of an incapacity risk",
        Description = """
            Replaces the FULL subsidy tranche set of the risk in one shot. The set must start
            at day 1, be contiguous and only the last tranche may be open-ended
            (`dayTo = null`), with payer codes `ISSS`, `EMPRESA` or `SIN_PAGO`; a risk with
            `hasSubsidy` requires at least one tranche and a risk without it only accepts an
            empty set (`422 RISK_PARAMETERS_INVALID` otherwise). Requires the current
            `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`).
            Returns the full risk with the new tranche set; the refreshed token is returned
            in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<IncapacityRiskResponse>> ReplaceParameters(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] ReplaceIncapacityRiskParametersRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplaceIncapacityRiskParametersCommand(
                id,
                request.Parameters ?? [],
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("incapacity-risks/{id:guid}/activate")]
    [ProducesResponseType<IncapacityRiskResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Activate an incapacity risk",
        Description = """
            Reactivates an inactive incapacity risk. Requires the current `concurrencyToken`
            in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is
            returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<IncapacityRiskResponse>> Activate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateIncapacityRiskCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("incapacity-risks/{id:guid}/inactivate")]
    [ProducesResponseType<IncapacityRiskResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Inactivate an incapacity risk",
        Description = """
            Deactivates (soft-delete) an incapacity risk. Requires the current
            `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The
            refreshed token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<IncapacityRiskResponse>> Inactivate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateIncapacityRiskCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    public sealed record CreateIncapacityRiskRequest(
        string Code,
        string Name,
        bool CountsSeventhDay,
        bool CountsSaturday,
        bool CountsHoliday,
        bool UsesWorkSchedule,
        bool AllowsIndefinite,
        bool AllowsExtension,
        bool UsesFund,
        bool HasSubsidy,
        IReadOnlyCollection<IncapacityRiskParameterInputModel>? Parameters = null);

    public sealed record UpdateIncapacityRiskRequest(
        string Code,
        string Name,
        bool CountsSeventhDay,
        bool CountsSaturday,
        bool CountsHoliday,
        bool UsesWorkSchedule,
        bool AllowsIndefinite,
        bool AllowsExtension,
        bool UsesFund,
        bool HasSubsidy);

    public sealed record ReplaceIncapacityRiskParametersRequest(
        IReadOnlyCollection<IncapacityRiskParameterInputModel>? Parameters);
}

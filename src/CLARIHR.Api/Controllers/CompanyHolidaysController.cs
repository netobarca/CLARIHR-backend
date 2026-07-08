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
[Tags("Company Holidays")]
[AuthorizationPolicySet(LeaveConfigurationPolicies.Read, LeaveConfigurationPolicies.Manage)]
[ResourceActions(LeaveConfigurationPermissionCodes.CompanyHolidaysResourceKey)]
public sealed class CompanyHolidaysController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("companies/{companyId:guid}/company-holidays")]
    [ProducesResponseType<PagedResponse<CompanyHolidayListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "List company holidays for a company",
        Description = """
            Returns a paginated list of the company's holidays ("días de asueto"), filterable
            by `year` (calendar year of the holiday date), `scopeCode`
            (NACIONAL/LOCAL/INSTITUCIONAL) and `isActive`, ordered by date ascending. The
            owning company is validated against the authenticated tenant. Set
            `includeAllowedActions=true` to receive per-item read/manage flags.
            """)]
    public async Task<ActionResult<PagedResponse<CompanyHolidayListItemResponse>>> Search(
        Guid companyId,
        [FromQuery] int? year,
        [FromQuery] string? scopeCode,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery, Range(1, LeaveConfigurationValidationRules.MaxPageSize)] int pageSize = LeaveConfigurationValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchCompanyHolidaysQuery(companyId, year, scopeCode, isActive, page, pageSize, includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("company-holidays/{id:guid}")]
    [ProducesResponseType<CompanyHolidayResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a company holiday by id",
        Description = """
            Returns a single company holiday by its public id. The owning company is resolved
            from the authenticated tenant; a non-existent id yields `404`, while an id that
            belongs to another tenant yields `403 TENANT_MISMATCH`. The current
            `concurrencyToken` is emitted as the `ETag` header on mutations.
            """)]
    public async Task<ActionResult<CompanyHolidayResponse>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetCompanyHolidayByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("companies/{companyId:guid}/company-holidays")]
    [ProducesResponseType<CompanyHolidayResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Create a company holiday",
        Description = """
            Creates a company holiday under the company and returns `201 Created` with the
            `Location` header pointing to the new resource and the `ETag` header carrying its
            initial `concurrencyToken`. `scopeCode` must be one of
            NACIONAL/LOCAL/INSTITUCIONAL (`400` otherwise). A duplicate date yields
            `409 HOLIDAY_DUPLICATE`.
            """)]
    public async Task<ActionResult<CompanyHolidayResponse>> Create(
        Guid companyId,
        [FromBody] CreateCompanyHolidayRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateCompanyHolidayCommand(
                companyId,
                request.Date,
                request.Description,
                request.ScopeCode),
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

    [HttpPut("company-holidays/{id:guid}")]
    [ProducesResponseType<CompanyHolidayResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Update a company holiday",
        Description = """
            Replaces the editable fields of a company holiday (date, description, scope
            code). `scopeCode` must be one of NACIONAL/LOCAL/INSTITUCIONAL (`400` otherwise).
            Requires the current `concurrencyToken` in the `If-Match` header; a
            missing/malformed header yields `400` and a stale token yields
            `409 CONCURRENCY_CONFLICT`. A duplicate date yields `409 HOLIDAY_DUPLICATE`. The
            refreshed token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<CompanyHolidayResponse>> Update(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateCompanyHolidayRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateCompanyHolidayCommand(
                id,
                request.Date,
                request.Description,
                request.ScopeCode,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("company-holidays/{id:guid}/activate")]
    [ProducesResponseType<CompanyHolidayResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Activate a company holiday",
        Description = """
            Reactivates an inactive company holiday. Requires the current `concurrencyToken`
            in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is
            returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<CompanyHolidayResponse>> Activate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateCompanyHolidayCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("company-holidays/{id:guid}/inactivate")]
    [ProducesResponseType<CompanyHolidayResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Inactivate a company holiday",
        Description = """
            Deactivates (soft-delete) a company holiday. Requires the current
            `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The
            refreshed token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<CompanyHolidayResponse>> Inactivate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateCompanyHolidayCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    public sealed record CreateCompanyHolidayRequest(
        DateOnly Date,
        string Description,
        string ScopeCode);

    public sealed record UpdateCompanyHolidayRequest(
        DateOnly Date,
        string Description,
        string ScopeCode);
}

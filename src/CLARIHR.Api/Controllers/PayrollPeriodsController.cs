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
[Tags("Payroll Periods")]
[AuthorizationPolicySet(LeaveConfigurationPolicies.Read, LeaveConfigurationPolicies.Manage)]
[ResourceActions(LeaveConfigurationPermissionCodes.PayrollPeriodsResourceKey)]
public sealed class PayrollPeriodsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("companies/{companyId:guid}/payroll-periods")]
    [ProducesResponseType<PagedResponse<PayrollPeriodListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "List payroll periods for a company",
        Description = """
            Returns a paginated list of the company's payroll period definitions, filterable
            by `payPeriodTypeCode` (pay-periods catalog code), `year` and `isActive`, ordered
            by year descending then number ascending. The owning company is validated against
            the authenticated tenant. Set `includeAllowedActions=true` to receive per-item
            read/manage flags.
            """)]
    public async Task<ActionResult<PagedResponse<PayrollPeriodListItemResponse>>> Search(
        Guid companyId,
        [FromQuery] string? payPeriodTypeCode,
        [FromQuery] int? year,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery, Range(1, LeaveConfigurationValidationRules.MaxPageSize)] int pageSize = LeaveConfigurationValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchPayrollPeriodsQuery(companyId, payPeriodTypeCode, year, isActive, page, pageSize, includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("payroll-periods/{id:guid}")]
    [ProducesResponseType<PayrollPeriodResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a payroll period by id",
        Description = """
            Returns a single payroll period by its public id. The owning company is resolved
            from the authenticated tenant; a non-existent id yields `404`, while an id that
            belongs to another tenant yields `403 TENANT_MISMATCH`. The current
            `concurrencyToken` is emitted as the `ETag` header on mutations.
            """)]
    public async Task<ActionResult<PayrollPeriodResponse>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPayrollPeriodByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("companies/{companyId:guid}/payroll-periods")]
    [ProducesResponseType<PayrollPeriodResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Create a payroll period",
        Description = """
            Creates a payroll period under the company and returns `201 Created` with the
            `Location` header pointing to the new resource and the `ETag` header carrying its
            initial `concurrencyToken`. `payPeriodTypeCode` must be an active code of the
            country-scoped pay-periods catalog (`422 PAYROLL_PERIOD_TYPE_INVALID` otherwise).
            A duplicate (type, year, number) yields `409 PAYROLL_PERIOD_DUPLICATE`; a date
            range overlapping another active period of the same type and year yields
            `422 PAYROLL_PERIOD_OVERLAP`.
            """)]
    public async Task<ActionResult<PayrollPeriodResponse>> Create(
        Guid companyId,
        [FromBody] CreatePayrollPeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreatePayrollPeriodCommand(
                companyId,
                request.PayPeriodTypeCode,
                request.Year,
                request.Number,
                request.Label,
                request.StartDate,
                request.EndDate),
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

    [HttpPut("payroll-periods/{id:guid}")]
    [ProducesResponseType<PayrollPeriodResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Update a payroll period",
        Description = """
            Replaces the editable fields of a payroll period (type, year, number, label,
            dates). `payPeriodTypeCode` must be an active code of the country-scoped
            pay-periods catalog (`422` otherwise); a duplicate (type, year, number) yields
            `409` and an overlapping date range yields `422 PAYROLL_PERIOD_OVERLAP`. Requires
            the current `concurrencyToken` in the `If-Match` header; a missing/malformed
            header yields `400` and a stale token yields `409 CONCURRENCY_CONFLICT`. The
            refreshed token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<PayrollPeriodResponse>> Update(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdatePayrollPeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePayrollPeriodCommand(
                id,
                request.PayPeriodTypeCode,
                request.Year,
                request.Number,
                request.Label,
                request.StartDate,
                request.EndDate,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("payroll-periods/{id:guid}/activate")]
    [ProducesResponseType<PayrollPeriodResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Activate a payroll period",
        Description = """
            Reactivates an inactive payroll period. Requires the current `concurrencyToken`
            in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is
            returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<PayrollPeriodResponse>> Activate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivatePayrollPeriodCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("payroll-periods/{id:guid}/inactivate")]
    [ProducesResponseType<PayrollPeriodResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Inactivate a payroll period",
        Description = """
            Deactivates (soft-delete) a payroll period. Requires the current
            `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The
            refreshed token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<PayrollPeriodResponse>> Inactivate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivatePayrollPeriodCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    public sealed record CreatePayrollPeriodRequest(
        string PayPeriodTypeCode,
        int Year,
        int Number,
        string Label,
        DateOnly StartDate,
        DateOnly EndDate);

    public sealed record UpdatePayrollPeriodRequest(
        string PayPeriodTypeCode,
        int Year,
        int Number,
        string Label,
        DateOnly StartDate,
        DateOnly EndDate);
}

using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using CLARIHR.Api.Authorization;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Payroll;
using CLARIHR.Application.Features.Payroll.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}")]
[Tags("Payroll Definitions")]
[AuthorizationPolicySet(PayrollConfigurationPolicies.Read, PayrollConfigurationPolicies.Manage)]
[ResourceActions(PayrollConfigurationPermissionCodes.PayrollDefinitionsResourceKey)]
public sealed class PayrollDefinitionsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("companies/{companyId:guid}/payroll-definitions")]
    [ProducesResponseType<PagedResponse<PayrollDefinitionListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "List payroll definitions for a company",
        Description = """
            Returns a paginated list of the company's payroll definitions ("Nóminas", REQ-012 — the master
            the payroll engine runs), filterable by `isActive` and free-text `q` over the code and name.
            Each definition carries its payroll type (`payroll-types` catalog), pay frequency
            (`pay-periods`), `totalPeriods` per year (soft-validated: 12/24/52 are the canonical counts for
            MENSUAL/QUINCENAL/SEMANAL but deliberate deviations such as a 13th run are accepted), the
            minimum-income guarantee flag, currency and the overtime/attendance window rules (offset in days
            over the period end). The owning company is validated against the authenticated tenant. Set
            `includeAllowedActions=true` to receive per-item read/manage flags.
            """)]
    public async Task<ActionResult<PagedResponse<PayrollDefinitionListItemResponse>>> Search(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery, Range(1, PayrollConfigurationValidationRules.MaxPageSize)] int pageSize = PayrollConfigurationValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchPayrollDefinitionsQuery(companyId, isActive, search, page, pageSize, includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("payroll-definitions/{id:guid}")]
    [ProducesResponseType<PayrollDefinitionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a payroll definition by id",
        Description = """
            Returns a single payroll definition by its public id. The owning company is resolved from the
            authenticated tenant; a non-existent id yields `404`, while an id that belongs to another tenant
            yields `403 TENANT_MISMATCH`. The current `concurrencyToken` is emitted as the `ETag` header on
            mutations.
            """)]
    public async Task<ActionResult<PayrollDefinitionResponse>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPayrollDefinitionByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("companies/{companyId:guid}/payroll-definitions")]
    [ProducesResponseType<PayrollDefinitionResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Create a payroll definition",
        Description = """
            Creates a payroll definition ("Nómina") under the company and returns `201 Created` with the
            `Location` header pointing to the new resource and the `ETag` header carrying its initial
            `concurrencyToken`. The `payrollTypeCode`, `payPeriodCode` and `currencyCode` must be ACTIVE
            codes of their country catalogs or the request yields `422 PAYROLL_DEFINITION_CATALOG_INVALID`.
            Window offsets require their window flag enabled. A duplicate active code yields `409`.
            """)]
    public async Task<ActionResult<PayrollDefinitionResponse>> Create(
        Guid companyId,
        [FromBody] CreatePayrollDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreatePayrollDefinitionCommand(
                companyId,
                request.Code,
                request.Name,
                request.PayrollTypeCode,
                request.PayPeriodCode,
                request.TotalPeriods,
                request.GuaranteesMinimumIncome,
                request.CurrencyCode,
                request.OvertimeWindowEnabled,
                request.OvertimeWindowOffsetDays,
                request.AttendanceWindowEnabled,
                request.AttendanceWindowOffsetDays),
            cancellationToken);

        // The PublicContractRouteConvention rewrites the GetById route token `{id}` to `{publicId}`,
        // so the Location route value MUST be keyed `publicId` (not `id`).
        return this.ToCreatedAtActionResult(
            result,
            nameof(GetById),
            value => new { publicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPut("payroll-definitions/{id:guid}")]
    [ProducesResponseType<PayrollDefinitionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Update a payroll definition",
        Description = """
            Replaces the editable fields of a payroll definition (code, name, payroll type, pay frequency,
            total periods, minimum-income guarantee, currency and the window rules). The catalog codes must
            be ACTIVE (`422 PAYROLL_DEFINITION_CATALOG_INVALID` otherwise). Requires the current
            `concurrencyToken` in the `If-Match` header; a missing/malformed header yields `400` and a stale
            token yields `409 CONCURRENCY_CONFLICT`. A duplicate active code yields `409`.
            """)]
    public async Task<ActionResult<PayrollDefinitionResponse>> Update(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdatePayrollDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePayrollDefinitionCommand(
                id,
                request.Code,
                request.Name,
                request.PayrollTypeCode,
                request.PayPeriodCode,
                request.TotalPeriods,
                request.GuaranteesMinimumIncome,
                request.CurrencyCode,
                request.OvertimeWindowEnabled,
                request.OvertimeWindowOffsetDays,
                request.AttendanceWindowEnabled,
                request.AttendanceWindowOffsetDays,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("payroll-definitions/{id:guid}/activation")]
    [ProducesResponseType<PayrollDefinitionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Activate a payroll definition",
        Description = """
            Reactivates an inactive payroll definition. Requires the current `concurrencyToken` in the
            `If-Match` header (missing → `400`, stale → `409`). If another active definition already uses
            the same code, activation yields `409`.
            """)]
    public async Task<ActionResult<PayrollDefinitionResponse>> Activate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivatePayrollDefinitionCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("payroll-definitions/{id:guid}/inactivation")]
    [ProducesResponseType<PayrollDefinitionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Inactivate a payroll definition",
        Description = """
            Deactivates (soft-delete) a payroll definition. A definition referenced by an active period or a
            live run yields `422 PAYROLL_DEFINITION_IN_USE`. Requires the current `concurrencyToken` in the
            `If-Match` header (missing → `400`, stale → `409`).
            """)]
    public async Task<ActionResult<PayrollDefinitionResponse>> Inactivate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivatePayrollDefinitionCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    public sealed record CreatePayrollDefinitionRequest(
        string Code,
        string Name,
        string PayrollTypeCode,
        string PayPeriodCode,
        int TotalPeriods,
        string CurrencyCode,
        bool GuaranteesMinimumIncome = false,
        bool OvertimeWindowEnabled = false,
        int? OvertimeWindowOffsetDays = null,
        bool AttendanceWindowEnabled = false,
        int? AttendanceWindowOffsetDays = null);

    public sealed record UpdatePayrollDefinitionRequest(
        string Code,
        string Name,
        string PayrollTypeCode,
        string PayPeriodCode,
        int TotalPeriods,
        string CurrencyCode,
        bool GuaranteesMinimumIncome = false,
        bool OvertimeWindowEnabled = false,
        int? OvertimeWindowOffsetDays = null,
        bool AttendanceWindowEnabled = false,
        int? AttendanceWindowOffsetDays = null);
}

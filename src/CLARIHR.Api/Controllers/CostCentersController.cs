using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.CostCenters;
using CLARIHR.Application.Features.CostCenters.Common;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Domain.CostCenters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}")]
[Tags("Cost Centers")]
[AuthorizationPolicySet(CostCenterPolicies.Read, CostCenterPolicies.Manage)]
public sealed class CostCentersController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    ReportExportDeliveryService reportExportDeliveryService) : ControllerBase
{
    [HttpGet("companies/{companyId:guid}/cost-centers")]
    [ProducesResponseType<PagedResponse<CostCenterListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "List cost centers for a company",
        Description = """
            Returns a paginated list of cost centers for the company, filterable by `type`,
            `isActive` and free-text `q`. The owning company is validated against the
            authenticated tenant. Set `includeAllowedActions=true` to receive per-item
            read/manage flags.
            """)]
    public async Task<ActionResult<PagedResponse<CostCenterListItemResponse>>> Search(
        Guid companyId,
        [FromQuery] CostCenterType? type,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery, Range(1, CostCenterValidationRules.MaxPageSize)] int pageSize = CostCenterValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchCostCentersQuery(companyId, type, isActive, search, page, pageSize, includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("cost-centers/{id:guid}")]
    [ProducesResponseType<CostCenterResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a cost center by id",
        Description = """
            Returns a single cost center by its public id. The owning company is resolved
            from the authenticated tenant; a non-existent id yields `404`, while an id that
            belongs to another tenant yields `403 TENANT_MISMATCH`. The current
            `concurrencyToken` is emitted as the `ETag` header on mutations.
            """)]
    public async Task<ActionResult<CostCenterResponse>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetCostCenterByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("cost-centers/{id:guid}/usage")]
    [ProducesResponseType<CostCenterUsageResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a cost center's usage",
        Description = """
            Returns the active/inactive reference counts for the cost center across
            organization units and position slots, indicating whether it is safe to
            inactivate.
            """)]
    public async Task<ActionResult<CostCenterUsageResponse>> Usage(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetCostCenterUsageQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("companies/{companyId:guid}/cost-centers/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "Export cost centers as a report",
        Description = """
            Exports the filtered cost centers as a downloadable report in the requested
            `format` (e.g. `xlsx`; an unknown format yields `400`). The same filters as the
            list endpoint apply (`type`, `isActive`, free-text `q`). The export is bounded by
            the synchronous read limit and audited.
            """)]
    public async Task<IActionResult> Export(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] CostCenterType? type = null,
        [FromQuery] bool? isActive = null,
        [FromQuery(Name = "q")] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportCostCentersQuery(
                companyId,
                type,
                isActive,
                search,
                reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<CostCenterExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "cost-centers",
            "CostCenters",
            AuditEntityTypes.CostCenter,
            ReportExportResources.CostCenters,
            "Exported cost centers report.",
            new { type, isActive, q = search },
            CostCenterErrors.ExportFormatInvalid,
            cancellationToken);
    }

    [HttpPost("companies/{companyId:guid}/cost-centers")]
    [ProducesResponseType<CostCenterResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Create a cost center",
        Description = """
            Creates a cost center under the company and returns `201 Created` with the
            `Location` header pointing to the new resource and the `ETag` header carrying its
            initial `concurrencyToken`. A duplicate code yields `409`.
            """)]
    public async Task<ActionResult<CostCenterResponse>> Create(
        Guid companyId,
        [FromBody] CreateCostCenterRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateCostCenterCommand(
                companyId,
                request.Code,
                request.Name,
                request.Type,
                request.PayrollExpenseAccountCode,
                request.EmployerContributionAccountCode,
                request.ProvisionAccountCode,
                request.Description),
            cancellationToken);

        // The PublicContractRouteConvention rewrites the GetById route token `{id}` to
        // `{publicId}`, so the Location route value MUST be keyed `publicId` (not `id`) or
        // link generation fails. Mirrors JobProfilesController's POST.
        return this.ToCreatedAtActionResult(
            result,
            nameof(GetById),
            value => new { publicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPut("cost-centers/{id:guid}")]
    [ProducesResponseType<CostCenterResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Update a cost center",
        Description = """
            Replaces the editable fields of a cost center (code, name, type, account codes,
            description). Requires the current `concurrencyToken` in the `If-Match` header; a
            missing/malformed header yields `400` and a stale token yields
            `409 CONCURRENCY_CONFLICT`. A duplicate code yields `409`. The refreshed token is
            returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<CostCenterResponse>> Update(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateCostCenterRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateCostCenterCommand(
                id,
                request.Code,
                request.Name,
                request.Type,
                request.PayrollExpenseAccountCode,
                request.EmployerContributionAccountCode,
                request.ProvisionAccountCode,
                request.Description,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("cost-centers/{id:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<CostCenterResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a cost center",
        Description = """
            Applies a partial update using JSON Patch (RFC 6902), media type
            `application/json-patch+json`. Supported operations are `add`/`replace`/`remove`
            on root paths `/code`, `/name`, `/type`, `/payrollExpenseAccountCode`,
            `/employerContributionAccountCode`, `/provisionAccountCode`, `/description`
            (activation is handled by the dedicated `/activate` and `/inactivate` endpoints).
            Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`,
            stale → `409`). The refreshed token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<CostCenterResponse>> Patch(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchCostCenterRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchCostCenterCommand(
                id,
                concurrencyToken,
                JsonPatchOperationMapper.Map(
                    patchDoc,
                    static (op, path, from, value) => new CostCenterPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("cost-centers/{id:guid}/activate")]
    [ProducesResponseType<CostCenterResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Activate a cost center",
        Description = """
            Reactivates an inactive cost center. Requires the current `concurrencyToken` in
            the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is
            returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<CostCenterResponse>> Activate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateCostCenterCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("cost-centers/{id:guid}/inactivate")]
    [ProducesResponseType<CostCenterResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Inactivate a cost center",
        Description = """
            Deactivates (soft-delete) a cost center. Fails with `409` if it is still used by
            active organization units or position slots. Requires the current
            `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The
            refreshed token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<CostCenterResponse>> Inactivate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateCostCenterCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    public sealed record CreateCostCenterRequest(
        string Code,
        string Name,
        CostCenterType Type,
        string? PayrollExpenseAccountCode,
        string? EmployerContributionAccountCode,
        string? ProvisionAccountCode,
        string? Description);

    public sealed record UpdateCostCenterRequest(
        string Code,
        string Name,
        CostCenterType Type,
        string? PayrollExpenseAccountCode,
        string? EmployerContributionAccountCode,
        string? ProvisionAccountCode,
        string? Description);

    public sealed class PatchCostCenterRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public CostCenterType Type { get; set; }
        public string? PayrollExpenseAccountCode { get; set; }
        public string? EmployerContributionAccountCode { get; set; }
        public string? ProvisionAccountCode { get; set; }
        public string? Description { get; set; }
    }
}

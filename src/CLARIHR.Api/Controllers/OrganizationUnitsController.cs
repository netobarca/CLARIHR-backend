using System.ComponentModel.DataAnnotations;
using System.Text;
using Asp.Versioning;
using CLARIHR.Api.Authorization;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.OrgUnits;
using CLARIHR.Application.Features.OrgUnits.Common;
using CLARIHR.Application.Features.Reports.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}")]
[Tags("Organization Units")]
[AuthorizationPolicySet(OrgUnitPolicies.Read, OrgUnitPolicies.Manage)]
[ResourceActions(OrgUnitPermissionCodes.ResourceKey)]
public sealed class OrganizationUnitsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    ReportExportDeliveryService reportExportDeliveryService,
    OrgUnitDiagramWriter diagramWriter) : ControllerBase
{
    [HttpGet("companies/{companyId:guid}/organization-units")]
    [EnableRateLimiting(OrgUnitRateLimitPolicies.Search)]
    [ProducesResponseType<PagedResponse<OrgUnitListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "List org units for a company",
        Description = """
            Returns a paginated list of organization units for the company, filterable by
            `isActive`, `orgUnitTypeId`, `functionalAreaId`, `parentId` and free-text `q`. The
            owning company is validated against the authenticated tenant. List items omit
            `description` (detail-only); fetch the unit by id for the full payload. Set
            `includeAllowedActions=true` to receive per-item read/manage flags. For the hierarchy
            use the `/tree` endpoint.
            """)]
    public async Task<ActionResult<PagedResponse<OrgUnitListItemResponse>>> Search(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery] Guid? orgUnitTypeId,
        [FromQuery] Guid? functionalAreaId,
        [FromQuery] Guid? parentId,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery, Range(1, OrgUnitValidationRules.MaxPageSize)] int pageSize = OrgUnitValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchOrgUnitsQuery(companyId, isActive, q, orgUnitTypeId, functionalAreaId, parentId, page, pageSize, includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("organization-units/{id:guid}")]
    [ProducesResponseType<OrgUnitResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get an org unit by id",
        Description = """
            Returns a single organization unit by its public id. The owning company is resolved
            from the authenticated tenant; a unit belonging to another tenant yields `404`. The
            `concurrencyToken` is emitted as the `ETag` header on mutations.
            """)]
    public async Task<ActionResult<OrgUnitResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetOrgUnitByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("companies/{companyId:guid}/organization-units/tree")]
    [EnableRateLimiting(OrgUnitRateLimitPolicies.Tree)]
    [ProducesResponseType<IReadOnlyCollection<OrgUnitTreeNodeResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query | StandardErrorSet.NotFound)]
    [SwaggerOperation(
        Summary = "Get the org units tree",
        Description = """
            Returns the organization unit hierarchy for the company as a nested tree, optionally
            scoped to a `rootId` and a `depth`. The owning company is validated against the
            authenticated tenant; an unknown `rootId` yields `404`. This is the hierarchical
            projection; the flat paginated list is served by the sibling list endpoint.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<OrgUnitTreeNodeResponse>>> Tree(
        Guid companyId,
        [FromQuery] Guid? rootId,
        [FromQuery] int? depth,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetOrgUnitTreeQuery(companyId, rootId, depth),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("companies/{companyId:guid}/organization-units/graph")]
    [EnableRateLimiting(OrgUnitRateLimitPolicies.Tree)]
    [ProducesResponseType<OrgUnitGraphResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query | StandardErrorSet.NotFound)]
    [SwaggerOperation(
        Summary = "Get the org units dependency graph (JSON)",
        Description = """
            Returns the nodes and edges of the organization unit graph for the company as JSON,
            optionally scoped to a `rootId` and a `depth`. The owning company is validated against
            the authenticated tenant.
            """)]
    public async Task<ActionResult<OrgUnitGraphResponse>> Graph(
        Guid companyId,
        [FromQuery] Guid? rootId,
        [FromQuery] int? depth,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetOrgUnitGraphQuery(companyId, rootId, depth),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("companies/{companyId:guid}/organization-units/export")]
    [EnableRateLimiting(OrgUnitRateLimitPolicies.Export)]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "Export org units as a report",
        Description = """
            Exports the filtered organization units as a downloadable report in the requested
            `format` (e.g. `xlsx`; an unknown format yields `400`). The same filters as the list
            endpoint apply. The export is bounded by the synchronous read limit and audited.
            """)]
    public async Task<IActionResult> Export(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] bool? isActive = null,
        [FromQuery] Guid? orgUnitTypeId = null,
        [FromQuery] Guid? functionalAreaId = null,
        [FromQuery] Guid? parentId = null,
        [FromQuery(Name = "q")] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetOrgUnitExportRowsQuery(
                companyId,
                isActive,
                search,
                orgUnitTypeId,
                functionalAreaId,
                parentId,
                reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<OrgUnitExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "org-units",
            "OrgUnits",
            AuditEntityTypes.OrgUnit,
            ReportExportResources.OrgUnits,
            "Exported org units report.",
            new { isActive, orgUnitTypeId, functionalAreaId, parentId, q = search },
            ReportPolicyErrors.FormatNotSupported,
            cancellationToken);
    }

    [HttpGet("companies/{companyId:guid}/organization-units/diagram-export")]
    [EnableRateLimiting(OrgUnitRateLimitPolicies.Export)]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesStandardErrors(StandardErrorSet.Query | StandardErrorSet.NotFound)]
    [SwaggerOperation(
        Summary = "Export the org units diagram",
        Description = """
            Exports the organization unit graph as a downloadable file in the requested `format`
            (`graphml`, `json` or `dot`; an unknown format yields `400`). Scope is controllable via
            `rootId` and `depth`. The result is capped at the configured maximum node count (`413`
            if exceeded) and the export is audited.
            """)]
    public async Task<IActionResult> DiagramExport(
        Guid companyId,
        [FromQuery] string format = "graphml",
        [FromQuery] Guid? rootId = null,
        [FromQuery] int? depth = null,
        CancellationToken cancellationToken = default)
    {
        var graphResult = await queryDispatcher.SendAsync(
            new GetOrgUnitGraphQuery(companyId, rootId, depth),
            cancellationToken);

        if (graphResult.IsFailure)
        {
            return this.ToActionResult(Result<OrgUnitGraphResponse>.Failure(graphResult.Error)).Result!;
        }

        if (graphResult.Value.Nodes.Count > reportExportDeliveryService.MaxDiagramNodes)
        {
            return CLARIHR.Api.Common.ProblemDetailsFactory.Create(HttpContext, ReportPolicyErrors.ExportLimitExceeded);
        }

        if (string.Equals(format, "graphml", StringComparison.OrdinalIgnoreCase))
        {
            await reportExportDeliveryService.LogExportAsync(
                AuditEntityTypes.OrgUnit,
                ReportExportResources.OrgUnits,
                "Exported org units diagram.",
                "graphml",
                new { rootId, depth },
                graphResult.Value.Nodes.Count,
                cancellationToken);

            var graphml = diagramWriter.WriteGraphMl(graphResult.Value);
            return File(Encoding.UTF8.GetBytes(graphml), "application/graphml+xml", "org-units-diagram.graphml");
        }

        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            await reportExportDeliveryService.LogExportAsync(
                AuditEntityTypes.OrgUnit,
                ReportExportResources.OrgUnits,
                "Exported org units diagram.",
                "json",
                new { rootId, depth },
                graphResult.Value.Nodes.Count,
                cancellationToken);

            var json = diagramWriter.WriteJson(graphResult.Value);
            return File(Encoding.UTF8.GetBytes(json), "application/json", "org-units-diagram.json");
        }

        if (string.Equals(format, "dot", StringComparison.OrdinalIgnoreCase))
        {
            await reportExportDeliveryService.LogExportAsync(
                AuditEntityTypes.OrgUnit,
                ReportExportResources.OrgUnits,
                "Exported org units diagram.",
                "dot",
                new { rootId, depth },
                graphResult.Value.Nodes.Count,
                cancellationToken);

            var dot = diagramWriter.WriteDot(graphResult.Value);
            return File(Encoding.UTF8.GetBytes(dot), "text/vnd.graphviz", "org-units-diagram.dot");
        }

        return this.ToActionResult(Result<OrgUnitGraphResponse>.Failure(ReportPolicyErrors.FormatNotSupported)).Result!;
    }

    [HttpPost("companies/{companyId:guid}/organization-units")]
    [ProducesResponseType<OrgUnitResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Create an org unit",
        Description = """
            Creates an organization unit under the company and returns `201 Created` with the
            `Location` header pointing to the new resource and the `ETag` header carrying its
            initial `concurrencyToken`. The type, optional functional area, optional parent and
            optional manager are referenced by public id; the cost center by code. A duplicate code
            yields `409`; an invalid cost center yields `422`; a depth/cycle violation yields `409`.
            """)]
    public async Task<ActionResult<OrgUnitResponse>> Create(
        Guid companyId,
        [FromBody] CreateOrgUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateOrgUnitCommand(
                companyId,
                request.Code,
                request.Name,
                request.OrgUnitTypePublicId,
                request.FunctionalAreaPublicId,
                request.ParentPublicId,
                request.SortOrder,
                request.Description,
                request.CostCenterCode,
                request.ManagerEmployeePublicId),
            cancellationToken);

        // The PublicContractRouteConvention rewrites the GetById route token `{id}` to
        // `{publicId}`, so the Location route value MUST be keyed `publicId` (not `id`).
        return this.ToCreatedAtActionResult(
            result,
            nameof(GetById),
            value => new { publicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPut("organization-units/{id:guid}")]
    [ProducesResponseType<OrgUnitResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Update an org unit",
        Description = """
            Replaces the editable fields of an organization unit (code, name, type, functional
            area, sort order, description, cost center, manager). The parent is changed via
            `/move`. Requires the current `concurrencyToken` in the `If-Match` header (missing →
            `400`, stale → `409`). A duplicate code yields `409`; an invalid cost center yields
            `422`. The refreshed token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<OrgUnitResponse>> Update(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateOrgUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateOrgUnitCommand(
                id,
                request.Code,
                request.Name,
                request.OrgUnitTypePublicId,
                request.FunctionalAreaPublicId,
                request.SortOrder,
                request.Description,
                request.CostCenterCode,
                request.ManagerEmployeePublicId,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("organization-units/{id:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<OrgUnitResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch an org unit",
        Description = """
            Applies a partial update using JSON Patch (RFC 6902), media type
            `application/json-patch+json`. Patchable descriptive paths: `/name`, `/sortOrder`,
            `/description`. The code (uniqueness-checked), the type/functional-area/manager
            references and the cost center (resolved/validated) are changed via PUT; the parent via
            `/move`; activation via `/activate` and `/inactivate`. Requires the current
            `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The
            refreshed token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<OrgUnitResponse>> Patch(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchOrgUnitRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchOrgUnitCommand(
                id,
                concurrencyToken,
                JsonPatchOperationMapper.Map(
                    patchDoc,
                    static (op, path, from, value) => new OrgUnitPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("organization-units/{id:guid}/move")]
    [ProducesResponseType<OrgUnitResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Move an org unit to another parent",
        Description = """
            Reparents the organization unit (by new parent public id) and optionally sets its sort
            order. A move that would create a cycle or exceed the depth limit yields `409`. Requires
            the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale →
            `409`). The refreshed token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<OrgUnitResponse>> Move(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] MoveOrgUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new MoveOrgUnitCommand(id, request.NewParentPublicId, request.SortOrder, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("organization-units/{id:guid}/activate")]
    [ProducesResponseType<OrgUnitResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Activate an org unit",
        Description = """
            Reactivates an inactive organization unit. Requires the current `concurrencyToken` in
            the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned
            in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<OrgUnitResponse>> Activate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateOrgUnitCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("organization-units/{id:guid}/inactivate")]
    [ProducesResponseType<OrgUnitResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Inactivate an org unit",
        Description = """
            Deactivates (soft-delete) an organization unit. Fails with `409` if it still has active
            child units. Requires the current `concurrencyToken` in the `If-Match` header (missing →
            `400`, stale → `409`). The refreshed token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<OrgUnitResponse>> Inactivate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateOrgUnitCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    public sealed record CreateOrgUnitRequest(
        string Code,
        string Name,
        Guid OrgUnitTypePublicId,
        Guid? FunctionalAreaPublicId,
        Guid? ParentPublicId,
        int? SortOrder,
        string? Description,
        string? CostCenterCode,
        Guid? ManagerEmployeePublicId);

    public sealed record UpdateOrgUnitRequest(
        string Code,
        string Name,
        Guid OrgUnitTypePublicId,
        Guid? FunctionalAreaPublicId,
        int? SortOrder,
        string? Description,
        string? CostCenterCode,
        Guid? ManagerEmployeePublicId);

    public sealed record MoveOrgUnitRequest(Guid? NewParentPublicId, int? SortOrder);

    public sealed class PatchOrgUnitRequest
    {
        public string Name { get; set; } = string.Empty;
        public int? SortOrder { get; set; }
        public string? Description { get; set; }
    }
}

using Asp.Versioning;
using System.ComponentModel.DataAnnotations;
using System.Text;
using CLARIHR.Api.Authorization;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.PositionSlots;
using CLARIHR.Application.Features.PositionSlots.Common;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Domain.PositionSlots;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}")]
[Tags("Position Slots")]
[AuthorizationPolicySet(PositionSlotPolicies.Read, PositionSlotPolicies.Manage)]
[ResourceActions(PositionSlotPermissionCodes.ResourceKey)]
public sealed class PositionSlotsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    ReportExportDeliveryService reportExportDeliveryService,
    PositionSlotDiagramWriter diagramWriter) : ControllerBase
{
    [EnableRateLimiting(PositionSlotRateLimitPolicies.Search)]
    [HttpGet("companies/{companyId:guid}/position-slots")]
    [ProducesResponseType<PagedResponse<PositionSlotListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "List position slots for a company",
        Description = """
            Returns a paginated list of position slots for the company, filterable by
            `status`, `jobProfileId`, `orgUnitId`, `workCenterId`, `contractTypeId` and
            free-text `q` (minimum 2 characters). The owning company is validated against
            the authenticated tenant. Set `includeAllowedActions=true` to receive per-item
            read/manage flags. Rate-limited per user+tenant.
            """)]
    public async Task<ActionResult<PagedResponse<PositionSlotListItemResponse>>> Search(
        Guid companyId,
        [FromQuery] PositionSlotStatus? status,
        [FromQuery] Guid? jobProfileId,
        [FromQuery] Guid? orgUnitId,
        [FromQuery] Guid? workCenterId,
        [FromQuery] Guid? contractTypeId,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery, Range(1, PositionSlotValidationRules.MaxPageSize)] int pageSize = PositionSlotValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchPositionSlotsQuery(
                companyId,
                status,
                jobProfileId,
                orgUnitId,
                workCenterId,
                contractTypeId,
                search,
                page,
                pageSize,
                includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("position-slots/{id:guid}")]
    [ProducesResponseType<PositionSlotResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a position slot by id",
        Description = """
            Returns a single position slot by its public id. The owning company is
            resolved from the authenticated tenant; a slot belonging to another tenant
            yields `404`. The current `concurrencyToken` is emitted as the `ETag` header
            for use in the `If-Match` header of subsequent updates.
            """)]
    public async Task<ActionResult<PositionSlotResponse>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPositionSlotByIdQuery(id), cancellationToken);
        return this.ToActionResultWithETag(result, static value => value.ConcurrencyToken);
    }

    [EnableRateLimiting(PositionSlotRateLimitPolicies.Export)]
    [HttpGet("companies/{companyId:guid}/position-slots/graph")]
    [ProducesResponseType<PositionSlotGraphResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesStandardErrors(StandardErrorSet.Query | StandardErrorSet.NotFound)]
    [SwaggerOperation(
        Summary = "Get the position slots dependency graph (JSON)",
        Description = """
            Returns the nodes and edges of the position slots dependency graph for the
            company as JSON, optionally scoped to a `rootId`, a `depth` and whether to
            include functional dependencies. The result is capped at the configured
            maximum node count — exceeding it yields `413 Payload Too Large`. Rate-limited
            per user+tenant under the export policy.
            """)]
    public async Task<ActionResult<PositionSlotGraphResponse>> Graph(
        Guid companyId,
        [FromQuery] Guid? rootId,
        [FromQuery] int? depth,
        [FromQuery] bool includeFunctional = true,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPositionSlotGraphQuery(
                companyId,
                rootId,
                depth,
                includeFunctional,
                reportExportDeliveryService.MaxDiagramNodes),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [EnableRateLimiting(PositionSlotRateLimitPolicies.Export)]
    [HttpGet("companies/{companyId:guid}/position-slots/diagram-export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesStandardErrors(StandardErrorSet.Query | StandardErrorSet.NotFound)]
    [SwaggerOperation(
        Summary = "Export the position slots diagram",
        Description = """
            Exports the position slots dependency graph as a downloadable file in the
            requested `format` (`graphml`, `json` or `dot`; an unknown format yields
            `400`). Scope is controllable via `rootId`, `depth` and `includeFunctional`.
            The result is capped at the configured maximum node count (`413` if exceeded)
            and the export is audited. Rate-limited per user+tenant under the export policy.
            """)]
    public async Task<IActionResult> DiagramExport(
        Guid companyId,
        [FromQuery] string format = "graphml",
        [FromQuery] Guid? rootId = null,
        [FromQuery] int? depth = null,
        [FromQuery] bool includeFunctional = true,
        CancellationToken cancellationToken = default)
    {
        // RA-2: reject an unknown format BEFORE paying for the graph computation (CountSlotsAsync +
        // §PS4 cap + the wide 8-table join). `descriptor` is the single source for the supported-format
        // set, content type, file name and writer method — replacing the previously triplicated branches.
        var descriptor = ResolveDiagramFormat(format);
        if (descriptor is null)
        {
            return this.ToActionResult(Result<PositionSlotGraphResponse>.Failure(PositionSlotErrors.DiagramFormatInvalid)).Result!;
        }

        var graphResult = await queryDispatcher.SendAsync(
            new GetPositionSlotGraphQuery(
                companyId,
                rootId,
                depth,
                includeFunctional,
                reportExportDeliveryService.MaxDiagramNodes),
            cancellationToken);

        if (graphResult.IsFailure)
        {
            return this.ToActionResult(Result<PositionSlotGraphResponse>.Failure(graphResult.Error)).Result!;
        }

        await reportExportDeliveryService.LogExportAsync(
            AuditEntityTypes.PositionSlot,
            ReportExportResources.PositionSlots,
            "Exported position slots diagram.",
            descriptor.Value.AuditFormat,
            new { rootId, depth, includeFunctional },
            graphResult.Value.Nodes.Count,
            cancellationToken);

        var content = descriptor.Value.Write(diagramWriter, graphResult.Value);
        return File(Encoding.UTF8.GetBytes(content), descriptor.Value.ContentType, descriptor.Value.FileName);
    }

    // RA-2: single source of truth for the supported diagram formats — validated up-front so an unknown
    // format short-circuits to 400 before the graph is computed — and their content-type / file-name /
    // writer mapping. Returns null for an unknown format.
    private static (string AuditFormat, string ContentType, string FileName, Func<PositionSlotDiagramWriter, PositionSlotGraphResponse, string> Write)? ResolveDiagramFormat(string format) =>
        format.ToLowerInvariant() switch
        {
            "graphml" => ("graphml", "application/graphml+xml", "position-slots-diagram.graphml", static (writer, graph) => writer.WriteGraphMl(graph)),
            "json" => ("json", "application/json", "position-slots-diagram.json", static (writer, graph) => writer.WriteJson(graph)),
            "dot" => ("dot", "text/vnd.graphviz", "position-slots-diagram.dot", static (writer, graph) => writer.WriteDot(graph)),
            _ => null
        };

    [EnableRateLimiting(PositionSlotRateLimitPolicies.Export)]
    [HttpGet("companies/{companyId:guid}/position-slots/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "Export position slots as a report",
        Description = """
            Exports the filtered position slots as a downloadable report in the requested
            `format` (e.g. `xlsx`, `csv`; an unknown format yields `400`). The same filters
            as the list endpoint apply (`status`, `jobProfileId`, `orgUnitId`,
            `workCenterId`, `contractTypeId`, free-text `q`). The export is bounded by the
            synchronous read limit, audited, and rate-limited per user+tenant.
            """)]
    public async Task<IActionResult> Export(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] PositionSlotStatus? status = null,
        [FromQuery] Guid? jobProfileId = null,
        [FromQuery] Guid? orgUnitId = null,
        [FromQuery] Guid? workCenterId = null,
        [FromQuery] Guid? contractTypeId = null,
        [FromQuery(Name = "q")] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPositionSlotExportRowsQuery(
                companyId,
                status,
                jobProfileId,
                orgUnitId,
                workCenterId,
                contractTypeId,
                search,
                reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<PositionSlotExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "position-slots",
            "PositionSlots",
            AuditEntityTypes.PositionSlot,
            ReportExportResources.PositionSlots,
            "Exported position slots report.",
            // §PS7: record the audit filters under the SAME public-contract keys the async
            // export handler reads (jobProfilePublicId/…/search) and the wire query string
            // uses, so the metadata is consistent with the job parameter contract
            // ([[publicid-auto-transform-mechanism]]) and would replay correctly if ever
            // fed to a ReportExportJob.
            new
            {
                status,
                jobProfilePublicId = jobProfileId,
                orgUnitPublicId = orgUnitId,
                workCenterPublicId = workCenterId,
                contractTypePublicId = contractTypeId,
                search,
            },
            PositionSlotErrors.ExportFormatInvalid,
            cancellationToken);
    }

    [HttpPost("companies/{companyId:guid}/position-slots")]
    [ProducesResponseType<PositionSlotResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Create a position slot",
        Description = """
            Creates a position slot under the company and returns `201 Created` with the
            `Location` header pointing to the new resource and the `ETag` header carrying its
            initial `concurrencyToken`. The slot references a job profile and optionally a role,
            work center and direct/functional dependency slots (by public id). Business-rule
            violations (e.g. duplicate code, dependency cycle) yield `409`/`422`. Domain
            capacity/date rules are validated.
            """)]
    public async Task<ActionResult<PositionSlotResponse>> Create(
        Guid companyId,
        [FromBody] CreatePositionSlotRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreatePositionSlotCommand(
                companyId,
                request.Code,
                request.Title,
                request.JobProfilePublicId,
                request.RolePublicId,
                request.WorkCenterPublicId,
                request.DirectDependencyPositionSlotPublicId,
                request.FunctionalDependencyPositionSlotPublicId,
                request.Status,
                request.MaxEmployees,
                request.OccupiedEmployees,
                request.EffectiveFromUtc,
                request.EffectiveToUtc,
                request.Notes,
                request.ConfiguredBaseSalary,
                request.ConfiguredBaseSalaryCurrencyCode),
            cancellationToken);

        // The PublicContractRouteConvention rewrites the GetById route token `{id}` to
        // `{publicId}`, so the Location route value MUST be keyed `publicId` (not `id`).
        return this.ToCreatedAtActionResult(
            result,
            nameof(GetById),
            value => new { publicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPut("position-slots/{id:guid}")]
    [ProducesResponseType<PositionSlotResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Update a position slot",
        Description = """
            Replaces the editable fields of a position slot (code, title, job profile,
            role, work center, capacity, effective dates, notes). Requires the current
            `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409
            CONCURRENCY_CONFLICT`). Domain capacity/date violations yield `422`. The refreshed
            token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<PositionSlotResponse>> Update(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdatePositionSlotRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePositionSlotCommand(
                id,
                request.Code,
                request.Title,
                request.JobProfilePublicId,
                request.RolePublicId,
                request.WorkCenterPublicId,
                request.MaxEmployees,
                request.EffectiveFromUtc,
                request.EffectiveToUtc,
                request.Notes,
                concurrencyToken,
                request.ConfiguredBaseSalary,
                request.ConfiguredBaseSalaryCurrencyCode),
            cancellationToken);

        return this.ToActionResultWithETag(result, static value => value.ConcurrencyToken);
    }

    [HttpPatch("position-slots/{id:guid}/status")]
    [ProducesResponseType<PositionSlotResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Change a position slot's status",
        Description = """
            Transitions the slot status (e.g. Vacant/Occupied/Suspended). Requires the
            current `concurrencyToken` in the `If-Match` header (missing → `400`, stale →
            `409 CONCURRENCY_CONFLICT`). Occupancy is reconciled with the target status by the
            domain. The refreshed token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<PositionSlotResponse>> UpdateStatus(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdatePositionSlotStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePositionSlotStatusCommand(id, request.Status, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, static value => value.ConcurrencyToken);
    }

    [HttpPatch("position-slots/{id:guid}/dependencies")]
    [ProducesResponseType<PositionSlotResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Update a position slot's dependencies",
        Description = """
            Sets the direct and/or functional dependency slots (by public id). A change
            that would introduce a dependency cycle (direct or functional) yields `409`.
            Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`,
            stale → `409 CONCURRENCY_CONFLICT`). The refreshed token is returned in the body
            and the `ETag` header.
            """)]
    public async Task<ActionResult<PositionSlotResponse>> UpdateDependencies(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdatePositionSlotDependenciesRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePositionSlotDependenciesCommand(
                id,
                request.DirectDependencyPositionSlotPublicId,
                request.FunctionalDependencyPositionSlotPublicId,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, static value => value.ConcurrencyToken);
    }

    [HttpPatch("position-slots/{id:guid}/occupancy")]
    [ProducesResponseType<PositionSlotResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Update a position slot's occupancy",
        Description = """
            Sets the number of occupied employees for the slot. A value exceeding the
            slot's capacity yields `422`. Requires the current `concurrencyToken` in the
            `If-Match` header (missing → `400`, stale → `409 CONCURRENCY_CONFLICT`). The
            refreshed token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<PositionSlotResponse>> UpdateOccupancy(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdatePositionSlotOccupancyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePositionSlotOccupancyCommand(id, request.OccupiedEmployees, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, static value => value.ConcurrencyToken);
    }

    public sealed record CreatePositionSlotRequest(
        string Code,
        string? Title,
        Guid JobProfilePublicId,
        Guid? RolePublicId,
        Guid? WorkCenterPublicId,
        Guid? DirectDependencyPositionSlotPublicId,
        Guid? FunctionalDependencyPositionSlotPublicId,
        PositionSlotStatus Status,
        int MaxEmployees,
        int OccupiedEmployees,
        DateTime EffectiveFromUtc,
        DateTime? EffectiveToUtc,
        string? Notes,
        decimal? ConfiguredBaseSalary,
        string? ConfiguredBaseSalaryCurrencyCode);

    // PS-A: the concurrency token now travels in the `If-Match` header (not the body), so these
    // request DTOs no longer carry it.
    public sealed record UpdatePositionSlotRequest(
        string Code,
        string? Title,
        Guid JobProfilePublicId,
        Guid? RolePublicId,
        Guid? WorkCenterPublicId,
        int MaxEmployees,
        DateTime EffectiveFromUtc,
        DateTime? EffectiveToUtc,
        string? Notes,
        decimal? ConfiguredBaseSalary,
        string? ConfiguredBaseSalaryCurrencyCode);

    public sealed record UpdatePositionSlotStatusRequest(PositionSlotStatus Status);

    public sealed record UpdatePositionSlotDependenciesRequest(
        Guid? DirectDependencyPositionSlotPublicId,
        Guid? FunctionalDependencyPositionSlotPublicId);

    public sealed record UpdatePositionSlotOccupancyRequest(int OccupiedEmployees);
}

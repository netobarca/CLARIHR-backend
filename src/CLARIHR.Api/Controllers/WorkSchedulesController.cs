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
[Tags("Work Schedules")]
[AuthorizationPolicySet(PayrollConfigurationPolicies.Read, PayrollConfigurationPolicies.Manage)]
[ResourceActions(PayrollConfigurationPermissionCodes.WorkSchedulesResourceKey)]
public sealed class WorkSchedulesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("companies/{companyId:guid}/work-schedules")]
    [ProducesResponseType<PagedResponse<WorkScheduleListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "List work schedules for a company",
        Description = """
            Returns a paginated list of the company's work schedules ("jornadas laborales", REQ-012 — the
            master the plaza's `workdayCode` references by code), filterable by `isActive` and free-text `q`
            over the code and name. Each row carries the attendance date anchor (`ENTRADA`/`SALIDA` — which
            side of a midnight-crossing shift owns the calendar date), the classification
            (`ORDINARIA`/`EXTRAORDINARIA`), the weekly hours and the day count; the full day set travels on
            the detail endpoint. Set `includeAllowedActions=true` to receive per-item read/manage flags.
            """)]
    public async Task<ActionResult<PagedResponse<WorkScheduleListItemResponse>>> Search(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery, Range(1, PayrollConfigurationValidationRules.MaxPageSize)] int pageSize = PayrollConfigurationValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchWorkSchedulesQuery(companyId, isActive, search, page, pageSize, includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("work-schedules/{id:guid}")]
    [ProducesResponseType<WorkScheduleResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a work schedule by id",
        Description = """
            Returns a single work schedule with its full weekday set (shift times, optional meal break and
            derived net hours per day). A non-existent id yields `404`; an id of another tenant yields
            `403 TENANT_MISMATCH`. The current `concurrencyToken` is emitted as the `ETag` header on
            mutations.
            """)]
    public async Task<ActionResult<WorkScheduleResponse>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetWorkScheduleByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("companies/{companyId:guid}/work-schedules")]
    [ProducesResponseType<WorkScheduleResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Create a work schedule",
        Description = """
            Creates a work schedule under the company and returns `201 Created` with the `Location` header
            pointing to the new resource and the `ETag` header carrying its initial `concurrencyToken`. The
            full weekday set travels in `days` (at least one; each weekday 0=Sunday…6=Saturday at most once;
            `endTime` earlier than `startTime` means a midnight-crossing night shift — allowed, anchored by
            `attendanceDateAnchor`; the optional meal break requires a day shift and must be contained in
            it) — violations yield `422 WORK_SCHEDULE_DAY_INVALID`. `totalWeeklyHours` is derived from the
            days when omitted and may be overridden. A duplicate active code yields `409`.
            """)]
    public async Task<ActionResult<WorkScheduleResponse>> Create(
        Guid companyId,
        [FromBody] CreateWorkScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateWorkScheduleCommand(
                companyId,
                request.Code,
                request.Name,
                request.ScheduleLabel,
                request.AttendanceDateAnchor,
                request.ScheduleClass,
                request.TotalWeeklyHours,
                request.Days),
            cancellationToken);

        // The PublicContractRouteConvention rewrites the GetById route token `{id}` to `{publicId}`,
        // so the Location route value MUST be keyed `publicId` (not `id`).
        return this.ToCreatedAtActionResult(
            result,
            nameof(GetById),
            value => new { publicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPut("work-schedules/{id:guid}")]
    [ProducesResponseType<WorkScheduleResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Update a work schedule",
        Description = """
            Replaces the editable fields of a work schedule INCLUDING its full weekday set (triple-replace:
            the whole week travels on every edit). Requires the current `concurrencyToken` in the `If-Match`
            header; a missing/malformed header yields `400` and a stale token yields
            `409 CONCURRENCY_CONFLICT`. Day-set violations yield `422 WORK_SCHEDULE_DAY_INVALID`; a
            duplicate active code yields `409`.
            """)]
    public async Task<ActionResult<WorkScheduleResponse>> Update(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateWorkScheduleRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateWorkScheduleCommand(
                id,
                request.Code,
                request.Name,
                request.ScheduleLabel,
                request.AttendanceDateAnchor,
                request.ScheduleClass,
                request.TotalWeeklyHours,
                request.Days,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("work-schedules/{id:guid}/activation")]
    [ProducesResponseType<WorkScheduleResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Activate a work schedule",
        Description = """
            Reactivates an inactive work schedule. Requires the current `concurrencyToken` in the `If-Match`
            header (missing → `400`, stale → `409`). If another active schedule already uses the same code,
            activation yields `409`.
            """)]
    public async Task<ActionResult<WorkScheduleResponse>> Activate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateWorkScheduleCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("work-schedules/{id:guid}/inactivation")]
    [ProducesResponseType<WorkScheduleResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Inactivate a work schedule",
        Description = """
            Deactivates (soft-delete) a work schedule. A schedule referenced by an ACTIVE employment
            assignment (the plaza's `workdayCode` carries the code) yields `422 WORK_SCHEDULE_IN_USE`.
            Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale →
            `409`).
            """)]
    public async Task<ActionResult<WorkScheduleResponse>> Inactivate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateWorkScheduleCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    public sealed record CreateWorkScheduleRequest(
        string Code,
        string Name,
        string AttendanceDateAnchor,
        string ScheduleClass,
        IReadOnlyCollection<WorkScheduleDayInputModel> Days,
        string? ScheduleLabel = null,
        decimal? TotalWeeklyHours = null);

    public sealed record UpdateWorkScheduleRequest(
        string Code,
        string Name,
        string AttendanceDateAnchor,
        string ScheduleClass,
        IReadOnlyCollection<WorkScheduleDayInputModel> Days,
        string? ScheduleLabel = null,
        decimal? TotalWeeklyHours = null);
}

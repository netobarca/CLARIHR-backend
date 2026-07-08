using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

/// <summary>
/// Company annual vacation plans (leave module §3.7, D-24): an indicative per-employee schedule of the intended
/// vacation windows for one year. Company-scoped and HR-managed (no self-service), so the declarative policy set
/// (ViewVacations read / ManageVacations write) matches every action; the precise RBAC is re-checked by the
/// handler gates. The plan is non-binding — POST/PUT return a per-line <c>warnings[]</c> (fund availability,
/// holiday / rest-day) instead of blocking; only same-employee line overlaps are rejected (422). The plan does
/// not journal a personnel action (it is indicative).
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewVacations, PersonnelFilePolicies.ManageVacations)]
public sealed class VacationPlansController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/companies/{companyId:guid}/vacation-plans")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<VacationPlanResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List the company's annual vacation plans",
        Description = """
            Returns the company's annual vacation plans, newest first, optionally filtered by `year`. The line
            warnings are populated only on the POST/PUT responses; plain reads carry empty warning lists. Requires
            the `ViewVacations` permission.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<VacationPlanResponse>>> GetVacationPlans(
        Guid companyId,
        [FromQuery] int? year = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetVacationPlansQuery(companyId, year), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/companies/{companyId:guid}/vacation-plans/{vacationPlanPublicId:guid}", Name = "GetVacationPlanById")]
    [Produces("application/json")]
    [ProducesResponseType<VacationPlanResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(Summary = "Get an annual vacation plan by id")]
    public async Task<ActionResult<VacationPlanResponse>> GetVacationPlanById(
        Guid companyId,
        Guid vacationPlanPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetVacationPlanByIdQuery(companyId, vacationPlanPublicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/companies/{companyId:guid}/vacation-plans")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<VacationPlanResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Create an annual vacation plan",
        Description = """
            Creates a VIGENTE annual vacation plan with its per-employee planned windows. Indicative scheduling
            (D-24): each line carries a non-blocking `warnings[]` (fund availability
            `VACATION_PLAN_WARNING_INSUFFICIENT_FUND`, holiday / rest-day `VACATION_PLAN_WARNING_DATE_RULE`).
            Same-employee overlapping windows are rejected (`VACATION_PLAN_LINE_OVERLAP`); a line referencing an
            employee outside the company is rejected (`VACATION_PLAN_EMPLOYEE_INVALID`). Manager-only
            (`ManageVacations`).
            """)]
    public async Task<ActionResult<VacationPlanResponse>> AddVacationPlan(
        Guid companyId,
        [FromBody] AddVacationPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddVacationPlanCommand(
                companyId,
                request.PlanYear,
                (request.Lines ?? []).Select(ToLineItem).ToArray()),
            cancellationToken);

        // The PublicContractRouteConvention rewrites the company-context route token `{companyId}` to
        // `{companyPublicId}`, so the Location route values MUST be keyed `companyPublicId` or link generation
        // fails ("No route matches the supplied values").
        return this.ToCreatedAtActionResult(
            result,
            nameof(GetVacationPlanById),
            value => new { companyPublicId = companyId, vacationPlanPublicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPut("api/v1/companies/{companyId:guid}/vacation-plans/{vacationPlanPublicId:guid}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<VacationPlanResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace the lines of an annual vacation plan",
        Description = """
            Replaces the full set of planned windows of a VIGENTE plan and returns the recomputed per-line
            warnings. Same-employee overlaps are rejected (`VACATION_PLAN_LINE_OVERLAP`); an annulled plan is
            rejected (`VACATION_PLAN_STATE_RULE_VIOLATION`). Requires the `If-Match` header. Manager-only.
            """)]
    public async Task<ActionResult<VacationPlanResponse>> UpdateVacationPlan(
        Guid companyId,
        Guid vacationPlanPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateVacationPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateVacationPlanCommand(
                companyId,
                vacationPlanPublicId,
                (request.Lines ?? []).Select(ToLineItem).ToArray(),
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/companies/{companyId:guid}/vacation-plans/{vacationPlanPublicId:guid}/annulment")]
    [Produces("application/json")]
    [ProducesResponseType<VacationPlanResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Annul an annual vacation plan",
        Description = """
            Annuls a VIGENTE plan (terminal). Rejected when the plan is already annulled
            (`VACATION_PLAN_STATE_RULE_VIOLATION`). Requires the `If-Match` header. Manager-only.
            """)]
    public async Task<ActionResult<VacationPlanResponse>> AnnulVacationPlan(
        Guid companyId,
        Guid vacationPlanPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AnnulVacationPlanCommand(companyId, vacationPlanPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    private static VacationPlanLineItem ToLineItem(VacationPlanLineRequest line) =>
        new(line.PersonnelFilePublicId, line.StartDate, line.EndDate, line.Days);
}

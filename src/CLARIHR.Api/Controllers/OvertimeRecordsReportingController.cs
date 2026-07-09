using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

/// <summary>
/// Company-wide overtime-record operations ("horas extras del empleado", REQ-007): the atomic apply-period batch
/// (RF-012) and the pending/overdue tray. Intentionally NOT annotated with [AuthorizationPolicySet] (the convention
/// would assign the Manage policy to the POST pending query — a READ — producing false 403s for view-only users);
/// authorization is enforced per handler (Manage for the batch, View for the tray). Precedent: settlements /
/// recurring-incomes / one-time-incomes reporting controllers.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
public sealed class OvertimeRecordsReportingController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpPost("api/v1/companies/{companyId:guid}/overtime-records/apply-period")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<OvertimeApplyPeriodResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Apply the AUTORIZADA overtime records of a payroll period",
        Description = """
            Applies, ATOMICALLY, every `AUTORIZADA` overtime record of the company of the given `payrollTypeCode`
            (RF-012) whose work date has elapsed — including the "atrasados" whose declared period already passed;
            future organized shifts are excluded. Provide a `payrollPeriodPublicId` (FK real; its id/label are
            snapshotted onto the applications) or a bare `payrollPeriodLabel` to override the destination for every
            applied record; omit both to default each application to its record's declared destination.
            `excludedRecordPublicIds` postpones records (they stay AUTORIZADA for the next run). Any conflict rolls
            the whole batch back (422). HR-only (`ManageOvertimeRecords`). Returns the count of applied and postponed
            records.
            """)]
    public async Task<ActionResult<OvertimeApplyPeriodResult>> ApplyPeriod(
        Guid companyId,
        [FromBody] ApplyOvertimePeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ApplyOvertimePeriodCommand(
                companyId,
                request.PayrollTypeCode,
                request.PayrollPeriodPublicId,
                request.PayrollPeriodLabel,
                request.ExcludedRecordPublicIds ?? []),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
    [HttpPost("api/v1/companies/{companyId:guid}/overtime-records/pending/query")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<OvertimeRecordPendingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [SwaggerOperation(
        Summary = "Query the pending/overdue overtime records tray",
        Description = """
            Returns the company's `AUTORIZADA` overtime records without an active application (RF-012) — the ones
            still to be applied — each marked `isOverdue` when its declared payroll-period end date already passed.
            Optionally filter by `payrollTypeCode` and/or `onlyOverdue`. HR-only (`ViewOvertimeRecords`).
            """)]
    public async Task<ActionResult<OvertimeRecordPendingResponse>> QueryPending(
        Guid companyId,
        [FromBody] QueryOvertimeRecordPendingRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new QueryOvertimeRecordPendingQuery(companyId, request.PayrollTypeCode, request.OnlyOverdue ?? false),
            cancellationToken);
        return this.ToActionResult(result);
    }
}

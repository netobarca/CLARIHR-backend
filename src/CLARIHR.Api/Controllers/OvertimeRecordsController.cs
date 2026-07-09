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
/// Overtime records of a personnel file ("horas extras del empleado", REQ-007): the CRUD + the lifecycle
/// (annulment of an EN_REVISION draft, re-imputation of an AUTORIZADA record to another payroll period). The
/// authorizer resolution/revocation lives in <see cref="OvertimeRecordResolutionController"/> (dedicated
/// <c>AuthorizeOvertimeRecords</c> grant). This controller carries the DUAL channel (P-01): both class-level
/// policies are AUTHN-ONLY so the fine-grained gate — HR (Manage) OR the employee acting on their own file under
/// the self-service preference — runs in the handlers. Reads pass with the self-read gate (P-12).
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewOvertimeRecords, PersonnelFilePolicies.ManageOvertimeRecords)]
public sealed class OvertimeRecordsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/personnel-files/{publicId:guid}/overtime-records")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<OvertimeRecordResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's overtime records",
        Description = """
            Returns the overtime records of the specified personnel file. Passes with the `ViewOvertimeRecords`
            permission OR when the caller is the employee reading their own records (self-read, P-12). Each item
            carries its own `concurrencyToken` for the `If-Match` header of subsequent writes.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<OvertimeRecordResponse>>> GetOvertimeRecords(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileOvertimeRecordsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/overtime-records/{overtimeRecordPublicId:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<OvertimeRecordResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file overtime record by id",
        Description = "Returns a single overtime record. The `concurrencyToken` is required in the `If-Match` header of subsequent writes.")]
    public async Task<ActionResult<OvertimeRecordResponse>> GetOvertimeRecordById(
        Guid publicId,
        Guid overtimeRecordPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileOvertimeRecordByIdQuery(publicId, overtimeRecordPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/overtime-records")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<OvertimeRecordResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Register an overtime record",
        Description = """
            Creates an overtime record in status `EN_REVISION` and returns it with `201 Created`. DUAL channel
            (P-01): the caller has `ManageOvertimeRecords` (origin `RRHH`, the requester trío is mandatory) OR the
            caller is the employee self-registering on their own file when the company self-service preference is
            enabled (origin `PORTAL`, the requester is the subject employee; preference off → 403
            `OVERTIME_SELF_SERVICE_DISABLED`). The overtime type + justification type are resolved against the
            company masters (active; inactive → 422) and snapshotted; the applied factor defaults to the type's
            reference factor (an override needs a note, P-06); the h:m duration yields the decimal hours; a work
            date more than 366 days ahead → 422; the daily cap (P-05) blocks with 422 when the day's minutes exceed
            it; the plaza is optional (principal when omitted); the payroll type is validated against the catalog.
            The `ETag` header carries the initial `concurrencyToken`.
            """)]
    public async Task<ActionResult<OvertimeRecordResponse>> AddOvertimeRecord(
        Guid publicId,
        [FromBody] AddOvertimeRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileOvertimeRecordCommand(publicId, ToInput(request)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetOvertimeRecordById),
            value => new { publicId, overtimeRecordPublicId = value.OvertimeRecordPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/overtime-records/{overtimeRecordPublicId:guid}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<OvertimeRecordResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace an overtime record's header + shift",
        Description = """
            Replaces the business fields (shift + motive + plaza + requester + destination) of an `EN_REVISION`
            overtime record (RN-02; an authorized record is no longer editable). The caller has
            `ManageOvertimeRecords` (any record) OR is the employee editing their OWN `EN_REVISION` record
            registered through the portal (origin `PORTAL`). Requires the `If-Match` header with the current
            `concurrencyToken`; the new token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<OvertimeRecordResponse>> UpdateOvertimeRecord(
        Guid publicId,
        Guid overtimeRecordPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateOvertimeRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileOvertimeRecordCommand(publicId, overtimeRecordPublicId, ToInput(request), concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/overtime-records/{overtimeRecordPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Discard (soft-delete) an EN_REVISION overtime record draft",
        Description = """
            Soft-deletes an `EN_REVISION` draft (sets it inactive; no physical removal). Only a draft can be
            discarded — an authorized record is revoked or annulled. The caller has `ManageOvertimeRecords` OR is
            the employee discarding their OWN portal `EN_REVISION` draft. Requires the `If-Match` header with the
            current `concurrencyToken`. Returns the parent personnel file's refreshed concurrency token.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteOvertimeRecord(
        Guid publicId,
        Guid overtimeRecordPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileOvertimeRecordCommand(publicId, overtimeRecordPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/overtime-records/{overtimeRecordPublicId:guid}/annulment")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<OvertimeRecordResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Annul an EN_REVISION overtime record",
        Description = """
            Annuls (retiro del trámite) an `EN_REVISION` record (→ `ANULADA`, terminal); the `reason` is mandatory.
            The caller has `ManageOvertimeRecords` OR is the employee withdrawing their OWN portal `EN_REVISION`
            record. An `AUTORIZADA` record is revoked by the authorizer instead. Requires the `If-Match` header with
            the current `concurrencyToken`.
            """)]
    public async Task<ActionResult<OvertimeRecordResponse>> AnnulOvertimeRecord(
        Guid publicId,
        Guid overtimeRecordPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] AnnulOvertimeRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AnnulPersonnelFileOvertimeRecordCommand(publicId, overtimeRecordPublicId, request.Reason, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/overtime-records/{overtimeRecordPublicId:guid}/period")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<OvertimeRecordResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Re-target an AUTORIZADA overtime record to another payroll period",
        Description = """
            Re-imputes ("enviar a otro periodo", RF-005) the payroll destination (payroll type + period + label +
            end date) of an `AUTORIZADA` record WITHOUT touching its hours/factor. HR-only (`ManageOvertimeRecords`,
            no self-service). Only an `AUTORIZADA` record can be re-targeted. Requires the `If-Match` header with
            the current `concurrencyToken`.
            """)]
    public async Task<ActionResult<OvertimeRecordResponse>> RetargetOvertimeRecordPeriod(
        Guid publicId,
        Guid overtimeRecordPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] RetargetOvertimeRecordPeriodRequest request,
        CancellationToken cancellationToken = default)
    {
        var period = new OvertimeRecordPeriodInput(
            request.PayrollTypeCode,
            request.PayrollPeriodPublicId,
            request.PayrollPeriodLabel,
            request.PayrollPeriodEndDate);

        var result = await commandDispatcher.SendAsync(
            new RetargetPersonnelFileOvertimeRecordPeriodCommand(publicId, overtimeRecordPublicId, period, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    private static OvertimeRecordInput ToInput(AddOvertimeRecordRequest request) =>
        new(
            request.WorkDate,
            request.OvertimeTypePublicId,
            request.FactorApplied,
            request.FactorOverrideNote,
            request.DurationHours,
            request.DurationMinutes,
            request.StartTime,
            request.EndTime,
            request.JustificationTypePublicId,
            request.Observations,
            request.AssignedPositionPublicId,
            request.RequesterFilePublicId,
            request.PayrollTypeCode,
            request.PayrollPeriodPublicId,
            request.PayrollPeriodLabel,
            request.PayrollPeriodEndDate);

    private static OvertimeRecordInput ToInput(UpdateOvertimeRecordRequest request) =>
        new(
            request.WorkDate,
            request.OvertimeTypePublicId,
            request.FactorApplied,
            request.FactorOverrideNote,
            request.DurationHours,
            request.DurationMinutes,
            request.StartTime,
            request.EndTime,
            request.JustificationTypePublicId,
            request.Observations,
            request.AssignedPositionPublicId,
            request.RequesterFilePublicId,
            request.PayrollTypeCode,
            request.PayrollPeriodPublicId,
            request.PayrollPeriodLabel,
            request.PayrollPeriodEndDate);
}

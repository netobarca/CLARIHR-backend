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
/// AUTHORIZER actions on overtime records: resolution (authorize/reject an EN_REVISION record) and revocation
/// (annul an AUTORIZADA record). Kept in a dedicated controller because the class-level policy set must map its
/// writes to <see cref="PersonnelFilePolicies.AuthorizeOvertimeRecords"/> — a grant that <c>PersonnelFiles.Admin</c>
/// deliberately does NOT imply (separation of duties, mirrors AuthorizeRetirement). Both actions also enforce, at
/// the handler, that neither the SUBJECT employee, the REGISTRAR nor the REQUESTER acts (TRIPLE anti-self → 403).
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewOvertimeRecords, PersonnelFilePolicies.AuthorizeOvertimeRecords)]
public sealed class OvertimeRecordResolutionController(
    ICommandDispatcher commandDispatcher) : ControllerBase
{
    [HttpPatch("api/v1/personnel-files/{publicId:guid}/overtime-records/{overtimeRecordPublicId:guid}/resolution")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<OvertimeRecordResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Authorize or reject an overtime record",
        Description = """
            Authorizer resolution of an `EN_REVISION` record: `targetStatusCode` = `AUTORIZADA` (authorize) or
            `RECHAZADA` (reject — note MANDATORY, terminal). Requires the dedicated `AuthorizeOvertimeRecords` grant
            (`PersonnelFiles.Admin` does NOT imply it). Separation of duties (TRIPLE anti-self): neither the subject
            employee, the registrar nor the requester may resolve (403). Requires the `If-Match` header with the
            record's current `concurrencyToken`.
            """)]
    public async Task<ActionResult<OvertimeRecordResponse>> ResolveOvertimeRecord(
        Guid publicId,
        Guid overtimeRecordPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] ResolveOvertimeRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ResolvePersonnelFileOvertimeRecordCommand(publicId, overtimeRecordPublicId, request.TargetStatusCode, request.Note, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/overtime-records/{overtimeRecordPublicId:guid}/revocation")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<OvertimeRecordResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Revoke an AUTORIZADA overtime record",
        Description = """
            Authorizer revocation of an `AUTORIZADA` record (→ `ANULADA`, terminal); the `reason` is mandatory.
            Requires the dedicated `AuthorizeOvertimeRecords` grant. Separation of duties (TRIPLE anti-self):
            neither the subject employee, the registrar nor the requester may revoke (403). Requires the `If-Match`
            header with the record's current `concurrencyToken`.
            """)]
    public async Task<ActionResult<OvertimeRecordResponse>> RevokeOvertimeRecord(
        Guid publicId,
        Guid overtimeRecordPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] RevokeOvertimeRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new RevokePersonnelFileOvertimeRecordCommand(publicId, overtimeRecordPublicId, request.Reason, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }
}

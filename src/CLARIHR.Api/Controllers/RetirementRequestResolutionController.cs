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
/// AUTHORIZER actions on retirement requests: resolution (authorize/reject, RF-004) and annulment of an
/// AUTORIZADA (RN-005.1). Kept in a dedicated controller because the class-level policy set must map its
/// writes to <see cref="PersonnelFilePolicies.AuthorizeRetirement"/> — a grant that
/// <c>PersonnelFiles.Admin</c> deliberately does NOT imply (D-12/D-13, separation of duties) and that a
/// pure authorizer holds without <c>ManageRetirements</c>. Both actions also enforce, at the handler, that
/// neither the SUBJECT employee nor the REQUESTER acts (403s dedicados).
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewRetirements, PersonnelFilePolicies.AuthorizeRetirement)]
public sealed class RetirementRequestResolutionController(
    ICommandDispatcher commandDispatcher) : ControllerBase
{
    [HttpPatch("api/v1/personnel-files/{publicId:guid}/retirement-requests/{retirementRequestPublicId:guid}/resolution")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileRetirementRequestResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Authorize or reject a retirement request",
        Description = """
            Authorizer resolution of a `SOLICITADA` request (RF-004): `targetStatusCode` = `AUTORIZADA` (note
            optional — enables the exit interview, D-07, and the execution) or `RECHAZADA` (note MANDATORY,
            RN-004.3; terminal). Requires the dedicated `AuthorizeRetirement` grant (`PersonnelFiles.Admin` does
            NOT imply it — D-12). Separation of duties (D-13): neither the subject employee nor the requester may
            resolve (403). Requires the `If-Match` header with the request's current `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFileRetirementRequestResponse>> ResolveRetirementRequest(
        Guid publicId,
        Guid retirementRequestPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] ResolveRetirementRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ResolveRetirementRequestCommand(publicId, retirementRequestPublicId, request.TargetStatusCode, request.Notes, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/retirement-requests/{retirementRequestPublicId:guid}/annulment")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileRetirementRequestResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Annul an AUTHORIZED retirement request",
        Description = """
            Authorizer annulment of an `AUTORIZADA` request (RN-005.1): terminal `ANULADA`; the employee leaves
            the interview tray and ALL their non-archived exit-interview submissions are archived (RN-005.3 —
            the baja will not happen, so it must not count in rotation analytics). A `SOLICITADA` is annulled by
            the manager via `PATCH …/cancel`; an `EJECUTADA` is never annulled — it is reverted. Requires the
            `If-Match` header with the request's current `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFileRetirementRequestResponse>> AnnulAuthorizedRetirementRequest(
        Guid publicId,
        Guid retirementRequestPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] CancelRetirementRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AnnulAuthorizedRetirementRequestCommand(publicId, retirementRequestPublicId, request.Notes, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }
}

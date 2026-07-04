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
/// Reversal of an executed retirement (RF-010). Kept in a dedicated controller because the class-level policy
/// set must map its write to <see cref="PersonnelFilePolicies.RevertRetirement"/> — a high-trust grant that
/// <c>PersonnelFiles.Admin</c> deliberately does NOT imply (D-12).
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewRetirements, PersonnelFilePolicies.RevertRetirement)]
public sealed class RetirementRequestReversalController(
    ICommandDispatcher commandDispatcher) : ControllerBase
{
    [HttpPatch("api/v1/personnel-files/{publicId:guid}/retirement-requests/{retirementRequestPublicId:guid}/reversal")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileRetirementRequestResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Revert an EXECUTED retirement (restores the execution snapshot)",
        Description = """
            Undoes an `EJECUTADA` baja in ONE transaction, restoring exactly what the execution changed
            (D-11): the profile's retirement metadata is cleared and its PRIOR employment status restored, the
            personnel file is reactivated with its prior rehire-block state, the closed plaza assignments and
            contracts are reopened with their PREVIOUS end dates, the login is reactivated only if it was
            active before, the exit-interview submissions are archived (D-09 — the baja "did not happen") and
            a `REVERSION_BAJA` action is journaled. Seniority resumes CONTINUOUS (unlike a rehire, which opens
            a new period). Blocks (422): more than 30 calendar days since the execution (ratified RN-012.4 —
            use a rehire instead), a rehire after the execution (D-10), a diverged employee state (RN-012.2),
            or not being the most recent executed retirement (RN-012.3). The reason is MANDATORY; the subject
            employee cannot revert their own baja (403). Requires the dedicated `RevertRetirement` grant
            (`PersonnelFiles.Admin` does NOT imply it) and the `If-Match` header with the request's current
            `concurrencyToken`. After reverting, the employee is eligible for a NEW retirement request.
            """)]
    public async Task<ActionResult<PersonnelFileRetirementRequestResponse>> RevertRetirementRequest(
        Guid publicId,
        Guid retirementRequestPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] RevertRetirementRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new RevertRetirementRequestCommand(publicId, retirementRequestPublicId, request.Reason, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }
}

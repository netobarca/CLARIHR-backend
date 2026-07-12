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
/// AUTHORIZER actions on one-time deductions: resolution (authorize/reject an EN_REVISION deduction) and
/// revocation (annul an AUTORIZADO one). Kept in a dedicated controller because the class-level policy set must
/// map its writes to <see cref="PersonnelFilePolicies.AuthorizeOneTimeDeductions"/> — a grant that
/// <c>PersonnelFiles.Admin</c> deliberately does NOT imply. Both actions also enforce, at the handler, the
/// TRIPLE anti-self: neither the SUBJECT employee, nor the REQUESTER, nor the REGISTRAR may act (403).
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewOneTimeDeductions, PersonnelFilePolicies.AuthorizeOneTimeDeductions)]
public sealed class OneTimeDeductionResolutionController(
    ICommandDispatcher commandDispatcher) : ControllerBase
{
    [HttpPatch("api/v1/personnel-files/{publicId:guid}/one-time-deductions/{oneTimeDeductionPublicId:guid}/resolution")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<OneTimeDeductionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Authorize or reject a one-time deduction",
        Description = """
            Authorizer resolution of an `EN_REVISION` deduction: `targetStatusCode` = `AUTORIZADO` (authorize) or
            `RECHAZADO` (reject — note MANDATORY, terminal). Requires the dedicated `AuthorizeOneTimeDeductions`
            grant (`PersonnelFiles.Admin` does NOT imply it). Separation of duties (anti-self TRIPLE): neither the
            subject employee, nor the requester, nor the registrar may resolve (403). Requires `If-Match`.
            """)]
    public async Task<ActionResult<OneTimeDeductionResponse>> ResolveOneTimeDeduction(
        Guid publicId,
        Guid oneTimeDeductionPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] ResolveOneTimeDeductionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ResolvePersonnelFileOneTimeDeductionCommand(
                publicId, oneTimeDeductionPublicId, request.TargetStatusCode, request.Note, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/one-time-deductions/{oneTimeDeductionPublicId:guid}/revocation")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<OneTimeDeductionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Revoke an AUTORIZADO one-time deduction",
        Description = """
            Authorizer revocation of an `AUTORIZADO` deduction (→ `ANULADO`, terminal); the `reason` is mandatory.
            An APPLIED deduction must have its application reverted first. Requires the dedicated
            `AuthorizeOneTimeDeductions` grant and enforces the anti-self TRIPLE (403). Requires `If-Match`.
            """)]
    public async Task<ActionResult<OneTimeDeductionResponse>> RevokeOneTimeDeduction(
        Guid publicId,
        Guid oneTimeDeductionPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] RevokeOneTimeDeductionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new RevokePersonnelFileOneTimeDeductionCommand(
                publicId, oneTimeDeductionPublicId, request.Reason, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }
}

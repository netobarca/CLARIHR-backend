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
/// AUTHORIZER actions on one-time incomes: resolution (authorize/reject an EN_REVISION income) and revocation
/// (annul an AUTORIZADO income). Kept in a dedicated controller because the class-level policy set must map its
/// writes to <see cref="PersonnelFilePolicies.AuthorizeOneTimeIncomes"/> — a grant that <c>PersonnelFiles.Admin</c>
/// deliberately does NOT imply (separation of duties, mirrors AuthorizeRetirement). Both actions also enforce, at
/// the handler, that neither the SUBJECT employee, the REGISTRAR nor the REQUESTER acts (TRIPLE anti-self → 403).
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewOneTimeIncomes, PersonnelFilePolicies.AuthorizeOneTimeIncomes)]
public sealed class OneTimeIncomeResolutionController(
    ICommandDispatcher commandDispatcher) : ControllerBase
{
    [HttpPatch("api/v1/personnel-files/{publicId:guid}/one-time-incomes/{oneTimeIncomePublicId:guid}/resolution")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<OneTimeIncomeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Authorize or reject a one-time income",
        Description = """
            Authorizer resolution of an `EN_REVISION` income: `targetStatusCode` = `AUTORIZADO` (authorize) or
            `RECHAZADO` (reject — note MANDATORY, terminal). Requires the dedicated `AuthorizeOneTimeIncomes`
            grant (`PersonnelFiles.Admin` does NOT imply it). Separation of duties (TRIPLE anti-self): neither the
            subject employee, the registrar nor the requester may resolve (403). Requires the `If-Match` header
            with the income's current `concurrencyToken`.
            """)]
    public async Task<ActionResult<OneTimeIncomeResponse>> ResolveOneTimeIncome(
        Guid publicId,
        Guid oneTimeIncomePublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] ResolveOneTimeIncomeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ResolvePersonnelFileOneTimeIncomeCommand(publicId, oneTimeIncomePublicId, request.TargetStatusCode, request.Note, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/one-time-incomes/{oneTimeIncomePublicId:guid}/revocation")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<OneTimeIncomeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Revoke an AUTORIZADO one-time income",
        Description = """
            Authorizer revocation of an `AUTORIZADO` income (→ `ANULADO`, terminal); the `reason` is mandatory.
            Requires the dedicated `AuthorizeOneTimeIncomes` grant. Separation of duties (TRIPLE anti-self):
            neither the subject employee, the registrar nor the requester may revoke (403). Requires the `If-Match`
            header with the income's current `concurrencyToken`.
            """)]
    public async Task<ActionResult<OneTimeIncomeResponse>> RevokeOneTimeIncome(
        Guid publicId,
        Guid oneTimeIncomePublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] RevokeOneTimeIncomeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new RevokePersonnelFileOneTimeIncomeCommand(publicId, oneTimeIncomePublicId, request.Reason, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }
}

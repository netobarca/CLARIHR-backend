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
/// AUTHORIZER actions on recurring incomes: resolution (authorize/reject an EN_REVISION income) and revocation
/// (annul a VIGENTE income). Kept in a dedicated controller because the class-level policy set must map its writes
/// to <see cref="PersonnelFilePolicies.AuthorizeRecurringIncomes"/> — a grant that <c>PersonnelFiles.Admin</c>
/// deliberately does NOT imply (separation of duties, mirrors AuthorizeRetirement). Both actions also enforce, at
/// the handler, that neither the SUBJECT employee nor the REGISTRAR acts (double anti-self → 403).
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewRecurringIncomes, PersonnelFilePolicies.AuthorizeRecurringIncomes)]
public sealed class RecurringIncomeResolutionController(
    ICommandDispatcher commandDispatcher) : ControllerBase
{
    [HttpPatch("api/v1/personnel-files/{publicId:guid}/recurring-incomes/{recurringIncomePublicId:guid}/resolution")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<RecurringIncomeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Authorize or reject a recurring income",
        Description = """
            Authorizer resolution of an `EN_REVISION` income: `targetStatusCode` = `VIGENTE` (authorize) or
            `RECHAZADO` (reject — note MANDATORY, terminal). Requires the dedicated `AuthorizeRecurringIncomes`
            grant (`PersonnelFiles.Admin` does NOT imply it). Separation of duties (double anti-self): neither the
            subject employee nor the registrar may resolve (403). Requires the `If-Match` header with the income's
            current `concurrencyToken`.
            """)]
    public async Task<ActionResult<RecurringIncomeResponse>> ResolveRecurringIncome(
        Guid publicId,
        Guid recurringIncomePublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] ResolveRecurringIncomeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ResolvePersonnelFileRecurringIncomeCommand(publicId, recurringIncomePublicId, request.TargetStatusCode, request.Note, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/recurring-incomes/{recurringIncomePublicId:guid}/revocation")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<RecurringIncomeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Revoke a VIGENTE recurring income",
        Description = """
            Authorizer revocation of a `VIGENTE` income (→ `ANULADO`, terminal); the `reason` is mandatory.
            Requires the dedicated `AuthorizeRecurringIncomes` grant. Separation of duties (double anti-self):
            neither the subject employee nor the registrar may revoke (403). Requires the `If-Match` header with
            the income's current `concurrencyToken`.
            """)]
    public async Task<ActionResult<RecurringIncomeResponse>> RevokeRecurringIncome(
        Guid publicId,
        Guid recurringIncomePublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] RevokeRecurringIncomeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new RevokePersonnelFileRecurringIncomeCommand(publicId, recurringIncomePublicId, request.Reason, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }
}

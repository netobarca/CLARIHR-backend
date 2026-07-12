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
/// AUTHORIZER actions on recurring deductions: resolution (authorize/reject an EN_REVISION credit) and revocation
/// (annul a VIGENTE one). Kept in a dedicated controller because the class-level policy set must map its writes to
/// <see cref="PersonnelFilePolicies.AuthorizeRecurringDeductions"/> — a grant that <c>PersonnelFiles.Admin</c>
/// deliberately does NOT imply (separation of duties). Both actions also enforce, at the handler, that neither the
/// SUBJECT employee nor the REGISTRAR acts (double anti-self → 403).
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewRecurringDeductions, PersonnelFilePolicies.AuthorizeRecurringDeductions)]
public sealed class RecurringDeductionResolutionController(
    ICommandDispatcher commandDispatcher) : ControllerBase
{
    [HttpPatch("api/v1/personnel-files/{publicId:guid}/recurring-deductions/{recurringDeductionPublicId:guid}/resolution")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<RecurringDeductionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Authorize or reject a recurring deduction",
        Description = """
            Authorizer resolution of an `EN_REVISION` credit: `targetStatusCode` = `VIGENTE` (authorize) or
            `RECHAZADO` (reject — note MANDATORY, terminal). Requires the dedicated `AuthorizeRecurringDeductions`
            grant (`PersonnelFiles.Admin` does NOT imply it). Separation of duties (double anti-self): neither the
            subject employee nor the registrar may resolve (403). Requires the `If-Match` header with the credit's
            current `concurrencyToken`.
            """)]
    public async Task<ActionResult<RecurringDeductionResponse>> ResolveRecurringDeduction(
        Guid publicId,
        Guid recurringDeductionPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] ResolveRecurringDeductionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ResolvePersonnelFileRecurringDeductionCommand(
                publicId, recurringDeductionPublicId, request.TargetStatusCode, request.Note, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/recurring-deductions/{recurringDeductionPublicId:guid}/revocation")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<RecurringDeductionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Revoke a VIGENTE recurring deduction",
        Description = """
            Authorizer revocation of a `VIGENTE` credit (→ `ANULADO`, terminal); the `reason` is mandatory.
            Requires the dedicated `AuthorizeRecurringDeductions` grant. Separation of duties (double anti-self):
            neither the subject employee nor the registrar may revoke (403). Requires the `If-Match` header with
            the credit's current `concurrencyToken`.
            """)]
    public async Task<ActionResult<RecurringDeductionResponse>> RevokeRecurringDeduction(
        Guid publicId,
        Guid recurringDeductionPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] RevokeRecurringDeductionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new RevokePersonnelFileRecurringDeductionCommand(
                publicId, recurringDeductionPublicId, request.Reason, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }
}

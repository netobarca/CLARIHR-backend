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
/// Compensatory-time credits ("acreditaciones de tiempo compensatorio", REQ-002). Kept in a dedicated controller
/// so it runs under its own authn-only policy set (<see cref="PersonnelFilePolicies.ViewCompensatoryTime"/> /
/// <see cref="PersonnelFilePolicies.ManageCompensatoryTime"/>); the precise permission-or-self decision for reads
/// and the HR-only writes (D-01) are enforced by the handler gates. Registering a credit computes the credited
/// hours (<c>hoursWorked × factor</c> snapshot, or a manual override) and requires the branch authorization
/// document when the company preference mandates it (D-20). Editing down or annulling a credit is re-verified
/// against the fund balance under an advisory lock so it can never uncover a negative balance (R-T1).
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewCompensatoryTime, PersonnelFilePolicies.ManageCompensatoryTime)]
public sealed class PersonnelFileCompensatoryTimeCreditsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/personnel-files/{publicId:guid}/compensatory-time-credits")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileCompensatoryTimeCreditResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's compensatory-time credits",
        Description = """
            Returns every compensatory-time credit recorded for the specified personnel file. Access requires the
            `ViewCompensatoryTime` permission or the employee reading their own credits.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileCompensatoryTimeCreditResponse>>> GetCredits(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetCompensatoryTimeCreditsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/compensatory-time-credits/{creditPublicId:guid}", Name = "GetCompensatoryTimeCreditById")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileCompensatoryTimeCreditResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(Summary = "Get a compensatory-time credit by id")]
    public async Task<ActionResult<PersonnelFileCompensatoryTimeCreditResponse>> GetCreditById(
        Guid publicId,
        Guid creditPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetCompensatoryTimeCreditByIdQuery(publicId, creditPublicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/compensatory-time-credits")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileCompensatoryTimeCreditResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Register a compensatory-time credit",
        Description = """
            Registers a compensatory-time credit and credits the hours into the employee fund (`hoursWorked ×
            factor` snapshot, or a manual `hoursCreditedOverride` with a mandatory note). HR-only
            (`ManageCompensatoryTime`). When the company preference requires it, an `authorizationFilePublicId`
            (purpose `CompensatoryTimeDocument`) is mandatory and is attached in the same transaction. Journals
            the ACREDITACION_TIEMPO_COMPENSATORIO personnel action.
            """)]
    public async Task<ActionResult<PersonnelFileCompensatoryTimeCreditResponse>> AddCredit(
        Guid publicId,
        [FromBody] AddCompensatoryTimeCreditRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddCompensatoryTimeCreditCommand(
                publicId,
                ToInput(request),
                request.AuthorizationFilePublicId,
                request.DocumentTypeCatalogItemPublicId,
                request.DocumentObservations),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetCreditById),
            value => new { publicId, creditPublicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/compensatory-time-credits/{creditPublicId:guid}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileCompensatoryTimeCreditResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Edit a compensatory-time credit's business fields",
        Description = """
            Updates the type, dates, hours and notes and recomputes the credited hours. Editing the hours down is
            re-verified against the fund balance under an advisory lock (the balance can never go negative — R-T1).
            Requires the `If-Match` header with the current `concurrencyToken`; the new token is returned in the
            `ETag` header. HR-only.
            """)]
    public async Task<ActionResult<PersonnelFileCompensatoryTimeCreditResponse>> UpdateCredit(
        Guid publicId,
        Guid creditPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateCompensatoryTimeCreditRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateCompensatoryTimeCreditCommand(publicId, creditPublicId, ToInput(request), concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/compensatory-time-credits/{creditPublicId:guid}/annulment")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileCompensatoryTimeCreditResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Annul a compensatory-time credit",
        Description = """
            Annuls a REGISTRADA credit (terminal); the reason is mandatory. An annulled credit no longer counts
            toward the fund balance. If debits already stand against the credit the annulment is rejected under an
            advisory lock (the balance would go negative — R-T1). Requires the `If-Match` header. HR-only.
            """)]
    public async Task<ActionResult<PersonnelFileCompensatoryTimeCreditResponse>> AnnulCredit(
        Guid publicId,
        Guid creditPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] AnnulCompensatoryTimeCreditRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AnnulCompensatoryTimeCreditCommand(publicId, creditPublicId, request.Reason, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    private static CompensatoryTimeCreditInput ToInput(AddCompensatoryTimeCreditRequest request) =>
        new(
            request.CompensatoryTimeTypePublicId,
            request.WorkDate,
            request.StartTime,
            request.EndTime,
            request.HoursWorked,
            request.HoursCreditedOverride,
            request.OverrideNote,
            request.WorkDetail,
            request.AuthorizedByText,
            request.AssignedPositionPublicId,
            request.OvertimeRecordPublicId,
            request.Notes);

    private static CompensatoryTimeCreditInput ToInput(UpdateCompensatoryTimeCreditRequest request) =>
        new(
            request.CompensatoryTimeTypePublicId,
            request.WorkDate,
            request.StartTime,
            request.EndTime,
            request.HoursWorked,
            request.HoursCreditedOverride,
            request.OverrideNote,
            request.WorkDetail,
            request.AuthorizedByText,
            request.AssignedPositionPublicId,
            request.OvertimeRecordPublicId,
            request.Notes);
}

using System.ComponentModel.DataAnnotations;
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
/// Compensatory-time absences ("ausencias / goces de tiempo compensatorio", REQ-002 PR-4). Kept in a dedicated
/// controller so it runs under its own authn-only policy set (<see cref="PersonnelFilePolicies.ViewCompensatoryTime"/>
/// / <see cref="PersonnelFilePolicies.ManageCompensatoryTime"/>); the permission-or-self decision for reads and the
/// HR-only writes (D-01) are enforced by the handler gates. Registering (or editing up) an absence debits hours
/// from the employee fund; the never-negative invariant is re-verified against the balance under an advisory lock
/// INSIDE the transaction (R-T1). Overlaps with another compensatory-time absence, an active incapacity or a live
/// vacation are rejected (RN-05); an optional payroll-period imputation is validated against the company master.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewCompensatoryTime, PersonnelFilePolicies.ManageCompensatoryTime)]
public sealed class PersonnelFileCompensatoryTimeAbsencesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/personnel-files/{publicId:guid}/compensatory-time-absences")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileCompensatoryTimeAbsenceResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's compensatory-time absences",
        Description = """
            Returns every compensatory-time absence recorded for the specified personnel file. Access requires the
            `ViewCompensatoryTime` permission or the employee reading their own absences.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileCompensatoryTimeAbsenceResponse>>> GetAbsences(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetCompensatoryTimeAbsencesQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/compensatory-time-absences/{absencePublicId:guid}", Name = "GetCompensatoryTimeAbsenceById")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileCompensatoryTimeAbsenceResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(Summary = "Get a compensatory-time absence by id")]
    public async Task<ActionResult<PersonnelFileCompensatoryTimeAbsenceResponse>> GetAbsenceById(
        Guid publicId,
        Guid absencePublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetCompensatoryTimeAbsenceByIdQuery(publicId, absencePublicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/compensatory-time-absences/absence-hours-suggestion")]
    [Produces("application/json")]
    [ProducesResponseType<CompensatoryTimeAbsenceHoursSuggestionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Suggest the hours to debit for an absence range",
        Description = """
            Returns the suggested hours to debit for the `[start, end]` range = working days (excluding the plaza
            rest day and the tenant holidays) × the standard daily hours (company preference, default 8). Advisory
            only — the value can be overridden in the POST. Read access (`ViewCompensatoryTime` or self).
            """)]
    public async Task<ActionResult<CompensatoryTimeAbsenceHoursSuggestionResponse>> GetAbsenceHoursSuggestion(
        Guid publicId,
        [FromQuery, Required] DateOnly start,
        [FromQuery, Required] DateOnly end,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetCompensatoryTimeAbsenceHoursSuggestionQuery(publicId, start, end), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/compensatory-time-absences")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileCompensatoryTimeAbsenceResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Register a compensatory-time absence",
        Description = """
            Registers a compensatory-time absence and debits the hours from the employee fund. HR-only
            (`ManageCompensatoryTime`). The debited hours are re-verified against the fund balance under an
            advisory lock (`422 COMPENSATORY_TIME_BALANCE_INSUFFICIENT` when the balance is not enough). The range
            must not overlap another active absence (`422 COMPENSATORY_TIME_ABSENCE_OVERLAP`), an active incapacity
            (`422 COMPENSATORY_TIME_INCAPACITY_OVERLAP`) or a live vacation (`422 COMPENSATORY_TIME_VACATION_OVERLAP`);
            an invalid `payrollPeriodPublicId` yields `422 COMPENSATORY_TIME_PAYROLL_PERIOD_INVALID`. Journals the
            GOCE_TIEMPO_COMPENSATORIO personnel action.
            """)]
    public async Task<ActionResult<PersonnelFileCompensatoryTimeAbsenceResponse>> AddAbsence(
        Guid publicId,
        [FromBody] AddCompensatoryTimeAbsenceRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddCompensatoryTimeAbsenceCommand(publicId, ToInput(request)), cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetAbsenceById),
            value => new { publicId, absencePublicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/compensatory-time-absences/{absencePublicId:guid}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileCompensatoryTimeAbsenceResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Edit a compensatory-time absence's business fields",
        Description = """
            Updates the type, dates, hours, reason and imputation. Raising the debited hours is re-verified against
            the fund balance under an advisory lock (the balance can never go negative — R-T1). Requires the
            `If-Match` header with the current `concurrencyToken`; the new token is returned in the `ETag` header.
            HR-only.
            """)]
    public async Task<ActionResult<PersonnelFileCompensatoryTimeAbsenceResponse>> UpdateAbsence(
        Guid publicId,
        Guid absencePublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateCompensatoryTimeAbsenceRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateCompensatoryTimeAbsenceCommand(publicId, absencePublicId, ToInput(request), concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/compensatory-time-absences/{absencePublicId:guid}/annulment")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileCompensatoryTimeAbsenceResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Annul a compensatory-time absence",
        Description = """
            Annuls a REGISTRADA absence (terminal); the reason is mandatory. Annulling restores the debited hours
            to the fund balance. Requires the `If-Match` header. HR-only.
            """)]
    public async Task<ActionResult<PersonnelFileCompensatoryTimeAbsenceResponse>> AnnulAbsence(
        Guid publicId,
        Guid absencePublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] AnnulCompensatoryTimeAbsenceRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AnnulCompensatoryTimeAbsenceCommand(publicId, absencePublicId, request.Reason, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    private static CompensatoryTimeAbsenceInput ToInput(AddCompensatoryTimeAbsenceRequest request) =>
        new(
            request.CompensatoryTimeTypePublicId,
            request.StartDate,
            request.EndDate,
            request.HoursDebited,
            request.Reason,
            request.PayrollPeriodPublicId,
            request.Notes);

    private static CompensatoryTimeAbsenceInput ToInput(UpdateCompensatoryTimeAbsenceRequest request) =>
        new(
            request.CompensatoryTimeTypePublicId,
            request.StartDate,
            request.EndDate,
            request.HoursDebited,
            request.Reason,
            request.PayrollPeriodPublicId,
            request.Notes);
}

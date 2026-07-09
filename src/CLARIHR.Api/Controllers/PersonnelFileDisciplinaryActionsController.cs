using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Application.Features.PersonnelFiles.PersonnelTransactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

/// <summary>
/// Employee disciplinary actions ("amonestaciones", REQ-003). Kept in a dedicated controller so it runs under
/// its own authn-only policy set (<see cref="PersonnelFilePolicies.ViewDisciplinaryActions"/> /
/// <see cref="PersonnelFilePolicies.ManageDisciplinaryActions"/>); the precise permission-or-self decision
/// (D-05/D-13), the dedicated <c>AuthorizeDisciplinaryActions</c> grant on the decision/revocation and the
/// double anti-self-approval check are enforced by the disciplinary-action handler gates, which a declarative
/// policy cannot express.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewDisciplinaryActions, PersonnelFilePolicies.ManageDisciplinaryActions)]
public sealed class PersonnelFileDisciplinaryActionsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/personnel-files/{publicId:guid}/disciplinary-actions")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileDisciplinaryActionResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's disciplinary actions",
        Description = """
            Returns the disciplinary actions recorded for the specified personnel file with their one-decision
            lifecycle. Access requires the `ViewDisciplinaryActions` permission or the employee reading their own
            disciplinary actions — the employee only ever sees their APLICADA disciplinary actions (D-13).
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileDisciplinaryActionResponse>>> GetDisciplinaryActions(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileDisciplinaryActionsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/disciplinary-actions/{disciplinaryActionPublicId:guid}", Name = "GetDisciplinaryActionById")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileDisciplinaryActionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(Summary = "Get a disciplinary action by id")]
    public async Task<ActionResult<PersonnelFileDisciplinaryActionResponse>> GetDisciplinaryActionById(
        Guid publicId,
        Guid disciplinaryActionPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileDisciplinaryActionByIdQuery(publicId, disciplinaryActionPublicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/disciplinary-actions")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileDisciplinaryActionResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Register a disciplinary action",
        Description = """
            Registers a disciplinary action (EN_REVISION) with no personnel-action entry yet. HR-only
            (`ManageDisciplinaryActions`). The incident date must be ≤ today; the suspension block is only
            allowed on a type that applies suspension (RN-05); a payroll deduction requires a positive amount
            (RN-06). The type and cause are validated against the active company-managed masters.
            """)]
    public async Task<ActionResult<PersonnelFileDisciplinaryActionResponse>> AddDisciplinaryAction(
        Guid publicId,
        [FromBody] AddDisciplinaryActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileDisciplinaryActionCommand(publicId, ToInput(request)), cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetDisciplinaryActionById),
            value => new { publicId, disciplinaryActionPublicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/disciplinary-actions/{disciplinaryActionPublicId:guid}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileDisciplinaryActionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Edit a disciplinary action's declarative fields",
        Description = """
            Updates the type, cause, incident date, facts, deduction block and suspension block of an
            EN_REVISION disciplinary action (RN-01, manager-only). Requires the `If-Match` header with the
            current `concurrencyToken`; the new token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileDisciplinaryActionResponse>> UpdateDisciplinaryAction(
        Guid publicId,
        Guid disciplinaryActionPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateDisciplinaryActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileDisciplinaryActionCommand(publicId, disciplinaryActionPublicId, ToInput(request), concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/disciplinary-actions/{disciplinaryActionPublicId:guid}/decision")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileDisciplinaryActionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Decide a disciplinary action (APLICAR / RECHAZAR)",
        Description = """
            The single decision (RN-01/RN-02). `APLICAR` moves EN_REVISION → APLICADA and journals an
            `AMONESTACION` personnel action (plus a `SUSPENSION` entry when the suspension block travels) in the
            same transaction, re-checking the suspension overlap under a per-employee lock (RN-18) and freezing
            the deduction concept (aclaración №5); `RECHAZAR` moves EN_REVISION → RECHAZADA and requires a
            `note`. Requires the dedicated `AuthorizeDisciplinaryActions` grant (Admin excluded); the subject
            employee and the registrar cannot decide (double anti-self, 403). Requires the `If-Match` header.
            """)]
    public async Task<ActionResult<PersonnelFileDisciplinaryActionResponse>> DecideDisciplinaryAction(
        Guid publicId,
        Guid disciplinaryActionPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] DisciplinaryActionDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DecidePersonnelFileDisciplinaryActionCommand(publicId, disciplinaryActionPublicId, request.Decision, request.Note, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/disciplinary-actions/{disciplinaryActionPublicId:guid}/annulment")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileDisciplinaryActionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Annul or revoke a disciplinary action",
        Description = """
            Annuls an EN_REVISION disciplinary action (trámite withdrawal — `ManageDisciplinaryActions`) or
            revokes an APLICADA one (revocation — the dedicated `AuthorizeDisciplinaryActions` grant + double
            anti-self; annuls the linked `AMONESTACION` and `SUSPENSION` entries in the same transaction). The
            reason is mandatory. Requires the `If-Match` header.
            """)]
    public async Task<ActionResult<PersonnelFileDisciplinaryActionResponse>> AnnulDisciplinaryAction(
        Guid publicId,
        Guid disciplinaryActionPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] DisciplinaryActionAnnulmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AnnulPersonnelFileDisciplinaryActionCommand(publicId, disciplinaryActionPublicId, request.Reason, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    private static DisciplinaryActionInput ToInput(AddDisciplinaryActionRequest request) =>
        new(
            request.DisciplinaryActionTypePublicId,
            request.DisciplinaryActionCausePublicId,
            request.IncidentDate,
            request.FactsDetail,
            request.HasPayrollDeduction,
            request.DeductionAmount,
            request.CurrencyCode,
            request.DeductionConceptTypeCode,
            request.SuspensionStartDate,
            request.SuspensionEndDate,
            request.AssignedPositionPublicId,
            request.Notes);

    private static DisciplinaryActionInput ToInput(UpdateDisciplinaryActionRequest request) =>
        new(
            request.DisciplinaryActionTypePublicId,
            request.DisciplinaryActionCausePublicId,
            request.IncidentDate,
            request.FactsDetail,
            request.HasPayrollDeduction,
            request.DeductionAmount,
            request.CurrencyCode,
            request.DeductionConceptTypeCode,
            request.SuspensionStartDate,
            request.SuspensionEndDate,
            request.AssignedPositionPublicId,
            request.Notes);
}

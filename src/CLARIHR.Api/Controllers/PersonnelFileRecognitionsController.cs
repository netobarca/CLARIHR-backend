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
/// Employee recognitions ("reconocimientos", REQ-003). Kept in a dedicated controller so it runs under its own
/// authn-only policy set (<see cref="PersonnelFilePolicies.ViewRecognitions"/> /
/// <see cref="PersonnelFilePolicies.ManageRecognitions"/>); the precise permission-or-self decision (D-05/D-13),
/// the dedicated <c>AuthorizeRecognitions</c> grant on the decision/revocation and the double
/// anti-self-approval check are enforced by the recognition handler gates, which a declarative policy cannot
/// express.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewRecognitions, PersonnelFilePolicies.ManageRecognitions)]
public sealed class PersonnelFileRecognitionsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/personnel-files/{publicId:guid}/recognitions")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileRecognitionResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's recognitions",
        Description = """
            Returns the recognitions recorded for the specified personnel file with their one-decision lifecycle.
            Access requires the `ViewRecognitions` permission or the employee reading their own recognitions —
            the employee only ever sees their APLICADA recognitions (D-13).
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileRecognitionResponse>>> GetRecognitions(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileRecognitionsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/recognitions/{recognitionPublicId:guid}", Name = "GetRecognitionById")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileRecognitionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(Summary = "Get a recognition by id")]
    public async Task<ActionResult<PersonnelFileRecognitionResponse>> GetRecognitionById(
        Guid publicId,
        Guid recognitionPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileRecognitionByIdQuery(publicId, recognitionPublicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/recognitions")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileRecognitionResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Register a recognition",
        Description = """
            Registers a recognition (EN_REVISION) with no personnel-action entry yet. HR-only
            (`ManageRecognitions`). The event date must be ≤ today; an informational amount, when it travels,
            must be positive and carry its currency (RN-17). The type is validated against the active
            company-managed master.
            """)]
    public async Task<ActionResult<PersonnelFileRecognitionResponse>> AddRecognition(
        Guid publicId,
        [FromBody] AddRecognitionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileRecognitionCommand(publicId, ToInput(request)), cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetRecognitionById),
            value => new { publicId, recognitionPublicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/recognitions/{recognitionPublicId:guid}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileRecognitionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Edit a recognition's declarative fields",
        Description = """
            Updates the type, event date, detail, amount and notes of an EN_REVISION recognition (RN-01,
            manager-only). Requires the `If-Match` header with the current `concurrencyToken`; the new token is
            returned in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileRecognitionResponse>> UpdateRecognition(
        Guid publicId,
        Guid recognitionPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateRecognitionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileRecognitionCommand(publicId, recognitionPublicId, ToInput(request), concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/recognitions/{recognitionPublicId:guid}/decision")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileRecognitionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Decide a recognition (APLICAR / RECHAZAR)",
        Description = """
            The single decision (RN-01/RN-02). `APLICAR` moves EN_REVISION → APLICADA and journals a
            `RECONOCIMIENTO` personnel action in the same transaction; `RECHAZAR` moves EN_REVISION → RECHAZADA
            and requires a `note`. Requires the dedicated `AuthorizeRecognitions` grant (Admin excluded); the
            subject employee and the registrar cannot decide (double anti-self, 403). Requires the `If-Match`
            header.
            """)]
    public async Task<ActionResult<PersonnelFileRecognitionResponse>> DecideRecognition(
        Guid publicId,
        Guid recognitionPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] RecognitionDecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DecidePersonnelFileRecognitionCommand(publicId, recognitionPublicId, request.Decision, request.Note, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/recognitions/{recognitionPublicId:guid}/annulment")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileRecognitionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Annul or revoke a recognition",
        Description = """
            Annuls an EN_REVISION recognition (trámite withdrawal — `ManageRecognitions`) or revokes an APLICADA
            one (revocation — the dedicated `AuthorizeRecognitions` grant + double anti-self; annuls the linked
            `RECONOCIMIENTO` entry in the same transaction). The reason is mandatory. Requires the `If-Match`
            header.
            """)]
    public async Task<ActionResult<PersonnelFileRecognitionResponse>> AnnulRecognition(
        Guid publicId,
        Guid recognitionPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] RecognitionAnnulmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AnnulPersonnelFileRecognitionCommand(publicId, recognitionPublicId, request.Reason, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    private static RecognitionInput ToInput(AddRecognitionRequest request) =>
        new(
            request.RecognitionTypePublicId,
            request.EventDate,
            request.Detail,
            request.Amount,
            request.CurrencyCode,
            request.AssignedPositionPublicId,
            request.Notes);

    private static RecognitionInput ToInput(UpdateRecognitionRequest request) =>
        new(
            request.RecognitionTypePublicId,
            request.EventDate,
            request.Detail,
            request.Amount,
            request.CurrencyCode,
            request.AssignedPositionPublicId,
            request.Notes);
}

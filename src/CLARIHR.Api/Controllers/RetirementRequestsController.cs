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
/// Definitive-retirement requests ("retiro definitivo") — register/read/edit/cancel/execute. Kept in a
/// dedicated controller under the retirement policy set (<see cref="PersonnelFilePolicies.ViewRetirements"/> /
/// <see cref="PersonnelFilePolicies.ManageRetirements"/>). Authorization/rejection and the annulment of an
/// AUTORIZADA live in <c>RetirementRequestResolutionController</c> (AuthorizeRetirement policy) and the
/// reversal in <c>RetirementRequestReversalController</c> (RevertRetirement policy) — the policy set is
/// class-level, so each distinct write permission gets its own controller (D-12/D-13).
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewRetirements, PersonnelFilePolicies.ManageRetirements)]
public sealed class RetirementRequestsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/personnel-files/{publicId:guid}/retirement-requests")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileRetirementRequestResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's retirement requests",
        Description = """
            Returns the retirement requests of the specified personnel file (RRHH-only — `ViewRetirements`,
            D-12). Each item carries its own `concurrencyToken` for the `If-Match` header of subsequent writes.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileRetirementRequestResponse>>> GetRetirementRequests(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileRetirementRequestsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/retirement-requests/{retirementRequestPublicId:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileRetirementRequestResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a retirement request by id",
        Description = """
            Returns a single retirement request with its full timeline (requested/resolved/canceled/executed/
            reverted — actor, date and notes of each transition). The `concurrencyToken` in the response is
            required in the `If-Match` header of subsequent writes.
            """)]
    public async Task<ActionResult<PersonnelFileRetirementRequestResponse>> GetRetirementRequestById(
        Guid publicId,
        Guid retirementRequestPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileRetirementRequestByIdQuery(publicId, retirementRequestPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/retirement-requests")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileRetirementRequestResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Register a retirement request for a personnel file",
        Description = """
            Registers the definitive-retirement request (RF-001): requester (a personnel-file reference — it may
            be the employee themself), request date, retirement date, retirement category + reason (hierarchical
            catalogs) and an optional note. HR-only (`ManageRetirements`, D-03). The request starts in status
            `SOLICITADA`; at most ONE open request per employee (RN-001.2). The `ETag` header carries the initial
            `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFileRetirementRequestResponse>> AddRetirementRequest(
        Guid publicId,
        [FromBody] AddRetirementRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileRetirementRequestCommand(publicId, ToInput(request.RequesterFilePublicId, request.RequestDate, request.RetirementDate, request.RetirementCategoryCode, request.RetirementReasonCode, request.Notes)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetRetirementRequestById),
            value => new { publicId, retirementRequestPublicId = value.RetirementRequestPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/retirement-requests/{retirementRequestPublicId:guid}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileRetirementRequestResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace a retirement request's business fields",
        Description = """
            Replaces the business fields (requester, dates, category/reason, note) of a request — only while it
            is `SOLICITADA` (RN-003.1; an `AUTORIZADA` is annulled and re-registered). HR-only. Requires the
            `If-Match` header with the current `concurrencyToken`; the new token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileRetirementRequestResponse>> UpdateRetirementRequest(
        Guid publicId,
        Guid retirementRequestPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateRetirementRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileRetirementRequestCommand(
                publicId,
                retirementRequestPublicId,
                ToInput(request.RequesterFilePublicId, request.RequestDate, request.RetirementDate, request.RetirementCategoryCode, request.RetirementReasonCode, request.Notes),
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/retirement-requests/{retirementRequestPublicId:guid}/cancel")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileRetirementRequestResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Annul a SOLICITADA retirement request",
        Description = """
            Annuls (terminal `ANULADA`) a request that is still `SOLICITADA` (RN-005.1 — manager). Annulling an
            `AUTORIZADA` is an authorizer action: use `PATCH …/annulment` instead; an `EJECUTADA` is never
            annulled — it is reverted. Requires the `If-Match` header with the current `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFileRetirementRequestResponse>> CancelRetirementRequest(
        Guid publicId,
        Guid retirementRequestPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] CancelRetirementRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CancelRetirementRequestCommand(publicId, retirementRequestPublicId, request.Notes, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/retirement-requests/{retirementRequestPublicId:guid}/execution")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileRetirementRequestResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Execute an AUTHORIZED retirement (orchestrated baja)",
        Description = """
            Consumes the baja in ONE transaction (RF-006): stamps the profile (retirement metadata +
            `RETIRADO`), deactivates the personnel file (optionally blocking rehire — D-18), closes all active
            plaza assignments and contracts at the retirement date, deactivates the linked login and revokes its
            sessions (D-06), journals the `BAJA` personnel action and moves the request to `EJECUTADA`, capturing
            the reversal snapshot (D-11). Manual action, only when the retirement date has arrived (`FechaRetiro ≤
            hoy` UTC — D-05; retroactive bajas allowed). Guards: subject ≠ executor (403), profile divergence,
            rows starting after the retirement date, and the company's LAST ACTIVE ADMIN cannot be retired until
            the administration is transferred (422). Requires `If-Match` with the request's `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFileRetirementRequestResponse>> ExecuteRetirementRequest(
        Guid publicId,
        Guid retirementRequestPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] ExecuteRetirementRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ExecuteRetirementRequestCommand(publicId, retirementRequestPublicId, request.BlockRehire, request.RehireBlockReason, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    private static RetirementRequestInput ToInput(
        Guid requesterFilePublicId,
        DateTime requestDate,
        DateTime retirementDate,
        string retirementCategoryCode,
        string retirementReasonCode,
        string? notes) =>
        new(requesterFilePublicId, requestDate, retirementDate, retirementCategoryCode, retirementReasonCode, notes);
}

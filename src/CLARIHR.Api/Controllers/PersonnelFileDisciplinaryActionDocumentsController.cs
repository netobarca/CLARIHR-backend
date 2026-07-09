using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Application.Features.PersonnelFiles.PersonnelTransactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

/// <summary>
/// Supporting documents (acta / descargo) of an employee disciplinary action (REQ-003 D-12/RF-008). Same policy
/// set as the disciplinary action itself; the manager-only writes and the View-OR-self-APLICADA read rule (D-13)
/// are enforced by the disciplinary-action handler gates.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewDisciplinaryActions, PersonnelFilePolicies.ManageDisciplinaryActions)]
public sealed class PersonnelFileDisciplinaryActionDocumentsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/personnel-files/{publicId:guid}/disciplinary-actions/{disciplinaryActionPublicId:guid}/documents")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<DisciplinaryActionDocumentResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(Summary = "List the supporting documents of a disciplinary action")]
    public async Task<ActionResult<IReadOnlyCollection<DisciplinaryActionDocumentResponse>>> GetDisciplinaryActionDocuments(
        Guid publicId,
        Guid disciplinaryActionPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetDisciplinaryActionDocumentsQuery(publicId, disciplinaryActionPublicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/disciplinary-actions/{disciplinaryActionPublicId:guid}/documents/{documentPublicId:guid}", Name = "GetDisciplinaryActionDocumentById")]
    [Produces("application/json")]
    [ProducesResponseType<DisciplinaryActionDocumentResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(Summary = "Get a disciplinary action document by id")]
    public async Task<ActionResult<DisciplinaryActionDocumentResponse>> GetDisciplinaryActionDocumentById(
        Guid publicId,
        Guid disciplinaryActionPublicId,
        Guid documentPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetDisciplinaryActionDocumentByIdQuery(publicId, disciplinaryActionPublicId, documentPublicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/disciplinary-actions/{disciplinaryActionPublicId:guid}/documents/{documentPublicId:guid}/read-url")]
    [Produces("application/json")]
    [ProducesResponseType<GetDisciplinaryActionDocumentReadUrlResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a time-limited download URL for a disciplinary action document",
        Description = """
            Authorizes the disciplinary action (same rule as reading it) and returns a short-lived, pre-signed
            (SAS) URL to download the attachment's binary directly from blob storage.
            """)]
    public async Task<ActionResult<GetDisciplinaryActionDocumentReadUrlResponse>> GetDisciplinaryActionDocumentReadUrl(
        Guid publicId,
        Guid disciplinaryActionPublicId,
        Guid documentPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetDisciplinaryActionDocumentReadUrlQuery(publicId, disciplinaryActionPublicId, documentPublicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/disciplinary-actions/{disciplinaryActionPublicId:guid}/documents")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<DisciplinaryActionDocumentResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Attach a supporting document to a disciplinary action",
        Description = """
            Links an already-uploaded file (created via the two-step `POST /files/upload-session` →
            `PATCH /files/{id}/complete` flow, with `purpose = DisciplinaryActionDocument`) to the disciplinary
            action. Returns the document metadata with its `ETag`. Manager-only (D-05).
            """)]
    public async Task<ActionResult<DisciplinaryActionDocumentResponse>> AddDisciplinaryActionDocument(
        Guid publicId,
        Guid disciplinaryActionPublicId,
        [FromBody] AddDisciplinaryActionDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddDisciplinaryActionDocumentCommand(
                publicId,
                disciplinaryActionPublicId,
                request.FilePublicId,
                request.DocumentTypeCatalogItemPublicId,
                request.Observations),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetDisciplinaryActionDocumentById),
            value => new { publicId, disciplinaryActionPublicId, documentPublicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/disciplinary-actions/{disciplinaryActionPublicId:guid}/documents/{documentPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove a supporting document from a disciplinary action",
        Description = """
            Soft-deletes the attachment and marks its backing blob for cleanup. Requires the `If-Match` header
            with the document's current `concurrencyToken`. Manager-only (D-05).
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteDisciplinaryActionDocument(
        Guid publicId,
        Guid disciplinaryActionPublicId,
        Guid documentPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeleteDisciplinaryActionDocumentCommand(publicId, disciplinaryActionPublicId, documentPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }
}

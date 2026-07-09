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
/// Supporting documents (diploma / memo) of an employee recognition (REQ-003 D-12/RF-005). Same policy set as
/// the recognition itself; the manager-only writes and the View-OR-self-APLICADA read rule (D-13) are enforced
/// by the recognition handler gates.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewRecognitions, PersonnelFilePolicies.ManageRecognitions)]
public sealed class PersonnelFileRecognitionDocumentsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/personnel-files/{publicId:guid}/recognitions/{recognitionPublicId:guid}/documents")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<RecognitionDocumentResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(Summary = "List the supporting documents of a recognition")]
    public async Task<ActionResult<IReadOnlyCollection<RecognitionDocumentResponse>>> GetRecognitionDocuments(
        Guid publicId,
        Guid recognitionPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetRecognitionDocumentsQuery(publicId, recognitionPublicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/recognitions/{recognitionPublicId:guid}/documents/{documentPublicId:guid}", Name = "GetRecognitionDocumentById")]
    [Produces("application/json")]
    [ProducesResponseType<RecognitionDocumentResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(Summary = "Get a recognition document by id")]
    public async Task<ActionResult<RecognitionDocumentResponse>> GetRecognitionDocumentById(
        Guid publicId,
        Guid recognitionPublicId,
        Guid documentPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetRecognitionDocumentByIdQuery(publicId, recognitionPublicId, documentPublicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/recognitions/{recognitionPublicId:guid}/documents/{documentPublicId:guid}/read-url")]
    [Produces("application/json")]
    [ProducesResponseType<GetRecognitionDocumentReadUrlResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a time-limited download URL for a recognition document",
        Description = """
            Authorizes the recognition (same rule as reading it) and returns a short-lived, pre-signed (SAS) URL
            to download the attachment's binary directly from blob storage.
            """)]
    public async Task<ActionResult<GetRecognitionDocumentReadUrlResponse>> GetRecognitionDocumentReadUrl(
        Guid publicId,
        Guid recognitionPublicId,
        Guid documentPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetRecognitionDocumentReadUrlQuery(publicId, recognitionPublicId, documentPublicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/recognitions/{recognitionPublicId:guid}/documents")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<RecognitionDocumentResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Attach a supporting document to a recognition",
        Description = """
            Links an already-uploaded file (created via the two-step `POST /files/upload-session` →
            `PATCH /files/{id}/complete` flow, with `purpose = RecognitionDocument`) to the recognition. Returns
            the document metadata with its `ETag`. Manager-only (D-05).
            """)]
    public async Task<ActionResult<RecognitionDocumentResponse>> AddRecognitionDocument(
        Guid publicId,
        Guid recognitionPublicId,
        [FromBody] AddRecognitionDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddRecognitionDocumentCommand(
                publicId,
                recognitionPublicId,
                request.FilePublicId,
                request.DocumentTypeCatalogItemPublicId,
                request.Observations),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetRecognitionDocumentById),
            value => new { publicId, recognitionPublicId, documentPublicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/recognitions/{recognitionPublicId:guid}/documents/{documentPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove a supporting document from a recognition",
        Description = """
            Soft-deletes the attachment and marks its backing blob for cleanup. Requires the `If-Match` header
            with the document's current `concurrencyToken`. Manager-only (D-05).
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteRecognitionDocument(
        Guid publicId,
        Guid recognitionPublicId,
        Guid documentPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeleteRecognitionDocumentCommand(publicId, recognitionPublicId, documentPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }
}

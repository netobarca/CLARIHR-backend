using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.Read, PersonnelFilePolicies.Manage)]
public sealed class PersonnelFileDocumentsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    // ─── Documents ──────────────────────────────────────────────────────────────

    [HttpGet("api/v1/personnel-files/{publicId:guid}/documents")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's documents",
        Description = """
            Returns the metadata of every document attached to the specified personnel file. Each
            item carries its own `concurrencyToken`, required in the `If-Match` header of subsequent
            `PUT`/`PATCH`/`DELETE` requests, plus `filePublicId` to fetch the binary via
            `GET /api/v1/files/{filePublicId}/read-url`.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>> GetDocuments(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileDocumentsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/documents/{documentPublicId:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileDocumentMetadataResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file document by id",
        Description = """
            Returns a single document's metadata. The `concurrencyToken` in the response is required
            in the `If-Match` header of subsequent `PUT`/`PATCH`/`DELETE` requests, and `filePublicId`
            is used with `GET /api/v1/files/{filePublicId}/read-url` to download the file.
            """)]
    public async Task<ActionResult<PersonnelFileDocumentMetadataResponse>> GetDocument(
        Guid publicId,
        Guid documentPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileDocumentByIdQuery(publicId, documentPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/documents")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileDocumentMetadataResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Attach a document to a personnel file",
        Description = """
            Links a previously uploaded file to the personnel file as a typed document. Upload the
            file first through the Files API (`POST /api/v1/files/upload-session` with
            `purpose=PersonnelDocument`, then `PATCH /api/v1/files/{filePublicId}/complete`) and pass
            the resulting `filePublicId`. Returns `201 Created` with the `Location` header pointing to
            the document and the `ETag` header carrying its initial `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFileDocumentMetadataResponse>> AddDocument(
        Guid publicId,
        [FromBody] AddPersonnelFileDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileDocumentCommand(
                publicId,
                request.FilePublicId,
                request.DocumentTypeCatalogItemPublicId,
                request.Observations),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetDocument),
            value => new { publicId, documentPublicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/documents/{documentPublicId:guid}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileDocumentMetadataResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Update a personnel file document",
        Description = """
            Replaces the document's metadata (type and observations) and, optionally, the underlying
            file when `filePublicId` is supplied (the new file must be an `Active` file with
            `purpose=PersonnelDocument`). Requires the `If-Match` header with the current
            `concurrencyToken`; the new token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileDocumentMetadataResponse>> UpdateDocument(
        Guid publicId,
        Guid documentPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdatePersonnelFileDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileDocumentCommand(
                publicId,
                documentPublicId,
                request.DocumentTypeCatalogItemPublicId,
                request.Observations,
                request.FilePublicId,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/documents/{documentPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [Produces("application/json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<PersonnelFileDocumentMetadataResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a personnel file document",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type `application/json-patch+json`) to an
            existing document's metadata. Mutable members are `documentTypeCatalogItemPublicId` and
            `observations`; the file content is replaced via `PUT`. Requires the `If-Match` header
            with the current `concurrencyToken`; the new token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileDocumentMetadataResponse>> PatchDocument(
        Guid publicId,
        Guid documentPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchDocumentRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchPersonnelFileDocumentCommand(
                publicId,
                documentPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new PersonnelFileDocumentPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/documents/{documentPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove a document from a personnel file",
        Description = """
            Soft-deletes the specified document (it is deactivated for retention) and soft-deletes its
            backing file so the storage cleanup job removes the blob. Requires the `If-Match` header
            with the current `concurrencyToken`. Returns the parent personnel file's refreshed
            concurrency token so the caller can keep mutating without an extra round-trip.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteDocument(
        Guid publicId,
        Guid documentPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileDocumentCommand(publicId, documentPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    // ─── Observations ────────────────────────────────────────────────────────────
    // Append-only log: list + create only (no update/delete by design).

    [HttpGet("api/v1/personnel-files/{publicId:guid}/observations")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileObservationResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's observations",
        Description = "Returns the append-only observation log recorded for the specified personnel file, newest first.")]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileObservationResponse>>> GetObservations(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileObservationsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/observations")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileObservationResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Add an observation to a personnel file",
        Description = """
            Appends an observation (attributed to the current user) to the personnel file's
            observation log. Observations are immutable once created and have no concurrency token.
            """)]
    public async Task<ActionResult<PersonnelFileObservationResponse>> AddObservation(
        Guid publicId,
        [FromBody] AddObservationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileObservationCommand(publicId, request.Note),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<PersonnelFileObservationResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }
}

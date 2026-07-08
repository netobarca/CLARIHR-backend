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
/// Supporting documents ("constancias") of an incapacity. Same authn-only policy set and permission-or-self
/// access rule as the incapacity itself (health data); the read/write decision is enforced by the handler gates.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewIncapacities, PersonnelFilePolicies.ManageIncapacities)]
public sealed class PersonnelFileIncapacityDocumentsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/personnel-files/{publicId:guid}/incapacities/{incapacityPublicId:guid}/documents")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<IncapacityDocumentResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(Summary = "List the supporting documents of an incapacity")]
    public async Task<ActionResult<IReadOnlyCollection<IncapacityDocumentResponse>>> GetIncapacityDocuments(
        Guid publicId,
        Guid incapacityPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetIncapacityDocumentsQuery(publicId, incapacityPublicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/incapacities/{incapacityPublicId:guid}/documents/{documentPublicId:guid}", Name = "GetIncapacityDocumentById")]
    [Produces("application/json")]
    [ProducesResponseType<IncapacityDocumentResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(Summary = "Get an incapacity document by id")]
    public async Task<ActionResult<IncapacityDocumentResponse>> GetIncapacityDocumentById(
        Guid publicId,
        Guid incapacityPublicId,
        Guid documentPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetIncapacityDocumentByIdQuery(publicId, incapacityPublicId, documentPublicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/incapacities/{incapacityPublicId:guid}/documents/{documentPublicId:guid}/read-url")]
    [Produces("application/json")]
    [ProducesResponseType<GetIncapacityDocumentReadUrlResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a time-limited download URL for an incapacity document",
        Description = """
            Authorizes the incapacity (same rule as reading it) and returns a short-lived, pre-signed (SAS) URL
            to download the attachment's binary directly from blob storage.
            """)]
    public async Task<ActionResult<GetIncapacityDocumentReadUrlResponse>> GetIncapacityDocumentReadUrl(
        Guid publicId,
        Guid incapacityPublicId,
        Guid documentPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetIncapacityDocumentReadUrlQuery(publicId, incapacityPublicId, documentPublicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/incapacities/{incapacityPublicId:guid}/documents")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<IncapacityDocumentResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Attach a supporting document to an incapacity",
        Description = """
            Links an already-uploaded file (two-step `POST /files/upload-session` → `PATCH /files/{id}/complete`
            flow, with `purpose = IncapacityDocument`) to the incapacity. The employee may attach documents to
            their own incapacities (D-18).
            """)]
    public async Task<ActionResult<IncapacityDocumentResponse>> AddIncapacityDocument(
        Guid publicId,
        Guid incapacityPublicId,
        [FromBody] AddIncapacityDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddIncapacityDocumentCommand(
                publicId,
                incapacityPublicId,
                request.FilePublicId,
                request.DocumentTypeCatalogItemPublicId,
                request.Observations),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetIncapacityDocumentById),
            value => new { publicId, incapacityPublicId, documentPublicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/incapacities/{incapacityPublicId:guid}/documents/{documentPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove a supporting document from an incapacity",
        Description = """
            Soft-deletes the attachment and marks its backing blob for cleanup. Requires the `If-Match` header
            with the document's current `concurrencyToken`. Manager-only.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteIncapacityDocument(
        Guid publicId,
        Guid incapacityPublicId,
        Guid documentPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeleteIncapacityDocumentCommand(publicId, incapacityPublicId, documentPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }
}

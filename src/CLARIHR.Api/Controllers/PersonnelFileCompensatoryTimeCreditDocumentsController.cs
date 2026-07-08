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
/// Authorization documents of a compensatory-time credit (D-20/RF-012). Same authn-only policy set as the credit
/// itself; the read decision (View OR self) and the HR-only writes (D-01) are enforced by the handler gates.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewCompensatoryTime, PersonnelFilePolicies.ManageCompensatoryTime)]
public sealed class PersonnelFileCompensatoryTimeCreditDocumentsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/personnel-files/{publicId:guid}/compensatory-time-credits/{creditPublicId:guid}/documents")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<CompensatoryTimeCreditDocumentResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(Summary = "List the authorization documents of a compensatory-time credit")]
    public async Task<ActionResult<IReadOnlyCollection<CompensatoryTimeCreditDocumentResponse>>> GetCreditDocuments(
        Guid publicId,
        Guid creditPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetCompensatoryTimeCreditDocumentsQuery(publicId, creditPublicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/compensatory-time-credits/{creditPublicId:guid}/documents/{documentPublicId:guid}", Name = "GetCompensatoryTimeCreditDocumentById")]
    [Produces("application/json")]
    [ProducesResponseType<CompensatoryTimeCreditDocumentResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(Summary = "Get a compensatory-time credit document by id")]
    public async Task<ActionResult<CompensatoryTimeCreditDocumentResponse>> GetCreditDocumentById(
        Guid publicId,
        Guid creditPublicId,
        Guid documentPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetCompensatoryTimeCreditDocumentByIdQuery(publicId, creditPublicId, documentPublicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/compensatory-time-credits/{creditPublicId:guid}/documents/{documentPublicId:guid}/read-url")]
    [Produces("application/json")]
    [ProducesResponseType<GetCompensatoryTimeCreditDocumentReadUrlResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a time-limited download URL for a compensatory-time credit document",
        Description = """
            Authorizes the credit (same rule as reading it) and returns a short-lived, pre-signed (SAS) URL to
            download the attachment's binary directly from blob storage.
            """)]
    public async Task<ActionResult<GetCompensatoryTimeCreditDocumentReadUrlResponse>> GetCreditDocumentReadUrl(
        Guid publicId,
        Guid creditPublicId,
        Guid documentPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetCompensatoryTimeCreditDocumentReadUrlQuery(publicId, creditPublicId, documentPublicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/compensatory-time-credits/{creditPublicId:guid}/documents")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<CompensatoryTimeCreditDocumentResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Attach an authorization document to a compensatory-time credit",
        Description = """
            Links an already-uploaded file (two-step `POST /files/upload-session` → `PATCH /files/{id}/complete`
            flow, with `purpose = CompensatoryTimeDocument`) to the credit. HR-only.
            """)]
    public async Task<ActionResult<CompensatoryTimeCreditDocumentResponse>> AddCreditDocument(
        Guid publicId,
        Guid creditPublicId,
        [FromBody] AddCompensatoryTimeCreditDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddCompensatoryTimeCreditDocumentCommand(
                publicId,
                creditPublicId,
                request.FilePublicId,
                request.DocumentTypeCatalogItemPublicId,
                request.Observations),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetCreditDocumentById),
            value => new { publicId, creditPublicId, documentPublicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/compensatory-time-credits/{creditPublicId:guid}/documents/{documentPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove an authorization document from a compensatory-time credit",
        Description = """
            Soft-deletes the attachment and marks its backing blob for cleanup. Requires the `If-Match` header
            with the document's current `concurrencyToken`. HR-only.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteCreditDocument(
        Guid publicId,
        Guid creditPublicId,
        Guid documentPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeleteCompensatoryTimeCreditDocumentCommand(publicId, creditPublicId, documentPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }
}

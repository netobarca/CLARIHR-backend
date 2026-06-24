using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

/// <summary>
/// Medical-insurance claims (diagnosis = special-category health data). Kept in a dedicated controller so it
/// runs under its own authn-only policy set (<see cref="PersonnelFilePolicies.ViewMedicalClaims"/> /
/// <see cref="PersonnelFilePolicies.ManageMedicalClaims"/>); the precise permission-or-self decision (D-08/D-09)
/// is enforced by the medical-claim handler gates, which a declarative policy cannot express.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewMedicalClaims, PersonnelFilePolicies.ManageMedicalClaims)]
public sealed class MedicalClaimsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/personnel-files/{publicId:guid}/medical-claims")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's medical claims",
        Description = """
            Returns every medical claim recorded for the specified personnel file. Each item carries its own
            `concurrencyToken`, required in the `If-Match` header of subsequent `PUT`/`PATCH`/`DELETE` requests.
            Access requires the `ViewMedicalClaims` permission or the employee reading their own claims (D-08/D-09).
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>> GetMedicalClaims(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileMedicalClaimsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/medical-claims/{medicalClaimPublicId:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileMedicalClaimResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file medical claim by id",
        Description = """
            Returns a single medical claim of the specified personnel file. The `concurrencyToken` in the
            response is required in the `If-Match` header of subsequent `PUT`/`PATCH`/`DELETE` requests.
            """)]
    public async Task<ActionResult<PersonnelFileMedicalClaimResponse>> GetMedicalClaimById(
        Guid publicId,
        Guid medicalClaimPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileMedicalClaimByIdQuery(publicId, medicalClaimPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/medical-claims")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileMedicalClaimResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Add a medical claim to a personnel file",
        Description = """
            Creates a new medical claim under the specified personnel file and returns it with a `201 Created`
            response. The `Location` header points to the created resource and the `ETag` header carries its
            initial `concurrencyToken`. The employee may register their own claims (self-service, D-09).
            """)]
    public async Task<ActionResult<PersonnelFileMedicalClaimResponse>> AddMedicalClaim(
        Guid publicId,
        [FromBody] AddMedicalClaimRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileMedicalClaimCommand(publicId, ToInput(request)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetMedicalClaimById),
            value => new { publicId, medicalClaimPublicId = value.MedicalClaimPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/medical-claims/{medicalClaimPublicId:guid}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileMedicalClaimResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace a personnel file medical claim",
        Description = """
            Replaces the business fields of an existing medical claim. The active state is preserved (it is
            mutated exclusively via `PATCH`). Requires the `If-Match` header with the current `concurrencyToken`;
            the new token is returned in the `ETag` header. Edits are manager-only (D-09).
            """)]
    public async Task<ActionResult<PersonnelFileMedicalClaimResponse>> UpdateMedicalClaim(
        Guid publicId,
        Guid medicalClaimPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateMedicalClaimRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileMedicalClaimCommand(publicId, medicalClaimPublicId, ToInput(request), concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/medical-claims/{medicalClaimPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [Produces("application/json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<PersonnelFileMedicalClaimResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a personnel file medical claim",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type `application/json-patch+json`) to an existing
            medical claim. Supports the business fields and the `isActive` flag. Requires the `If-Match` header
            with the current `concurrencyToken`; the new token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileMedicalClaimResponse>> PatchMedicalClaim(
        Guid publicId,
        Guid medicalClaimPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchMedicalClaimRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchPersonnelFileMedicalClaimCommand(
                publicId,
                medicalClaimPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new PersonnelFileMedicalClaimPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/medical-claims/{medicalClaimPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove a medical claim from a personnel file",
        Description = """
            Deletes the specified medical claim. Requires the `If-Match` header with the current
            `concurrencyToken`. Returns the parent personnel file's refreshed concurrency token. Manager-only (D-09).
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteMedicalClaim(
        Guid publicId,
        Guid medicalClaimPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileMedicalClaimCommand(publicId, medicalClaimPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    // ─── Medical Claim Documents (attachments) ────────────────────────────────

    [HttpGet("api/v1/personnel-files/{publicId:guid}/medical-claims/{medicalClaimPublicId:guid}/documents")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<MedicalClaimDocumentResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List the supporting documents of a medical claim",
        Description = """
            Returns the active supporting documents (invoice, prescription, EOB, medical report…) attached to
            the claim. Documents carry health data and are protected by the same access rule as the claim
            (`ViewMedicalClaims` permission or the owner employee). Use the per-document `read-url` endpoint to
            download the binary.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<MedicalClaimDocumentResponse>>> GetMedicalClaimDocuments(
        Guid publicId,
        Guid medicalClaimPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetMedicalClaimDocumentsQuery(publicId, medicalClaimPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/medical-claims/{medicalClaimPublicId:guid}/documents/{documentPublicId:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<MedicalClaimDocumentResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(Summary = "Get a medical claim document by id")]
    public async Task<ActionResult<MedicalClaimDocumentResponse>> GetMedicalClaimDocumentById(
        Guid publicId,
        Guid medicalClaimPublicId,
        Guid documentPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetMedicalClaimDocumentByIdQuery(publicId, medicalClaimPublicId, documentPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/medical-claims/{medicalClaimPublicId:guid}/documents/{documentPublicId:guid}/read-url")]
    [Produces("application/json")]
    [ProducesResponseType<GetMedicalClaimDocumentReadUrlResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a time-limited download URL for a medical claim document",
        Description = """
            Authorizes the medical claim (same rule as reading it) and returns a short-lived, pre-signed
            (SAS) URL to download the attachment's binary directly from blob storage.
            """)]
    public async Task<ActionResult<GetMedicalClaimDocumentReadUrlResponse>> GetMedicalClaimDocumentReadUrl(
        Guid publicId,
        Guid medicalClaimPublicId,
        Guid documentPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetMedicalClaimDocumentReadUrlQuery(publicId, medicalClaimPublicId, documentPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/medical-claims/{medicalClaimPublicId:guid}/documents")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<MedicalClaimDocumentResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Attach a supporting document to a medical claim",
        Description = """
            Links an already-uploaded file (created via the two-step `POST /files/upload-session` →
            `PATCH /files/{id}/complete` flow, with `purpose = MedicalClaimDocument`) to the claim. Returns the
            document metadata with its `ETag`. The employee may attach documents to their own claims (D-09).
            """)]
    public async Task<ActionResult<MedicalClaimDocumentResponse>> AddMedicalClaimDocument(
        Guid publicId,
        Guid medicalClaimPublicId,
        [FromBody] AddMedicalClaimDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddMedicalClaimDocumentCommand(
                publicId,
                medicalClaimPublicId,
                request.FilePublicId,
                request.DocumentTypeCatalogItemPublicId,
                request.Observations),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetMedicalClaimDocumentById),
            value => new { publicId, medicalClaimPublicId, documentPublicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/medical-claims/{medicalClaimPublicId:guid}/documents/{documentPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove a supporting document from a medical claim",
        Description = """
            Soft-deletes the attachment and marks its backing blob for cleanup. Requires the `If-Match` header
            with the document's current `concurrencyToken`. Manager-only (D-09).
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteMedicalClaimDocument(
        Guid publicId,
        Guid medicalClaimPublicId,
        Guid documentPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeleteMedicalClaimDocumentCommand(publicId, medicalClaimPublicId, documentPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    private static MedicalClaimInput ToInput(AddMedicalClaimRequest request) =>
        new(
            request.InsurancePublicId,
            request.AccountNumber,
            request.ClaimantType,
            request.BeneficiaryPublicId,
            request.ClaimTypeCode,
            request.Diagnosis,
            request.ClaimAmount,
            request.CurrencyCode,
            request.PaidAmount,
            request.Notes,
            request.ClaimDateUtc,
            request.ResolutionDateUtc,
            request.ClaimStatusCode,
            request.SourceSystem,
            request.SourceReference,
            request.SourceSyncedUtc);

    private static MedicalClaimInput ToInput(UpdateMedicalClaimRequest request) =>
        new(
            request.InsurancePublicId,
            request.AccountNumber,
            request.ClaimantType,
            request.BeneficiaryPublicId,
            request.ClaimTypeCode,
            request.Diagnosis,
            request.ClaimAmount,
            request.CurrencyCode,
            request.PaidAmount,
            request.Notes,
            request.ClaimDateUtc,
            request.ResolutionDateUtc,
            request.ClaimStatusCode,
            request.SourceSystem,
            request.SourceReference,
            request.SourceSyncedUtc);
}

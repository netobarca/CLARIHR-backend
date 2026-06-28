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
/// Employee certificate requests ("constancias": salario / laboral / embajada / …). Kept in a dedicated
/// controller so it runs under its own authn-only policy set
/// (<see cref="PersonnelFilePolicies.ViewCertificateRequests"/> /
/// <see cref="PersonnelFilePolicies.ManageCertificateRequests"/>); the precise decisions — self-service
/// create/read/cancel for the employee (D-02), HR-only processing/issuance (D-04), and the salary-printing
/// ViewCompensation coupling (D-20) — are enforced by the certificate handler gates, which a declarative policy
/// cannot express.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewCertificateRequests, PersonnelFilePolicies.ManageCertificateRequests)]
public sealed class CertificateRequestsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/personnel-files/{publicId:guid}/certificate-requests")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileCertificateRequestResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's certificate requests",
        Description = """
            Returns the certificate requests of the specified personnel file. The employee may read only their OWN
            requests (self-service, D-02); HR with the `ViewCertificateRequests` permission reads all. Each item
            carries its own `concurrencyToken` for the `If-Match` header of subsequent writes.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileCertificateRequestResponse>>> GetCertificateRequests(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileCertificateRequestsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/certificate-requests/{certificateRequestPublicId:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileCertificateRequestResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file certificate request by id",
        Description = "Returns a single certificate request. The employee may read only their OWN request (D-02). The `concurrencyToken` is required in the `If-Match` header of subsequent writes.")]
    public async Task<ActionResult<PersonnelFileCertificateRequestResponse>> GetCertificateRequestById(
        Guid publicId,
        Guid certificateRequestPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileCertificateRequestByIdQuery(publicId, certificateRequestPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/certificate-requests")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileCertificateRequestResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Request a certificate for a personnel file",
        Description = """
            Creates a new certificate request and returns it with `201 Created`. Self-service: the employee may
            create a request on their OWN file (D-02); HR may create on the employee's behalf. The request starts
            in status `SOLICITADA`. An addressee ("dirigida a") is required for an embassy certificate (D-06). The
            `ETag` header carries the initial `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFileCertificateRequestResponse>> AddCertificateRequest(
        Guid publicId,
        [FromBody] AddCertificateRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileCertificateRequestCommand(publicId, ToInput(request)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetCertificateRequestById),
            value => new { publicId, certificateRequestPublicId = value.CertificateRequestPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/certificate-requests/{certificateRequestPublicId:guid}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileCertificateRequestResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace a certificate request's business fields",
        Description = """
            Replaces the business fields (type, purpose, addressee, delivery method, language, copies, dates) of an
            existing request. HR-only (D-04); status, issuance and delivery are driven by the dedicated actions.
            Requires the `If-Match` header with the current `concurrencyToken`; the new token is returned in `ETag`.
            """)]
    public async Task<ActionResult<PersonnelFileCertificateRequestResponse>> UpdateCertificateRequest(
        Guid publicId,
        Guid certificateRequestPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateCertificateRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileCertificateRequestCommand(publicId, certificateRequestPublicId, ToInput(request), concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/certificate-requests/{certificateRequestPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Deactivate (soft-delete) a certificate request",
        Description = "Soft-deletes the request (sets it inactive; no physical removal — RN-08). HR-only. Requires the `If-Match` header with the current `concurrencyToken`. Returns the parent personnel file's refreshed concurrency token.")]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteCertificateRequest(
        Guid publicId,
        Guid certificateRequestPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileCertificateRequestCommand(publicId, certificateRequestPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    // ─── HR lifecycle actions (D-04, linear) ──────────────

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/certificate-requests/{certificateRequestPublicId:guid}/processing")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileCertificateRequestResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Mark a certificate request as in process",
        Description = "Transitions `SOLICITADA` → `EN_PROCESO`. HR-only. Requires the `If-Match` header with the current `concurrencyToken`.")]
    public async Task<ActionResult<PersonnelFileCertificateRequestResponse>> ProcessCertificateRequest(
        Guid publicId,
        Guid certificateRequestPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ProcessCertificateRequestCommand(publicId, certificateRequestPublicId, concurrencyToken),
            cancellationToken);
        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/certificate-requests/{certificateRequestPublicId:guid}/issue")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileCertificateRequestResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Issue a certificate (generate the PDF) and transition it to EMITIDA",
        Description = """
            HR-only (D-04). Generates the certificate PDF server-side, stores it as the issued document, and
            transitions a pending request to `EMITIDA`, recording the issuer and date. A salary-printing type
            (constancia de salario / embajada) additionally requires the `ViewCompensation` permission (D-20) and
            available salary data (otherwise `422 CERTIFICATE_GENERATION_DATA_UNAVAILABLE`). Requires the `If-Match`
            header with the current `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFileCertificateRequestResponse>> IssueCertificateRequest(
        Guid publicId,
        Guid certificateRequestPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] IssueCertificateRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new IssueCertificateRequestCommand(publicId, certificateRequestPublicId, request.Notes, concurrencyToken),
            cancellationToken);
        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/certificate-requests/{certificateRequestPublicId:guid}/delivery")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileCertificateRequestResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Mark an issued certificate as delivered",
        Description = "Transitions `EMITIDA` → `ENTREGADA` with the delivery date (must not precede the issue date). HR-only. Requires the `If-Match` header.")]
    public async Task<ActionResult<PersonnelFileCertificateRequestResponse>> DeliverCertificateRequest(
        Guid publicId,
        Guid certificateRequestPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] DeliverCertificateRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeliverCertificateRequestCommand(publicId, certificateRequestPublicId, request.DeliveredDateUtc, concurrencyToken),
            cancellationToken);
        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/certificate-requests/{certificateRequestPublicId:guid}/reject")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileCertificateRequestResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Reject a pending certificate request",
        Description = "Transitions a pending request (`SOLICITADA` / `EN_PROCESO`) to `RECHAZADA`. HR-only. Requires the `If-Match` header.")]
    public async Task<ActionResult<PersonnelFileCertificateRequestResponse>> RejectCertificateRequest(
        Guid publicId,
        Guid certificateRequestPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] RejectCertificateRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new RejectCertificateRequestCommand(publicId, certificateRequestPublicId, request.Notes, concurrencyToken),
            cancellationToken);
        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/certificate-requests/{certificateRequestPublicId:guid}/cancel")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileCertificateRequestResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Cancel (withdraw) a pending certificate request",
        Description = "Transitions a still-pending request (`SOLICITADA` / `EN_PROCESO`) to `ANULADA`. Self-service: the employee may cancel their OWN pending request (D-02); HR may cancel any. Requires the `If-Match` header.")]
    public async Task<ActionResult<PersonnelFileCertificateRequestResponse>> CancelCertificateRequest(
        Guid publicId,
        Guid certificateRequestPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CancelCertificateRequestCommand(publicId, certificateRequestPublicId, concurrencyToken),
            cancellationToken);
        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    // ─── Documents: the issued PDF (system-generated) + manual overrides (D-05) ──────────────

    [HttpGet("api/v1/personnel-files/{publicId:guid}/certificate-requests/{certificateRequestPublicId:guid}/documents")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<CertificateRequestDocumentResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List the documents of a certificate request",
        Description = "Returns the active documents (the system-generated PDF and any manual overrides). Use the per-document `read-url` endpoint to download the binary.")]
    public async Task<ActionResult<IReadOnlyCollection<CertificateRequestDocumentResponse>>> GetCertificateRequestDocuments(
        Guid publicId,
        Guid certificateRequestPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetCertificateRequestDocumentsQuery(publicId, certificateRequestPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/certificate-requests/{certificateRequestPublicId:guid}/documents/{documentPublicId:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<CertificateRequestDocumentResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(Summary = "Get a certificate request document by id")]
    public async Task<ActionResult<CertificateRequestDocumentResponse>> GetCertificateRequestDocumentById(
        Guid publicId,
        Guid certificateRequestPublicId,
        Guid documentPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetCertificateRequestDocumentByIdQuery(publicId, certificateRequestPublicId, documentPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/certificate-requests/{certificateRequestPublicId:guid}/documents/{documentPublicId:guid}/read-url")]
    [Produces("application/json")]
    [ProducesResponseType<GetCertificateRequestDocumentReadUrlResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a time-limited download URL for a certificate document",
        Description = """
            Authorizes the certificate request (permission or owner — the employee may download their OWN issued
            certificate, D-02) and returns a short-lived, pre-signed (SAS) URL to download the document's binary
            directly from blob storage.
            """)]
    public async Task<ActionResult<GetCertificateRequestDocumentReadUrlResponse>> GetCertificateRequestDocumentReadUrl(
        Guid publicId,
        Guid certificateRequestPublicId,
        Guid documentPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetCertificateRequestDocumentReadUrlQuery(publicId, certificateRequestPublicId, documentPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/certificate-requests/{certificateRequestPublicId:guid}/documents")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<CertificateRequestDocumentResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Attach a manual override document to a certificate request",
        Description = """
            Links an already-uploaded file (created via `POST /files/upload-session` → `PATCH /files/{id}/complete`
            with `purpose = CertificateRequestDocument`) to the request as a manual override. HR-only (D-05/RN-13).
            Returns the document metadata with its `ETag`.
            """)]
    public async Task<ActionResult<CertificateRequestDocumentResponse>> AddCertificateRequestDocument(
        Guid publicId,
        Guid certificateRequestPublicId,
        [FromBody] AddCertificateRequestDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddCertificateRequestDocumentCommand(publicId, certificateRequestPublicId, request.FilePublicId, request.Observations),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetCertificateRequestDocumentById),
            value => new { publicId, certificateRequestPublicId, documentPublicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/certificate-requests/{certificateRequestPublicId:guid}/documents/{documentPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove a document from a certificate request",
        Description = "Soft-deletes the document and marks its backing blob for cleanup. HR-only. Requires the `If-Match` header with the document's current `concurrencyToken`.")]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteCertificateRequestDocument(
        Guid publicId,
        Guid certificateRequestPublicId,
        Guid documentPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeleteCertificateRequestDocumentCommand(publicId, certificateRequestPublicId, documentPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    private static CertificateRequestInput ToInput(AddCertificateRequestRequest request) =>
        new(request.TypeCode, request.PurposeCode, request.AddressedTo, request.DeliveryMethodCode, request.LanguageCode, request.Copies, request.RequestDateUtc, request.NeededByDateUtc);

    private static CertificateRequestInput ToInput(UpdateCertificateRequestRequest request) =>
        new(request.TypeCode, request.PurposeCode, request.AddressedTo, request.DeliveryMethodCode, request.LanguageCode, request.Copies, request.RequestDateUtc, request.NeededByDateUtc);
}

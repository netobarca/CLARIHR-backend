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
/// Employee economic-aid requests ("ayuda económica" — emergency assistance the employee requests and HR
/// validates). Kept in a dedicated controller so it runs under its own authn-only policy set
/// (<see cref="PersonnelFilePolicies.ViewEconomicAidRequests"/> /
/// <see cref="PersonnelFilePolicies.ManageEconomicAidRequests"/>); the precise decisions — self-service create/read
/// for the employee (D-02), HR-only validation with no self-approval (D-03), self-cancel of a pending request
/// (D-11) — are enforced by the economic-aid handler gates, which a declarative policy cannot express.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewEconomicAidRequests, PersonnelFilePolicies.ManageEconomicAidRequests)]
public sealed class EconomicAidRequestsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/personnel-files/{publicId:guid}/economic-aid-requests")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileEconomicAidRequestResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's economic-aid requests",
        Description = """
            Returns the economic-aid requests of the specified personnel file. The employee may read only their
            OWN requests (self-service, D-02); HR with the `ViewEconomicAidRequests` permission reads all. Each
            item carries its own `concurrencyToken` for the `If-Match` header of subsequent writes.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileEconomicAidRequestResponse>>> GetEconomicAidRequests(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileEconomicAidRequestsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/economic-aid-requests/{economicAidRequestPublicId:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileEconomicAidRequestResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file economic-aid request by id",
        Description = """
            Returns a single economic-aid request. The employee may read only their OWN request (D-02). The
            `concurrencyToken` in the response is required in the `If-Match` header of subsequent writes.
            """)]
    public async Task<ActionResult<PersonnelFileEconomicAidRequestResponse>> GetEconomicAidRequestById(
        Guid publicId,
        Guid economicAidRequestPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileEconomicAidRequestByIdQuery(publicId, economicAidRequestPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/economic-aid-requests")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileEconomicAidRequestResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Request economic aid for a personnel file",
        Description = """
            Creates a new economic-aid request and returns it with `201 Created`. Self-service: the employee may
            create a request on their OWN file (D-02); HR may create on the employee's behalf. The request starts
            in status `SOLICITADA`. Subject to the company's minimum-seniority eligibility (D-08). The `ETag`
            header carries the initial `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFileEconomicAidRequestResponse>> AddEconomicAidRequest(
        Guid publicId,
        [FromBody] AddEconomicAidRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileEconomicAidRequestCommand(publicId, ToInput(request)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetEconomicAidRequestById),
            value => new { publicId, economicAidRequestPublicId = value.EconomicAidRequestPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/economic-aid-requests/{economicAidRequestPublicId:guid}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileEconomicAidRequestResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace an economic-aid request's business fields",
        Description = """
            Replaces the business fields (type, description, requested amount, currency) of an existing request.
            HR-only (D-03); status, resolution and disbursement are driven by the dedicated actions. Requires the
            `If-Match` header with the current `concurrencyToken`; the new token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileEconomicAidRequestResponse>> UpdateEconomicAidRequest(
        Guid publicId,
        Guid economicAidRequestPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateEconomicAidRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileEconomicAidRequestCommand(publicId, economicAidRequestPublicId, ToInput(request), concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/economic-aid-requests/{economicAidRequestPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Deactivate (soft-delete) an economic-aid request",
        Description = """
            Soft-deletes the request (sets it inactive; no physical removal — RN-10). HR-only. Requires the
            `If-Match` header with the current `concurrencyToken`. Returns the parent personnel file's refreshed
            concurrency token.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteEconomicAidRequest(
        Guid publicId,
        Guid economicAidRequestPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileEconomicAidRequestCommand(publicId, economicAidRequestPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    // ─── Validation actions (HR), forward-compatible with a future approval flow (RF-011) ──────────────

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/economic-aid-requests/{economicAidRequestPublicId:guid}/resolution")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileEconomicAidRequestResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Validate an economic-aid request (approve / reject / request documentation / review)",
        Description = """
            HR validation (D-03): transitions a pending request to `EN_REVISION`, `PENDIENTE_DOCUMENTACION`,
            `APROBADA` or `RECHAZADA`. Approving requires `approvedAmount` &gt; 0 (partial allowed — D-05). The
            requesting employee can never validate their own request (no self-approval). Requires the `If-Match`
            header with the current `concurrencyToken`; the new token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileEconomicAidRequestResponse>> ResolveEconomicAidRequest(
        Guid publicId,
        Guid economicAidRequestPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] ResolveEconomicAidRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ResolveEconomicAidRequestCommand(
                publicId,
                economicAidRequestPublicId,
                request.TargetStatusCode,
                request.ApprovedAmount,
                request.Notes,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/economic-aid-requests/{economicAidRequestPublicId:guid}/disbursement")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileEconomicAidRequestResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Record the (informational) disbursement of an approved economic-aid request",
        Description = """
            Marks an `APROBADA` request as `DESEMBOLSADA` with the disbursement date, amount and optional payment
            method. Informational only (D-09): it does not execute a payment nor post to payroll. Requires the
            `If-Match` header with the current `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFileEconomicAidRequestResponse>> DisburseEconomicAidRequest(
        Guid publicId,
        Guid economicAidRequestPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] DisburseEconomicAidRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DisburseEconomicAidRequestCommand(
                publicId,
                economicAidRequestPublicId,
                request.DisbursedAmount,
                request.DisbursementDateUtc,
                request.PaymentMethodCode,
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/economic-aid-requests/{economicAidRequestPublicId:guid}/cancel")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileEconomicAidRequestResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Cancel (withdraw) a pending economic-aid request",
        Description = """
            Transitions a still-pending request (`SOLICITADA` / `EN_REVISION` / `PENDIENTE_DOCUMENTACION`) to
            `ANULADA`. Self-service: the employee may cancel their OWN pending request (D-11); HR may cancel any.
            Requires the `If-Match` header with the current `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFileEconomicAidRequestResponse>> CancelEconomicAidRequest(
        Guid publicId,
        Guid economicAidRequestPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CancelEconomicAidRequestCommand(publicId, economicAidRequestPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    // ─── Economic-aid request documents (supporting evidence) ──────────────

    [HttpGet("api/v1/personnel-files/{publicId:guid}/economic-aid-requests/{economicAidRequestPublicId:guid}/documents")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<EconomicAidRequestDocumentResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List the supporting documents of an economic-aid request",
        Description = "Returns the active documents attached to the request. Use the per-document `read-url` endpoint to download the binary.")]
    public async Task<ActionResult<IReadOnlyCollection<EconomicAidRequestDocumentResponse>>> GetEconomicAidRequestDocuments(
        Guid publicId,
        Guid economicAidRequestPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetEconomicAidRequestDocumentsQuery(publicId, economicAidRequestPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/economic-aid-requests/{economicAidRequestPublicId:guid}/documents/{documentPublicId:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<EconomicAidRequestDocumentResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(Summary = "Get an economic-aid request document by id")]
    public async Task<ActionResult<EconomicAidRequestDocumentResponse>> GetEconomicAidRequestDocumentById(
        Guid publicId,
        Guid economicAidRequestPublicId,
        Guid documentPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetEconomicAidRequestDocumentByIdQuery(publicId, economicAidRequestPublicId, documentPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/economic-aid-requests/{economicAidRequestPublicId:guid}/documents/{documentPublicId:guid}/read-url")]
    [Produces("application/json")]
    [ProducesResponseType<GetEconomicAidRequestDocumentReadUrlResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a time-limited download URL for an economic-aid request document",
        Description = """
            Authorizes the economic-aid request (same rule as reading it — permission or owner) and returns a
            short-lived, pre-signed (SAS) URL to download the document's binary directly from blob storage.
            """)]
    public async Task<ActionResult<GetEconomicAidRequestDocumentReadUrlResponse>> GetEconomicAidRequestDocumentReadUrl(
        Guid publicId,
        Guid economicAidRequestPublicId,
        Guid documentPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetEconomicAidRequestDocumentReadUrlQuery(publicId, economicAidRequestPublicId, documentPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/economic-aid-requests/{economicAidRequestPublicId:guid}/documents")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<EconomicAidRequestDocumentResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Attach a supporting document to an economic-aid request",
        Description = """
            Links an already-uploaded file (created via `POST /files/upload-session` → `PATCH /files/{id}/complete`
            with `purpose = EconomicAidRequestDocument`) to the request. Self-service: the employee may attach to
            their OWN request (D-06); HR may attach to any. The `documentTypeCatalogItemPublicId` classification is
            optional. Returns the document metadata with its `ETag`.
            """)]
    public async Task<ActionResult<EconomicAidRequestDocumentResponse>> AddEconomicAidRequestDocument(
        Guid publicId,
        Guid economicAidRequestPublicId,
        [FromBody] AddEconomicAidRequestDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddEconomicAidRequestDocumentCommand(
                publicId,
                economicAidRequestPublicId,
                request.FilePublicId,
                request.DocumentTypeCatalogItemPublicId,
                request.Observations),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetEconomicAidRequestDocumentById),
            value => new { publicId, economicAidRequestPublicId, documentPublicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/economic-aid-requests/{economicAidRequestPublicId:guid}/documents/{documentPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove a supporting document from an economic-aid request",
        Description = """
            Soft-deletes the attachment and marks its backing blob for cleanup. HR-only. Requires the `If-Match`
            header with the document's current `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteEconomicAidRequestDocument(
        Guid publicId,
        Guid economicAidRequestPublicId,
        Guid documentPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeleteEconomicAidRequestDocumentCommand(publicId, economicAidRequestPublicId, documentPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    private static EconomicAidRequestInput ToInput(AddEconomicAidRequestRequest request) =>
        new(request.TypeCode, request.Description, request.RequestedAmount, request.CurrencyCode, request.RequestDateUtc);

    private static EconomicAidRequestInput ToInput(UpdateEconomicAidRequestRequest request) =>
        new(request.TypeCode, request.Description, request.RequestedAmount, request.CurrencyCode, request.RequestDateUtc);
}

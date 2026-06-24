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
/// Off-payroll transactions ("transacciones fuera de nómina"): company expenses incurred on behalf of an
/// employee outside payroll (tools, PPE, uniforms, promotional items, recognitions, gifts…). Kept in a
/// dedicated controller so it runs under its own authn-only policy set
/// (<see cref="PersonnelFilePolicies.ViewOffPayrollTransactions"/> /
/// <see cref="PersonnelFilePolicies.ManageOffPayrollTransactions"/>); the precise HR-only permission decision
/// (D-06, no self-service) is enforced by the off-payroll handler gates, which a declarative policy cannot express.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewOffPayrollTransactions, PersonnelFilePolicies.ManageOffPayrollTransactions)]
public sealed class OffPayrollTransactionsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/personnel-files/{publicId:guid}/off-payroll-transactions")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileOffPayrollTransactionResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's off-payroll transactions",
        Description = """
            Returns every off-payroll transaction recorded for the specified personnel file (active and
            deactivated). Each item carries its own `concurrencyToken`, required in the `If-Match` header of
            subsequent `PUT`/`PATCH`/`DELETE` requests. Access requires the `ViewOffPayrollTransactions`
            permission (HR-only, no self-service — D-06).
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileOffPayrollTransactionResponse>>> GetOffPayrollTransactions(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileOffPayrollTransactionsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/off-payroll-transactions/totals")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<OffPayrollTransactionCurrencyTotalResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get the per-currency totals of a personnel file's off-payroll transactions",
        Description = """
            Returns the employee-level subtotals grouped by `currencyCode` (negative adjustments subtract), with a
            count per currency. There is NO currency conversion (D-08/D-13): each currency is reported
            independently. Only active transactions are included.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<OffPayrollTransactionCurrencyTotalResponse>>> GetOffPayrollTransactionTotals(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileOffPayrollTransactionTotalsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/off-payroll-transactions/{offPayrollTransactionPublicId:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileOffPayrollTransactionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file off-payroll transaction by id",
        Description = """
            Returns a single off-payroll transaction of the specified personnel file. The `concurrencyToken` in
            the response is required in the `If-Match` header of subsequent `PUT`/`PATCH`/`DELETE` requests.
            """)]
    public async Task<ActionResult<PersonnelFileOffPayrollTransactionResponse>> GetOffPayrollTransactionById(
        Guid publicId,
        Guid offPayrollTransactionPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileOffPayrollTransactionByIdQuery(publicId, offPayrollTransactionPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/off-payroll-transactions")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileOffPayrollTransactionResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Add an off-payroll transaction to a personnel file",
        Description = """
            Creates a new off-payroll transaction under the specified personnel file and returns it with a
            `201 Created` response. The `Location` header points to the created resource and the `ETag` header
            carries its initial `concurrencyToken`. HR-only (no self-service, D-06). A negative `amount`
            (adjustment) must reference the original transaction it corrects via `correctsTransactionPublicId`.
            """)]
    public async Task<ActionResult<PersonnelFileOffPayrollTransactionResponse>> AddOffPayrollTransaction(
        Guid publicId,
        [FromBody] AddOffPayrollTransactionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileOffPayrollTransactionCommand(publicId, ToInput(request)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetOffPayrollTransactionById),
            value => new { publicId, offPayrollTransactionPublicId = value.OffPayrollTransactionPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/off-payroll-transactions/{offPayrollTransactionPublicId:guid}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileOffPayrollTransactionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace a personnel file off-payroll transaction",
        Description = """
            Replaces the business fields of an existing off-payroll transaction. The active state is preserved
            (it is mutated exclusively via `PATCH`/`DELETE`). Requires the `If-Match` header with the current
            `concurrencyToken`; the new token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileOffPayrollTransactionResponse>> UpdateOffPayrollTransaction(
        Guid publicId,
        Guid offPayrollTransactionPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateOffPayrollTransactionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileOffPayrollTransactionCommand(publicId, offPayrollTransactionPublicId, ToInput(request), concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/off-payroll-transactions/{offPayrollTransactionPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [Produces("application/json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<PersonnelFileOffPayrollTransactionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a personnel file off-payroll transaction",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type `application/json-patch+json`) to an existing
            off-payroll transaction. Supports the business fields and the `isActive` flag. Requires the `If-Match`
            header with the current `concurrencyToken`; the new token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileOffPayrollTransactionResponse>> PatchOffPayrollTransaction(
        Guid publicId,
        Guid offPayrollTransactionPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchOffPayrollTransactionRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchPersonnelFileOffPayrollTransactionCommand(
                publicId,
                offPayrollTransactionPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new PersonnelFileOffPayrollTransactionPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/off-payroll-transactions/{offPayrollTransactionPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Deactivate (soft-delete) an off-payroll transaction",
        Description = """
            Soft-deletes the specified off-payroll transaction (sets it inactive; no physical removal — RN-10).
            Requires the `If-Match` header with the current `concurrencyToken`. Returns the parent personnel
            file's refreshed concurrency token.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteOffPayrollTransaction(
        Guid publicId,
        Guid offPayrollTransactionPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileOffPayrollTransactionCommand(publicId, offPayrollTransactionPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    // ─── Off-payroll transaction documents (receipts / comprobantes) ──────────────

    [HttpGet("api/v1/personnel-files/{publicId:guid}/off-payroll-transactions/{offPayrollTransactionPublicId:guid}/documents")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<OffPayrollTransactionDocumentResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List the supporting documents of an off-payroll transaction",
        Description = """
            Returns the active receipts (invoice, receipt, photo…) attached to the transaction. Use the
            per-document `read-url` endpoint to download the binary.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<OffPayrollTransactionDocumentResponse>>> GetOffPayrollTransactionDocuments(
        Guid publicId,
        Guid offPayrollTransactionPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetOffPayrollTransactionDocumentsQuery(publicId, offPayrollTransactionPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/off-payroll-transactions/{offPayrollTransactionPublicId:guid}/documents/{documentPublicId:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<OffPayrollTransactionDocumentResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(Summary = "Get an off-payroll transaction document by id")]
    public async Task<ActionResult<OffPayrollTransactionDocumentResponse>> GetOffPayrollTransactionDocumentById(
        Guid publicId,
        Guid offPayrollTransactionPublicId,
        Guid documentPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetOffPayrollTransactionDocumentByIdQuery(publicId, offPayrollTransactionPublicId, documentPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/off-payroll-transactions/{offPayrollTransactionPublicId:guid}/documents/{documentPublicId:guid}/read-url")]
    [Produces("application/json")]
    [ProducesResponseType<GetOffPayrollTransactionDocumentReadUrlResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a time-limited download URL for an off-payroll transaction document",
        Description = """
            Authorizes the off-payroll transaction (same rule as reading it) and returns a short-lived,
            pre-signed (SAS) URL to download the receipt's binary directly from blob storage.
            """)]
    public async Task<ActionResult<GetOffPayrollTransactionDocumentReadUrlResponse>> GetOffPayrollTransactionDocumentReadUrl(
        Guid publicId,
        Guid offPayrollTransactionPublicId,
        Guid documentPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetOffPayrollTransactionDocumentReadUrlQuery(publicId, offPayrollTransactionPublicId, documentPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/off-payroll-transactions/{offPayrollTransactionPublicId:guid}/documents")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<OffPayrollTransactionDocumentResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Attach a supporting document to an off-payroll transaction",
        Description = """
            Links an already-uploaded file (created via the two-step `POST /files/upload-session` →
            `PATCH /files/{id}/complete` flow, with `purpose = OffPayrollTransactionDocument`) to the transaction.
            The `documentTypeCatalogItemPublicId` classification is optional (D-07). Returns the document metadata
            with its `ETag`.
            """)]
    public async Task<ActionResult<OffPayrollTransactionDocumentResponse>> AddOffPayrollTransactionDocument(
        Guid publicId,
        Guid offPayrollTransactionPublicId,
        [FromBody] AddOffPayrollTransactionDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddOffPayrollTransactionDocumentCommand(
                publicId,
                offPayrollTransactionPublicId,
                request.FilePublicId,
                request.DocumentTypeCatalogItemPublicId,
                request.Observations),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetOffPayrollTransactionDocumentById),
            value => new { publicId, offPayrollTransactionPublicId, documentPublicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/off-payroll-transactions/{offPayrollTransactionPublicId:guid}/documents/{documentPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove a supporting document from an off-payroll transaction",
        Description = """
            Soft-deletes the attachment and marks its backing blob for cleanup. Requires the `If-Match` header
            with the document's current `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteOffPayrollTransactionDocument(
        Guid publicId,
        Guid offPayrollTransactionPublicId,
        Guid documentPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeleteOffPayrollTransactionDocumentCommand(publicId, offPayrollTransactionPublicId, documentPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    private static OffPayrollTransactionInput ToInput(AddOffPayrollTransactionRequest request) =>
        new(
            request.TransactionTypeCode,
            request.TransactionDateUtc,
            request.CurrencyCode,
            request.Amount,
            request.Year,
            request.Month,
            request.Comment,
            request.AssetAccessPublicId,
            request.CorrectsTransactionPublicId);

    private static OffPayrollTransactionInput ToInput(UpdateOffPayrollTransactionRequest request) =>
        new(
            request.TransactionTypeCode,
            request.TransactionDateUtc,
            request.CurrencyCode,
            request.Amount,
            request.Year,
            request.Month,
            request.Comment,
            request.AssetAccessPublicId,
            request.CorrectsTransactionPublicId);
}

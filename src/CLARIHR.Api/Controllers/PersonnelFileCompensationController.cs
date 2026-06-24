using System.ComponentModel.DataAnnotations;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Domain.PersonnelFiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.Read, PersonnelFilePolicies.Manage)]
public sealed class PersonnelFileCompensationController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    ReportExportDeliveryService reportExportDeliveryService) : ControllerBase
{
    // ─── Bank Accounts ────────────────────────────────────────────────────────

    [HttpGet("api/v1/personnel-files/{publicId:guid}/bank-accounts")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileBankAccountResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's bank accounts",
        Description = """
            Returns every bank account recorded for the specified personnel file. Each item
            carries its own `concurrencyToken`, required in the `If-Match` header of subsequent
            `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileBankAccountResponse>>> GetBankAccounts(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileBankAccountsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/bank-accounts/{bankAccountPublicId:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileBankAccountResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file bank account by id",
        Description = """
            Returns a single bank account of the specified personnel file. The `concurrencyToken`
            in the response is required in the `If-Match` header of subsequent
            `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<PersonnelFileBankAccountResponse>> GetBankAccountById(
        Guid publicId,
        Guid bankAccountPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileBankAccountByIdQuery(publicId, bankAccountPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/bank-accounts")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileBankAccountResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Add a bank account to a personnel file",
        Description = """
            Creates a new bank account under the specified personnel file and returns it with a
            `201 Created` response. The `Location` header points to the created resource and the
            `ETag` header carries its initial `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFileBankAccountResponse>> AddBankAccount(
        Guid publicId,
        [FromBody] AddBankAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileBankAccountCommand(
                publicId,
                new BankAccountInput(
                    request.BankPublicId,
                    request.CurrencyCode,
                    request.AccountNumber,
                    request.AccountTypeCode,
                    request.IsPrimary)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetBankAccountById),
            value => new { publicId, bankAccountPublicId = value.BankAccountPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/bank-accounts/{bankAccountPublicId:guid}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileBankAccountResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace a personnel file bank account",
        Description = """
            Replaces all fields of an existing bank account. Requires the `If-Match` header with
            the current `concurrencyToken`; the new token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileBankAccountResponse>> UpdateBankAccount(
        Guid publicId,
        Guid bankAccountPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateBankAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileBankAccountCommand(
                publicId,
                bankAccountPublicId,
                new BankAccountInput(
                    request.BankPublicId,
                    request.CurrencyCode,
                    request.AccountNumber,
                    request.AccountTypeCode,
                    request.IsPrimary),
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/bank-accounts/{bankAccountPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [Produces("application/json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<PersonnelFileBankAccountResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a personnel file bank account",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type
            `application/json-patch+json`) to an existing bank account. Requires the `If-Match`
            header with the current `concurrencyToken`; the new token is returned in the `ETag`
            header. Mutable members are the bank account input fields.
            """)]
    public async Task<ActionResult<PersonnelFileBankAccountResponse>> PatchBankAccount(
        Guid publicId,
        Guid bankAccountPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchBankAccountRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchPersonnelFileBankAccountCommand(
                publicId,
                bankAccountPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new PersonnelFileBankAccountPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/bank-accounts/{bankAccountPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove a bank account from a personnel file",
        Description = """
            Deletes the specified bank account. Requires the `If-Match` header with the current
            `concurrencyToken`. Returns the parent personnel file's refreshed concurrency token
            so the caller can keep mutating without an extra round-trip.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteBankAccount(
        Guid publicId,
        Guid bankAccountPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileBankAccountCommand(publicId, bankAccountPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    // ─── Additional Benefits ──────────────────────────────────────────────────

    [HttpGet("api/v1/personnel-files/{publicId:guid}/additional-benefits")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's additional benefits",
        Description = """
            Returns every additional benefit recorded for the specified personnel file. Each item
            carries its own `concurrencyToken`, required in the `If-Match` header of subsequent
            `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>> GetAdditionalBenefits(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileAdditionalBenefitsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/additional-benefits/{additionalBenefitPublicId:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileAdditionalBenefitResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file additional benefit by id",
        Description = """
            Returns a single additional benefit of the specified personnel file. The `concurrencyToken`
            in the response is required in the `If-Match` header of subsequent
            `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<PersonnelFileAdditionalBenefitResponse>> GetAdditionalBenefitById(
        Guid publicId,
        Guid additionalBenefitPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileAdditionalBenefitByIdQuery(publicId, additionalBenefitPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/additional-benefits")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileAdditionalBenefitResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Add an additional benefit to a personnel file",
        Description = """
            Creates a new additional benefit under the specified personnel file and returns it with a
            `201 Created` response. The `Location` header points to the created resource and the
            `ETag` header carries its initial `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFileAdditionalBenefitResponse>> AddAdditionalBenefit(
        Guid publicId,
        [FromBody] AddAdditionalBenefitRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileAdditionalBenefitCommand(
                publicId,
                new AdditionalBenefitInput(
                    request.BenefitTypeCode,
                    request.StartDate,
                    request.EndDate,
                    request.IsActive,
                    request.Notes)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetAdditionalBenefitById),
            value => new { publicId, additionalBenefitPublicId = value.AdditionalBenefitPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/additional-benefits/{additionalBenefitPublicId:guid}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileAdditionalBenefitResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace a personnel file additional benefit",
        Description = """
            Replaces the business fields of an existing additional benefit. The active state is
            preserved (it is mutated exclusively via `PATCH`). Requires the `If-Match` header
            with the current `concurrencyToken`; the new token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileAdditionalBenefitResponse>> UpdateAdditionalBenefit(
        Guid publicId,
        Guid additionalBenefitPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateAdditionalBenefitRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileAdditionalBenefitCommand(
                publicId,
                additionalBenefitPublicId,
                new AdditionalBenefitInput(
                    request.BenefitTypeCode,
                    request.StartDate,
                    request.EndDate,
                    IsActive: true,
                    request.Notes),
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/additional-benefits/{additionalBenefitPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [Produces("application/json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<PersonnelFileAdditionalBenefitResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a personnel file additional benefit",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type
            `application/json-patch+json`) to an existing additional benefit. Supports the business
            fields and the `isActive` flag. Requires the `If-Match` header with the current
            `concurrencyToken`; the new token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileAdditionalBenefitResponse>> PatchAdditionalBenefit(
        Guid publicId,
        Guid additionalBenefitPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchAdditionalBenefitRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchPersonnelFileAdditionalBenefitCommand(
                publicId,
                additionalBenefitPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new PersonnelFileAdditionalBenefitPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/additional-benefits/{additionalBenefitPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove an additional benefit from a personnel file",
        Description = """
            Deletes the specified additional benefit. Requires the `If-Match` header with the current
            `concurrencyToken`. Returns the parent personnel file's refreshed concurrency token
            so the caller can keep mutating without an extra round-trip.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteAdditionalBenefit(
        Guid publicId,
        Guid additionalBenefitPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileAdditionalBenefitCommand(publicId, additionalBenefitPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    // ─── Payroll Transactions ─────────────────────────────────────────────────

    [HttpGet("api/v1/personnel-files/{publicId:guid}/payroll-transactions")]
    [ProducesResponseType<PagedResponse<PersonnelFilePayrollTransactionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [SwaggerOperation(
        Summary = "Search a personnel file's payroll transactions",
        Description = """
            Returns a paginated, filterable list of payroll transactions for the specified personnel
            file. Supports date-range, type, status, and free-text (`q`) filters plus sorting. Each row
            carries its own `concurrencyToken` for use in the `If-Match` header of a subsequent `PATCH`.
            """)]
    public async Task<ActionResult<PagedResponse<PersonnelFilePayrollTransactionResponse>>> SearchPayrollTransactions(
        Guid publicId,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] string? type = null,
        [FromQuery] string? status = null,
        [FromQuery(Name = "q")] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] PersonnelFileSortDirection sortDirection = PersonnelFileSortDirection.Desc,
        [FromQuery(Name = "page")] int pageNumber = 1,
        [Range(1, PersonnelFileValidationRules.MaxPageSize)]
        [FromQuery] int pageSize = PersonnelFileValidationRules.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchPersonnelFilePayrollTransactionsQuery(
                publicId,
                fromUtc,
                toUtc,
                type,
                status,
                search,
                sortBy,
                sortDirection,
                pageNumber,
                pageSize),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
    [HttpGet("api/v1/personnel-files/{publicId:guid}/payroll-transactions/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [SwaggerOperation(
        Summary = "Export a personnel file's payroll transactions",
        Description = """
            Exports the filtered payroll transactions of the specified personnel file as a file
            (default `xlsx`). Accepts the same date-range, type, status, free-text (`q`), and sorting
            filters as the search endpoint.
            """)]
    public async Task<IActionResult> ExportPayrollTransactions(
        Guid publicId,
        [FromQuery] string format = "xlsx",
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] string? type = null,
        [FromQuery] string? status = null,
        [FromQuery(Name = "q")] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] PersonnelFileSortDirection sortDirection = PersonnelFileSortDirection.Desc,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportPersonnelFilePayrollTransactionsQuery(
                publicId,
                fromUtc,
                toUtc,
                type,
                status,
                search,
                sortBy,
                sortDirection,
                reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<PersonnelFilePayrollTransactionExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "payroll-transactions",
            "PayrollTransactions",
            AuditEntityTypes.PersonnelFile,
            ReportExportResources.PersonnelFilePayrollTransactions,
            "Exported payroll transactions report.",
            new { personnelFileId = publicId, fromUtc, toUtc, type, status, q = search, sortBy, sortDirection },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/payroll-transactions/{payrollTransactionPublicId:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFilePayrollTransactionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file payroll transaction by id",
        Description = """
            Returns a single payroll transaction of the specified personnel file. The `concurrencyToken`
            in the response is required in the `If-Match` header of a subsequent `PATCH` request to
            prevent lost updates.
            """)]
    public async Task<ActionResult<PersonnelFilePayrollTransactionResponse>> GetPayrollTransactionById(
        Guid publicId,
        Guid payrollTransactionPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFilePayrollTransactionByIdQuery(publicId, payrollTransactionPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/payroll-transactions")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFilePayrollTransactionResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Add a payroll transaction to a personnel file",
        Description = """
            Creates a new payroll transaction under the specified personnel file and returns it with a
            `201 Created` response. The `Location` header points to the created resource and the
            `ETag` header carries its initial `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFilePayrollTransactionResponse>> AddPayrollTransaction(
        Guid publicId,
        [FromBody] AddPayrollTransactionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFilePayrollTransactionCommand(
                publicId,
                new PayrollTransactionInput(
                    request.TransactionTypeCode,
                    request.TransactionDateUtc,
                    request.PayrollPeriodCode,
                    request.Description,
                    request.Amount,
                    request.CurrencyCode,
                    request.IsDebit,
                    request.SourceSystem,
                    request.SourceReference,
                    request.SourceSyncedUtc)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetPayrollTransactionById),
            value => new { publicId, payrollTransactionPublicId = value.PayrollTransactionPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/payroll-transactions/{payrollTransactionPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [Produces("application/json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<PersonnelFilePayrollTransactionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a personnel file payroll transaction",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type
            `application/json-patch+json`) to an existing payroll transaction. Supports only the
            `isActive` flag (the business fields are an immutable audit record). Requires the `If-Match`
            header with the current `concurrencyToken`; the new token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFilePayrollTransactionResponse>> PatchPayrollTransaction(
        Guid publicId,
        Guid payrollTransactionPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchPayrollTransactionRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchPersonnelFilePayrollTransactionCommand(
                publicId,
                payrollTransactionPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new PersonnelFilePayrollTransactionPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    // ─── Insurances ───────────────────────────────────────────────────────────

    [HttpGet("api/v1/personnel-files/{publicId:guid}/insurances")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileInsuranceResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's insurances",
        Description = """
            Returns every insurance recorded for the specified personnel file, each with its
            beneficiaries. Each insurance carries its own `concurrencyToken`, required in the
            `If-Match` header of subsequent `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>> GetInsurances(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileInsurancesQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/insurances/{insurancePublicId:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileInsuranceResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file insurance by id",
        Description = """
            Returns a single insurance of the specified personnel file, with its beneficiaries.
            The `concurrencyToken` in the response is required in the `If-Match` header of subsequent
            `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<PersonnelFileInsuranceResponse>> GetInsuranceById(
        Guid publicId,
        Guid insurancePublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileInsuranceByIdQuery(publicId, insurancePublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/insurances")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileInsuranceResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Add an insurance to a personnel file",
        Description = """
            Creates a new insurance under the specified personnel file and returns it with a
            `201 Created` response. Beneficiaries are managed separately via the nested
            `beneficiaries` sub-resource. The `Location` header points to the created resource and
            the `ETag` header carries its initial `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFileInsuranceResponse>> AddInsurance(
        Guid publicId,
        [FromBody] AddInsuranceRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileInsuranceCommand(
                publicId,
                new InsuranceInput(
                    request.InsuranceCode,
                    request.EmployeeContribution,
                    request.EmployerContribution,
                    request.RangeCode,
                    request.PolicyNumber,
                    request.InsuredAmount,
                    request.CurrencyCode,
                    request.IsActive,
                    request.StartDateUtc,
                    request.EndDateUtc)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetInsuranceById),
            value => new { publicId, insurancePublicId = value.InsurancePublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/insurances/{insurancePublicId:guid}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileInsuranceResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace a personnel file insurance",
        Description = """
            Replaces the business fields of an existing insurance. The active state is
            preserved (it is mutated exclusively via `PATCH`) and beneficiaries are managed via the
            nested `beneficiaries` sub-resource. Requires the `If-Match` header with the current
            `concurrencyToken`; the new token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileInsuranceResponse>> UpdateInsurance(
        Guid publicId,
        Guid insurancePublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateInsuranceRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileInsuranceCommand(
                publicId,
                insurancePublicId,
                new InsuranceInput(
                    request.InsuranceCode,
                    request.EmployeeContribution,
                    request.EmployerContribution,
                    request.RangeCode,
                    request.PolicyNumber,
                    request.InsuredAmount,
                    request.CurrencyCode,
                    IsActive: true,
                    request.StartDateUtc,
                    request.EndDateUtc),
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/insurances/{insurancePublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [Produces("application/json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<PersonnelFileInsuranceResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a personnel file insurance",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type
            `application/json-patch+json`) to an existing insurance. Supports the business
            fields and the `isActive` flag. Requires the `If-Match` header with the current
            `concurrencyToken`; the new token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileInsuranceResponse>> PatchInsurance(
        Guid publicId,
        Guid insurancePublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchInsuranceRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchPersonnelFileInsuranceCommand(
                publicId,
                insurancePublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new PersonnelFileInsurancePatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/insurances/{insurancePublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove an insurance from a personnel file",
        Description = """
            Deletes the specified insurance and its beneficiaries. Requires the `If-Match` header
            with the current `concurrencyToken`. Returns the parent personnel file's refreshed
            concurrency token so the caller can keep mutating without an extra round-trip.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteInsurance(
        Guid publicId,
        Guid insurancePublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileInsuranceCommand(publicId, insurancePublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    // ─── Insurance Beneficiaries ──────────────────────────────────────────────

    [HttpGet("api/v1/personnel-files/{publicId:guid}/insurances/{insurancePublicId:guid}/beneficiaries")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileInsuranceBeneficiaryResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List an insurance's beneficiaries",
        Description = """
            Returns every beneficiary recorded for the specified insurance. Each beneficiary
            carries its own `concurrencyToken`, required in the `If-Match` header of subsequent
            `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileInsuranceBeneficiaryResponse>>> GetInsuranceBeneficiaries(
        Guid publicId,
        Guid insurancePublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileInsuranceBeneficiariesQuery(publicId, insurancePublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/insurances/{insurancePublicId:guid}/beneficiaries/{beneficiaryPublicId:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileInsuranceBeneficiaryResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get an insurance beneficiary by id",
        Description = """
            Returns a single beneficiary of the specified insurance. The `concurrencyToken`
            in the response is required in the `If-Match` header of subsequent
            `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<PersonnelFileInsuranceBeneficiaryResponse>> GetInsuranceBeneficiaryById(
        Guid publicId,
        Guid insurancePublicId,
        Guid beneficiaryPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileInsuranceBeneficiaryByIdQuery(publicId, insurancePublicId, beneficiaryPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/insurances/{insurancePublicId:guid}/beneficiaries")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileInsuranceBeneficiaryResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Add a beneficiary to an insurance",
        Description = """
            Creates a new beneficiary under the specified insurance and returns it with a
            `201 Created` response. The `Location` header points to the created resource and the
            `ETag` header carries its initial `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFileInsuranceBeneficiaryResponse>> AddInsuranceBeneficiary(
        Guid publicId,
        Guid insurancePublicId,
        [FromBody] AddInsuranceBeneficiaryRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileInsuranceBeneficiaryCommand(
                publicId,
                insurancePublicId,
                new InsuranceBeneficiaryInput(
                    request.FullName,
                    request.DocumentNumber,
                    request.DocumentTypeCode,
                    request.BirthDate,
                    request.KinshipCode,
                    request.AllocationPercentage,
                    request.BeneficiaryType)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetInsuranceBeneficiaryById),
            value => new { publicId, insurancePublicId, beneficiaryPublicId = value.BeneficiaryPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/insurances/{insurancePublicId:guid}/beneficiaries/{beneficiaryPublicId:guid}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileInsuranceBeneficiaryResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace an insurance beneficiary",
        Description = """
            Replaces the business fields of an existing beneficiary. The active state is
            preserved (it is mutated exclusively via `PATCH`). Requires the `If-Match` header
            with the current `concurrencyToken`; the new token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileInsuranceBeneficiaryResponse>> UpdateInsuranceBeneficiary(
        Guid publicId,
        Guid insurancePublicId,
        Guid beneficiaryPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateInsuranceBeneficiaryRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileInsuranceBeneficiaryCommand(
                publicId,
                insurancePublicId,
                beneficiaryPublicId,
                new InsuranceBeneficiaryInput(
                    request.FullName,
                    request.DocumentNumber,
                    request.DocumentTypeCode,
                    request.BirthDate,
                    request.KinshipCode,
                    request.AllocationPercentage,
                    request.BeneficiaryType),
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/insurances/{insurancePublicId:guid}/beneficiaries/{beneficiaryPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [Produces("application/json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<PersonnelFileInsuranceBeneficiaryResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch an insurance beneficiary",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type
            `application/json-patch+json`) to an existing beneficiary. Supports the business
            fields and the `isActive` flag. Requires the `If-Match` header with the current
            `concurrencyToken`; the new token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileInsuranceBeneficiaryResponse>> PatchInsuranceBeneficiary(
        Guid publicId,
        Guid insurancePublicId,
        Guid beneficiaryPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchInsuranceBeneficiaryRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchPersonnelFileInsuranceBeneficiaryCommand(
                publicId,
                insurancePublicId,
                beneficiaryPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new PersonnelFileInsuranceBeneficiaryPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/insurances/{insurancePublicId:guid}/beneficiaries/{beneficiaryPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove a beneficiary from an insurance",
        Description = """
            Deletes the specified beneficiary. Requires the `If-Match` header with the current
            `concurrencyToken`. Returns the parent personnel file's refreshed concurrency token
            so the caller can keep mutating without an extra round-trip.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteInsuranceBeneficiary(
        Guid publicId,
        Guid insurancePublicId,
        Guid beneficiaryPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileInsuranceBeneficiaryCommand(publicId, insurancePublicId, beneficiaryPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

}

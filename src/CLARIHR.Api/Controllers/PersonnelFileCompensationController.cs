using CLARIHR.Api.Common;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Domain.PersonnelFiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
public sealed class PersonnelFileCompensationController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    ReportExportDeliveryService reportExportDeliveryService) : ControllerBase
{
    // ─── Bank Accounts ────────────────────────────────────────────────────────

    [HttpGet("api/v1/personnel-files/{publicId:guid}/bank-accounts")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileBankAccountResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileBankAccountResponse>>> GetBankAccounts(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileBankAccountsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/bank-accounts")]
    [ProducesResponseType<PersonnelFileBankAccountResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
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
                    request.IsPrimary),
                request.ConcurrencyToken),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<PersonnelFileBankAccountResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/bank-accounts/{itemPublicId:guid}")]
    [ProducesResponseType<PersonnelFileBankAccountResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonnelFileBankAccountResponse>> UpdateBankAccount(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] UpdateBankAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileBankAccountCommand(
                publicId,
                itemPublicId,
                new BankAccountInput(
                    request.BankPublicId,
                    request.CurrencyCode,
                    request.AccountNumber,
                    request.AccountTypeCode,
                    request.IsPrimary),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/bank-accounts/{itemPublicId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteBankAccount(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileBankAccountCommand(publicId, itemPublicId, request.ConcurrencyToken),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(result).Result!
            : NoContent();
    }

    // ─── Salary Items ─────────────────────────────────────────────────────────

    [HttpGet("api/v1/personnel-files/{publicId:guid}/salary-items")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>> GetSalaryItems(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileSalaryItemsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/salary-items")]
    [ProducesResponseType<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>>> AddSalaryItem(
        Guid publicId,
        [FromBody] AddSalaryItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileSalaryItemCommand(
                publicId,
                new SalaryItemInput(
                    request.IncomeTypeCode,
                    request.SalaryRubricCode,
                    request.CurrencyCode,
                    request.PayPeriodCode,
                    request.Amount,
                    request.StartDate,
                    request.EndDate,
                    request.IsActive),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/salary-items/{itemPublicId:guid}")]
    [ProducesResponseType<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>>> UpdateSalaryItem(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] UpdateSalaryItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileSalaryItemCommand(
                publicId,
                itemPublicId,
                new SalaryItemInput(
                    request.IncomeTypeCode,
                    request.SalaryRubricCode,
                    request.CurrencyCode,
                    request.PayPeriodCode,
                    request.Amount,
                    request.StartDate,
                    request.EndDate,
                    request.IsActive),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/salary-items/{itemPublicId:guid}/deactivate")]
    [ProducesResponseType<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>>> DeactivateSalaryItem(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeactivatePersonnelFileSalaryItemCommand(publicId, itemPublicId, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    // ─── Additional Benefits ──────────────────────────────────────────────────

    [HttpGet("api/v1/personnel-files/{publicId:guid}/additional-benefits")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>> GetAdditionalBenefits(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileAdditionalBenefitsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/additional-benefits")]
    [ProducesResponseType<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>>> AddAdditionalBenefit(
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
                    request.Notes),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/additional-benefits/{itemPublicId:guid}")]
    [ProducesResponseType<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>>> UpdateAdditionalBenefit(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] UpdateAdditionalBenefitRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileAdditionalBenefitCommand(
                publicId,
                itemPublicId,
                new AdditionalBenefitInput(
                    request.BenefitTypeCode,
                    request.StartDate,
                    request.EndDate,
                    request.IsActive,
                    request.Notes),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/additional-benefits/{itemPublicId:guid}/deactivate")]
    [ProducesResponseType<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>>> DeactivateAdditionalBenefit(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeactivatePersonnelFileAdditionalBenefitCommand(publicId, itemPublicId, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    // ─── Payment Methods ──────────────────────────────────────────────────────

    [HttpGet("api/v1/personnel-files/{publicId:guid}/payment-methods")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>> GetPaymentMethods(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFilePaymentMethodsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/payment-methods")]
    [ProducesResponseType<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>>> AddPaymentMethod(
        Guid publicId,
        [FromBody] AddPaymentMethodRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFilePaymentMethodCommand(
                publicId,
                new PaymentMethodInput(
                    request.PaymentMethodCode,
                    request.BankAccountPublicId,
                    request.IsPrimary,
                    request.IsActive,
                    request.EffectiveFromUtc,
                    request.EffectiveToUtc,
                    request.Notes),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/payment-methods/{itemPublicId:guid}")]
    [ProducesResponseType<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>>> UpdatePaymentMethod(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] UpdatePaymentMethodRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFilePaymentMethodCommand(
                publicId,
                itemPublicId,
                new PaymentMethodInput(
                    request.PaymentMethodCode,
                    request.BankAccountPublicId,
                    request.IsPrimary,
                    request.IsActive,
                    request.EffectiveFromUtc,
                    request.EffectiveToUtc,
                    request.Notes),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/payment-methods/{itemPublicId:guid}/deactivate")]
    [ProducesResponseType<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>>> DeactivatePaymentMethod(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeactivatePersonnelFilePaymentMethodCommand(publicId, itemPublicId, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    // ─── Payroll Transactions ─────────────────────────────────────────────────

    [HttpGet("api/v1/personnel-files/{publicId:guid}/payroll-transactions")]
    [ProducesResponseType<PagedResponse<PersonnelFilePayrollTransactionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
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

    [HttpGet("api/v1/personnel-files/{publicId:guid}/payroll-transactions/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
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

    [HttpPost("api/v1/personnel-files/{publicId:guid}/payroll-transactions")]
    [ProducesResponseType<PersonnelFilePayrollTransactionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
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
                    request.SourceSyncedUtc),
                request.ConcurrencyToken),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<PersonnelFilePayrollTransactionResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/payroll-transactions/{itemPublicId:guid}/deactivate")]
    [ProducesResponseType<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePayrollTransactionResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePayrollTransactionResponse>>>> DeactivatePayrollTransaction(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeactivatePersonnelFilePayrollTransactionCommand(publicId, itemPublicId, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    // ─── Insurances ───────────────────────────────────────────────────────────

    [HttpGet("api/v1/personnel-files/{publicId:guid}/insurances")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileInsuranceResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>> GetInsurances(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileInsurancesQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/insurances")]
    [ProducesResponseType<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>>> AddInsurance(
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
                    request.EndDateUtc,
                    request.Beneficiaries.Select(beneficiary => new InsuranceBeneficiaryInput(
                        beneficiary.FullName,
                        beneficiary.DocumentNumber,
                        beneficiary.BirthDate,
                        beneficiary.KinshipCode)).ToArray()),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/insurances/{itemPublicId:guid}")]
    [ProducesResponseType<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>>> UpdateInsurance(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] UpdateInsuranceRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileInsuranceCommand(
                publicId,
                itemPublicId,
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
                    request.EndDateUtc,
                    request.Beneficiaries.Select(beneficiary => new InsuranceBeneficiaryInput(
                        beneficiary.FullName,
                        beneficiary.DocumentNumber,
                        beneficiary.BirthDate,
                        beneficiary.KinshipCode)).ToArray()),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/insurances/{itemPublicId:guid}/deactivate")]
    [ProducesResponseType<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>>> DeactivateInsurance(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeactivatePersonnelFileInsuranceCommand(publicId, itemPublicId, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    // ─── Medical Claims ───────────────────────────────────────────────────────

    [HttpGet("api/v1/personnel-files/{publicId:guid}/medical-claims")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>> GetMedicalClaims(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileMedicalClaimsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/medical-claims")]
    [ProducesResponseType<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>>> AddMedicalClaim(
        Guid publicId,
        [FromBody] AddMedicalClaimRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileMedicalClaimCommand(
                publicId,
                new MedicalClaimInput(
                    request.InsurancePublicId,
                    request.AccountNumber,
                    request.ClaimTypeCode,
                    request.Diagnosis,
                    request.ClaimAmount,
                    request.CurrencyCode,
                    request.PaidAmount,
                    request.ResponseTimeDays,
                    request.Notes,
                    request.ClaimDateUtc,
                    request.SourceSystem,
                    request.SourceReference,
                    request.SourceSyncedUtc),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/medical-claims/{itemPublicId:guid}")]
    [ProducesResponseType<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>>> UpdateMedicalClaim(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] UpdateMedicalClaimRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileMedicalClaimCommand(
                publicId,
                itemPublicId,
                new MedicalClaimInput(
                    request.InsurancePublicId,
                    request.AccountNumber,
                    request.ClaimTypeCode,
                    request.Diagnosis,
                    request.ClaimAmount,
                    request.CurrencyCode,
                    request.PaidAmount,
                    request.ResponseTimeDays,
                    request.Notes,
                    request.ClaimDateUtc,
                    request.SourceSystem,
                    request.SourceReference,
                    request.SourceSyncedUtc),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/medical-claims/{itemPublicId:guid}/deactivate")]
    [ProducesResponseType<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>>> DeactivateMedicalClaim(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeactivatePersonnelFileMedicalClaimCommand(publicId, itemPublicId, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

}

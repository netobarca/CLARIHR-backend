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
    [HttpGet("api/v1/personnel-files/{id:guid}/salary-items")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>> GetSalaryItems(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileSalaryItemsQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/salary-items")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileSalaryItemResponse>>> ReplaceSalaryItems(
        Guid id,
        [FromBody] ReplaceSalaryItemsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFileSalaryItemsCommand(
                id,
                request.Items.Select(item => new SalaryItemInput(
                    item.IncomeTypeCode,
                    item.SalaryRubricCode,
                    item.CurrencyCode,
                    item.PayPeriodCode,
                    item.Amount,
                    item.StartDate,
                    item.EndDate,
                    item.IsActive)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{id:guid}/additional-benefits")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>> GetAdditionalBenefits(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileAdditionalBenefitsQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/additional-benefits")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>>> ReplaceAdditionalBenefits(
        Guid id,
        [FromBody] ReplaceAdditionalBenefitsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFileAdditionalBenefitsCommand(
                id,
                request.Items.Select(item => new AdditionalBenefitInput(
                    item.BenefitTypeCode,
                    item.StartDate,
                    item.EndDate,
                    item.IsActive,
                    item.Notes)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{id:guid}/payment-methods")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>> GetPaymentMethods(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFilePaymentMethodsQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/payment-methods")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>>> ReplacePaymentMethods(
        Guid id,
        [FromBody] ReplacePaymentMethodsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFilePaymentMethodsCommand(
                id,
                request.Items.Select(item => new PaymentMethodInput(
                    item.PaymentMethodCode,
                    item.BankAccountPublicId,
                    item.IsPrimary,
                    item.IsActive,
                    item.EffectiveFromUtc,
                    item.EffectiveToUtc,
                    item.Notes)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/payroll-transactions")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFilePayrollTransactionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFilePayrollTransactionResponse>>> ReplacePayrollTransactions(
        Guid id,
        [FromBody] ReplacePayrollTransactionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFilePayrollTransactionsCommand(
                id,
                request.Items.Select(item => new PayrollTransactionInput(
                    item.TransactionTypeCode,
                    item.TransactionDateUtc,
                    item.PayrollPeriodCode,
                    item.Description,
                    item.Amount,
                    item.CurrencyCode,
                    item.IsDebit,
                    item.SourceSystem,
                    item.SourceReference,
                    item.SourceSyncedUtc)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{id:guid}/payroll-transactions")]
    [ProducesResponseType<PagedResponse<PersonnelFilePayrollTransactionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PagedResponse<PersonnelFilePayrollTransactionResponse>>> SearchPayrollTransactions(
        Guid id,
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
                id,
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

    [HttpGet("api/v1/personnel-files/{id:guid}/payroll-transactions/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ExportPayrollTransactions(
        Guid id,
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
                id,
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
            new { personnelFileId = id, fromUtc, toUtc, type, status, q = search, sortBy, sortDirection },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/insurances")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileInsuranceResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>> ReplaceInsurances(
        Guid id,
        [FromBody] ReplaceInsurancesRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFileInsurancesCommand(
                id,
                request.Items.Select(item => new InsuranceInput(
                    item.InsuranceCode,
                    item.EmployeeContribution,
                    item.EmployerContribution,
                    item.RangeCode,
                    item.PolicyNumber,
                    item.InsuredAmount,
                    item.CurrencyCode,
                    item.IsActive,
                    item.StartDateUtc,
                    item.EndDateUtc,
                    item.Beneficiaries.Select(beneficiary => new InsuranceBeneficiaryInput(
                        beneficiary.FullName,
                        beneficiary.DocumentNumber,
                        beneficiary.BirthDate,
                        beneficiary.KinshipCode)).ToArray())).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{id:guid}/insurances")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileInsuranceResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileInsuranceResponse>>> GetInsurances(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileInsurancesQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/medical-claims")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>> ReplaceMedicalClaims(
        Guid id,
        [FromBody] ReplaceMedicalClaimsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFileMedicalClaimsCommand(
                id,
                request.Items.Select(item => new MedicalClaimInput(
                    item.InsurancePublicId,
                    item.AccountNumber,
                    item.ClaimTypeCode,
                    item.Diagnosis,
                    item.ClaimAmount,
                    item.CurrencyCode,
                    item.PaidAmount,
                    item.ResponseTimeDays,
                    item.Notes,
                    item.ClaimDateUtc,
                    item.SourceSystem,
                    item.SourceReference,
                    item.SourceSyncedUtc)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{id:guid}/medical-claims")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>>> GetMedicalClaims(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileMedicalClaimsQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/bank-accounts")]
    [ProducesResponseType<PersonnelFileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonnelFileResponse>> ReplaceBankAccounts(
        Guid id,
        [FromBody] ReplaceBankAccountsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFileBankAccountsCommand(
                id,
                request.Items.Select(item => new BankAccountInput(
                    item.BankCode,
                    item.CurrencyCode,
                    item.AccountNumber,
                    item.AccountTypeCode,
                    item.IsPrimary)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

}

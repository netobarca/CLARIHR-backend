using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
public sealed class PersonnelFilesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    IAuditService auditService,
    IUnitOfWork unitOfWork) : ControllerBase
{
    [HttpPost("api/v1/companies/{companyId:guid}/personnel-files")]
    [ProducesResponseType<PersonnelFileResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileResponse>> Create(
        Guid companyId,
        [FromBody] CreatePersonnelFileRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreatePersonnelFileCommand(
                companyId,
                request.RecordType,
                request.FirstName,
                request.LastName,
                request.BirthDate,
                request.MaritalStatus,
                request.Profession,
                request.Nationality,
                request.PersonalEmail,
                request.InstitutionalEmail,
                request.PersonalPhone,
                request.InstitutionalPhone,
                request.BirthCountry,
                request.BirthDepartment,
                request.BirthMunicipality,
                request.PhotoUrl,
                request.OrgUnitId,
                request.CustomDataJson,
                request.Identifications.Select(item => new IdentificationInput(
                    item.IdentificationType,
                    item.IdentificationNumber,
                    item.IssuedDate,
                    item.ExpiryDate,
                    item.Issuer,
                    item.IsPrimary)).ToArray()),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<PersonnelFileResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpGet("api/v1/companies/{companyId:guid}/personnel-files")]
    [ProducesResponseType<PagedResponse<PersonnelFileListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<PersonnelFileListItemResponse>>> Search(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery] PersonnelFileRecordType? recordType,
        [FromQuery] Guid? orgUnitId,
        [FromQuery] int? minAge,
        [FromQuery] int? maxAge,
        [FromQuery] string? maritalStatus,
        [FromQuery] string? nationality,
        [FromQuery] string? profession,
        [FromQuery] DateTime? createdFromUtc,
        [FromQuery] DateTime? createdToUtc,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] string? sortBy,
        [FromQuery] PersonnelFileSortDirection sortDirection = PersonnelFileSortDirection.Asc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PersonnelFileValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchPersonnelFilesQuery(
                companyId,
                isActive,
                recordType,
                orgUnitId,
                minAge,
                maxAge,
                maritalStatus,
                nationality,
                profession,
                createdFromUtc,
                createdToUtc,
                search,
                sortBy,
                sortDirection,
                page,
                pageSize,
                includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{id:guid}")]
    [ProducesResponseType<PersonnelFileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PersonnelFileResponse>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{id:guid}/print")]
    [ProducesResponseType<PersonnelFilePrintResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PersonnelFilePrintResponse>> Print(
        Guid id,
        [FromQuery] string? sections,
        CancellationToken cancellationToken = default)
    {
        var parsedSections = string.IsNullOrWhiteSpace(sections)
            ? null
            : sections.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFilePrintQuery(id, parsedSections),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        await auditService.LogAsync(
            new AuditLogEntry(
                AuditEventTypes.ReportPrinted,
                AuditEntityTypes.PersonnelFile,
                id,
                PersonnelFilePermissionCodes.ResourceKey,
                AuditActions.Print,
                "Printed personnel file report.",
                After: new
                {
                    resourceKey = PersonnelFilePermissionCodes.ResourceKey,
                    format = "print",
                    filters = new { id, sections = result.Value.IncludedSections },
                    rowCount = 1
                }),
            cancellationToken);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/personal-info")]
    [ProducesResponseType<PersonnelFileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonnelFileResponse>> UpdatePersonalInfo(
        Guid id,
        [FromBody] UpdatePersonnelFilePersonalInfoRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFilePersonalInfoCommand(
                id,
                request.RecordType,
                request.FirstName,
                request.LastName,
                request.BirthDate,
                request.MaritalStatus,
                request.Profession,
                request.Nationality,
                request.PersonalEmail,
                request.InstitutionalEmail,
                request.PersonalPhone,
                request.InstitutionalPhone,
                request.BirthCountry,
                request.BirthDepartment,
                request.BirthMunicipality,
                request.PhotoUrl,
                request.OrgUnitId,
                request.CustomDataJson,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{id:guid}/hire")]
    [ProducesResponseType<PersonnelFileEmployeeProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileEmployeeProfileResponse>> Hire(
        Guid id,
        [FromBody] HirePersonnelFileRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new HirePersonnelFileCommand(
                id,
                request.EmployeeCode,
                request.EmploymentStatusCode,
                request.IsEmploymentActive,
                request.ContractTypeCode,
                request.HireDate,
                request.WorkdayCode,
                request.PayrollTypeCode,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/employee-profile")]
    [ProducesResponseType<PersonnelFileEmployeeProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileEmployeeProfileResponse>> UpdateEmployeeProfile(
        Guid id,
        [FromBody] UpdatePersonnelFileEmployeeProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileEmployeeProfileCommand(
                id,
                request.EmployeeCode,
                request.EmploymentStatusCode,
                request.IsEmploymentActive,
                request.ContractTypeCode,
                request.HireDate,
                request.RetirementCategoryCode,
                request.RetirementReasonCode,
                request.RetirementNotes,
                request.RetirementDate,
                request.WorkdayCode,
                request.PayrollTypeCode,
                request.PositionSlotId,
                request.JobProfileId,
                request.OrgUnitId,
                request.WorkCenterId,
                request.CostCenterId,
                request.ContractStartDate,
                request.ContractEndDate,
                request.VacationConfigurationJson,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/employment-assignments")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>> ReplaceEmploymentAssignments(
        Guid id,
        [FromBody] ReplaceEmploymentAssignmentsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFileEmploymentAssignmentsCommand(
                id,
                request.Items.Select(item => new EmploymentAssignmentInput(
                    item.AssignmentTypeCode,
                    item.PositionSlotId,
                    item.OrgUnitId,
                    item.WorkCenterId,
                    item.CostCenterId,
                    item.StartDate,
                    item.EndDate,
                    item.IsPrimary,
                    item.IsActive,
                    item.Notes)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/contract-history")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileContractHistoryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileContractHistoryResponse>>> ReplaceContractHistory(
        Guid id,
        [FromBody] ReplaceContractHistoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFileContractHistoryCommand(
                id,
                request.Items.Select(item => new ContractHistoryInput(
                    item.ContractTypeCode,
                    item.ContractDate,
                    item.ContractEndDate,
                    item.PositionSlotId,
                    item.Notes)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{id:guid}/position-hierarchy")]
    [ProducesResponseType<PersonnelFilePositionHierarchyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFilePositionHierarchyResponse>> GetPositionHierarchy(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFilePositionHierarchyQuery(id), cancellationToken);
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
                    item.BankAccountId,
                    item.IsPrimary,
                    item.IsActive,
                    item.EffectiveFromUtc,
                    item.EffectiveToUtc,
                    item.Notes)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/authorization-substitutions")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>> ReplaceAuthorizationSubstitutions(
        Guid id,
        [FromBody] ReplaceAuthorizationSubstitutionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFileAuthorizationSubstitutionsCommand(
                id,
                request.Items.Select(item => new AuthorizationSubstitutionInput(
                    item.SubstitutionTypeCode,
                    item.SubstitutePersonnelFileId,
                    item.SubstitutePositionTitle,
                    item.StartDate,
                    item.EndDate,
                    item.IsActive,
                    item.Notes)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{id:guid}/personnel-actions")]
    [ProducesResponseType<PersonnelFilePersonnelActionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFilePersonnelActionResponse>> AddPersonnelAction(
        Guid id,
        [FromBody] AddPersonnelActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFilePersonnelActionCommand(
                id,
                request.ActionTypeCode,
                request.ActionStatusCode,
                request.ActionDateUtc,
                request.EffectiveFromUtc,
                request.EffectiveToUtc,
                request.Description,
                request.Reference,
                request.Amount,
                request.CurrencyCode,
                request.ConcurrencyToken),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<PersonnelFilePersonnelActionResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpGet("api/v1/personnel-files/{id:guid}/personnel-actions")]
    [ProducesResponseType<PagedResponse<PersonnelFilePersonnelActionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PagedResponse<PersonnelFilePersonnelActionResponse>>> SearchPersonnelActions(
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
            new SearchPersonnelFilePersonnelActionsQuery(
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

    [HttpGet("api/v1/personnel-files/{id:guid}/personnel-actions/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ExportPersonnelActions(
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
            new ExportPersonnelFilePersonnelActionsQuery(
                id,
                fromUtc,
                toUtc,
                type,
                status,
                search,
                sortBy,
                sortDirection),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<PersonnelFilePersonnelActionExportRow>>.Failure(result.Error)).Result!;
        }

        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.ReportExported,
                    AuditEntityTypes.PersonnelFile,
                    id,
                    PersonnelFilePermissionCodes.ResourceKey,
                    AuditActions.Export,
                    "Exported personnel actions report.",
                    After: new
                    {
                        resourceKey = PersonnelFilePermissionCodes.ResourceKey,
                        format = "csv",
                        filters = new { fromUtc, toUtc, type, status, q = search, sortBy, sortDirection },
                        rowCount = result.Value.Count
                    }),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var csv = BuildPersonnelActionsCsv(result.Value);
            return File(Encoding.UTF8.GetBytes(csv), "text/csv", "personnel-actions.csv");
        }

        if (string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase))
        {
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.ReportExported,
                    AuditEntityTypes.PersonnelFile,
                    id,
                    PersonnelFilePermissionCodes.ResourceKey,
                    AuditActions.Export,
                    "Exported personnel actions report.",
                    After: new
                    {
                        resourceKey = PersonnelFilePermissionCodes.ResourceKey,
                        format = "xlsx",
                        filters = new { fromUtc, toUtc, type, status, q = search, sortBy, sortDirection },
                        rowCount = result.Value.Count
                    }),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var xlsx = BuildPersonnelActionsXlsx(result.Value);
            return File(
                xlsx,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "personnel-actions.xlsx");
        }

        return this.ToActionResult(Result<IReadOnlyCollection<PersonnelFilePersonnelActionExportRow>>.Failure(PersonnelFileErrors.ExportFormatInvalid)).Result!;
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
                sortDirection),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<PersonnelFilePayrollTransactionExportRow>>.Failure(result.Error)).Result!;
        }

        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.ReportExported,
                    AuditEntityTypes.PersonnelFile,
                    id,
                    PersonnelFilePermissionCodes.ResourceKey,
                    AuditActions.Export,
                    "Exported payroll transactions report.",
                    After: new
                    {
                        resourceKey = PersonnelFilePermissionCodes.ResourceKey,
                        format = "csv",
                        filters = new { fromUtc, toUtc, type, status, q = search, sortBy, sortDirection },
                        rowCount = result.Value.Count
                    }),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var csv = BuildPayrollTransactionsCsv(result.Value);
            return File(Encoding.UTF8.GetBytes(csv), "text/csv", "payroll-transactions.csv");
        }

        if (string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase))
        {
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.ReportExported,
                    AuditEntityTypes.PersonnelFile,
                    id,
                    PersonnelFilePermissionCodes.ResourceKey,
                    AuditActions.Export,
                    "Exported payroll transactions report.",
                    After: new
                    {
                        resourceKey = PersonnelFilePermissionCodes.ResourceKey,
                        format = "xlsx",
                        filters = new { fromUtc, toUtc, type, status, q = search, sortBy, sortDirection },
                        rowCount = result.Value.Count
                    }),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var xlsx = BuildPayrollTransactionsXlsx(result.Value);
            return File(
                xlsx,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "payroll-transactions.xlsx");
        }

        return this.ToActionResult(Result<IReadOnlyCollection<PersonnelFilePayrollTransactionExportRow>>.Failure(PersonnelFileErrors.ExportFormatInvalid)).Result!;
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/assets-accesses")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileAssetAccessResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileAssetAccessResponse>>> ReplaceAssetsAccesses(
        Guid id,
        [FromBody] ReplaceAssetsAccessesRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFileAssetsAccessesCommand(
                id,
                request.Items.Select(item => new AssetAccessInput(
                    item.AssetTypeCode,
                    item.AssetOrAccessName,
                    item.AccessLevelCode,
                    item.StartDateUtc,
                    item.EndDateUtc,
                    item.DeliveryDateUtc,
                    item.DeliveryStatusCode,
                    item.IsActive,
                    item.Notes)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
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
                    item.InsuranceId,
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

    [HttpPut("api/v1/personnel-files/{id:guid}/evaluations")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>>> ReplaceEvaluations(
        Guid id,
        [FromBody] ReplacePerformanceEvaluationsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFilePerformanceEvaluationsCommand(
                id,
                request.Items.Select(item => new PerformanceEvaluationInput(
                    item.EvaluatorName,
                    item.EvaluationDateUtc,
                    item.Score,
                    item.QualitativeScoreCode,
                    item.Comment,
                    item.SourceSystem,
                    item.SourceReference,
                    item.SourceSyncedUtc)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{id:guid}/evaluations")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>>> GetEvaluations(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFilePerformanceEvaluationsQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/position-competency-results")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>> ReplacePositionCompetencyResults(
        Guid id,
        [FromBody] ReplacePositionCompetencyResultsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFilePositionCompetencyResultsCommand(
                id,
                request.Items.Select(item => new PositionCompetencyResultInput(
                    item.CompetencyCode,
                    item.DesiredBehaviors,
                    item.ExpectedScore,
                    item.AchievedScore,
                    item.GapScore,
                    item.EvaluationDateUtc,
                    item.SourceSystem,
                    item.SourceReference,
                    item.SourceSyncedUtc)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{id:guid}/position-competencies")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>> GetPositionCompetencies(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFilePositionCompetencyResultsQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/selection-contests")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileSelectionContestResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileSelectionContestResponse>>> ReplaceSelectionContests(
        Guid id,
        [FromBody] ReplaceSelectionContestsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFileSelectionContestsCommand(
                id,
                request.Items.Select(item => new SelectionContestInput(
                    item.ContestCode,
                    item.ContestName,
                    item.ContestDateUtc,
                    item.ResultCode,
                    item.Notes,
                    item.SourceSystem,
                    item.SourceReference,
                    item.SourceSyncedUtc)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{id:guid}/selection-contests")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileSelectionContestResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileSelectionContestResponse>>> GetSelectionContests(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileSelectionContestsQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/curricular-competencies")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>> ReplaceCurricularCompetencies(
        Guid id,
        [FromBody] ReplaceCurricularCompetenciesRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFileCurricularCompetenciesCommand(
                id,
                request.Items.Select(item => new CurricularCompetencyInput(
                    item.RequirementTypeCode,
                    item.RequirementName,
                    item.CompetencyDomain,
                    item.ExperienceTimeValue,
                    item.MetricCode,
                    item.Notes,
                    item.SourceSystem,
                    item.SourceReference,
                    item.SourceSyncedUtc)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/identifications")]
    [ProducesResponseType<PersonnelFileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonnelFileResponse>> ReplaceIdentifications(
        Guid id,
        [FromBody] ReplaceIdentificationsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFileIdentificationsCommand(
                id,
                request.Identifications.Select(item => new IdentificationInput(
                    item.IdentificationType,
                    item.IdentificationNumber,
                    item.IssuedDate,
                    item.ExpiryDate,
                    item.Issuer,
                    item.IsPrimary)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/addresses")]
    [ProducesResponseType<PersonnelFileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonnelFileResponse>> ReplaceAddresses(
        Guid id,
        [FromBody] ReplaceAddressesRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFileAddressesCommand(
                id,
                request.Addresses.Select(item => new AddressInput(
                    item.AddressLine,
                    item.Country,
                    item.Department,
                    item.Municipality,
                    item.PostalCode,
                    item.IsCurrent)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/emergency-contacts")]
    [ProducesResponseType<PersonnelFileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonnelFileResponse>> ReplaceEmergencyContacts(
        Guid id,
        [FromBody] ReplaceEmergencyContactsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFileEmergencyContactsCommand(
                id,
                request.Contacts.Select(item => new EmergencyContactInput(
                    item.Name,
                    item.Relationship,
                    item.Phone,
                    item.Address,
                    item.Workplace)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/family-members")]
    [ProducesResponseType<PersonnelFileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileResponse>> ReplaceFamilyMembers(
        Guid id,
        [FromBody] ReplaceFamilyMembersRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFileFamilyMembersCommand(
                id,
                request.FamilyMembers.Select(item => new FamilyMemberInput(
                    item.FirstName,
                    item.LastName,
                    item.Relationship,
                    item.Nationality,
                    item.BirthDate,
                    item.Sex,
                    item.MaritalStatus,
                    item.Occupation,
                    item.DocumentType,
                    item.DocumentNumber,
                    item.Phone,
                    item.IsStudying,
                    item.StudyPlace,
                    item.AcademicLevel,
                    item.IsBeneficiary,
                    item.IsWorking,
                    item.Workplace,
                    item.JobTitle,
                    item.WorkPhone,
                    item.Salary,
                    item.IsDeceased,
                    item.DeceasedDate)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/hobbies")]
    [ProducesResponseType<PersonnelFileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonnelFileResponse>> ReplaceHobbies(
        Guid id,
        [FromBody] ReplaceHobbiesRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFileHobbiesCommand(
                id,
                request.Hobbies.Select(item => new HobbyInput(item.HobbyName)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/employee-relations")]
    [ProducesResponseType<PersonnelFileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonnelFileResponse>> ReplaceEmployeeRelations(
        Guid id,
        [FromBody] ReplaceEmployeeRelationsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFileEmployeeRelationsCommand(
                id,
                request.Relations.Select(item => new EmployeeRelationInput(item.RelatedEmployeeName, item.Relationship)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

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
                request.BankAccounts.Select(item => new BankAccountInput(
                    item.BankCode,
                    item.CurrencyCode,
                    item.AccountNumber,
                    item.AccountTypeCode,
                    item.IsPrimary)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/associations")]
    [ProducesResponseType<PersonnelFileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonnelFileResponse>> ReplaceAssociations(
        Guid id,
        [FromBody] ReplaceAssociationsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFileAssociationsCommand(
                id,
                request.Associations.Select(item => new AssociationInput(
                    item.AssociationName,
                    item.Role,
                    item.JoinedDate,
                    item.LeftDate,
                    item.Payment)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/educations")]
    [ProducesResponseType<PersonnelFileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileResponse>> ReplaceEducations(
        Guid id,
        [FromBody] ReplaceEducationsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFileEducationsCommand(
                id,
                request.Educations.Select(item => new EducationInput(
                    item.StatusCode,
                    item.DegreeTitle,
                    item.StudyTypeCode,
                    item.Career,
                    item.Institution,
                    item.CountryCode,
                    item.Specialty,
                    item.IsCurrentlyStudying,
                    item.StartDate,
                    item.EndDate,
                    item.ShiftCode,
                    item.ModalityCode,
                    item.TotalSubjects,
                    item.ApprovedSubjects)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/languages")]
    [ProducesResponseType<PersonnelFileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileResponse>> ReplaceLanguages(
        Guid id,
        [FromBody] ReplaceLanguagesRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFileLanguagesCommand(
                id,
                request.Languages.Select(item => new LanguageInput(
                    item.LanguageCode,
                    item.LevelCode,
                    item.Speaks,
                    item.Writes,
                    item.Reads)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/trainings")]
    [ProducesResponseType<PersonnelFileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileResponse>> ReplaceTrainings(
        Guid id,
        [FromBody] ReplaceTrainingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFileTrainingsCommand(
                id,
                request.Trainings.Select(item => new TrainingInput(
                    item.TrainingName,
                    item.TrainingTypeCode,
                    item.Description,
                    item.Topic,
                    item.Institution,
                    item.Instructors,
                    item.Score,
                    item.StartDate,
                    item.EndDate,
                    item.IsInternal,
                    item.IsLocal,
                    item.CountryCode,
                    item.DurationValue,
                    item.DurationUnitCode,
                    item.CostAmount,
                    item.CostCurrencyCode)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/previous-employments")]
    [ProducesResponseType<PersonnelFileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileResponse>> ReplacePreviousEmployments(
        Guid id,
        [FromBody] ReplacePreviousEmploymentsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFilePreviousEmploymentsCommand(
                id,
                request.PreviousEmployments.Select(item => new PreviousEmploymentInput(
                    item.Institution,
                    item.Place,
                    item.LastPosition,
                    item.ManagerName,
                    item.EntryDate,
                    item.RetirementDate,
                    item.CompanyPhone,
                    item.ExitReason,
                    item.FirstSalaryAmount,
                    item.LastSalaryAmount,
                    item.AverageCommissionAmount,
                    item.CurrencyCode)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/references")]
    [ProducesResponseType<PersonnelFileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileResponse>> ReplaceReferences(
        Guid id,
        [FromBody] ReplaceReferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFileReferencesCommand(
                id,
                request.References.Select(item => new ReferenceInput(
                    item.PersonName,
                    item.Address,
                    item.Phone,
                    item.ReferenceTypeCode,
                    item.Occupation,
                    item.Workplace,
                    item.WorkPhone,
                    item.KnownTimeYears)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/personnel-files/{id:guid}/activate")]
    [ProducesResponseType<PersonnelFileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonnelFileResponse>> Activate(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(new ActivatePersonnelFileCommand(id, request.ConcurrencyToken), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/personnel-files/{id:guid}/inactivate")]
    [ProducesResponseType<PersonnelFileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonnelFileResponse>> Inactivate(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(new InactivatePersonnelFileCommand(id, request.ConcurrencyToken), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/companies/{companyId:guid}/personnel-catalogs/{category}")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelCatalogItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelCatalogItemResponse>>> GetCatalogItems(
        Guid companyId,
        string category,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelCatalogItemsQuery(companyId, category), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{id:guid}/documents")]
    [ProducesResponseType<PersonnelFileDocumentMetadataResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileDocumentMetadataResponse>> UploadDocument(
        Guid id,
        [FromForm] UploadPersonnelFileDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.File is null || request.File.Length == 0)
        {
            return this.ToActionResult(Result<PersonnelFileDocumentMetadataResponse>.Failure(PersonnelFileErrors.DocumentFileRequired));
        }

        await using var stream = request.File.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);

        var result = await commandDispatcher.SendAsync(
            new UploadPersonnelFileDocumentCommand(
                id,
                request.DocumentType,
                request.Observations,
                request.DeliveryDate,
                request.LoanDate,
                request.ReturnDate,
                request.File.FileName,
                request.File.ContentType,
                memory.ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<PersonnelFileDocumentMetadataResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPatch("api/v1/personnel-file-documents/{documentId:guid}/inactivate")]
    [ProducesResponseType<PersonnelFileDocumentMetadataResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonnelFileDocumentMetadataResponse>> InactivateDocument(
        Guid documentId,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivatePersonnelFileDocumentCommand(documentId, request.ConcurrencyToken),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-file-documents/{documentId:guid}/download")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadDocument(Guid documentId, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileDocumentDownloadQuery(documentId), cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(Result<PersonnelFileDocumentDownloadResponse>.Failure(result.Error)).Result!;
        }

        return File(result.Value.FileData, result.Value.ContentType, result.Value.FileName);
    }

    [HttpPost("api/v1/personnel-files/{id:guid}/observations")]
    [ProducesResponseType<PersonnelFileObservationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonnelFileObservationResponse>> AddObservation(
        Guid id,
        [FromBody] AddObservationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileObservationCommand(id, request.Note, request.ConcurrencyToken),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<PersonnelFileObservationResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpGet("api/v1/companies/{companyId:guid}/personnel-custom-field-definitions")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelCustomFieldDefinitionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelCustomFieldDefinitionResponse>>> GetCustomFieldDefinitions(
        Guid companyId,
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelCustomFieldDefinitionsQuery(companyId, isActive), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/companies/{companyId:guid}/personnel-custom-field-definitions")]
    [ProducesResponseType<PersonnelCustomFieldDefinitionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonnelCustomFieldDefinitionResponse>> CreateCustomFieldDefinition(
        Guid companyId,
        [FromBody] CreateCustomFieldDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreatePersonnelCustomFieldDefinitionCommand(
                companyId,
                request.Key,
                request.Label,
                request.FieldType,
                request.IsRequired,
                request.IsActive,
                request.OptionsJson,
                request.SortOrder),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<PersonnelCustomFieldDefinitionResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("api/v1/personnel-custom-field-definitions/{id:guid}")]
    [ProducesResponseType<PersonnelCustomFieldDefinitionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonnelCustomFieldDefinitionResponse>> UpdateCustomFieldDefinition(
        Guid id,
        [FromBody] UpdateCustomFieldDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelCustomFieldDefinitionCommand(
                id,
                request.Key,
                request.Label,
                request.FieldType,
                request.IsRequired,
                request.IsActive,
                request.OptionsJson,
                request.SortOrder,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/companies/{companyId:guid}/personnel-files/dynamic-query")]
    [ProducesResponseType<PersonnelFileDynamicQueryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileDynamicQueryResponse>> DynamicQuery(
        Guid companyId,
        [FromBody] DynamicQueryPersonnelFilesRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new DynamicQueryPersonnelFilesQuery(
                companyId,
                (request.Filters ?? Array.Empty<DynamicPersonnelFileFilterRequest>()).Select(item => new PersonnelFileDynamicFilterInput(
                    item.Field,
                    item.Operator,
                    item.Value,
                    item.ValueTo,
                    item.Values)).ToArray(),
                request.GroupBy ?? Array.Empty<string>(),
                (request.Sort ?? Array.Empty<DynamicPersonnelFileSortRequest>()).Select(item => new PersonnelFileDynamicSortInput(item.Field, item.Direction)).ToArray(),
                request.Q,
                request.Page,
                request.PageSize,
                request.IncludeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/companies/{companyId:guid}/personnel-files/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Export(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] bool? isActive = null,
        [FromQuery] PersonnelFileRecordType? recordType = null,
        [FromQuery] Guid? orgUnitId = null,
        [FromQuery] int? minAge = null,
        [FromQuery] int? maxAge = null,
        [FromQuery] string? maritalStatus = null,
        [FromQuery] string? nationality = null,
        [FromQuery] string? profession = null,
        [FromQuery] DateTime? createdFromUtc = null,
        [FromQuery] DateTime? createdToUtc = null,
        [FromQuery(Name = "q")] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] PersonnelFileSortDirection sortDirection = PersonnelFileSortDirection.Asc,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportPersonnelFilesQuery(
                companyId,
                isActive,
                recordType,
                orgUnitId,
                minAge,
                maxAge,
                maritalStatus,
                nationality,
                profession,
                createdFromUtc,
                createdToUtc,
                search,
                sortBy,
                sortDirection),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<PersonnelFileExportRow>>.Failure(result.Error)).Result!;
        }

        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.ReportExported,
                    AuditEntityTypes.PersonnelFile,
                    null,
                    PersonnelFilePermissionCodes.ResourceKey,
                    AuditActions.Export,
                    "Exported personnel files report.",
                    After: new
                    {
                        resourceKey = PersonnelFilePermissionCodes.ResourceKey,
                        format = "csv",
                        filters = new
                        {
                            isActive,
                            recordType,
                            orgUnitId,
                            minAge,
                            maxAge,
                            maritalStatus,
                            nationality,
                            profession,
                            createdFromUtc,
                            createdToUtc,
                            q = search,
                            sortBy,
                            sortDirection
                        },
                        rowCount = result.Value.Count
                    }),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var csv = BuildCsv(result.Value);
            return File(Encoding.UTF8.GetBytes(csv), "text/csv", "personnel-files.csv");
        }

        if (string.Equals(format, "xlsx", StringComparison.OrdinalIgnoreCase))
        {
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.ReportExported,
                    AuditEntityTypes.PersonnelFile,
                    null,
                    PersonnelFilePermissionCodes.ResourceKey,
                    AuditActions.Export,
                    "Exported personnel files report.",
                    After: new
                    {
                        resourceKey = PersonnelFilePermissionCodes.ResourceKey,
                        format = "xlsx",
                        filters = new
                        {
                            isActive,
                            recordType,
                            orgUnitId,
                            minAge,
                            maxAge,
                            maritalStatus,
                            nationality,
                            profession,
                            createdFromUtc,
                            createdToUtc,
                            q = search,
                            sortBy,
                            sortDirection
                        },
                        rowCount = result.Value.Count
                    }),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var xlsx = BuildXlsx(result.Value);
            return File(
                xlsx,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "personnel-files.xlsx");
        }

        return this.ToActionResult(Result<IReadOnlyCollection<PersonnelFileExportRow>>.Failure(PersonnelFileErrors.ExportFormatInvalid)).Result!;
    }

    [HttpGet("api/v1/companies/{companyId:guid}/personnel-files/analytics/summary")]
    [ProducesResponseType<PersonnelFileAnalyticsSummaryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PersonnelFileAnalyticsSummaryResponse>> AnalyticsSummary(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery] PersonnelFileRecordType? recordType,
        [FromQuery] Guid? orgUnitId,
        [FromQuery] int? minAge,
        [FromQuery] int? maxAge,
        [FromQuery(Name = "q")] string? search,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFilesAnalyticsSummaryQuery(companyId, isActive, recordType, orgUnitId, minAge, maxAge, search),
            cancellationToken);

        return this.ToActionResult(result);
    }

    private static string BuildPersonnelActionsCsv(IReadOnlyCollection<PersonnelFilePersonnelActionExportRow> rows)
    {
        var lines = new List<string>
        {
            "Id,ActionTypeCode,ActionStatusCode,ActionDateUtc,EffectiveFromUtc,EffectiveToUtc,Description,Reference,Amount,CurrencyCode,IsSystemGenerated,CreatedAtUtc,ModifiedAtUtc"
        };

        lines.AddRange(rows.Select(row => string.Join(",",
            EscapeCsv(row.Id.ToString()),
            EscapeCsv(row.ActionTypeCode),
            EscapeCsv(row.ActionStatusCode),
            EscapeCsv(row.ActionDateUtc.ToString("O", CultureInfo.InvariantCulture)),
            EscapeCsv(row.EffectiveFromUtc?.ToString("O", CultureInfo.InvariantCulture)),
            EscapeCsv(row.EffectiveToUtc?.ToString("O", CultureInfo.InvariantCulture)),
            EscapeCsv(row.Description),
            EscapeCsv(row.Reference),
            EscapeCsv(row.Amount?.ToString(CultureInfo.InvariantCulture)),
            EscapeCsv(row.CurrencyCode),
            row.IsSystemGenerated ? "true" : "false",
            EscapeCsv(row.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture)),
            EscapeCsv(row.ModifiedAtUtc?.ToString("O", CultureInfo.InvariantCulture)))));

        return string.Join("\n", lines);
    }

    private static string BuildPayrollTransactionsCsv(IReadOnlyCollection<PersonnelFilePayrollTransactionExportRow> rows)
    {
        var lines = new List<string>
        {
            "Id,TransactionTypeCode,TransactionDateUtc,PayrollPeriodCode,Description,Amount,CurrencyCode,IsDebit,SourceSystem,SourceReference,SourceSyncedUtc,CreatedAtUtc,ModifiedAtUtc"
        };

        lines.AddRange(rows.Select(row => string.Join(",",
            EscapeCsv(row.Id.ToString()),
            EscapeCsv(row.TransactionTypeCode),
            EscapeCsv(row.TransactionDateUtc.ToString("O", CultureInfo.InvariantCulture)),
            EscapeCsv(row.PayrollPeriodCode),
            EscapeCsv(row.Description),
            EscapeCsv(row.Amount.ToString(CultureInfo.InvariantCulture)),
            EscapeCsv(row.CurrencyCode),
            row.IsDebit ? "true" : "false",
            EscapeCsv(row.SourceSystem),
            EscapeCsv(row.SourceReference),
            EscapeCsv(row.SourceSyncedUtc?.ToString("O", CultureInfo.InvariantCulture)),
            EscapeCsv(row.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture)),
            EscapeCsv(row.ModifiedAtUtc?.ToString("O", CultureInfo.InvariantCulture)))));

        return string.Join("\n", lines);
    }

    private static byte[] BuildPersonnelActionsXlsx(IReadOnlyCollection<PersonnelFilePersonnelActionExportRow> rows) =>
        BuildSimpleXlsx(
            "PersonnelActions",
            [
                "Id",
                "ActionTypeCode",
                "ActionStatusCode",
                "ActionDateUtc",
                "EffectiveFromUtc",
                "EffectiveToUtc",
                "Description",
                "Reference",
                "Amount",
                "CurrencyCode",
                "IsSystemGenerated",
                "CreatedAtUtc",
                "ModifiedAtUtc"
            ],
            rows.Select(row => new[]
            {
                row.Id.ToString(),
                row.ActionTypeCode,
                row.ActionStatusCode,
                row.ActionDateUtc.ToString("O", CultureInfo.InvariantCulture),
                row.EffectiveFromUtc?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
                row.EffectiveToUtc?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
                row.Description ?? string.Empty,
                row.Reference ?? string.Empty,
                row.Amount?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                row.CurrencyCode ?? string.Empty,
                row.IsSystemGenerated ? "true" : "false",
                row.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                row.ModifiedAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty
            }).ToArray());

    private static byte[] BuildPayrollTransactionsXlsx(IReadOnlyCollection<PersonnelFilePayrollTransactionExportRow> rows) =>
        BuildSimpleXlsx(
            "PayrollTransactions",
            [
                "Id",
                "TransactionTypeCode",
                "TransactionDateUtc",
                "PayrollPeriodCode",
                "Description",
                "Amount",
                "CurrencyCode",
                "IsDebit",
                "SourceSystem",
                "SourceReference",
                "SourceSyncedUtc",
                "CreatedAtUtc",
                "ModifiedAtUtc"
            ],
            rows.Select(row => new[]
            {
                row.Id.ToString(),
                row.TransactionTypeCode,
                row.TransactionDateUtc.ToString("O", CultureInfo.InvariantCulture),
                row.PayrollPeriodCode,
                row.Description ?? string.Empty,
                row.Amount.ToString(CultureInfo.InvariantCulture),
                row.CurrencyCode,
                row.IsDebit ? "true" : "false",
                row.SourceSystem ?? string.Empty,
                row.SourceReference ?? string.Empty,
                row.SourceSyncedUtc?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty,
                row.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture),
                row.ModifiedAtUtc?.ToString("O", CultureInfo.InvariantCulture) ?? string.Empty
            }).ToArray());

    private static byte[] BuildSimpleXlsx(string sheetName, IReadOnlyList<string> headers, IReadOnlyCollection<string[]> rows)
    {
        static string Cell(string? value) =>
            $"<c t=\"inlineStr\"><is><t>{EscapeXml(value)}</t></is></c>";

        var sheetRows = new StringBuilder();
        sheetRows.Append("<row r=\"1\">");
        foreach (var header in headers)
        {
            sheetRows.Append(Cell(header));
        }

        sheetRows.Append("</row>");

        var rowIndex = 2;
        foreach (var row in rows)
        {
            sheetRows.Append($"<row r=\"{rowIndex++}\">");
            foreach (var value in row)
            {
                sheetRows.Append(Cell(value));
            }

            sheetRows.Append("</row>");
        }

        var sheetXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<sheetData>" + sheetRows + "</sheetData>" +
            "</worksheet>";

        var contentTypesXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
            "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
            "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
            "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
            "</Types>";

        var relsXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "</Relationships>";

        var workbookXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
            $"<sheets><sheet name=\"{EscapeXml(sheetName)}\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
            "</workbook>";

        var workbookRelsXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
            "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
            "</Relationships>";

        var stylesXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<fonts count=\"1\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
            "<fills count=\"1\"><fill><patternFill patternType=\"none\"/></fill></fills>" +
            "<borders count=\"1\"><border/></borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            "<cellXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/></cellXfs>" +
            "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
            "</styleSheet>";

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", contentTypesXml);
            WriteEntry(archive, "_rels/.rels", relsXml);
            WriteEntry(archive, "xl/workbook.xml", workbookXml);
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", workbookRelsXml);
            WriteEntry(archive, "xl/worksheets/sheet1.xml", sheetXml);
            WriteEntry(archive, "xl/styles.xml", stylesXml);
        }

        return stream.ToArray();
    }

    private static string BuildCsv(IReadOnlyCollection<PersonnelFileExportRow> rows)
    {
        var lines = new List<string>
        {
            "Id,RecordType,FirstName,LastName,FullName,BirthDate,Age,MaritalStatus,Profession,Nationality,PersonalEmail,InstitutionalEmail,PersonalPhone,InstitutionalPhone,OrgUnitId,IsActive,CreatedAtUtc,ModifiedAtUtc"
        };

        lines.AddRange(rows.Select(row => string.Join(",",
            EscapeCsv(row.Id.ToString()),
            EscapeCsv(row.RecordType.ToString()),
            EscapeCsv(row.FirstName),
            EscapeCsv(row.LastName),
            EscapeCsv(row.FullName),
            EscapeCsv(row.BirthDate.ToString("O", CultureInfo.InvariantCulture)),
            row.Age.ToString(CultureInfo.InvariantCulture),
            EscapeCsv(row.MaritalStatus),
            EscapeCsv(row.Profession),
            EscapeCsv(row.Nationality),
            EscapeCsv(row.PersonalEmail),
            EscapeCsv(row.InstitutionalEmail),
            EscapeCsv(row.PersonalPhone),
            EscapeCsv(row.InstitutionalPhone),
            EscapeCsv(row.OrgUnitId?.ToString()),
            row.IsActive ? "true" : "false",
            EscapeCsv(row.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture)),
            EscapeCsv(row.ModifiedAtUtc?.ToString("O", CultureInfo.InvariantCulture)))));

        return string.Join("\n", lines);
    }

    private static byte[] BuildXlsx(IReadOnlyCollection<PersonnelFileExportRow> rows)
    {
        static string Cell(string? value) =>
            $"<c t=\"inlineStr\"><is><t>{EscapeXml(value)}</t></is></c>";

        var headers = new[]
        {
            "Id",
            "RecordType",
            "FirstName",
            "LastName",
            "FullName",
            "BirthDate",
            "Age",
            "MaritalStatus",
            "Profession",
            "Nationality",
            "PersonalEmail",
            "InstitutionalEmail",
            "PersonalPhone",
            "InstitutionalPhone",
            "OrgUnitId",
            "IsActive",
            "CreatedAtUtc",
            "ModifiedAtUtc"
        };

        var sheetRows = new StringBuilder();
        sheetRows.Append("<row r=\"1\">");
        foreach (var header in headers)
        {
            sheetRows.Append(Cell(header));
        }

        sheetRows.Append("</row>");

        var rowIndex = 2;
        foreach (var row in rows)
        {
            sheetRows.Append($"<row r=\"{rowIndex++}\">");
            sheetRows.Append(Cell(row.Id.ToString()));
            sheetRows.Append(Cell(row.RecordType.ToString()));
            sheetRows.Append(Cell(row.FirstName));
            sheetRows.Append(Cell(row.LastName));
            sheetRows.Append(Cell(row.FullName));
            sheetRows.Append(Cell(row.BirthDate.ToString("O", CultureInfo.InvariantCulture)));
            sheetRows.Append(Cell(row.Age.ToString(CultureInfo.InvariantCulture)));
            sheetRows.Append(Cell(row.MaritalStatus));
            sheetRows.Append(Cell(row.Profession));
            sheetRows.Append(Cell(row.Nationality));
            sheetRows.Append(Cell(row.PersonalEmail));
            sheetRows.Append(Cell(row.InstitutionalEmail));
            sheetRows.Append(Cell(row.PersonalPhone));
            sheetRows.Append(Cell(row.InstitutionalPhone));
            sheetRows.Append(Cell(row.OrgUnitId?.ToString()));
            sheetRows.Append(Cell(row.IsActive ? "true" : "false"));
            sheetRows.Append(Cell(row.CreatedAtUtc.ToString("O", CultureInfo.InvariantCulture)));
            sheetRows.Append(Cell(row.ModifiedAtUtc?.ToString("O", CultureInfo.InvariantCulture)));
            sheetRows.Append("</row>");
        }

        var sheetXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<sheetData>" + sheetRows + "</sheetData>" +
            "</worksheet>";

        var contentTypesXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
            "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
            "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
            "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
            "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
            "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
            "</Types>";

        var relsXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
            "</Relationships>";

        var workbookXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
            "<sheets><sheet name=\"PersonnelFiles\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
            "</workbook>";

        var workbookRelsXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
            "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
            "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
            "</Relationships>";

        var stylesXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
            "<fonts count=\"1\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
            "<fills count=\"1\"><fill><patternFill patternType=\"none\"/></fill></fills>" +
            "<borders count=\"1\"><border/></borders>" +
            "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
            "<cellXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/></cellXfs>" +
            "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
            "</styleSheet>";

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", contentTypesXml);
            WriteEntry(archive, "_rels/.rels", relsXml);
            WriteEntry(archive, "xl/workbook.xml", workbookXml);
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", workbookRelsXml);
            WriteEntry(archive, "xl/worksheets/sheet1.xml", sheetXml);
            WriteEntry(archive, "xl/styles.xml", stylesXml);
        }

        return stream.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        using var entryStream = entry.Open();
        using var writer = new StreamWriter(entryStream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        var escaped = value.Replace("\"", "\"\"");
        return needsQuotes ? $"\"{escaped}\"" : escaped;
    }

    private static string EscapeXml(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return SecurityElement.Escape(value) ?? string.Empty;
    }

    public sealed record CreatePersonnelFileRequest(
        PersonnelFileRecordType RecordType,
        string FirstName,
        string LastName,
        DateTime BirthDate,
        string? MaritalStatus,
        string? Profession,
        string? Nationality,
        string? PersonalEmail,
        string? InstitutionalEmail,
        string? PersonalPhone,
        string? InstitutionalPhone,
        string? BirthCountry,
        string? BirthDepartment,
        string? BirthMunicipality,
        string? PhotoUrl,
        Guid? OrgUnitId,
        string? CustomDataJson,
        IReadOnlyCollection<IdentificationItemRequest> Identifications);

    public sealed record UpdatePersonnelFilePersonalInfoRequest(
        PersonnelFileRecordType RecordType,
        string FirstName,
        string LastName,
        DateTime BirthDate,
        string? MaritalStatus,
        string? Profession,
        string? Nationality,
        string? PersonalEmail,
        string? InstitutionalEmail,
        string? PersonalPhone,
        string? InstitutionalPhone,
        string? BirthCountry,
        string? BirthDepartment,
        string? BirthMunicipality,
        string? PhotoUrl,
        Guid? OrgUnitId,
        string? CustomDataJson,
        Guid ConcurrencyToken);

    public sealed record HirePersonnelFileRequest(
        string EmployeeCode,
        string EmploymentStatusCode,
        bool IsEmploymentActive,
        string ContractTypeCode,
        DateTime HireDate,
        string? WorkdayCode,
        string? PayrollTypeCode,
        Guid ConcurrencyToken);

    public sealed record UpdatePersonnelFileEmployeeProfileRequest(
        string EmployeeCode,
        string EmploymentStatusCode,
        bool IsEmploymentActive,
        string ContractTypeCode,
        DateTime HireDate,
        string? RetirementCategoryCode,
        string? RetirementReasonCode,
        string? RetirementNotes,
        DateTime? RetirementDate,
        string? WorkdayCode,
        string? PayrollTypeCode,
        Guid? PositionSlotId,
        Guid? JobProfileId,
        Guid? OrgUnitId,
        Guid? WorkCenterId,
        Guid? CostCenterId,
        DateTime? ContractStartDate,
        DateTime? ContractEndDate,
        string? VacationConfigurationJson,
        Guid ConcurrencyToken);

    public sealed record EmploymentAssignmentItemRequest(
        string AssignmentTypeCode,
        Guid? PositionSlotId,
        Guid? OrgUnitId,
        Guid? WorkCenterId,
        Guid? CostCenterId,
        DateTime StartDate,
        DateTime? EndDate,
        bool IsPrimary,
        bool IsActive,
        string? Notes);

    public sealed record ReplaceEmploymentAssignmentsRequest(IReadOnlyCollection<EmploymentAssignmentItemRequest> Items, Guid ConcurrencyToken);

    public sealed record ContractHistoryItemRequest(
        string ContractTypeCode,
        DateTime ContractDate,
        DateTime? ContractEndDate,
        Guid? PositionSlotId,
        string? Notes);

    public sealed record ReplaceContractHistoryRequest(IReadOnlyCollection<ContractHistoryItemRequest> Items, Guid ConcurrencyToken);

    public sealed record SalaryItemItemRequest(
        string IncomeTypeCode,
        string SalaryRubricCode,
        string CurrencyCode,
        string PayPeriodCode,
        decimal Amount,
        DateTime StartDate,
        DateTime? EndDate,
        bool IsActive);

    public sealed record ReplaceSalaryItemsRequest(IReadOnlyCollection<SalaryItemItemRequest> Items, Guid ConcurrencyToken);

    public sealed record AdditionalBenefitItemRequest(
        string BenefitTypeCode,
        DateTime? StartDate,
        DateTime? EndDate,
        bool IsActive,
        string? Notes);

    public sealed record ReplaceAdditionalBenefitsRequest(IReadOnlyCollection<AdditionalBenefitItemRequest> Items, Guid ConcurrencyToken);

    public sealed record PaymentMethodItemRequest(
        string PaymentMethodCode,
        Guid? BankAccountId,
        bool IsPrimary,
        bool IsActive,
        DateTime EffectiveFromUtc,
        DateTime? EffectiveToUtc,
        string? Notes);

    public sealed record ReplacePaymentMethodsRequest(IReadOnlyCollection<PaymentMethodItemRequest> Items, Guid ConcurrencyToken);

    public sealed record AuthorizationSubstitutionItemRequest(
        string SubstitutionTypeCode,
        Guid SubstitutePersonnelFileId,
        string? SubstitutePositionTitle,
        DateTime StartDate,
        DateTime? EndDate,
        bool IsActive,
        string? Notes);

    public sealed record ReplaceAuthorizationSubstitutionsRequest(
        IReadOnlyCollection<AuthorizationSubstitutionItemRequest> Items,
        Guid ConcurrencyToken);

    public sealed record AddPersonnelActionRequest(
        string ActionTypeCode,
        string ActionStatusCode,
        DateTime ActionDateUtc,
        DateTime? EffectiveFromUtc,
        DateTime? EffectiveToUtc,
        string? Description,
        string? Reference,
        decimal? Amount,
        string? CurrencyCode,
        Guid ConcurrencyToken);

    public sealed record PayrollTransactionItemRequest(
        string TransactionTypeCode,
        DateTime TransactionDateUtc,
        string PayrollPeriodCode,
        string? Description,
        decimal Amount,
        string CurrencyCode,
        bool IsDebit,
        string? SourceSystem,
        string? SourceReference,
        DateTime? SourceSyncedUtc);

    public sealed record ReplacePayrollTransactionsRequest(IReadOnlyCollection<PayrollTransactionItemRequest> Items, Guid ConcurrencyToken);

    public sealed record AssetAccessItemRequest(
        string AssetTypeCode,
        string AssetOrAccessName,
        string? AccessLevelCode,
        DateTime StartDateUtc,
        DateTime? EndDateUtc,
        DateTime? DeliveryDateUtc,
        string? DeliveryStatusCode,
        bool IsActive,
        string? Notes);

    public sealed record ReplaceAssetsAccessesRequest(IReadOnlyCollection<AssetAccessItemRequest> Items, Guid ConcurrencyToken);

    public sealed record InsuranceBeneficiaryItemRequest(
        string FullName,
        string? DocumentNumber,
        DateTime? BirthDate,
        string KinshipCode);

    public sealed record InsuranceItemRequest(
        string InsuranceCode,
        decimal? EmployeeContribution,
        decimal? EmployerContribution,
        string? RangeCode,
        string? PolicyNumber,
        decimal? InsuredAmount,
        string? CurrencyCode,
        bool IsActive,
        DateTime? StartDateUtc,
        DateTime? EndDateUtc,
        IReadOnlyCollection<InsuranceBeneficiaryItemRequest> Beneficiaries);

    public sealed record ReplaceInsurancesRequest(IReadOnlyCollection<InsuranceItemRequest> Items, Guid ConcurrencyToken);

    public sealed record MedicalClaimItemRequest(
        Guid? InsuranceId,
        string? AccountNumber,
        string ClaimTypeCode,
        string? Diagnosis,
        decimal? ClaimAmount,
        string? CurrencyCode,
        decimal? PaidAmount,
        int? ResponseTimeDays,
        string? Notes,
        DateTime ClaimDateUtc,
        string? SourceSystem,
        string? SourceReference,
        DateTime? SourceSyncedUtc);

    public sealed record ReplaceMedicalClaimsRequest(IReadOnlyCollection<MedicalClaimItemRequest> Items, Guid ConcurrencyToken);

    public sealed record PerformanceEvaluationItemRequest(
        string EvaluatorName,
        DateTime EvaluationDateUtc,
        decimal? Score,
        string? QualitativeScoreCode,
        string? Comment,
        string? SourceSystem,
        string? SourceReference,
        DateTime? SourceSyncedUtc);

    public sealed record ReplacePerformanceEvaluationsRequest(IReadOnlyCollection<PerformanceEvaluationItemRequest> Items, Guid ConcurrencyToken);

    public sealed record PositionCompetencyResultItemRequest(
        string CompetencyCode,
        string? DesiredBehaviors,
        decimal? ExpectedScore,
        decimal? AchievedScore,
        decimal? GapScore,
        DateTime? EvaluationDateUtc,
        string? SourceSystem,
        string? SourceReference,
        DateTime? SourceSyncedUtc);

    public sealed record ReplacePositionCompetencyResultsRequest(
        IReadOnlyCollection<PositionCompetencyResultItemRequest> Items,
        Guid ConcurrencyToken);

    public sealed record SelectionContestItemRequest(
        string ContestCode,
        string ContestName,
        DateTime ContestDateUtc,
        string ResultCode,
        string? Notes,
        string? SourceSystem,
        string? SourceReference,
        DateTime? SourceSyncedUtc);

    public sealed record ReplaceSelectionContestsRequest(IReadOnlyCollection<SelectionContestItemRequest> Items, Guid ConcurrencyToken);

    public sealed record CurricularCompetencyItemRequest(
        string RequirementTypeCode,
        string RequirementName,
        string CompetencyDomain,
        decimal? ExperienceTimeValue,
        string? MetricCode,
        string? Notes,
        string? SourceSystem,
        string? SourceReference,
        DateTime? SourceSyncedUtc);

    public sealed record ReplaceCurricularCompetenciesRequest(IReadOnlyCollection<CurricularCompetencyItemRequest> Items, Guid ConcurrencyToken);

    public sealed record IdentificationItemRequest(
        string IdentificationType,
        string IdentificationNumber,
        DateTime? IssuedDate,
        DateTime? ExpiryDate,
        string? Issuer,
        bool IsPrimary = false);

    public sealed record ReplaceIdentificationsRequest(
        IReadOnlyCollection<IdentificationItemRequest> Identifications,
        Guid ConcurrencyToken);

    public sealed record AddressItemRequest(
        string AddressLine,
        string? Country,
        string? Department,
        string? Municipality,
        string? PostalCode,
        bool IsCurrent = false);

    public sealed record ReplaceAddressesRequest(IReadOnlyCollection<AddressItemRequest> Addresses, Guid ConcurrencyToken);

    public sealed record EmergencyContactItemRequest(
        string Name,
        string Relationship,
        string Phone,
        string? Address,
        string? Workplace);

    public sealed record ReplaceEmergencyContactsRequest(IReadOnlyCollection<EmergencyContactItemRequest> Contacts, Guid ConcurrencyToken);

    public sealed record FamilyMemberItemRequest(
        string FirstName,
        string LastName,
        string Relationship,
        string? Nationality,
        DateTime? BirthDate,
        PersonnelFamilyMemberSex Sex,
        string? MaritalStatus,
        string? Occupation,
        string? DocumentType,
        string? DocumentNumber,
        string? Phone,
        bool IsStudying,
        string? StudyPlace,
        string? AcademicLevel,
        bool IsBeneficiary,
        bool IsWorking,
        string? Workplace,
        string? JobTitle,
        string? WorkPhone,
        decimal? Salary,
        bool IsDeceased,
        DateTime? DeceasedDate);

    public sealed record ReplaceFamilyMembersRequest(IReadOnlyCollection<FamilyMemberItemRequest> FamilyMembers, Guid ConcurrencyToken);

    public sealed record HobbyItemRequest(string HobbyName);

    public sealed record ReplaceHobbiesRequest(IReadOnlyCollection<HobbyItemRequest> Hobbies, Guid ConcurrencyToken);

    public sealed record EmployeeRelationItemRequest(string RelatedEmployeeName, string Relationship);

    public sealed record ReplaceEmployeeRelationsRequest(IReadOnlyCollection<EmployeeRelationItemRequest> Relations, Guid ConcurrencyToken);

    public sealed record BankAccountItemRequest(
        string BankCode,
        string CurrencyCode,
        string AccountNumber,
        string AccountTypeCode,
        bool IsPrimary = false);

    public sealed record ReplaceBankAccountsRequest(IReadOnlyCollection<BankAccountItemRequest> BankAccounts, Guid ConcurrencyToken);

    public sealed record AssociationItemRequest(
        string AssociationName,
        string? Role,
        DateTime? JoinedDate,
        DateTime? LeftDate,
        decimal? Payment);

    public sealed record ReplaceAssociationsRequest(IReadOnlyCollection<AssociationItemRequest> Associations, Guid ConcurrencyToken);

    public sealed record EducationItemRequest(
        string StatusCode,
        string? DegreeTitle,
        string StudyTypeCode,
        string Career,
        string Institution,
        string CountryCode,
        string? Specialty,
        bool IsCurrentlyStudying,
        DateTime StartDate,
        DateTime? EndDate,
        string? ShiftCode,
        string? ModalityCode,
        int? TotalSubjects,
        int? ApprovedSubjects);

    public sealed record ReplaceEducationsRequest(IReadOnlyCollection<EducationItemRequest> Educations, Guid ConcurrencyToken);

    public sealed record LanguageItemRequest(
        string LanguageCode,
        string LevelCode,
        bool Speaks,
        bool Writes,
        bool Reads);

    public sealed record ReplaceLanguagesRequest(IReadOnlyCollection<LanguageItemRequest> Languages, Guid ConcurrencyToken);

    public sealed record TrainingItemRequest(
        string TrainingName,
        string TrainingTypeCode,
        string? Description,
        string? Topic,
        string? Institution,
        string? Instructors,
        decimal? Score,
        DateTime StartDate,
        DateTime? EndDate,
        bool IsInternal,
        bool IsLocal,
        string CountryCode,
        decimal DurationValue,
        string DurationUnitCode,
        decimal? CostAmount,
        string? CostCurrencyCode);

    public sealed record ReplaceTrainingsRequest(IReadOnlyCollection<TrainingItemRequest> Trainings, Guid ConcurrencyToken);

    public sealed record PreviousEmploymentItemRequest(
        string Institution,
        string? Place,
        string? LastPosition,
        string? ManagerName,
        DateTime EntryDate,
        DateTime? RetirementDate,
        string? CompanyPhone,
        string? ExitReason,
        decimal? FirstSalaryAmount,
        decimal? LastSalaryAmount,
        decimal? AverageCommissionAmount,
        string CurrencyCode);

    public sealed record ReplacePreviousEmploymentsRequest(
        IReadOnlyCollection<PreviousEmploymentItemRequest> PreviousEmployments,
        Guid ConcurrencyToken);

    public sealed record ReferenceItemRequest(
        string PersonName,
        string? Address,
        string Phone,
        string ReferenceTypeCode,
        string? Occupation,
        string? Workplace,
        string? WorkPhone,
        decimal KnownTimeYears);

    public sealed record ReplaceReferencesRequest(IReadOnlyCollection<ReferenceItemRequest> References, Guid ConcurrencyToken);

    public sealed record DynamicPersonnelFileFilterRequest(
        string Field,
        string Operator,
        string? Value,
        string? ValueTo,
        IReadOnlyCollection<string>? Values);

    public sealed record DynamicPersonnelFileSortRequest(
        string Field,
        PersonnelFileSortDirection Direction = PersonnelFileSortDirection.Asc);

    public sealed record DynamicQueryPersonnelFilesRequest(
        IReadOnlyCollection<DynamicPersonnelFileFilterRequest>? Filters,
        IReadOnlyCollection<string>? GroupBy,
        IReadOnlyCollection<DynamicPersonnelFileSortRequest>? Sort,
        string? Q,
        int Page = 1,
        int PageSize = PersonnelFileValidationRules.DefaultPageSize,
        bool IncludeAllowedActions = false);

    public sealed record UploadPersonnelFileDocumentRequest(
        string DocumentType,
        string? Observations,
        DateTime? DeliveryDate,
        DateTime? LoanDate,
        DateTime? ReturnDate,
        Guid ConcurrencyToken,
        IFormFile File);

    public sealed record ConcurrencyRequest(Guid ConcurrencyToken);

    public sealed record AddObservationRequest(string Note, Guid ConcurrencyToken);

    public sealed record CreateCustomFieldDefinitionRequest(
        string Key,
        string Label,
        PersonnelCustomFieldType FieldType,
        bool IsRequired,
        bool IsActive,
        string? OptionsJson,
        int SortOrder);

    public sealed record UpdateCustomFieldDefinitionRequest(
        string Key,
        string Label,
        PersonnelCustomFieldType FieldType,
        bool IsRequired,
        bool IsActive,
        string? OptionsJson,
        int SortOrder,
        Guid ConcurrencyToken);
}

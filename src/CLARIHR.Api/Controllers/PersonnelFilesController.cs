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

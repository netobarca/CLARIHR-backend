using CLARIHR.Api.Common;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.PersonnelFiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
public sealed class PersonnelFileProfileController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/personnel-files/{id:guid}/personal-info")]
    [ProducesResponseType<PersonnelFilePersonalInfoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PersonnelFilePersonalInfoResponse>> GetPersonalInfo(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFilePersonalInfoQuery(id), cancellationToken);
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
                request.MaritalStatusCode,
                request.ProfessionCode,
                request.Nationality,
                request.PersonalEmail,
                request.InstitutionalEmail,
                request.PersonalPhone,
                request.InstitutionalPhone,
                request.BirthCountryCode,
                request.BirthDepartmentCode,
                request.BirthMunicipalityCode,
                request.PhotoUrl,
                request.OrgUnitPublicId,
                request.AssignedPositionSlotPublicId,
                request.CustomDataJson,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{id:guid}/identifications")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileIdentificationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileIdentificationResponse>>> GetIdentifications(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileIdentificationsQuery(id), cancellationToken);
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
                    item.IdentificationTypeCode,
                    item.IdentificationNumber,
                    item.IssuedDate,
                    item.ExpiryDate,
                    item.Issuer,
                    item.IsPrimary)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{id:guid}/addresses")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileAddressResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileAddressResponse>>> GetAddresses(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileAddressesQuery(id), cancellationToken);
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

    [HttpGet("api/v1/personnel-files/{id:guid}/emergency-contacts")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileEmergencyContactResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileEmergencyContactResponse>>> GetEmergencyContacts(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileEmergencyContactsQuery(id), cancellationToken);
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

    [HttpGet("api/v1/personnel-files/{id:guid}/family-members")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileFamilyMemberResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileFamilyMemberResponse>>> GetFamilyMembers(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileFamilyMembersQuery(id), cancellationToken);
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

    [HttpGet("api/v1/personnel-files/{id:guid}/hobbies")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileHobbyResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileHobbyResponse>>> GetHobbies(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileHobbiesQuery(id), cancellationToken);
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

    [HttpGet("api/v1/personnel-files/{id:guid}/employee-relations")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileEmployeeRelationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileEmployeeRelationResponse>>> GetEmployeeRelations(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileEmployeeRelationsQuery(id), cancellationToken);
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

    [HttpGet("api/v1/personnel-files/{id:guid}/associations")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileAssociationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileAssociationResponse>>> GetAssociations(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileAssociationsQuery(id), cancellationToken);
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

    [HttpGet("api/v1/personnel-files/{id:guid}/educations")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileEducationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileEducationResponse>>> GetEducations(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileEducationsQuery(id), cancellationToken);
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

    [HttpGet("api/v1/personnel-files/{id:guid}/languages")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileLanguageResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileLanguageResponse>>> GetLanguages(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileLanguagesQuery(id), cancellationToken);
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

    [HttpGet("api/v1/personnel-files/{id:guid}/trainings")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileTrainingResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileTrainingResponse>>> GetTrainings(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileTrainingsQuery(id), cancellationToken);
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

    [HttpGet("api/v1/personnel-files/{id:guid}/previous-employments")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFilePreviousEmploymentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFilePreviousEmploymentResponse>>> GetPreviousEmployments(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFilePreviousEmploymentsQuery(id), cancellationToken);
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

    [HttpGet("api/v1/personnel-files/{id:guid}/references")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileReferenceResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileReferenceResponse>>> GetReferences(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileReferencesQuery(id), cancellationToken);
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
}

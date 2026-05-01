using CLARIHR.Api.Common;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
public sealed class PersonnelFilePersonalInfoController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    // ─── Personal Info ────────────────────────────────────────────────────────

    [HttpGet("api/v1/personnel-files/{publicId:guid}/personal-info")]
    [ProducesResponseType<PersonnelFilePersonalInfoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PersonnelFilePersonalInfoResponse>> GetPersonalInfo(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFilePersonalInfoQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/personal-info")]
    [ProducesResponseType<PersonnelFileSectionResult<PersonnelFilePersonalInfoResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonnelFileSectionResult<PersonnelFilePersonalInfoResponse>>> UpdatePersonalInfo(
        Guid publicId,
        [FromBody] UpdatePersonnelFilePersonalInfoRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFilePersonalInfoCommand(
                publicId,
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
                request.PhotoFilePublicId,
                request.OrgUnitPublicId,
                request.AssignedPositionSlotPublicId,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    // ─── Identifications ──────────────────────────────────────────────────────

    [HttpGet("api/v1/personnel-files/{publicId:guid}/identifications")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileIdentificationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileIdentificationResponse>>> GetIdentifications(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileIdentificationsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/identifications")]
    [ProducesResponseType<PersonnelFileIdentificationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileIdentificationResponse>> AddIdentification(
        Guid publicId,
        [FromBody] AddIdentificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileIdentificationCommand(
                publicId,
                new IdentificationInput(
                    request.IdentificationTypeCode,
                    request.IdentificationNumber,
                    request.IssuedDate,
                    request.ExpiryDate,
                    request.Issuer,
                    request.IsPrimary),
                request.ConcurrencyToken),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<PersonnelFileIdentificationResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/identifications/{itemPublicId:guid}")]
    [ProducesResponseType<PersonnelFileIdentificationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonnelFileIdentificationResponse>> UpdateIdentification(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] UpdateIdentificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileIdentificationCommand(
                publicId,
                itemPublicId,
                new IdentificationInput(
                    request.IdentificationTypeCode,
                    request.IdentificationNumber,
                    request.IssuedDate,
                    request.ExpiryDate,
                    request.Issuer,
                    request.IsPrimary),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/identifications/{itemPublicId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteIdentification(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileIdentificationCommand(publicId, itemPublicId, request.ConcurrencyToken),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(result).Result!
            : NoContent();
    }

    // ─── Addresses ────────────────────────────────────────────────────────────

    [HttpGet("api/v1/personnel-files/{publicId:guid}/addresses")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileAddressResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileAddressResponse>>> GetAddresses(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileAddressesQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/addresses")]
    [ProducesResponseType<PersonnelFileAddressResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileAddressResponse>> AddAddress(
        Guid publicId,
        [FromBody] AddAddressRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileAddressCommand(
                publicId,
                new AddressInput(
                    request.AddressLine,
                    request.Country,
                    request.Department,
                    request.Municipality,
                    request.PostalCode,
                    request.IsCurrent),
                request.ConcurrencyToken),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<PersonnelFileAddressResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/addresses/{itemPublicId:guid}")]
    [ProducesResponseType<PersonnelFileAddressResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileAddressResponse>> UpdateAddress(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] UpdateAddressRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileAddressCommand(
                publicId,
                itemPublicId,
                new AddressInput(
                    request.AddressLine,
                    request.Country,
                    request.Department,
                    request.Municipality,
                    request.PostalCode,
                    request.IsCurrent),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/addresses/{itemPublicId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteAddress(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileAddressCommand(publicId, itemPublicId, request.ConcurrencyToken),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(result).Result!
            : NoContent();
    }

    // ─── Emergency Contacts ───────────────────────────────────────────────────

    [HttpGet("api/v1/personnel-files/{publicId:guid}/emergency-contacts")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileEmergencyContactResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileEmergencyContactResponse>>> GetEmergencyContacts(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileEmergencyContactsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/emergency-contacts")]
    [ProducesResponseType<PersonnelFileEmergencyContactResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileEmergencyContactResponse>> AddEmergencyContact(
        Guid publicId,
        [FromBody] AddEmergencyContactRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileEmergencyContactCommand(
                publicId,
                new EmergencyContactInput(
                    request.Name,
                    request.Relationship,
                    request.Phone,
                    request.Address,
                    request.Workplace),
                request.ConcurrencyToken),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<PersonnelFileEmergencyContactResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/emergency-contacts/{itemPublicId:guid}")]
    [ProducesResponseType<PersonnelFileEmergencyContactResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileEmergencyContactResponse>> UpdateEmergencyContact(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] UpdateEmergencyContactRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileEmergencyContactCommand(
                publicId,
                itemPublicId,
                new EmergencyContactInput(
                    request.Name,
                    request.Relationship,
                    request.Phone,
                    request.Address,
                    request.Workplace),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/emergency-contacts/{itemPublicId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteEmergencyContact(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileEmergencyContactCommand(publicId, itemPublicId, request.ConcurrencyToken),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(result).Result!
            : NoContent();
    }

    // ─── Family Members ───────────────────────────────────────────────────────

    [HttpGet("api/v1/personnel-files/{publicId:guid}/family-members")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileFamilyMemberResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileFamilyMemberResponse>>> GetFamilyMembers(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileFamilyMembersQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/family-members")]
    [ProducesResponseType<PersonnelFileFamilyMemberResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileFamilyMemberResponse>> AddFamilyMember(
        Guid publicId,
        [FromBody] AddFamilyMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileFamilyMemberCommand(
                publicId,
                new FamilyMemberInput(
                    request.FirstName,
                    request.LastName,
                    request.KinshipCode,
                    request.Nationality,
                    request.BirthDate,
                    request.Sex,
                    request.MaritalStatus,
                    request.Occupation,
                    request.DocumentType,
                    request.DocumentNumber,
                    request.Phone,
                    request.IsStudying,
                    request.StudyPlace,
                    request.AcademicLevel,
                    request.IsBeneficiary,
                    request.IsWorking,
                    request.Workplace,
                    request.JobTitle,
                    request.WorkPhone,
                    request.Salary,
                    request.IsDeceased,
                    request.DeceasedDate),
                request.ConcurrencyToken),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<PersonnelFileFamilyMemberResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/family-members/{itemPublicId:guid}")]
    [ProducesResponseType<PersonnelFileFamilyMemberResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileFamilyMemberResponse>> UpdateFamilyMember(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] UpdateFamilyMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileFamilyMemberCommand(
                publicId,
                itemPublicId,
                new FamilyMemberInput(
                    request.FirstName,
                    request.LastName,
                    request.KinshipCode,
                    request.Nationality,
                    request.BirthDate,
                    request.Sex,
                    request.MaritalStatus,
                    request.Occupation,
                    request.DocumentType,
                    request.DocumentNumber,
                    request.Phone,
                    request.IsStudying,
                    request.StudyPlace,
                    request.AcademicLevel,
                    request.IsBeneficiary,
                    request.IsWorking,
                    request.Workplace,
                    request.JobTitle,
                    request.WorkPhone,
                    request.Salary,
                    request.IsDeceased,
                    request.DeceasedDate),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/family-members/{itemPublicId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteFamilyMember(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileFamilyMemberCommand(publicId, itemPublicId, request.ConcurrencyToken),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(result).Result!
            : NoContent();
    }
}

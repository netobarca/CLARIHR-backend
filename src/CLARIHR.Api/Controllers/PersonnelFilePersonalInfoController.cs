using Asp.Versioning;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}")]
[Consumes("application/json")]
[Produces("application/json")]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.Read, PersonnelFilePolicies.Manage)]
public sealed class PersonnelFilePersonalInfoController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    // ─── Personal Info ────────────────────────────────────────────────────────

    [HttpGet("personnel-files/{publicId:guid}/personal-info")]
    [ProducesResponseType<PersonnelFilePersonalInfoResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file's personal info",
        Description = """
            Returns the personal-info section of the specified personnel file. Personal-info
            writes are served by the canonical shell endpoints `PUT`/`PATCH`
            `/personnel-files/{publicId}` (PersonnelFilesController); this is a plain read.
            """)]
    public async Task<ActionResult<PersonnelFilePersonalInfoResponse>> GetPersonalInfo(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFilePersonalInfoQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    // ─── Identifications ──────────────────────────────────────────────────────

    [HttpGet("personnel-files/{publicId:guid}/identifications")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileIdentificationResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's identifications",
        Description = """
            Returns every identification entry recorded for the specified personnel file.
            Each item carries its own `concurrencyToken`, required in the `If-Match` header
            of subsequent `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileIdentificationResponse>>> GetIdentifications(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileIdentificationsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("personnel-files/{publicId:guid}/identifications/{identificationPublicId:guid}")]
    [ProducesResponseType<PersonnelFileIdentificationResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file identification by id",
        Description = """
            Returns a single identification entry of the specified personnel file. The
            `concurrencyToken` in the response is required in the `If-Match` header of
            subsequent `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<PersonnelFileIdentificationResponse>> GetIdentificationById(
        Guid publicId,
        Guid identificationPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileIdentificationByIdQuery(publicId, identificationPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("personnel-files/{publicId:guid}/identifications")]
    [ProducesResponseType<PersonnelFileIdentificationResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Add an identification to a personnel file",
        Description = """
            Creates a new identification entry under the specified personnel file and returns
            it with a `201 Created` response. The `Location` header points to the created
            resource and the `ETag` header carries its initial `concurrencyToken`.
            """)]
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
                    request.IsPrimary)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetIdentificationById),
            value => new { publicId, identificationPublicId = value.IdentificationPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("personnel-files/{publicId:guid}/identifications/{identificationPublicId:guid}")]
    [ProducesResponseType<PersonnelFileIdentificationResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace a personnel file identification",
        Description = """
            Replaces all fields of an existing identification entry. Requires the `If-Match`
            header with the current `concurrencyToken`; the new token is returned in the
            `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileIdentificationResponse>> UpdateIdentification(
        Guid publicId,
        Guid identificationPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateIdentificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileIdentificationCommand(
                publicId,
                identificationPublicId,
                new IdentificationInput(
                    request.IdentificationTypeCode,
                    request.IdentificationNumber,
                    request.IssuedDate,
                    request.ExpiryDate,
                    request.Issuer,
                    request.IsPrimary),
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("personnel-files/{publicId:guid}/identifications/{identificationPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<PersonnelFileIdentificationResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a personnel file identification",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type
            `application/json-patch+json`) to an existing identification entry. Requires the
            `If-Match` header with the current `concurrencyToken`; the new token is returned
            in the `ETag` header. Mutable members are the identification input fields.
            """)]
    public async Task<ActionResult<PersonnelFileIdentificationResponse>> PatchIdentification(
        Guid publicId,
        Guid identificationPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchIdentificationRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchPersonnelFileIdentificationCommand(
                publicId,
                identificationPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new PersonnelFileIdentificationPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("personnel-files/{publicId:guid}/identifications/{identificationPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove an identification from a personnel file",
        Description = """
            Deletes the specified identification entry. Requires the `If-Match` header with
            the current `concurrencyToken`. Returns the parent personnel file's refreshed
            concurrency token so the caller can keep mutating without an extra round-trip.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteIdentification(
        Guid publicId,
        Guid identificationPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileIdentificationCommand(publicId, identificationPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    // ─── Addresses ────────────────────────────────────────────────────────────

    [HttpGet("personnel-files/{publicId:guid}/addresses")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileAddressResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's addresses",
        Description = """
            Returns every address entry recorded for the specified personnel file. Each item
            carries its own `concurrencyToken`, required in the `If-Match` header of
            subsequent `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileAddressResponse>>> GetAddresses(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileAddressesQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("personnel-files/{publicId:guid}/addresses/{addressPublicId:guid}")]
    [ProducesResponseType<PersonnelFileAddressResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file address by id",
        Description = """
            Returns a single address entry of the specified personnel file. The
            `concurrencyToken` in the response is required in the `If-Match` header of
            subsequent `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<PersonnelFileAddressResponse>> GetAddressById(
        Guid publicId,
        Guid addressPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileAddressByIdQuery(publicId, addressPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("personnel-files/{publicId:guid}/addresses")]
    [ProducesResponseType<PersonnelFileAddressResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Add an address to a personnel file",
        Description = """
            Creates a new address entry under the specified personnel file and returns it with
            a `201 Created` response. The `Location` header points to the created resource and
            the `ETag` header carries its initial `concurrencyToken`.
            """)]
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
                    request.IsCurrent)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetAddressById),
            value => new { publicId, addressPublicId = value.AddressPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("personnel-files/{publicId:guid}/addresses/{addressPublicId:guid}")]
    [ProducesResponseType<PersonnelFileAddressResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace a personnel file address",
        Description = """
            Replaces all fields of an existing address entry. Requires the `If-Match` header
            with the current `concurrencyToken`; the new token is returned in the `ETag`
            header.
            """)]
    public async Task<ActionResult<PersonnelFileAddressResponse>> UpdateAddress(
        Guid publicId,
        Guid addressPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateAddressRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileAddressCommand(
                publicId,
                addressPublicId,
                new AddressInput(
                    request.AddressLine,
                    request.Country,
                    request.Department,
                    request.Municipality,
                    request.PostalCode,
                    request.IsCurrent),
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("personnel-files/{publicId:guid}/addresses/{addressPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<PersonnelFileAddressResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a personnel file address",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type
            `application/json-patch+json`) to an existing address entry. Requires the
            `If-Match` header with the current `concurrencyToken`; the new token is returned
            in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileAddressResponse>> PatchAddress(
        Guid publicId,
        Guid addressPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchAddressRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchPersonnelFileAddressCommand(
                publicId,
                addressPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new PersonnelFileAddressPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("personnel-files/{publicId:guid}/addresses/{addressPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove an address from a personnel file",
        Description = """
            Deletes the specified address entry. Requires the `If-Match` header with the
            current `concurrencyToken`. Returns the parent personnel file's refreshed
            concurrency token so the caller can keep mutating without an extra round-trip.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteAddress(
        Guid publicId,
        Guid addressPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileAddressCommand(publicId, addressPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    // ─── Emergency Contacts ───────────────────────────────────────────────────

    [HttpGet("personnel-files/{publicId:guid}/emergency-contacts")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileEmergencyContactResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's emergency contacts",
        Description = """
            Returns every emergency contact entry recorded for the specified personnel file.
            Each item carries its own `concurrencyToken`, required in the `If-Match` header of
            subsequent `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileEmergencyContactResponse>>> GetEmergencyContacts(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileEmergencyContactsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("personnel-files/{publicId:guid}/emergency-contacts/{emergencyContactPublicId:guid}")]
    [ProducesResponseType<PersonnelFileEmergencyContactResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file emergency contact by id",
        Description = """
            Returns a single emergency contact entry of the specified personnel file. The
            `concurrencyToken` in the response is required in the `If-Match` header of
            subsequent `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<PersonnelFileEmergencyContactResponse>> GetEmergencyContactById(
        Guid publicId,
        Guid emergencyContactPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileEmergencyContactByIdQuery(publicId, emergencyContactPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("personnel-files/{publicId:guid}/emergency-contacts")]
    [ProducesResponseType<PersonnelFileEmergencyContactResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Add an emergency contact to a personnel file",
        Description = """
            Creates a new emergency contact entry under the specified personnel file and
            returns it with a `201 Created` response. The `Location` header points to the
            created resource and the `ETag` header carries its initial `concurrencyToken`.
            """)]
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
                    request.Workplace)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetEmergencyContactById),
            value => new { publicId, emergencyContactPublicId = value.EmergencyContactPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("personnel-files/{publicId:guid}/emergency-contacts/{emergencyContactPublicId:guid}")]
    [ProducesResponseType<PersonnelFileEmergencyContactResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace a personnel file emergency contact",
        Description = """
            Replaces all fields of an existing emergency contact entry. Requires the
            `If-Match` header with the current `concurrencyToken`; the new token is returned
            in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileEmergencyContactResponse>> UpdateEmergencyContact(
        Guid publicId,
        Guid emergencyContactPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateEmergencyContactRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileEmergencyContactCommand(
                publicId,
                emergencyContactPublicId,
                new EmergencyContactInput(
                    request.Name,
                    request.Relationship,
                    request.Phone,
                    request.Address,
                    request.Workplace),
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("personnel-files/{publicId:guid}/emergency-contacts/{emergencyContactPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<PersonnelFileEmergencyContactResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a personnel file emergency contact",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type
            `application/json-patch+json`) to an existing emergency contact entry. Requires
            the `If-Match` header with the current `concurrencyToken`; the new token is
            returned in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileEmergencyContactResponse>> PatchEmergencyContact(
        Guid publicId,
        Guid emergencyContactPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchEmergencyContactRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchPersonnelFileEmergencyContactCommand(
                publicId,
                emergencyContactPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new PersonnelFileEmergencyContactPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("personnel-files/{publicId:guid}/emergency-contacts/{emergencyContactPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove an emergency contact from a personnel file",
        Description = """
            Deletes the specified emergency contact entry. Requires the `If-Match` header with
            the current `concurrencyToken`. Returns the parent personnel file's refreshed
            concurrency token so the caller can keep mutating without an extra round-trip.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteEmergencyContact(
        Guid publicId,
        Guid emergencyContactPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileEmergencyContactCommand(publicId, emergencyContactPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    // ─── Family Members ───────────────────────────────────────────────────────

    [HttpGet("personnel-files/{publicId:guid}/family-members")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileFamilyMemberResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's family members",
        Description = """
            Returns every family member entry recorded for the specified personnel file. Each
            item carries its own `concurrencyToken`, required in the `If-Match` header of
            subsequent `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileFamilyMemberResponse>>> GetFamilyMembers(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileFamilyMembersQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("personnel-files/{publicId:guid}/family-members/{familyMemberPublicId:guid}")]
    [ProducesResponseType<PersonnelFileFamilyMemberResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file family member by id",
        Description = """
            Returns a single family member entry of the specified personnel file. The
            `concurrencyToken` in the response is required in the `If-Match` header of
            subsequent `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<PersonnelFileFamilyMemberResponse>> GetFamilyMemberById(
        Guid publicId,
        Guid familyMemberPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileFamilyMemberByIdQuery(publicId, familyMemberPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("personnel-files/{publicId:guid}/family-members")]
    [ProducesResponseType<PersonnelFileFamilyMemberResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Add a family member to a personnel file",
        Description = """
            Creates a new family member entry under the specified personnel file and returns
            it with a `201 Created` response. The `Location` header points to the created
            resource and the `ETag` header carries its initial `concurrencyToken`.
            """)]
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
                    request.DeceasedDate)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetFamilyMemberById),
            value => new { publicId, familyMemberPublicId = value.FamilyMemberPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("personnel-files/{publicId:guid}/family-members/{familyMemberPublicId:guid}")]
    [ProducesResponseType<PersonnelFileFamilyMemberResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace a personnel file family member",
        Description = """
            Replaces all fields of an existing family member entry. Requires the `If-Match`
            header with the current `concurrencyToken`; the new token is returned in the
            `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileFamilyMemberResponse>> UpdateFamilyMember(
        Guid publicId,
        Guid familyMemberPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateFamilyMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileFamilyMemberCommand(
                publicId,
                familyMemberPublicId,
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
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("personnel-files/{publicId:guid}/family-members/{familyMemberPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<PersonnelFileFamilyMemberResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a personnel file family member",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type
            `application/json-patch+json`) to an existing family member entry. Requires the
            `If-Match` header with the current `concurrencyToken`; the new token is returned
            in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileFamilyMemberResponse>> PatchFamilyMember(
        Guid publicId,
        Guid familyMemberPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchFamilyMemberRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchPersonnelFileFamilyMemberCommand(
                publicId,
                familyMemberPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new PersonnelFileFamilyMemberPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("personnel-files/{publicId:guid}/family-members/{familyMemberPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove a family member from a personnel file",
        Description = """
            Deletes the specified family member entry. Requires the `If-Match` header with the
            current `concurrencyToken`. Returns the parent personnel file's refreshed
            concurrency token so the caller can keep mutating without an extra round-trip.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteFamilyMember(
        Guid publicId,
        Guid familyMemberPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileFamilyMemberCommand(publicId, familyMemberPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }
}

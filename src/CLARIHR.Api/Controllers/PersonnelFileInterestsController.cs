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
public sealed class PersonnelFileInterestsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    // ─── Hobbies ──────────────────────────────────────────────────────────────

    [HttpGet("personnel-files/{publicId:guid}/hobbies")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileHobbyResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's hobbies",
        Description = """
            Returns every hobby entry recorded for the specified personnel file. Each item
            carries its own `concurrencyToken`, required in the `If-Match` header of
            subsequent `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileHobbyResponse>>> GetHobbies(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileHobbiesQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("personnel-files/{publicId:guid}/hobbies/{hobbyPublicId:guid}")]
    [ProducesResponseType<PersonnelFileHobbyResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file hobby by id",
        Description = """
            Returns a single hobby entry of the specified personnel file. The
            `concurrencyToken` in the response is required in the `If-Match` header of
            subsequent `PUT`/`PATCH`/`DELETE` requests.
            """)]
    public async Task<ActionResult<PersonnelFileHobbyResponse>> GetHobbyById(
        Guid publicId,
        Guid hobbyPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileHobbyByIdQuery(publicId, hobbyPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("personnel-files/{publicId:guid}/hobbies")]
    [ProducesResponseType<PersonnelFileHobbyResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Add a hobby to a personnel file",
        Description = """
            Creates a new hobby entry under the specified personnel file and returns it with
            a `201 Created` response. The `Location` header points to the created resource and
            the `ETag` header carries its initial `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFileHobbyResponse>> AddHobby(
        Guid publicId,
        [FromBody] AddHobbyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileHobbyCommand(
                publicId,
                new HobbyInput(request.HobbyName)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetHobbyById),
            value => new { publicId, hobbyPublicId = value.HobbyPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("personnel-files/{publicId:guid}/hobbies/{hobbyPublicId:guid}")]
    [ProducesResponseType<PersonnelFileHobbyResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace a personnel file hobby",
        Description = """
            Replaces all fields of an existing hobby entry. Requires the `If-Match` header
            with the current `concurrencyToken`; the new token is returned in the `ETag`
            header.
            """)]
    public async Task<ActionResult<PersonnelFileHobbyResponse>> UpdateHobby(
        Guid publicId,
        Guid hobbyPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateHobbyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileHobbyCommand(
                publicId,
                hobbyPublicId,
                new HobbyInput(request.HobbyName),
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("personnel-files/{publicId:guid}/hobbies/{hobbyPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<PersonnelFileHobbyResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a personnel file hobby",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type
            `application/json-patch+json`) to an existing hobby entry. Requires the `If-Match`
            header with the current `concurrencyToken`; the new token is returned in the
            `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileHobbyResponse>> PatchHobby(
        Guid publicId,
        Guid hobbyPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchHobbyRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchPersonnelFileHobbyCommand(
                publicId,
                hobbyPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new PersonnelFileHobbyPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("personnel-files/{publicId:guid}/hobbies/{hobbyPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove a hobby from a personnel file",
        Description = """
            Deletes the specified hobby entry. Requires the `If-Match` header with the current
            `concurrencyToken`. Returns the parent personnel file's refreshed concurrency token
            so the caller can keep mutating without an extra round-trip.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteHobby(
        Guid publicId,
        Guid hobbyPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileHobbyCommand(publicId, hobbyPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    // ─── Associations ─────────────────────────────────────────────────────────

    [HttpGet("personnel-files/{publicId:guid}/associations")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileAssociationResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's associations",
        Description = """
            Returns every association entry recorded for the specified personnel file. Each
            item carries its own `concurrencyToken`, required in the `If-Match` header of
            subsequent `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileAssociationResponse>>> GetAssociations(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileAssociationsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("personnel-files/{publicId:guid}/associations/{associationPublicId:guid}")]
    [ProducesResponseType<PersonnelFileAssociationResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file association by id",
        Description = """
            Returns a single association entry of the specified personnel file. The
            `concurrencyToken` in the response is required in the `If-Match` header of
            subsequent `PUT`/`PATCH`/`DELETE` requests.
            """)]
    public async Task<ActionResult<PersonnelFileAssociationResponse>> GetAssociationById(
        Guid publicId,
        Guid associationPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileAssociationByIdQuery(publicId, associationPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("personnel-files/{publicId:guid}/associations")]
    [ProducesResponseType<PersonnelFileAssociationResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Add an association to a personnel file",
        Description = """
            Creates a new association entry under the specified personnel file and returns it
            with a `201 Created` response. The `Location` header points to the created resource
            and the `ETag` header carries its initial `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFileAssociationResponse>> AddAssociation(
        Guid publicId,
        [FromBody] AddAssociationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileAssociationCommand(
                publicId,
                new AssociationInput(
                    request.AssociationName,
                    request.Role,
                    request.JoinedDate,
                    request.LeftDate,
                    request.Payment)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetAssociationById),
            value => new { publicId, associationPublicId = value.AssociationPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("personnel-files/{publicId:guid}/associations/{associationPublicId:guid}")]
    [ProducesResponseType<PersonnelFileAssociationResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace a personnel file association",
        Description = """
            Replaces all fields of an existing association entry. Requires the `If-Match`
            header with the current `concurrencyToken`; the new token is returned in the
            `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileAssociationResponse>> UpdateAssociation(
        Guid publicId,
        Guid associationPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateAssociationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileAssociationCommand(
                publicId,
                associationPublicId,
                new AssociationInput(
                    request.AssociationName,
                    request.Role,
                    request.JoinedDate,
                    request.LeftDate,
                    request.Payment),
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("personnel-files/{publicId:guid}/associations/{associationPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<PersonnelFileAssociationResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a personnel file association",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type
            `application/json-patch+json`) to an existing association entry. Requires the
            `If-Match` header with the current `concurrencyToken`; the new token is returned in
            the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileAssociationResponse>> PatchAssociation(
        Guid publicId,
        Guid associationPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchAssociationRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchPersonnelFileAssociationCommand(
                publicId,
                associationPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new PersonnelFileAssociationPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("personnel-files/{publicId:guid}/associations/{associationPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove an association from a personnel file",
        Description = """
            Deletes the specified association entry. Requires the `If-Match` header with the
            current `concurrencyToken`. Returns the parent personnel file's refreshed concurrency
            token so the caller can keep mutating without an extra round-trip.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteAssociation(
        Guid publicId,
        Guid associationPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileAssociationCommand(publicId, associationPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    // ─── Employee Relations ───────────────────────────────────────────────────

    [HttpGet("personnel-files/{publicId:guid}/employee-relations")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileEmployeeRelationResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's employee relations",
        Description = """
            Returns every employee relation entry recorded for the specified personnel file.
            Each item carries its own `concurrencyToken`, required in the `If-Match` header of
            subsequent `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileEmployeeRelationResponse>>> GetEmployeeRelations(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileEmployeeRelationsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("personnel-files/{publicId:guid}/employee-relations/{employeeRelationPublicId:guid}")]
    [ProducesResponseType<PersonnelFileEmployeeRelationResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file employee relation by id",
        Description = """
            Returns a single employee relation entry of the specified personnel file. The
            `concurrencyToken` in the response is required in the `If-Match` header of
            subsequent `PUT`/`PATCH`/`DELETE` requests.
            """)]
    public async Task<ActionResult<PersonnelFileEmployeeRelationResponse>> GetEmployeeRelationById(
        Guid publicId,
        Guid employeeRelationPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileEmployeeRelationByIdQuery(publicId, employeeRelationPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("personnel-files/{publicId:guid}/employee-relations")]
    [ProducesResponseType<PersonnelFileEmployeeRelationResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Add an employee relation to a personnel file",
        Description = """
            Creates a new employee relation entry under the specified personnel file and
            returns it with a `201 Created` response. The `Location` header points to the
            created resource and the `ETag` header carries its initial `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFileEmployeeRelationResponse>> AddEmployeeRelation(
        Guid publicId,
        [FromBody] AddEmployeeRelationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileEmployeeRelationCommand(
                publicId,
                new EmployeeRelationInput(request.RelatedEmployeePublicId, request.Relationship)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetEmployeeRelationById),
            value => new { publicId, employeeRelationPublicId = value.EmployeeRelationPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("personnel-files/{publicId:guid}/employee-relations/{employeeRelationPublicId:guid}")]
    [ProducesResponseType<PersonnelFileEmployeeRelationResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace a personnel file employee relation",
        Description = """
            Replaces all fields of an existing employee relation entry. Requires the `If-Match`
            header with the current `concurrencyToken`; the new token is returned in the
            `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileEmployeeRelationResponse>> UpdateEmployeeRelation(
        Guid publicId,
        Guid employeeRelationPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateEmployeeRelationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileEmployeeRelationCommand(
                publicId,
                employeeRelationPublicId,
                new EmployeeRelationInput(request.RelatedEmployeePublicId, request.Relationship),
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("personnel-files/{publicId:guid}/employee-relations/{employeeRelationPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<PersonnelFileEmployeeRelationResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a personnel file employee relation",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type
            `application/json-patch+json`) to an existing employee relation entry. Requires the
            `If-Match` header with the current `concurrencyToken`; the new token is returned in
            the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileEmployeeRelationResponse>> PatchEmployeeRelation(
        Guid publicId,
        Guid employeeRelationPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchEmployeeRelationRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchPersonnelFileEmployeeRelationCommand(
                publicId,
                employeeRelationPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new PersonnelFileEmployeeRelationPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("personnel-files/{publicId:guid}/employee-relations/{employeeRelationPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove an employee relation from a personnel file",
        Description = """
            Deletes the specified employee relation entry. Requires the `If-Match` header with
            the current `concurrencyToken`. Returns the parent personnel file's refreshed
            concurrency token so the caller can keep mutating without an extra round-trip.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteEmployeeRelation(
        Guid publicId,
        Guid employeeRelationPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileEmployeeRelationCommand(publicId, employeeRelationPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }
}

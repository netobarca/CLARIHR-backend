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
/// Authorization substitutions — designating who covers an employee during an absence (D-01…D-12). Reads use
/// <see cref="PersonnelFilePolicies.Read"/>; writes use the dedicated <see cref="PersonnelFilePolicies.ManageSubstitutions"/>
/// policy (D-09), separate from the generic Manage policy of the rest of the employment shell. The routes are
/// identical to the previous Employment-controller routes, so this is an authorization split, not an API change.
/// </summary>
[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.Read, PersonnelFilePolicies.ManageSubstitutions)]
public sealed class PersonnelFileAuthorizationSubstitutionController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/personnel-files/{publicId:guid}/authorization-substitutions")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's authorization substitutions",
        Description = """
            Returns every authorization substitution recorded for the specified personnel file. Each item
            carries its own `concurrencyToken`, required in the `If-Match` header of subsequent
            `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>> GetAuthorizationSubstitutions(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileAuthorizationSubstitutionsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/authorization-substitutions/{authorizationSubstitutionPublicId:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileAuthorizationSubstitutionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file authorization substitution by id",
        Description = """
            Returns a single authorization substitution of the specified personnel file. The `concurrencyToken`
            in the response is required in the `If-Match` header of subsequent
            `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<PersonnelFileAuthorizationSubstitutionResponse>> GetAuthorizationSubstitutionById(
        Guid publicId,
        Guid authorizationSubstitutionPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileAuthorizationSubstitutionByIdQuery(publicId, authorizationSubstitutionPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/authorization-substitutions")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileAuthorizationSubstitutionResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Add an authorization substitution to a personnel file",
        Description = """
            Creates a new authorization substitution under the specified personnel file and returns it with a
            `201 Created` response. The `Location` header points to the created resource and the
            `ETag` header carries its initial `concurrencyToken`. Requires the dedicated
            `PersonnelFiles.ManageSubstitutions` permission.
            """)]
    public async Task<ActionResult<PersonnelFileAuthorizationSubstitutionResponse>> AddAuthorizationSubstitution(
        Guid publicId,
        [FromBody] AddAuthorizationSubstitutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileAuthorizationSubstitutionCommand(
                publicId,
                new AuthorizationSubstitutionInput(
                    request.SubstitutionTypeCode,
                    request.SubstitutePersonnelFilePublicId,
                    request.SubstitutePositionSlotPublicId,
                    request.StartDate,
                    request.EndDate,
                    request.IsActive,
                    request.Notes)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetAuthorizationSubstitutionById),
            value => new { publicId, authorizationSubstitutionPublicId = value.AuthorizationSubstitutionPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/authorization-substitutions/{authorizationSubstitutionPublicId:guid}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileAuthorizationSubstitutionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace a personnel file authorization substitution",
        Description = """
            Replaces the business fields of an existing authorization substitution. The active state is
            preserved (it is mutated exclusively via `PATCH`). Requires the `If-Match` header
            with the current `concurrencyToken`; the new token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileAuthorizationSubstitutionResponse>> UpdateAuthorizationSubstitution(
        Guid publicId,
        Guid authorizationSubstitutionPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateAuthorizationSubstitutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileAuthorizationSubstitutionCommand(
                publicId,
                authorizationSubstitutionPublicId,
                new AuthorizationSubstitutionInput(
                    request.SubstitutionTypeCode,
                    request.SubstitutePersonnelFilePublicId,
                    request.SubstitutePositionSlotPublicId,
                    request.StartDate,
                    request.EndDate,
                    IsActive: true,
                    request.Notes),
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/authorization-substitutions/{authorizationSubstitutionPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [Produces("application/json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<PersonnelFileAuthorizationSubstitutionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a personnel file authorization substitution",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type
            `application/json-patch+json`) to an existing authorization substitution. Supports the business
            fields and the `isActive` flag. Requires the `If-Match` header with the current
            `concurrencyToken`; the new token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileAuthorizationSubstitutionResponse>> PatchAuthorizationSubstitution(
        Guid publicId,
        Guid authorizationSubstitutionPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchAuthorizationSubstitutionRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchPersonnelFileAuthorizationSubstitutionCommand(
                publicId,
                authorizationSubstitutionPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new PersonnelFileAuthorizationSubstitutionPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/authorization-substitutions/{authorizationSubstitutionPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove an authorization substitution from a personnel file",
        Description = """
            Deletes the specified authorization substitution. Requires the `If-Match` header with the current
            `concurrencyToken`. Returns the parent personnel file's refreshed concurrency token
            so the caller can keep mutating without an extra round-trip.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteAuthorizationSubstitution(
        Guid publicId,
        Guid authorizationSubstitutionPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileAuthorizationSubstitutionCommand(publicId, authorizationSubstitutionPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }
}

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

/// <summary>
/// "Competencias del puesto" — position competencies of a personnel file. Carved out of
/// <see cref="PersonnelFileTalentController"/> into its own controller so the dedicated
/// <c>PersonnelFiles.ViewCompetencies</c> / <c>PersonnelFiles.ManageCompetencies</c> policies apply (the
/// class-level <c>[AuthorizationPolicySet]</c> cannot vary per sub-resource). The routes are identical to the
/// previous Talent routes, so this is an authorization split, not an API change. Reads allow employee
/// self-service (the precise gate lives in the handlers); writes are HR-only.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}")]
[Consumes("application/json")]
[Produces("application/json")]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewCompetencies, PersonnelFilePolicies.ManageCompetencies)]
public sealed class PersonnelFileCompetencyController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("personnel-files/{publicId:guid}/position-competencies")]
    [ProducesResponseType<EmployeePositionCompetenciesResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Consult the employee's position competencies",
        Description = """
            Returns the "Competencias del puesto" view: the competencies expected for the employee's assigned
            position (derived from the job-profile competency matrix by hierarchical level) combined with the
            employee's achieved scores — with the gap computed (expected − achieved) and the evaluation history
            per competency, grouped by competency type. When the employee has no resolvable active position the
            response carries `hasAssignedPosition = false` and empty groups.
            """)]
    public async Task<ActionResult<EmployeePositionCompetenciesResponse>> GetEmployeePositionCompetencies(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetEmployeePositionCompetenciesQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("personnel-files/{publicId:guid}/position-competency-results")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's position competency results",
        Description = """
            Returns every position competency result recorded for the specified personnel file.
            Each item carries its own `concurrencyToken`, required in the `If-Match` header of
            subsequent `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>> GetPositionCompetencyResults(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFilePositionCompetencyResultsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("personnel-files/{publicId:guid}/position-competency-results/{positionCompetencyResultPublicId:guid}")]
    [ProducesResponseType<PersonnelFilePositionCompetencyResultResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file position competency result by id",
        Description = """
            Returns a single position competency result of the specified personnel file. The
            `concurrencyToken` in the response is required in the `If-Match` header of
            subsequent `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<PersonnelFilePositionCompetencyResultResponse>> GetPositionCompetencyResultById(
        Guid publicId,
        Guid positionCompetencyResultPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFilePositionCompetencyResultByIdQuery(publicId, positionCompetencyResultPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("personnel-files/{publicId:guid}/position-competency-results")]
    [ProducesResponseType<PersonnelFilePositionCompetencyResultResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Add a position competency result to a personnel file",
        Description = """
            Records an achieved score for a competency expected by the employee's assigned position (referenced by
            `expectationPublicId`). The gap is computed (expected − achieved) and the evaluation date must not be
            in the future. Returns the created result with `201 Created`; the `ETag` header carries its initial
            `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFilePositionCompetencyResultResponse>> AddPositionCompetencyResult(
        Guid publicId,
        [FromBody] AddPositionCompetencyResultRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFilePositionCompetencyResultCommand(
                publicId,
                new PositionCompetencyResultInput(
                    request.ExpectationPublicId,
                    request.AchievedScore,
                    request.EvaluationDateUtc,
                    request.SourceSystem,
                    request.SourceReference,
                    request.SourceSyncedUtc)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetPositionCompetencyResultById),
            value => new { publicId, positionCompetencyResultPublicId = value.PositionCompetencyResultPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("personnel-files/{publicId:guid}/position-competency-results/{positionCompetencyResultPublicId:guid}")]
    [ProducesResponseType<PersonnelFilePositionCompetencyResultResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace a personnel file position competency result",
        Description = """
            Replaces the achieved score / evaluation date / source of an existing position competency result.
            Requires the `If-Match` header with the current `concurrencyToken`; the new token is returned in
            the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFilePositionCompetencyResultResponse>> UpdatePositionCompetencyResult(
        Guid publicId,
        Guid positionCompetencyResultPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdatePositionCompetencyResultRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFilePositionCompetencyResultCommand(
                publicId,
                positionCompetencyResultPublicId,
                new PositionCompetencyResultInput(
                    request.ExpectationPublicId,
                    request.AchievedScore,
                    request.EvaluationDateUtc,
                    request.SourceSystem,
                    request.SourceReference,
                    request.SourceSyncedUtc),
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("personnel-files/{publicId:guid}/position-competency-results/{positionCompetencyResultPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<PersonnelFilePositionCompetencyResultResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a personnel file position competency result",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type `application/json-patch+json`) to an existing
            position competency result. Patchable fields: `expectationPublicId`, `achievedScore`,
            `evaluationDateUtc`, `sourceSystem`, `sourceReference`, `sourceSyncedUtc` (`expectedScore`/`gapScore`
            are derived). Requires the `If-Match` header with the current `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFilePositionCompetencyResultResponse>> PatchPositionCompetencyResult(
        Guid publicId,
        Guid positionCompetencyResultPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchPositionCompetencyResultRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchPersonnelFilePositionCompetencyResultCommand(
                publicId,
                positionCompetencyResultPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new PersonnelFilePositionCompetencyResultPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("personnel-files/{publicId:guid}/position-competency-results/{positionCompetencyResultPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove a position competency result from a personnel file",
        Description = """
            Deletes the specified position competency result. Requires the `If-Match` header
            with the current `concurrencyToken`. Returns the parent personnel file's refreshed
            concurrency token so the caller can keep mutating without an extra round-trip.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeletePositionCompetencyResult(
        Guid publicId,
        Guid positionCompetencyResultPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFilePositionCompetencyResultCommand(publicId, positionCompetencyResultPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }
}

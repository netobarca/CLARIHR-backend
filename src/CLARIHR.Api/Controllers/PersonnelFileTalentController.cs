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
public sealed class PersonnelFileTalentController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    // ─── Performance Evaluations ──────────────────────────────────────────────

    [HttpGet("personnel-files/{publicId:guid}/evaluations")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's performance evaluations",
        Description = """
            Returns every performance evaluation entry recorded for the specified personnel
            file. Each item carries its own `concurrencyToken`, required in the `If-Match`
            header of subsequent `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>>> GetEvaluations(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFilePerformanceEvaluationsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("personnel-files/{publicId:guid}/evaluations/{evaluationPublicId:guid}")]
    [ProducesResponseType<PersonnelFilePerformanceEvaluationResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file performance evaluation by id",
        Description = """
            Returns a single performance evaluation entry of the specified personnel file. The
            `concurrencyToken` in the response is required in the `If-Match` header of
            subsequent `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<PersonnelFilePerformanceEvaluationResponse>> GetEvaluationById(
        Guid publicId,
        Guid evaluationPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFilePerformanceEvaluationByIdQuery(publicId, evaluationPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("personnel-files/{publicId:guid}/evaluations")]
    [ProducesResponseType<PersonnelFilePerformanceEvaluationResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Add a performance evaluation to a personnel file",
        Description = """
            Creates a new performance evaluation entry under the specified personnel file and
            returns it with a `201 Created` response. The `Location` header points to the
            created resource and the `ETag` header carries its initial `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFilePerformanceEvaluationResponse>> AddEvaluation(
        Guid publicId,
        [FromBody] AddPerformanceEvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFilePerformanceEvaluationCommand(
                publicId,
                new PerformanceEvaluationInput(
                    request.EvaluatorName,
                    request.EvaluationDateUtc,
                    request.Score,
                    request.QualitativeScoreCode,
                    request.Comment,
                    request.SourceSystem,
                    request.SourceReference,
                    request.SourceSyncedUtc)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetEvaluationById),
            value => new { publicId, evaluationPublicId = value.EvaluationPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("personnel-files/{publicId:guid}/evaluations/{evaluationPublicId:guid}")]
    [ProducesResponseType<PersonnelFilePerformanceEvaluationResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace a personnel file performance evaluation",
        Description = """
            Replaces all fields of an existing performance evaluation entry. Requires the
            `If-Match` header with the current `concurrencyToken`; the new token is returned in
            the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFilePerformanceEvaluationResponse>> UpdateEvaluation(
        Guid publicId,
        Guid evaluationPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdatePerformanceEvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFilePerformanceEvaluationCommand(
                publicId,
                evaluationPublicId,
                new PerformanceEvaluationInput(
                    request.EvaluatorName,
                    request.EvaluationDateUtc,
                    request.Score,
                    request.QualitativeScoreCode,
                    request.Comment,
                    request.SourceSystem,
                    request.SourceReference,
                    request.SourceSyncedUtc),
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("personnel-files/{publicId:guid}/evaluations/{evaluationPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<PersonnelFilePerformanceEvaluationResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a personnel file performance evaluation",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type
            `application/json-patch+json`) to an existing performance evaluation entry.
            Requires the `If-Match` header with the current `concurrencyToken`; the new token
            is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFilePerformanceEvaluationResponse>> PatchEvaluation(
        Guid publicId,
        Guid evaluationPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchPerformanceEvaluationRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchPersonnelFilePerformanceEvaluationCommand(
                publicId,
                evaluationPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new PersonnelFilePerformanceEvaluationPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("personnel-files/{publicId:guid}/evaluations/{evaluationPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove a performance evaluation from a personnel file",
        Description = """
            Deletes the specified performance evaluation entry. Requires the `If-Match` header
            with the current `concurrencyToken`. Returns the parent personnel file's refreshed
            concurrency token so the caller can keep mutating without an extra round-trip.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteEvaluation(
        Guid publicId,
        Guid evaluationPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFilePerformanceEvaluationCommand(publicId, evaluationPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    // ─── Position Competency Results ──────────────────────────────────────────

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
            Creates a new position competency result under the specified personnel file and
            returns it with a `201 Created` response. The `Location` header points to the
            created resource and the `ETag` header carries its initial `concurrencyToken`.
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
                    request.CompetencyCode,
                    request.DesiredBehaviors,
                    request.ExpectedScore,
                    request.AchievedScore,
                    request.GapScore,
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
            Replaces all fields of an existing position competency result. Requires the
            `If-Match` header with the current `concurrencyToken`; the new token is returned in
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
                    request.CompetencyCode,
                    request.DesiredBehaviors,
                    request.ExpectedScore,
                    request.AchievedScore,
                    request.GapScore,
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
            Applies a JSON Patch document (RFC 6902, media type
            `application/json-patch+json`) to an existing position competency result. Requires
            the `If-Match` header with the current `concurrencyToken`; the new token is returned
            in the `ETag` header.
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

    // ─── Selection Contests ───────────────────────────────────────────────────

    [HttpGet("personnel-files/{publicId:guid}/selection-contests")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileSelectionContestResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's selection contests",
        Description = """
            Returns every selection contest entry recorded for the specified personnel file.
            Each item carries its own `concurrencyToken`, required in the `If-Match` header of
            subsequent `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileSelectionContestResponse>>> GetSelectionContests(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileSelectionContestsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("personnel-files/{publicId:guid}/selection-contests/{selectionContestPublicId:guid}")]
    [ProducesResponseType<PersonnelFileSelectionContestResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file selection contest by id",
        Description = """
            Returns a single selection contest entry of the specified personnel file. The
            `concurrencyToken` in the response is required in the `If-Match` header of
            subsequent `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<PersonnelFileSelectionContestResponse>> GetSelectionContestById(
        Guid publicId,
        Guid selectionContestPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileSelectionContestByIdQuery(publicId, selectionContestPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("personnel-files/{publicId:guid}/selection-contests")]
    [ProducesResponseType<PersonnelFileSelectionContestResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Add a selection contest to a personnel file",
        Description = """
            Creates a new selection contest entry under the specified personnel file and
            returns it with a `201 Created` response. The `Location` header points to the
            created resource and the `ETag` header carries its initial `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFileSelectionContestResponse>> AddSelectionContest(
        Guid publicId,
        [FromBody] AddSelectionContestRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileSelectionContestCommand(
                publicId,
                new SelectionContestInput(
                    request.ContestCode,
                    request.ContestName,
                    request.ContestDateUtc,
                    request.ResultCode,
                    request.Notes,
                    request.SourceSystem,
                    request.SourceReference,
                    request.SourceSyncedUtc)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetSelectionContestById),
            value => new { publicId, selectionContestPublicId = value.SelectionContestPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("personnel-files/{publicId:guid}/selection-contests/{selectionContestPublicId:guid}")]
    [ProducesResponseType<PersonnelFileSelectionContestResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace a personnel file selection contest",
        Description = """
            Replaces all fields of an existing selection contest entry. Requires the `If-Match`
            header with the current `concurrencyToken`; the new token is returned in the `ETag`
            header.
            """)]
    public async Task<ActionResult<PersonnelFileSelectionContestResponse>> UpdateSelectionContest(
        Guid publicId,
        Guid selectionContestPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateSelectionContestRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileSelectionContestCommand(
                publicId,
                selectionContestPublicId,
                new SelectionContestInput(
                    request.ContestCode,
                    request.ContestName,
                    request.ContestDateUtc,
                    request.ResultCode,
                    request.Notes,
                    request.SourceSystem,
                    request.SourceReference,
                    request.SourceSyncedUtc),
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("personnel-files/{publicId:guid}/selection-contests/{selectionContestPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<PersonnelFileSelectionContestResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a personnel file selection contest",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type
            `application/json-patch+json`) to an existing selection contest entry. Requires the
            `If-Match` header with the current `concurrencyToken`; the new token is returned in
            the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileSelectionContestResponse>> PatchSelectionContest(
        Guid publicId,
        Guid selectionContestPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchSelectionContestRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchPersonnelFileSelectionContestCommand(
                publicId,
                selectionContestPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new PersonnelFileSelectionContestPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("personnel-files/{publicId:guid}/selection-contests/{selectionContestPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove a selection contest from a personnel file",
        Description = """
            Deletes the specified selection contest entry. Requires the `If-Match` header with
            the current `concurrencyToken`. Returns the parent personnel file's refreshed
            concurrency token so the caller can keep mutating without an extra round-trip.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteSelectionContest(
        Guid publicId,
        Guid selectionContestPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileSelectionContestCommand(publicId, selectionContestPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    // ─── Curricular Competencies ──────────────────────────────────────────────

    [HttpGet("personnel-files/{publicId:guid}/curricular-competencies")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's curricular competencies",
        Description = """
            Returns every curricular competency entry recorded for the specified personnel
            file. Each item carries its own `concurrencyToken`, required in the `If-Match`
            header of subsequent `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>> GetCurricularCompetencies(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileCurricularCompetenciesQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("personnel-files/{publicId:guid}/curricular-competencies/{curricularCompetencyPublicId:guid}")]
    [ProducesResponseType<PersonnelFileCurricularCompetencyResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file curricular competency by id",
        Description = """
            Returns a single curricular competency entry of the specified personnel file. The
            `concurrencyToken` in the response is required in the `If-Match` header of
            subsequent `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<PersonnelFileCurricularCompetencyResponse>> GetCurricularCompetencyById(
        Guid publicId,
        Guid curricularCompetencyPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileCurricularCompetencyByIdQuery(publicId, curricularCompetencyPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("personnel-files/{publicId:guid}/curricular-competencies")]
    [ProducesResponseType<PersonnelFileCurricularCompetencyResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Add a curricular competency to a personnel file",
        Description = """
            Creates a new curricular competency entry under the specified personnel file and
            returns it with a `201 Created` response. The `Location` header points to the
            created resource and the `ETag` header carries its initial `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFileCurricularCompetencyResponse>> AddCurricularCompetency(
        Guid publicId,
        [FromBody] AddCurricularCompetencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileCurricularCompetencyCommand(
                publicId,
                new CurricularCompetencyInput(
                    request.RequirementTypeCode,
                    request.RequirementName,
                    request.CompetencyDomain,
                    request.ExperienceTimeValue,
                    request.MetricCode,
                    request.Notes,
                    request.SourceSystem,
                    request.SourceReference,
                    request.SourceSyncedUtc)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetCurricularCompetencyById),
            value => new { publicId, curricularCompetencyPublicId = value.CurricularCompetencyPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("personnel-files/{publicId:guid}/curricular-competencies/{curricularCompetencyPublicId:guid}")]
    [ProducesResponseType<PersonnelFileCurricularCompetencyResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace a personnel file curricular competency",
        Description = """
            Replaces all fields of an existing curricular competency entry. Requires the
            `If-Match` header with the current `concurrencyToken`; the new token is returned in
            the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileCurricularCompetencyResponse>> UpdateCurricularCompetency(
        Guid publicId,
        Guid curricularCompetencyPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateCurricularCompetencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileCurricularCompetencyCommand(
                publicId,
                curricularCompetencyPublicId,
                new CurricularCompetencyInput(
                    request.RequirementTypeCode,
                    request.RequirementName,
                    request.CompetencyDomain,
                    request.ExperienceTimeValue,
                    request.MetricCode,
                    request.Notes,
                    request.SourceSystem,
                    request.SourceReference,
                    request.SourceSyncedUtc),
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("personnel-files/{publicId:guid}/curricular-competencies/{curricularCompetencyPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<PersonnelFileCurricularCompetencyResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a personnel file curricular competency",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type
            `application/json-patch+json`) to an existing curricular competency entry. Requires
            the `If-Match` header with the current `concurrencyToken`; the new token is returned
            in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileCurricularCompetencyResponse>> PatchCurricularCompetency(
        Guid publicId,
        Guid curricularCompetencyPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchCurricularCompetencyRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchPersonnelFileCurricularCompetencyCommand(
                publicId,
                curricularCompetencyPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new PersonnelFileCurricularCompetencyPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("personnel-files/{publicId:guid}/curricular-competencies/{curricularCompetencyPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove a curricular competency from a personnel file",
        Description = """
            Deletes the specified curricular competency entry. Requires the `If-Match` header
            with the current `concurrencyToken`. Returns the parent personnel file's refreshed
            concurrency token so the caller can keep mutating without an extra round-trip.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteCurricularCompetency(
        Guid publicId,
        Guid curricularCompetencyPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileCurricularCompetencyCommand(publicId, curricularCompetencyPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }
}

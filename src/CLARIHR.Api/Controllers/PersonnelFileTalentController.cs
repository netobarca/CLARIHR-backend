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
public sealed class PersonnelFileTalentController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    // ── Evaluations ──────────────────────────────────────────────────────

    [HttpGet("api/v1/personnel-files/{publicId:guid}/evaluations")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>>> GetEvaluations(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFilePerformanceEvaluationsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/evaluations")]
    [ProducesResponseType<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>>>> AddEvaluation(
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
                    request.SourceSyncedUtc),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/evaluations/{itemPublicId:guid}")]
    [ProducesResponseType<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>>>> UpdateEvaluation(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] UpdatePerformanceEvaluationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFilePerformanceEvaluationCommand(
                publicId,
                itemPublicId,
                new PerformanceEvaluationInput(
                    request.EvaluatorName,
                    request.EvaluationDateUtc,
                    request.Score,
                    request.QualitativeScoreCode,
                    request.Comment,
                    request.SourceSystem,
                    request.SourceReference,
                    request.SourceSyncedUtc),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    // ── Position Competency Results ──────────────────────────────────────

    [HttpGet("api/v1/personnel-files/{publicId:guid}/position-competency-results")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>> GetPositionCompetencyResults(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFilePositionCompetencyResultsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/position-competency-results")]
    [ProducesResponseType<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>>> AddPositionCompetencyResult(
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
                    request.SourceSyncedUtc),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/position-competency-results/{itemPublicId:guid}")]
    [ProducesResponseType<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>>>> UpdatePositionCompetencyResult(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] UpdatePositionCompetencyResultRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFilePositionCompetencyResultCommand(
                publicId,
                itemPublicId,
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
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    // ── Selection Contests ───────────────────────────────────────────────

    [HttpGet("api/v1/personnel-files/{publicId:guid}/selection-contests")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileSelectionContestResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileSelectionContestResponse>>> GetSelectionContests(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileSelectionContestsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/selection-contests")]
    [ProducesResponseType<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSelectionContestResponse>>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSelectionContestResponse>>>> AddSelectionContest(
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
                    request.SourceSyncedUtc),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/selection-contests/{itemPublicId:guid}")]
    [ProducesResponseType<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSelectionContestResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileSelectionContestResponse>>>> UpdateSelectionContest(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] UpdateSelectionContestRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileSelectionContestCommand(
                publicId,
                itemPublicId,
                new SelectionContestInput(
                    request.ContestCode,
                    request.ContestName,
                    request.ContestDateUtc,
                    request.ResultCode,
                    request.Notes,
                    request.SourceSystem,
                    request.SourceReference,
                    request.SourceSyncedUtc),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    // ── Curricular Competencies ──────────────────────────────────────────

    [HttpGet("api/v1/personnel-files/{publicId:guid}/curricular-competencies")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>> GetCurricularCompetencies(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileCurricularCompetenciesQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/curricular-competencies")]
    [ProducesResponseType<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>>> AddCurricularCompetency(
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
                    request.SourceSyncedUtc),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/curricular-competencies/{itemPublicId:guid}")]
    [ProducesResponseType<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>>>> UpdateCurricularCompetency(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] UpdateCurricularCompetencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileCurricularCompetencyCommand(
                publicId,
                itemPublicId,
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
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }
}

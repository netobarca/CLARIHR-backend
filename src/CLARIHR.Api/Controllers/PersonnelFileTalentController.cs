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
}

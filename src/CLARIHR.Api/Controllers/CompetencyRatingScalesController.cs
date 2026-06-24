using Asp.Versioning;
using CLARIHR.Api.Authorization;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.CompetencyFramework;
using CLARIHR.Application.Features.CompetencyFramework.Common;
using CLARIHR.Domain.CompetencyFramework;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

/// <summary>
/// The company-configurable competency rating scale (decision D-04). Each company has a single active scale —
/// the source of truth for expected/achieved competency scores. It is seeded with a default (1–5) and can be
/// redefined in place (numeric range or discrete ordered levels) via the upsert endpoint.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}")]
[Tags("Competency Framework")]
[ResourceActions(CompetencyFrameworkPermissionCodes.ResourceKey)]
public sealed class CompetencyRatingScalesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("companies/{companyId:guid}/competency-rating-scale")]
    [ProducesResponseType<ActiveCompetencyRatingScaleResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get the company's active competency rating scale",
        Description = "Returns the company's active competency rating scale (numeric range or discrete levels). When none is configured, `isConfigured` is false and `scale` is null.")]
    public async Task<ActionResult<ActiveCompetencyRatingScaleResponse>> GetActiveCompetencyRatingScale(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetActiveCompetencyRatingScaleQuery(companyId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("companies/{companyId:guid}/competency-rating-scale")]
    [ProducesResponseType<CompetencyRatingScaleResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Set the company's competency rating scale",
        Description = "Creates or redefines the company's active competency rating scale in place. For a numeric scale provide `minValue`/`maxValue`/`decimals`; for a discrete scale provide at least two `levels` with distinct ordinal `value`s. Expected and achieved competency scores are validated against this scale.")]
    public async Task<ActionResult<CompetencyRatingScaleResponse>> SetCompetencyRatingScale(
        Guid companyId,
        [FromBody] SetCompetencyRatingScaleRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new SetCompetencyRatingScaleCommand(
                companyId,
                request.Code,
                request.Name,
                request.ScaleType,
                request.MinValue,
                request.MaxValue,
                request.Decimals,
                (request.Levels ?? [])
                    .Select(level => new CompetencyRatingScaleLevelInput(level.Code, level.Label, level.Value, level.SortOrder))
                    .ToArray()),
            cancellationToken);

        return this.ToActionResult(result);
    }

    public sealed record SetCompetencyRatingScaleRequest(
        string Code,
        string Name,
        CompetencyRatingScaleType ScaleType,
        decimal? MinValue,
        decimal? MaxValue,
        int Decimals,
        IReadOnlyCollection<SetCompetencyRatingScaleLevelRequest>? Levels);

    public sealed record SetCompetencyRatingScaleLevelRequest(
        string Code,
        string Label,
        decimal Value,
        int SortOrder);
}

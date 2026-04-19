using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.CompetencyFramework;
using CLARIHR.Application.Features.CompetencyFramework.Common;
using CLARIHR.Application.Features.Reports.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
public sealed class CompetencyFrameworkController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    ReportExportDeliveryService reportExportDeliveryService) : ControllerBase
{
    [HttpGet("api/v1/companies/{companyId:guid}/occupational-pyramid-levels")]
    [ProducesResponseType<PagedResponse<OccupationalPyramidLevelListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<OccupationalPyramidLevelListItemResponse>>> SearchOccupationalPyramidLevels(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = CompetencyFrameworkValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchOccupationalPyramidLevelsQuery(companyId, isActive, search, page, pageSize, includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/occupational-pyramid-levels/{id:guid}")]
    [ProducesResponseType<OccupationalPyramidLevelResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OccupationalPyramidLevelResponse>> GetOccupationalPyramidLevelById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetOccupationalPyramidLevelByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/companies/{companyId:guid}/occupational-pyramid-levels")]
    [ProducesResponseType<OccupationalPyramidLevelResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OccupationalPyramidLevelResponse>> CreateOccupationalPyramidLevel(
        Guid companyId,
        [FromBody] CreateOccupationalPyramidLevelRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateOccupationalPyramidLevelCommand(companyId, request.Code, request.Name, request.LevelOrder, request.Description),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<OccupationalPyramidLevelResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("api/v1/occupational-pyramid-levels/{id:guid}")]
    [ProducesResponseType<OccupationalPyramidLevelResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OccupationalPyramidLevelResponse>> UpdateOccupationalPyramidLevel(
        Guid id,
        [FromBody] UpdateOccupationalPyramidLevelRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateOccupationalPyramidLevelCommand(id, request.Code, request.Name, request.LevelOrder, request.Description, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/occupational-pyramid-levels/{id:guid}/activate")]
    [ProducesResponseType<OccupationalPyramidLevelResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OccupationalPyramidLevelResponse>> ActivateOccupationalPyramidLevel(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateOccupationalPyramidLevelCommand(id, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/occupational-pyramid-levels/{id:guid}/inactivate")]
    [ProducesResponseType<OccupationalPyramidLevelResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<OccupationalPyramidLevelResponse>> InactivateOccupationalPyramidLevel(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateOccupationalPyramidLevelCommand(id, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/companies/{companyId:guid}/competency-conducts")]
    [ProducesResponseType<PagedResponse<CompetencyConductListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<CompetencyConductListItemResponse>>> SearchCompetencyConducts(
        Guid companyId,
        [FromQuery] Guid? competencyId,
        [FromQuery] Guid? competencyTypeId,
        [FromQuery] Guid? behaviorLevelId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = CompetencyFrameworkValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchCompetencyConductsQuery(
                companyId,
                competencyId,
                competencyTypeId,
                behaviorLevelId,
                isActive,
                search,
                page,
                pageSize,
                includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/competency-conducts/{id:guid}")]
    [ProducesResponseType<CompetencyConductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompetencyConductResponse>> GetCompetencyConductById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetCompetencyConductByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/companies/{companyId:guid}/competency-conducts")]
    [ProducesResponseType<CompetencyConductResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompetencyConductResponse>> CreateCompetencyConduct(
        Guid companyId,
        [FromBody] CreateCompetencyConductRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateCompetencyConductCommand(
                companyId,
                request.CompetencyPublicId,
                request.CompetencyTypePublicId,
                request.BehaviorLevelPublicId,
                request.Description,
                request.SortOrder),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<CompetencyConductResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("api/v1/competency-conducts/{id:guid}")]
    [ProducesResponseType<CompetencyConductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompetencyConductResponse>> UpdateCompetencyConduct(
        Guid id,
        [FromBody] UpdateCompetencyConductRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateCompetencyConductCommand(
                id,
                request.CompetencyPublicId,
                request.CompetencyTypePublicId,
                request.BehaviorLevelPublicId,
                request.Description,
                request.SortOrder,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/competency-conducts/{id:guid}/activate")]
    [ProducesResponseType<CompetencyConductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompetencyConductResponse>> ActivateCompetencyConduct(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateCompetencyConductCommand(id, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/competency-conducts/{id:guid}/inactivate")]
    [ProducesResponseType<CompetencyConductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompetencyConductResponse>> InactivateCompetencyConduct(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateCompetencyConductCommand(id, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/competency-conducts/{id:guid}/behaviors")]
    [ProducesResponseType<CompetencyConductResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CompetencyConductResponse>> UpdateCompetencyConductBehaviors(
        Guid id,
        [FromBody] UpdateCompetencyConductBehaviorsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateCompetencyConductBehaviorsCommand(
                id,
                request.Behaviors?.Select(item => new CompetencyConductBehaviorInput(item.BehaviorPublicId, item.Notes, item.SortOrder)).ToArray() ?? [],
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/job-profiles/{id:guid}/competency-matrix")]
    [ProducesResponseType<JobProfileCompetencyMatrixResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobProfileCompetencyMatrixResponse>> GetJobProfileCompetencyMatrix(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetJobProfileCompetencyMatrixQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/job-profiles/{id:guid}/competency-matrix")]
    [ProducesResponseType<JobProfileCompetencyMatrixResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<JobProfileCompetencyMatrixResponse>> UpdateJobProfileCompetencyMatrix(
        Guid id,
        [FromBody] UpdateJobProfileCompetencyMatrixRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateJobProfileCompetencyMatrixCommand(
                id,
                request.Items?.Select(item => new JobProfileCompetencyMatrixItemInput(
                        item.OccupationalPyramidLevelPublicId,
                        item.CompetencyPublicId,
                        item.CompetencyTypePublicId,
                        item.BehaviorLevelPublicId,
                        item.ConductPublicIds ?? [],
                        item.ExpectedEvidence,
                        item.SortOrder))
                    .ToArray() ?? [],
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/job-profiles/{id:guid}/competency-matrix/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> ExportJobProfileCompetencyMatrix(
        Guid id,
        [FromQuery] string format = "xlsx",
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportJobProfileCompetencyMatrixQuery(id, reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<JobProfileCompetencyMatrixExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "job-profile-competency-matrix",
            "CompetencyMatrix",
            AuditEntityTypes.JobProfileCompetencyMatrix,
            ReportExportResources.JobProfileCompetencyMatrix,
            "Exported job profile competency matrix report.",
            new { jobProfileId = id },
            CompetencyFrameworkErrors.ExportFormatInvalid,
            cancellationToken);
    }

    public sealed record CreateOccupationalPyramidLevelRequest(
        string Code,
        string Name,
        int LevelOrder,
        string? Description);

    public sealed record UpdateOccupationalPyramidLevelRequest(
        string Code,
        string Name,
        int LevelOrder,
        string? Description,
        Guid ConcurrencyToken);

    public sealed record CreateCompetencyConductRequest(
        Guid CompetencyPublicId,
        Guid CompetencyTypePublicId,
        Guid BehaviorLevelPublicId,
        string Description,
        int SortOrder);

    public sealed record UpdateCompetencyConductRequest(
        Guid CompetencyPublicId,
        Guid CompetencyTypePublicId,
        Guid BehaviorLevelPublicId,
        string Description,
        int SortOrder,
        Guid ConcurrencyToken);

    public sealed record UpdateCompetencyConductBehaviorsRequest(
        IReadOnlyCollection<CompetencyConductBehaviorRequest>? Behaviors,
        Guid ConcurrencyToken);

    public sealed record CompetencyConductBehaviorRequest(
        Guid BehaviorPublicId,
        string? Notes,
        int SortOrder);

    public sealed record UpdateJobProfileCompetencyMatrixRequest(
        IReadOnlyCollection<JobProfileCompetencyMatrixItemRequest>? Items,
        Guid ConcurrencyToken);

    public sealed record JobProfileCompetencyMatrixItemRequest(
        Guid OccupationalPyramidLevelPublicId,
        Guid CompetencyPublicId,
        Guid CompetencyTypePublicId,
        Guid BehaviorLevelPublicId,
        IReadOnlyCollection<Guid>? ConductPublicIds,
        string? ExpectedEvidence,
        int SortOrder);

    public sealed record ConcurrencyRequest(Guid ConcurrencyToken);
}

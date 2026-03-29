using System.Globalization;
using System.Text;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Domain.JobProfiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
public sealed class JobProfilesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    IAuditService auditService,
    IUnitOfWork unitOfWork) : ControllerBase
{
    [HttpGet("api/v1/companies/{companyId:guid}/job-profiles")]
    [ProducesResponseType<PagedResponse<JobProfileListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<JobProfileListItemResponse>>> Search(
        Guid companyId,
        [FromQuery] JobProfileStatus? status,
        [FromQuery] Guid? orgUnitId,
        [FromQuery] Guid? salaryClass,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchJobProfilesQuery(companyId, status, orgUnitId, salaryClass, search, page, pageSize, includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/job-profiles/{id:guid}")]
    [ProducesResponseType<JobProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobProfileResponse>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetJobProfileByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/job-profiles/{id:guid}/vacancy-template")]
    [ProducesResponseType<JobProfileVacancyTemplateResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobProfileVacancyTemplateResponse>> VacancyTemplate(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetJobProfileVacancyTemplateQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/job-profiles/{id:guid}/print")]
    [ProducesResponseType<JobProfilePrintResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobProfilePrintResponse>> Print(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetJobProfilePrintQuery(id), cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        await auditService.LogAsync(
            new AuditLogEntry(
                AuditEventTypes.ReportPrinted,
                AuditEntityTypes.JobProfile,
                id,
                JobProfilePermissionCodes.ResourceKey,
                AuditActions.Print,
                "Printed job profile report.",
                After: new
                {
                    resourceKey = JobProfilePermissionCodes.ResourceKey,
                    format = "print",
                    filters = new { id },
                    rowCount = 1
                }),
            cancellationToken);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/job-profiles/{id:guid}/export")]
    [ProducesResponseType<JobProfilePrintResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Export(
        Guid id,
        [FromQuery] string format = "json",
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetJobProfilePrintQuery(id), cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(Result<JobProfilePrintResponse>.Failure(result.Error)).Result!;
        }

        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.ReportExported,
                    AuditEntityTypes.JobProfile,
                    id,
                    JobProfilePermissionCodes.ResourceKey,
                    AuditActions.Export,
                    "Exported job profile report.",
                    After: new
                    {
                        resourceKey = JobProfilePermissionCodes.ResourceKey,
                        format = "json",
                        filters = new { id },
                        rowCount = 1
                    }),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            return Ok(result.Value);
        }

        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.ReportExported,
                    AuditEntityTypes.JobProfile,
                    id,
                    JobProfilePermissionCodes.ResourceKey,
                    AuditActions.Export,
                    "Exported job profile report.",
                    After: new
                    {
                        resourceKey = JobProfilePermissionCodes.ResourceKey,
                        format = "csv",
                        filters = new { id },
                        rowCount = 1
                    }),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var csv = ExportCsv(result.Value);
            var bytes = Encoding.UTF8.GetBytes(csv);
            var fileName = $"job-profile-{result.Value.Profile.Code}.csv";
            return File(bytes, "text/csv", fileName);
        }

        return this.ToActionResult(Result<JobProfilePrintResponse>.Failure(JobProfileErrors.ExportFormatInvalid)).Result!;
    }

    [HttpPost("api/v1/companies/{companyId:guid}/job-profiles")]
    [ProducesResponseType<JobProfileResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<JobProfileResponse>> Create(
        Guid companyId,
        [FromBody] CreateJobProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateJobProfileCommand(
                companyId,
                request.Code,
                request.Title,
                request.Objective,
                request.OrgUnitPublicId,
                request.ReportsToJobProfilePublicId,
                request.PositionCategoryPublicId,
                request.StrategicObjectiveCatalogItemPublicId,
                request.AssignedWorkEquipmentCatalogItemPublicId,
                request.ResponsibilityCatalogItemPublicId,
                request.DecisionScope,
                request.AssignedResources,
                request.Responsibilities,
                request.BenefitsSummary,
                request.WorkingConditionSummary,
                request.MarketSalaryReference,
                request.ValuationNotes,
                request.EffectiveFromUtc,
                request.EffectiveToUtc,
                request.AllowInlineCatalogCreate,
                MapRequirements(request.Requirements),
                MapFunctions(request.Functions),
                MapRelations(request.Relations),
                MapCompetencies(request.Competencies),
                MapTrainings(request.Trainings),
                MapCompensations(request.Compensations),
                MapBenefits(request.Benefits),
                MapWorkingConditions(request.WorkingConditions),
                MapDependentPositions(request.DependentPositions)),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<JobProfileResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("api/v1/job-profiles/{id:guid}")]
    [ProducesResponseType<JobProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<JobProfileResponse>> Update(
        Guid id,
        [FromBody] UpdateJobProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateJobProfileCommand(
                id,
                request.Code,
                request.Title,
                request.Objective,
                request.OrgUnitPublicId,
                request.ReportsToJobProfilePublicId,
                request.PositionCategoryPublicId,
                request.StrategicObjectiveCatalogItemPublicId,
                request.AssignedWorkEquipmentCatalogItemPublicId,
                request.ResponsibilityCatalogItemPublicId,
                request.DecisionScope,
                request.AssignedResources,
                request.Responsibilities,
                request.BenefitsSummary,
                request.WorkingConditionSummary,
                request.MarketSalaryReference,
                request.ValuationNotes,
                request.EffectiveFromUtc,
                request.EffectiveToUtc,
                request.AllowInlineCatalogCreate,
                MapRequirements(request.Requirements),
                MapFunctions(request.Functions),
                MapRelations(request.Relations),
                MapCompetencies(request.Competencies),
                MapTrainings(request.Trainings),
                MapCompensations(request.Compensations),
                MapBenefits(request.Benefits),
                MapWorkingConditions(request.WorkingConditions),
                MapDependentPositions(request.DependentPositions),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/job-profiles/{id:guid}/publish")]
    [ProducesResponseType<JobProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<JobProfileResponse>> Publish(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PublishJobProfileCommand(id, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/job-profiles/{id:guid}/archive")]
    [ProducesResponseType<JobProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JobProfileResponse>> Archive(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ArchiveJobProfileCommand(id, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    private static IReadOnlyCollection<JobProfileRequirementInput> MapRequirements(IReadOnlyCollection<JobProfileRequirementRequest>? values) =>
        values?.Select(value => new JobProfileRequirementInput(
                value.RequirementType,
                value.RequirementTypeCatalogItemPublicId,
                value.CatalogItemPublicId,
                value.CatalogCode,
                value.CatalogName,
                value.Description,
                value.SortOrder))
            .ToArray() ?? [];

    private static IReadOnlyCollection<JobProfileFunctionInput> MapFunctions(IReadOnlyCollection<JobProfileFunctionRequest>? values) =>
        values?.Select(value => new JobProfileFunctionInput(
                value.FunctionType,
                value.FrequencyCatalogItemPublicId,
                value.Description,
                value.SortOrder))
            .ToArray() ?? [];

    private static IReadOnlyCollection<JobProfileRelationInput> MapRelations(IReadOnlyCollection<JobProfileRelationRequest>? values) =>
        values?.Select(value => new JobProfileRelationInput(
                value.RelationType,
                value.CatalogItemPublicId,
                value.CatalogCode,
                value.CatalogName,
                value.Counterpart,
                value.Notes,
                value.SortOrder))
            .ToArray() ?? [];

    private static IReadOnlyCollection<JobProfileCompetencyInput> MapCompetencies(IReadOnlyCollection<JobProfileCompetencyRequest>? values) =>
        values?.Select(value => new JobProfileCompetencyInput(
                value.CatalogItemPublicId,
                value.CatalogCode,
                value.CatalogName,
                value.Name,
                value.ExpectedLevel,
                value.Notes,
                value.SortOrder))
            .ToArray() ?? [];

    private static IReadOnlyCollection<JobProfileTrainingInput> MapTrainings(IReadOnlyCollection<JobProfileTrainingRequest>? values) =>
        values?.Select(value => new JobProfileTrainingInput(
                value.CatalogItemPublicId,
                value.CatalogCode,
                value.CatalogName,
                value.Name,
                value.Notes,
                value.SortOrder))
            .ToArray() ?? [];

    private static IReadOnlyCollection<JobProfileCompensationInput> MapCompensations(IReadOnlyCollection<JobProfileCompensationRequest>? values) =>
        values?.Select(value => new JobProfileCompensationInput(
                value.SalaryClassPublicId,
                value.SalaryClassCode,
                value.SalaryClassName,
                value.MinSalary,
                value.MaxSalary,
                value.CurrencyCode,
                value.WorkSchedule,
                value.IsPrimary))
            .ToArray() ?? [];

    private static IReadOnlyCollection<JobProfileBenefitInput> MapBenefits(IReadOnlyCollection<JobProfileBenefitRequest>? values) =>
        values?.Select(value => new JobProfileBenefitInput(
                value.CatalogItemPublicId,
                value.CatalogCode,
                value.CatalogName,
                value.Name,
                value.Notes,
                value.SortOrder))
            .ToArray() ?? [];

    private static IReadOnlyCollection<JobProfileWorkingConditionInput> MapWorkingConditions(IReadOnlyCollection<JobProfileWorkingConditionRequest>? values) =>
        values?.Select(value => new JobProfileWorkingConditionInput(
                value.WorkConditionTypeCatalogItemPublicId,
                value.CatalogItemPublicId,
                value.CatalogCode,
                value.CatalogName,
                value.Name,
                value.Notes,
                value.SortOrder))
            .ToArray() ?? [];

    private static IReadOnlyCollection<JobProfileDependentPositionInput> MapDependentPositions(IReadOnlyCollection<JobProfileDependentPositionRequest>? values) =>
        values?.Select(value => new JobProfileDependentPositionInput(value.DependentJobProfilePublicId, value.Quantity, value.Notes)).ToArray() ?? [];

    private static string ExportCsv(JobProfilePrintResponse payload)
    {
        var profile = payload.Profile;
        var lines = new List<string>
        {
            "Field,Value",
            $"PublicId,{Escape(profile.Id.ToString())}",
            $"Code,{Escape(profile.Code)}",
            $"Title,{Escape(profile.Title)}",
            $"Status,{Escape(profile.Status.ToString())}",
            $"Version,{profile.Version}",
            $"Objective,{Escape(profile.Objective)}",
            $"Responsibilities,{Escape(profile.Responsibilities)}",
            $"GeneratedAtUtc,{Escape(payload.GeneratedAtUtc.ToString("O", CultureInfo.InvariantCulture))}"
        };

        return string.Join("\n", lines);
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
        var escaped = value.Replace("\"", "\"\"");
        return needsQuotes ? $"\"{escaped}\"" : escaped;
    }

    public sealed record CreateJobProfileRequest(
        string Code,
        string Title,
        string? Objective,
        Guid? OrgUnitPublicId,
        Guid? ReportsToJobProfilePublicId,
        Guid? PositionCategoryPublicId,
        Guid? StrategicObjectiveCatalogItemPublicId,
        Guid? AssignedWorkEquipmentCatalogItemPublicId,
        Guid? ResponsibilityCatalogItemPublicId,
        string? DecisionScope,
        string? AssignedResources,
        string? Responsibilities,
        string? BenefitsSummary,
        string? WorkingConditionSummary,
        string? MarketSalaryReference,
        string? ValuationNotes,
        DateTime? EffectiveFromUtc,
        DateTime? EffectiveToUtc,
        bool AllowInlineCatalogCreate,
        IReadOnlyCollection<JobProfileRequirementRequest>? Requirements,
        IReadOnlyCollection<JobProfileFunctionRequest>? Functions,
        IReadOnlyCollection<JobProfileRelationRequest>? Relations,
        IReadOnlyCollection<JobProfileCompetencyRequest>? Competencies,
        IReadOnlyCollection<JobProfileTrainingRequest>? Trainings,
        IReadOnlyCollection<JobProfileCompensationRequest>? Compensations,
        IReadOnlyCollection<JobProfileBenefitRequest>? Benefits,
        IReadOnlyCollection<JobProfileWorkingConditionRequest>? WorkingConditions,
        IReadOnlyCollection<JobProfileDependentPositionRequest>? DependentPositions);

    public sealed record UpdateJobProfileRequest(
        string Code,
        string Title,
        string? Objective,
        Guid? OrgUnitPublicId,
        Guid? ReportsToJobProfilePublicId,
        Guid? PositionCategoryPublicId,
        Guid? StrategicObjectiveCatalogItemPublicId,
        Guid? AssignedWorkEquipmentCatalogItemPublicId,
        Guid? ResponsibilityCatalogItemPublicId,
        string? DecisionScope,
        string? AssignedResources,
        string? Responsibilities,
        string? BenefitsSummary,
        string? WorkingConditionSummary,
        string? MarketSalaryReference,
        string? ValuationNotes,
        DateTime? EffectiveFromUtc,
        DateTime? EffectiveToUtc,
        bool AllowInlineCatalogCreate,
        IReadOnlyCollection<JobProfileRequirementRequest>? Requirements,
        IReadOnlyCollection<JobProfileFunctionRequest>? Functions,
        IReadOnlyCollection<JobProfileRelationRequest>? Relations,
        IReadOnlyCollection<JobProfileCompetencyRequest>? Competencies,
        IReadOnlyCollection<JobProfileTrainingRequest>? Trainings,
        IReadOnlyCollection<JobProfileCompensationRequest>? Compensations,
        IReadOnlyCollection<JobProfileBenefitRequest>? Benefits,
        IReadOnlyCollection<JobProfileWorkingConditionRequest>? WorkingConditions,
        IReadOnlyCollection<JobProfileDependentPositionRequest>? DependentPositions,
        Guid ConcurrencyToken);

    public sealed record JobProfileRequirementRequest(
        JobRequirementType RequirementType,
        Guid? RequirementTypeCatalogItemPublicId,
        Guid? CatalogItemPublicId,
        string? CatalogCode,
        string? CatalogName,
        string Description,
        int SortOrder);

    public sealed record JobProfileFunctionRequest(
        JobFunctionType FunctionType,
        Guid? FrequencyCatalogItemPublicId,
        string Description,
        int SortOrder);

    public sealed record JobProfileRelationRequest(
        JobRelationType RelationType,
        Guid? CatalogItemPublicId,
        string? CatalogCode,
        string? CatalogName,
        string Counterpart,
        string? Notes,
        int SortOrder);

    public sealed record JobProfileCompetencyRequest(
        Guid? CatalogItemPublicId,
        string? CatalogCode,
        string? CatalogName,
        string Name,
        string? ExpectedLevel,
        string? Notes,
        int SortOrder);

    public sealed record JobProfileTrainingRequest(
        Guid? CatalogItemPublicId,
        string? CatalogCode,
        string? CatalogName,
        string Name,
        string? Notes,
        int SortOrder);

    public sealed record JobProfileCompensationRequest(
        Guid? SalaryClassPublicId,
        string? SalaryClassCode,
        string? SalaryClassName,
        decimal? MinSalary,
        decimal? MaxSalary,
        string? CurrencyCode,
        string? WorkSchedule,
        bool IsPrimary);

    public sealed record JobProfileBenefitRequest(
        Guid? CatalogItemPublicId,
        string? CatalogCode,
        string? CatalogName,
        string Name,
        string? Notes,
        int SortOrder);

    public sealed record JobProfileWorkingConditionRequest(
        Guid? WorkConditionTypeCatalogItemPublicId,
        Guid? CatalogItemPublicId,
        string? CatalogCode,
        string? CatalogName,
        string Name,
        string? Notes,
        int SortOrder);

    public sealed record JobProfileDependentPositionRequest(
        Guid DependentJobProfilePublicId,
        int Quantity,
        string? Notes);

    public sealed record ConcurrencyRequest(Guid ConcurrencyToken);
}

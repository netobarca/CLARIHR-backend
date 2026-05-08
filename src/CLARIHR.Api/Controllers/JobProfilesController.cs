using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.CompetencyFramework;
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
                request.ResolvedOrgUnitPublicId,
                request.ResolvedReportsToJobProfilePublicId,
                request.ResolvedPositionCategoryPublicId,
                request.ResolvedStrategicObjectiveCatalogItemPublicId,
                request.ResolvedAssignedWorkEquipmentCatalogItemPublicId,
                request.ResolvedResponsibilityCatalogItemPublicId,
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
                [],
                [],
                [],
                [],
                [],
                MapCompensation(request.ResolveCompensation()),
                [],
                [],
                []),
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
                request.ResolvedOrgUnitPublicId,
                request.ResolvedReportsToJobProfilePublicId,
                request.ResolvedPositionCategoryPublicId,
                request.ResolvedStrategicObjectiveCatalogItemPublicId,
                request.ResolvedAssignedWorkEquipmentCatalogItemPublicId,
                request.ResolvedResponsibilityCatalogItemPublicId,
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
                [],
                [],
                [],
                [],
                [],
                MapCompensation(request.ResolveCompensation()),
                [],
                [],
                [],
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

    private static JobProfileCompensationInput? MapCompensation(JobProfileCompensationRequest? value) =>
        value is null
            ? null
            : new JobProfileCompensationInput(
                value.SalaryTabulatorLineId,
                value.ResolvedSalaryClassId,
                value.SalaryClassCode,
                value.CurrencyCode,
                value.MinSalary,
                value.MaxSalary);

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

    public sealed class CreateJobProfileRequest : JobProfileMutationRequest;

    public sealed class UpdateJobProfileRequest : JobProfileMutationRequest
    {
        public Guid ConcurrencyToken { get; init; }
    }

    public abstract class JobProfileMutationRequest
    {
        public string Code { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string? Objective { get; init; }
        public Guid OrgUnitPublicId { get; init; }
        public Guid? OrgUnitId { get; init; }
        public Guid? ReportsToJobProfilePublicId { get; init; }
        public Guid? ReportsToJobProfileId { get; init; }
        public Guid? PositionCategoryPublicId { get; init; }
        public Guid? PositionCategoryId { get; init; }
        public Guid? StrategicObjectiveCatalogItemPublicId { get; init; }
        public Guid? StrategicObjectiveCatalogItemId { get; init; }
        public Guid? AssignedWorkEquipmentCatalogItemPublicId { get; init; }
        public Guid? AssignedWorkEquipmentCatalogItemId { get; init; }
        public Guid? ResponsibilityCatalogItemPublicId { get; init; }
        public Guid? ResponsibilityCatalogItemId { get; init; }
        public string? DecisionScope { get; init; }
        public string? AssignedResources { get; init; }
        public string? Responsibilities { get; init; }
        public string? BenefitsSummary { get; init; }
        public string? WorkingConditionSummary { get; init; }
        public string? MarketSalaryReference { get; init; }
        public string? ValuationNotes { get; init; }
        public DateTime? EffectiveFromUtc { get; init; }
        public DateTime? EffectiveToUtc { get; init; }
        public bool AllowInlineCatalogCreate { get; init; }
        public JobProfileCompensationRequest? Compensation { get; init; }
        public IReadOnlyCollection<JobProfileCompensationRequest>? Compensations { get; init; }

        [JsonIgnore]
        public Guid ResolvedOrgUnitPublicId => OrgUnitPublicId != Guid.Empty ? OrgUnitPublicId : OrgUnitId ?? Guid.Empty;

        [JsonIgnore]
        public Guid? ResolvedReportsToJobProfilePublicId => ReportsToJobProfilePublicId ?? ReportsToJobProfileId;

        [JsonIgnore]
        public Guid? ResolvedPositionCategoryPublicId => PositionCategoryPublicId ?? PositionCategoryId;

        [JsonIgnore]
        public Guid? ResolvedStrategicObjectiveCatalogItemPublicId => StrategicObjectiveCatalogItemPublicId ?? StrategicObjectiveCatalogItemId;

        [JsonIgnore]
        public Guid? ResolvedAssignedWorkEquipmentCatalogItemPublicId => AssignedWorkEquipmentCatalogItemPublicId ?? AssignedWorkEquipmentCatalogItemId;

        [JsonIgnore]
        public Guid? ResolvedResponsibilityCatalogItemPublicId => ResponsibilityCatalogItemPublicId ?? ResponsibilityCatalogItemId;

        public JobProfileCompensationRequest? ResolveCompensation()
        {
            if (Compensation is not null)
            {
                return Compensation;
            }

            if (Compensations is null || Compensations.Count == 0)
            {
                return null;
            }

            return Compensations.FirstOrDefault(item => item.IsPrimary == true) ?? Compensations.First();
        }
    }

    public sealed class JobProfileCompensationRequest
    {
        public Guid? SalaryTabulatorLineId { get; init; }
        public Guid? SalaryClassPublicId { get; init; }
        public Guid? SalaryClassId { get; init; }
        public string? SalaryClassCode { get; init; }
        public decimal? MinSalary { get; init; }
        public decimal? MaxSalary { get; init; }
        public string? CurrencyCode { get; init; }
        public string? WorkSchedule { get; init; }
        public bool? IsPrimary { get; init; }

        [JsonIgnore]
        public Guid? ResolvedSalaryClassId => SalaryClassPublicId ?? SalaryClassId;
    }

    public sealed record ConcurrencyRequest(Guid ConcurrencyToken);
}

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
using Microsoft.AspNetCore.JsonPatch;
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
    [ProducesResponseType<JobProfileCoreResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<JobProfileCoreResponse>> Create(
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
                MapCompensation(request.ResolveCompensation())),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<JobProfileCoreResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("api/v1/job-profiles/{id:guid}")]
    [ProducesResponseType<JobProfileCoreResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<JobProfileCoreResponse>> Update(
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
                MapCompensation(request.ResolveCompensation()),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/job-profiles/{id:guid}")]
    [ProducesResponseType<JobProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<JobProfileCoreResponse>> Patch(
        Guid id,
        [FromBody] JsonPatchDocument<UpdateJobProfileRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        if (patchDoc is null)
        {
            return BadRequest(ProblemDetailsFactory.CreateProblemDetails(HttpContext, statusCode: StatusCodes.Status400BadRequest, detail: "Invalid patch document."));
        }

        var currentProfileResult = await queryDispatcher.SendAsync(new GetJobProfileCoreByIdQuery(id), cancellationToken);
        if (currentProfileResult.IsFailure)
        {
            return this.ToActionResult(Result<JobProfileCoreResponse>.Failure(currentProfileResult.Error));
        }

        var updateRequest = MapToUpdateRequest(currentProfileResult.Value);
        patchDoc.ApplyTo(updateRequest, ModelState);

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var updateResult = await commandDispatcher.SendAsync(
            new UpdateJobProfileCommand(
                id,
                updateRequest.Code,
                updateRequest.Title,
                updateRequest.Objective,
                updateRequest.ResolvedOrgUnitPublicId,
                updateRequest.ResolvedReportsToJobProfilePublicId,
                updateRequest.ResolvedPositionCategoryPublicId,
                updateRequest.ResolvedStrategicObjectiveCatalogItemPublicId,
                updateRequest.ResolvedAssignedWorkEquipmentCatalogItemPublicId,
                updateRequest.ResolvedResponsibilityCatalogItemPublicId,
                updateRequest.DecisionScope,
                updateRequest.AssignedResources,
                updateRequest.Responsibilities,
                updateRequest.BenefitsSummary,
                updateRequest.WorkingConditionSummary,
                updateRequest.MarketSalaryReference,
                updateRequest.ValuationNotes,
                updateRequest.EffectiveFromUtc,
                updateRequest.EffectiveToUtc,
                updateRequest.AllowInlineCatalogCreate,
                MapCompensation(updateRequest.ResolveCompensation()),
                updateRequest.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(updateResult);
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

    private static UpdateJobProfileRequest MapToUpdateRequest(JobProfileCoreResponse response) =>
        new()
        {
            Code = response.Code,
            Title = response.Title,
            Objective = response.Objective,
            OrgUnitPublicId = response.OrgUnitId ?? Guid.Empty,
            ReportsToJobProfilePublicId = response.ReportsToJobProfileId,
            PositionCategoryPublicId = response.PositionCategoryId,
            StrategicObjectiveCatalogItemPublicId = response.StrategicObjectiveCatalogItemId,
            AssignedWorkEquipmentCatalogItemPublicId = response.AssignedWorkEquipmentCatalogItemId,
            ResponsibilityCatalogItemPublicId = response.ResponsibilityCatalogItemId,
            DecisionScope = response.DecisionScope,
            AssignedResources = response.AssignedResources,
            Responsibilities = response.Responsibilities,
            BenefitsSummary = response.BenefitsSummary,
            WorkingConditionSummary = response.WorkingConditionSummary,
            MarketSalaryReference = response.MarketSalaryReference,
            ValuationNotes = response.ValuationNotes,
            EffectiveFromUtc = response.EffectiveFromUtc,
            EffectiveToUtc = response.EffectiveToUtc,
            AllowInlineCatalogCreate = false,
            Compensation = response.Compensation is not null
                ? new JobProfileCompensationRequest
                {
                    SalaryClassPublicId = response.Compensation.SalaryClassId,
                    SalaryClassCode = response.Compensation.SalaryScaleCode
                }
                : null,
            ConcurrencyToken = response.ConcurrencyToken
        };

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
        public Guid ConcurrencyToken { get; set; }
    }

    public abstract class JobProfileMutationRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Objective { get; set; }
        public Guid OrgUnitPublicId { get; set; }
        public Guid? OrgUnitId { get; set; }
        public Guid? ReportsToJobProfilePublicId { get; set; }
        public Guid? ReportsToJobProfileId { get; set; }
        public Guid? PositionCategoryPublicId { get; set; }
        public Guid? PositionCategoryId { get; set; }
        public Guid? StrategicObjectiveCatalogItemPublicId { get; set; }
        public Guid? StrategicObjectiveCatalogItemId { get; set; }
        public Guid? AssignedWorkEquipmentCatalogItemPublicId { get; set; }
        public Guid? AssignedWorkEquipmentCatalogItemId { get; set; }
        public Guid? ResponsibilityCatalogItemPublicId { get; set; }
        public Guid? ResponsibilityCatalogItemId { get; set; }
        public string? DecisionScope { get; set; }
        public string? AssignedResources { get; set; }
        public string? Responsibilities { get; set; }
        public string? BenefitsSummary { get; set; }
        public string? WorkingConditionSummary { get; set; }
        public string? MarketSalaryReference { get; set; }
        public string? ValuationNotes { get; set; }
        public DateTime? EffectiveFromUtc { get; set; }
        public DateTime? EffectiveToUtc { get; set; }
        public bool AllowInlineCatalogCreate { get; set; }
        public JobProfileCompensationRequest? Compensation { get; set; }
        public IReadOnlyCollection<JobProfileCompensationRequest>? Compensations { get; set; }

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
        public Guid? SalaryTabulatorLineId { get; set; }
        public Guid? SalaryClassPublicId { get; set; }
        public Guid? SalaryClassId { get; set; }
        public string? SalaryClassCode { get; set; }
        public decimal? MinSalary { get; set; }
        public decimal? MaxSalary { get; set; }
        public string? CurrencyCode { get; set; }
        public string? WorkSchedule { get; set; }
        public bool? IsPrimary { get; set; }

        [JsonIgnore]
        public Guid? ResolvedSalaryClassId => SalaryClassPublicId ?? SalaryClassId;
    }

    public sealed record ConcurrencyRequest(Guid ConcurrencyToken);
}

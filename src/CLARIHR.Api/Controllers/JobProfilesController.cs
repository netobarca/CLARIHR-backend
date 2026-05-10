
using System.Text.Json;
using System.Text.Json.Serialization;
using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.CompetencyFramework;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Domain.JobProfiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1")]
public sealed class JobProfilesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("companies/{companyId:guid}/job-profiles")]
    [Authorize(Policy = JobProfilePolicies.Read)]
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

    [HttpGet("job-profiles/{publicId:guid}")]
    [Authorize(Policy = JobProfilePolicies.Read)]
    [ProducesResponseType<JobProfileEntityResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobProfileEntityResponse>> GetById(Guid publicId, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetJobProfileByIdQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("job-profiles/{publicId:guid}/vacancy-template")]
    [Authorize(Policy = JobProfilePolicies.Read)]
    [ProducesResponseType<JobProfileVacancyTemplateResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobProfileVacancyTemplateResponse>> VacancyTemplate(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetJobProfileVacancyTemplateQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("job-profiles/{publicId:guid}/print")]
    [Authorize(Policy = JobProfilePolicies.Read)]
    [ProducesResponseType<JobProfilePrintResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JobProfilePrintResponse>> Print(Guid publicId, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetJobProfilePrintQuery(publicId), cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(result);
        }

        var auditResult = await commandDispatcher.SendAsync(new MarkJobProfilePrintedCommand(publicId), cancellationToken);
        if (auditResult.IsFailure)
        {
            return this.ToActionResult(Result<JobProfilePrintResponse>.Failure(auditResult.Error));
        }

        return this.ToActionResult(result);
    }

    [HttpPost("companies/{companyId:guid}/job-profiles")]
    [Authorize(Policy = JobProfilePolicies.Manage)]
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
                JobProfilesMappers.MapCompensation(request.ResolveCompensation())),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<JobProfileCoreResponse>.Failure(result.Error))
            : CreatedAtAction(nameof(GetById), new { publicId = result.Value.Id }, result.Value);
    }

    [HttpPut("job-profiles/{publicId:guid}")]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [ProducesResponseType<JobProfileCoreResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<JobProfileCoreResponse>> Update(
        Guid publicId,
        [FromBody] UpdateJobProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateJobProfileCommand(
                publicId,
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
                JobProfilesMappers.MapCompensation(request.ResolveCompensation()),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("job-profiles/{publicId:guid}")]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [ProducesResponseType<JobProfileCoreResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<JobProfileCoreResponse>> Patch(
        Guid publicId,
        [FromBody] JsonPatchDocument<UpdateJobProfileRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        if (patchDoc is null)
        {
            return BadRequest(ProblemDetailsFactory.CreateProblemDetails(HttpContext, statusCode: StatusCodes.Status400BadRequest, detail: "Invalid patch document."));
        }

        var updateResult = await commandDispatcher.SendAsync(
            new PatchJobProfileCommand(publicId, MapPatchOperations(patchDoc)),
            cancellationToken);

        return this.ToActionResult(updateResult);
    }

    [HttpPatch("job-profiles/{publicId:guid}/publish")]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [ProducesResponseType<JobProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<JobProfileResponse>> Publish(
        Guid publicId,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PublishJobProfileCommand(publicId, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("job-profiles/{publicId:guid}/archive")]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [ProducesResponseType<JobProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JobProfileResponse>> Archive(
        Guid publicId,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ArchiveJobProfileCommand(publicId, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    private static IReadOnlyCollection<JobProfilePatchOperation> MapPatchOperations(JsonPatchDocument<UpdateJobProfileRequest> patchDoc) =>
        patchDoc.Operations
            .Select(operation => new JobProfilePatchOperation(
                operation.op,
                operation.path,
                operation.from,
                MapPatchValue(operation.value)))
            .ToArray();

    private static JsonElement? MapPatchValue(object? value)
    {
        if (value is null)
        {
            return JsonSerializer.SerializeToElement<object?>(null);
        }

        if (value is JToken token)
        {
            using var document = JsonDocument.Parse(token.ToString(Newtonsoft.Json.Formatting.None));
            return document.RootElement.Clone();
        }

        return JsonSerializer.SerializeToElement(value, value.GetType());
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

        public JobProfileCompensationRequest? ResolveCompensation() => Compensation;
    }

    public sealed record ConcurrencyRequest(Guid ConcurrencyToken);
}

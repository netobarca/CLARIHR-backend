
using System.Text.Json;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
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
[Consumes("application/json")]
[Produces("application/json")]
public sealed class JobProfilesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("companies/{companyId:guid}/job-profiles")]
    [Authorize(Policy = JobProfilePolicies.Read)]
    [ProducesResponseType<PagedResponse<JobProfileListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
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
    [ProducesStandardErrors(StandardErrorSet.Read)]
    public async Task<ActionResult<JobProfileEntityResponse>> GetById(Guid publicId, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetJobProfileByIdQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }



    [HttpPost("companies/{companyId:guid}/job-profiles")]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [ProducesResponseType<JobProfileCoreResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
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
                request.AllowInlineCatalogCreate),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<JobProfileCoreResponse>.Failure(result.Error))
            : CreatedAtAction(nameof(GetById), new { publicId = result.Value.Id }, result.Value);
    }

    [HttpPut("job-profiles/{publicId:guid}")]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [ProducesResponseType<JobProfileCoreResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
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
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("job-profiles/{publicId:guid}")]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [ProducesResponseType<JobProfileCoreResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
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
        public JobProfileStatus? Status { get; set; }
    }

    public abstract class JobProfileMutationRequest
    {
        public string Code { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Objective { get; set; }
        public Guid OrgUnitPublicId { get; set; }
        public Guid? ReportsToJobProfilePublicId { get; set; }
        public Guid? PositionCategoryPublicId { get; set; }
        public Guid? StrategicObjectiveCatalogItemPublicId { get; set; }
        public Guid? AssignedWorkEquipmentCatalogItemPublicId { get; set; }
        public Guid? ResponsibilityCatalogItemPublicId { get; set; }
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
    }
}

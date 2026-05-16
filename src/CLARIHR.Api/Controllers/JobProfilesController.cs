using Asp.Versioning;
using System.ComponentModel.DataAnnotations;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.CompetencyFramework;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Domain.JobProfiles;
using Microsoft.AspNetCore.Authorization;
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
[Tags("Job Profiles")]
public sealed class JobProfilesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("companies/{companyId:guid}/job-profiles")]
    [ProducesResponseType<PagedResponse<JobProfileListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "Search job profiles",
        Description = """
            Returns a paginated list of job profiles for the specified company.

            Supports optional filtering by `status`, `orgUnitId` and `salaryClass`,
            plus a free-text query (`q`) matched against code and title.
            Set `includeAllowedActions=true` to include, per item, the set of
            operations the current user is authorized to perform on it.
            """)]
    public async Task<ActionResult<PagedResponse<JobProfileListItemResponse>>> Search(
        Guid companyId,
        [FromQuery] JobProfileStatus? status,
        [FromQuery] Guid? orgUnitId,
        [FromQuery] Guid? salaryClass,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [Range(1, JobProfileValidationRules.MaxPageSize)]
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
    [ProducesResponseType<JobProfileEntityResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a job profile by id",
        Description = """
            Returns the full job profile entity, including its sub-resources
            (functions, requirements, competencies, benefits and the rest).

            The `concurrencyToken` in the response body is required in the
            `If-Match` header of subsequent `PUT`/`PATCH`/`DELETE` requests
            to prevent lost updates.
            """)]
    public async Task<ActionResult<JobProfileEntityResponse>> GetById(Guid publicId, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetJobProfileByIdQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }



    [HttpPost("companies/{companyId:guid}/job-profiles")]
    [ProducesResponseType<JobProfileCoreResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Create a job profile",
        Description = """
            Creates a new job profile for the specified company.

            **Inline Catalog Create**: When `allowInlineCatalogCreate` is `true`,
            catalog items referenced by code/name that do not yet exist in the system
            will be created on the fly during the operation.
            This requires the **`JobCatalogs.Admin`** permission in addition to `JobProfiles.Admin`.
            If the caller lacks `JobCatalogs.Admin`, the request will fail with
            `403 Forbidden` and error code `JOB_CATALOG_INLINE_CREATE_FORBIDDEN`.
            """)]
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

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetById),
            value => new { publicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPut("job-profiles/{publicId:guid}")]
    [ProducesResponseType<JobProfileCoreResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Update a job profile",
        Description = """
            Replaces the core fields of an existing job profile.
            Requires the `If-Match` header with the current `concurrencyToken` to prevent lost updates.

            **Inline Catalog Create**: When `allowInlineCatalogCreate` is `true`,
            catalog items referenced by code/name that do not yet exist in the system
            will be created on the fly during the operation.
            This requires the **`JobCatalogs.Admin`** permission in addition to `JobProfiles.Admin`.
            If the caller lacks `JobCatalogs.Admin`, the request will fail with
            `403 Forbidden` and error code `JOB_CATALOG_INLINE_CREATE_FORBIDDEN`.

            **Status transitions** must be performed via `PATCH /job-profiles/{publicId}`,
            not via this endpoint. The `status` field is intentionally absent from the request body.
            """)]
    public async Task<ActionResult<JobProfileCoreResponse>> Update(
        Guid publicId,
        [FromIfMatch] Guid concurrencyToken,
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
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("job-profiles/{publicId:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<JobProfileCoreResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a job profile",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type
            `application/json-patch+json`) to an existing job profile.
            Requires the `If-Match` header with the current `concurrencyToken`
            to prevent lost updates.

            Unlike `PUT /job-profiles/{publicId}`, this endpoint **can change the
            `status`** field, making it the supported mechanism for job profile
            **status transitions**.
            """)]
    public async Task<ActionResult<JobProfileCoreResponse>> Patch(
        Guid publicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchJobProfileRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var updateResult = await commandDispatcher.SendAsync(
            new PatchJobProfileCommand(publicId, concurrencyToken, JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new JobProfilePatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(updateResult, value => value.ConcurrencyToken);
    }

    public sealed class CreateJobProfileRequest : JobProfileMutationRequest;

    public sealed class UpdateJobProfileRequest : JobProfileMutationRequest;

    public sealed class PatchJobProfileRequest : JobProfileMutationRequest
    {
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

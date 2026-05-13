using System.Text.Json;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
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
[Route("api/v1/job-profiles/{jobProfilePublicId:guid}/requirements")]
[Consumes("application/json")]
[Produces("application/json")]
public sealed class JobProfileRequirementsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = JobProfilePolicies.Read)]
    [ProducesResponseType<IReadOnlyCollection<JobProfileRequirementResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    public async Task<ActionResult<IReadOnlyCollection<JobProfileRequirementResponse>>> Get(
        Guid jobProfilePublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetJobProfileRequirementsQuery(jobProfilePublicId),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [ProducesResponseType<JobProfileRequirementResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<JobProfileRequirementResponse>> Add(
        Guid jobProfilePublicId,
        [FromBody] AddRequirementRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddJobProfileRequirementCommand(
                jobProfilePublicId,
                request.RequirementType,
                request.RequirementTypeCatalogItemPublicId,
                request.CatalogItemPublicId,
                request.CatalogCode,
                request.CatalogName,
                request.Description,
                request.SortOrder),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(result).Result!;
        }

        return Created($"{Request.Path}/{result.Value.RequirementPublicId:D}", result.Value);
    }

    [HttpPut("{requirementPublicId:guid}")]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [ProducesResponseType<JobProfileRequirementResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<JobProfileRequirementResponse>> Update(
        Guid jobProfilePublicId,
        Guid requirementPublicId,
        [FromBody] UpdateRequirementRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateJobProfileRequirementCommand(
                jobProfilePublicId,
                requirementPublicId,
                request.RequirementType,
                request.RequirementTypeCatalogItemPublicId,
                request.CatalogItemPublicId,
                request.CatalogCode,
                request.CatalogName,
                request.Description,
                request.SortOrder,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("{requirementPublicId:guid}")]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [Consumes("application/json-patch+json")]
    [ProducesResponseType<JobProfileRequirementResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<JobProfileRequirementResponse>> Patch(
        Guid jobProfilePublicId,
        Guid requirementPublicId,
        [FromBody] JsonPatchDocument<UpdateRequirementRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        if (patchDoc is null)
        {
            return BadRequest(ProblemDetailsFactory.CreateProblemDetails(
                HttpContext,
                statusCode: StatusCodes.Status400BadRequest,
                detail: "Invalid patch document."));
        }

        var result = await commandDispatcher.SendAsync(
            new PatchJobProfileRequirementCommand(
                jobProfilePublicId,
                requirementPublicId,
                MapPatchOperations(patchDoc)),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpDelete("{requirementPublicId:guid}")]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [ProducesResponseType<JobProfileRequirementResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<JobProfileRequirementResponse>> Remove(
        Guid jobProfilePublicId,
        Guid requirementPublicId,
        [FromHeader(Name = IfMatchHeader.HeaderName)] string? ifMatch,
        CancellationToken cancellationToken = default)
    {
        if (!IfMatchHeader.TryParseConcurrencyToken(ifMatch, out var concurrencyToken))
        {
            return BadRequest(ProblemDetailsFactory.CreateProblemDetails(
                HttpContext,
                statusCode: StatusCodes.Status400BadRequest,
                detail: IfMatchHeader.MissingDetail));
        }

        var result = await commandDispatcher.SendAsync(
            new RemoveJobProfileRequirementCommand(jobProfilePublicId, requirementPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    private static IReadOnlyCollection<JobProfileRequirementPatchOperation> MapPatchOperations(JsonPatchDocument<UpdateRequirementRequest> patchDoc) =>
        patchDoc.Operations
            .Select(operation => new JobProfileRequirementPatchOperation(
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

    public sealed class AddRequirementRequest
    {
        public JobRequirementType RequirementType { get; set; }
        public Guid? RequirementTypeCatalogItemPublicId { get; set; }
        public Guid? CatalogItemPublicId { get; set; }
        public string? CatalogCode { get; set; }
        public string? CatalogName { get; set; }
        public string Description { get; set; } = string.Empty;
        public int SortOrder { get; set; }
    }

    public sealed class UpdateRequirementRequest
    {
        public JobRequirementType RequirementType { get; set; }
        public Guid? RequirementTypeCatalogItemPublicId { get; set; }
        public Guid? CatalogItemPublicId { get; set; }
        public string? CatalogCode { get; set; }
        public string? CatalogName { get; set; }
        public string Description { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public Guid ConcurrencyToken { get; set; }
    }
}

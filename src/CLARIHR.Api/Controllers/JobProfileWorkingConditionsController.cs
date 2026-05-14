using System.Text.Json;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Application.Features.JobProfiles.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/job-profiles/{jobProfilePublicId:guid}/working-conditions")]
[Consumes("application/json")]
[Produces("application/json")]
public sealed class JobProfileWorkingConditionsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = JobProfilePolicies.Read)]
    [ProducesResponseType<IReadOnlyCollection<JobProfileWorkingConditionResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    public async Task<ActionResult<IReadOnlyCollection<JobProfileWorkingConditionResponse>>> Get(
        Guid jobProfilePublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetJobProfileWorkingConditionsQuery(jobProfilePublicId),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [ProducesResponseType<JobProfileWorkingConditionResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<JobProfileWorkingConditionResponse>> Add(
        Guid jobProfilePublicId,
        [FromBody] AddWorkingConditionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddJobProfileWorkingConditionCommand(
                jobProfilePublicId,
                request.WorkConditionTypeCatalogItemId,
                request.CatalogItemId,
                request.Name,
                request.Notes,
                request.SortOrder),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(result).Result!;
        }

        return Created($"{Request.Path}/{result.Value.Id:D}", result.Value);
    }

    [HttpPut("{workingConditionPublicId:guid}")]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [ProducesResponseType<JobProfileWorkingConditionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<JobProfileWorkingConditionResponse>> Update(
        Guid jobProfilePublicId,
        Guid workingConditionPublicId,
        [FromBody] UpdateWorkingConditionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateJobProfileWorkingConditionCommand(
                jobProfilePublicId,
                workingConditionPublicId,
                request.WorkConditionTypeCatalogItemId,
                request.CatalogItemId,
                request.Name,
                request.Notes,
                request.SortOrder,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("{workingConditionPublicId:guid}")]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [Consumes("application/json-patch+json")]
    [ProducesResponseType<JobProfileWorkingConditionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<JobProfileWorkingConditionResponse>> Patch(
        Guid jobProfilePublicId,
        Guid workingConditionPublicId,
        [FromBody] JsonPatchDocument<UpdateWorkingConditionRequest> patchDoc,
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
            new PatchJobProfileWorkingConditionCommand(
                jobProfilePublicId,
                workingConditionPublicId,
                MapPatchOperations(patchDoc)),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpDelete("{workingConditionPublicId:guid}")]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [ProducesResponseType<JobProfileWorkingConditionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<JobProfileWorkingConditionResponse>> Remove(
        Guid jobProfilePublicId,
        Guid workingConditionPublicId,
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
            new RemoveJobProfileWorkingConditionCommand(jobProfilePublicId, workingConditionPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    private static IReadOnlyCollection<JobProfileWorkingConditionPatchOperation> MapPatchOperations(JsonPatchDocument<UpdateWorkingConditionRequest> patchDoc) =>
        patchDoc.Operations
            .Select(operation => new JobProfileWorkingConditionPatchOperation(
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

    public sealed class AddWorkingConditionRequest
    {
        public Guid? WorkConditionTypeCatalogItemId { get; set; }
        public Guid? CatalogItemId { get; set; }
        public string? Name { get; set; }
        public string? Notes { get; set; }
        public int SortOrder { get; set; }
    }

    public sealed class UpdateWorkingConditionRequest
    {
        public Guid? WorkConditionTypeCatalogItemId { get; set; }
        public Guid? CatalogItemId { get; set; }
        public string? Name { get; set; }
        public string? Notes { get; set; }
        public int SortOrder { get; set; }
        public Guid ConcurrencyToken { get; set; }
    }
}

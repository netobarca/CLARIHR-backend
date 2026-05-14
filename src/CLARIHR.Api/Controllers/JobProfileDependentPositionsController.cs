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
[Route("api/v1/job-profiles/{jobProfilePublicId:guid}/dependent-positions")]
[Consumes("application/json")]
[Produces("application/json")]
public sealed class JobProfileDependentPositionsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = JobProfilePolicies.Read)]
    [ProducesResponseType<IReadOnlyCollection<JobProfileDependentPositionResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    public async Task<ActionResult<IReadOnlyCollection<JobProfileDependentPositionResponse>>> Get(
        Guid jobProfilePublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetJobProfileDependentPositionsQuery(jobProfilePublicId),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [ProducesResponseType<JobProfileDependentPositionResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<JobProfileDependentPositionResponse>> Add(
        Guid jobProfilePublicId,
        [FromBody] AddDependentPositionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddJobProfileDependentPositionCommand(
                jobProfilePublicId,
                request.DependentJobProfileId,
                request.Quantity,
                request.Notes),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(result).Result!;
        }

        return Created($"{Request.Path}/{result.Value.Id:D}", result.Value);
    }

    [HttpPut("{dependentPositionPublicId:guid}")]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [ProducesResponseType<JobProfileDependentPositionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<JobProfileDependentPositionResponse>> Update(
        Guid jobProfilePublicId,
        Guid dependentPositionPublicId,
        [FromBody] UpdateDependentPositionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateJobProfileDependentPositionCommand(
                jobProfilePublicId,
                dependentPositionPublicId,
                request.DependentJobProfileId,
                request.Quantity,
                request.Notes,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("{dependentPositionPublicId:guid}")]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [Consumes("application/json-patch+json")]
    [ProducesResponseType<JobProfileDependentPositionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<JobProfileDependentPositionResponse>> Patch(
        Guid jobProfilePublicId,
        Guid dependentPositionPublicId,
        [FromBody] JsonPatchDocument<UpdateDependentPositionRequest> patchDoc,
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
            new PatchJobProfileDependentPositionCommand(
                jobProfilePublicId,
                dependentPositionPublicId,
                MapPatchOperations(patchDoc)),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpDelete("{dependentPositionPublicId:guid}")]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [ProducesResponseType<JobProfileDependentPositionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<JobProfileDependentPositionResponse>> Remove(
        Guid jobProfilePublicId,
        Guid dependentPositionPublicId,
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
            new RemoveJobProfileDependentPositionCommand(jobProfilePublicId, dependentPositionPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    private static IReadOnlyCollection<JobProfileDependentPositionPatchOperation> MapPatchOperations(JsonPatchDocument<UpdateDependentPositionRequest> patchDoc) =>
        patchDoc.Operations
            .Select(operation => new JobProfileDependentPositionPatchOperation(
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

    public sealed class AddDependentPositionRequest
    {
        public Guid DependentJobProfileId { get; set; }
        public int Quantity { get; set; }
        public string? Notes { get; set; }
    }

    public sealed class UpdateDependentPositionRequest
    {
        public Guid DependentJobProfileId { get; set; }
        public int Quantity { get; set; }
        public string? Notes { get; set; }
        public Guid ConcurrencyToken { get; set; }
    }
}

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
[Route("api/v1/job-profiles/{jobProfilePublicId:guid}/competencies")]
[Consumes("application/json")]
[Produces("application/json")]
public sealed class JobProfileCompetenciesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet]
    [Authorize(Policy = JobProfilePolicies.Read)]
    [ProducesResponseType<IReadOnlyCollection<JobProfileLegacyCompetencyResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    public async Task<ActionResult<IReadOnlyCollection<JobProfileLegacyCompetencyResponse>>> Get(
        Guid jobProfilePublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetJobProfileCompetenciesQuery(jobProfilePublicId),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [ProducesResponseType<JobProfileLegacyCompetencyResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<JobProfileLegacyCompetencyResponse>> Add(
        Guid jobProfilePublicId,
        [FromBody] AddCompetencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddJobProfileCompetencyCommand(
                jobProfilePublicId,
                request.CatalogItemPublicId,
                request.Name,
                request.ExpectedLevel,
                request.Notes,
                request.SortOrder),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(result).Result!;
        }

        return Created($"{Request.Path}/{result.Value.CompetencyPublicId:D}", result.Value);
    }

    [HttpPut("{competencyPublicId:guid}")]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [ProducesResponseType<JobProfileLegacyCompetencyResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<JobProfileLegacyCompetencyResponse>> Update(
        Guid jobProfilePublicId,
        Guid competencyPublicId,
        [FromBody] UpdateCompetencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateJobProfileCompetencyCommand(
                jobProfilePublicId,
                competencyPublicId,
                request.CatalogItemPublicId,
                request.Name,
                request.ExpectedLevel,
                request.Notes,
                request.SortOrder,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("{competencyPublicId:guid}")]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [Consumes("application/json-patch+json")]
    [ProducesResponseType<JobProfileLegacyCompetencyResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<JobProfileLegacyCompetencyResponse>> Patch(
        Guid jobProfilePublicId,
        Guid competencyPublicId,
        [FromBody] JsonPatchDocument<UpdateCompetencyRequest> patchDoc,
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
            new PatchJobProfileCompetencyCommand(
                jobProfilePublicId,
                competencyPublicId,
                MapPatchOperations(patchDoc)),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpDelete("{competencyPublicId:guid}")]
    [Authorize(Policy = JobProfilePolicies.Manage)]
    [ProducesResponseType<JobProfileLegacyCompetencyResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    public async Task<ActionResult<JobProfileLegacyCompetencyResponse>> Remove(
        Guid jobProfilePublicId,
        Guid competencyPublicId,
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
            new RemoveJobProfileCompetencyCommand(jobProfilePublicId, competencyPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    private static IReadOnlyCollection<JobProfileCompetencyPatchOperation> MapPatchOperations(JsonPatchDocument<UpdateCompetencyRequest> patchDoc) =>
        patchDoc.Operations
            .Select(operation => new JobProfileCompetencyPatchOperation(
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

    public sealed class AddCompetencyRequest
    {
        public Guid? CatalogItemPublicId { get; set; }
        public string? Name { get; set; }
        public string? ExpectedLevel { get; set; }
        public string? Notes { get; set; }
        public int SortOrder { get; set; }
    }

    public sealed class UpdateCompetencyRequest
    {
        public Guid? CatalogItemPublicId { get; set; }
        public string? Name { get; set; }
        public string? ExpectedLevel { get; set; }
        public string? Notes { get; set; }
        public int SortOrder { get; set; }
        public Guid ConcurrencyToken { get; set; }
    }
}

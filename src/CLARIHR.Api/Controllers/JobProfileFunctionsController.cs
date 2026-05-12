using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Domain.JobProfiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize(Policy = JobProfilePolicies.Manage)]
[Route("api/v1/job-profiles/{publicId:guid}/functions")]
[Consumes("application/json")]
[Produces("application/json")]
[ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
public sealed class JobProfileFunctionsController(
    ICommandDispatcher commandDispatcher) : ControllerBase
{
    private const string ParentConcurrencyTokenHeaderName = "Parent-Concurrency-Token";

    [HttpPost]
    [ProducesResponseType<JobProfileSubResourceResult<JobProfileFunctionResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<JobProfileSubResourceResult<JobProfileFunctionResponse>>> Add(
        Guid publicId,
        [FromBody] AddFunctionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddJobProfileFunctionCommand(
                publicId,
                request.FunctionType,
                request.FrequencyCatalogItemId,
                request.Description,
                request.SortOrder,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("{functionPublicId:guid}")]
    [ProducesResponseType<JobProfileSubResourceResult<JobProfileFunctionResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<JobProfileSubResourceResult<JobProfileFunctionResponse>>> Update(
        Guid publicId,
        Guid functionPublicId,
        [FromBody] UpdateFunctionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateJobProfileFunctionCommand(
                publicId,
                functionPublicId,
                request.FunctionType,
                request.FrequencyCatalogItemId,
                request.Description,
                request.SortOrder,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpDelete("{functionPublicId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Remove(
        Guid publicId,
        Guid functionPublicId,
        [FromHeader(Name = IfMatchHeader.HeaderName)] string? ifMatch,
        CancellationToken cancellationToken = default)
    {
        if (!IfMatchHeader.TryParseConcurrencyToken(ifMatch, out var concurrencyToken))
        {
            return BadRequest(ProblemDetailsFactory.CreateProblemDetails(HttpContext, statusCode: StatusCodes.Status400BadRequest, detail: IfMatchHeader.MissingDetail));
        }

        var result = await commandDispatcher.SendAsync(
            new RemoveJobProfileFunctionCommand(publicId, functionPublicId, concurrencyToken),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(result).Result!;
        }

        Response.Headers[ParentConcurrencyTokenHeaderName] = result.Value.ParentConcurrencyToken.ToString();
        return NoContent();
    }

    public sealed class AddFunctionRequest
    {
        public JobFunctionType FunctionType { get; init; }
        public Guid? FrequencyCatalogItemId { get; init; }
        public string Description { get; init; } = string.Empty;
        public int SortOrder { get; init; }
        public Guid ConcurrencyToken { get; init; }
    }

    public sealed class UpdateFunctionRequest
    {
        public JobFunctionType FunctionType { get; init; }
        public Guid? FrequencyCatalogItemId { get; init; }
        public string Description { get; init; } = string.Empty;
        public int SortOrder { get; init; }
        public Guid ConcurrencyToken { get; init; }
    }

}

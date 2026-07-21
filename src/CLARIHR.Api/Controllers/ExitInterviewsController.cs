using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

/// <summary>
/// Exit-interview submissions (D-04/D-06/D-14). The employee fills their OWN interview (self-service) and
/// RRHH can capture or read; reading others' submissions is RRHH-only. Runs under authn-only supersets
/// (<see cref="PersonnelFilePolicies.ViewExitInterviews"/> / <see cref="PersonnelFilePolicies.ManageExitInterviews"/>);
/// the precise self-service / RRHH gate lives in the handlers (a declarative policy cannot express it).
/// </summary>
[ApiController]
[Authorize]
[Tags("Exit Interviews")]
[AuthorizationPolicySet(PersonnelFilePolicies.ViewExitInterviews, PersonnelFilePolicies.ManageExitInterviews)]
public sealed class ExitInterviewsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/personnel-files/{publicId:guid}/exit-interview")]
    [Produces("application/json")]
    [ProducesResponseType<ExitInterviewForFileResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Open the employee's exit interview",
        Description = "Returns the exit-interview form applicable to the employee's retirement reason (or `hasForm = false`) plus the employee's current submission, if any. The employee may open their own (self-service); RRHH may open any.")]
    public async Task<ActionResult<ExitInterviewForFileResponse>> GetForFile(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetExitInterviewForFileQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/exit-interview/submission")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<ExitInterviewSubmissionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Save or submit the employee's exit interview",
        Description = "Upserts the employee's exit-interview submission. With `submit = false` it is saved as a draft (resumable, non-anonymous forms only); with `submit = true` required answers are validated and the weighted 0–100 score is computed. The employee may fill their own (self-service); RRHH may capture for any. Because this is an upsert (not a plain update), concurrency is protected via a body field, not the `If-Match` header: `concurrencyToken` is omitted on the first save (nothing to version yet) and required on every subsequent save against the same submission (missing → `400`, stale → `409`).")]
    public async Task<ActionResult<ExitInterviewSubmissionResponse>> SaveSubmission(
        Guid publicId,
        [FromBody] SaveExitInterviewSubmissionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new SaveExitInterviewSubmissionCommand(publicId, request.Answers ?? [], request.Submit, request.ConcurrencyToken),
            cancellationToken);
        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpGet("api/v1/exit-interviews")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<ExitInterviewSubmissionListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List exit-interview submissions (RRHH)",
        Description = "Lists submitted/draft exit interviews for the tenant, optionally filtered by retirement reason or period (YYYY-MM). RRHH-only (D-14); anonymous submissions are listed without any link to the employee.")]
    public async Task<ActionResult<IReadOnlyCollection<ExitInterviewSubmissionListItemResponse>>> ListSubmissions(
        [FromQuery] string? reasonCode,
        [FromQuery] string? period,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new ListExitInterviewSubmissionsQuery(reasonCode, period), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/exit-interviews/{submissionId:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<ExitInterviewSubmissionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get an exit-interview submission by id (RRHH)",
        Description = "Returns a single exit-interview submission with its answers. RRHH-only (D-14); an anonymous submission carries no link to the employee.")]
    public async Task<ActionResult<ExitInterviewSubmissionResponse>> GetSubmissionById(
        Guid submissionId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetExitInterviewSubmissionByIdQuery(submissionId), cancellationToken);
        return this.ToActionResult(result);
    }
}

using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

/// <summary>
/// Exit-interview form builder (D-01, exclusive module). HR designs forms here: header (name, description,
/// anonymous flag), then the full nested definition (groups + fields + options) is saved in one call while
/// the form is in draft. Runs under its own HR-only policy set
/// (<see cref="PersonnelFilePolicies.ManageExitInterviewForms"/> for both read and write — building forms is
/// design-time, never self-service).
/// </summary>
[ApiController]
[Authorize]
[Tags("Exit Interviews")]
[AuthorizationPolicySet(PersonnelFilePolicies.ManageExitInterviewForms, PersonnelFilePolicies.ManageExitInterviewForms)]
public sealed class ExitInterviewFormsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/exit-interview-forms")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<ExitInterviewFormListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List exit-interview forms",
        Description = "Lists the tenant's exit-interview forms, optionally filtered by status, associated retirement reason, or name.")]
    public async Task<ActionResult<IReadOnlyCollection<ExitInterviewFormListItemResponse>>> ListForms(
        [FromQuery] ExitInterviewFormStatus? status,
        [FromQuery] string? reasonCode,
        [FromQuery] string? search,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new ListExitInterviewFormsQuery(status, reasonCode, search), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/exit-interview-forms/{formId:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<ExitInterviewFormResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get an exit-interview form (full definition)",
        Description = "Returns the form header plus its groups, fields and options. The `concurrencyToken` is required in the `If-Match` header of subsequent writes.")]
    public async Task<ActionResult<ExitInterviewFormResponse>> GetExitInterviewFormById(
        Guid formId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetExitInterviewFormByIdQuery(formId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/exit-interview-forms")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<ExitInterviewFormResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Create an exit-interview form (header)",
        Description = "Creates a draft form with its header (name, description, anonymous flag). The definition (groups/fields/options) is then saved via the `definition` endpoint.")]
    public async Task<ActionResult<ExitInterviewFormResponse>> CreateForm(
        [FromBody] CreateExitInterviewFormRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateExitInterviewFormCommand(request.Name, request.Description, request.IsAnonymous),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetExitInterviewFormById),
            value => new { formId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPut("api/v1/exit-interview-forms/{formId:guid}/definition")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<ExitInterviewFormResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Save an exit-interview form's full definition",
        Description = "Replaces the draft form's header and its whole definition (groups + fields + options) in one call. Requires the `If-Match` header with the current `concurrencyToken`; the new token is returned in the `ETag`.")]
    public async Task<ActionResult<ExitInterviewFormResponse>> SaveDefinition(
        Guid formId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] SaveExitInterviewFormDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new SaveExitInterviewFormDefinitionCommand(
                formId,
                concurrencyToken,
                request.Name,
                request.Description,
                request.IsAnonymous,
                request.Groups ?? [],
                request.Fields ?? []),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("api/v1/exit-interview-forms/{formId:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<ExitInterviewFormResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Deactivate (soft-delete) an exit-interview form",
        Description = "Deactivates the form and removes it as the active form for any reason. Requires the `If-Match` header with the current `concurrencyToken`.")]
    public async Task<ActionResult<ExitInterviewFormResponse>> DeleteForm(
        Guid formId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeleteExitInterviewFormCommand(formId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPost("api/v1/exit-interview-forms/{formId:guid}/publish")]
    [Produces("application/json")]
    [ProducesResponseType<ExitInterviewFormResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Publish an exit-interview form",
        Description = "Validates the draft definition (≥1 field, coherent options/range) and publishes it. Requires the `If-Match` header.")]
    public async Task<ActionResult<ExitInterviewFormResponse>> PublishForm(
        Guid formId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(new PublishExitInterviewFormCommand(formId, concurrencyToken), cancellationToken);
        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPost("api/v1/exit-interview-forms/{formId:guid}/reopen")]
    [Produces("application/json")]
    [ProducesResponseType<ExitInterviewFormResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Reopen a published exit-interview form for editing (new version)",
        Description = "Moves a published form back to draft and bumps its version so it can be edited; existing submissions keep their version snapshot. Requires the `If-Match` header.")]
    public async Task<ActionResult<ExitInterviewFormResponse>> ReopenForm(
        Guid formId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(new ReopenExitInterviewFormCommand(formId, concurrencyToken), cancellationToken);
        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPost("api/v1/exit-interview-forms/{formId:guid}/archive")]
    [Produces("application/json")]
    [ProducesResponseType<ExitInterviewFormResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Archive an exit-interview form",
        Description = "Archives the form (removes it from use and as the active form for any reason). Requires the `If-Match` header.")]
    public async Task<ActionResult<ExitInterviewFormResponse>> ArchiveForm(
        Guid formId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(new ArchiveExitInterviewFormCommand(formId, concurrencyToken), cancellationToken);
        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPut("api/v1/exit-interview-forms/{formId:guid}/reason")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<ExitInterviewFormResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Associate a published form to a single retirement reason",
        Description = "Associates the published form to one retirement reason and makes it the active form for it (single-active: any previously-active form for that reason is deactivated). Requires the `If-Match` header.")]
    public async Task<ActionResult<ExitInterviewFormResponse>> AssignReason(
        Guid formId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] AssignExitInterviewFormReasonRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AssignExitInterviewFormReasonCommand(formId, concurrencyToken, request.ReasonCode),
            cancellationToken);
        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpGet("api/v1/exit-interview-forms/applicable")]
    [Produces("application/json")]
    [ProducesResponseType<ExitInterviewApplicableFormResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Resolve the form applicable to a retirement reason",
        Description = "Returns the active published exit-interview form associated to the given retirement reason, or `hasForm = false` when none applies (the interview is optional).")]
    public async Task<ActionResult<ExitInterviewApplicableFormResponse>> ResolveApplicable(
        [FromQuery] string reasonCode,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new ResolveApplicableExitInterviewFormQuery(reasonCode), cancellationToken);
        return this.ToActionResult(result);
    }
}

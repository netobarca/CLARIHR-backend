using CLARIHR.Api.Common;
using CLARIHR.Api.Contracts.Files;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Files;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
public sealed class FilesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpPost("api/v1/files/upload-session")]
    [ProducesResponseType<CreateUploadSessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CreateUploadSessionResponse>> CreateUploadSession(
        [FromBody] CreateUploadSessionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateUploadSessionCommand(
                request.FileName,
                request.ContentType,
                request.SizeBytes,
                request.Purpose,
                request.EntityId),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/files/{filePublicId:guid}/complete")]
    [ProducesResponseType<CompleteFileUploadResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<CompleteFileUploadResponse>> CompleteUpload(
        Guid filePublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CompleteFileUploadCommand(filePublicId),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/files/{filePublicId:guid}/read-url")]
    [ProducesResponseType<GetFileReadUrlResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<GetFileReadUrlResponse>> GetReadUrl(
        Guid filePublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetFileReadUrlQuery(filePublicId),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpDelete("api/v1/files/{filePublicId:guid}")]
    [ProducesResponseType<DeleteFileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeleteFileResponse>> Delete(
        Guid filePublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeleteFileCommand(filePublicId),
            cancellationToken);

        return this.ToActionResult(result);
    }
}

using CLARIHR.Api.Common;
using CLARIHR.Api.Contracts.Files;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Files;
using CLARIHR.Application.Features.Files.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
[Tags("Files")]
public sealed class FilesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpPost("api/v1/files/upload-session")]
    [EnableRateLimiting(FileRateLimitPolicies.Upload)]
    [ProducesResponseType<CreateUploadSessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [SwaggerOperation(
        Summary = "Create a direct-upload session",
        Description = """
            Reserves a file record and returns a short-lived, write-only pre-signed `uploadUrl` (plus
            `requiredHeaders`) to upload the binary directly to storage, scoped to the chosen
            `purpose`. The declared `contentType`/extension/`sizeBytes` are validated against the
            purpose rules (unknown purpose → `400`, disallowed type/extension → `422`, oversize →
            `413`). After uploading, call `PATCH /api/v1/files/{filePublicId}/complete`.
            """)]
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

    [HttpPatch("api/v1/files/{filePublicId:guid}/complete")]
    [EnableRateLimiting(FileRateLimitPolicies.Lifecycle)]
    [ProducesResponseType<CompleteFileUploadResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [SwaggerOperation(
        Summary = "Complete a file upload",
        Description = """
            Finalizes a pending upload: verifies the binary exists in storage and adopts its
            server-side size/content-type, then marks the file `Active`. Only the file's uploader may
            complete it (`403`). Requires the current `concurrencyToken` in the body; a stale token
            yields `409 CONCURRENCY_CONFLICT`, and a file not pending / not found in storage yields
            `422`.
            """)]
    public async Task<ActionResult<CompleteFileUploadResponse>> CompleteUpload(
        Guid filePublicId,
        [FromBody] CompleteFileUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CompleteFileUploadCommand(filePublicId, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/files/{filePublicId:guid}/read-url")]
    [EnableRateLimiting(FileRateLimitPolicies.Read)]
    [ProducesResponseType<GetFileReadUrlResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [SwaggerOperation(
        Summary = "Get a download URL for an owned file",
        Description = """
            Returns a short-lived, read-only pre-signed URL to download the file. Restricted to the
            file's uploader (`403` otherwise) — domains that serve a file to other authorized users
            (e.g. personnel-file documents, profile photos) expose their own authorized download path.
            A file that is not `Active` yields `422`.
            """)]
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
    [EnableRateLimiting(FileRateLimitPolicies.Lifecycle)]
    [ProducesResponseType<DeleteFileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [SwaggerOperation(
        Summary = "Delete a file",
        Description = """
            Soft-deletes a file (marks it `Deleted`; the stored binary is reclaimed by the background
            cleanup). Restricted to the file's uploader (`403` otherwise).
            """)]
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

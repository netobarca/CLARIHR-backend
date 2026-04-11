using CLARIHR.Api.Common;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
public sealed class PersonnelFileDocumentsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/personnel-files/{id:guid}/documents")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>> GetDocuments(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileDocumentsQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{id:guid}/documents")]
    [ProducesResponseType<PersonnelFileDocumentMetadataResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileDocumentMetadataResponse>> UploadDocument(
        Guid id,
        [FromForm] UploadPersonnelFileDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.File is null || request.File.Length == 0)
        {
            return this.ToActionResult(Result<PersonnelFileDocumentMetadataResponse>.Failure(PersonnelFileErrors.DocumentFileRequired));
        }

        await using var stream = request.File.OpenReadStream();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);

        var result = await commandDispatcher.SendAsync(
            new UploadPersonnelFileDocumentCommand(
                id,
                request.DocumentType,
                request.Observations,
                request.DeliveryDate,
                request.LoanDate,
                request.ReturnDate,
                request.File.FileName,
                request.File.ContentType,
                memory.ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<PersonnelFileDocumentMetadataResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPatch("api/v1/personnel-file-documents/{documentId:guid}/inactivate")]
    [ProducesResponseType<PersonnelFileDocumentMetadataResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonnelFileDocumentMetadataResponse>> InactivateDocument(
        Guid documentId,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivatePersonnelFileDocumentCommand(documentId, request.ConcurrencyToken),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-file-documents/{documentId:guid}/download")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadDocument(Guid documentId, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileDocumentDownloadQuery(documentId), cancellationToken);
        if (result.IsFailure)
        {
            return this.ToActionResult(Result<PersonnelFileDocumentDownloadResponse>.Failure(result.Error)).Result!;
        }

        return File(result.Value.FileData, result.Value.ContentType, result.Value.FileName);
    }

    [HttpPost("api/v1/personnel-files/{id:guid}/observations")]
    [ProducesResponseType<PersonnelFileObservationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonnelFileObservationResponse>> AddObservation(
        Guid id,
        [FromBody] AddObservationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileObservationCommand(id, request.Note, request.ConcurrencyToken),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<PersonnelFileObservationResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }
}

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
    // ─── Documents ──────────────────────────────────────────────────────────────

    [HttpGet("api/v1/personnel-files/{publicId:guid}/documents")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>> GetDocuments(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileDocumentsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/documents/{documentPublicId:guid}")]
    [ProducesResponseType<PersonnelFileDocumentMetadataResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PersonnelFileDocumentMetadataResponse>> GetDocument(
        Guid publicId,
        Guid documentPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileDocumentByIdQuery(publicId, documentPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/documents")]
    [ProducesResponseType<PersonnelFileDocumentMetadataResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileDocumentMetadataResponse>> AddDocument(
        Guid publicId,
        [FromBody] AddPersonnelFileDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileDocumentCommand(
                publicId,
                request.FilePublicId,
                request.DocumentTypeCatalogItemPublicId,
                request.Observations,
                request.DeliveryDate,
                request.LoanDate,
                request.ReturnDate,
                request.ConcurrencyToken),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<PersonnelFileDocumentMetadataResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/documents/{documentPublicId:guid}")]
    [ProducesResponseType<PersonnelFileDocumentMetadataResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileDocumentMetadataResponse>> UpdateDocument(
        Guid publicId,
        Guid documentPublicId,
        [FromBody] UpdatePersonnelFileDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileDocumentCommand(
                publicId,
                documentPublicId,
                request.DocumentTypeCatalogItemPublicId,
                request.Observations,
                request.DeliveryDate,
                request.LoanDate,
                request.ReturnDate,
                request.FilePublicId,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/documents/{documentPublicId:guid}/inactivate")]
    [ProducesResponseType<PersonnelFileDocumentMetadataResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonnelFileDocumentMetadataResponse>> InactivateDocument(
        Guid publicId,
        Guid documentPublicId,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivatePersonnelFileDocumentCommand(publicId, documentPublicId, request.ConcurrencyToken),
            cancellationToken);
        return this.ToActionResult(result);
    }

    // ─── Observations ────────────────────────────────────────────────────────────

    [HttpGet("api/v1/personnel-files/{publicId:guid}/observations")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileObservationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileObservationResponse>>> GetObservations(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileObservationsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/observations")]
    [ProducesResponseType<PersonnelFileObservationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonnelFileObservationResponse>> AddObservation(
        Guid publicId,
        [FromBody] AddObservationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileObservationCommand(publicId, request.Note, request.ConcurrencyToken),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<PersonnelFileObservationResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }
}

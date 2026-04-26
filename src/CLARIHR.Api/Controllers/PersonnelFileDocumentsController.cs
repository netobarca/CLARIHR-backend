using CLARIHR.Api.Common;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
public sealed class PersonnelFileDocumentsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    private static readonly JsonSerializerOptions ManifestJsonOptions = new(JsonSerializerDefaults.Web);

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

    [HttpPut("api/v1/personnel-files/{id:guid}/documents")]
    [ProducesResponseType<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>>> ReplaceDocuments(
        Guid id,
        [FromForm] ReplacePersonnelFileDocumentsRequest request,
        CancellationToken cancellationToken = default)
    {
        ReplacePersonnelFileDocumentsManifestRequest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<ReplacePersonnelFileDocumentsManifestRequest>(request.ManifestJson, ManifestJsonOptions);
        }
        catch (JsonException)
        {
            return this.ToActionResult(Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>>.Failure(
                ErrorCatalog.Validation(new Dictionary<string, string[]>
                {
                    ["manifestJson"] = ["ManifestJson must contain a valid JSON payload."]
                })));
        }

        if (manifest?.Items is null)
        {
            return this.ToActionResult(Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>>.Failure(
                ErrorCatalog.Validation(new Dictionary<string, string[]>
                {
                    ["manifestJson"] = ["ManifestJson must include an 'items' array."]
                })));
        }

        var filesByKey = new Dictionary<string, IFormFile>(StringComparer.Ordinal);
        foreach (var formFile in Request.Form.Files)
        {
            if (!filesByKey.TryAdd(formFile.Name, formFile))
            {
                return this.ToActionResult(Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>>.Failure(
                    ErrorCatalog.Validation(new Dictionary<string, string[]>
                    {
                        ["files"] = [$"Duplicate uploaded file key '{formFile.Name}' is not allowed."]
                    })));
            }
        }

        var usedFileKeys = new HashSet<string>(StringComparer.Ordinal);
        var items = new List<PersonnelFileDocumentInput>(manifest.Items.Count);
        for (var index = 0; index < manifest.Items.Count; index++)
        {
            var item = manifest.Items.ElementAt(index);
            string? fileName = null;
            string? contentType = null;
            byte[]? fileData = null;

            if (!string.IsNullOrWhiteSpace(item.FileKey))
            {
                if (!filesByKey.TryGetValue(item.FileKey, out var file))
                {
                    return this.ToActionResult(Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>>.Failure(
                        ErrorCatalog.Validation(new Dictionary<string, string[]>
                        {
                            [$"items[{index}].fileKey"] = [$"FileKey '{item.FileKey}' does not match any uploaded file part."]
                        })));
                }

                if (!usedFileKeys.Add(item.FileKey))
                {
                    return this.ToActionResult(Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>>.Failure(
                        ErrorCatalog.Validation(new Dictionary<string, string[]>
                        {
                            [$"items[{index}].fileKey"] = [$"FileKey '{item.FileKey}' is duplicated in the manifest."]
                        })));
                }

                var fileValidation = await PersonnelFileDocumentUploadGuard.ValidateAsync(file, cancellationToken);
                if (fileValidation != Error.None)
                {
                    return this.ToActionResult(Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>>.Failure(fileValidation));
                }

                fileName = PersonnelFileDocumentUploadGuard.GetSafeFileName(file);
                contentType = file.ContentType.Trim();

                await using var stream = file.OpenReadStream();
                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory, cancellationToken);
                fileData = memory.ToArray();
            }

            items.Add(new PersonnelFileDocumentInput(
                item.DocumentPublicId,
                item.DocumentType,
                item.Observations,
                item.DeliveryDate,
                item.LoanDate,
                item.ReturnDate,
                item.FileKey,
                fileName,
                contentType,
                fileData));
        }

        var unusedFileKeys = filesByKey.Keys
            .Where(fileKey => !usedFileKeys.Contains(fileKey))
            .ToArray();
        if (unusedFileKeys.Length > 0)
        {
            return this.ToActionResult(Result<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>>.Failure(
                ErrorCatalog.Validation(new Dictionary<string, string[]>
                {
                    ["files"] = [$"Uploaded files are not referenced by the manifest: {string.Join(", ", unusedFileKeys)}"]
                })));
        }

        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFileDocumentsCommand(id, items, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{id:guid}/observations")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileObservationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileObservationResponse>>> GetObservations(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileObservationsQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{id:guid}/documents")]
    [ProducesResponseType<PersonnelFileDocumentMetadataResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileDocumentMetadataResponse>> UploadDocument(
        Guid id,
        [FromForm] UploadPersonnelFileDocumentRequest request,
        CancellationToken cancellationToken = default)
    {
        var fileValidation = await PersonnelFileDocumentUploadGuard.ValidateAsync(request.File, cancellationToken);
        if (fileValidation != Error.None)
        {
            return this.ToActionResult(Result<PersonnelFileDocumentMetadataResponse>.Failure(fileValidation));
        }

        var safeFileName = PersonnelFileDocumentUploadGuard.GetSafeFileName(request.File);
        var normalizedContentType = request.File.ContentType.Trim();

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
                safeFileName,
                normalizedContentType,
                memory.ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<PersonnelFileDocumentMetadataResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
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

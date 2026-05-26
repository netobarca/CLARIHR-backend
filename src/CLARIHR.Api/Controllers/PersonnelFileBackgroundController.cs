using Asp.Versioning;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}")]
[Consumes("application/json")]
[Produces("application/json")]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.Read, PersonnelFilePolicies.Manage)]
public sealed class PersonnelFileBackgroundController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    // ─── Educations ───────────────────────────────────────────────────────────

    [HttpGet("personnel-files/{publicId:guid}/educations")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileEducationResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's educations",
        Description = """
            Returns every education entry recorded for the specified personnel file.

            Each item carries its own `concurrencyToken`, required in the `If-Match`
            header of subsequent `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileEducationResponse>>> GetEducations(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileEducationsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("personnel-files/{publicId:guid}/educations/{educationPublicId:guid}")]
    [ProducesResponseType<PersonnelFileEducationResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file education by id",
        Description = """
            Returns a single education entry of the specified personnel file. The
            `concurrencyToken` in the response is required in the `If-Match` header of
            subsequent `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<PersonnelFileEducationResponse>> GetEducationById(
        Guid publicId,
        Guid educationPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileEducationByIdQuery(publicId, educationPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("personnel-files/{publicId:guid}/educations")]
    [ProducesResponseType<PersonnelFileEducationResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Add an education to a personnel file",
        Description = """
            Creates a new education entry under the specified personnel file and returns
            it with a `201 Created` response. The `Location` header points to the created
            resource and the `ETag` header carries its initial `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFileEducationResponse>> AddEducation(
        Guid publicId,
        [FromBody] AddEducationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileEducationCommand(
                publicId,
                new EducationInput(
                    request.StatusPublicId,
                    request.DegreeTitle,
                    request.StudyTypePublicId,
                    request.CareerPublicId,
                    request.Institution,
                    request.CountryCode,
                    request.Specialty,
                    request.IsCurrentlyStudying,
                    request.StartDate,
                    request.EndDate,
                    request.ShiftPublicId,
                    request.ModalityPublicId,
                    request.TotalSubjects,
                    request.ApprovedSubjects)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetEducationById),
            value => new { publicId, educationPublicId = value.EducationPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("personnel-files/{publicId:guid}/educations/{educationPublicId:guid}")]
    [ProducesResponseType<PersonnelFileEducationResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace a personnel file education",
        Description = """
            Replaces all fields of an existing education entry. Requires the `If-Match`
            header with the current `concurrencyToken` to prevent lost updates; the new
            token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileEducationResponse>> UpdateEducation(
        Guid publicId,
        Guid educationPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateEducationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileEducationCommand(
                publicId,
                educationPublicId,
                new EducationInput(
                    request.StatusPublicId,
                    request.DegreeTitle,
                    request.StudyTypePublicId,
                    request.CareerPublicId,
                    request.Institution,
                    request.CountryCode,
                    request.Specialty,
                    request.IsCurrentlyStudying,
                    request.StartDate,
                    request.EndDate,
                    request.ShiftPublicId,
                    request.ModalityPublicId,
                    request.TotalSubjects,
                    request.ApprovedSubjects),
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("personnel-files/{publicId:guid}/educations/{educationPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<PersonnelFileEducationResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a personnel file education",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type
            `application/json-patch+json`) to an existing education entry. Requires the
            `If-Match` header with the current `concurrencyToken`; the new token is
            returned in the `ETag` header. Mutable members are the education input fields.
            """)]
    public async Task<ActionResult<PersonnelFileEducationResponse>> PatchEducation(
        Guid publicId,
        Guid educationPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchEducationRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchPersonnelFileEducationCommand(
                publicId,
                educationPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new PersonnelFileEducationPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("personnel-files/{publicId:guid}/educations/{educationPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove an education from a personnel file",
        Description = """
            Deletes the specified education entry. Requires the `If-Match` header with the
            current `concurrencyToken`. Returns the parent personnel file's refreshed
            concurrency token so the caller can keep mutating without an extra round-trip.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteEducation(
        Guid publicId,
        Guid educationPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileEducationCommand(publicId, educationPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    // ─── Languages ────────────────────────────────────────────────────────────

    [HttpGet("personnel-files/{publicId:guid}/languages")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileLanguageResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's languages",
        Description = """
            Returns every language entry recorded for the specified personnel file. Each
            item carries its own `concurrencyToken`, required in the `If-Match` header of
            subsequent `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileLanguageResponse>>> GetLanguages(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileLanguagesQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("personnel-files/{publicId:guid}/languages/{languagePublicId:guid}")]
    [ProducesResponseType<PersonnelFileLanguageResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file language by id",
        Description = """
            Returns a single language entry of the specified personnel file. The
            `concurrencyToken` in the response is required in the `If-Match` header of
            subsequent `PUT`/`PATCH`/`DELETE` requests.
            """)]
    public async Task<ActionResult<PersonnelFileLanguageResponse>> GetLanguageById(
        Guid publicId,
        Guid languagePublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileLanguageByIdQuery(publicId, languagePublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("personnel-files/{publicId:guid}/languages")]
    [ProducesResponseType<PersonnelFileLanguageResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Add a language to a personnel file",
        Description = """
            Creates a new language entry under the specified personnel file and returns it
            with a `201 Created` response. The `Location` header points to the created
            resource and the `ETag` header carries its initial `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFileLanguageResponse>> AddLanguage(
        Guid publicId,
        [FromBody] AddLanguageRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileLanguageCommand(
                publicId,
                new LanguageInput(
                    request.LanguageCode,
                    request.LevelCode,
                    request.Speaks,
                    request.Writes,
                    request.Reads)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetLanguageById),
            value => new { publicId, languagePublicId = value.LanguagePublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("personnel-files/{publicId:guid}/languages/{languagePublicId:guid}")]
    [ProducesResponseType<PersonnelFileLanguageResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace a personnel file language",
        Description = """
            Replaces all fields of an existing language entry. Requires the `If-Match`
            header with the current `concurrencyToken`; the new token is returned in the
            `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileLanguageResponse>> UpdateLanguage(
        Guid publicId,
        Guid languagePublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateLanguageRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileLanguageCommand(
                publicId,
                languagePublicId,
                new LanguageInput(
                    request.LanguageCode,
                    request.LevelCode,
                    request.Speaks,
                    request.Writes,
                    request.Reads),
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("personnel-files/{publicId:guid}/languages/{languagePublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<PersonnelFileLanguageResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a personnel file language",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type
            `application/json-patch+json`) to an existing language entry. Requires the
            `If-Match` header with the current `concurrencyToken`; the new token is
            returned in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileLanguageResponse>> PatchLanguage(
        Guid publicId,
        Guid languagePublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchLanguageRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchPersonnelFileLanguageCommand(
                publicId,
                languagePublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new PersonnelFileLanguagePatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("personnel-files/{publicId:guid}/languages/{languagePublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove a language from a personnel file",
        Description = """
            Deletes the specified language entry. Requires the `If-Match` header with the
            current `concurrencyToken`. Returns the parent personnel file's refreshed
            concurrency token so the caller can keep mutating without an extra round-trip.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteLanguage(
        Guid publicId,
        Guid languagePublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileLanguageCommand(publicId, languagePublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    // ─── Trainings ────────────────────────────────────────────────────────────

    [HttpGet("personnel-files/{publicId:guid}/trainings")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileTrainingResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's trainings",
        Description = """
            Returns every training entry recorded for the specified personnel file. Each
            item carries its own `concurrencyToken`, required in the `If-Match` header of
            subsequent `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileTrainingResponse>>> GetTrainings(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileTrainingsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("personnel-files/{publicId:guid}/trainings/{trainingPublicId:guid}")]
    [ProducesResponseType<PersonnelFileTrainingResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file training by id",
        Description = """
            Returns a single training entry of the specified personnel file. The
            `concurrencyToken` in the response is required in the `If-Match` header of
            subsequent `PUT`/`PATCH`/`DELETE` requests.
            """)]
    public async Task<ActionResult<PersonnelFileTrainingResponse>> GetTrainingById(
        Guid publicId,
        Guid trainingPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileTrainingByIdQuery(publicId, trainingPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("personnel-files/{publicId:guid}/trainings")]
    [ProducesResponseType<PersonnelFileTrainingResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Add a training to a personnel file",
        Description = """
            Creates a new training entry under the specified personnel file and returns it
            with a `201 Created` response. The `Location` header points to the created
            resource and the `ETag` header carries its initial `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFileTrainingResponse>> AddTraining(
        Guid publicId,
        [FromBody] AddTrainingRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileTrainingCommand(
                publicId,
                new TrainingInput(
                    request.TrainingName,
                    request.TrainingTypeCode,
                    request.Description,
                    request.Topic,
                    request.Institution,
                    request.Instructors,
                    request.Score,
                    request.StartDate,
                    request.EndDate,
                    request.IsInternal,
                    request.IsLocal,
                    request.CountryCode,
                    request.DurationValue,
                    request.DurationUnitCode,
                    request.CostAmount,
                    request.CostCurrencyCode)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetTrainingById),
            value => new { publicId, trainingPublicId = value.TrainingPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("personnel-files/{publicId:guid}/trainings/{trainingPublicId:guid}")]
    [ProducesResponseType<PersonnelFileTrainingResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace a personnel file training",
        Description = """
            Replaces all fields of an existing training entry. Requires the `If-Match`
            header with the current `concurrencyToken`; the new token is returned in the
            `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileTrainingResponse>> UpdateTraining(
        Guid publicId,
        Guid trainingPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateTrainingRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileTrainingCommand(
                publicId,
                trainingPublicId,
                new TrainingInput(
                    request.TrainingName,
                    request.TrainingTypeCode,
                    request.Description,
                    request.Topic,
                    request.Institution,
                    request.Instructors,
                    request.Score,
                    request.StartDate,
                    request.EndDate,
                    request.IsInternal,
                    request.IsLocal,
                    request.CountryCode,
                    request.DurationValue,
                    request.DurationUnitCode,
                    request.CostAmount,
                    request.CostCurrencyCode),
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("personnel-files/{publicId:guid}/trainings/{trainingPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<PersonnelFileTrainingResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a personnel file training",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type
            `application/json-patch+json`) to an existing training entry. Requires the
            `If-Match` header with the current `concurrencyToken`; the new token is
            returned in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileTrainingResponse>> PatchTraining(
        Guid publicId,
        Guid trainingPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchTrainingRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchPersonnelFileTrainingCommand(
                publicId,
                trainingPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new PersonnelFileTrainingPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("personnel-files/{publicId:guid}/trainings/{trainingPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove a training from a personnel file",
        Description = """
            Deletes the specified training entry. Requires the `If-Match` header with the
            current `concurrencyToken`. Returns the parent personnel file's refreshed
            concurrency token so the caller can keep mutating without an extra round-trip.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteTraining(
        Guid publicId,
        Guid trainingPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileTrainingCommand(publicId, trainingPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    // ─── Previous Employments ─────────────────────────────────────────────────

    [HttpGet("personnel-files/{publicId:guid}/previous-employments")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFilePreviousEmploymentResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's previous employments",
        Description = """
            Returns every previous employment entry recorded for the specified personnel
            file. Each item carries its own `concurrencyToken`, required in the `If-Match`
            header of subsequent `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFilePreviousEmploymentResponse>>> GetPreviousEmployments(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFilePreviousEmploymentsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("personnel-files/{publicId:guid}/previous-employments/{previousEmploymentPublicId:guid}")]
    [ProducesResponseType<PersonnelFilePreviousEmploymentResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file previous employment by id",
        Description = """
            Returns a single previous employment entry of the specified personnel file. The
            `concurrencyToken` in the response is required in the `If-Match` header of
            subsequent `PUT`/`PATCH`/`DELETE` requests.
            """)]
    public async Task<ActionResult<PersonnelFilePreviousEmploymentResponse>> GetPreviousEmploymentById(
        Guid publicId,
        Guid previousEmploymentPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFilePreviousEmploymentByIdQuery(publicId, previousEmploymentPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("personnel-files/{publicId:guid}/previous-employments")]
    [ProducesResponseType<PersonnelFilePreviousEmploymentResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Add a previous employment to a personnel file",
        Description = """
            Creates a new previous employment entry under the specified personnel file and
            returns it with a `201 Created` response. The `Location` header points to the
            created resource and the `ETag` header carries its initial `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFilePreviousEmploymentResponse>> AddPreviousEmployment(
        Guid publicId,
        [FromBody] AddPreviousEmploymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFilePreviousEmploymentCommand(
                publicId,
                new PreviousEmploymentInput(
                    request.Institution,
                    request.Place,
                    request.LastPosition,
                    request.ManagerName,
                    request.EntryDate,
                    request.RetirementDate,
                    request.CompanyPhone,
                    request.ExitReason,
                    request.FirstSalaryAmount,
                    request.LastSalaryAmount,
                    request.AverageCommissionAmount,
                    request.CurrencyCode)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetPreviousEmploymentById),
            value => new { publicId, previousEmploymentPublicId = value.PreviousEmploymentPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("personnel-files/{publicId:guid}/previous-employments/{previousEmploymentPublicId:guid}")]
    [ProducesResponseType<PersonnelFilePreviousEmploymentResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace a personnel file previous employment",
        Description = """
            Replaces all fields of an existing previous employment entry. Requires the
            `If-Match` header with the current `concurrencyToken`; the new token is returned
            in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFilePreviousEmploymentResponse>> UpdatePreviousEmployment(
        Guid publicId,
        Guid previousEmploymentPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdatePreviousEmploymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFilePreviousEmploymentCommand(
                publicId,
                previousEmploymentPublicId,
                new PreviousEmploymentInput(
                    request.Institution,
                    request.Place,
                    request.LastPosition,
                    request.ManagerName,
                    request.EntryDate,
                    request.RetirementDate,
                    request.CompanyPhone,
                    request.ExitReason,
                    request.FirstSalaryAmount,
                    request.LastSalaryAmount,
                    request.AverageCommissionAmount,
                    request.CurrencyCode),
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("personnel-files/{publicId:guid}/previous-employments/{previousEmploymentPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<PersonnelFilePreviousEmploymentResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a personnel file previous employment",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type
            `application/json-patch+json`) to an existing previous employment entry. Requires
            the `If-Match` header with the current `concurrencyToken`; the new token is
            returned in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFilePreviousEmploymentResponse>> PatchPreviousEmployment(
        Guid publicId,
        Guid previousEmploymentPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchPreviousEmploymentRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchPersonnelFilePreviousEmploymentCommand(
                publicId,
                previousEmploymentPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new PersonnelFilePreviousEmploymentPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("personnel-files/{publicId:guid}/previous-employments/{previousEmploymentPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove a previous employment from a personnel file",
        Description = """
            Deletes the specified previous employment entry. Requires the `If-Match` header
            with the current `concurrencyToken`. Returns the parent personnel file's
            refreshed concurrency token so the caller can keep mutating without an extra
            round-trip.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeletePreviousEmployment(
        Guid publicId,
        Guid previousEmploymentPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFilePreviousEmploymentCommand(publicId, previousEmploymentPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    // ─── References ───────────────────────────────────────────────────────────

    [HttpGet("personnel-files/{publicId:guid}/references")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileReferenceResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's references",
        Description = """
            Returns every reference entry recorded for the specified personnel file. Each
            item carries its own `concurrencyToken`, required in the `If-Match` header of
            subsequent `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileReferenceResponse>>> GetReferences(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileReferencesQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("personnel-files/{publicId:guid}/references/{referencePublicId:guid}")]
    [ProducesResponseType<PersonnelFileReferenceResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file reference by id",
        Description = """
            Returns a single reference entry of the specified personnel file. The
            `concurrencyToken` in the response is required in the `If-Match` header of
            subsequent `PUT`/`PATCH`/`DELETE` requests.
            """)]
    public async Task<ActionResult<PersonnelFileReferenceResponse>> GetReferenceById(
        Guid publicId,
        Guid referencePublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileReferenceByIdQuery(publicId, referencePublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("personnel-files/{publicId:guid}/references")]
    [ProducesResponseType<PersonnelFileReferenceResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Add a reference to a personnel file",
        Description = """
            Creates a new reference entry under the specified personnel file and returns it
            with a `201 Created` response. The `Location` header points to the created
            resource and the `ETag` header carries its initial `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFileReferenceResponse>> AddReference(
        Guid publicId,
        [FromBody] AddReferenceRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileReferenceCommand(
                publicId,
                new ReferenceInput(
                    request.PersonName,
                    request.Address,
                    request.Phone,
                    request.ReferenceTypeCode,
                    request.Occupation,
                    request.Workplace,
                    request.WorkPhone,
                    request.KnownTimeYears)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetReferenceById),
            value => new { publicId, referencePublicId = value.ReferencePublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("personnel-files/{publicId:guid}/references/{referencePublicId:guid}")]
    [ProducesResponseType<PersonnelFileReferenceResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace a personnel file reference",
        Description = """
            Replaces all fields of an existing reference entry. Requires the `If-Match`
            header with the current `concurrencyToken`; the new token is returned in the
            `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileReferenceResponse>> UpdateReference(
        Guid publicId,
        Guid referencePublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateReferenceRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileReferenceCommand(
                publicId,
                referencePublicId,
                new ReferenceInput(
                    request.PersonName,
                    request.Address,
                    request.Phone,
                    request.ReferenceTypeCode,
                    request.Occupation,
                    request.Workplace,
                    request.WorkPhone,
                    request.KnownTimeYears),
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("personnel-files/{publicId:guid}/references/{referencePublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<PersonnelFileReferenceResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a personnel file reference",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type
            `application/json-patch+json`) to an existing reference entry. Requires the
            `If-Match` header with the current `concurrencyToken`; the new token is returned
            in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileReferenceResponse>> PatchReference(
        Guid publicId,
        Guid referencePublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchReferenceRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchPersonnelFileReferenceCommand(
                publicId,
                referencePublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new PersonnelFileReferencePatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("personnel-files/{publicId:guid}/references/{referencePublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove a reference from a personnel file",
        Description = """
            Deletes the specified reference entry. Requires the `If-Match` header with the
            current `concurrencyToken`. Returns the parent personnel file's refreshed
            concurrency token so the caller can keep mutating without an extra round-trip.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteReference(
        Guid publicId,
        Guid referencePublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileReferenceCommand(publicId, referencePublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }
}

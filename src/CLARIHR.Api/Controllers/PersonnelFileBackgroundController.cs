using CLARIHR.Api.Common;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
public sealed class PersonnelFileBackgroundController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    // ─── Educations ───────────────────────────────────────────────────────────

    [HttpGet("api/v1/personnel-files/{publicId:guid}/educations")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileEducationResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileEducationResponse>>> GetEducations(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileEducationsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/educations")]
    [ProducesResponseType<PersonnelFileEducationResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
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

        return result.IsFailure
            ? this.ToActionResult(Result<PersonnelFileEducationResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/educations/{itemPublicId:guid}")]
    [ProducesResponseType<PersonnelFileEducationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileEducationResponse>> UpdateEducation(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] UpdateEducationRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileEducationCommand(
                publicId,
                itemPublicId,
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
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/educations/{itemPublicId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteEducation(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileEducationCommand(publicId, itemPublicId, request.ConcurrencyToken),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(result).Result!
            : NoContent();
    }

    // ─── Languages ────────────────────────────────────────────────────────────

    [HttpGet("api/v1/personnel-files/{publicId:guid}/languages")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileLanguageResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileLanguageResponse>>> GetLanguages(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileLanguagesQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/languages")]
    [ProducesResponseType<PersonnelFileLanguageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
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

        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/languages/{itemPublicId:guid}")]
    [ProducesResponseType<PersonnelFileLanguageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileLanguageResponse>> UpdateLanguage(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] UpdateLanguageRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileLanguageCommand(
                publicId,
                itemPublicId,
                new LanguageInput(
                    request.LanguageCode,
                    request.LevelCode,
                    request.Speaks,
                    request.Writes,
                    request.Reads),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/languages/{itemPublicId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> DeleteLanguage(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileLanguageCommand(
                publicId,
                itemPublicId,
                request.ConcurrencyToken),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(result).Result!
            : NoContent();
    }

    // ─── Trainings ────────────────────────────────────────────────────────────

    [HttpGet("api/v1/personnel-files/{publicId:guid}/trainings")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileTrainingResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileTrainingResponse>>> GetTrainings(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileTrainingsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/trainings")]
    [ProducesResponseType<PersonnelFileTrainingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
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

        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/trainings/{itemPublicId:guid}")]
    [ProducesResponseType<PersonnelFileTrainingResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileTrainingResponse>> UpdateTraining(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] UpdateTrainingRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileTrainingCommand(
                publicId,
                itemPublicId,
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
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/trainings/{itemPublicId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> DeleteTraining(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileTrainingCommand(
                publicId,
                itemPublicId,
                request.ConcurrencyToken),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(result).Result!
            : NoContent();
    }

    // ─── Previous Employments ─────────────────────────────────────────────────

    [HttpGet("api/v1/personnel-files/{publicId:guid}/previous-employments")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFilePreviousEmploymentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFilePreviousEmploymentResponse>>> GetPreviousEmployments(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFilePreviousEmploymentsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/previous-employments")]
    [ProducesResponseType<PersonnelFilePreviousEmploymentResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
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

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetPreviousEmployments), new { publicId }, result.Value)
            : this.ToActionResult(result).Result!;
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/previous-employments/{itemPublicId:guid}")]
    [ProducesResponseType<PersonnelFilePreviousEmploymentResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFilePreviousEmploymentResponse>> UpdatePreviousEmployment(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] UpdatePreviousEmploymentRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFilePreviousEmploymentCommand(
                publicId,
                itemPublicId,
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
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/previous-employments/{itemPublicId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeletePreviousEmployment(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFilePreviousEmploymentCommand(
                publicId,
                itemPublicId,
                request.ConcurrencyToken),
            cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : this.ToActionResult(result).Result!;
    }

    // ─── References ───────────────────────────────────────────────────────────

    [HttpGet("api/v1/personnel-files/{publicId:guid}/references")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileReferenceResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileReferenceResponse>>> GetReferences(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileReferencesQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/references")]
    [ProducesResponseType<PersonnelFileReferenceResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
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

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetReferences), new { publicId }, result.Value)
            : this.ToActionResult(result).Result!;
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/references/{itemPublicId:guid}")]
    [ProducesResponseType<PersonnelFileReferenceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileReferenceResponse>> UpdateReference(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] UpdateReferenceRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileReferenceCommand(
                publicId,
                itemPublicId,
                new ReferenceInput(
                    request.PersonName,
                    request.Address,
                    request.Phone,
                    request.ReferenceTypeCode,
                    request.Occupation,
                    request.Workplace,
                    request.WorkPhone,
                    request.KnownTimeYears),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/references/{itemPublicId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteReference(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileReferenceCommand(
                publicId,
                itemPublicId,
                request.ConcurrencyToken),
            cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : this.ToActionResult(result).Result!;
    }
}

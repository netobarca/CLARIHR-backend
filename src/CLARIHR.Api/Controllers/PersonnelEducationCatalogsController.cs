using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PersonnelEducationCatalogs;
using CLARIHR.Application.Features.PersonnelEducationCatalogs.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
public sealed class PersonnelEducationCatalogsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher)
    : ControllerBase
{
    [HttpGet("api/v1/companies/{companyId:guid}/education-statuses")]
    [ProducesResponseType<PagedResponse<PersonnelEducationCatalogItemResponse>>(StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResponse<PersonnelEducationCatalogItemResponse>>> SearchEducationStatuses(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PersonnelEducationCatalogValidationRules.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        Search(companyId, PersonnelEducationCatalogType.EducationStatus, isActive, search, page, pageSize, cancellationToken);

    [HttpGet("api/v1/companies/{companyId:guid}/education-study-types")]
    [ProducesResponseType<PagedResponse<PersonnelEducationCatalogItemResponse>>(StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResponse<PersonnelEducationCatalogItemResponse>>> SearchEducationStudyTypes(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PersonnelEducationCatalogValidationRules.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        Search(companyId, PersonnelEducationCatalogType.StudyType, isActive, search, page, pageSize, cancellationToken);

    [HttpGet("api/v1/companies/{companyId:guid}/education-careers")]
    [ProducesResponseType<PagedResponse<PersonnelEducationCatalogItemResponse>>(StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResponse<PersonnelEducationCatalogItemResponse>>> SearchEducationCareers(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PersonnelEducationCatalogValidationRules.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        Search(companyId, PersonnelEducationCatalogType.Career, isActive, search, page, pageSize, cancellationToken);

    [HttpGet("api/v1/companies/{companyId:guid}/education-shifts")]
    [ProducesResponseType<PagedResponse<PersonnelEducationCatalogItemResponse>>(StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResponse<PersonnelEducationCatalogItemResponse>>> SearchEducationShifts(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PersonnelEducationCatalogValidationRules.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        Search(companyId, PersonnelEducationCatalogType.Shift, isActive, search, page, pageSize, cancellationToken);

    [HttpGet("api/v1/companies/{companyId:guid}/education-modalities")]
    [ProducesResponseType<PagedResponse<PersonnelEducationCatalogItemResponse>>(StatusCodes.Status200OK)]
    public Task<ActionResult<PagedResponse<PersonnelEducationCatalogItemResponse>>> SearchEducationModalities(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PersonnelEducationCatalogValidationRules.DefaultPageSize,
        CancellationToken cancellationToken = default) =>
        Search(companyId, PersonnelEducationCatalogType.Modality, isActive, search, page, pageSize, cancellationToken);

    [HttpGet("api/v1/companies/{companyId:guid}/education-statuses/{id:guid}")]
    [ProducesResponseType<PersonnelEducationCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PersonnelEducationCatalogItemResponse>> GetEducationStatusById(
        Guid companyId,
        Guid id,
        CancellationToken cancellationToken = default) =>
        GetById(companyId, PersonnelEducationCatalogType.EducationStatus, id, cancellationToken);

    [HttpGet("api/v1/companies/{companyId:guid}/education-study-types/{id:guid}")]
    [ProducesResponseType<PersonnelEducationCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PersonnelEducationCatalogItemResponse>> GetEducationStudyTypeById(
        Guid companyId,
        Guid id,
        CancellationToken cancellationToken = default) =>
        GetById(companyId, PersonnelEducationCatalogType.StudyType, id, cancellationToken);

    [HttpGet("api/v1/companies/{companyId:guid}/education-careers/{id:guid}")]
    [ProducesResponseType<PersonnelEducationCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PersonnelEducationCatalogItemResponse>> GetEducationCareerById(
        Guid companyId,
        Guid id,
        CancellationToken cancellationToken = default) =>
        GetById(companyId, PersonnelEducationCatalogType.Career, id, cancellationToken);

    [HttpGet("api/v1/companies/{companyId:guid}/education-shifts/{id:guid}")]
    [ProducesResponseType<PersonnelEducationCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PersonnelEducationCatalogItemResponse>> GetEducationShiftById(
        Guid companyId,
        Guid id,
        CancellationToken cancellationToken = default) =>
        GetById(companyId, PersonnelEducationCatalogType.Shift, id, cancellationToken);

    [HttpGet("api/v1/companies/{companyId:guid}/education-modalities/{id:guid}")]
    [ProducesResponseType<PersonnelEducationCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PersonnelEducationCatalogItemResponse>> GetEducationModalityById(
        Guid companyId,
        Guid id,
        CancellationToken cancellationToken = default) =>
        GetById(companyId, PersonnelEducationCatalogType.Modality, id, cancellationToken);

    [HttpPost("api/v1/companies/{companyId:guid}/education-statuses")]
    [ProducesResponseType<PersonnelEducationCatalogItemResponse>(StatusCodes.Status201Created)]
    public Task<ActionResult<PersonnelEducationCatalogItemResponse>> CreateEducationStatus(
        Guid companyId,
        [FromBody] UpsertPersonnelEducationCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        Create(companyId, PersonnelEducationCatalogType.EducationStatus, request, cancellationToken);

    [HttpPost("api/v1/companies/{companyId:guid}/education-study-types")]
    [ProducesResponseType<PersonnelEducationCatalogItemResponse>(StatusCodes.Status201Created)]
    public Task<ActionResult<PersonnelEducationCatalogItemResponse>> CreateEducationStudyType(
        Guid companyId,
        [FromBody] UpsertPersonnelEducationCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        Create(companyId, PersonnelEducationCatalogType.StudyType, request, cancellationToken);

    [HttpPost("api/v1/companies/{companyId:guid}/education-careers")]
    [ProducesResponseType<PersonnelEducationCatalogItemResponse>(StatusCodes.Status201Created)]
    public Task<ActionResult<PersonnelEducationCatalogItemResponse>> CreateEducationCareer(
        Guid companyId,
        [FromBody] UpsertPersonnelEducationCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        Create(companyId, PersonnelEducationCatalogType.Career, request, cancellationToken);

    [HttpPost("api/v1/companies/{companyId:guid}/education-shifts")]
    [ProducesResponseType<PersonnelEducationCatalogItemResponse>(StatusCodes.Status201Created)]
    public Task<ActionResult<PersonnelEducationCatalogItemResponse>> CreateEducationShift(
        Guid companyId,
        [FromBody] UpsertPersonnelEducationCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        Create(companyId, PersonnelEducationCatalogType.Shift, request, cancellationToken);

    [HttpPost("api/v1/companies/{companyId:guid}/education-modalities")]
    [ProducesResponseType<PersonnelEducationCatalogItemResponse>(StatusCodes.Status201Created)]
    public Task<ActionResult<PersonnelEducationCatalogItemResponse>> CreateEducationModality(
        Guid companyId,
        [FromBody] UpsertPersonnelEducationCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        Create(companyId, PersonnelEducationCatalogType.Modality, request, cancellationToken);

    [HttpPut("api/v1/education-statuses/{id:guid}")]
    [ProducesResponseType<PersonnelEducationCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PersonnelEducationCatalogItemResponse>> UpdateEducationStatus(
        Guid id,
        [FromBody] UpdatePersonnelEducationCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        Update(PersonnelEducationCatalogType.EducationStatus, id, request, cancellationToken);

    [HttpPut("api/v1/education-study-types/{id:guid}")]
    [ProducesResponseType<PersonnelEducationCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PersonnelEducationCatalogItemResponse>> UpdateEducationStudyType(
        Guid id,
        [FromBody] UpdatePersonnelEducationCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        Update(PersonnelEducationCatalogType.StudyType, id, request, cancellationToken);

    [HttpPut("api/v1/education-careers/{id:guid}")]
    [ProducesResponseType<PersonnelEducationCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PersonnelEducationCatalogItemResponse>> UpdateEducationCareer(
        Guid id,
        [FromBody] UpdatePersonnelEducationCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        Update(PersonnelEducationCatalogType.Career, id, request, cancellationToken);

    [HttpPut("api/v1/education-shifts/{id:guid}")]
    [ProducesResponseType<PersonnelEducationCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PersonnelEducationCatalogItemResponse>> UpdateEducationShift(
        Guid id,
        [FromBody] UpdatePersonnelEducationCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        Update(PersonnelEducationCatalogType.Shift, id, request, cancellationToken);

    [HttpPut("api/v1/education-modalities/{id:guid}")]
    [ProducesResponseType<PersonnelEducationCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PersonnelEducationCatalogItemResponse>> UpdateEducationModality(
        Guid id,
        [FromBody] UpdatePersonnelEducationCatalogItemRequest request,
        CancellationToken cancellationToken = default) =>
        Update(PersonnelEducationCatalogType.Modality, id, request, cancellationToken);

    [HttpPatch("api/v1/education-statuses/{id:guid}/activate")]
    [ProducesResponseType<PersonnelEducationCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PersonnelEducationCatalogItemResponse>> ActivateEducationStatus(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        Activate(PersonnelEducationCatalogType.EducationStatus, id, request, cancellationToken);

    [HttpPatch("api/v1/education-study-types/{id:guid}/activate")]
    [ProducesResponseType<PersonnelEducationCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PersonnelEducationCatalogItemResponse>> ActivateEducationStudyType(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        Activate(PersonnelEducationCatalogType.StudyType, id, request, cancellationToken);

    [HttpPatch("api/v1/education-careers/{id:guid}/activate")]
    [ProducesResponseType<PersonnelEducationCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PersonnelEducationCatalogItemResponse>> ActivateEducationCareer(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        Activate(PersonnelEducationCatalogType.Career, id, request, cancellationToken);

    [HttpPatch("api/v1/education-shifts/{id:guid}/activate")]
    [ProducesResponseType<PersonnelEducationCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PersonnelEducationCatalogItemResponse>> ActivateEducationShift(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        Activate(PersonnelEducationCatalogType.Shift, id, request, cancellationToken);

    [HttpPatch("api/v1/education-modalities/{id:guid}/activate")]
    [ProducesResponseType<PersonnelEducationCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PersonnelEducationCatalogItemResponse>> ActivateEducationModality(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        Activate(PersonnelEducationCatalogType.Modality, id, request, cancellationToken);

    [HttpPatch("api/v1/education-statuses/{id:guid}/inactivate")]
    [ProducesResponseType<PersonnelEducationCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PersonnelEducationCatalogItemResponse>> InactivateEducationStatus(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        Inactivate(PersonnelEducationCatalogType.EducationStatus, id, request, cancellationToken);

    [HttpPatch("api/v1/education-study-types/{id:guid}/inactivate")]
    [ProducesResponseType<PersonnelEducationCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PersonnelEducationCatalogItemResponse>> InactivateEducationStudyType(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        Inactivate(PersonnelEducationCatalogType.StudyType, id, request, cancellationToken);

    [HttpPatch("api/v1/education-careers/{id:guid}/inactivate")]
    [ProducesResponseType<PersonnelEducationCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PersonnelEducationCatalogItemResponse>> InactivateEducationCareer(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        Inactivate(PersonnelEducationCatalogType.Career, id, request, cancellationToken);

    [HttpPatch("api/v1/education-shifts/{id:guid}/inactivate")]
    [ProducesResponseType<PersonnelEducationCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PersonnelEducationCatalogItemResponse>> InactivateEducationShift(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        Inactivate(PersonnelEducationCatalogType.Shift, id, request, cancellationToken);

    [HttpPatch("api/v1/education-modalities/{id:guid}/inactivate")]
    [ProducesResponseType<PersonnelEducationCatalogItemResponse>(StatusCodes.Status200OK)]
    public Task<ActionResult<PersonnelEducationCatalogItemResponse>> InactivateEducationModality(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default) =>
        Inactivate(PersonnelEducationCatalogType.Modality, id, request, cancellationToken);

    private async Task<ActionResult<PagedResponse<PersonnelEducationCatalogItemResponse>>> Search(
        Guid companyId,
        PersonnelEducationCatalogType catalogType,
        bool? isActive,
        string? search,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchPersonnelEducationCatalogItemsQuery(companyId, catalogType, isActive, search, page, pageSize),
            cancellationToken);
        return this.ToActionResult(result);
    }

    private async Task<ActionResult<PersonnelEducationCatalogItemResponse>> GetById(
        Guid companyId,
        PersonnelEducationCatalogType catalogType,
        Guid id,
        CancellationToken cancellationToken)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelEducationCatalogItemByIdQuery(companyId, catalogType, id),
            cancellationToken);
        return this.ToActionResult(result);
    }

    private async Task<ActionResult<PersonnelEducationCatalogItemResponse>> Create(
        Guid companyId,
        PersonnelEducationCatalogType catalogType,
        UpsertPersonnelEducationCatalogItemRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(
            new CreatePersonnelEducationCatalogItemCommand(
                companyId,
                catalogType,
                request.Code,
                request.Name,
                request.SortOrder),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(result)
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    private async Task<ActionResult<PersonnelEducationCatalogItemResponse>> Update(
        PersonnelEducationCatalogType catalogType,
        Guid id,
        UpdatePersonnelEducationCatalogItemRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelEducationCatalogItemCommand(
                catalogType,
                id,
                request.Code,
                request.Name,
                request.SortOrder,
                request.ConcurrencyToken),
            cancellationToken);
        return this.ToActionResult(result);
    }

    private async Task<ActionResult<PersonnelEducationCatalogItemResponse>> Activate(
        PersonnelEducationCatalogType catalogType,
        Guid id,
        ConcurrencyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivatePersonnelEducationCatalogItemCommand(catalogType, id, request.ConcurrencyToken),
            cancellationToken);
        return this.ToActionResult(result);
    }

    private async Task<ActionResult<PersonnelEducationCatalogItemResponse>> Inactivate(
        PersonnelEducationCatalogType catalogType,
        Guid id,
        ConcurrencyRequest request,
        CancellationToken cancellationToken)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivatePersonnelEducationCatalogItemCommand(catalogType, id, request.ConcurrencyToken),
            cancellationToken);
        return this.ToActionResult(result);
    }

    public sealed record UpsertPersonnelEducationCatalogItemRequest(
        string Code,
        string Name,
        int SortOrder);

    public sealed record UpdatePersonnelEducationCatalogItemRequest(
        string Code,
        string Name,
        int SortOrder,
        Guid ConcurrencyToken);

    public sealed record ConcurrencyRequest(Guid ConcurrencyToken);
}

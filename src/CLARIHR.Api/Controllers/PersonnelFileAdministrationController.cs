using CLARIHR.Api.Common;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
public sealed class PersonnelFileAdministrationController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet("api/v1/companies/{companyId:guid}/personnel-catalogs/{category}")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelCatalogItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelCatalogItemResponse>>> GetCatalogItems(
        Guid companyId,
        string category,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelCatalogItemsQuery(companyId, category), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/companies/{companyId:guid}/personnel-custom-field-definitions")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelCustomFieldDefinitionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelCustomFieldDefinitionResponse>>> GetCustomFieldDefinitions(
        Guid companyId,
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelCustomFieldDefinitionsQuery(companyId, isActive), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/companies/{companyId:guid}/personnel-custom-field-definitions")]
    [ProducesResponseType<PersonnelCustomFieldDefinitionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonnelCustomFieldDefinitionResponse>> CreateCustomFieldDefinition(
        Guid companyId,
        [FromBody] CreateCustomFieldDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreatePersonnelCustomFieldDefinitionCommand(
                companyId,
                request.Key,
                request.Label,
                request.FieldType,
                request.IsRequired,
                request.IsActive,
                request.OptionsJson,
                request.SortOrder),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<PersonnelCustomFieldDefinitionResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("api/v1/personnel-custom-field-definitions/{id:guid}")]
    [ProducesResponseType<PersonnelCustomFieldDefinitionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonnelCustomFieldDefinitionResponse>> UpdateCustomFieldDefinition(
        Guid id,
        [FromBody] UpdateCustomFieldDefinitionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelCustomFieldDefinitionCommand(
                id,
                request.Key,
                request.Label,
                request.FieldType,
                request.IsRequired,
                request.IsActive,
                request.OptionsJson,
                request.SortOrder,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }
}

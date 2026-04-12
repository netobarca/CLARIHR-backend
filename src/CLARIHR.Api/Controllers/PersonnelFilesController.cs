using CLARIHR.Api.Common;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
public sealed class PersonnelFilesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpPost("api/v1/companies/{companyId:guid}/personnel-files")]
    [ProducesResponseType<PersonnelFileResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileResponse>> Create(
        Guid companyId,
        [FromBody] CreatePersonnelFileRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreatePersonnelFileCommand(
                companyId,
                request.RecordType,
                request.FirstName,
                request.LastName,
                request.BirthDate,
                request.MaritalStatusCode,
                request.ProfessionCode,
                request.Nationality,
                request.PersonalEmail,
                request.InstitutionalEmail,
                request.PersonalPhone,
                request.InstitutionalPhone,
                request.BirthCountryCode,
                request.BirthDepartmentCode,
                request.BirthMunicipalityCode,
                request.PhotoUrl,
                request.OrgUnitPublicId,
                request.AssignedPositionSlotPublicId,
                request.CustomDataJson,
                request.Identifications.Select(item => new IdentificationInput(
                    item.IdentificationTypeCode,
                    item.IdentificationNumber,
                    item.IssuedDate,
                    item.ExpiryDate,
                    item.Issuer,
                    item.IsPrimary)).ToArray()),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<PersonnelFileResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpGet("api/v1/companies/{companyId:guid}/personnel-files")]
    [ProducesResponseType<PagedResponse<PersonnelFileListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<PersonnelFileListItemResponse>>> Search(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery] PersonnelFileRecordType? recordType,
        [FromQuery] Guid? orgUnitId,
        [FromQuery] int? minAge,
        [FromQuery] int? maxAge,
        [FromQuery] string? maritalStatus,
        [FromQuery] string? nationality,
        [FromQuery] string? profession,
        [FromQuery] DateTime? createdFromUtc,
        [FromQuery] DateTime? createdToUtc,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] string? sortBy,
        [FromQuery] PersonnelFileSortDirection sortDirection = PersonnelFileSortDirection.Asc,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = PersonnelFileValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchPersonnelFilesQuery(
                companyId,
                isActive,
                recordType,
                orgUnitId,
                minAge,
                maxAge,
                maritalStatus,
                nationality,
                profession,
                createdFromUtc,
                createdToUtc,
                search,
                sortBy,
                sortDirection,
                page,
                pageSize,
                includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{id:guid}")]
    [ProducesResponseType<PersonnelFileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PersonnelFileResponse>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/personnel-files/{id:guid}/activate")]
    [ProducesResponseType<PersonnelFileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonnelFileResponse>> Activate(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(new ActivatePersonnelFileCommand(id, request.ConcurrencyToken), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/personnel-files/{id:guid}/inactivate")]
    [ProducesResponseType<PersonnelFileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PersonnelFileResponse>> Inactivate(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(new InactivatePersonnelFileCommand(id, request.ConcurrencyToken), cancellationToken);
        return this.ToActionResult(result);
    }
}

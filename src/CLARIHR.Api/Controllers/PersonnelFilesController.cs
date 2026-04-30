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
using Microsoft.AspNetCore.RateLimiting;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
public sealed class PersonnelFilesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [EnableRateLimiting("personnel-files-create")]
    [HttpPost("api/v1/companies/{companyPublicId:guid}/personnel-files")]
    [ProducesResponseType<PersonnelFileShellResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileShellResponse>> Create(
        Guid companyPublicId,
        [FromBody] CreatePersonnelFileRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.HasLegacyItemsPayload())
        {
            return this.ToActionResult(Result<PersonnelFileShellResponse>.Failure(
                ErrorCatalog.Validation(new Dictionary<string, string[]>
                {
                    ["items"] = ["Items is no longer accepted on personnel file create. Use POST /api/v1/personnel-files/{publicId}/identifications instead."]
                })));
        }

        var result = await commandDispatcher.SendAsync(
            new CreatePersonnelFileCommand(
                companyPublicId,
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
                request.AssignedPositionSlotPublicId),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<PersonnelFileShellResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [EnableRateLimiting("personnel-files-search")]
    [HttpGet("api/v1/companies/{companyPublicId:guid}/personnel-files")]
    [ProducesResponseType<PagedResponse<PersonnelFileListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<PagedResponse<PersonnelFileListItemResponse>>> Search(
        Guid companyPublicId,
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
                companyPublicId,
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

    [HttpGet("api/v1/personnel-files/{publicId:guid}")]
    [ProducesResponseType<PersonnelFileShellResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PersonnelFileShellResponse>> GetById(Guid publicId, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileByIdQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [EnableRateLimiting("personnel-files-lifecycle")]
    [HttpPatch("api/v1/personnel-files/{publicId:guid}/activate")]
    [ProducesResponseType<PersonnelFileShellResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<PersonnelFileShellResponse>> Activate(
        Guid publicId,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(new ActivatePersonnelFileCommand(publicId, request.ConcurrencyToken), cancellationToken);
        return this.ToActionResult(result);
    }

    [EnableRateLimiting("personnel-files-lifecycle")]
    [HttpPatch("api/v1/personnel-files/{publicId:guid}/inactivate")]
    [ProducesResponseType<PersonnelFileShellResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<PersonnelFileShellResponse>> Inactivate(
        Guid publicId,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(new InactivatePersonnelFileCommand(publicId, request.ConcurrencyToken), cancellationToken);
        return this.ToActionResult(result);
    }
}

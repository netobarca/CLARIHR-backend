using Asp.Versioning;
using System.ComponentModel.DataAnnotations;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
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
public sealed class PersonnelFilesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Create)]
    [HttpPost("companies/{companyPublicId:guid}/personnel-files")]
    [ProducesResponseType<PersonnelFileShellResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Create a personnel file",
        Description = """
            Creates a new personnel file (the shell record) for the specified company and
            returns it with a `201 Created` response. The `Location` header points to the
            created resource and the `ETag` header carries its initial `concurrencyToken`.

            Sub-resources (identifications, addresses, family members, …) are created via
            their own endpoints after the shell exists; they are not accepted on create.
            """)]
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
                request.PhotoFilePublicId,
                request.OrgUnitPublicId),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetById),
            value => new { publicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Search)]
    [HttpGet("companies/{companyPublicId:guid}/personnel-files")]
    [ProducesResponseType<PagedResponse<PersonnelFileListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "Search personnel files",
        Description = """
            Returns a paginated list of personnel files for the specified company.

            Supports filtering by `isActive`, `recordType`, `orgUnitId`, age range,
            marital status, nationality, profession and creation date range, plus a
            free-text query (`q`) matched against the full name. Set
            `includeAllowedActions=true` to include, per item, the operations the
            current user is authorized to perform on it.
            """)]
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
        [Range(1, PersonnelFileValidationRules.MaxPageSize)]
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

    [HttpGet("personnel-files/{publicId:guid}")]
    [ProducesResponseType<PersonnelFileShellResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file by id",
        Description = """
            Returns the personnel file shell entity.

            The `concurrencyToken` in the response body is required in the `If-Match`
            header of subsequent `PUT`/`PATCH` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<PersonnelFileShellResponse>> GetById(Guid publicId, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileByIdQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("personnel-files/{publicId:guid}")]
    [ProducesResponseType<PersonnelFilePersonalInfoResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Update a personnel file",
        Description = """
            Replaces the core (personal-info) fields of an existing personnel file and
            returns the updated personal information. Requires the `If-Match` header with
            the current `concurrencyToken` to prevent lost updates; the new token is
            returned in the `ETag` header.

            The active/inactive state is **not** changed by this endpoint — toggle
            `isActive` via `PATCH /personnel-files/{publicId}`. The `recordType` cannot be
            transitioned in this module.
            """)]
    public async Task<ActionResult<PersonnelFilePersonalInfoResponse>> Update(
        Guid publicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdatePersonnelFileRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileCommand(
                publicId,
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
                request.PhotoFilePublicId,
                request.OrgUnitPublicId,
                concurrencyToken),
            cancellationToken);

        var mapped = result.IsSuccess
            ? Result<PersonnelFilePersonalInfoResponse>.Success(result.Value.Data)
            : Result<PersonnelFilePersonalInfoResponse>.Failure(result.Error);

        return this.ToActionResultWithETag(mapped, value => value.ConcurrencyToken);
    }

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Lifecycle)]
    [HttpPatch("personnel-files/{publicId:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<PersonnelFilePersonalInfoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a personnel file",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type
            `application/json-patch+json`) to an existing personnel file and returns the
            updated personal information. Requires the `If-Match` header with the current
            `concurrencyToken`; the new token is returned in the `ETag` header.

            Patchable members are the core personal-info fields plus `isActive`. Setting
            `isActive` is the supported mechanism for **activating/deactivating** a
            personnel file (this endpoint replaces the former `/activate` and
            `/inactivate` endpoints). The lifecycle transition Draft → Completed remains a
            separate finalize flow.
            """)]
    public async Task<ActionResult<PersonnelFilePersonalInfoResponse>> Patch(
        Guid publicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchPersonnelFileRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchPersonnelFileCommand(
                publicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new PersonnelFilePatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }
}

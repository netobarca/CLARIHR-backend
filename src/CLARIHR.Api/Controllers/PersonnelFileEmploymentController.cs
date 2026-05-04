using CLARIHR.Api.Common;
using CLARIHR.Api.Contracts.PersonnelFiles;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Domain.PersonnelFiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
public sealed class PersonnelFileEmploymentController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    ReportExportDeliveryService reportExportDeliveryService) : ControllerBase
{
    [HttpGet("api/v1/personnel-files/{publicId:guid}/employee-profile")]
    [ProducesResponseType<PersonnelFileSectionResult<PersonnelFileEmployeeProfileResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileEmployeeProfileResponse>> GetEmployeeProfile(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileEmployeeProfileQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/finalize")]
    [ProducesResponseType<FinalizePersonnelFileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<FinalizePersonnelFileResponse>> Finalize(
        Guid publicId,
        [FromBody] FinalizePersonnelFileRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new FinalizePersonnelFileCommand(publicId, request.ConcurrencyToken, request.CreateUserAccount ?? true),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/finalize/preview")]
    [ProducesResponseType<FinalizePersonnelFilePreviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FinalizePersonnelFilePreviewResponse>> PreviewFinalize(
        Guid publicId,
        [FromQuery] bool? createUserAccount = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new PreviewFinalizePersonnelFileQuery(publicId, createUserAccount ?? true),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/employee-profile")]
    [ProducesResponseType<PersonnelFileEmployeeProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult<PersonnelFileEmployeeProfileResponse>>> UpdateEmployeeProfile(
        Guid publicId,
        [FromBody] UpdatePersonnelFileEmployeeProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileEmployeeProfileCommand(
                publicId,
                request.EmployeeCode,
                request.EmploymentStatusCode,
                request.IsEmploymentActive,
                request.ContractTypeCode,
                request.HireDate,
                request.RetirementCategoryCode,
                request.RetirementReasonCode,
                request.RetirementNotes,
                request.RetirementDate,
                request.WorkdayCode,
                request.PayrollTypeCode,
                request.PositionSlotPublicId,
                request.JobProfilePublicId,
                request.OrgUnitPublicId,
                request.WorkCenterPublicId,
                request.CostCenterPublicId,
                request.ContractStartDate,
                request.ContractEndDate,
                request.VacationConfigurationJson,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/employment-assignments")]
    [ProducesResponseType<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>> GetEmploymentAssignments(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileEmploymentAssignmentsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/employment-assignments")]
    [ProducesResponseType<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>>> AddEmploymentAssignment(
        Guid publicId,
        [FromBody] AddEmploymentAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileEmploymentAssignmentCommand(
                publicId,
                new EmploymentAssignmentInput(
                    request.AssignmentTypeCode,
                    request.PositionSlotPublicId,
                    request.OrgUnitPublicId,
                    request.WorkCenterPublicId,
                    request.CostCenterPublicId,
                    request.StartDate,
                    request.EndDate,
                    request.IsPrimary,
                    request.IsActive,
                    request.Notes)),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(result)
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/employment-assignments/{itemPublicId:guid}")]
    [ProducesResponseType<PersonnelFileSectionResult<PersonnelFileEmploymentAssignmentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult<PersonnelFileEmploymentAssignmentResponse>>> UpdateEmploymentAssignment(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] UpdateEmploymentAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileEmploymentAssignmentCommand(
                publicId,
                itemPublicId,
                new EmploymentAssignmentInput(
                    request.AssignmentTypeCode,
                    request.PositionSlotPublicId,
                    request.OrgUnitPublicId,
                    request.WorkCenterPublicId,
                    request.CostCenterPublicId,
                    request.StartDate,
                    request.EndDate,
                    request.IsPrimary,
                    request.IsActive,
                    request.Notes),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/employment-assignments/{itemPublicId:guid}/deactivate")]
    [ProducesResponseType<PersonnelFileSectionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult>> DeactivateEmploymentAssignment(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeactivatePersonnelFileEmploymentAssignmentCommand(publicId, itemPublicId, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/contract-history")]
    [ProducesResponseType<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileContractHistoryResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileContractHistoryResponse>>> GetContractHistory(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileContractHistoryQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/contract-history")]
    [ProducesResponseType<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileContractHistoryResponse>>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileContractHistoryResponse>>>> AddContractHistory(
        Guid publicId,
        [FromBody] AddContractHistoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileContractHistoryCommand(
                publicId,
                new ContractHistoryInput(
                    request.ContractTypeCode,
                    request.ContractDate,
                    request.ContractEndDate,
                    request.PositionSlotPublicId,
                    request.IsActive,
                    request.Notes)),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(result)
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/contract-history/{itemPublicId:guid}")]
    [ProducesResponseType<PersonnelFileSectionResult<PersonnelFileContractHistoryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult<PersonnelFileContractHistoryResponse>>> UpdateContractHistory(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] UpdateContractHistoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileContractHistoryCommand(
                publicId,
                itemPublicId,
                new ContractHistoryInput(
                    request.ContractTypeCode,
                    request.ContractDate,
                    request.ContractEndDate,
                    request.PositionSlotPublicId,
                    request.IsActive,
                    request.Notes),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/contract-history/{itemPublicId:guid}/deactivate")]
    [ProducesResponseType<PersonnelFileSectionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult>> DeactivateContractHistory(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeactivatePersonnelFileContractHistoryCommand(publicId, itemPublicId, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/position-hierarchy")]
    [ProducesResponseType<PersonnelFilePositionHierarchyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFilePositionHierarchyResponse>> GetPositionHierarchy(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFilePositionHierarchyQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/authorization-substitutions")]
    [ProducesResponseType<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>>> AddAuthorizationSubstitution(
        Guid publicId,
        [FromBody] AddAuthorizationSubstitutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileAuthorizationSubstitutionCommand(
                publicId,
                new AuthorizationSubstitutionInput(
                    request.SubstitutionTypeCode,
                    request.SubstitutePersonnelFilePublicId,
                    request.SubstitutePositionTitle,
                    request.StartDate,
                    request.EndDate,
                    request.IsActive,
                    request.Notes)),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(result)
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/authorization-substitutions/{itemPublicId:guid}")]
    [ProducesResponseType<PersonnelFileSectionResult<PersonnelFileAuthorizationSubstitutionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult<PersonnelFileAuthorizationSubstitutionResponse>>> UpdateAuthorizationSubstitution(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] UpdateAuthorizationSubstitutionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileAuthorizationSubstitutionCommand(
                publicId,
                itemPublicId,
                new AuthorizationSubstitutionInput(
                    request.SubstitutionTypeCode,
                    request.SubstitutePersonnelFilePublicId,
                    request.SubstitutePositionTitle,
                    request.StartDate,
                    request.EndDate,
                    request.IsActive,
                    request.Notes),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/authorization-substitutions/{itemPublicId:guid}/deactivate")]
    [ProducesResponseType<PersonnelFileSectionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult>> DeactivateAuthorizationSubstitution(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeactivatePersonnelFileAuthorizationSubstitutionCommand(publicId, itemPublicId, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/authorization-substitutions")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>> GetAuthorizationSubstitutions(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileAuthorizationSubstitutionsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/personnel-actions")]
    [ProducesResponseType<PersonnelFilePersonnelActionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFilePersonnelActionResponse>> AddPersonnelAction(
        Guid publicId,
        [FromBody] AddPersonnelActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFilePersonnelActionCommand(
                publicId,
                request.ActionTypeCode,
                request.ActionStatusCode,
                request.ActionDateUtc,
                request.EffectiveFromUtc,
                request.EffectiveToUtc,
                request.Description,
                request.Reference,
                request.Amount,
                request.CurrencyCode),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<PersonnelFilePersonnelActionResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/personnel-actions")]
    [ProducesResponseType<PagedResponse<PersonnelFilePersonnelActionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PagedResponse<PersonnelFilePersonnelActionResponse>>> SearchPersonnelActions(
        Guid publicId,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] string? type = null,
        [FromQuery] string? status = null,
        [FromQuery(Name = "q")] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] PersonnelFileSortDirection sortDirection = PersonnelFileSortDirection.Desc,
        [FromQuery(Name = "page")] int pageNumber = 1,
        [FromQuery] int pageSize = PersonnelFileValidationRules.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchPersonnelFilePersonnelActionsQuery(
                publicId,
                fromUtc,
                toUtc,
                type,
                status,
                search,
                sortBy,
                sortDirection,
                pageNumber,
                pageSize),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/personnel-actions/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ExportPersonnelActions(
        Guid publicId,
        [FromQuery] string format = "xlsx",
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromQuery] string? type = null,
        [FromQuery] string? status = null,
        [FromQuery(Name = "q")] string? search = null,
        [FromQuery] string? sortBy = null,
        [FromQuery] PersonnelFileSortDirection sortDirection = PersonnelFileSortDirection.Desc,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportPersonnelFilePersonnelActionsQuery(
                publicId,
                fromUtc,
                toUtc,
                type,
                status,
                search,
                sortBy,
                sortDirection,
                reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<PersonnelFilePersonnelActionExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "personnel-actions",
            "PersonnelActions",
            AuditEntityTypes.PersonnelFile,
            ReportExportResources.PersonnelFilePersonnelActions,
            "Exported personnel actions report.",
            new { personnelFileId = publicId, fromUtc, toUtc, type, status, q = search, sortBy, sortDirection },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/assets-accesses")]
    [ProducesResponseType<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAssetAccessResponse>>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAssetAccessResponse>>>> AddAssetAccess(
        Guid publicId,
        [FromBody] AddAssetAccessRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileAssetAccessCommand(
                publicId,
                new AssetAccessInput(
                    request.AssetTypeCode,
                    request.AssetOrAccessName,
                    request.AccessLevelCode,
                    request.StartDateUtc,
                    request.EndDateUtc,
                    request.DeliveryDateUtc,
                    request.DeliveryStatusCode,
                    request.IsActive,
                    request.Notes)),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(result)
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/assets-accesses/{itemPublicId:guid}")]
    [ProducesResponseType<PersonnelFileSectionResult<PersonnelFileAssetAccessResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult<PersonnelFileAssetAccessResponse>>> UpdateAssetAccess(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] UpdateAssetAccessRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileAssetAccessCommand(
                publicId,
                itemPublicId,
                new AssetAccessInput(
                    request.AssetTypeCode,
                    request.AssetOrAccessName,
                    request.AccessLevelCode,
                    request.StartDateUtc,
                    request.EndDateUtc,
                    request.DeliveryDateUtc,
                    request.DeliveryStatusCode,
                    request.IsActive,
                    request.Notes),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/assets-accesses/{itemPublicId:guid}/deactivate")]
    [ProducesResponseType<PersonnelFileSectionResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult>> DeactivateAssetAccess(
        Guid publicId,
        Guid itemPublicId,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeactivatePersonnelFileAssetAccessCommand(publicId, itemPublicId, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/assets-accesses")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileAssetAccessResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileAssetAccessResponse>>> GetAssetsAccesses(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileAssetsAccessesQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

}

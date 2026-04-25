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
    [HttpGet("api/v1/personnel-files/{id:guid}/employee-profile")]
    [ProducesResponseType<PersonnelFileSectionResult<PersonnelFileEmployeeProfileResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileEmployeeProfileResponse>> GetEmployeeProfile(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileEmployeeProfileQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{id:guid}/finalize")]
    [ProducesResponseType<FinalizePersonnelFileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<FinalizePersonnelFileResponse>> Finalize(
        Guid id,
        [FromBody] FinalizePersonnelFileRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new FinalizePersonnelFileCommand(id, request.ConcurrencyToken, request.CreateUserAccount ?? true),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{id:guid}/finalize/preview")]
    [ProducesResponseType<FinalizePersonnelFilePreviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FinalizePersonnelFilePreviewResponse>> PreviewFinalize(
        Guid id,
        [FromQuery] bool? createUserAccount = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new PreviewFinalizePersonnelFileQuery(id, createUserAccount ?? true),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/employee-profile")]
    [ProducesResponseType<PersonnelFileEmployeeProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult<PersonnelFileEmployeeProfileResponse>>> UpdateEmployeeProfile(
        Guid id,
        [FromBody] UpdatePersonnelFileEmployeeProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileEmployeeProfileCommand(
                id,
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

    [HttpGet("api/v1/personnel-files/{id:guid}/employment-assignments")]
    [ProducesResponseType<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>> GetEmploymentAssignments(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileEmploymentAssignmentsQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/employment-assignments")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>>> ReplaceEmploymentAssignments(
        Guid id,
        [FromBody] ReplaceEmploymentAssignmentsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFileEmploymentAssignmentsCommand(
                id,
                request.Items.Select(item => new EmploymentAssignmentInput(
                    item.AssignmentTypeCode,
                    item.PositionSlotPublicId,
                    item.OrgUnitPublicId,
                    item.WorkCenterPublicId,
                    item.CostCenterPublicId,
                    item.StartDate,
                    item.EndDate,
                    item.IsPrimary,
                    item.IsActive,
                    item.Notes)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{id:guid}/contract-history")]
    [ProducesResponseType<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileContractHistoryResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileContractHistoryResponse>>> GetContractHistory(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileContractHistoryQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/contract-history")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileContractHistoryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileContractHistoryResponse>>>> ReplaceContractHistory(
        Guid id,
        [FromBody] ReplaceContractHistoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFileContractHistoryCommand(
                id,
                request.Items.Select(item => new ContractHistoryInput(
                    item.ContractTypeCode,
                    item.ContractDate,
                    item.ContractEndDate,
                    item.PositionSlotPublicId,
                    item.Notes)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{id:guid}/position-hierarchy")]
    [ProducesResponseType<PersonnelFilePositionHierarchyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFilePositionHierarchyResponse>> GetPositionHierarchy(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFilePositionHierarchyQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/authorization-substitutions")]
    [ProducesResponseType<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>>> ReplaceAuthorizationSubstitutions(
        Guid id,
        [FromBody] ReplaceAuthorizationSubstitutionsRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFileAuthorizationSubstitutionsCommand(
                id,
                request.Items.Select(item => new AuthorizationSubstitutionInput(
                    item.SubstitutionTypeCode,
                    item.SubstitutePersonnelFilePublicId,
                    item.SubstitutePositionTitle,
                    item.StartDate,
                    item.EndDate,
                    item.IsActive,
                    item.Notes)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{id:guid}/authorization-substitutions")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>>> GetAuthorizationSubstitutions(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileAuthorizationSubstitutionsQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{id:guid}/personnel-actions")]
    [ProducesResponseType<PersonnelFilePersonnelActionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFilePersonnelActionResponse>> AddPersonnelAction(
        Guid id,
        [FromBody] AddPersonnelActionRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFilePersonnelActionCommand(
                id,
                request.ActionTypeCode,
                request.ActionStatusCode,
                request.ActionDateUtc,
                request.EffectiveFromUtc,
                request.EffectiveToUtc,
                request.Description,
                request.Reference,
                request.Amount,
                request.CurrencyCode,
                request.ConcurrencyToken),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<PersonnelFilePersonnelActionResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpGet("api/v1/personnel-files/{id:guid}/personnel-actions")]
    [ProducesResponseType<PagedResponse<PersonnelFilePersonnelActionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PagedResponse<PersonnelFilePersonnelActionResponse>>> SearchPersonnelActions(
        Guid id,
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
                id,
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

    [HttpGet("api/v1/personnel-files/{id:guid}/personnel-actions/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ExportPersonnelActions(
        Guid id,
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
                id,
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
            new { personnelFileId = id, fromUtc, toUtc, type, status, q = search, sortBy, sortDirection },
            PersonnelFileErrors.ExportFormatInvalid,
            cancellationToken);
    }

    [HttpPut("api/v1/personnel-files/{id:guid}/assets-accesses")]
    [ProducesResponseType<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAssetAccessResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileAssetAccessResponse>>>> ReplaceAssetsAccesses(
        Guid id,
        [FromBody] ReplaceAssetsAccessesRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplacePersonnelFileAssetsAccessesCommand(
                id,
                request.Items.Select(item => new AssetAccessInput(
                    item.AssetTypeCode,
                    item.AssetOrAccessName,
                    item.AccessLevelCode,
                    item.StartDateUtc,
                    item.EndDateUtc,
                    item.DeliveryDateUtc,
                    item.DeliveryStatusCode,
                    item.IsActive,
                    item.Notes)).ToArray(),
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{id:guid}/assets-accesses")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileAssetAccessResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileAssetAccessResponse>>> GetAssetsAccesses(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileAssetsAccessesQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

}

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
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Domain.PersonnelFiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
[Tags("Personnel Files")]
[AuthorizationPolicySet(PersonnelFilePolicies.Read, PersonnelFilePolicies.Manage)]
public sealed class PersonnelFileEmploymentController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    ReportExportDeliveryService reportExportDeliveryService) : ControllerBase
{
    [HttpGet("api/v1/personnel-files/{publicId:guid}/employment-information")]
    [ProducesResponseType<PersonnelFileEmployeeProfileResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [SwaggerOperation(
        Summary = "Get a personnel file's employment information",
        Description = """
            Returns the single employment-information section of the specified personnel file, including its
            current `concurrencyToken` for use in the `If-Match` header of a subsequent `PUT`. The section
            is created lazily on the first `PUT`; until then this returns `200 OK` with a `null` body
            (consistent with the sibling employee sub-resources, whose lists return an empty array when
            empty). A `404` means the personnel file itself does not exist.
            """)]
    public async Task<ActionResult<PersonnelFileEmployeeProfileResponse?>> GetEmployeeProfile(
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
    [SwaggerOperation(
        Summary = "Finalize a personnel file",
        Description = """
            Transitions the specified personnel file out of `Draft` (and optionally provisions a user
            account). Requires the `If-Match` header with the current `concurrencyToken`. Returns the
            finalization outcome.
            """)]
    public async Task<ActionResult<FinalizePersonnelFileResponse>> Finalize(
        Guid publicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] FinalizePersonnelFileRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new FinalizePersonnelFileCommand(publicId, concurrencyToken, request.CreateUserAccount ?? true, request.PositionSlotPublicId),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/rehire")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<RehireEmployeeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [SwaggerOperation(
        Summary = "Rehire a retired employee",
        Description = """
            Reactivates a retired employee's existing personnel file and opens a new employment
            period (new hire date, contract and position-slot assignment), preserving the prior
            period as derived history and re-provisioning the user account. Requires the `If-Match`
            header with the current `concurrencyToken`. A file marked "not rehireable" additionally
            requires the `PersonnelFiles.AuthorizeRehire` permission and an `authorizationReason`.
            """)]
    public async Task<ActionResult<RehireEmployeeResponse>> Rehire(
        Guid publicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] RehireEmployeeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new RehireEmployeeCommand(
                publicId,
                concurrencyToken,
                request.NewHireDate,
                request.ContractTypeCode,
                request.ContractStartDate,
                request.ContractEndDate,
                request.PositionSlotPublicId,
                request.AssignmentTypeCode,
                request.CreateUserAccount ?? true,
                request.NewInstitutionalEmail,
                request.PriorPeriodClosureConfirmed,
                request.AuthorizationReason),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/employment-periods")]
    [ProducesResponseType<EmploymentPeriodsTimelineResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [SwaggerOperation(
        Summary = "Get the employment-periods timeline",
        Description = """
            Returns the chronological timeline of the employee's employment periods (RF-011),
            derived from contract history (no dedicated entity). Each rehire adds a period; the
            active period is flagged with `isCurrent`.
            """)]
    public async Task<ActionResult<EmploymentPeriodsTimelineResponse>> GetEmploymentPeriods(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetEmploymentPeriodsTimelineQuery(publicId), cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/finalize/preview")]
    [ProducesResponseType<FinalizePersonnelFilePreviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [SwaggerOperation(
        Summary = "Preview a personnel file finalization",
        Description = """
            Returns a read-only preview of what finalizing the specified personnel file would do
            (validation results and the optional user-account provisioning), without mutating anything.
            """)]
    public async Task<ActionResult<FinalizePersonnelFilePreviewResponse>> PreviewFinalize(
        Guid publicId,
        [FromQuery] bool? createUserAccount = null,
        [FromQuery] Guid? positionSlotPublicId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new PreviewFinalizePersonnelFileQuery(publicId, createUserAccount ?? true, positionSlotPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/employment-information")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileEmployeeProfileResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Create or replace a personnel file's employment information",
        Description = """
            Upserts the single employment-information section of the specified personnel file. When a profile
            already exists, the `If-Match` header with its current `concurrencyToken` is required to
            prevent lost updates; the refreshed token is returned in the `ETag` header. The first
            create requires no `If-Match`. Supplying `institutionalEmail` changes the employee's institutional
            email — which is the linked sign-in account's identifier, so the account is re-synced to it; omit it
            (or send `null`) to leave it unchanged. A `409` is returned when the email already belongs to
            another account. The retirement metadata is NOT written here (retirement module D-01): the baja is
            registered through `…/retirement-requests`, the `RETIRADO` status is reserved to its execution, and
            a retired profile rejects this PUT entirely (`422`) — only the reversal or a rehire touch it.
            """)]
    public async Task<ActionResult<PersonnelFileEmployeeProfileResponse>> UpdateEmployeeProfile(
        Guid publicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdatePersonnelFileEmployeeProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileEmployeeProfileCommand(
                publicId,
                request.EmployeeCode,
                request.EmploymentStatusCode,
                request.HireDate,
                concurrencyToken,
                request.InstitutionalEmail),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/assigned-positions")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's employment assignments",
        Description = """
            Returns every employment assignment recorded for the specified personnel file. Each item
            carries its own `concurrencyToken`, required in the `If-Match` header of subsequent
            `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>>> GetEmploymentAssignments(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileEmploymentAssignmentsQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/assigned-positions/{employmentAssignmentPublicId:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileEmploymentAssignmentResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file employment assignment by id",
        Description = """
            Returns a single employment assignment of the specified personnel file. The `concurrencyToken`
            in the response is required in the `If-Match` header of subsequent
            `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<PersonnelFileEmploymentAssignmentResponse>> GetEmploymentAssignmentById(
        Guid publicId,
        Guid employmentAssignmentPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileEmploymentAssignmentByIdQuery(publicId, employmentAssignmentPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/assigned-positions")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileEmploymentAssignmentResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Add an employment assignment to a personnel file",
        Description = """
            Creates a new employment assignment under the specified personnel file and returns it with a
            `201 Created` response. The `Location` header points to the created resource and the
            `ETag` header carries its initial `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFileEmploymentAssignmentResponse>> AddEmploymentAssignment(
        Guid publicId,
        [FromBody] AddEmploymentAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new AddPersonnelFileEmploymentAssignmentCommand(
                publicId,
                new EmploymentAssignmentInput(
                    request.AssignmentTypeCode,
                    request.ContractTypeCode,
                    request.WorkdayCode,
                    request.PayrollTypeCode,
                    request.PositionSlotPublicId,
                    request.OrgUnitPublicId,
                    request.WorkCenterPublicId,
                    request.CostCenterPublicId,
                    request.StartDate,
                    request.EndDate,
                    request.IsPrimary,
                    request.IsActive,
                    request.Notes,
                    request.PaymentMethodCode,
                    request.PaymentBankAccountPublicId)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetEmploymentAssignmentById),
            value => new { publicId, employmentAssignmentPublicId = value.EmploymentAssignmentPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/assigned-positions/{employmentAssignmentPublicId:guid}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileEmploymentAssignmentResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace a personnel file employment assignment",
        Description = """
            Replaces the business fields of an existing employment assignment. The active state is
            preserved (it is mutated exclusively via `PATCH`). Requires the `If-Match` header
            with the current `concurrencyToken`; the new token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileEmploymentAssignmentResponse>> UpdateEmploymentAssignment(
        Guid publicId,
        Guid employmentAssignmentPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateEmploymentAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileEmploymentAssignmentCommand(
                publicId,
                employmentAssignmentPublicId,
                new EmploymentAssignmentInput(
                    request.AssignmentTypeCode,
                    request.ContractTypeCode,
                    request.WorkdayCode,
                    request.PayrollTypeCode,
                    request.PositionSlotPublicId,
                    request.OrgUnitPublicId,
                    request.WorkCenterPublicId,
                    request.CostCenterPublicId,
                    request.StartDate,
                    request.EndDate,
                    request.IsPrimary,
                    IsActive: true,
                    request.Notes,
                    request.PaymentMethodCode,
                    request.PaymentBankAccountPublicId),
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/assigned-positions/{employmentAssignmentPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [Produces("application/json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<PersonnelFileEmploymentAssignmentResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a personnel file employment assignment",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type
            `application/json-patch+json`) to an existing employment assignment. Supports the business
            fields and the `isActive` flag. Requires the `If-Match` header with the current
            `concurrencyToken`; the new token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileEmploymentAssignmentResponse>> PatchEmploymentAssignment(
        Guid publicId,
        Guid employmentAssignmentPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchEmploymentAssignmentRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchPersonnelFileEmploymentAssignmentCommand(
                publicId,
                employmentAssignmentPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new PersonnelFileEmploymentAssignmentPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/assigned-positions/{employmentAssignmentPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove an employment assignment from a personnel file",
        Description = """
            Deletes the specified employment assignment. Requires the `If-Match` header with the current
            `concurrencyToken`. Returns the parent personnel file's refreshed concurrency token
            so the caller can keep mutating without an extra round-trip.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteEmploymentAssignment(
        Guid publicId,
        Guid employmentAssignmentPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileEmploymentAssignmentCommand(publicId, employmentAssignmentPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/contract-history")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileContractHistoryResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's contract history",
        Description = """
            Returns every contract history entry recorded for the specified personnel file. Each item
            carries its own `concurrencyToken`, required in the `If-Match` header of subsequent
            `PUT`/`PATCH` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileContractHistoryResponse>>> GetContractHistory(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileContractHistoryQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/contract-history/{contractHistoryPublicId:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileContractHistoryResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file contract history entry by id",
        Description = """
            Returns a single contract history entry of the specified personnel file. The `concurrencyToken`
            in the response is required in the `If-Match` header of subsequent
            `PUT`/`PATCH` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<PersonnelFileContractHistoryResponse>> GetContractHistoryById(
        Guid publicId,
        Guid contractHistoryPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileContractHistoryByIdQuery(publicId, contractHistoryPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/contract-history")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileContractHistoryResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Add a contract history entry to a personnel file",
        Description = """
            Creates a new contract history entry under the specified personnel file and returns it with a
            `201 Created` response. The `Location` header points to the created resource and the
            `ETag` header carries its initial `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFileContractHistoryResponse>> AddContractHistory(
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

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetContractHistoryById),
            value => new { publicId, contractHistoryPublicId = value.ContractHistoryPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/contract-history/{contractHistoryPublicId:guid}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileContractHistoryResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace a personnel file contract history entry",
        Description = """
            Replaces the business fields of an existing contract history entry. The active state is
            preserved (it is mutated exclusively via `PATCH`). Requires the `If-Match` header
            with the current `concurrencyToken`; the new token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileContractHistoryResponse>> UpdateContractHistory(
        Guid publicId,
        Guid contractHistoryPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateContractHistoryRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileContractHistoryCommand(
                publicId,
                contractHistoryPublicId,
                new ContractHistoryInput(
                    request.ContractTypeCode,
                    request.ContractDate,
                    request.ContractEndDate,
                    request.PositionSlotPublicId,
                    IsActive: true,
                    request.Notes),
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/contract-history/{contractHistoryPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [Produces("application/json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<PersonnelFileContractHistoryResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a personnel file contract history entry",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type
            `application/json-patch+json`) to an existing contract history entry. Supports the business
            fields and the `isActive` flag. Requires the `If-Match` header with the current
            `concurrencyToken`; the new token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileContractHistoryResponse>> PatchContractHistory(
        Guid publicId,
        Guid contractHistoryPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchContractHistoryRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchPersonnelFileContractHistoryCommand(
                publicId,
                contractHistoryPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new PersonnelFileContractHistoryPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/position-hierarchy")]
    [ProducesResponseType<PersonnelFilePositionHierarchyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [SwaggerOperation(
        Summary = "Get a personnel file's position hierarchy",
        Description = """
            Returns the read-only position hierarchy (reporting line and subordinates) computed for the
            specified personnel file's assigned position.
            """)]
    public async Task<ActionResult<PersonnelFilePositionHierarchyResponse>> GetPositionHierarchy(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFilePositionHierarchyQuery(publicId), cancellationToken);
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
    [SwaggerOperation(
        Summary = "Add a personnel action to a personnel file",
        Description = """
            Creates a new personnel action under the specified personnel file and returns it with a
            `201 Created` response. The `Location` header points to the created resource and the
            `ETag` header carries its initial `concurrencyToken`. Personnel actions are an append-only
            audit log; they cannot be updated or deleted.
            """)]
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

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetPersonnelActionById),
            value => new { publicId, personnelActionPublicId = value.PersonnelActionPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/personnel-actions/{personnelActionPublicId:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFilePersonnelActionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file personnel action by id",
        Description = """
            Returns a single personnel action of the specified personnel file. Personnel actions are an
            append-only audit log; the `concurrencyToken` in the response is informational, as they
            cannot be updated or deleted.
            """)]
    public async Task<ActionResult<PersonnelFilePersonnelActionResponse>> GetPersonnelActionById(
        Guid publicId,
        Guid personnelActionPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFilePersonnelActionByIdQuery(publicId, personnelActionPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/personnel-actions")]
    [ProducesResponseType<PagedResponse<PersonnelFilePersonnelActionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [SwaggerOperation(
        Summary = "Search a personnel file's personnel actions",
        Description = """
            Returns a paginated, filterable list of personnel actions for the specified personnel
            file. Supports date-range, type, status, and free-text (`q`) filters plus sorting. Personnel
            actions are an append-only audit log.
            """)]
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
        [Range(1, PersonnelFileValidationRules.MaxPageSize)]
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

    [EnableRateLimiting(PersonnelFileRateLimitPolicies.Export)]
    [HttpGet("api/v1/personnel-files/{publicId:guid}/personnel-actions/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [SwaggerOperation(
        Summary = "Export a personnel file's personnel actions",
        Description = """
            Exports the filtered personnel actions of the specified personnel file as a file
            (default `xlsx`). Accepts the same date-range, type, status, free-text (`q`), and sorting
            filters as the search endpoint.
            """)]
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

    [HttpGet("api/v1/personnel-files/{publicId:guid}/assets-accesses")]
    [Produces("application/json")]
    [ProducesResponseType<IReadOnlyCollection<PersonnelFileAssetAccessResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "List a personnel file's assets and accesses",
        Description = """
            Returns every asset/access recorded for the specified personnel file. Each item
            carries its own `concurrencyToken`, required in the `If-Match` header of subsequent
            `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<IReadOnlyCollection<PersonnelFileAssetAccessResponse>>> GetAssetsAccesses(
        Guid publicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetPersonnelFileAssetsAccessesQuery(publicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/personnel-files/{publicId:guid}/assets-accesses/{assetAccessPublicId:guid}")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileAssetAccessResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a personnel file asset/access by id",
        Description = """
            Returns a single asset/access of the specified personnel file. The `concurrencyToken`
            in the response is required in the `If-Match` header of subsequent
            `PUT`/`PATCH`/`DELETE` requests to prevent lost updates.
            """)]
    public async Task<ActionResult<PersonnelFileAssetAccessResponse>> GetAssetAccessById(
        Guid publicId,
        Guid assetAccessPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetPersonnelFileAssetAccessByIdQuery(publicId, assetAccessPublicId),
            cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/personnel-files/{publicId:guid}/assets-accesses")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileAssetAccessResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Add an asset/access to a personnel file",
        Description = """
            Creates a new asset/access under the specified personnel file and returns it with a
            `201 Created` response. The `Location` header points to the created resource and the
            `ETag` header carries its initial `concurrencyToken`.
            """)]
    public async Task<ActionResult<PersonnelFileAssetAccessResponse>> AddAssetAccess(
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

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetAssetAccessById),
            value => new { publicId, assetAccessPublicId = value.AssetAccessPublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("api/v1/personnel-files/{publicId:guid}/assets-accesses/{assetAccessPublicId:guid}")]
    [Consumes("application/json")]
    [Produces("application/json")]
    [ProducesResponseType<PersonnelFileAssetAccessResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Replace a personnel file asset/access",
        Description = """
            Replaces the business fields of an existing asset/access. The active state is
            preserved (it is mutated exclusively via `PATCH`). Requires the `If-Match` header
            with the current `concurrencyToken`; the new token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileAssetAccessResponse>> UpdateAssetAccess(
        Guid publicId,
        Guid assetAccessPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateAssetAccessRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdatePersonnelFileAssetAccessCommand(
                publicId,
                assetAccessPublicId,
                new AssetAccessInput(
                    request.AssetTypeCode,
                    request.AssetOrAccessName,
                    request.AccessLevelCode,
                    request.StartDateUtc,
                    request.EndDateUtc,
                    request.DeliveryDateUtc,
                    request.DeliveryStatusCode,
                    IsActive: true,
                    request.Notes),
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/personnel-files/{publicId:guid}/assets-accesses/{assetAccessPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [Produces("application/json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<PersonnelFileAssetAccessResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a personnel file asset/access",
        Description = """
            Applies a JSON Patch document (RFC 6902, media type
            `application/json-patch+json`) to an existing asset/access. Supports the business
            fields and the `isActive` flag. Requires the `If-Match` header with the current
            `concurrencyToken`; the new token is returned in the `ETag` header.
            """)]
    public async Task<ActionResult<PersonnelFileAssetAccessResponse>> PatchAssetAccess(
        Guid publicId,
        Guid assetAccessPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchAssetAccessRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchPersonnelFileAssetAccessCommand(
                publicId,
                assetAccessPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(patchDoc, static (op, path, from, value) => new PersonnelFileAssetAccessPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpDelete("api/v1/personnel-files/{publicId:guid}/assets-accesses/{assetAccessPublicId:guid}")]
    [ProducesResponseType<PersonnelFileParentConcurrencyResult>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
    [SwaggerOperation(
        Summary = "Remove an asset/access from a personnel file",
        Description = """
            Deletes the specified asset/access. Requires the `If-Match` header with the current
            `concurrencyToken`. Returns the parent personnel file's refreshed concurrency token
            so the caller can keep mutating without an extra round-trip.
            """)]
    public async Task<ActionResult<PersonnelFileParentConcurrencyResult>> DeleteAssetAccess(
        Guid publicId,
        Guid assetAccessPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new DeletePersonnelFileAssetAccessCommand(publicId, assetAccessPublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ParentConcurrencyToken);
    }

}

using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Application.Features.SalaryTabulator;
using CLARIHR.Application.Features.SalaryTabulator.Common;
using CLARIHR.Domain.SalaryTabulator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

// Authorization is handler-gated via ISalaryTabulatorAuthorizationService (company-scoped capability
// checks run inside the command/query handlers), NOT declarative RBAC. This family is intentionally
// excluded from [AuthorizationPolicySet]/GovernedFamilyRegex (like AccountCompanies and
// PersonnelFileReporting): there is no single Read/Manage policy pair to declare, so a declarative
// attribute would be misleading. The literal `api/v1/...` routes are kept unversioned (no [ApiVersion])
// because the wire path must stay identical for the existing FE.
[ApiController]
[Authorize]
[Tags("Salary Tabulator")]
public sealed class SalaryTabulatorController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    ReportExportDeliveryService reportExportDeliveryService) : ControllerBase
{
    [HttpGet("api/v1/companies/{companyId:guid}/salary-tabulator/lines")]
    [ProducesResponseType<PagedResponse<SalaryTabulatorLineListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<SalaryTabulatorLineListItemResponse>>> SearchLines(
        Guid companyId,
        [FromQuery] Guid? salaryClassId,
        [FromQuery] string? salaryScale,
        [FromQuery] bool? isActive,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = SalaryTabulatorValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchSalaryTabulatorLinesQuery(
                companyId,
                salaryClassId,
                salaryScale,
                isActive,
                search,
                page,
                pageSize,
                includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/salary-tabulator/lines/{id:guid}")]
    [ProducesResponseType<SalaryTabulatorLineResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SalaryTabulatorLineResponse>> GetLineById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetSalaryTabulatorLineByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/companies/{companyId:guid}/salary-tabulator/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> ExportLines(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] Guid? salaryClassId = null,
        [FromQuery] string? salaryScale = null,
        [FromQuery] bool? isActive = null,
        [FromQuery(Name = "q")] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportSalaryTabulatorLinesQuery(
                companyId,
                salaryClassId,
                salaryScale,
                isActive,
                search,
                reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<SalaryTabulatorLineExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "salary-tabulator",
            "SalaryTabulator",
            AuditEntityTypes.SalaryTabulatorLine,
            ReportExportResources.SalaryTabulator,
            "Exported salary tabulator lines report.",
            new { salaryClassId, salaryScale, isActive, q = search },
            SalaryTabulatorErrors.ExportFormatInvalid,
            cancellationToken);
    }

    [HttpGet("api/v1/companies/{companyId:guid}/salary-tabulator/change-requests")]
    [ProducesResponseType<PagedResponse<SalaryTabulatorChangeRequestListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<SalaryTabulatorChangeRequestListItemResponse>>> SearchRequests(
        Guid companyId,
        [FromQuery] SalaryTabulatorChangeRequestStatus? status,
        [FromQuery] Guid? requestedBy,
        [FromQuery] DateTime? effectiveFrom,
        [FromQuery] DateTime? effectiveTo,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = SalaryTabulatorValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchSalaryTabulatorChangeRequestsQuery(
                companyId,
                status,
                requestedBy,
                effectiveFrom,
                effectiveTo,
                page,
                pageSize,
                includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/salary-tabulator/change-requests/{id:guid}")]
    [ProducesResponseType<SalaryTabulatorChangeRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SalaryTabulatorChangeRequestResponse>> GetRequestById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetSalaryTabulatorChangeRequestByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/salary-tabulator/change-requests/{id:guid}/impact")]
    [ProducesResponseType<SalaryTabulatorChangeRequestImpactResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SalaryTabulatorChangeRequestImpactResponse>> GetRequestImpact(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetSalaryTabulatorChangeRequestImpactQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost("api/v1/companies/{companyId:guid}/salary-tabulator/change-requests")]
    [ProducesResponseType<SalaryTabulatorChangeRequestResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [SwaggerOperation(
        Summary = "Create a salary-tabulator change request",
        Description = """
            Creates a new change request in `Draft` status for the company's salary tabulator. Returns
            `201` with the created request; the current `concurrencyToken` is included in the body and
            the `ETag` header for use in a subsequent update or lifecycle action.
            """)]
    public async Task<ActionResult<SalaryTabulatorChangeRequestResponse>> CreateRequest(
        Guid companyId,
        [FromBody] CreateSalaryTabulatorChangeRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateSalaryTabulatorChangeRequestCommand(
                companyId,
                request.EffectiveFromUtc,
                request.EffectiveToUtc,
                MapCreateItems(request)),
            cancellationToken);

        // The PublicContractRouteConvention rewrites the GetRequestById route token `{id}` to
        // `{publicId}`, so the Location route value MUST be keyed `publicId` (not `id`).
        return this.ToCreatedAtActionResult(
            result,
            nameof(GetRequestById),
            value => new { publicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPut("api/v1/salary-tabulator/change-requests/{id:guid}")]
    [ProducesResponseType<SalaryTabulatorChangeRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [SwaggerOperation(
        Summary = "Update a draft change request",
        Description = """
            Replaces the editable fields (reason, effective range and items) of a `Draft` change
            request. Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`,
            stale → `409`). The refreshed token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<SalaryTabulatorChangeRequestResponse>> UpdateRequest(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateSalaryTabulatorChangeRequestRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateSalaryTabulatorChangeRequestCommand(
                id,
                request.Reason,
                request.EffectiveFromUtc,
                request.EffectiveToUtc,
                MapItems(request.Items),
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/salary-tabulator/change-requests/{id:guid}/submit")]
    [ProducesResponseType<SalaryTabulatorChangeRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [SwaggerOperation(
        Summary = "Submit a draft change request for approval",
        Description = """
            Transitions a `Draft` change request to `Submitted`. Requires the current `concurrencyToken`
            in the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in
            the body and the `ETag` header.
            """)]
    public async Task<ActionResult<SalaryTabulatorChangeRequestResponse>> SubmitRequest(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new SubmitSalaryTabulatorChangeRequestCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/salary-tabulator/change-requests/{id:guid}/approve")]
    [ProducesResponseType<SalaryTabulatorChangeRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [SwaggerOperation(
        Summary = "Approve a submitted change request",
        Description = """
            Transitions a `Submitted` change request to `Approved` and applies its items to the salary
            tabulator lines. The decision comment travels in the body. Requires the current
            `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The refreshed
            token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<SalaryTabulatorChangeRequestResponse>> ApproveRequest(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] ApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ApproveSalaryTabulatorChangeRequestCommand(id, request.DecisionComment, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/salary-tabulator/change-requests/{id:guid}/reject")]
    [ProducesResponseType<SalaryTabulatorChangeRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    [SwaggerOperation(
        Summary = "Reject a submitted change request",
        Description = """
            Transitions a `Submitted` change request to `Rejected` without applying its items. The
            decision comment travels in the body. Requires the current `concurrencyToken` in the
            `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in the
            body and the `ETag` header.
            """)]
    public async Task<ActionResult<SalaryTabulatorChangeRequestResponse>> RejectRequest(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] ApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new RejectSalaryTabulatorChangeRequestCommand(id, request.DecisionComment, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("api/v1/salary-tabulator/change-requests/{id:guid}/cancel")]
    [ProducesResponseType<SalaryTabulatorChangeRequestResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [SwaggerOperation(
        Summary = "Cancel a draft change request",
        Description = """
            Cancels a `Draft` change request. Requires the current `concurrencyToken` in the `If-Match`
            header (missing → `400`, stale → `409`). The refreshed token is returned in the body and the
            `ETag` header.
            """)]
    public async Task<ActionResult<SalaryTabulatorChangeRequestResponse>> CancelRequest(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CancelSalaryTabulatorChangeRequestCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    private static IReadOnlyCollection<SalaryTabulatorChangeRequestItemInput> MapCreateItems(CreateSalaryTabulatorChangeRequestRequest request)
        => MapItems(request.Items ?? []);

    private static IReadOnlyCollection<SalaryTabulatorChangeRequestItemInput> MapItems(IReadOnlyCollection<SalaryTabulatorChangeRequestItemRequest> items) =>
        items.Select(item => new SalaryTabulatorChangeRequestItemInput(
                item.SalaryClassPublicId,
                item.SalaryScaleCode,
                item.CurrencyCode,
                item.ChangeType,
                item.ProposedBaseAmount,
                item.ProposedMinAmount,
                item.ProposedMaxAmount,
                item.Notes))
            .ToArray();

    public sealed class CreateSalaryTabulatorChangeRequestRequest
    {
        public DateTime EffectiveFromUtc { get; init; }
        public DateTime? EffectiveToUtc { get; init; }
        public IReadOnlyCollection<SalaryTabulatorChangeRequestItemRequest> Items { get; init; } = [];
    }

    public sealed record UpdateSalaryTabulatorChangeRequestRequest(
        string Reason,
        DateTime EffectiveFromUtc,
        DateTime? EffectiveToUtc,
        IReadOnlyCollection<SalaryTabulatorChangeRequestItemRequest> Items);

    public sealed record SalaryTabulatorChangeRequestItemRequest(
        Guid SalaryClassPublicId,
        string SalaryScaleCode,
        string CurrencyCode,
        SalaryTabulatorChangeType ChangeType,
        decimal? ProposedBaseAmount,
        decimal? ProposedMinAmount,
        decimal? ProposedMaxAmount,
        string? Notes);

    public sealed record ApprovalRequest(string DecisionComment);
}

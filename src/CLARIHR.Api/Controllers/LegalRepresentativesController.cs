using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.LegalRepresentatives;
using CLARIHR.Application.Features.LegalRepresentatives.Common;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Domain.LegalRepresentatives;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
public sealed class LegalRepresentativesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    ReportExportDeliveryService reportExportDeliveryService) : ControllerBase
{
    [HttpGet("api/v1/companies/{companyId:guid}/legal-representatives")]
    [ProducesResponseType<PagedResponse<LegalRepresentativeListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResponse<LegalRepresentativeListItemResponse>>> Search(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery] bool? isPrimary,
        [FromQuery] LegalRepresentativeRepresentationType? representationType,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = LegalRepresentativeValidationRules.DefaultPageSize,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchLegalRepresentativesQuery(
                companyId,
                isActive,
                isPrimary,
                representationType,
                search,
                page,
                pageSize,
                includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/legal-representatives/{id:guid}")]
    [ProducesResponseType<LegalRepresentativeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LegalRepresentativeResponse>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetLegalRepresentativeByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/legal-representatives/{id:guid}/usage")]
    [ProducesResponseType<LegalRepresentativeUsageResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LegalRepresentativeUsageResponse>> Usage(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetLegalRepresentativeUsageQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("api/v1/companies/{companyId:guid}/legal-representatives/export")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    public async Task<IActionResult> Export(
        Guid companyId,
        [FromQuery] string format = "xlsx",
        [FromQuery] bool? isActive = null,
        [FromQuery] bool? isPrimary = null,
        [FromQuery] LegalRepresentativeRepresentationType? representationType = null,
        [FromQuery(Name = "q")] string? search = null,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new ExportLegalRepresentativesQuery(
                companyId,
                isActive,
                isPrimary,
                representationType,
                search,
                reportExportDeliveryService.SynchronousReadLimit),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToActionResult(Result<IReadOnlyCollection<LegalRepresentativeExportRow>>.Failure(result.Error)).Result!;
        }

        return await reportExportDeliveryService.CreateFileResultAsync(
            this,
            result.Value,
            format,
            "legal-representatives",
            "LegalRepresentatives",
            AuditEntityTypes.LegalRepresentative,
            ReportExportResources.LegalRepresentatives,
            "Exported legal representatives report.",
            new { isActive, isPrimary, representationType, q = search },
            LegalRepresentativeErrors.ExportFormatInvalid,
            cancellationToken);
    }

    [HttpPost("api/v1/companies/{companyId:guid}/legal-representatives")]
    [ProducesResponseType<LegalRepresentativeResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<LegalRepresentativeResponse>> Create(
        Guid companyId,
        [FromBody] CreateLegalRepresentativeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateLegalRepresentativeCommand(
                companyId,
                request.FirstName,
                request.LastName,
                request.DocumentType,
                request.DocumentNumber,
                request.PositionTitle,
                request.RepresentationType,
                request.AuthorityDescription,
                request.AppointmentInstrument,
                request.AppointmentDateUtc,
                request.EffectiveFromUtc,
                request.EffectiveToUtc,
                request.Email,
                request.Phone,
                request.IsPrimary),
            cancellationToken);

        return result.IsFailure
            ? this.ToActionResult(Result<LegalRepresentativeResponse>.Failure(result.Error))
            : StatusCode(StatusCodes.Status201Created, result.Value);
    }

    [HttpPut("api/v1/legal-representatives/{id:guid}")]
    [ProducesResponseType<LegalRepresentativeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<LegalRepresentativeResponse>> Update(
        Guid id,
        [FromBody] UpdateLegalRepresentativeRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateLegalRepresentativeCommand(
                id,
                request.FirstName,
                request.LastName,
                request.DocumentType,
                request.DocumentNumber,
                request.PositionTitle,
                request.RepresentationType,
                request.AuthorityDescription,
                request.AppointmentInstrument,
                request.AppointmentDateUtc,
                request.EffectiveFromUtc,
                request.EffectiveToUtc,
                request.Email,
                request.Phone,
                request.IsPrimary,
                request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/legal-representatives/{id:guid}/activate")]
    [ProducesResponseType<LegalRepresentativeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LegalRepresentativeResponse>> Activate(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateLegalRepresentativeCommand(id, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/legal-representatives/{id:guid}/inactivate")]
    [ProducesResponseType<LegalRepresentativeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<LegalRepresentativeResponse>> Inactivate(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateLegalRepresentativeCommand(id, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPatch("api/v1/legal-representatives/{id:guid}/set-primary")]
    [ProducesResponseType<LegalRepresentativeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<LegalRepresentativeResponse>> SetPrimary(
        Guid id,
        [FromBody] ConcurrencyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new SetPrimaryLegalRepresentativeCommand(id, request.ConcurrencyToken),
            cancellationToken);

        return this.ToActionResult(result);
    }

    public sealed record CreateLegalRepresentativeRequest(
        string FirstName,
        string LastName,
        string DocumentType,
        string DocumentNumber,
        string PositionTitle,
        LegalRepresentativeRepresentationType RepresentationType,
        string? AuthorityDescription,
        string? AppointmentInstrument,
        DateTime? AppointmentDateUtc,
        DateTime EffectiveFromUtc,
        DateTime? EffectiveToUtc,
        string? Email,
        string? Phone,
        bool IsPrimary = false);

    public sealed record UpdateLegalRepresentativeRequest(
        string FirstName,
        string LastName,
        string DocumentType,
        string DocumentNumber,
        string PositionTitle,
        LegalRepresentativeRepresentationType RepresentationType,
        string? AuthorityDescription,
        string? AppointmentInstrument,
        DateTime? AppointmentDateUtc,
        DateTime EffectiveFromUtc,
        DateTime? EffectiveToUtc,
        string? Email,
        string? Phone,
        bool IsPrimary,
        Guid ConcurrencyToken);

    public sealed record ConcurrencyRequest(Guid ConcurrencyToken);
}

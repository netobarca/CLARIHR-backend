using System.ComponentModel.DataAnnotations;
using Asp.Versioning;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.LegalRepresentatives;
using CLARIHR.Application.Features.LegalRepresentatives.Common;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Domain.LegalRepresentatives;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}")]
[Tags("Legal Representatives")]
[AuthorizationPolicySet(LegalRepresentativePolicies.Read, LegalRepresentativePolicies.Manage)]
public sealed class LegalRepresentativesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher,
    ReportExportDeliveryService reportExportDeliveryService) : ControllerBase
{
    [HttpGet("companies/{companyId:guid}/legal-representatives")]
    [EnableRateLimiting(LegalRepresentativeRateLimitPolicies.Search)]
    [ProducesResponseType<PagedResponse<LegalRepresentativeListItemResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "List legal representatives for a company",
        Description = """
            Returns a paginated list of legal representatives for the company, filterable by
            `isActive`, `isPrimary`, `representationType` and free-text `q`. The owning company is
            validated against the authenticated tenant. Set `includeAllowedActions=true` to receive
            per-item read/manage flags.
            """)]
    public async Task<ActionResult<PagedResponse<LegalRepresentativeListItemResponse>>> Search(
        Guid companyId,
        [FromQuery] bool? isActive,
        [FromQuery] bool? isPrimary,
        [FromQuery] LegalRepresentativeRepresentationType? representationType,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int page = 1,
        [FromQuery, Range(1, LegalRepresentativeValidationRules.MaxPageSize)] int pageSize = LegalRepresentativeValidationRules.DefaultPageSize,
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

    [HttpGet("legal-representatives/{id:guid}")]
    [ProducesResponseType<LegalRepresentativeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a legal representative by id",
        Description = """
            Returns a single legal representative by its public id. The owning company is resolved
            from the authenticated tenant; a representative belonging to another tenant yields
            `404`. The current `concurrencyToken` is emitted as the `ETag` header on mutations.
            """)]
    public async Task<ActionResult<LegalRepresentativeResponse>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetLegalRepresentativeByIdQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("legal-representatives/{id:guid}/usage")]
    [ProducesResponseType<LegalRepresentativeUsageResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get a legal representative's usage",
        Description = """
            Returns the active reference counts for the representative and whether it can be
            inactivated (the company must keep at least one active representative).
            """)]
    public async Task<ActionResult<LegalRepresentativeUsageResponse>> Usage(Guid id, CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetLegalRepresentativeUsageQuery(id), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("companies/{companyId:guid}/legal-representatives/export")]
    [EnableRateLimiting(LegalRepresentativeRateLimitPolicies.Export)]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    [ProducesStandardErrors(StandardErrorSet.Query)]
    [SwaggerOperation(
        Summary = "Export legal representatives as a report",
        Description = """
            Exports the filtered legal representatives as a downloadable report in the requested
            `format` (e.g. `xlsx`; an unknown format yields `400`). The same filters as the list
            endpoint apply. The export is bounded by the synchronous read limit and audited.
            """)]
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

    [HttpPost("companies/{companyId:guid}/legal-representatives")]
    [ProducesResponseType<LegalRepresentativeResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Create a legal representative",
        Description = """
            Creates a legal representative under the company and returns `201 Created` with the
            `Location` header pointing to the new resource and the `ETag` header carrying its
            initial `concurrencyToken`. A duplicate document yields `409`; an invalid effective
            date range yields `422`. Creating one as primary demotes the current primary.
            """)]
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

        // The PublicContractRouteConvention rewrites the GetById route token `{id}` to
        // `{publicId}`, so the Location route value MUST be keyed `publicId` (not `id`).
        return this.ToCreatedAtActionResult(
            result,
            nameof(GetById),
            value => new { publicId = value.Id },
            value => value.ConcurrencyToken);
    }

    [HttpPut("legal-representatives/{id:guid}")]
    [ProducesResponseType<LegalRepresentativeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Update a legal representative",
        Description = """
            Replaces the editable fields of a legal representative (identity, position, authority,
            appointment, effective dates, contact, primary flag). Requires the current
            `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). A
            duplicate document yields `409`; an invalid date range or primary-on-inactive yields
            `422`. The refreshed token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<LegalRepresentativeResponse>> Update(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
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
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("legal-representatives/{id:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<LegalRepresentativeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Patch a legal representative",
        Description = """
            Applies a partial update using JSON Patch (RFC 6902), media type
            `application/json-patch+json`. Patchable descriptive/contact paths: `/firstName`,
            `/lastName`, `/positionTitle`, `/representationType`, `/authorityDescription`,
            `/appointmentInstrument`, `/appointmentDateUtc`, `/email`, `/phone`. The legal identity
            (`/documentType`, `/documentNumber`) and the `/effectiveFromUtc`-`/effectiveToUtc` range
            are validated as units via PUT; the `/isPrimary` flag is changed via `/set-primary`; and
            activation via `/activate` and `/inactivate`. Requires the current `concurrencyToken` in
            the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned
            in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<LegalRepresentativeResponse>> Patch(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchLegalRepresentativeRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchLegalRepresentativeCommand(
                id,
                concurrencyToken,
                JsonPatchOperationMapper.Map(
                    patchDoc,
                    static (op, path, from, value) => new LegalRepresentativePatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("legal-representatives/{id:guid}/activate")]
    [ProducesResponseType<LegalRepresentativeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Activate a legal representative",
        Description = """
            Reactivates an inactive legal representative. Requires the current `concurrencyToken` in
            the `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned
            in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<LegalRepresentativeResponse>> Activate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ActivateLegalRepresentativeCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("legal-representatives/{id:guid}/inactivate")]
    [ProducesResponseType<LegalRepresentativeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Inactivate a legal representative",
        Description = """
            Deactivates (soft-delete) a legal representative. Fails with `409` if it is the last
            active representative (the company must keep at least one active). Requires the current
            `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The
            refreshed token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<LegalRepresentativeResponse>> Inactivate(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new InactivateLegalRepresentativeCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("legal-representatives/{id:guid}/set-primary")]
    [ProducesResponseType<LegalRepresentativeResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Command)]
    [SwaggerOperation(
        Summary = "Set a legal representative as primary",
        Description = """
            Designates the representative as the company's primary, demoting the current primary.
            Only an active representative can be made primary (`422` otherwise). Requires the
            current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`).
            The refreshed token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<LegalRepresentativeResponse>> SetPrimary(
        Guid id,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new SetPrimaryLegalRepresentativeCommand(id, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
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
        bool IsPrimary);

    public sealed class PatchLegalRepresentativeRequest
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string PositionTitle { get; set; } = string.Empty;
        public LegalRepresentativeRepresentationType RepresentationType { get; set; }
        public string? AuthorityDescription { get; set; }
        public string? AppointmentInstrument { get; set; }
        public DateTime? AppointmentDateUtc { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
    }
}

using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.AccountCompanies;
using CLARIHR.Application.Features.LegalRepresentatives;
using CLARIHR.Application.Features.LegalRepresentatives.Common;
using CLARIHR.Application.Features.Locations.Countries;
using CLARIHR.Application.Features.OrgStructureCatalogs;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.LegalRepresentatives;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

// Authorization is bespoke per-resource ownership (the company's CreatedByUserPublicId must match the
// JWT subject), enforced in the handlers via AccountCompanyActorResolver — NOT RBAC. This family is
// intentionally excluded from [AuthorizationPolicySet]/GovernedFamilyRegex (like PersonnelFileReporting):
// there is no permission/policy to declare, so a declarative attribute would be misleading. The literal
// route (`api/account/companies` + `{companyPublicId}` + `/switch`) is locked by
// PublicContractGuardrailsIntegrationTests and must not be versioned or renamed.
[ApiController]
[Authorize]
[Route("api/account/companies")]
[Tags("Account Companies")]
public sealed class AccountCompaniesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<AccountCompanySummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResponse<AccountCompanySummaryResponse>>> List(
        [FromQuery] CompanyStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool includeAllowedActions = false,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new GetOwnedCompaniesQuery(status, page, pageSize, includeAllowedActions),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpGet("{companyPublicId:guid}")]
    [ProducesResponseType<AccountCompanyDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountCompanyDetailResponse>> GetById(
        Guid companyPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetOwnedCompanyByIdQuery(companyPublicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("{companyPublicId:guid}/access-context")]
    [ProducesResponseType<AccountCompanyAccessContextResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountCompanyAccessContextResponse>> GetAccessContext(
        Guid companyPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetOwnedCompanyAccessContextQuery(companyPublicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("{companyPublicId:guid}/authorization/role-builder-catalog")]
    [ProducesResponseType<AccountCompanyRoleBuilderCatalogResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountCompanyRoleBuilderCatalogResponse>> GetRoleBuilderCatalog(
        Guid companyPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetOwnedCompanyRoleBuilderCatalogQuery(companyPublicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("{companyPublicId:guid}/authorization/resource-policies/{resourceKey}")]
    [ProducesResponseType<AccountCompanyResourcePolicyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountCompanyResourcePolicyResponse>> GetResourcePolicy(
        Guid companyPublicId,
        string resourceKey,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetOwnedCompanyResourcePolicyQuery(companyPublicId, resourceKey), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("countries")]
    [ProducesResponseType<IReadOnlyCollection<CountryCatalogItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyCollection<CountryCatalogItemResponse>>> GetCountries(
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetCountryCatalogItemsQuery(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("company-types")]
    [ProducesResponseType<IReadOnlyCollection<CompanyTypeCatalogItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyCollection<CompanyTypeCatalogItemResponse>>> GetCompanyTypes(
        [FromQuery] string countryCode,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetAvailableCompanyTypesQuery(countryCode), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("legal-representative-position-titles")]
    [ProducesResponseType<IReadOnlyCollection<LegalRepresentativePositionTitleCatalogItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyCollection<LegalRepresentativePositionTitleCatalogItemResponse>>> GetLegalRepresentativePositionTitles(
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetLegalRepresentativePositionTitlesQuery(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("legal-representative-representation-types")]
    [ProducesResponseType<IReadOnlyCollection<LegalRepresentativeRepresentationTypeCatalogItemResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyCollection<LegalRepresentativeRepresentationTypeCatalogItemResponse>>> GetLegalRepresentativeRepresentationTypes(
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetLegalRepresentativeRepresentationTypesQuery(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType<AccountCompanyDetailResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [SwaggerOperation(
        Summary = "Create a company",
        Description = """
            Provisions a new company owned by the authenticated user (with its initial legal
            representative). Returns `201` with the created company; the current `concurrencyToken`
            is included in the body and the `ETag` header for use in a subsequent update.
            """)]
    public async Task<ActionResult<AccountCompanyDetailResponse>> Create(
        [FromBody] CreateAccountCompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateAccountCompanyCommand(
                request.Name,
                request.CountryCode,
                request.CompanyTypePublicId,
                new InitialLegalRepresentativeInput(
                    request.InitialLegalRepresentative.FirstName,
                    request.InitialLegalRepresentative.LastName,
                    request.InitialLegalRepresentative.DocumentType,
                    request.InitialLegalRepresentative.DocumentNumber,
                    request.InitialLegalRepresentative.PositionTitle,
                    request.InitialLegalRepresentative.RepresentationType,
                    request.InitialLegalRepresentative.AuthorityDescription,
                    request.InitialLegalRepresentative.AppointmentInstrument,
                    request.InitialLegalRepresentative.AppointmentDateUtc,
                    request.InitialLegalRepresentative.EffectiveFromUtc,
                    request.InitialLegalRepresentative.EffectiveToUtc,
                    request.InitialLegalRepresentative.Email,
                    request.InitialLegalRepresentative.Phone,
                    request.InitialLegalRepresentative.IsPrimary)),
            cancellationToken);

        return this.ToCreatedAtActionResult(
            result,
            nameof(GetById),
            value => new { companyPublicId = value.PublicId },
            value => value.ConcurrencyToken);
    }

    [HttpPut("{companyPublicId:guid}")]
    [ProducesResponseType<AccountCompanyDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [SwaggerOperation(
        Summary = "Update a company",
        Description = """
            Replaces the editable fields (name and company type). Requires the current
            `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The
            refreshed token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<AccountCompanyDetailResponse>> Update(
        Guid companyPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateAccountCompanyRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateAccountCompanyCommand(companyPublicId, request.Name, request.CompanyTypePublicId, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("{companyPublicId:guid}")]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<AccountCompanyDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [SwaggerOperation(
        Summary = "Patch a company (RFC 6902 JSON Patch)",
        Description = """
            Applies a partial update using JSON Patch (RFC 6902), media type
            `application/json-patch+json`. Patchable paths: `/name` (max 150 characters, required,
            cannot be removed) and `/companyTypePublicId` (a company-type publicId, or `null`/remove
            to clear it). Status transitions use the dedicated `/archive` and `/reactivate` actions,
            not this patch. Requires the current `concurrencyToken` in the `If-Match` header
            (missing → `400`, stale → `409`). The refreshed token is returned in the body and the
            `ETag` header.
            """)]
    public async Task<ActionResult<AccountCompanyDetailResponse>> Patch(
        Guid companyPublicId,
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchAccountCompanyRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchAccountCompanyCommand(
                companyPublicId,
                concurrencyToken,
                JsonPatchOperationMapper.Map(
                    patchDoc,
                    static (op, path, from, value) => new AccountCompanyPatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("{companyPublicId:guid}/archive")]
    [ProducesResponseType<AccountCompanyDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [SwaggerOperation(
        Summary = "Archive a company",
        Description = """
            Archives an owned company (cannot be the active or primary company). Requires the current
            `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`). The
            refreshed token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<AccountCompanyDetailResponse>> Archive(
        Guid companyPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(new ArchiveAccountCompanyCommand(companyPublicId, concurrencyToken), cancellationToken);
        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch("{companyPublicId:guid}/reactivate")]
    [ProducesResponseType<AccountCompanyDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [SwaggerOperation(
        Summary = "Reactivate a company",
        Description = """
            Reactivates an archived company (subject to the active-company capacity limit). Requires
            the current `concurrencyToken` in the `If-Match` header (missing → `400`, stale → `409`).
            The refreshed token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<AccountCompanyDetailResponse>> Reactivate(
        Guid companyPublicId,
        [FromIfMatch] Guid concurrencyToken,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(new ReactivateAccountCompanyCommand(companyPublicId, concurrencyToken), cancellationToken);
        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPost("{companyPublicId:guid}/switch")]
    [ProducesResponseType<SwitchActiveCompanyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SwitchActiveCompanyResponse>> Switch(
        Guid companyPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(new SwitchActiveCompanyCommand(companyPublicId), cancellationToken);
        return this.ToActionResult(result);
    }

    public sealed record CreateAccountCompanyRequest(
        string Name,
        string CountryCode,
        Guid? CompanyTypePublicId,
        InitialLegalRepresentativeRequest InitialLegalRepresentative);

    public sealed record InitialLegalRepresentativeRequest(
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
        bool? IsPrimary = null);

    public sealed record UpdateAccountCompanyRequest(string Name, Guid? CompanyTypePublicId);

    public sealed class PatchAccountCompanyRequest
    {
        public string Name { get; set; } = string.Empty;

        public Guid? CompanyTypePublicId { get; set; }
    }
}

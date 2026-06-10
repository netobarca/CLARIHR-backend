using System.ComponentModel.DataAnnotations;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.AccountCompanies;
using CLARIHR.Application.Features.AccountCompanies.Common;
using CLARIHR.Application.Features.LegalRepresentatives.Common;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.LegalRepresentatives;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

// Authorization is bespoke per-resource ownership (the company's CreatedByUserPublicId must match the
// JWT subject), enforced in the handlers via AccountCompanyActorResolver — NOT RBAC. This family is
// intentionally excluded from [AuthorizationPolicySet]/GovernedFamilyRegex (like PersonnelFileReporting):
// there is no permission/policy to declare, so a declarative attribute would be misleading. The route is
// canonically versioned under `api/v1/account/companies` (+ `{companyPublicId}` + `/switch`), pinned by
// PublicContractGuardrailsIntegrationTests; the whole `api/account/*` family migrated to `api/v1` together.
[ApiController]
[Authorize]
[Route("api/v1/account/companies")]
[Tags("Account Companies")]
public sealed class AccountCompaniesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<PagedResponse<AccountCompanySummaryResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.BadRequest | StandardErrorSet.Unauthorized)]
    [SwaggerOperation(
        Summary = "List the caller's companies",
        Description = """
            Returns the paged set of companies owned by the authenticated user (optionally filtered by
            `status`). Set `includeAllowedActions=true` to enrich each row with the caller's allowed
            actions (edit/archive/reactivate) for client affordances. This is the only endpoint that
            returns the full company collection.
            """)]
    public async Task<ActionResult<PagedResponse<AccountCompanySummaryResponse>>> List(
        [FromQuery] CompanyStatus? status,
        [FromQuery] int page = 1,
        [FromQuery][Range(1, AccountCompanyValidationRules.MaxPageSize)] int pageSize = 20,
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
    [ProducesStandardErrors(StandardErrorSet.Read)]
    [SwaggerOperation(
        Summary = "Get an owned company",
        Description = """
            Returns a single owned company with its detail (active legal representatives and the company
            type). The current `concurrencyToken` is included in the body and the `ETag` header for use in
            the `If-Match` header of a subsequent update/patch. Requires ownership: a company owned by
            another user yields `403`, an unknown id `404`.
            """)]
    public async Task<ActionResult<AccountCompanyDetailResponse>> GetById(
        Guid companyPublicId,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetOwnedCompanyByIdQuery(companyPublicId), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPost]
    [ProducesResponseType<AccountCompanyDetailResponse>(StatusCodes.Status201Created)]
    [ProducesStandardErrors(StandardErrorSet.BadRequest | StandardErrorSet.Unauthorized | StandardErrorSet.Conflict)]
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
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
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
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
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
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
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
    [ProducesStandardErrors(StandardErrorSet.SubResourceWrite)]
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
    [EnableRateLimiting(AccountCompanyRateLimitPolicies.Switch)]
    [ProducesResponseType<SwitchActiveCompanyResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Read | StandardErrorSet.Conflict)]
    [SwaggerOperation(
        Summary = "Switch the active company",
        Description = """
            Sets the given owned company as the caller's active company and re-issues the session: returns a
            fresh access token (and refresh token), the active-company summary, and the full access context.
            The company must be active and the caller must have an active membership, otherwise `403`. This
            action mutates membership and re-issues the JWT, so it does not take an `If-Match` token.
            """)]
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

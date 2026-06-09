using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.InternalCatalogs;
using CLARIHR.Application.Features.InternalCatalogs.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

// Job Profiles' "Internal" catalog family: the user-generated, free-text autocomplete-with-create
// dictionary that backs the Education / Knowledge / Certification requirement descriptions (see
// JobProfileCatalogBindingMap, family "Internal"). Unlike its sibling families (PositionDescription /
// JobCatalog, which are per-company curated catalogs under /companies/{companyId}/...), this catalog is
// PLATFORM-GLOBAL and cross-tenant (InternalCatalogValue : AuditableEntity, no TenantId; writes are
// platform-audited), so it has no {companyId} segment and is authn-only by design — [AuthorizationPolicySet]
// would be the wrong (tenant-scoped) gate, so it stays out of the authz GovernedFamilyRegex (mirror
// JobProfileCompetencyMatrix). Tagged "Job Profiles" + enrolled in the OpenAPI guardrail.
[ApiController]
[Authorize]
[Route("api/v1/job-profiles/internal-catalogs")]
[Tags("Job Profiles")]
public sealed class JobProfileInternalCatalogsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher)
    : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<InternalCatalogDefinitionResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.BadRequest | StandardErrorSet.Unauthorized | StandardErrorSet.NotFound)]
    [SwaggerOperation(
        Summary = "List internal catalog definitions for a context",
        Description = """
            Returns the internal-catalog field definitions for the given `context` (e.g.
            `job-profile.requirements`): for each field, its render type (search / select / free-text),
            the backing `catalogKey`, whether new values can be created, and the minimum query length.
            The frontend uses this manifest to drive the requirement autocomplete inputs. An unknown
            context yields `404`. These catalogs are platform-global (shared across companies).
            """)]
    public async Task<ActionResult<IReadOnlyCollection<InternalCatalogDefinitionResponse>>> GetDefinitions(
        [FromQuery] string context,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetInternalCatalogDefinitionsQuery(context), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("{catalogKey}/values")]
    [ProducesResponseType<IReadOnlyCollection<InternalCatalogValueSuggestionResponse>>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.BadRequest | StandardErrorSet.Unauthorized | StandardErrorSet.NotFound)]
    [SwaggerOperation(
        Summary = "Search internal catalog value suggestions",
        Description = """
            Returns up to `limit` (1–20, default 10) value suggestions for the catalog identified by
            `catalogKey`, fuzzy-matched against the free-text query `q` and ranked by similarity score.
            Below the catalog's minimum query length the result is empty. An unsupported or
            non-searchable `catalogKey` yields `404`. The suggestion pool is platform-global
            (crowd-sourced across companies).
            """)]
    public async Task<ActionResult<IReadOnlyCollection<InternalCatalogValueSuggestionResponse>>> SearchValues(
        string catalogKey,
        [FromQuery(Name = "q")] string? search,
        [FromQuery] int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(
            new SearchInternalCatalogValuesQuery(catalogKey, search, limit),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPost("{catalogKey}/values")]
    [ProducesResponseType<InternalCatalogValueSuggestionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<InternalCatalogValueSuggestionResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.BadRequest | StandardErrorSet.Unauthorized | StandardErrorSet.NotFound | StandardErrorSet.Conflict)]
    [SwaggerOperation(
        Summary = "Create an internal catalog value",
        Description = """
            Adds a free-text value to the catalog identified by `catalogKey` (only catalogs that allow
            creation). Returns `201` with the new value, or `200` reusing an existing exact match. If the
            value is too similar to existing ones it is rejected with `409` and the response carries the
            closest `suggestions`. The value joins the platform-global pool and its popularity is tracked
            by usage.
            """)]
    public async Task<ActionResult<InternalCatalogValueSuggestionResponse>> CreateValue(
        string catalogKey,
        [FromBody] CreateInternalCatalogValueRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new CreateInternalCatalogValueCommand(catalogKey, request.Value),
            cancellationToken);
        if (result.IsFailure)
        {
            return new ActionResult<InternalCatalogValueSuggestionResponse>(
                CLARIHR.Api.Common.ProblemDetailsFactory.Create(HttpContext, result.Error));
        }

        if (result.Value.Outcome == InternalCatalogCreateOutcome.RejectedSimilar)
        {
            var problemDetails = CLARIHR.Api.Common.ProblemDetailsFactory.CreateProblemDetails(
                HttpContext,
                InternalCatalogErrors.SimilarValueConflict);
            problemDetails.Extensions["suggestions"] = result.Value.Suggestions;
            return Conflict(problemDetails);
        }

        return result.Value.Outcome == InternalCatalogCreateOutcome.Created
            ? StatusCode(StatusCodes.Status201Created, result.Value.CatalogValue)
            : Ok(result.Value.CatalogValue);
    }

    public sealed record CreateInternalCatalogValueRequest(string Value);
}

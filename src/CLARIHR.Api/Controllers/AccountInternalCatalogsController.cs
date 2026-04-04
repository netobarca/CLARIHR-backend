using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.InternalCatalogs;
using CLARIHR.Application.Features.InternalCatalogs.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/account/internal-catalogs")]
public sealed class AccountInternalCatalogsController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher)
    : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<InternalCatalogDefinitionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyCollection<InternalCatalogDefinitionResponse>>> GetDefinitions(
        [FromQuery] string context,
        CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetInternalCatalogDefinitionsQuery(context), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpGet("{catalogKey}/values")]
    [ProducesResponseType<IReadOnlyCollection<InternalCatalogValueSuggestionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
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
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
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

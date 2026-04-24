using CLARIHR.Api.Common;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Preferences.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/account/me/preferences")]
public sealed class UserPreferencesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<UserPreferenceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserPreferenceResponse>> Get(CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetCurrentUserPreferencesQuery(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut]
    [ProducesResponseType<UserPreferenceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserPreferenceResponse>> Update(
        [FromBody] UpdateUserPreferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateCurrentUserPreferencesCommand(request.Language),
            cancellationToken);

        return this.ToActionResult(result);
    }

    [HttpPut("social-links")]
    [ProducesResponseType<UserPreferenceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserPreferenceResponse>> ReplaceSocialLinks(
        [FromBody] ReplaceUserSocialLinksRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplaceCurrentUserSocialLinksCommand(
                request.Items
                    .Select(static item => new UpdateCurrentUserSocialLinkItem(item.ProviderCode, item.Url))
                    .ToArray()),
            cancellationToken);

        return this.ToActionResult(result);
    }

    public sealed record UpdateUserPreferencesRequest(string Language);

    public sealed record ReplaceUserSocialLinksRequest(IReadOnlyCollection<UserSocialLinkItemRequest> Items);

    public sealed record UserSocialLinkItemRequest(string ProviderCode, string Url);
}

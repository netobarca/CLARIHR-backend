using Asp.Versioning;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Binders;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.JsonPatch;
using CLARIHR.Application.Features.Preferences.User;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.JsonPatch.SystemTextJson;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Authorize]
[Route("api/v{version:apiVersion}/account/me/preferences")]
[Tags("User Preferences")]
public sealed class UserPreferencesController(
    ICommandDispatcher commandDispatcher,
    IQueryDispatcher queryDispatcher) : ControllerBase
{
    // Self-scoped resource (resolved from the JWT). There is no RBAC gate beyond authentication,
    // and the singleton is auto-provisioned on first access, so 403/404 are not reachable; the
    // error sets are scoped accordingly rather than using the broader RBAC presets.
    private const StandardErrorSet WriteErrors =
        StandardErrorSet.BadRequest | StandardErrorSet.Unauthorized | StandardErrorSet.Conflict;

    [HttpGet]
    [ProducesResponseType<UserPreferenceResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(StandardErrorSet.Unauthorized)]
    [SwaggerOperation(
        Summary = "Get the current user's preferences",
        Description = """
            Returns the preferences (language and social links) for the authenticated user. The
            record is auto-provisioned on first access, so it always exists for an authenticated
            caller. The current `concurrencyToken` is included in the body for use in the `If-Match`
            header of a subsequent update.
            """)]
    public async Task<ActionResult<UserPreferenceResponse>> Get(CancellationToken cancellationToken = default)
    {
        var result = await queryDispatcher.SendAsync(new GetCurrentUserPreferencesQuery(), cancellationToken);
        return this.ToActionResult(result);
    }

    [HttpPut]
    [ProducesResponseType<UserPreferenceResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(WriteErrors)]
    [SwaggerOperation(
        Summary = "Replace the current user's language",
        Description = """
            Replaces the user's language preference. Requires the current `concurrencyToken` in the
            `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in
            the body and the `ETag` header.
            """)]
    public async Task<ActionResult<UserPreferenceResponse>> Update(
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] UpdateUserPreferencesRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new UpdateCurrentUserPreferencesCommand(request.Language, concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPut("social-links")]
    [ProducesResponseType<UserPreferenceResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(WriteErrors)]
    [SwaggerOperation(
        Summary = "Replace the current user's social links",
        Description = """
            Replaces the full set of social links (max 10, unique provider codes, absolute https
            URLs). Requires the current `concurrencyToken` in the `If-Match` header (missing → `400`,
            stale → `409`). The refreshed token is returned in the body and the `ETag` header.
            """)]
    public async Task<ActionResult<UserPreferenceResponse>> ReplaceSocialLinks(
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] ReplaceUserSocialLinksRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new ReplaceCurrentUserSocialLinksCommand(
                request.Items
                    .Select(static item => new UpdateCurrentUserSocialLinkItem(item.ProviderCode, item.Url))
                    .ToArray(),
                concurrencyToken),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    [HttpPatch]
    [Consumes("application/json-patch+json")]
    [RequestSizeLimit(JsonPatchHardening.MaxRequestBodySizeBytes)]
    [ProducesResponseType<UserPreferenceResponse>(StatusCodes.Status200OK)]
    [ProducesStandardErrors(WriteErrors)]
    [SwaggerOperation(
        Summary = "Patch the current user's preferences",
        Description = """
            Applies a partial update using JSON Patch (RFC 6902), media type
            `application/json-patch+json`. The only patchable path is `/language` (2–3 letters);
            it is required and cannot be removed. Social links are replaced via
            `PUT /social-links`, not patched here. Requires the current `concurrencyToken` in the
            `If-Match` header (missing → `400`, stale → `409`). The refreshed token is returned in
            the body and the `ETag` header.
            """)]
    public async Task<ActionResult<UserPreferenceResponse>> Patch(
        [FromIfMatch] Guid concurrencyToken,
        [FromBody] JsonPatchDocument<PatchUserPreferencesRequest> patchDoc,
        CancellationToken cancellationToken = default)
    {
        var result = await commandDispatcher.SendAsync(
            new PatchCurrentUserPreferencesCommand(
                concurrencyToken,
                JsonPatchOperationMapper.Map(
                    patchDoc,
                    static (op, path, from, value) => new UserPreferencePatchOperation(op, path, from, value))),
            cancellationToken);

        return this.ToActionResultWithETag(result, value => value.ConcurrencyToken);
    }

    public sealed record UpdateUserPreferencesRequest(string Language);

    public sealed record ReplaceUserSocialLinksRequest(IReadOnlyCollection<UserSocialLinkItemRequest> Items);

    public sealed record UserSocialLinkItemRequest(string ProviderCode, string Url);

    public sealed class PatchUserPreferencesRequest
    {
        public string Language { get; set; } = string.Empty;
    }
}

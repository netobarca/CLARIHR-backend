using System.Security.Claims;
using CLARIHR.Application.Abstractions.IdentityAccess;
using Microsoft.AspNetCore.Authentication;

namespace CLARIHR.Api.Common.Authorization;

/// <summary>
/// Attaches the caller's effective roles and permissions to the principal on every authenticated request,
/// resolved from the current database state rather than read out of the access token.
///
/// This is what keeps authorization current. The token says who you are and which tenant you are in; this
/// says what you may do, as of this request. Revoking a role or deactivating a user therefore takes effect
/// immediately instead of when the token expires.
///
/// It runs after authentication and before authorization, so everything downstream — the RBAC service,
/// <see cref="PermissionClaimEvaluator"/>, policy handlers — keeps reading permissions off the principal
/// exactly as before and needs no change.
/// </summary>
internal sealed class EffectiveAccessClaimsTransformation(IEffectiveAccessResolver resolver) : IClaimsTransformation
{
    // ASP.NET Core may invoke claims transformation more than once for the same request. Without a marker
    // the permission claims would be appended again on each pass.
    private const string AppliedMarkerClaimType = "effective_access_applied";

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            return principal;
        }

        if (principal.HasClaim(static claim => claim.Type == AppliedMarkerClaimType))
        {
            return principal;
        }

        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub"), out var userPublicId))
        {
            return principal;
        }

        if (!Guid.TryParse(principal.FindFirstValue("tid"), out var tenantId))
        {
            // No active tenant on the token (platform clients, or a session before the first company was
            // provisioned). There is no tenant to resolve permissions in, so the caller simply has none.
            return principal;
        }

        var access = await resolver.ResolveAsync(userPublicId, tenantId, CancellationToken.None);

        var identity = new ClaimsIdentity(principal.Identity.AuthenticationType);
        identity.AddClaim(new Claim(AppliedMarkerClaimType, "1"));

        foreach (var role in access.Roles)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        foreach (var permission in access.Permissions)
        {
            identity.AddClaim(new Claim("permission", permission));
        }

        principal.AddIdentity(identity);
        return principal;
    }
}

using System.Security.Claims;
using CLARIHR.Application.Abstractions.Authentication;
using Microsoft.AspNetCore.Http;

namespace CLARIHR.Infrastructure.Authentication;

internal sealed class HttpCurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public bool IsAuthenticated => httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;

    public string? UserId =>
        httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier) ??
        httpContextAccessor.HttpContext?.User.FindFirstValue("sub");

    // Roles and permissions are put on the principal by EffectiveAccessClaimsTransformation, resolved from
    // the database on every request. They are NOT carried in the access token. One claim type each — the
    // duplicate `role` / `permissions` spellings are gone, so there is a single place a value can come from.
    public IReadOnlyCollection<string> Roles =>
        httpContextAccessor.HttpContext?.User.Claims
            .Where(static claim => claim.Type == ClaimTypes.Role)
            .Select(static claim => claim.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ??
        [];

    public IReadOnlyCollection<string> Permissions =>
        httpContextAccessor.HttpContext?.User.Claims
            .Where(static claim => claim.Type == "permission")
            .Select(static claim => claim.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ??
        [];
}

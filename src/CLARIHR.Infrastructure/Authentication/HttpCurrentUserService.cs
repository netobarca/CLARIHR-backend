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

    public IReadOnlyCollection<string> Roles =>
        httpContextAccessor.HttpContext?.User.Claims
            .Where(static claim => claim.Type is ClaimTypes.Role or "role")
            .Select(static claim => claim.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray() ??
        [];

    public IReadOnlyCollection<string> Permissions =>
        httpContextAccessor.HttpContext?.User.Claims
            .Where(static claim => claim.Type is "permission" or "permissions")
            .Select(static claim => claim.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ??
        [];
}

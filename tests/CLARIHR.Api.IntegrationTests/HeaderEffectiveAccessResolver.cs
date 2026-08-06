using CLARIHR.Application.Abstractions.IdentityAccess;
using Microsoft.AspNetCore.Http;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// Test stand-in for <see cref="IEffectiveAccessResolver"/> that reads the roles and permissions a test
/// declared through the X-Test-* headers instead of querying the database.
///
/// Why this exists rather than the handler injecting claims directly: the claims transformation ADDS an
/// identity, it does not replace one. With the handler injecting permissions AND the real resolver reading
/// the seeded IAM rows, a test actor ended up holding the union of both — which silently broke rules that
/// deny for holding too much (the anti-self guards, the Admin exclusion on the Authorize* policies).
///
/// Substituting the resolver keeps every test going through the real pipeline (resolver → claims
/// transformation → principal → RBAC), with the headers as the data source. The real resolver's own query
/// and caching are covered by the dedicated tests that do NOT substitute it.
/// </summary>
internal sealed class HeaderEffectiveAccessResolver(IHttpContextAccessor httpContextAccessor) : IEffectiveAccessResolver
{
    // Nothing is cached here — every call re-reads the current request's headers — so there is no
    // invalidation half to stand in for.
    public Task<EffectiveAccess> ResolveAsync(Guid userPublicId, Guid tenantId, CancellationToken cancellationToken) =>
        Task.FromResult(new EffectiveAccess(
            Read(TestAuthenticationHandler.RolesHeader),
            Read(TestAuthenticationHandler.PermissionsHeader)));

    private string[] Read(string headerName)
    {
        var headers = httpContextAccessor.HttpContext?.Request.Headers;
        if (headers is null || !headers.TryGetValue(headerName, out var values))
        {
            return [];
        }

        return values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(static value => value!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}

using System.Collections.Concurrent;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CLARIHR.Infrastructure.IdentityAccess;

/// <inheritdoc />
internal sealed class EffectiveAccessResolver(
    ApplicationDbContext dbContext,
    IMemoryCache cache) : IEffectiveAccessResolver
{
    // Safety net for multi-instance deployments only: an InvalidateUser/InvalidateTenant raised on one
    // instance never reaches the others, so the TTL bounds how long a stale grant can survive there. On a
    // single instance revocation is immediate. Keep this short — it IS the revocation window in a farm.
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(60);

    // IMemoryCache cannot enumerate or drop keys by prefix, so tenant-wide invalidation is done by bumping a
    // generation counter that participates in the cache key: every previously cached entry for that tenant
    // becomes unreachable at once and is evicted later by its own TTL.
    private static readonly ConcurrentDictionary<Guid, long> TenantGenerations = new();

    public async Task<EffectiveAccess> ResolveAsync(
        Guid userPublicId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (userPublicId == Guid.Empty || tenantId == Guid.Empty)
        {
            return EffectiveAccess.None;
        }

        var cacheKey = BuildUserKey(userPublicId, tenantId);
        if (cache.TryGetValue(cacheKey, out EffectiveAccess? cached) && cached is not null)
        {
            return cached;
        }

        var resolved = await QueryAsync(userPublicId, tenantId, cancellationToken);
        _ = cache.Set(cacheKey, resolved, CacheLifetime);
        return resolved;
    }

    public void InvalidateUser(Guid userPublicId, Guid tenantId) =>
        cache.Remove(BuildUserKey(userPublicId, tenantId));

    public void InvalidateTenant(Guid tenantId) =>
        _ = TenantGenerations.AddOrUpdate(tenantId, 1, static (_, current) => current + 1);

    private async Task<EffectiveAccess> QueryAsync(
        Guid userPublicId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var projection = await dbContext.IamUsers
            .AsNoTracking()
            // This runs during claims transformation, BEFORE HttpContext.User is assigned, so the ambient
            // tenant context is still empty and the fail-closed global filter would match nothing. The
            // tenant comes from the token's `tid` claim and is applied explicitly in the Where below.
            // Intentional tenant filter bypass: scoping is enforced by that predicate, not skipped.
            .IgnoreQueryFilters()
            .Where(user =>
                user.TenantId == tenantId &&
                user.LinkedUserPublicId == userPublicId &&
                user.IsActive)
            .Select(user => new
            {
                Roles = user.RoleAssignments
                    .Select(assignment => assignment.Role.NormalizedName)
                    .ToList(),
                Permissions = user.RoleAssignments
                    .SelectMany(assignment => assignment.Role.PermissionAssignments)
                    .Select(assignment => assignment.Permission.NormalizedCode)
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (projection is null)
        {
            return EffectiveAccess.None;
        }

        return new EffectiveAccess(
            Normalize(projection.Roles),
            Normalize(projection.Permissions));
    }

    private static string[] Normalize(IEnumerable<string> values) =>
        values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToArray();

    private static string BuildUserKey(Guid userPublicId, Guid tenantId) =>
        $"effective-access:{tenantId:N}:{TenantGenerations.GetValueOrDefault(tenantId)}:{userPublicId:N}";
}

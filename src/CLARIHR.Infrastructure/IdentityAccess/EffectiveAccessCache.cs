using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using CLARIHR.Application.Abstractions.IdentityAccess;
using Microsoft.Extensions.Caching.Memory;

namespace CLARIHR.Infrastructure.IdentityAccess;

/// <summary>
/// Holds resolved effective access between requests, and drops it when an IAM write lands.
///
/// It is a singleton that depends on nothing but the memory cache. That is a requirement, not a
/// preference: the SaveChanges interceptor that invalidates on write is constructed while
/// <c>ApplicationDbContext</c>'s options are being built, so anything it depends on must be reachable
/// without a <c>DbContext</c>. See <see cref="IEffectiveAccessInvalidator"/> for what happens otherwise.
/// </summary>
internal sealed class EffectiveAccessCache(IMemoryCache cache) : IEffectiveAccessInvalidator
{
    // Safety net for multi-instance deployments only: an InvalidateUser/InvalidateTenant raised on one
    // instance never reaches the others, so the TTL bounds how long a stale grant can survive there. On a
    // single instance revocation is immediate. Keep this short — it IS the revocation window in a farm.
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    // IMemoryCache cannot enumerate or drop keys by prefix, so tenant-wide invalidation is done by bumping a
    // generation counter that participates in the cache key: every previously cached entry for that tenant
    // becomes unreachable at once and is evicted later by its own TTL.
    private readonly ConcurrentDictionary<Guid, long> _tenantGenerations = new();

    public bool TryGet(Guid userPublicId, Guid tenantId, [NotNullWhen(true)] out EffectiveAccess? access)
    {
        if (cache.TryGetValue(BuildUserKey(userPublicId, tenantId), out EffectiveAccess? cached) && cached is not null)
        {
            access = cached;
            return true;
        }

        access = null;
        return false;
    }

    public void Store(Guid userPublicId, Guid tenantId, EffectiveAccess access) =>
        _ = cache.Set(BuildUserKey(userPublicId, tenantId), access, Lifetime);

    public void InvalidateUser(Guid userPublicId, Guid tenantId) =>
        cache.Remove(BuildUserKey(userPublicId, tenantId));

    public void InvalidateTenant(Guid tenantId) =>
        _ = _tenantGenerations.AddOrUpdate(tenantId, 1, static (_, current) => current + 1);

    private string BuildUserKey(Guid userPublicId, Guid tenantId) =>
        $"effective-access:{tenantId:N}:{_tenantGenerations.GetValueOrDefault(tenantId)}:{userPublicId:N}";
}

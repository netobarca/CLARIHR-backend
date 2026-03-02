using System.Text.Json;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Infrastructure.Configuration;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace CLARIHR.Infrastructure.IdentityAccess;

internal sealed class FieldPermissionOverrideCache(
    IMemoryCache memoryCache,
    IEnumerable<IDistributedCache> distributedCaches,
    IOptions<FieldPermissionCacheOptions> options) : IFieldPermissionOverrideCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IMemoryCache _memoryCache = memoryCache;
    private readonly IDistributedCache? _distributedCache = distributedCaches.FirstOrDefault();
    private readonly FieldPermissionCacheOptions _options = options.Value;

    public async Task<IReadOnlyDictionary<string, FieldPermissionOverrideState>?> GetRoleOverridesAsync(
        Guid tenantId,
        long roleId,
        string resourceKey,
        CancellationToken cancellationToken)
    {
        var cacheKey = BuildCacheKey(tenantId, roleId, resourceKey);

        if (_options.Mode == FieldPermissionCacheMode.MemoryOnly)
        {
            return _memoryCache.TryGetValue(cacheKey, out IReadOnlyDictionary<string, FieldPermissionOverrideState>? cached) &&
                   cached is not null
                ? cached
                : null;
        }

        var payload = await RequireDistributedCache()
            .GetStringAsync(cacheKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        var deserialized = JsonSerializer.Deserialize<Dictionary<string, FieldPermissionOverrideState>>(payload, SerializerOptions);
        return Normalize(deserialized);
    }

    public async Task SetRoleOverridesAsync(
        Guid tenantId,
        long roleId,
        string resourceKey,
        IReadOnlyDictionary<string, FieldPermissionOverrideState> overrides,
        CancellationToken cancellationToken)
    {
        var cacheKey = BuildCacheKey(tenantId, roleId, resourceKey);
        var normalized = Normalize(overrides);

        if (_options.Mode == FieldPermissionCacheMode.MemoryOnly)
        {
            _memoryCache.Set(cacheKey, normalized, BuildMemoryEntryOptions());
            return;
        }

        _memoryCache.Remove(cacheKey);

        var payload = JsonSerializer.Serialize(normalized, SerializerOptions);
        await RequireDistributedCache()
            .SetStringAsync(cacheKey, payload, BuildDistributedEntryOptions(), cancellationToken);
    }

    public async Task RemoveRoleOverridesAsync(
        Guid tenantId,
        long roleId,
        string resourceKey,
        CancellationToken cancellationToken)
    {
        var cacheKey = BuildCacheKey(tenantId, roleId, resourceKey);
        _memoryCache.Remove(cacheKey);

        if (_options.Mode == FieldPermissionCacheMode.Distributed)
        {
            await RequireDistributedCache()
                .RemoveAsync(cacheKey, cancellationToken);
        }
    }

    private MemoryCacheEntryOptions BuildMemoryEntryOptions() =>
        new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.EntryTtlMinutes)
        };

    private DistributedCacheEntryOptions BuildDistributedEntryOptions() =>
        new()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.EntryTtlMinutes)
        };

    private IDistributedCache RequireDistributedCache() =>
        _distributedCache ?? throw new InvalidOperationException(
            "Field permission cache mode 'Distributed' requires an IDistributedCache registration.");

    private static IReadOnlyDictionary<string, FieldPermissionOverrideState> Normalize(
        IReadOnlyDictionary<string, FieldPermissionOverrideState>? source)
    {
        var normalized = new Dictionary<string, FieldPermissionOverrideState>(StringComparer.OrdinalIgnoreCase);
        if (source is null)
        {
            return normalized;
        }

        foreach (var pair in source)
        {
            normalized[pair.Key] = pair.Value;
        }

        return normalized;
    }

    private static string BuildCacheKey(Guid tenantId, long roleId, string resourceKey) =>
        $"field-permissions:{tenantId:N}:{roleId}:{resourceKey.Trim().ToUpperInvariant()}";
}

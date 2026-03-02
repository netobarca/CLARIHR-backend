using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Infrastructure.Configuration;
using CLARIHR.Infrastructure.IdentityAccess;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CLARIHR.Application.UnitTests;

public sealed class FieldPermissionOverrideCacheTests
{
    [Fact]
    public async Task MemoryOnlyMode_ShouldRoundTripOverrides()
    {
        var cache = CreateCache(
            new FieldPermissionCacheOptions
            {
                Mode = FieldPermissionCacheMode.MemoryOnly,
                EntryTtlMinutes = 10
            });
        var tenantId = Guid.NewGuid();
        var overrides = CreateOverrides();

        await cache.SetRoleOverridesAsync(tenantId, roleId: 42, "RBAC_USERS", overrides, CancellationToken.None);

        var cached = await cache.GetRoleOverridesAsync(tenantId, roleId: 42, "RBAC_USERS", CancellationToken.None);

        Assert.NotNull(cached);
        Assert.Equal(overrides["RBAC_USERS.EMAIL"], cached!["RBAC_USERS.EMAIL"]);
    }

    [Fact]
    public async Task DistributedMode_ShouldRoundTripOverrides()
    {
        var services = new ServiceCollection();
        services.AddDistributedMemoryCache();
        await using var serviceProvider = services.BuildServiceProvider();
        var distributedCache = serviceProvider.GetRequiredService<IDistributedCache>();

        var cache = CreateCache(
            new FieldPermissionCacheOptions
            {
                Mode = FieldPermissionCacheMode.Distributed,
                EntryTtlMinutes = 10
            },
            [distributedCache]);
        var tenantId = Guid.NewGuid();
        var overrides = CreateOverrides();

        await cache.SetRoleOverridesAsync(tenantId, roleId: 99, "RBAC_USERS", overrides, CancellationToken.None);

        var cached = await cache.GetRoleOverridesAsync(tenantId, roleId: 99, "RBAC_USERS", CancellationToken.None);

        Assert.NotNull(cached);
        Assert.Equal(overrides["RBAC_USERS.EMAIL"], cached!["RBAC_USERS.EMAIL"]);
    }

    [Fact]
    public async Task DistributedMode_WithoutProvider_ShouldFailFast()
    {
        var cache = CreateCache(
            new FieldPermissionCacheOptions
            {
                Mode = FieldPermissionCacheMode.Distributed,
                EntryTtlMinutes = 10
            });

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            cache.SetRoleOverridesAsync(Guid.NewGuid(), roleId: 7, "RBAC_USERS", CreateOverrides(), CancellationToken.None));

        Assert.Contains("IDistributedCache", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Remove_ShouldInvalidateStoredOverrides()
    {
        var cache = CreateCache(
            new FieldPermissionCacheOptions
            {
                Mode = FieldPermissionCacheMode.MemoryOnly,
                EntryTtlMinutes = 10
            });
        var tenantId = Guid.NewGuid();

        await cache.SetRoleOverridesAsync(tenantId, roleId: 13, "RBAC_USERS", CreateOverrides(), CancellationToken.None);
        await cache.RemoveRoleOverridesAsync(tenantId, roleId: 13, "RBAC_USERS", CancellationToken.None);

        var cached = await cache.GetRoleOverridesAsync(tenantId, roleId: 13, "RBAC_USERS", CancellationToken.None);

        Assert.Null(cached);
    }

    private static IFieldPermissionOverrideCache CreateCache(
        FieldPermissionCacheOptions options,
        IEnumerable<IDistributedCache>? distributedCaches = null) =>
        new FieldPermissionOverrideCache(
            new MemoryCache(new MemoryCacheOptions()),
            distributedCaches ?? [],
            Options.Create(options));

    private static IReadOnlyDictionary<string, FieldPermissionOverrideState> CreateOverrides() =>
        new Dictionary<string, FieldPermissionOverrideState>(StringComparer.OrdinalIgnoreCase)
        {
            ["RBAC_USERS.EMAIL"] = new(IsVisible: false, IsEditable: false, IsRequired: false, IsMasked: true)
        };
}

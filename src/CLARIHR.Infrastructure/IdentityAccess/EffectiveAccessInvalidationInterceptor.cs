using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Domain.IdentityAccess;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CLARIHR.Infrastructure.IdentityAccess;

/// <summary>
/// Drops the cached effective access of a tenant whenever anything that shapes it is written.
///
/// This lives at the persistence boundary on purpose. Wiring invalidation into each IAM handler would work
/// until someone adds the next write path and forgets — and a forgotten invalidation here does not fail
/// loudly, it silently keeps a revoked permission alive. Sitting on SaveChanges makes the coverage
/// structural: if the change reaches the database, the cache is dropped.
///
/// The granularity is the whole tenant rather than the affected user. A role's permission set is shared, so
/// most edits already invalidate every holder of that role; resolving exactly which users are touched would
/// add queries to save a cache drop that costs one query per active user, on an operation that happens a
/// handful of times a day.
/// </summary>
internal sealed class EffectiveAccessInvalidationInterceptor(IEffectiveAccessResolver resolver) : SaveChangesInterceptor
{
    private readonly HashSet<Guid> _pendingTenantIds = [];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        CaptureAffectedTenants(eventData);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        // Invalidate only once the write actually landed, so a failed save cannot evict a valid cache entry.
        foreach (var tenantId in _pendingTenantIds)
        {
            resolver.InvalidateTenant(tenantId);
        }

        _pendingTenantIds.Clear();
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override Task SaveChangesFailedAsync(
        DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        _pendingTenantIds.Clear();
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void CaptureAffectedTenants(DbContextEventData eventData)
    {
        var changeTracker = eventData.Context?.ChangeTracker;
        if (changeTracker is null)
        {
            return;
        }

        foreach (var entry in changeTracker.Entries())
        {
            if (entry.State is not (Microsoft.EntityFrameworkCore.EntityState.Added
                or Microsoft.EntityFrameworkCore.EntityState.Modified
                or Microsoft.EntityFrameworkCore.EntityState.Deleted))
            {
                continue;
            }

            var tenantId = entry.Entity switch
            {
                IamUser user => user.TenantId,
                IamUserRoleAssignment assignment => assignment.TenantId,
                IamRole role => role.TenantId,
                IamRolePermissionAssignment assignment => assignment.TenantId,
                IamPermission permission => permission.TenantId,
                _ => (Guid?)null
            };

            if (tenantId.HasValue && tenantId.Value != Guid.Empty)
            {
                _ = _pendingTenantIds.Add(tenantId.Value);
            }
        }
    }
}

using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.IdentityAccess;

/// <inheritdoc />
internal sealed class EffectiveAccessResolver(
    ApplicationDbContext dbContext,
    EffectiveAccessCache cache) : IEffectiveAccessResolver
{
    public async Task<EffectiveAccess> ResolveAsync(
        Guid userPublicId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (userPublicId == Guid.Empty || tenantId == Guid.Empty)
        {
            return EffectiveAccess.None;
        }

        if (cache.TryGet(userPublicId, tenantId, out var cached))
        {
            return cached;
        }

        var resolved = await QueryAsync(userPublicId, tenantId, cancellationToken);
        cache.Store(userPublicId, tenantId, resolved);
        return resolved;
    }

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
}

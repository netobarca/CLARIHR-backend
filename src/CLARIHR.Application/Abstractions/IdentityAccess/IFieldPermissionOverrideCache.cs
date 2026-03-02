using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.Abstractions.IdentityAccess;

public interface IFieldPermissionOverrideCache
{
    Task<IReadOnlyDictionary<string, FieldPermissionOverrideState>?> GetRoleOverridesAsync(
        Guid tenantId,
        long roleId,
        string resourceKey,
        CancellationToken cancellationToken);

    Task SetRoleOverridesAsync(
        Guid tenantId,
        long roleId,
        string resourceKey,
        IReadOnlyDictionary<string, FieldPermissionOverrideState> overrides,
        CancellationToken cancellationToken);

    Task RemoveRoleOverridesAsync(
        Guid tenantId,
        long roleId,
        string resourceKey,
        CancellationToken cancellationToken);
}

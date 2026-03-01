using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.IdentityAccess.Contracts;

namespace CLARIHR.Application.Abstractions.IdentityAccess;

public interface IFieldPermissionService
{
    Task<Result<ResourceFieldsResponse>> GetResourceFieldsAsync(
        string resourceKey,
        CancellationToken cancellationToken);

    Task<Result<RoleFieldPermissionsResponse>> GetRoleFieldPermissionsAsync(
        Guid roleId,
        string resourceKey,
        CancellationToken cancellationToken);

    Task<Result<RoleFieldPermissionsResponse>> UpsertRoleFieldPermissionsAsync(
        Guid roleId,
        string resourceKey,
        IReadOnlyCollection<RoleFieldPermissionUpdateModel> fields,
        CancellationToken cancellationToken);

    Task<Result<FieldAccessProfile>> GetCurrentUserAccessProfileAsync(
        string resourceKey,
        CancellationToken cancellationToken);
}

public sealed record RoleFieldPermissionUpdateModel(
    string FieldKey,
    bool IsVisible,
    bool IsEditable,
    bool IsRequired,
    bool IsMasked);

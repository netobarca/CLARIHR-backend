using CLARIHR.Domain.IdentityAccess;

namespace CLARIHR.Application.Features.IdentityAccess.Contracts;

public sealed record IamRoleReferenceResponse(
    Guid Id,
    string Name,
    string? Description);

public sealed record IamPermissionReferenceResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string Module,
    string Screen,
    IamPermissionKind Kind,
    string? Action,
    string? FieldName,
    IamFieldAccessLevel? FieldAccess);

public sealed record IamUserSummaryResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    int RoleCount);

public sealed record IamUserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    IReadOnlyCollection<IamRoleReferenceResponse> Roles);

public sealed record IamRoleSummaryResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystemRole,
    int PermissionCount,
    int UserCount);

public sealed record IamRoleResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystemRole,
    int UserCount,
    IReadOnlyCollection<IamPermissionReferenceResponse> Permissions);

public sealed record IamPermissionSummaryResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string Module,
    string Screen,
    IamPermissionKind Kind,
    string? Action,
    string? FieldName,
    IamFieldAccessLevel? FieldAccess);

public sealed record IamPermissionResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string Module,
    string Screen,
    IamPermissionKind Kind,
    string? Action,
    string? FieldName,
    IamFieldAccessLevel? FieldAccess);

public sealed record PermissionMatrixActionResponse(
    bool Supported,
    bool Granted,
    Guid? PermissionId,
    string? PermissionCode);

public sealed record PermissionMatrixScreenResponse(
    string ResourceKey,
    string DisplayName,
    string Module,
    string Screen,
    bool ManagedByOverride,
    PermissionMatrixActionResponse Access,
    PermissionMatrixActionResponse Read,
    PermissionMatrixActionResponse Create,
    PermissionMatrixActionResponse Update,
    PermissionMatrixActionResponse Delete);

public sealed record RolePermissionMatrixResponse(
    Guid RoleId,
    string RoleName,
    bool IsSystemRole,
    IReadOnlyCollection<PermissionMatrixScreenResponse> Screens);

public sealed record RbacResourceResponse(
    string ResourceKey,
    string DisplayName);

public sealed record RbacResourcesResponse(
    IReadOnlyCollection<RbacResourceResponse> Items);

public sealed record ResourceFieldResponse(
    string FieldKey,
    string PropertyName,
    string DisplayName,
    string DataType,
    bool IsConfigurable,
    bool IsSensitive);

public sealed record ResourceFieldsResponse(
    string ResourceKey,
    IReadOnlyCollection<ResourceFieldResponse> Fields);

public sealed record RoleFieldPermissionResponse(
    string FieldKey,
    string PropertyName,
    string DisplayName,
    string DataType,
    bool IsSensitive,
    bool IsVisible,
    bool IsEditable,
    bool IsRequired,
    bool IsMasked,
    bool IsReadOnly);

public sealed record RoleFieldPermissionsResponse(
    Guid RoleId,
    string RoleName,
    string ResourceKey,
    IReadOnlyCollection<RoleFieldPermissionResponse> Fields);

public sealed record RbacRolePermissionResponse(
    string ResourceKey,
    string DisplayName,
    bool HasAccess,
    bool CanRead,
    bool CanCreate,
    bool CanUpdate,
    bool CanDelete);

public sealed record RbacRolePermissionsResponse(
    Guid RoleId,
    string RoleName,
    bool IsSystemRole,
    IReadOnlyCollection<RbacRolePermissionResponse> Permissions);

public sealed record RbacPermissionAuditStateResponse(
    bool HasAccess,
    bool CanRead,
    bool CanCreate,
    bool CanUpdate,
    bool CanDelete);

public sealed record RbacPermissionAuditEntryResponse(
    long Id,
    Guid CompanyId,
    Guid RoleId,
    string ResourceKey,
    Guid ChangedByUserId,
    string ChangeType,
    RbacPermissionAuditStateResponse Before,
    RbacPermissionAuditStateResponse After,
    DateTime ChangedAtUtc);

public sealed record RbacPermissionAuditListResponse(
    IReadOnlyCollection<RbacPermissionAuditEntryResponse> Items);

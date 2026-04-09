using CLARIHR.Domain.IdentityAccess;
using CLARIHR.Application.Common.Policies;

namespace CLARIHR.Application.Features.IdentityAccess.Contracts;

public sealed record IamRoleReferenceResponse(
    Guid Id,
    string Name,
    string? Description);

public sealed record IamUserRoleResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystemRole,
    IReadOnlyCollection<IamPermissionReferenceResponse> Permissions);

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
    int RoleCount,
    AllowedActionsResponse? AllowedActions = null);

public sealed record IamUserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool IsActive,
    IReadOnlyCollection<IamUserRoleResponse> Roles);

public sealed record IamRoleSummaryResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystemRole,
    int PermissionCount,
    int UserCount,
    AllowedActionsResponse? AllowedActions = null);

public sealed record IamRoleResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystemRole,
    int UserCount,
    IReadOnlyCollection<IamPermissionReferenceResponse> Permissions);

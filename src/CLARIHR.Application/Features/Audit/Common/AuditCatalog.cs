namespace CLARIHR.Application.Features.Audit.Common;

public static class AuditEventTypes
{
    public const string UserCreated = "USER_CREATED";
    public const string UserUpdated = "USER_UPDATED";
    public const string UserDeactivated = "USER_DEACTIVATED";
    public const string UserReactivated = "USER_REACTIVATED";
    public const string UserInvited = "USER_INVITED";
    public const string UserInvitationReset = "USER_INVITATION_RESET";
    public const string RoleCreated = "ROLE_CREATED";
    public const string RoleUpdated = "ROLE_UPDATED";
    public const string RoleCloned = "ROLE_CLONED";
    public const string RoleResourcePermissionsUpdated = "ROLE_RESOURCE_PERMISSIONS_UPDATED";
    public const string RoleFieldPermissionsUpdated = "ROLE_FIELD_PERMISSIONS_UPDATED";

    public static readonly IReadOnlyCollection<string> All =
    [
        UserCreated,
        UserUpdated,
        UserDeactivated,
        UserReactivated,
        UserInvited,
        UserInvitationReset,
        RoleCreated,
        RoleUpdated,
        RoleCloned,
        RoleResourcePermissionsUpdated,
        RoleFieldPermissionsUpdated
    ];

    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        normalized = All.SingleOrDefault(candidate => candidate.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        return normalized.Length > 0;
    }
}

public static class AuditEntityTypes
{
    public const string User = "User";
    public const string Role = "Role";
    public const string Permission = "Permission";

    public static readonly IReadOnlyCollection<string> All =
    [
        User,
        Role,
        Permission
    ];

    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        normalized = All.SingleOrDefault(candidate => candidate.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        return normalized.Length > 0;
    }
}

public static class AuditActions
{
    public const string Create = "Create";
    public const string Update = "Update";
    public const string Deactivate = "Deactivate";
    public const string Reactivate = "Reactivate";
    public const string Invite = "Invite";
    public const string InvitationReset = "InvitationReset";
    public const string Clone = "Clone";
    public const string PermissionChange = "PermissionChange";
}

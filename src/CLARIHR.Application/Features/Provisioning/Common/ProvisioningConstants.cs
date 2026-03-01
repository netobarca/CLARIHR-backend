namespace CLARIHR.Application.Features.Provisioning.Common;

public static class ProvisioningConstants
{
    public const string FreePlanCode = "FREE";
    public const string RbacModuleKey = "RBAC";
    public const string UsersModuleKey = "USERS";
    public const string CompanyAdminRoleName = "Admin de Empresa";
    public const string StandardUserRoleName = "Usuario Estándar";

    public static readonly string[] FreePlanEnabledModules =
    [
        RbacModuleKey,
        UsersModuleKey
    ];

    public static readonly ProvisioningPermissionDefinition[] CompanyAdminPermissions =
    [
        new("iam.administration.manage", "Administrar IAM", "Administracion completa de identidad.", "IAM", "Administration", "Manage"),
        new("RBAC.USERS.MANAGE", "Gestionar usuarios", "Administracion de usuarios del tenant.", RbacModuleKey, "Users", "Manage"),
        new("RBAC.ROLES.MANAGE", "Gestionar roles", "Administracion de roles del tenant.", RbacModuleKey, "Roles", "Manage"),
        new("RBAC.PERMISSIONS.MANAGE", "Gestionar permisos", "Administracion de permisos del tenant.", RbacModuleKey, "Permissions", "Manage")
    ];
}

public sealed record ProvisioningPermissionDefinition(
    string Code,
    string Name,
    string Description,
    string Module,
    string Screen,
    string Action);

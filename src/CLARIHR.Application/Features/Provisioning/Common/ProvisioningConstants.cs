namespace CLARIHR.Application.Features.Provisioning.Common;

public static class ProvisioningConstants
{
    public const string FreePlanCode = "FREE";
    public const string RbacModuleKey = "RBAC";
    public const string UsersModuleKey = "USERS";
    public const string JobProfilesModuleKey = "JOB_PROFILES";
    public const string PositionSlotsModuleKey = "POSITION_SLOTS";
    public const string SalaryTabulatorModuleKey = "SALARY_TABULATOR";
    public const string CostCentersModuleKey = "COST_CENTERS";
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
        new("RBAC.PERMISSIONS.MANAGE", "Gestionar permisos", "Administracion de permisos del tenant.", RbacModuleKey, "Permissions", "Manage"),
        new("JobProfiles.Read", "Leer perfiles de puesto", "Consulta del manual descriptivo de puestos.", JobProfilesModuleKey, "JobProfiles", "Read"),
        new("JobProfiles.Admin", "Administrar perfiles de puesto", "Administracion completa de perfiles de puesto.", JobProfilesModuleKey, "JobProfiles", "Manage"),
        new("JobCatalogs.Admin", "Administrar catalogos de puestos", "Administracion de catalogos del manual de puestos.", JobProfilesModuleKey, "JobCatalogs", "Manage"),
        new("PositionSlots.Read", "Leer plazas", "Consulta de plazas y estructura de dependencias.", PositionSlotsModuleKey, "PositionSlots", "Read"),
        new("PositionSlots.Admin", "Administrar plazas", "Administracion completa de plazas y ocupacion.", PositionSlotsModuleKey, "PositionSlots", "Manage"),
        new("CostCenters.Read", "Leer centros de costo", "Consulta de centros de costo contable y su uso.", CostCentersModuleKey, "CostCenters", "Read"),
        new("CostCenters.Admin", "Administrar centros de costo", "Administracion completa de centros de costo contable.", CostCentersModuleKey, "CostCenters", "Manage"),
        new("SalaryTabulator.Read", "Leer tabulador salarial", "Consulta de lineas y solicitudes del tabulador salarial.", SalaryTabulatorModuleKey, "SalaryTabulator", "Read"),
        new("SalaryTabulator.Request", "Solicitar cambios de tabulador salarial", "Creacion y gestion de solicitudes de cambio al tabulador salarial.", SalaryTabulatorModuleKey, "SalaryTabulator", "Request"),
        new("SalaryTabulator.Approve", "Aprobar cambios de tabulador salarial", "Aprobacion o rechazo de solicitudes del tabulador salarial.", SalaryTabulatorModuleKey, "SalaryTabulator", "Approve"),
        new("SalaryTabulator.Admin", "Administrar tabulador salarial", "Administracion completa del tabulador salarial.", SalaryTabulatorModuleKey, "SalaryTabulator", "Manage")
    ];
}

public sealed record ProvisioningPermissionDefinition(
    string Code,
    string Name,
    string Description,
    string Module,
    string Screen,
    string Action);

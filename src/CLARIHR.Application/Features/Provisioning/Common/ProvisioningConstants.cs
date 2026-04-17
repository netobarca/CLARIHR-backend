using CLARIHR.Domain.Companies;

namespace CLARIHR.Application.Features.Provisioning.Common;

public static class ProvisioningConstants
{
    public const string FreePlanCode = "FREE";
    public const string MasterPlanCode = "MASTER";
    public const string FreePlanName = "Free";
    public const string MasterPlanName = "Master";
    public const string FreePlanDescription = "Public baseline commercial plan used during standard provisioning.";
    public const string MasterPlanDescription = "Internal master commercial plan reserved for CLARI operators.";
    public const string RbacModuleKey = CommercialModuleKeys.Rbac;
    public const string UsersModuleKey = CommercialModuleKeys.Users;
    public const string OrgStructureCatalogsModuleKey = CommercialModuleKeys.OrgStructureCatalogs;
    public const string PositionDescriptionCatalogsModuleKey = CommercialModuleKeys.PositionDescriptionCatalogs;
    public const string JobProfilesModuleKey = CommercialModuleKeys.JobProfiles;
    public const string PositionSlotsModuleKey = CommercialModuleKeys.PositionSlots;
    public const string SalaryTabulatorModuleKey = CommercialModuleKeys.SalaryTabulator;
    public const string CostCentersModuleKey = CommercialModuleKeys.CostCenters;
    public const string LegalRepresentativesModuleKey = CommercialModuleKeys.LegalRepresentatives;
    public const string CompetencyFrameworkModuleKey = CommercialModuleKeys.CompetencyFramework;
    public const string OrgUnitsModuleKey = CommercialModuleKeys.OrgUnits;
    public const string LocationsModuleKey = CommercialModuleKeys.Locations;
    public const string PersonnelFilesModuleKey = CommercialModuleKeys.PersonnelFiles;
    public const string CompanyAdminRoleName = "Admin de Empresa";
    public const string StandardUserRoleName = "Usuario Estándar";

    public static readonly string[] FreePlanEnabledModules = CommercialModuleCatalog.DefaultFreeModuleKeys.ToArray();
    public static readonly string[] MasterPlanEnabledModules = CommercialModuleCatalog.DefaultMasterModuleKeys.ToArray();
    public static readonly string[] EnterpriseLegacyPlanAliases = ["ENTERPRISE_LEGACY", "ENTERPRISE-LEGACY", "ENTERPRISE LEGACY"];

    public static readonly ProvisioningPermissionDefinition[] CompanyAdminPermissions =
    [
        new("iam.administration.manage", "Administrar IAM", "Administracion completa de identidad.", "IAM", "Administration", "Manage"),
        new("RBAC.USERS.MANAGE", "Gestionar usuarios", "Administracion de usuarios del tenant.", RbacModuleKey, "Users", "Manage"),
        new("RBAC.ROLES.MANAGE", "Gestionar roles", "Administracion de roles del tenant.", RbacModuleKey, "Roles", "Manage"),
        new("RBAC.PERMISSIONS.MANAGE", "Gestionar permisos", "Administracion de permisos del tenant.", RbacModuleKey, "Permissions", "Manage"),
        new("CompanyPreferences.Read", "Leer preferencias de compañía", "Consulta de moneda y zona horaria configuradas para la compañía.", RbacModuleKey, "CompanyPreferences", "Read"),
        new("CompanyPreferences.Admin", "Administrar preferencias de compañía", "Administración de moneda y zona horaria de la compañía.", RbacModuleKey, "CompanyPreferences", "Manage"),
        new("CompanyUsers.Read", "Leer usuarios de empresa", "Consulta de usuarios operativos del tenant.", UsersModuleKey, "CompanyUsers", "Read"),
        new("CompanyUsers.Admin", "Administrar usuarios de empresa", "Administracion completa de usuarios operativos del tenant.", UsersModuleKey, "CompanyUsers", "Manage"),
        new("OrgStructureCatalogs.Read", "Leer catalogos de estructura organizativa", "Consulta de catalogos de tipos de empresa, unidades y areas funcionales.", OrgStructureCatalogsModuleKey, "OrgStructureCatalogs", "Read"),
        new("OrgStructureCatalogs.Admin", "Administrar catalogos de estructura organizativa", "Administracion completa de catalogos de estructura organizativa.", OrgStructureCatalogsModuleKey, "OrgStructureCatalogs", "Manage"),
        new("PositionDescriptionCatalogs.Read", "Leer catalogos de descripcion de puesto", "Consulta de catalogos de descripcion de puesto.", PositionDescriptionCatalogsModuleKey, "PositionDescriptionCatalogs", "Read"),
        new("PositionDescriptionCatalogs.Admin", "Administrar catalogos de descripcion de puesto", "Administracion completa de catalogos de descripcion de puesto.", PositionDescriptionCatalogsModuleKey, "PositionDescriptionCatalogs", "Manage"),
        new("JobProfiles.Read", "Leer perfiles de puesto", "Consulta del manual descriptivo de puestos.", JobProfilesModuleKey, "JobProfiles", "Read"),
        new("JobProfiles.Admin", "Administrar perfiles de puesto", "Administracion completa de perfiles de puesto.", JobProfilesModuleKey, "JobProfiles", "Manage"),
        new("JobCatalogs.Admin", "Administrar catalogos de puestos", "Administracion de catalogos del manual de puestos.", JobProfilesModuleKey, "JobCatalogs", "Manage"),
        new("PositionSlots.Read", "Leer plazas", "Consulta de plazas y estructura de dependencias.", PositionSlotsModuleKey, "PositionSlots", "Read"),
        new("PositionSlots.Admin", "Administrar plazas", "Administracion completa de plazas y ocupacion.", PositionSlotsModuleKey, "PositionSlots", "Manage"),
        new("CostCenters.Read", "Leer centros de costo", "Consulta de centros de costo contable y su uso.", CostCentersModuleKey, "CostCenters", "Read"),
        new("CostCenters.Admin", "Administrar centros de costo", "Administracion completa de centros de costo contable.", CostCentersModuleKey, "CostCenters", "Manage"),
        new("LegalRepresentatives.Read", "Leer representantes legales", "Consulta de representantes legales activos e historicos.", LegalRepresentativesModuleKey, "LegalRepresentatives", "Read"),
        new("LegalRepresentatives.Admin", "Administrar representantes legales", "Administracion completa de representantes legales.", LegalRepresentativesModuleKey, "LegalRepresentatives", "Manage"),
        new("CompetencyFramework.Read", "Leer marco de competencias", "Consulta del marco de competencias y conductas por puesto.", CompetencyFrameworkModuleKey, "CompetencyFramework", "Read"),
        new("CompetencyFramework.Admin", "Administrar marco de competencias", "Administracion completa de competencias, conductas y piramide ocupacional.", CompetencyFrameworkModuleKey, "CompetencyFramework", "Manage"),
        new("SalaryTabulator.Read", "Leer tabulador salarial", "Consulta de lineas y solicitudes del tabulador salarial.", SalaryTabulatorModuleKey, "SalaryTabulator", "Read"),
        new("SalaryTabulator.Request", "Solicitar cambios de tabulador salarial", "Creacion y gestion de solicitudes de cambio al tabulador salarial.", SalaryTabulatorModuleKey, "SalaryTabulator", "Request"),
        new("SalaryTabulator.Approve", "Aprobar cambios de tabulador salarial", "Aprobacion o rechazo de solicitudes del tabulador salarial.", SalaryTabulatorModuleKey, "SalaryTabulator", "Approve"),
        new("SalaryTabulator.Admin", "Administrar tabulador salarial", "Administracion completa del tabulador salarial.", SalaryTabulatorModuleKey, "SalaryTabulator", "Manage"),
        new("OrgUnits.Read", "Leer unidades organizativas", "Consulta de unidades organizativas y su jerarquia.", OrgUnitsModuleKey, "OrgUnits", "Read"),
        new("OrgUnits.Admin", "Administrar unidades organizativas", "Administracion completa de unidades organizativas.", OrgUnitsModuleKey, "OrgUnits", "Manage"),
        new("WorkCenters.Read", "Leer centros de trabajo", "Consulta de centros de trabajo y tipos de centro del tenant.", LocationsModuleKey, "WorkCenters", "Read"),
        new("WorkCenters.Admin", "Administrar centros de trabajo", "Administracion completa de centros de trabajo y tipos de centro del tenant.", LocationsModuleKey, "WorkCenters", "Manage"),
        new("Locations.Read", "Leer ubicaciones y centros de trabajo", "Consulta de ubicaciones, niveles, grupos y centros de trabajo.", LocationsModuleKey, "Locations", "Read"),
        new("Locations.Admin", "Administrar ubicaciones y centros de trabajo", "Administracion completa de ubicaciones y centros de trabajo.", LocationsModuleKey, "Locations", "Manage"),
        new("PersonnelFiles.Read", "Leer expedientes de personal", "Consulta de expedientes de personal y curriculum.", PersonnelFilesModuleKey, "PersonnelFiles", "Read"),
        new("PersonnelFiles.Admin", "Administrar expedientes de personal", "Administracion completa de expedientes de personal.", PersonnelFilesModuleKey, "PersonnelFiles", "Manage")
    ];
}

public sealed record ProvisioningPermissionDefinition(
    string Code,
    string Name,
    string Description,
    string Module,
    string Screen,
    string Action);

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
        new("iam.administration.manage", "Administrar IAM", "Administración completa de identidad.", "IAM", "Administration", "Manage"),
        new("RBAC.USERS.MANAGE", "Gestionar usuarios", "Administración de usuarios del tenant.", RbacModuleKey, "Users", "Manage"),
        new("RBAC.ROLES.MANAGE", "Gestionar roles", "Administración de roles del tenant.", RbacModuleKey, "Roles", "Manage"),
        new("RBAC.PERMISSIONS.MANAGE", "Gestionar permisos", "Administración de permisos del tenant.", RbacModuleKey, "Permissions", "Manage"),
        new("CompanyPreferences.Read", "Leer preferencias de compañía", "Consulta de moneda y zona horaria configuradas para la compañía.", RbacModuleKey, "CompanyPreferences", "Read"),
        new("CompanyPreferences.Admin", "Administrar preferencias de compañía", "Administración de moneda y zona horaria de la compañía.", RbacModuleKey, "CompanyPreferences", "Manage"),
        new("CompanyUsers.Read", "Leer usuarios de empresa", "Consulta de usuarios operativos del tenant.", UsersModuleKey, "CompanyUsers", "Read"),
        new("CompanyUsers.Admin", "Administrar usuarios de empresa", "Administración completa de usuarios operativos del tenant.", UsersModuleKey, "CompanyUsers", "Manage"),
        new("OrgStructureCatalogs.Read", "Leer catálogos de estructura organizativa", "Consulta de catálogos de tipos de empresa, unidades y areas funcionales.", OrgStructureCatalogsModuleKey, "OrgStructureCatalogs", "Read"),
        new("OrgStructureCatalogs.Admin", "Administrar catálogos de estructura organizativa", "Administración completa de catálogos de estructura organizativa.", OrgStructureCatalogsModuleKey, "OrgStructureCatalogs", "Manage"),
        new("PositionDescriptionCatalogs.Read", "Leer catálogos de descripción de puesto", "Consulta de catálogos de descripción de puesto.", PositionDescriptionCatalogsModuleKey, "PositionDescriptionCatalogs", "Read"),
        new("PositionDescriptionCatalogs.Admin", "Administrar catálogos de descripción de puesto", "Administración completa de catálogos de descripción de puesto.", PositionDescriptionCatalogsModuleKey, "PositionDescriptionCatalogs", "Manage"),
        new("JobProfiles.Read", "Leer perfiles de puesto", "Consulta del manual descriptivo de puestos.", JobProfilesModuleKey, "JobProfiles", "Read"),
        new("JobProfiles.Admin", "Administrar perfiles de puesto", "Administración completa de perfiles de puesto.", JobProfilesModuleKey, "JobProfiles", "Manage"),
        new("JobProfiles.Publish", "Publicar perfiles de puesto", "Publicar, reabrir y archivar perfiles de puesto. No implicado por la administración de perfiles.", JobProfilesModuleKey, "JobProfiles", "Publish"),
        new("JobCatalogs.Admin", "Administrar catálogos de puestos", "Administración de catálogos del manual de puestos.", JobProfilesModuleKey, "JobCatalogs", "Manage"),
        new("PositionSlots.Read", "Leer plazas", "Consulta de plazas y estructura de dependencias.", PositionSlotsModuleKey, "PositionSlots", "Read"),
        new("PositionSlots.Admin", "Administrar plazas", "Administración completa de plazas y ocupación.", PositionSlotsModuleKey, "PositionSlots", "Manage"),
        new("CostCenters.Read", "Leer centros de costo", "Consulta de centros de costo contable y su uso.", CostCentersModuleKey, "CostCenters", "Read"),
        new("CostCenters.Admin", "Administrar centros de costo", "Administración completa de centros de costo contable.", CostCentersModuleKey, "CostCenters", "Manage"),
        new("LeaveConfiguration.Read", "Leer configuración de vacaciones e incapacidades", "Consulta de los maestros de configuración de vacaciones e incapacidades (clínicas médicas, riesgos y tipos de incapacidad, asuetos y periodos de planilla).", PersonnelFilesModuleKey, "LeaveConfiguration", "Read"),
        new("LeaveConfiguration.Admin", "Administrar configuración de vacaciones e incapacidades", "Administración completa de los maestros de configuración de vacaciones e incapacidades y carga de la plantilla legal.", PersonnelFilesModuleKey, "LeaveConfiguration", "Manage"),
        new("EmployeeRelationsConfiguration.Read", "Leer configuración de relaciones laborales", "Consulta de los maestros de configuración de otras transacciones de personal (tipos de reconocimiento, tipos y causas de amonestación).", PersonnelFilesModuleKey, "EmployeeRelationsConfiguration", "Read"),
        new("EmployeeRelationsConfiguration.Admin", "Administrar configuración de relaciones laborales", "Administración completa de los maestros de configuración de otras transacciones de personal y carga de la plantilla.", PersonnelFilesModuleKey, "EmployeeRelationsConfiguration", "Manage"),
        new("LegalRepresentatives.Read", "Leer representantes legales", "Consulta de representantes legales activos e históricos.", LegalRepresentativesModuleKey, "LegalRepresentatives", "Read"),
        new("LegalRepresentatives.Admin", "Administrar representantes legales", "Administración completa de representantes legales.", LegalRepresentativesModuleKey, "LegalRepresentatives", "Manage"),
        new("CompetencyFramework.Read", "Leer marco de competencias", "Consulta del marco de competencias y conductas por puesto.", CompetencyFrameworkModuleKey, "CompetencyFramework", "Read"),
        new("CompetencyFramework.Admin", "Administrar marco de competencias", "Administración completa de competencias, conductas y pirámide ocupacional.", CompetencyFrameworkModuleKey, "CompetencyFramework", "Manage"),
        new("SalaryTabulator.Read", "Leer tabulador salarial", "Consulta de líneas y solicitudes del tabulador salarial.", SalaryTabulatorModuleKey, "SalaryTabulator", "Read"),
        new("SalaryTabulator.Request", "Solicitar cambios de tabulador salarial", "Creación y gestión de solicitudes de cambio al tabulador salarial.", SalaryTabulatorModuleKey, "SalaryTabulator", "Request"),
        new("SalaryTabulator.Approve", "Aprobar cambios de tabulador salarial", "Aprobación o rechazo de solicitudes del tabulador salarial.", SalaryTabulatorModuleKey, "SalaryTabulator", "Approve"),
        new("SalaryTabulator.Admin", "Administrar tabulador salarial", "Administración completa del tabulador salarial.", SalaryTabulatorModuleKey, "SalaryTabulator", "Manage"),
        new("OrgUnits.Read", "Leer unidades organizativas", "Consulta de unidades organizativas y su jerarquía.", OrgUnitsModuleKey, "OrgUnits", "Read"),
        new("OrgUnits.Admin", "Administrar unidades organizativas", "Administración completa de unidades organizativas.", OrgUnitsModuleKey, "OrgUnits", "Manage"),
        new("WorkCenters.Read", "Leer centros de trabajo", "Consulta de centros de trabajo y tipos de centro del tenant.", LocationsModuleKey, "WorkCenters", "Read"),
        new("WorkCenters.Admin", "Administrar centros de trabajo", "Administración completa de centros de trabajo y tipos de centro del tenant.", LocationsModuleKey, "WorkCenters", "Manage"),
        new("Locations.Read", "Leer ubicaciones y centros de trabajo", "Consulta de ubicaciones, niveles, grupos y centros de trabajo.", LocationsModuleKey, "Locations", "Read"),
        new("Locations.Admin", "Administrar ubicaciones y centros de trabajo", "Administración completa de ubicaciones y centros de trabajo.", LocationsModuleKey, "Locations", "Manage"),
        new("PersonnelFiles.Read", "Leer expedientes de personal", "Consulta de expedientes de personal y curriculum.", PersonnelFilesModuleKey, "PersonnelFiles", "Read"),
        new("PersonnelFiles.Admin", "Administrar expedientes de personal", "Administración completa de expedientes de personal.", PersonnelFilesModuleKey, "PersonnelFiles", "Manage"),
        new("PersonnelFiles.AuthorizeRehire", "Autorizar recontratación", "Autorizar la recontratación de empleados marcados como no recontratables.", PersonnelFilesModuleKey, "PersonnelFiles", "AuthorizeRehire"),
        new("PersonnelFiles.ViewCompensation", "Ver compensación", "Consulta de la compensación (salario, ingresos y egresos) de los expedientes.", PersonnelFilesModuleKey, "PersonnelFiles", "ViewCompensation"),
        new("PersonnelFiles.ManageSubstitutions", "Gestionar sustituciones", "Designar y administrar el sustituto de un empleado durante su ausencia.", PersonnelFilesModuleKey, "PersonnelFiles", "ManageSubstitutions"),
        new("PersonnelFiles.ViewInsurance", "Ver seguros", "Consulta de los seguros y beneficiarios de los expedientes.", PersonnelFilesModuleKey, "PersonnelFiles", "ViewInsurance"),
        new("PersonnelFiles.ViewMedicalClaims", "Ver reclamos de seguro médico", "Consulta de los reclamos de seguro médico (incluye el diagnóstico, dato de salud) de los expedientes.", PersonnelFilesModuleKey, "PersonnelFiles", "ViewMedicalClaims"),
        new("PersonnelFiles.ManageMedicalClaims", "Gestionar reclamos de seguro médico", "Crear, editar y eliminar los reclamos de seguro médico de los expedientes.", PersonnelFilesModuleKey, "PersonnelFiles", "ManageMedicalClaims"),
        new("PersonnelFiles.ViewCompetencies", "Ver competencias del puesto", "Consulta de las competencias del puesto del empleado (notas esperadas/alcanzadas y brechas).", PersonnelFilesModuleKey, "PersonnelFiles", "ViewCompetencies"),
        new("PersonnelFiles.ManageCompetencies", "Gestionar competencias del puesto", "Registrar, editar y eliminar las evaluaciones de competencias del puesto de los expedientes.", PersonnelFilesModuleKey, "PersonnelFiles", "ManageCompetencies"),
        new("PersonnelFiles.ViewOffPayrollTransactions", "Ver transacciones fuera de nómina", "Consulta de las transacciones fuera de nómina (gastos de la empresa por el empleado) de los expedientes.", PersonnelFilesModuleKey, "PersonnelFiles", "ViewOffPayrollTransactions"),
        new("PersonnelFiles.ManageOffPayrollTransactions", "Gestionar transacciones fuera de nómina", "Registrar, editar y eliminar las transacciones fuera de nómina de los expedientes.", PersonnelFilesModuleKey, "PersonnelFiles", "ManageOffPayrollTransactions"),
        new("PersonnelFiles.ViewEconomicAidRequests", "Ver ayuda económica", "Consulta de las solicitudes de ayuda económica (asistencia por emergencia) de los expedientes.", PersonnelFilesModuleKey, "PersonnelFiles", "ViewEconomicAidRequests"),
        new("PersonnelFiles.ManageEconomicAidRequests", "Gestionar ayuda económica", "Validar (aprobar/rechazar), desembolsar, editar y dar de baja las solicitudes de ayuda económica de los expedientes.", PersonnelFilesModuleKey, "PersonnelFiles", "ManageEconomicAidRequests"),
        new("PersonnelFiles.ViewCertificateRequests", "Ver constancias", "Consulta de las solicitudes de constancia (salario/laboral/embajada) de los expedientes y de la bandeja de la empresa.", PersonnelFilesModuleKey, "PersonnelFiles", "ViewCertificateRequests"),
        new("PersonnelFiles.ManageCertificateRequests", "Gestionar constancias", "Procesar, emitir, entregar, rechazar, editar y dar de baja las solicitudes de constancia, y configurar el formato de constancias de la empresa.", PersonnelFilesModuleKey, "PersonnelFiles", "ManageCertificateRequests"),
        new("PersonnelFiles.ManageExitInterviewForms", "Gestionar formularios de entrevista de retiro", "Disenar, publicar y asociar los formularios de entrevista de retiro (salida) de la institución.", PersonnelFilesModuleKey, "PersonnelFiles", "ManageExitInterviewForms"),
        new("PersonnelFiles.ViewExitInterviews", "Ver entrevistas de retiro", "Consulta de las entrevistas de retiro (salida) respondidas por los empleados.", PersonnelFilesModuleKey, "PersonnelFiles", "ViewExitInterviews"),
        new("PersonnelFiles.ManageExitInterviews", "Gestionar entrevistas de retiro", "Capturar y administrar las entrevistas de retiro (salida) de los empleados.", PersonnelFilesModuleKey, "PersonnelFiles", "ManageExitInterviews"),
        new("PersonnelFiles.ViewRetirements", "Ver retiros definitivos", "Consulta de las solicitudes de retiro definitivo, la bandeja de la empresa y la bandeja de entrevistas de autorizados.", PersonnelFilesModuleKey, "PersonnelFiles", "ViewRetirements"),
        new("PersonnelFiles.ManageRetirements", "Gestionar retiros definitivos", "Registrar, editar, anular y ejecutar las solicitudes de retiro definitivo de los empleados.", PersonnelFilesModuleKey, "PersonnelFiles", "ManageRetirements"),
        new("PersonnelFiles.AuthorizeRetirement", "Autorizar retiros definitivos", "Autorizar o rechazar las solicitudes de retiro definitivo (y anular una autorizada). No implicado por la administración de expedientes.", PersonnelFilesModuleKey, "PersonnelFiles", "AuthorizeRetirement"),
        new("PersonnelFiles.RevertRetirement", "Revertir retiros definitivos", "Revertir un retiro definitivo ejecutado restaurando los estados del empleado. No implicado por la administración de expedientes.", PersonnelFilesModuleKey, "PersonnelFiles", "RevertRetirement"),
        new("PersonnelFiles.ViewSettlements", "Ver liquidaciones", "Consulta de las liquidaciones de personal (detalle por expediente, bandeja de la empresa y exportaciones).", PersonnelFilesModuleKey, "PersonnelFiles", "ViewSettlements"),
        new("PersonnelFiles.ManageSettlements", "Gestionar liquidaciones", "Crear, editar, emitir y anular las liquidaciones de personal, y administrar los escenarios de simulación.", PersonnelFilesModuleKey, "PersonnelFiles", "ManageSettlements"),
        new("PersonnelFiles.ViewIncapacities", "Ver incapacidades", "Consulta de las incapacidades y periodos de lactancia de los expedientes, la bandeja de la empresa y sus exportaciones.", PersonnelFilesModuleKey, "PersonnelFiles", "ViewIncapacities"),
        new("PersonnelFiles.ManageIncapacities", "Gestionar incapacidades", "Registrar, confirmar, cerrar, anular y prorrogar incapacidades, y administrar los periodos de lactancia.", PersonnelFilesModuleKey, "PersonnelFiles", "ManageIncapacities"),
        new("PersonnelFiles.ViewVacations", "Ver vacaciones", "Consulta del fondo de vacaciones, saldos, solicitudes, calendario y plan anual de los expedientes.", PersonnelFilesModuleKey, "PersonnelFiles", "ViewVacations"),
        new("PersonnelFiles.ManageVacations", "Gestionar vacaciones", "Generar el fondo de vacaciones, decidir y devolver solicitudes, y administrar el plan anual.", PersonnelFilesModuleKey, "PersonnelFiles", "ManageVacations"),
        new("PersonnelFiles.ViewCompensatoryTime", "Ver tiempo compensatorio", "Consulta del fondo de tiempo compensatorio, estado de cuenta, acreditaciones y ausencias de los expedientes.", PersonnelFilesModuleKey, "PersonnelFiles", "ViewCompensatoryTime"),
        new("PersonnelFiles.ManageCompensatoryTime", "Gestionar tiempo compensatorio", "Registrar, editar y anular acreditaciones y ausencias de tiempo compensatorio.", PersonnelFilesModuleKey, "PersonnelFiles", "ManageCompensatoryTime"),
        new("PersonnelFiles.ViewRecognitions", "Ver reconocimientos", "Consulta de los reconocimientos de los expedientes, la bandeja de la empresa y sus exportaciones.", PersonnelFilesModuleKey, "PersonnelFiles", "ViewRecognitions"),
        new("PersonnelFiles.ManageRecognitions", "Gestionar reconocimientos", "Registrar, editar y anular reconocimientos (en revisión) de los expedientes.", PersonnelFilesModuleKey, "PersonnelFiles", "ManageRecognitions"),
        new("PersonnelFiles.AuthorizeRecognitions", "Autorizar reconocimientos", "Decidir (aplicar/rechazar) y revocar los reconocimientos de los empleados. No implicado por la administración de expedientes.", PersonnelFilesModuleKey, "PersonnelFiles", "AuthorizeRecognitions"),
        new("PersonnelFiles.ViewDisciplinaryActions", "Ver amonestaciones", "Consulta de las amonestaciones de los expedientes, la bandeja de la empresa y sus exportaciones.", PersonnelFilesModuleKey, "PersonnelFiles", "ViewDisciplinaryActions"),
        new("PersonnelFiles.ManageDisciplinaryActions", "Gestionar amonestaciones", "Registrar, editar y anular amonestaciones (en revisión) de los expedientes.", PersonnelFilesModuleKey, "PersonnelFiles", "ManageDisciplinaryActions"),
        new("PersonnelFiles.AuthorizeDisciplinaryActions", "Autorizar amonestaciones", "Decidir (aplicar/rechazar) y revocar las amonestaciones de los empleados. No implicado por la administración de expedientes.", PersonnelFilesModuleKey, "PersonnelFiles", "AuthorizeDisciplinaryActions"),
        new("PersonnelFiles.ViewTimeAvailability", "Ver disponibilidad de tiempos", "Consulta de la disponibilidad de tiempos (suspensiones y fin de contratos temporales) de la empresa.", PersonnelFilesModuleKey, "PersonnelFiles", "ViewTimeAvailability"),
        new("PersonnelFiles.ViewRecurringIncomes", "Ver ingresos cíclicos", "Consulta de los ingresos cíclicos de los expedientes, la bandeja de la empresa y sus exportaciones (insumo de planilla).", PersonnelFilesModuleKey, "PersonnelFiles", "ViewRecurringIncomes"),
        new("PersonnelFiles.ManageRecurringIncomes", "Gestionar ingresos cíclicos", "Registrar, editar, suspender, cerrar y anular ingresos cíclicos, y aplicar sus cuotas por periodo.", PersonnelFilesModuleKey, "PersonnelFiles", "ManageRecurringIncomes"),
        new("PersonnelFiles.AuthorizeRecurringIncomes", "Autorizar ingresos cíclicos", "Decidir (autorizar/rechazar) y revocar los ingresos cíclicos de los empleados. No implicado por la administración de expedientes.", PersonnelFilesModuleKey, "PersonnelFiles", "AuthorizeRecurringIncomes"),
        new("PersonnelFiles.ViewOneTimeDeductions", "Ver descuentos eventuales", "Consulta de los descuentos eventuales de los empleados, la bandeja de la empresa y sus exportaciones (insumo de planilla).", PersonnelFilesModuleKey, "PersonnelFiles", "ViewOneTimeDeductions"),
        new("PersonnelFiles.ManageOneTimeDeductions", "Gestionar descuentos eventuales", "Registrar, editar y anular descuentos eventuales, y aplicarlos (o revertir su aplicación) en la planilla.", PersonnelFilesModuleKey, "PersonnelFiles", "ManageOneTimeDeductions"),
        new("PersonnelFiles.AuthorizeOneTimeDeductions", "Autorizar descuentos eventuales", "Decidir (autorizar/rechazar) y revocar los descuentos eventuales de los empleados. No implicado por la administración de expedientes.", PersonnelFilesModuleKey, "PersonnelFiles", "AuthorizeOneTimeDeductions"),
        new("PersonnelFiles.ViewRecurringDeductions", "Ver descuentos cíclicos", "Consulta de los descuentos cíclicos de los expedientes, su tabla de amortización, la bandeja de la empresa y sus exportaciones (insumo de planilla).", PersonnelFilesModuleKey, "PersonnelFiles", "ViewRecurringDeductions"),
        new("PersonnelFiles.ManageRecurringDeductions", "Gestionar descuentos cíclicos", "Registrar, editar, suspender, cerrar y anular descuentos cíclicos, y aplicar sus cuotas (regulares y extraordinarias) por periodo.", PersonnelFilesModuleKey, "PersonnelFiles", "ManageRecurringDeductions"),
        new("PersonnelFiles.AuthorizeRecurringDeductions", "Autorizar descuentos cíclicos", "Decidir (autorizar/rechazar) y revocar los descuentos cíclicos de los empleados. No implicado por la administración de expedientes.", PersonnelFilesModuleKey, "PersonnelFiles", "AuthorizeRecurringDeductions"),
        new("PersonnelFiles.ViewIndebtedness", "Ver endeudamiento", "Consultar el nivel de endeudamiento de un empleado (base de ingreso, carga, porcentaje, límites) y simular una deducción adicional. Dato agregado sensible.", PersonnelFilesModuleKey, "PersonnelFiles", "ViewIndebtedness"),
        new("PersonnelFiles.ManageIndebtednessParameters", "Gestionar parámetros de endeudamiento", "Configurar los límites de endeudamiento por tipo de descuento cíclico de la empresa.", PersonnelFilesModuleKey, "PersonnelFiles", "ManageIndebtednessParameters"),
        new("PersonnelFiles.ViewNotWorkedTimes", "Ver tiempos no trabajados", "Consulta de los tiempos no trabajados de los expedientes, la bandeja de la empresa y sus exportaciones (insumo de planilla).", PersonnelFilesModuleKey, "PersonnelFiles", "ViewNotWorkedTimes"),
        new("PersonnelFiles.ManageNotWorkedTimes", "Gestionar tiempos no trabajados", "Registrar y anular tiempos no trabajados (ausencias, suspensiones con descuento, llegadas tardías) con su descuento calculado.", PersonnelFilesModuleKey, "PersonnelFiles", "ManageNotWorkedTimes"),
        new("PersonnelFiles.ManageNotWorkedTimeTypes", "Gestionar tipos de tiempo no trabajado", "Configurar el maestro de tipos de tiempo no trabajado de la empresa (flags de conteo, porcentaje de descuento, conceptos).", PersonnelFilesModuleKey, "PersonnelFiles", "ManageNotWorkedTimeTypes"),
        new("PersonnelFiles.ViewOneTimeIncomes", "Ver ingresos eventuales", "Consulta de los ingresos eventuales de los expedientes, la bandeja de la empresa y sus exportaciones (insumo de planilla).", PersonnelFilesModuleKey, "PersonnelFiles", "ViewOneTimeIncomes"),
        new("PersonnelFiles.ManageOneTimeIncomes", "Gestionar ingresos eventuales", "Registrar, editar y anular ingresos eventuales, y aplicarlos por periodo (unitario o en lote).", PersonnelFilesModuleKey, "PersonnelFiles", "ManageOneTimeIncomes"),
        new("PersonnelFiles.AuthorizeOneTimeIncomes", "Autorizar ingresos eventuales", "Decidir (autorizar/rechazar) y revocar los ingresos eventuales de los empleados. No implicado por la administración de expedientes.", PersonnelFilesModuleKey, "PersonnelFiles", "AuthorizeOneTimeIncomes"),
        new("PersonnelFiles.ViewOvertimeRecords", "Ver horas extras", "Consulta de las horas extras de los expedientes, la bandeja de la empresa, sus exportaciones (insumo de planilla) y los maestros de configuración de horas extras.", PersonnelFilesModuleKey, "PersonnelFiles", "ViewOvertimeRecords"),
        new("PersonnelFiles.ManageOvertimeRecords", "Gestionar horas extras", "Registrar, editar y anular horas extras, aplicarlas por periodo (unitario o en lote) y administrar los maestros de configuración de horas extras (tipos, justificaciones y carga de la plantilla).", PersonnelFilesModuleKey, "PersonnelFiles", "ManageOvertimeRecords"),
        new("PersonnelFiles.AuthorizeOvertimeRecords", "Autorizar horas extras", "Decidir (autorizar/rechazar) y revocar las horas extras de los empleados. No implicado por la administración de expedientes.", PersonnelFilesModuleKey, "PersonnelFiles", "AuthorizeOvertimeRecords"),
        new("PersonnelFiles.ViewPayrollRuns", "Ver corridas de planilla", "Consulta de las corridas de planilla: bandeja de la empresa, detalle con drill por empleado, exportaciones e historial de pagos corporativo.", PersonnelFilesModuleKey, "PersonnelFiles", "ViewPayrollRuns"),
        new("PersonnelFiles.ManagePayrollRuns", "Gestionar corridas de planilla", "Generar, ajustar (overrides/incluir-excluir), recalcular, regenerar, cerrar y anular las corridas de planilla.", PersonnelFilesModuleKey, "PersonnelFiles", "ManagePayrollRuns"),
        new("PersonnelFiles.AuthorizePayrollRuns", "Autorizar corridas de planilla", "Autorizar una corrida de planilla o devolverla con motivo. No implicado por la administración de expedientes.", PersonnelFilesModuleKey, "PersonnelFiles", "AuthorizePayrollRuns"),
        new("PersonnelFiles.ViewComplianceReports", "Ver reportes legales de planilla", "Consulta y descarga de los reportes legales de planilla: F-14, Planilla Única y Planilla Patronal (REQ-016).", PersonnelFilesModuleKey, "PersonnelFiles", "ViewComplianceReports"),
        new("PayrollConfiguration.Read", "Leer configuración de planillas", "Consulta de los maestros de configuración de planillas (nóminas y jornadas laborales).", PersonnelFilesModuleKey, "PayrollConfiguration", "Read"),
        new("PayrollConfiguration.Manage", "Administrar configuración de planillas", "Administración completa de los maestros de configuración de planillas (nóminas, jornadas laborales y carga de la plantilla).", PersonnelFilesModuleKey, "PayrollConfiguration", "Manage")
    ];
}

public sealed record ProvisioningPermissionDefinition(
    string Code,
    string Name,
    string Description,
    string Module,
    string Screen,
    string Action);

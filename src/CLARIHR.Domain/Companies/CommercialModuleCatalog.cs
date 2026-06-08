namespace CLARIHR.Domain.Companies;

public static class CommercialModuleKeys
{
    public const string Rbac = "RBAC";
    public const string Users = "USERS";
    public const string OrgStructureCatalogs = "ORG_STRUCTURE_CATALOGS";
    public const string PositionDescriptionCatalogs = "POSITION_DESCRIPTION_CATALOGS";
    public const string JobProfiles = "JOB_PROFILES";
    public const string PositionSlots = "POSITION_SLOTS";
    public const string SalaryTabulator = "SALARY_TABULATOR";
    public const string CostCenters = "COST_CENTERS";
    public const string LegalRepresentatives = "LEGAL_REPRESENTATIVES";
    public const string CompetencyFramework = "COMPETENCY_FRAMEWORK";
    public const string OrgUnits = "ORG_UNITS";
    public const string Locations = "LOCATIONS";
    public const string PersonnelFiles = "PERSONNEL_FILES";
}

public sealed record CommercialModuleDefinition(
    string ModuleKey,
    string DisplayName,
    string Description);

public static class CommercialModuleCatalog
{
    public static readonly IReadOnlyCollection<CommercialModuleDefinition> All =
    [
        new(CommercialModuleKeys.Rbac, "RBAC", "Gobierno de roles, permisos y auditoria de acceso."),
        new(CommercialModuleKeys.Users, "Users", "Administracion de usuarios de empresa y membresias operativas."),
        new(CommercialModuleKeys.OrgStructureCatalogs, "Org Structure Catalogs", "Catalogos base de estructura organizacional."),
        new(CommercialModuleKeys.PositionDescriptionCatalogs, "Position Description Catalogs", "Catalogos de descripcion y clasificacion de puestos."),
        new(CommercialModuleKeys.JobProfiles, "Job Profiles", "Perfiles y catalogos descriptivos de puestos."),
        new(CommercialModuleKeys.PositionSlots, "Position Slots", "Plazas, dependencias y ocupacion."),
        new(CommercialModuleKeys.SalaryTabulator, "Salary Tabulator", "Tabulador salarial y sus solicitudes."),
        new(CommercialModuleKeys.CostCenters, "Cost Centers", "Centros de costo y su administracion."),
        new(CommercialModuleKeys.LegalRepresentatives, "Legal Representatives", "Representantes legales y su vigencia."),
        new(CommercialModuleKeys.CompetencyFramework, "Competency Framework", "Marco de competencias, conductas y expectativas."),
        new(CommercialModuleKeys.OrgUnits, "Organization Units", "Unidades organizativas, jerarquia y grafo."),
        new(CommercialModuleKeys.Locations, "Locations", "Ubicaciones, grupos y centros de trabajo."),
        new(CommercialModuleKeys.PersonnelFiles, "Personnel Files", "Expedientes de personal y operacion asociada.")
    ];

    private static readonly IReadOnlyCollection<string> DefaultFullAccessModuleKeys = All
        .Select(static definition => definition.ModuleKey)
        .ToArray();

    public static readonly IReadOnlyCollection<string> DefaultFreeModuleKeys = DefaultFullAccessModuleKeys;

    public static readonly IReadOnlyCollection<string> DefaultMasterModuleKeys = DefaultFullAccessModuleKeys;

    private static readonly IReadOnlyDictionary<string, CommercialModuleDefinition> ByKey =
        All.ToDictionary(static definition => definition.ModuleKey, static definition => definition, StringComparer.Ordinal);

    public static bool IsKnown(string moduleKey)
    {
        if (string.IsNullOrWhiteSpace(moduleKey))
        {
            return false;
        }

        return ByKey.ContainsKey(moduleKey.Trim().ToUpperInvariant());
    }

    public static CommercialModuleDefinition Get(string moduleKey)
    {
        var normalizedKey = CompanyNormalization.NormalizeModuleKey(moduleKey);
        return ByKey.TryGetValue(normalizedKey, out var definition)
            ? definition
            : throw new ArgumentException($"Unknown commercial module key '{normalizedKey}'.", nameof(moduleKey));
    }

    public static string NormalizeKnownKey(string moduleKey) => Get(moduleKey).ModuleKey;
}

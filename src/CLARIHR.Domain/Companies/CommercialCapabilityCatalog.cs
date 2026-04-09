namespace CLARIHR.Domain.Companies;

public static class CommercialCapabilityKeys
{
    public const string RbacAdministration = "RBAC_ADMINISTRATION";
    public const string UserAdministration = "USER_ADMINISTRATION";
    public const string OrgStructureCatalogAdministration = "ORG_STRUCTURE_CATALOG_ADMINISTRATION";
    public const string PositionDescriptionCatalogAdministration = "POSITION_DESCRIPTION_CATALOG_ADMINISTRATION";
    public const string JobProfileAdministration = "JOB_PROFILE_ADMINISTRATION";
    public const string PositionSlotAdministration = "POSITION_SLOT_ADMINISTRATION";
    public const string SalaryTabulatorAdministration = "SALARY_TABULATOR_ADMINISTRATION";
    public const string CostCenterAdministration = "COST_CENTER_ADMINISTRATION";
    public const string LegalRepresentativeAdministration = "LEGAL_REPRESENTATIVE_ADMINISTRATION";
    public const string CompetencyFrameworkAdministration = "COMPETENCY_FRAMEWORK_ADMINISTRATION";
    public const string OrgUnitAdministration = "ORG_UNIT_ADMINISTRATION";
    public const string LocationAdministration = "LOCATION_ADMINISTRATION";
    public const string PersonnelFileAdministration = "PERSONNEL_FILE_ADMINISTRATION";
}

public sealed record CommercialCapabilityDefinition(
    string Code,
    string ModuleKey,
    string DisplayName,
    string Description,
    bool IsNavigable);

public static class CommercialCapabilityCatalog
{
    public static readonly IReadOnlyCollection<CommercialCapabilityDefinition> All =
    [
        new(
            CommercialCapabilityKeys.RbacAdministration,
            CommercialModuleKeys.Rbac,
            "RBAC Administration",
            "Governance for roles, permissions and access auditing.",
            IsNavigable: true),
        new(
            CommercialCapabilityKeys.UserAdministration,
            CommercialModuleKeys.Users,
            "User Administration",
            "Operational company user administration and membership management.",
            IsNavigable: true),
        new(
            CommercialCapabilityKeys.OrgStructureCatalogAdministration,
            CommercialModuleKeys.OrgStructureCatalogs,
            "Org Structure Catalog Administration",
            "Administration of the organizational structure catalogs.",
            IsNavigable: true),
        new(
            CommercialCapabilityKeys.PositionDescriptionCatalogAdministration,
            CommercialModuleKeys.PositionDescriptionCatalogs,
            "Position Description Catalog Administration",
            "Administration of position description catalogs.",
            IsNavigable: true),
        new(
            CommercialCapabilityKeys.JobProfileAdministration,
            CommercialModuleKeys.JobProfiles,
            "Job Profile Administration",
            "Administration of job profiles and their descriptive content.",
            IsNavigable: true),
        new(
            CommercialCapabilityKeys.PositionSlotAdministration,
            CommercialModuleKeys.PositionSlots,
            "Position Slot Administration",
            "Administration of position slots, occupancies and dependencies.",
            IsNavigable: true),
        new(
            CommercialCapabilityKeys.SalaryTabulatorAdministration,
            CommercialModuleKeys.SalaryTabulator,
            "Salary Tabulator Administration",
            "Administration of salary tabulator lines and requests.",
            IsNavigable: true),
        new(
            CommercialCapabilityKeys.CostCenterAdministration,
            CommercialModuleKeys.CostCenters,
            "Cost Center Administration",
            "Administration of accounting cost centers.",
            IsNavigable: true),
        new(
            CommercialCapabilityKeys.LegalRepresentativeAdministration,
            CommercialModuleKeys.LegalRepresentatives,
            "Legal Representative Administration",
            "Administration of legal representatives and their validity.",
            IsNavigable: true),
        new(
            CommercialCapabilityKeys.CompetencyFrameworkAdministration,
            CommercialModuleKeys.CompetencyFramework,
            "Competency Framework Administration",
            "Administration of competencies, behaviors and expectations.",
            IsNavigable: true),
        new(
            CommercialCapabilityKeys.OrgUnitAdministration,
            CommercialModuleKeys.OrgUnits,
            "Org Unit Administration",
            "Administration of org units and hierarchy relationships.",
            IsNavigable: true),
        new(
            CommercialCapabilityKeys.LocationAdministration,
            CommercialModuleKeys.Locations,
            "Location Administration",
            "Administration of locations, groups and work centers.",
            IsNavigable: true),
        new(
            CommercialCapabilityKeys.PersonnelFileAdministration,
            CommercialModuleKeys.PersonnelFiles,
            "Personnel File Administration",
            "Administration of personnel files and related operations.",
            IsNavigable: true)
    ];

    private static readonly IReadOnlyDictionary<string, CommercialCapabilityDefinition> ByCode =
        All.ToDictionary(static definition => definition.Code, static definition => definition, StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, CommercialCapabilityDefinition> ByModuleKey =
        All.ToDictionary(static definition => definition.ModuleKey, static definition => definition, StringComparer.Ordinal);

    public static bool IsKnown(string capabilityCode)
    {
        if (string.IsNullOrWhiteSpace(capabilityCode))
        {
            return false;
        }

        return ByCode.ContainsKey(capabilityCode.Trim().ToUpperInvariant());
    }

    public static CommercialCapabilityDefinition Get(string capabilityCode)
    {
        var normalizedCode = CompanyNormalization.NormalizeModuleKey(capabilityCode);
        return ByCode.TryGetValue(normalizedCode, out var definition)
            ? definition
            : throw new ArgumentException($"Unknown commercial capability code '{normalizedCode}'.", nameof(capabilityCode));
    }

    public static CommercialCapabilityDefinition GetByModuleKey(string moduleKey)
    {
        var normalizedModuleKey = CommercialModuleCatalog.NormalizeKnownKey(moduleKey);
        return ByModuleKey.TryGetValue(normalizedModuleKey, out var definition)
            ? definition
            : throw new ArgumentException($"Unknown commercial capability module key '{normalizedModuleKey}'.", nameof(moduleKey));
    }

    public static string NormalizeKnownCode(string capabilityCode) => Get(capabilityCode).Code;

    public static IReadOnlyCollection<string> GetCapabilityCodesForModules(IEnumerable<string> moduleKeys) =>
        moduleKeys
            .Select(GetByModuleKey)
            .Select(static definition => definition.Code)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static code => code, StringComparer.Ordinal)
            .ToArray();

    public static IReadOnlyCollection<string> GetModuleKeysForCapabilities(IEnumerable<string> capabilityCodes) =>
        capabilityCodes
            .Select(Get)
            .Select(static definition => definition.ModuleKey)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static moduleKey => moduleKey, StringComparer.Ordinal)
            .ToArray();
}

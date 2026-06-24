namespace CLARIHR.Application.Features.JobProfileCatalogTypes;

/// <summary>
/// Catalog families used by Job Profile and its sub-resources. The family decides
/// how the frontend reaches the catalog items endpoint (see
/// <see cref="JobProfileCatalogFieldBinding.ApiEndpointTemplate"/>).
/// </summary>
public static class CatalogFamilies
{
    public const string PositionDescription = "PositionDescription";
    public const string JobCatalog = "JobCatalog";
    public const string Internal = "Internal";
}

/// <summary>
/// A canonical Job Profile catalog type. This is the single source of truth for
/// (a) what the seed inserts into <c>catalog_type_descriptors</c> and (b) the
/// frontend wire contract (slug + family). <see cref="RegistryCode"/> is the stable
/// link to the registry row; it equals the enum member name for the
/// PositionDescription/JobCatalog families so the anti-drift guardrail can pin it
/// to <c>PositionDescriptionCatalogType</c>/<c>JobCatalogCategory</c>.
/// </summary>
public sealed record CatalogTypeDefinition(
    string Family,
    string RegistryCode,
    string DisplayName,
    string Slug);

/// <summary>
/// Binds one Job Profile (sub-)resource request field to a canonical catalog type.
/// The field set is fixed by the C# request DTOs, so this binding lives in code;
/// only the catalog metadata behind a <see cref="RegistryCode"/> is DB-driven.
/// </summary>
public sealed record JobProfileCatalogFieldBinding(
    string SubResource,
    string FieldName,
    string RegistryCode);

/// <summary>
/// Single source of truth for the Job Profile catalog manifest and the registry seed.
/// Pinned to the catalog enums and <c>PositionDescriptionCatalogRouteMap</c> by
/// <c>JobProfileCatalogBindingMapGuardrailsTests</c> so the contract cannot drift.
/// </summary>
public static class JobProfileCatalogBindingMap
{
    /// <summary>Job Profile sub-resources in canonical manifest order (includes the catalog-less ones).</summary>
    public static IReadOnlyList<string> SubResources { get; } =
    [
        "jobProfile",
        "requirement",
        "function",
        "competency",
        "training",
        "benefit",
        "relation",
        "workingCondition",
        "dependentPosition",
        "compensation",
    ];

    /// <summary>
    /// The ~27 canonical catalog types (13 PositionDescription + 11 JobCatalog + 3 Internal).
    /// Seeded into the registry; the order is the seed/display order.
    /// </summary>
    public static IReadOnlyList<CatalogTypeDefinition> CanonicalTypes { get; } =
    [
        // ── PositionDescription family (slug = PositionDescriptionCatalogRouteMap slug) ──
        new(CatalogFamilies.PositionDescription, "PositionFunctionType", "Position Function Type", "position-function-types"),
        new(CatalogFamilies.PositionDescription, "PositionContractType", "Position Contract Type", "position-contract-types"),
        new(CatalogFamilies.PositionDescription, "StrategicObjective", "Strategic Objective", "strategic-objectives"),
        new(CatalogFamilies.PositionDescription, "Frequency", "Frequency", "frequencies"),
        new(CatalogFamilies.PositionDescription, "RequirementType", "Requirement Type", "requirement-types"),
        new(CatalogFamilies.PositionDescription, "Requirement", "Requirement", "requirements"),
        new(CatalogFamilies.PositionDescription, "GeneralFunction", "General Function", "general-functions"),
        new(CatalogFamilies.PositionDescription, "SalaryClass", "Salary Class", "salary-classes"),
        new(CatalogFamilies.PositionDescription, "WorkEquipment", "Work Equipment", "work-equipments"),
        new(CatalogFamilies.PositionDescription, "Responsibility", "Responsibility", "responsibilities-catalog"),
        new(CatalogFamilies.PositionDescription, "Benefit", "Benefit", "benefits-catalog"),
        new(CatalogFamilies.PositionDescription, "WorkConditionType", "Work Condition Type", "work-condition-types"),
        new(CatalogFamilies.PositionDescription, "WorkCondition", "Work Condition", "work-conditions"),
        new(CatalogFamilies.PositionDescription, "CompetencyDomain", "Competency Domain", "competency-domains"),

        // ── JobCatalog family (slug = JobCatalogCategory enum member name) ──
        new(CatalogFamilies.JobCatalog, "EducationLevel", "Education Level", "EducationLevel"),
        new(CatalogFamilies.JobCatalog, "KnowledgeArea", "Knowledge Area", "KnowledgeArea"),
        new(CatalogFamilies.JobCatalog, "Competency", "Competency", "Competency"),
        new(CatalogFamilies.JobCatalog, "Training", "Training", "Training"),
        new(CatalogFamilies.JobCatalog, "BenefitType", "Benefit Type", "BenefitType"),
        new(CatalogFamilies.JobCatalog, "WorkingCondition", "Working Condition", "WorkingCondition"),
        new(CatalogFamilies.JobCatalog, "RelationType", "Relation Type", "RelationType"),
        new(CatalogFamilies.JobCatalog, "DecisionLevel", "Decision Level", "DecisionLevel"),
        new(CatalogFamilies.JobCatalog, "CompetencyType", "Competency Type", "CompetencyType"),
        new(CatalogFamilies.JobCatalog, "BehaviorLevel", "Behavior Level", "BehaviorLevel"),
        new(CatalogFamilies.JobCatalog, "Behavior", "Behavior", "Behavior"),

        // ── Internal family (slug = InternalCatalogRegistry catalog key) ──
        new(CatalogFamilies.Internal, "RequirementsEducation", "Requirements: Education", "job-profile.requirements.education"),
        new(CatalogFamilies.Internal, "RequirementsKnowledge", "Requirements: Knowledge", "job-profile.requirements.knowledge"),
        new(CatalogFamilies.Internal, "RequirementsCertification", "Requirements: Certification", "job-profile.requirements.certification"),
    ];

    /// <summary>
    /// Maps each Job Profile (sub-)resource catalog field to a canonical type.
    /// A field may have several bindings when it accepts more than one catalog
    /// (e.g. the polymorphic requirement <c>catalogItemPublicId</c>).
    /// </summary>
    public static IReadOnlyList<JobProfileCatalogFieldBinding> FieldBindings { get; } =
    [
        new("jobProfile", "positionCategoryPublicId", "PositionFunctionType"),
        new("jobProfile", "strategicObjectiveCatalogItemPublicId", "StrategicObjective"),
        new("jobProfile", "assignedWorkEquipmentCatalogItemPublicId", "WorkEquipment"),
        new("jobProfile", "responsibilityCatalogItemPublicId", "Responsibility"),

        new("requirement", "requirementTypeCatalogItemPublicId", "RequirementType"),
        new("requirement", "catalogItemPublicId", "EducationLevel"),
        new("requirement", "catalogItemPublicId", "KnowledgeArea"),
        new("requirement", "catalogItemPublicId", "RequirementsEducation"),
        new("requirement", "catalogItemPublicId", "RequirementsKnowledge"),
        new("requirement", "catalogItemPublicId", "RequirementsCertification"),

        new("function", "frequencyCatalogItemPublicId", "Frequency"),

        new("competency", "catalogItemPublicId", "Competency"),
        new("training", "catalogItemPublicId", "Training"),
        new("benefit", "catalogItemPublicId", "BenefitType"),
        new("relation", "catalogItemPublicId", "RelationType"),

        new("workingCondition", "workConditionTypeCatalogItemPublicId", "WorkConditionType"),
        new("workingCondition", "catalogItemPublicId", "WorkCondition"),
    ];

    public static string ApiEndpointTemplate(string family, string slug) => family switch
    {
        CatalogFamilies.PositionDescription =>
            $"/api/v1/companies/{{companyId}}/position-description-catalogs/{slug}/items",
        CatalogFamilies.JobCatalog =>
            $"/api/v1/companies/{{companyId}}/job-catalogs/{slug}",
        CatalogFamilies.Internal =>
            $"/api/v1/job-profiles/internal-catalogs/{slug}/values",
        _ => string.Empty,
    };
}

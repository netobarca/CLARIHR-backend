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

    /// <summary>
    /// H-10 — tenant-scoped resources of the position structure that are NOT item catalogs: they have their own
    /// controllers and their rows reference each other (a category hangs off a classification, which combines
    /// the function, contract and hierarchy axes). The manifest had no vocabulary for them, so
    /// <c>positionCategoryPublicId</c> ended up bound to <c>PositionFunctionType</c> — the nearest available
    /// thing, and a list whose every option the field rejects.
    /// </summary>
    public const string PositionStructure = "PositionStructure";

    /// <summary>
    /// H-10 — organization structure catalogs, from another module. The job profile chain reaches them because
    /// a position category classification needs an <c>orgUnitType</c>, and that field is required. The coupling
    /// is conceptual, not compile-time: this map is strings, so nothing here references that module's types.
    /// What it does introduce is drift risk — its route segments are literals in the controller attributes, with
    /// no route map to pin the slug against (unlike <see cref="PositionDescription"/>). The end-to-end manifest
    /// tests are the guard: they walk the published URL instead of comparing it.
    /// </summary>
    public const string OrgStructure = "OrgStructure";
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

        // H-10 — not a sub-resource of the job profile: it is the resource that FEEDS
        // `jobProfile.positionCategoryPublicId`. It earns a place here because its own reference
        // (`classificationPublicId`) had nowhere else to be published, and a frontend building the position
        // structure screens needs that endpoint from the same map. Appended last so no existing index moves.
        "positionCategory",

        // H-10 — same reasoning one level up. Publishing only `classificationPublicId` let the frontend CHOOSE a
        // classification but not CREATE one: the three axes it is built from are all required. Covering a form
        // halfway is worse than not covering it, so the chain is closed here:
        // unit-type / function-type / contract-type → classification → category → profile.
        "positionCategoryClassification",
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

        // ── PositionStructure family (slug = the resource's own route segment) ──
        // Order is the dependency order: a category cannot exist without its classification.
        new(CatalogFamilies.PositionStructure, "PositionCategoryClassification", "Position Category Classification", "position-category-classifications"),
        new(CatalogFamilies.PositionStructure, "PositionCategory", "Position Category", "position-categories"),

        // ── OrgStructure family (slug = the collection segment under organization-structure-catalogs) ──
        // Only the unit type: it is the third axis of a classification and the only one of the three that was
        // not already a canonical type. `functional-areas`, the other collection of that module, stays out —
        // nothing in this chain references it.
        new(CatalogFamilies.OrgStructure, "OrgUnitType", "Org Unit Type", "unit-types"),

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
        // H-10 — was bound to `PositionFunctionType`. That is the first AXIS of the classification a category
        // hangs off, not the category: the ids live in different tables, so every option the manifest offered
        // was rejected with `POSITION_CATEGORY_NOT_FOUND`. And the mapping is 1→N, not 1→1 — one function type
        // (`OPERATIVA`) can fan out into several categories, and a function type can have none.
        new("jobProfile", "positionCategoryPublicId", "PositionCategory"),
        new("jobProfile", "strategicObjectiveCatalogItemPublicId", "StrategicObjective"),
        new("jobProfile", "assignedWorkEquipmentCatalogItemPublicId", "WorkEquipment"),
        new("jobProfile", "responsibilityCatalogItemPublicId", "Responsibility"),

        new("requirement", "requirementTypeCatalogItemPublicId", "RequirementType"),

        // H-07 — `catalogItemPublicId` resuelve SOLO contra los job catalogs, y solo dos tipos de requisito
        // tienen categoría equivalente: Education→EducationLevel y Knowledge→KnowledgeArea. Cualquier otra
        // combinación se rechaza (JOB_PROFILE_REQUIREMENT_CATALOG_CATEGORY_MISMATCH / _NOT_APPLICABLE).
        new("requirement", "catalogItemPublicId", "EducationLevel"),
        new("requirement", "catalogItemPublicId", "KnowledgeArea"),

        // Los catálogos internos alimentan `description`, NO `catalogItemPublicId`: para estos tipos el
        // `description` se auto-resuelve o se crea en `job-profile.requirements.*`. Estaban declarados sobre
        // `catalogItemPublicId`, así que el manifiesto ofrecía CINCO listas para un solo campo y un frontend
        // que lo leyera literal habría propuesto tres que ese campo rechaza.
        new("requirement", "description", "RequirementsEducation"),
        new("requirement", "description", "RequirementsKnowledge"),
        new("requirement", "description", "RequirementsCertification"),

        new("function", "frequencyCatalogItemPublicId", "Frequency"),

        new("competency", "catalogItemPublicId", "Competency"),
        new("training", "catalogItemPublicId", "Training"),
        new("benefit", "catalogItemPublicId", "BenefitType"),
        new("relation", "catalogItemPublicId", "RelationType"),

        new("workingCondition", "workConditionTypeCatalogItemPublicId", "WorkConditionType"),
        new("workingCondition", "catalogItemPublicId", "WorkCondition"),

        // H-10 — the field names are the WIRE names the frontend sends, not the C# properties.
        new("positionCategory", "classificationPublicId", "PositionCategoryClassification"),

        new("positionCategoryClassification", "positionFunctionTypePublicId", "PositionFunctionType"),
        new("positionCategoryClassification", "positionContractTypePublicId", "PositionContractType"),
        new("positionCategoryClassification", "orgUnitTypePublicId", "OrgUnitType"),
    ];

    public static string ApiEndpointTemplate(string family, string slug) => family switch
    {
        CatalogFamilies.PositionDescription =>
            $"/api/v1/companies/{{companyId}}/position-description-catalogs/{slug}/items",
        CatalogFamilies.JobCatalog =>
            $"/api/v1/companies/{{companyId}}/job-catalogs/{slug}",
        CatalogFamilies.Internal =>
            $"/api/v1/job-profiles/internal-catalogs/{slug}/values",
        // H-10 — these resources are not `/items` collections under a catalog type: the slug IS the route
        // segment, so the template is the tenant-scoped collection itself.
        CatalogFamilies.PositionStructure =>
            $"/api/v1/companies/{{companyId}}/{slug}",
        CatalogFamilies.OrgStructure =>
            $"/api/v1/companies/{{companyId}}/organization-structure-catalogs/{slug}",
        _ => string.Empty,
    };
}

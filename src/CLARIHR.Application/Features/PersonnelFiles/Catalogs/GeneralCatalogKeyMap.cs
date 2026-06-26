using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// GC4 single source of truth for the public <c>catalogKey</c> wire segment ⇄ internal catalog
/// <c>Category</c> mapping consumed by <c>GeneralCatalogsController</c>. Previously the controller
/// hard-coded two <c>switch</c> blocks of category string literals that duplicated (and silently
/// drifted from) the <see cref="PersonnelCurriculumCatalogCategories"/> /
/// <see cref="PersonnelReferenceCatalogCategories"/> constants and the repository's category
/// <c>switch</c>. The map keys off the category <b>constants</b> (never literals); the
/// <c>GeneralCatalogKeyMapGuardrailsTests</c> completeness guardrail asserts a bijection between
/// each constant set and its map, so adding a category constant or renaming one without wiring the
/// wire key (or vice versa) fails loudly instead of producing a silent <c>400</c>.
/// </summary>
public static class GeneralCatalogKeyMap
{
    /// <summary>
    /// <c>general-catalogs/{catalogKey}</c> segment → curriculum/system/country-scoped catalog category.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> CatalogKeys =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["languages"] = PersonnelCurriculumCatalogCategories.Language,
            ["language-levels"] = PersonnelCurriculumCatalogCategories.LanguageLevel,
            ["training-types"] = PersonnelCurriculumCatalogCategories.TrainingType,
            ["assignment-types"] = PersonnelCurriculumCatalogCategories.AssignmentType,
            ["substitution-types"] = PersonnelCurriculumCatalogCategories.SubstitutionType,
            ["medical-claim-types"] = PersonnelCurriculumCatalogCategories.MedicalClaimType,
            ["medical-claim-status"] = PersonnelCurriculumCatalogCategories.MedicalClaimStatus,
            ["employment-statuses"] = PersonnelCurriculumCatalogCategories.EmploymentStatus,
            ["duration-units"] = PersonnelCurriculumCatalogCategories.DurationUnit,
            ["reference-types"] = PersonnelCurriculumCatalogCategories.ReferenceType,
            ["currencies"] = PersonnelCurriculumCatalogCategories.Currency,
            ["countries"] = PersonnelCurriculumCatalogCategories.Country,
            ["education-statuses"] = PersonnelCurriculumCatalogCategories.EducationStatus,
            ["education-study-types"] = PersonnelCurriculumCatalogCategories.StudyType,
            ["education-shifts"] = PersonnelCurriculumCatalogCategories.Shift,
            ["education-modalities"] = PersonnelCurriculumCatalogCategories.Modality,
            ["education-careers"] = PersonnelCurriculumCatalogCategories.Career,
            ["file-document-types"] = PersonnelCurriculumCatalogCategories.FileDocumentType,
            ["banks"] = PersonnelCurriculumCatalogCategories.Bank,
            ["compensation-concept-types"] = PersonnelCurriculumCatalogCategories.CompensationConceptType,
            ["pay-periods"] = PersonnelCurriculumCatalogCategories.PayPeriod,
            ["calculation-bases"] = PersonnelCurriculumCatalogCategories.CalculationBase,
            ["payment-methods"] = PersonnelCurriculumCatalogCategories.PaymentMethod,
            ["asset-access-types"] = PersonnelCurriculumCatalogCategories.AssetAccessType,
            ["delivery-statuses"] = PersonnelCurriculumCatalogCategories.DeliveryStatus,
            ["experience-metrics"] = PersonnelCurriculumCatalogCategories.ExperienceMetric,
            ["off-payroll-transaction-types"] = PersonnelCurriculumCatalogCategories.OffPayrollTransactionType,
            ["form-control-types"] = PersonnelCurriculumCatalogCategories.FormControlType,
        };

    /// <summary>
    /// <c>reference-catalogs/{catalogKey}</c> segment → country-scoped reference catalog category.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ReferenceCatalogKeys =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["professions"] = PersonnelReferenceCatalogCategories.Profession,
            ["marital-statuses"] = PersonnelReferenceCatalogCategories.MaritalStatus,
            ["identification-types"] = PersonnelReferenceCatalogCategories.IdentificationType,
            ["kinships"] = PersonnelReferenceCatalogCategories.Kinship,
            ["departments"] = PersonnelReferenceCatalogCategories.Department,
            ["municipalities"] = PersonnelReferenceCatalogCategories.Municipality,
            ["insurance-types"] = PersonnelReferenceCatalogCategories.InsuranceType,
            ["insurance-ranges"] = PersonnelReferenceCatalogCategories.InsuranceRange,
            ["retirement-categories"] = PersonnelReferenceCatalogCategories.RetirementCategory,
            ["retirement-reasons"] = PersonnelReferenceCatalogCategories.RetirementReason,
        };

    /// <summary>Resolves a <c>general-catalogs</c> wire key to its category; <c>false</c> for an unknown key.</summary>
    public static bool TryResolveCatalogCategory(string? catalogKey, out string category) =>
        TryResolve(CatalogKeys, catalogKey, out category);

    /// <summary>Resolves a <c>reference-catalogs</c> wire key to its category; <c>false</c> for an unknown key.</summary>
    public static bool TryResolveReferenceCategory(string? catalogKey, out string category) =>
        TryResolve(ReferenceCatalogKeys, catalogKey, out category);

    private static bool TryResolve(IReadOnlyDictionary<string, string> map, string? catalogKey, out string category)
    {
        if (!string.IsNullOrWhiteSpace(catalogKey) &&
            map.TryGetValue(catalogKey.Trim(), out var resolved))
        {
            category = resolved;
            return true;
        }

        category = string.Empty;
        return false;
    }
}

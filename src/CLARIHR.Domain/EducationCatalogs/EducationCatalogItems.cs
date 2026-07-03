using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.EducationCatalogs;

/// <summary>
/// Base class for all education catalog items.
/// These are system-wide catalogs managed by Backoffice with no country scope.
/// </summary>
public abstract class EducationCatalogItem : SystemScopedCatalogItem
{
    protected EducationCatalogItem()
    {
    }

    protected EducationCatalogItem(
        Guid publicId,
        string code,
        string name,
        int sortOrder)
        : base(publicId, code, name, isActive: true, sortOrder)
    {
    }
}

public sealed class EducationStatusCatalogItem : EducationCatalogItem
{
    private EducationStatusCatalogItem()
    {
    }

    private EducationStatusCatalogItem(Guid publicId, string code, string name, int sortOrder)
        : base(publicId, code, name, sortOrder)
    {
    }

    public static EducationStatusCatalogItem Create(string code, string name, int sortOrder) =>
        new(Guid.NewGuid(), code, name, sortOrder);
}

/// <summary>
/// Global catalog of education LEVELS (RF-014): the ordered ladder (Básico → Medio → Técnico →
/// Superior → Posgrado) each study type maps to. System-scoped like the rest of the education family.
/// </summary>
public sealed class EducationLevelCatalogItem : EducationCatalogItem
{
    private EducationLevelCatalogItem()
    {
    }

    private EducationLevelCatalogItem(Guid publicId, string code, string name, int sortOrder)
        : base(publicId, code, name, sortOrder)
    {
    }

    public static EducationLevelCatalogItem Create(string code, string name, int sortOrder) =>
        new(Guid.NewGuid(), code, name, sortOrder);
}

public sealed class EducationStudyTypeCatalogItem : EducationCatalogItem
{
    private EducationStudyTypeCatalogItem()
    {
    }

    private EducationStudyTypeCatalogItem(
        Guid publicId,
        string code,
        string name,
        int sortOrder,
        string? abbreviation,
        long? educationLevelCatalogItemId)
        : base(publicId, code, name, sortOrder)
    {
        Abbreviation = NormalizeOptionalAbbreviation(abbreviation);
        EducationLevelCatalogItemId = educationLevelCatalogItemId;
    }

    /// <summary>Optional short label (RF-008), seed-delivered (DP-03).</summary>
    public string? Abbreviation { get; private set; }

    /// <summary>
    /// Optional FK to the education level (RF-008 parte 2). Nullable so the thin backoffice CRUD
    /// (code/name/sortOrder) keeps working (DP-03); the seed maps every SV study type to its level.
    /// </summary>
    public long? EducationLevelCatalogItemId { get; private set; }

    public EducationLevelCatalogItem? EducationLevelCatalogItem { get; private set; }

    public static EducationStudyTypeCatalogItem Create(
        string code,
        string name,
        int sortOrder,
        string? abbreviation = null,
        long? educationLevelCatalogItemId = null) =>
        new(Guid.NewGuid(), code, name, sortOrder, abbreviation, educationLevelCatalogItemId);

    private static string? NormalizeOptionalAbbreviation(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}

/// <summary>
/// COUNTRY-scoped enriched catalog of careers (RF-009, DP-06 — converted from the former global
/// education catalog by drop &amp; recreate, RT-02). Carries the abbreviation, the salary-increment
/// percentage per degree (RT-03, consumed by the future payroll), the "officially recognized" flag and
/// a required FK to its study type (Caso C). Administered by seed only in this phase (career CRUD is
/// deferred, DP-03); consumed read-only via <c>general-catalogs/education-careers</c>.
/// </summary>
public sealed class EducationCareerCatalogItem : CountryScopedCatalogItem
{
    private EducationCareerCatalogItem()
    {
    }

    private EducationCareerCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder,
        string? abbreviation,
        decimal increment,
        bool isRecognized,
        long educationStudyTypeCatalogItemId)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
        Abbreviation = string.IsNullOrWhiteSpace(abbreviation) ? null : abbreviation.Trim().ToUpperInvariant();
        SetIncrement(increment);
        IsRecognized = isRecognized;
        SetStudyType(educationStudyTypeCatalogItemId);
    }

    /// <summary>Optional short label (II, LAE…), seed-delivered (DP-03).</summary>
    public string? Abbreviation { get; private set; }

    /// <summary>Salary-increment percentage per degree (RT-03): decimal 0–100, seeded 0, consumed by payroll.</summary>
    public decimal Increment { get; private set; }

    /// <summary>True when the career is officially recognized (MINED/acreditación).</summary>
    public bool IsRecognized { get; private set; }

    public long EducationStudyTypeCatalogItemId { get; private set; }

    public EducationStudyTypeCatalogItem? EducationStudyTypeCatalogItem { get; private set; }

    public static EducationCareerCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder,
        string? abbreviation,
        decimal increment,
        bool isRecognized,
        long educationStudyTypeCatalogItemId) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder, abbreviation, increment, isRecognized, educationStudyTypeCatalogItemId);

    private void SetIncrement(decimal increment)
    {
        if (increment is < 0m or > 100m)
        {
            throw new ArgumentOutOfRangeException(nameof(increment), "Increment must be a percentage between 0 and 100.");
        }

        Increment = increment;
    }

    private void SetStudyType(long educationStudyTypeCatalogItemId)
    {
        if (educationStudyTypeCatalogItemId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(educationStudyTypeCatalogItemId), "Education study type catalog item id cannot be zero.");
        }

        EducationStudyTypeCatalogItemId = educationStudyTypeCatalogItemId;
    }
}

public sealed class EducationShiftCatalogItem : EducationCatalogItem
{
    private EducationShiftCatalogItem()
    {
    }

    private EducationShiftCatalogItem(Guid publicId, string code, string name, int sortOrder)
        : base(publicId, code, name, sortOrder)
    {
    }

    public static EducationShiftCatalogItem Create(string code, string name, int sortOrder) =>
        new(Guid.NewGuid(), code, name, sortOrder);
}

public sealed class EducationModalityCatalogItem : EducationCatalogItem
{
    private EducationModalityCatalogItem()
    {
    }

    private EducationModalityCatalogItem(Guid publicId, string code, string name, int sortOrder)
        : base(publicId, code, name, sortOrder)
    {
    }

    public static EducationModalityCatalogItem Create(string code, string name, int sortOrder) =>
        new(Guid.NewGuid(), code, name, sortOrder);
}

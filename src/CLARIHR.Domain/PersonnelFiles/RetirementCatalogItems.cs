namespace CLARIHR.Domain.PersonnelFiles;

/// <summary>
/// HRIS classification of a retirement/separation category, used to roll up turnover reports
/// (D-02). Persisted as a string on <see cref="RetirementCategoryCatalogItem"/>.
/// </summary>
public enum RetirementSeparationType
{
    Voluntaria = 0,
    Involuntaria = 1,
    Otra = 2,
}

/// <summary>
/// Country-scoped catalog of retirement/separation categories (e.g. VOLUNTARIA, JUBILACION,
/// INVOLUNTARIA…). Each category carries a <see cref="RetirementSeparationType"/> classification for
/// reporting roll-up. Parent of <see cref="RetirementReasonCatalogItem"/>. Mirrors the
/// InsuranceType → InsuranceRange hierarchical pattern.
/// </summary>
public sealed class RetirementCategoryCatalogItem : PersonnelReferenceCatalogItemBase
{
    private RetirementCategoryCatalogItem()
    {
    }

    private RetirementCategoryCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder,
        RetirementSeparationType separationType)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
        SeparationType = separationType;
    }

    public RetirementSeparationType SeparationType { get; private set; }

    public static RetirementCategoryCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder,
        RetirementSeparationType separationType) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder, separationType);
}

/// <summary>
/// Country-scoped catalog of specific retirement reasons (e.g. MEJOR_OFERTA_SALARIAL), each belonging
/// to exactly one <see cref="RetirementCategoryCatalogItem"/>. The reason code can repeat under
/// different categories, so the unique index is (country, category, code).
/// </summary>
public sealed class RetirementReasonCatalogItem : PersonnelReferenceCatalogItemBase
{
    private RetirementReasonCatalogItem()
    {
    }

    private RetirementReasonCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder,
        long retirementCategoryCatalogItemId)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
        SetCategory(retirementCategoryCatalogItemId);
    }

    public long RetirementCategoryCatalogItemId { get; private set; }

    public RetirementCategoryCatalogItem? RetirementCategoryCatalogItem { get; private set; }

    public static RetirementReasonCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder,
        long retirementCategoryCatalogItemId) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder, retirementCategoryCatalogItemId);

    private void SetCategory(long retirementCategoryCatalogItemId)
    {
        if (retirementCategoryCatalogItemId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(retirementCategoryCatalogItemId), "Retirement category catalog item id cannot be zero.");
        }

        RetirementCategoryCatalogItemId = retirementCategoryCatalogItemId;
        RefreshConcurrencyToken();
    }
}

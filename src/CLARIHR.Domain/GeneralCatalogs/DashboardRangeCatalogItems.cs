namespace CLARIHR.Domain.GeneralCatalogs;

/// <summary>
/// Country-scoped, parametrizable catalog of AGE ranges used to bucket the HR analytics dashboard's age
/// distribution (business decision D-10). Bounds are whole YEARS: <see cref="LowerBoundYears"/> is inclusive,
/// <see cref="UpperBoundYears"/> is inclusive, and a null upper bound means the range is open-ended (e.g. "56+").
/// Seeded for SV via HasData; editable per country (admin CRUD deferred to a later phase).
/// </summary>
public sealed class AgeRangeCatalogItem : GeneralCatalogItem
{
    private AgeRangeCatalogItem()
    {
    }

    private AgeRangeCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder,
        int lowerBoundYears,
        int? upperBoundYears)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
        SetBounds(lowerBoundYears, upperBoundYears);
    }

    public int LowerBoundYears { get; private set; }

    public int? UpperBoundYears { get; private set; }

    public static AgeRangeCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder,
        int lowerBoundYears,
        int? upperBoundYears) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder, lowerBoundYears, upperBoundYears);

    private void SetBounds(int lowerBoundYears, int? upperBoundYears)
    {
        if (lowerBoundYears < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lowerBoundYears), "Lower bound (years) must be greater than or equal to zero.");
        }

        if (upperBoundYears.HasValue && upperBoundYears.Value < lowerBoundYears)
        {
            throw new ArgumentOutOfRangeException(nameof(upperBoundYears), "Upper bound (years) must be greater than or equal to the lower bound.");
        }

        LowerBoundYears = lowerBoundYears;
        UpperBoundYears = upperBoundYears;
    }
}

/// <summary>
/// Country-scoped, parametrizable catalog of SENIORITY (antigüedad) ranges used to bucket the HR analytics
/// dashboard's seniority distribution (D-10). Bounds are whole MONTHS: <see cref="LowerBoundMonths"/> is
/// inclusive, <see cref="UpperBoundMonths"/> is inclusive, and a null upper bound means open-ended (e.g. "10+ años").
/// Seeded for SV via HasData; editable per country (admin CRUD deferred).
/// </summary>
public sealed class SeniorityRangeCatalogItem : GeneralCatalogItem
{
    private SeniorityRangeCatalogItem()
    {
    }

    private SeniorityRangeCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder,
        int lowerBoundMonths,
        int? upperBoundMonths)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
        SetBounds(lowerBoundMonths, upperBoundMonths);
    }

    public int LowerBoundMonths { get; private set; }

    public int? UpperBoundMonths { get; private set; }

    public static SeniorityRangeCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder,
        int lowerBoundMonths,
        int? upperBoundMonths) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder, lowerBoundMonths, upperBoundMonths);

    private void SetBounds(int lowerBoundMonths, int? upperBoundMonths)
    {
        if (lowerBoundMonths < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lowerBoundMonths), "Lower bound (months) must be greater than or equal to zero.");
        }

        if (upperBoundMonths.HasValue && upperBoundMonths.Value < lowerBoundMonths)
        {
            throw new ArgumentOutOfRangeException(nameof(upperBoundMonths), "Upper bound (months) must be greater than or equal to the lower bound.");
        }

        LowerBoundMonths = lowerBoundMonths;
        UpperBoundMonths = upperBoundMonths;
    }
}

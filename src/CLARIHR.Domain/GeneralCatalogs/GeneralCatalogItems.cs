using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.GeneralCatalogs;

public abstract class GeneralCatalogItem : TenantEntity
{
    protected GeneralCatalogItem()
    {
    }

    protected GeneralCatalogItem(
        Guid publicId,
        string code,
        string name,
        bool isSystem,
        bool isActive,
        int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "SortOrder cannot be negative.");
        }

        PublicId = publicId;
        SetCode(code);
        SetName(name);
        IsSystem = isSystem;
        IsActive = isActive;
        SortOrder = sortOrder;
        ConcurrencyToken = Guid.NewGuid();
    }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public bool IsSystem { get; private set; }

    public bool IsActive { get; private set; }

    public int SortOrder { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    private void SetCode(string value)
    {
        Code = Clean(value, nameof(value)).ToUpperInvariant();
        NormalizedCode = Code;
    }

    private void SetName(string value)
    {
        Name = Clean(value, nameof(value));
        NormalizedName = Name.ToUpperInvariant();
    }

    private static string Clean(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{parameterName} is required.", parameterName);
        }

        return value.Trim();
    }
}

public sealed class LanguageCatalogItem : GeneralCatalogItem
{
    private LanguageCatalogItem()
    {
    }

    private LanguageCatalogItem(
        Guid publicId,
        string code,
        string name,
        bool isSystem,
        bool isActive,
        int sortOrder)
        : base(publicId, code, name, isSystem, isActive, sortOrder)
    {
    }

    public static LanguageCatalogItem Create(
        string code,
        string name,
        bool isSystem,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), code, name, isSystem, isActive, sortOrder);
}

public sealed class LanguageLevelCatalogItem : GeneralCatalogItem
{
    private LanguageLevelCatalogItem()
    {
    }

    private LanguageLevelCatalogItem(
        Guid publicId,
        string code,
        string name,
        bool isSystem,
        bool isActive,
        int sortOrder)
        : base(publicId, code, name, isSystem, isActive, sortOrder)
    {
    }

    public static LanguageLevelCatalogItem Create(
        string code,
        string name,
        bool isSystem,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), code, name, isSystem, isActive, sortOrder);
}

public sealed class TrainingTypeCatalogItem : GeneralCatalogItem
{
    private TrainingTypeCatalogItem()
    {
    }

    private TrainingTypeCatalogItem(
        Guid publicId,
        string code,
        string name,
        bool isSystem,
        bool isActive,
        int sortOrder)
        : base(publicId, code, name, isSystem, isActive, sortOrder)
    {
    }

    public static TrainingTypeCatalogItem Create(
        string code,
        string name,
        bool isSystem,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), code, name, isSystem, isActive, sortOrder);
}

public sealed class DurationUnitCatalogItem : GeneralCatalogItem
{
    private DurationUnitCatalogItem()
    {
    }

    private DurationUnitCatalogItem(
        Guid publicId,
        string code,
        string name,
        bool isSystem,
        bool isActive,
        int sortOrder)
        : base(publicId, code, name, isSystem, isActive, sortOrder)
    {
    }

    public static DurationUnitCatalogItem Create(
        string code,
        string name,
        bool isSystem,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), code, name, isSystem, isActive, sortOrder);
}

public sealed class ReferenceTypeCatalogItem : GeneralCatalogItem
{
    private ReferenceTypeCatalogItem()
    {
    }

    private ReferenceTypeCatalogItem(
        Guid publicId,
        string code,
        string name,
        bool isSystem,
        bool isActive,
        int sortOrder)
        : base(publicId, code, name, isSystem, isActive, sortOrder)
    {
    }

    public static ReferenceTypeCatalogItem Create(
        string code,
        string name,
        bool isSystem,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), code, name, isSystem, isActive, sortOrder);
}

public sealed class CurrencyCatalogItem : GeneralCatalogItem
{
    private CurrencyCatalogItem()
    {
    }

    private CurrencyCatalogItem(
        Guid publicId,
        string code,
        string name,
        bool isSystem,
        bool isActive,
        int sortOrder)
        : base(publicId, code, name, isSystem, isActive, sortOrder)
    {
    }

    public static CurrencyCatalogItem Create(
        string code,
        string name,
        bool isSystem,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), code, name, isSystem, isActive, sortOrder);
}

using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.GeneralCatalogs;

public abstract class GeneralCatalogItem : CountryScopedCatalogItem
{
    protected GeneralCatalogItem()
    {
    }

    protected GeneralCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }
}

public sealed class LanguageCatalogItem : GeneralCatalogItem
{
    private LanguageCatalogItem()
    {
    }

    private LanguageCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static LanguageCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

public sealed class LanguageLevelCatalogItem : GeneralCatalogItem
{
    private LanguageLevelCatalogItem()
    {
    }

    private LanguageLevelCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static LanguageLevelCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

public sealed class TrainingTypeCatalogItem : GeneralCatalogItem
{
    private TrainingTypeCatalogItem()
    {
    }

    private TrainingTypeCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static TrainingTypeCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

public sealed class AssignmentTypeCatalogItem : GeneralCatalogItem
{
    private AssignmentTypeCatalogItem()
    {
    }

    private AssignmentTypeCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static AssignmentTypeCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

public sealed class EmploymentStatusCatalogItem : GeneralCatalogItem
{
    private EmploymentStatusCatalogItem()
    {
    }

    private EmploymentStatusCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static EmploymentStatusCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

public sealed class DurationUnitCatalogItem : GeneralCatalogItem
{
    private DurationUnitCatalogItem()
    {
    }

    private DurationUnitCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static DurationUnitCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

public sealed class ReferenceTypeCatalogItem : GeneralCatalogItem
{
    private ReferenceTypeCatalogItem()
    {
    }

    private ReferenceTypeCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static ReferenceTypeCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

public sealed class CurrencyCatalogItem : GeneralCatalogItem
{
    private CurrencyCatalogItem()
    {
    }

    private CurrencyCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
    }

    public static CurrencyCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

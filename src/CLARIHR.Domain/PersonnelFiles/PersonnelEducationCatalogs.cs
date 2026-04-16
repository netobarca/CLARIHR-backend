using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.PersonnelFiles;

public abstract class PersonnelEducationCatalogItem : CountryScopedCatalogItem
{
    protected PersonnelEducationCatalogItem()
    {
    }

    protected PersonnelEducationCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive: true, sortOrder)
    {
    }
}

public sealed class EducationStatusCatalogItem : PersonnelEducationCatalogItem
{
    private EducationStatusCatalogItem()
    {
    }

    private EducationStatusCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, sortOrder)
    {
    }

    public static EducationStatusCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, sortOrder);
}

public sealed class EducationStudyTypeCatalogItem : PersonnelEducationCatalogItem
{
    private EducationStudyTypeCatalogItem()
    {
    }

    private EducationStudyTypeCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, sortOrder)
    {
    }

    public static EducationStudyTypeCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, sortOrder);
}

public sealed class EducationCareerCatalogItem : PersonnelEducationCatalogItem
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
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, sortOrder)
    {
    }

    public static EducationCareerCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, sortOrder);
}

public sealed class EducationShiftCatalogItem : PersonnelEducationCatalogItem
{
    private EducationShiftCatalogItem()
    {
    }

    private EducationShiftCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, sortOrder)
    {
    }

    public static EducationShiftCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, sortOrder);
}

public sealed class EducationModalityCatalogItem : PersonnelEducationCatalogItem
{
    private EducationModalityCatalogItem()
    {
    }

    private EducationModalityCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        int sortOrder)
        : base(publicId, countryCatalogItemId, countryCode, code, name, sortOrder)
    {
    }

    public static EducationModalityCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, sortOrder);
}

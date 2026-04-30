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

public sealed class EducationStudyTypeCatalogItem : EducationCatalogItem
{
    private EducationStudyTypeCatalogItem()
    {
    }

    private EducationStudyTypeCatalogItem(Guid publicId, string code, string name, int sortOrder)
        : base(publicId, code, name, sortOrder)
    {
    }

    public static EducationStudyTypeCatalogItem Create(string code, string name, int sortOrder) =>
        new(Guid.NewGuid(), code, name, sortOrder);
}

public sealed class EducationCareerCatalogItem : EducationCatalogItem
{
    private EducationCareerCatalogItem()
    {
    }

    private EducationCareerCatalogItem(Guid publicId, string code, string name, int sortOrder)
        : base(publicId, code, name, sortOrder)
    {
    }

    public static EducationCareerCatalogItem Create(string code, string name, int sortOrder) =>
        new(Guid.NewGuid(), code, name, sortOrder);
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

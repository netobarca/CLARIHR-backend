using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.PersonnelFiles;

public abstract class PersonnelEducationCatalogItem : TenantEntity
{
    protected PersonnelEducationCatalogItem()
    {
    }

    protected PersonnelEducationCatalogItem(
        Guid publicId,
        string code,
        string name,
        int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order cannot be negative.");
        }

        PublicId = publicId;
        SetCode(code);
        SetName(name);
        SortOrder = sortOrder;
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void Update(
        string code,
        string name,
        int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order cannot be negative.");
        }

        SetCode(code);
        SetName(name);
        SortOrder = sortOrder;
        RefreshConcurrencyToken();
    }

    public void Activate()
    {
        IsActive = true;
        RefreshConcurrencyToken();
    }

    public void Inactivate()
    {
        IsActive = false;
        RefreshConcurrencyToken();
    }

    private void SetCode(string code)
    {
        Code = PersonnelFileNormalization.NormalizeCode(code);
        NormalizedCode = Code;
    }

    private void SetName(string name)
    {
        Name = PersonnelFileNormalization.Clean(name, nameof(name));
        NormalizedName = PersonnelFileNormalization.NormalizeName(name);
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}

public sealed class EducationStatusCatalogItem : PersonnelEducationCatalogItem
{
    private EducationStatusCatalogItem()
    {
    }

    private EducationStatusCatalogItem(
        Guid publicId,
        string code,
        string name,
        int sortOrder)
        : base(publicId, code, name, sortOrder)
    {
    }

    public static EducationStatusCatalogItem Create(
        string code,
        string name,
        int sortOrder) =>
        new(Guid.NewGuid(), code, name, sortOrder);
}

public sealed class EducationStudyTypeCatalogItem : PersonnelEducationCatalogItem
{
    private EducationStudyTypeCatalogItem()
    {
    }

    private EducationStudyTypeCatalogItem(
        Guid publicId,
        string code,
        string name,
        int sortOrder)
        : base(publicId, code, name, sortOrder)
    {
    }

    public static EducationStudyTypeCatalogItem Create(
        string code,
        string name,
        int sortOrder) =>
        new(Guid.NewGuid(), code, name, sortOrder);
}

public sealed class EducationCareerCatalogItem : PersonnelEducationCatalogItem
{
    private EducationCareerCatalogItem()
    {
    }

    private EducationCareerCatalogItem(
        Guid publicId,
        string code,
        string name,
        int sortOrder)
        : base(publicId, code, name, sortOrder)
    {
    }

    public static EducationCareerCatalogItem Create(
        string code,
        string name,
        int sortOrder) =>
        new(Guid.NewGuid(), code, name, sortOrder);
}

public sealed class EducationShiftCatalogItem : PersonnelEducationCatalogItem
{
    private EducationShiftCatalogItem()
    {
    }

    private EducationShiftCatalogItem(
        Guid publicId,
        string code,
        string name,
        int sortOrder)
        : base(publicId, code, name, sortOrder)
    {
    }

    public static EducationShiftCatalogItem Create(
        string code,
        string name,
        int sortOrder) =>
        new(Guid.NewGuid(), code, name, sortOrder);
}

public sealed class EducationModalityCatalogItem : PersonnelEducationCatalogItem
{
    private EducationModalityCatalogItem()
    {
    }

    private EducationModalityCatalogItem(
        Guid publicId,
        string code,
        string name,
        int sortOrder)
        : base(publicId, code, name, sortOrder)
    {
    }

    public static EducationModalityCatalogItem Create(
        string code,
        string name,
        int sortOrder) =>
        new(Guid.NewGuid(), code, name, sortOrder);
}

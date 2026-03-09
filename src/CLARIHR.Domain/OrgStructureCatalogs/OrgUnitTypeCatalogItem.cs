using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.OrgStructureCatalogs;

public sealed class OrgUnitTypeCatalogItem : TenantEntity
{
    private OrgUnitTypeCatalogItem()
    {
    }

    private OrgUnitTypeCatalogItem(
        Guid publicId,
        string code,
        string name,
        string? description,
        int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be greater than or equal to zero.");
        }

        PublicId = publicId;
        SetCode(code);
        SetName(name);
        Description = OrgStructureCatalogNormalization.CleanOptional(description);
        SortOrder = sortOrder;
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public Guid PublicId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static OrgUnitTypeCatalogItem Create(
        string code,
        string name,
        string? description,
        int sortOrder) =>
        new(Guid.NewGuid(), code, name, description, sortOrder);

    public void Update(
        string code,
        string name,
        string? description,
        int sortOrder)
    {
        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be greater than or equal to zero.");
        }

        SetCode(code);
        SetName(name);
        Description = OrgStructureCatalogNormalization.CleanOptional(description);
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
        Code = OrgStructureCatalogNormalization.Clean(code, nameof(code));
        NormalizedCode = OrgStructureCatalogNormalization.NormalizeCode(code);
    }

    private void SetName(string name)
    {
        Name = OrgStructureCatalogNormalization.Clean(name, nameof(name));
        NormalizedName = OrgStructureCatalogNormalization.NormalizeName(name);
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}

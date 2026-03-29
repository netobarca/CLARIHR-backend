using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.PositionDescriptionCatalogs;

public sealed class PositionDescriptionCatalogItem : TenantEntity
{
    private PositionDescriptionCatalogItem()
    {
    }

    private PositionDescriptionCatalogItem(
        Guid publicId,
        PositionDescriptionCatalogType catalogType,
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
        CatalogType = catalogType;
        SetCode(code);
        SetName(name);
        Description = PositionDescriptionCatalogNormalization.CleanOptional(description);
        SortOrder = sortOrder;
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public PositionDescriptionCatalogType CatalogType { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static PositionDescriptionCatalogItem Create(
        PositionDescriptionCatalogType catalogType,
        string code,
        string name,
        string? description,
        int sortOrder) =>
        new(Guid.NewGuid(), catalogType, code, name, description, sortOrder);

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
        Description = PositionDescriptionCatalogNormalization.CleanOptional(description);
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
        Code = PositionDescriptionCatalogNormalization.NormalizeCode(code);
        NormalizedCode = Code;
    }

    private void SetName(string name)
    {
        Name = PositionDescriptionCatalogNormalization.Clean(name, nameof(name));
        NormalizedName = PositionDescriptionCatalogNormalization.NormalizeName(name);
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}

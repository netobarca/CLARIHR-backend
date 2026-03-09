using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.PositionDescriptionCatalogs;

public sealed class PositionCategory : TenantEntity
{
    private PositionCategory()
    {
    }

    private PositionCategory(
        Guid publicId,
        string code,
        string name,
        string? description,
        long positionCategoryClassificationId,
        int sortOrder)
    {
        if (positionCategoryClassificationId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(positionCategoryClassificationId), "Classification id must be greater than zero.");
        }

        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be greater than or equal to zero.");
        }

        PublicId = publicId;
        SetCode(code);
        SetName(name);
        Description = PositionDescriptionCatalogNormalization.CleanOptional(description);
        PositionCategoryClassificationId = positionCategoryClassificationId;
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

    public long PositionCategoryClassificationId { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static PositionCategory Create(
        string code,
        string name,
        string? description,
        long positionCategoryClassificationId,
        int sortOrder) =>
        new(Guid.NewGuid(), code, name, description, positionCategoryClassificationId, sortOrder);

    public void Update(
        string code,
        string name,
        string? description,
        long positionCategoryClassificationId,
        int sortOrder)
    {
        if (positionCategoryClassificationId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(positionCategoryClassificationId), "Classification id must be greater than zero.");
        }

        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be greater than or equal to zero.");
        }

        SetCode(code);
        SetName(name);
        Description = PositionDescriptionCatalogNormalization.CleanOptional(description);
        PositionCategoryClassificationId = positionCategoryClassificationId;
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
        Code = PositionDescriptionCatalogNormalization.Clean(code, nameof(code));
        NormalizedCode = PositionDescriptionCatalogNormalization.NormalizeCode(code);
    }

    private void SetName(string name)
    {
        Name = PositionDescriptionCatalogNormalization.Clean(name, nameof(name));
        NormalizedName = PositionDescriptionCatalogNormalization.NormalizeName(name);
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}

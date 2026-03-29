using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.PositionDescriptionCatalogs;

public sealed class PositionCategoryClassification : TenantEntity
{
    private PositionCategoryClassification()
    {
    }

    private PositionCategoryClassification(
        Guid publicId,
        string code,
        string name,
        string? description,
        long positionFunctionCatalogItemId,
        long positionContractCatalogItemId,
        long orgUnitTypeCatalogItemId,
        int sortOrder)
    {
        EnsurePositiveId(positionFunctionCatalogItemId, nameof(positionFunctionCatalogItemId));
        EnsurePositiveId(positionContractCatalogItemId, nameof(positionContractCatalogItemId));
        EnsurePositiveId(orgUnitTypeCatalogItemId, nameof(orgUnitTypeCatalogItemId));

        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be greater than or equal to zero.");
        }

        PublicId = publicId;
        SetCode(code);
        SetName(name);
        Description = PositionDescriptionCatalogNormalization.CleanOptional(description);
        PositionFunctionCatalogItemId = positionFunctionCatalogItemId;
        PositionContractCatalogItemId = positionContractCatalogItemId;
        OrgUnitTypeCatalogItemId = orgUnitTypeCatalogItemId;
        SortOrder = sortOrder;
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public long PositionFunctionCatalogItemId { get; private set; }

    public long PositionContractCatalogItemId { get; private set; }

    public long OrgUnitTypeCatalogItemId { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static PositionCategoryClassification Create(
        string code,
        string name,
        string? description,
        long positionFunctionCatalogItemId,
        long positionContractCatalogItemId,
        long orgUnitTypeCatalogItemId,
        int sortOrder) =>
        new(
            Guid.NewGuid(),
            code,
            name,
            description,
            positionFunctionCatalogItemId,
            positionContractCatalogItemId,
            orgUnitTypeCatalogItemId,
            sortOrder);

    public void Update(
        string code,
        string name,
        string? description,
        long positionFunctionCatalogItemId,
        long positionContractCatalogItemId,
        long orgUnitTypeCatalogItemId,
        int sortOrder)
    {
        EnsurePositiveId(positionFunctionCatalogItemId, nameof(positionFunctionCatalogItemId));
        EnsurePositiveId(positionContractCatalogItemId, nameof(positionContractCatalogItemId));
        EnsurePositiveId(orgUnitTypeCatalogItemId, nameof(orgUnitTypeCatalogItemId));

        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be greater than or equal to zero.");
        }

        SetCode(code);
        SetName(name);
        Description = PositionDescriptionCatalogNormalization.CleanOptional(description);
        PositionFunctionCatalogItemId = positionFunctionCatalogItemId;
        PositionContractCatalogItemId = positionContractCatalogItemId;
        OrgUnitTypeCatalogItemId = orgUnitTypeCatalogItemId;
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

    private static void EnsurePositiveId(long id, string paramName)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(paramName, "Identifier must be greater than zero.");
        }
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}

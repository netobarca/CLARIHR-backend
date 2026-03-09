using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.OrgUnits;

public sealed class OrgUnit : TenantEntity
{
    private OrgUnit()
    {
    }

    private OrgUnit(
        Guid publicId,
        string code,
        string name,
        long orgUnitTypeCatalogItemId,
        long? functionalAreaCatalogItemId,
        long? parentId,
        int? sortOrder,
        string? description,
        string? costCenterCode,
        Guid? managerEmployeeId)
    {
        if (orgUnitTypeCatalogItemId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(orgUnitTypeCatalogItemId), "Organization unit type catalog id must be greater than zero.");
        }

        if (functionalAreaCatalogItemId.HasValue && functionalAreaCatalogItemId.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(functionalAreaCatalogItemId), "Functional area catalog id must be greater than zero.");
        }

        if (sortOrder.HasValue && sortOrder.Value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be greater than or equal to zero.");
        }

        PublicId = publicId;
        SetCode(code);
        SetName(name);
        OrgUnitTypeCatalogItemId = orgUnitTypeCatalogItemId;
        FunctionalAreaCatalogItemId = functionalAreaCatalogItemId;
        ParentId = parentId;
        SortOrder = sortOrder;
        Description = OrgUnitNormalization.CleanOptional(description);
        CostCenterCode = OrgUnitNormalization.CleanOptional(costCenterCode);
        ManagerEmployeeId = managerEmployeeId;
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public Guid PublicId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public long OrgUnitTypeCatalogItemId { get; private set; }

    public long? FunctionalAreaCatalogItemId { get; private set; }

    public long? ParentId { get; private set; }

    public int? SortOrder { get; private set; }

    public string? Description { get; private set; }

    public string? CostCenterCode { get; private set; }

    public Guid? ManagerEmployeeId { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static OrgUnit Create(
        string code,
        string name,
        long orgUnitTypeCatalogItemId,
        long? functionalAreaCatalogItemId,
        long? parentId,
        int? sortOrder,
        string? description,
        string? costCenterCode,
        Guid? managerEmployeeId) =>
        new(
            Guid.NewGuid(),
            code,
            name,
            orgUnitTypeCatalogItemId,
            functionalAreaCatalogItemId,
            parentId,
            sortOrder,
            description,
            costCenterCode,
            managerEmployeeId);

    public void Update(
        string code,
        string name,
        long orgUnitTypeCatalogItemId,
        long? functionalAreaCatalogItemId,
        int? sortOrder,
        string? description,
        string? costCenterCode,
        Guid? managerEmployeeId)
    {
        if (orgUnitTypeCatalogItemId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(orgUnitTypeCatalogItemId), "Organization unit type catalog id must be greater than zero.");
        }

        if (functionalAreaCatalogItemId.HasValue && functionalAreaCatalogItemId.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(functionalAreaCatalogItemId), "Functional area catalog id must be greater than zero.");
        }

        if (sortOrder.HasValue && sortOrder.Value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be greater than or equal to zero.");
        }

        SetCode(code);
        SetName(name);
        OrgUnitTypeCatalogItemId = orgUnitTypeCatalogItemId;
        FunctionalAreaCatalogItemId = functionalAreaCatalogItemId;
        SortOrder = sortOrder;
        Description = OrgUnitNormalization.CleanOptional(description);
        CostCenterCode = OrgUnitNormalization.CleanOptional(costCenterCode);
        ManagerEmployeeId = managerEmployeeId;
        RefreshConcurrencyToken();
    }

    public void Move(long? parentId, int? sortOrder)
    {
        if (sortOrder.HasValue && sortOrder.Value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be greater than or equal to zero.");
        }

        ParentId = parentId;
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
        Code = OrgUnitNormalization.Clean(code, nameof(code));
        NormalizedCode = OrgUnitNormalization.NormalizeCode(code);
    }

    private void SetName(string name)
    {
        Name = OrgUnitNormalization.Clean(name, nameof(name));
        NormalizedName = OrgUnitNormalization.NormalizeName(name);
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}

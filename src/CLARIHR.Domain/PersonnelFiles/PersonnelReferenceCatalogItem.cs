using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.PersonnelFiles;

public abstract class PersonnelReferenceCatalogItemBase : CountryScopedCatalogItem
{
    protected PersonnelReferenceCatalogItemBase()
    {
    }

    protected PersonnelReferenceCatalogItemBase(
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

public sealed class IdentificationTypeCatalogItem : PersonnelReferenceCatalogItemBase
{
    private IdentificationTypeCatalogItem()
    {
    }

    private IdentificationTypeCatalogItem(
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

    public static IdentificationTypeCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

public sealed class ProfessionCatalogItem : PersonnelReferenceCatalogItemBase
{
    private ProfessionCatalogItem()
    {
    }

    private ProfessionCatalogItem(
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

    public static ProfessionCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

public sealed class MaritalStatusCatalogItem : PersonnelReferenceCatalogItemBase
{
    private MaritalStatusCatalogItem()
    {
    }

    private MaritalStatusCatalogItem(
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

    public static MaritalStatusCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

public sealed class KinshipCatalogItem : PersonnelReferenceCatalogItemBase
{
    private KinshipCatalogItem()
    {
    }

    private KinshipCatalogItem(
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

    public static KinshipCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

public sealed class DepartmentCatalogItem : PersonnelReferenceCatalogItemBase
{
    private DepartmentCatalogItem()
    {
    }

    private DepartmentCatalogItem(
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

    public static DepartmentCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

public sealed class MunicipalityCatalogItem : PersonnelReferenceCatalogItemBase
{
    private MunicipalityCatalogItem()
    {
    }

    private MunicipalityCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder,
        long departmentCatalogItemId)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
        SetDepartment(departmentCatalogItemId);
    }

    public long DepartmentCatalogItemId { get; private set; }

    public DepartmentCatalogItem? DepartmentCatalogItem { get; private set; }

    public static MunicipalityCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder,
        long departmentCatalogItemId) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder, departmentCatalogItemId);

    public void Update(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        int sortOrder,
        long departmentCatalogItemId)
    {
        base.Update(countryCatalogItemId, countryCode, code, name, sortOrder);
        SetDepartment(departmentCatalogItemId);
    }

    private void SetDepartment(long departmentCatalogItemId)
    {
        if (departmentCatalogItemId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(departmentCatalogItemId), "Department catalog item id cannot be zero.");
        }

        DepartmentCatalogItemId = departmentCatalogItemId;
        RefreshConcurrencyToken();
    }
}

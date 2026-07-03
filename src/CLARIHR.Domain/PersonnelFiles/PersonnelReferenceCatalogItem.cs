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
        int sortOrder,
        string? numberFormat)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
        NumberFormat = string.IsNullOrWhiteSpace(numberFormat) ? null : numberFormat.Trim();
    }

    /// <summary>
    /// Optional ANCHORED regex the identification number must match for this type (RF-003, BT-04).
    /// Null keeps the generic length/character validation only. Seed-delivered defaults per §20.3
    /// (e.g. DUI <c>^\d{8}-\d$</c>); editable via migration/admin later (DP-03).
    /// </summary>
    public string? NumberFormat { get; private set; }

    public static IdentificationTypeCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder,
        string? numberFormat = null) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder, numberFormat);
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

public sealed class InsuranceTypeCatalogItem : PersonnelReferenceCatalogItemBase
{
    private InsuranceTypeCatalogItem()
    {
    }

    private InsuranceTypeCatalogItem(
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

    public static InsuranceTypeCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

public sealed class InsuranceRangeCatalogItem : PersonnelReferenceCatalogItemBase
{
    private InsuranceRangeCatalogItem()
    {
    }

    private InsuranceRangeCatalogItem(
        Guid publicId,
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder,
        long insuranceTypeCatalogItemId)
        : base(publicId, countryCatalogItemId, countryCode, code, name, isActive, sortOrder)
    {
        SetInsuranceType(insuranceTypeCatalogItemId);
    }

    public long InsuranceTypeCatalogItemId { get; private set; }

    public InsuranceTypeCatalogItem? InsuranceTypeCatalogItem { get; private set; }

    public static InsuranceRangeCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder,
        long insuranceTypeCatalogItemId) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder, insuranceTypeCatalogItemId);

    private void SetInsuranceType(long insuranceTypeCatalogItemId)
    {
        if (insuranceTypeCatalogItemId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(insuranceTypeCatalogItemId), "Insurance type catalog item id cannot be zero.");
        }

        InsuranceTypeCatalogItemId = insuranceTypeCatalogItemId;
        RefreshConcurrencyToken();
    }
}

/// <summary>
/// Country-scoped catalog of personal titles / salutations (reference-catalogs key
/// <c>personal-titles</c>) backing the optional <c>personalTitleCode</c> person attribute
/// (RF-001). Mirrors <see cref="MaritalStatusCatalogItem"/>; seeded for SV via HasData.
/// </summary>
public sealed class PersonalTitleCatalogItem : PersonnelReferenceCatalogItemBase
{
    private PersonalTitleCatalogItem()
    {
    }

    private PersonalTitleCatalogItem(
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

    public static PersonalTitleCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

/// <summary>
/// Country-scoped catalog of address types (reference-catalogs key <c>address-types</c>) backing
/// the optional <c>addressTypeCode</c> of a personnel-file address (RF-002). Mirrors
/// <see cref="KinshipCatalogItem"/>; seeded for SV via HasData.
/// </summary>
public sealed class AddressTypeCatalogItem : PersonnelReferenceCatalogItemBase
{
    private AddressTypeCatalogItem()
    {
    }

    private AddressTypeCatalogItem(
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

    public static AddressTypeCatalogItem Create(
        long countryCatalogItemId,
        string countryCode,
        string code,
        string name,
        bool isActive,
        int sortOrder) =>
        new(Guid.NewGuid(), countryCatalogItemId, countryCode, code, name, isActive, sortOrder);
}

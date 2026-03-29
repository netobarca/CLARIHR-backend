using CLARIHR.Domain.Companies;
using CLARIHR.Domain.OrgStructureCatalogs;

namespace CLARIHR.Application.UnitTests;

public sealed class OrgStructureCatalogDomainTests
{
    [Fact]
    public void CompanyTypeCatalogItem_Create_ShouldNormalizeCodeAndName()
    {
        var ownerId = Guid.NewGuid();

        var item = CompanyTypeCatalogItem.Create(
            ownerId,
            "  private  ",
            "  Private Company  ",
            "  Seed  ",
            sortOrder: 10);

        Assert.Equal("private", item.Code);
        Assert.Equal("PRIVATE", item.NormalizedCode);
        Assert.Equal("Private Company", item.Name);
        Assert.Equal("PRIVATE COMPANY", item.NormalizedName);
        Assert.Equal("Seed", item.Description);
        Assert.True(item.IsActive);
    }

    [Fact]
    public void OrgUnitTypeCatalogItem_Update_ShouldRefreshConcurrencyToken()
    {
        var item = OrgUnitTypeCatalogItem.Create("DIR", "Direccion", null, 10);
        var tokenBefore = item.ConcurrencyToken;

        item.Update("DIR", "Direccion General", "Updated", 20);

        Assert.NotEqual(tokenBefore, item.ConcurrencyToken);
        Assert.Equal("Direccion General", item.Name);
        Assert.Equal(20, item.SortOrder);
    }

    [Fact]
    public void FunctionalAreaCatalogItem_Inactivate_ShouldDisableItem()
    {
        var item = FunctionalAreaCatalogItem.Create("ADM", "Administracion", null, 10);

        item.Inactivate();

        Assert.False(item.IsActive);
    }

    [Fact]
    public void Company_SetCompanyType_ShouldAllowNullAndPositiveIds()
    {
        var company = Company.Create("Acme", "acme", Guid.NewGuid(), "SV", 1);
        company.SetCompanyType(100);
        Assert.Equal(100, company.CompanyTypeCatalogItemId);

        company.SetCompanyType(null);
        Assert.Null(company.CompanyTypeCatalogItemId);
    }
}

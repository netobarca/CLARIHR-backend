using CLARIHR.Domain.Banks;

namespace CLARIHR.Application.UnitTests;

public sealed class BankCatalogDomainTests
{
    [Fact]
    public void Create_ShouldNormalizeOptionalMetadata()
    {
        var bank = BankCatalogItem.Create(
            -7068L,
            "sv",
            " banco_agricola ",
            " Banco Agricola ",
            " agricola ",
            " bacrsvss ",
            " 123456789 ",
            isActive: true,
            sortOrder: 10);

        Assert.Equal("SV", bank.CountryCode);
        Assert.Equal("BANCO_AGRICOLA", bank.Code);
        Assert.Equal("Banco Agricola", bank.Name);
        Assert.Equal("agricola", bank.Alias);
        Assert.Equal("AGRICOLA", bank.NormalizedAlias);
        Assert.Equal("BACRSVSS", bank.SwiftCode);
        Assert.Equal("BACRSVSS", bank.NormalizedSwiftCode);
        Assert.Equal("123456789", bank.RoutingCode);
        Assert.Equal("123456789", bank.NormalizedRoutingCode);
    }

    [Fact]
    public void Update_ShouldAllowClearingOptionalMetadata()
    {
        var bank = BankCatalogItem.Create(
            -7068L,
            "SV",
            "BANCO_AGRICOLA",
            "Banco Agricola",
            "Agricola",
            "BACRSVSS",
            "123456789",
            isActive: true,
            sortOrder: 10);

        bank.Update(
            -7068L,
            "SV",
            "BANCO_AGRICOLA",
            "Banco Agricola",
            alias: null,
            swiftCode: null,
            routingCode: null,
            sortOrder: 20);

        Assert.Null(bank.Alias);
        Assert.Null(bank.NormalizedAlias);
        Assert.Null(bank.SwiftCode);
        Assert.Null(bank.NormalizedSwiftCode);
        Assert.Null(bank.RoutingCode);
        Assert.Null(bank.NormalizedRoutingCode);
        Assert.Equal(20, bank.SortOrder);
    }
}

using CLARIHR.Domain.CostCenters;

namespace CLARIHR.Application.UnitTests;

public sealed class CostCenterTypeDomainTests
{
    [Fact]
    public void CostCenterType_Create_ShouldNormalizeCodeAndName()
    {
        var costCenterType = CostCenterType.Create(
            code: "  salary-expense  ",
            name: "  Gasto Salarial  ",
            description: " gasto de planilla ");

        Assert.Equal("SALARY-EXPENSE", costCenterType.Code);
        Assert.Equal("SALARY-EXPENSE", costCenterType.NormalizedCode);
        Assert.Equal("Gasto Salarial", costCenterType.Name);
        Assert.Equal("GASTO SALARIAL", costCenterType.NormalizedName);
        Assert.Equal("gasto de planilla", costCenterType.Description);
        Assert.True(costCenterType.IsActive);
    }

    [Fact]
    public void CostCenterType_Update_ShouldRefreshConcurrencyToken()
    {
        var costCenterType = CostCenterType.Create("MIXED", "Mixto", description: null);
        var beforeToken = costCenterType.ConcurrencyToken;

        costCenterType.Update("MIXED", "Mixto Actualizado", "actualizado");

        Assert.Equal("Mixto Actualizado", costCenterType.Name);
        Assert.Equal("actualizado", costCenterType.Description);
        Assert.NotEqual(beforeToken, costCenterType.ConcurrencyToken);
    }

    [Fact]
    public void CostCenterType_ActivateInactivate_ShouldUpdateStateAndToken()
    {
        var costCenterType = CostCenterType.Create("MIXED", "Mixto", description: null);

        var beforeInactivateToken = costCenterType.ConcurrencyToken;
        costCenterType.Inactivate();
        var afterInactivateToken = costCenterType.ConcurrencyToken;

        Assert.False(costCenterType.IsActive);
        Assert.NotEqual(beforeInactivateToken, afterInactivateToken);

        costCenterType.Activate();

        Assert.True(costCenterType.IsActive);
        Assert.NotEqual(afterInactivateToken, costCenterType.ConcurrencyToken);
    }
}

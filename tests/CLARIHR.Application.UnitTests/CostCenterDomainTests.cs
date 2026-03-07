using CLARIHR.Domain.CostCenters;

namespace CLARIHR.Application.UnitTests;

public sealed class CostCenterDomainTests
{
    [Fact]
    public void CostCenter_Create_ShouldNormalizeCodeAndName()
    {
        var costCenter = CostCenter.Create(
            code: "  cc-001  ",
            name: "  Centro Contable  ",
            type: CostCenterType.Mixed,
            payrollExpenseAccountCode: " 5101-001 ",
            employerContributionAccountCode: " 5102-001 ",
            provisionAccountCode: " 5103-001 ",
            description: " principal ");

        Assert.Equal("cc-001", costCenter.Code);
        Assert.Equal("CC-001", costCenter.NormalizedCode);
        Assert.Equal("Centro Contable", costCenter.Name);
        Assert.Equal("CENTRO CONTABLE", costCenter.NormalizedName);
        Assert.Equal("5101-001", costCenter.PayrollExpenseAccountCode);
        Assert.Equal("5102-001", costCenter.EmployerContributionAccountCode);
        Assert.Equal("5103-001", costCenter.ProvisionAccountCode);
        Assert.Equal("principal", costCenter.Description);
        Assert.True(costCenter.IsActive);
    }

    [Fact]
    public void CostCenter_Update_ShouldRefreshConcurrencyToken()
    {
        var costCenter = CostCenter.Create(
            code: "CC-001",
            name: "Centro",
            type: CostCenterType.Mixed,
            payrollExpenseAccountCode: null,
            employerContributionAccountCode: null,
            provisionAccountCode: null,
            description: null);

        var beforeToken = costCenter.ConcurrencyToken;

        costCenter.Update(
            code: "CC-001",
            name: "Centro Actualizado",
            type: CostCenterType.SalaryExpense,
            payrollExpenseAccountCode: "5101-002",
            employerContributionAccountCode: null,
            provisionAccountCode: null,
            description: "Actualizado");

        Assert.Equal("Centro Actualizado", costCenter.Name);
        Assert.Equal(CostCenterType.SalaryExpense, costCenter.Type);
        Assert.NotEqual(beforeToken, costCenter.ConcurrencyToken);
    }

    [Fact]
    public void CostCenter_ActivateInactivate_ShouldUpdateStateAndToken()
    {
        var costCenter = CostCenter.Create(
            code: "CC-001",
            name: "Centro",
            type: CostCenterType.Mixed,
            payrollExpenseAccountCode: null,
            employerContributionAccountCode: null,
            provisionAccountCode: null,
            description: null);

        var beforeInactivateToken = costCenter.ConcurrencyToken;
        costCenter.Inactivate();
        var afterInactivateToken = costCenter.ConcurrencyToken;

        Assert.False(costCenter.IsActive);
        Assert.NotEqual(beforeInactivateToken, afterInactivateToken);

        costCenter.Activate();

        Assert.True(costCenter.IsActive);
        Assert.NotEqual(afterInactivateToken, costCenter.ConcurrencyToken);
    }
}

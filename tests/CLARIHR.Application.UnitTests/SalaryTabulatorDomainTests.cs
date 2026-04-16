using CLARIHR.Domain.SalaryTabulator;

namespace CLARIHR.Application.UnitTests;

public sealed class SalaryTabulatorDomainTests
{
    [Fact]
    public void SalaryTabulatorLine_Create_ShouldNormalizeCodesAndInitializeVersion()
    {
        var line = SalaryTabulatorLine.Create(
            salaryClassCode: "  cls-a  ",
            salaryScaleCode: "  s1  ",
            currencyCode: " usd ",
            baseAmount: 1200,
            minAmount: 1000,
            maxAmount: 1500,
            effectiveFromUtc: DateTime.UtcNow.Date,
            effectiveToUtc: null,
            notes: " initial ");

        Assert.Equal("CLS-A", line.SalaryClassCode);
        Assert.Equal("CLS-A", line.NormalizedSalaryClassCode);
        Assert.Equal("S1", line.SalaryScaleCode);
        Assert.Equal("S1", line.NormalizedSalaryScaleCode);
        Assert.Equal("USD", line.CurrencyCode);
        Assert.Equal(1, line.Version);
        Assert.True(line.IsActive);
    }

    [Fact]
    public void SalaryTabulatorLine_ApplySameDateUpdate_ShouldBumpVersionAndToken()
    {
        var line = SalaryTabulatorLine.Create(
            salaryClassCode: "CLS-A",
            salaryScaleCode: "S1",
            currencyCode: "USD",
            baseAmount: 1200,
            minAmount: 1000,
            maxAmount: 1500,
            effectiveFromUtc: DateTime.UtcNow.Date,
            effectiveToUtc: null,
            notes: null);

        var beforeToken = line.ConcurrencyToken;

        line.ApplySameDateUpdate(
            currencyCode: "USD",
            baseAmount: 1300,
            minAmount: 1100,
            maxAmount: 1600,
            notes: "updated");

        Assert.Equal(2, line.Version);
        Assert.Equal(1300, line.BaseAmount);
        Assert.NotEqual(beforeToken, line.ConcurrencyToken);
    }

    [Fact]
    public void SalaryTabulatorChangeRequest_Approve_WhenSelfApprovalIsBlocked_ShouldThrow()
    {
        var requesterId = Guid.NewGuid();
        var item = SalaryTabulatorChangeRequestItem.Create(
            salaryClassCode: "CLS-A",
            salaryScaleCode: "S1",
            currencyCode: "USD",
            changeType: SalaryTabulatorChangeType.Create,
            currentBaseAmount: null,
            proposedBaseAmount: 1200,
            currentMinAmount: null,
            proposedMinAmount: null,
            currentMaxAmount: null,
            proposedMaxAmount: null,
            notes: null);

        var request = SalaryTabulatorChangeRequest.Create(
            requestNumber: "STR-0001",
            reason: "revision",
            effectiveFromUtc: DateTime.UtcNow.Date,
            requestedByUserId: requesterId,
            items: [item]);

        request.Submit(DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            request.Approve(requesterId, DateTime.UtcNow, "ok", allowSelfApproval: false));
    }

    [Fact]
    public void SalaryTabulatorChangeRequest_UpdateDraft_WhenSubmitted_ShouldThrow()
    {
        var item = SalaryTabulatorChangeRequestItem.Create(
            salaryClassCode: "CLS-A",
            salaryScaleCode: "S1",
            currencyCode: "USD",
            changeType: SalaryTabulatorChangeType.Create,
            currentBaseAmount: null,
            proposedBaseAmount: 1200,
            currentMinAmount: null,
            proposedMinAmount: null,
            currentMaxAmount: null,
            proposedMaxAmount: null,
            notes: null);

        var request = SalaryTabulatorChangeRequest.Create(
            requestNumber: "STR-0001",
            reason: "revision",
            effectiveFromUtc: DateTime.UtcNow.Date,
            requestedByUserId: Guid.NewGuid(),
            items: [item]);

        request.Submit(DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            request.UpdateDraft("new reason", DateTime.UtcNow.Date, [item]));
    }
}

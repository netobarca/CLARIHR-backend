using CLARIHR.Application.Features.SalaryTabulator;
using CLARIHR.Domain.SalaryTabulator;

namespace CLARIHR.Application.UnitTests;

public sealed class SalaryTabulatorDomainTests
{
    [Fact]
    public void CreateSalaryTabulatorChangeRequestValidator_WhenMultipleItems_ShouldBeValid()
    {
        var validator = new CreateSalaryTabulatorChangeRequestCommandValidator();
        var command = new CreateSalaryTabulatorChangeRequestCommand(
            CompanyId: Guid.NewGuid(),
            EffectiveFromUtc: DateTime.UtcNow.Date,
            EffectiveToUtc: null,
            Items:
            [
                new SalaryTabulatorChangeRequestItemInput(
                    SalaryClassId: Guid.NewGuid(),
                    SalaryScaleCode: "S1",
                    CurrencyCode: "USD",
                    ChangeType: SalaryTabulatorChangeType.Create,
                    ProposedBaseAmount: 1200m,
                    ProposedMinAmount: 1000m,
                    ProposedMaxAmount: 1500m,
                    Notes: "linea uno"),
                new SalaryTabulatorChangeRequestItemInput(
                    SalaryClassId: Guid.NewGuid(),
                    SalaryScaleCode: "S2",
                    CurrencyCode: "USD",
                    ChangeType: SalaryTabulatorChangeType.Create,
                    ProposedBaseAmount: 1400m,
                    ProposedMinAmount: 1200m,
                    ProposedMaxAmount: 1700m,
                    Notes: "linea dos")
            ]);

        var result = validator.Validate(command);

        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(error => error.ErrorMessage)));
    }

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
            effectiveToUtc: null,
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
            effectiveToUtc: null,
            requestedByUserId: Guid.NewGuid(),
            items: [item]);

        request.Submit(DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            request.UpdateDraft("new reason", DateTime.UtcNow.Date, effectiveToUtc: null, items: [item]));
    }
}

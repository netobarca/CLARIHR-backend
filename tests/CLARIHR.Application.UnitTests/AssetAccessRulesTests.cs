using CLARIHR.Application.Features.PersonnelFiles;
using Xunit;

namespace CLARIHR.Application.UnitTests;

public sealed class AssetAccessRulesTests
{
    private static readonly DateTime Start = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ValidateDates_NoEndNoDelivery_Succeeds()
    {
        var result = AssetAccessRules.ValidateDates(Start, endDateUtc: null, deliveryDateUtc: null);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ValidateDates_EndAfterStart_Succeeds()
    {
        var result = AssetAccessRules.ValidateDates(Start, Start.AddDays(30), deliveryDateUtc: null);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ValidateDates_EndEqualsStart_Succeeds()
    {
        var result = AssetAccessRules.ValidateDates(Start, Start, deliveryDateUtc: null);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void ValidateDates_EndBeforeStart_FailsDateRange()
    {
        var result = AssetAccessRules.ValidateDates(Start, Start.AddDays(-1), deliveryDateUtc: null);

        Assert.True(result.IsFailure);
        Assert.Equal("ASSET_ACCESS_DATE_RANGE_INVALID", result.Error.Code);
    }

    [Fact]
    public void ValidateDates_DeliveryOnOrAfterStart_Succeeds()
    {
        Assert.True(AssetAccessRules.ValidateDates(Start, endDateUtc: null, Start).IsSuccess);
        Assert.True(AssetAccessRules.ValidateDates(Start, endDateUtc: null, Start.AddDays(2)).IsSuccess);
    }

    [Fact]
    public void ValidateDates_DeliveryBeforeStart_FailsDeliveryDate()
    {
        var result = AssetAccessRules.ValidateDates(Start, endDateUtc: null, Start.AddDays(-2));

        Assert.True(result.IsFailure);
        Assert.Equal("ASSET_ACCESS_DELIVERY_DATE_INVALID", result.Error.Code);
    }

    [Fact]
    public void ValidateDates_EndBeforeStart_TakesPrecedenceOverDelivery()
    {
        var result = AssetAccessRules.ValidateDates(Start, Start.AddDays(-1), Start.AddDays(-2));

        Assert.True(result.IsFailure);
        Assert.Equal("ASSET_ACCESS_DATE_RANGE_INVALID", result.Error.Code);
    }
}

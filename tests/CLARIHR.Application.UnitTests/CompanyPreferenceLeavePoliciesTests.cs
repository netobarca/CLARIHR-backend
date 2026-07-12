using CLARIHR.Application.Features.Preferences.Company;
using CLARIHR.Domain.Preferences;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the vacation/incapacity parametrization added to <see cref="CompanyPreference"/>
/// (D-20/D-24/D-26/D-27): the <c>SetLeavePolicies</c> guarded mutator (day counts 0-365, rest day of
/// week 0-6 Sunday-first) and the matching range rules on the PUT validator. All ten columns are
/// nullable — null means "use the legal default", which is resolved when the policy is consumed and
/// never stored on the preference row.
/// </summary>
public sealed class CompanyPreferenceLeavePoliciesTests
{
    [Fact]
    public void SetLeavePolicies_WithValidValues_SetsAllFieldsAndRotatesToken()
    {
        var preference = CompanyPreference.Create("USD", "UTC");
        var originalToken = preference.ConcurrencyToken;

        preference.SetLeavePolicies(
            annualVacationDaysDefault: 20,
            additionalVacationBenefitDaysDefault: 5,
            allowVacationStartOnHoliday: true,
            allowVacationEndOnHoliday: false,
            allowVacationStartOnRestDay: true,
            defaultUseAnniversary: false,
            companyRestDayOfWeek: 6,
            employerCoveredIncapacityDaysPerYear: 12,
            additionalIncapacityBenefitDaysPerYear: 3,
            incapacityRequiresDocument: false);

        Assert.Equal(20, preference.AnnualVacationDaysDefault);
        Assert.Equal(5, preference.AdditionalVacationBenefitDaysDefault);
        Assert.True(preference.AllowVacationStartOnHoliday);
        Assert.False(preference.AllowVacationEndOnHoliday);
        Assert.True(preference.AllowVacationStartOnRestDay);
        Assert.False(preference.DefaultUseAnniversary);
        Assert.Equal(6, preference.CompanyRestDayOfWeek);
        Assert.Equal(12, preference.EmployerCoveredIncapacityDaysPerYear);
        Assert.Equal(3, preference.AdditionalIncapacityBenefitDaysPerYear);
        Assert.False(preference.IncapacityRequiresDocument);
        Assert.NotEqual(originalToken, preference.ConcurrencyToken);
    }

    [Fact]
    public void SetLeavePolicies_WithBoundaryValues_Succeeds()
    {
        var preference = CompanyPreference.Create("USD", "UTC");

        preference.SetLeavePolicies(0, 365, null, null, null, null, 0, 365, 0, null);

        Assert.Equal(0, preference.AnnualVacationDaysDefault);
        Assert.Equal(365, preference.AdditionalVacationBenefitDaysDefault);
        Assert.Equal(0, preference.CompanyRestDayOfWeek);
        Assert.Equal(365, preference.EmployerCoveredIncapacityDaysPerYear);
        Assert.Equal(0, preference.AdditionalIncapacityBenefitDaysPerYear);
    }

    [Fact]
    public void SetLeavePolicies_WithAllNulls_ClearsStoredValuesAndRotatesToken()
    {
        var preference = CompanyPreference.Create("USD", "UTC");
        preference.SetLeavePolicies(20, 5, true, false, true, false, 6, 12, 3, false);
        var tokenAfterSet = preference.ConcurrencyToken;

        // Null = back to "use the legal default" (resolved on consumption, never stored).
        preference.SetLeavePolicies(null, null, null, null, null, null, null, null, null, null);

        Assert.Null(preference.AnnualVacationDaysDefault);
        Assert.Null(preference.AdditionalVacationBenefitDaysDefault);
        Assert.Null(preference.AllowVacationStartOnHoliday);
        Assert.Null(preference.AllowVacationEndOnHoliday);
        Assert.Null(preference.AllowVacationStartOnRestDay);
        Assert.Null(preference.DefaultUseAnniversary);
        Assert.Null(preference.CompanyRestDayOfWeek);
        Assert.Null(preference.EmployerCoveredIncapacityDaysPerYear);
        Assert.Null(preference.AdditionalIncapacityBenefitDaysPerYear);
        Assert.Null(preference.IncapacityRequiresDocument);
        Assert.NotEqual(tokenAfterSet, preference.ConcurrencyToken);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(366)]
    public void SetLeavePolicies_WhenAnnualVacationDaysOutOfRange_Throws(int value)
    {
        var preference = CompanyPreference.Create("USD", "UTC");
        var originalToken = preference.ConcurrencyToken;

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            preference.SetLeavePolicies(value, null, null, null, null, null, null, null, null, null));

        Assert.Equal("annualVacationDaysDefault", exception.ParamName);
        Assert.Null(preference.AnnualVacationDaysDefault); // guard fires before any assignment
        Assert.Equal(originalToken, preference.ConcurrencyToken);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(366)]
    public void SetLeavePolicies_WhenAdditionalVacationBenefitDaysOutOfRange_Throws(int value)
    {
        var preference = CompanyPreference.Create("USD", "UTC");

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            preference.SetLeavePolicies(null, value, null, null, null, null, null, null, null, null));

        Assert.Equal("additionalVacationBenefitDaysDefault", exception.ParamName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    public void SetLeavePolicies_WhenCompanyRestDayOfWeekOutOfRange_Throws(int value)
    {
        var preference = CompanyPreference.Create("USD", "UTC");

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            preference.SetLeavePolicies(null, null, null, null, null, null, value, null, null, null));

        Assert.Equal("companyRestDayOfWeek", exception.ParamName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(366)]
    public void SetLeavePolicies_WhenEmployerCoveredIncapacityDaysOutOfRange_Throws(int value)
    {
        var preference = CompanyPreference.Create("USD", "UTC");

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            preference.SetLeavePolicies(null, null, null, null, null, null, null, value, null, null));

        Assert.Equal("employerCoveredIncapacityDaysPerYear", exception.ParamName);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(366)]
    public void SetLeavePolicies_WhenAdditionalIncapacityBenefitDaysOutOfRange_Throws(int value)
    {
        var preference = CompanyPreference.Create("USD", "UTC");

        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            preference.SetLeavePolicies(null, null, null, null, null, null, null, null, value, null));

        Assert.Equal("additionalIncapacityBenefitDaysPerYear", exception.ParamName);
    }

    [Fact]
    public void Validator_WhenLeavePolicyFieldsAreNull_IsValid()
    {
        var result = new UpdateCompanyPreferencesCommandValidator().Validate(ValidCommand());

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validator_WhenLeavePolicyValuesAtBounds_IsValid()
    {
        var result = new UpdateCompanyPreferencesCommandValidator().Validate(ValidCommand() with
        {
            AnnualVacationDaysDefault = 0,
            AdditionalVacationBenefitDaysDefault = 365,
            AllowVacationStartOnHoliday = true,
            AllowVacationEndOnHoliday = false,
            AllowVacationStartOnRestDay = true,
            DefaultUseAnniversary = false,
            CompanyRestDayOfWeek = 6,
            EmployerCoveredIncapacityDaysPerYear = 365,
            AdditionalIncapacityBenefitDaysPerYear = 0,
            IncapacityRequiresDocument = true
        });

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(366)]
    public void Validator_WhenAnnualVacationDaysOutOfRange_Fails(int value)
    {
        var result = new UpdateCompanyPreferencesCommandValidator()
            .Validate(ValidCommand() with { AnnualVacationDaysDefault = value });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.PropertyName == nameof(UpdateCompanyPreferencesCommand.AnnualVacationDaysDefault) &&
            error.ErrorMessage == "Annual vacation days must be between 0 and 365.");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(366)]
    public void Validator_WhenAdditionalVacationBenefitDaysOutOfRange_Fails(int value)
    {
        var result = new UpdateCompanyPreferencesCommandValidator()
            .Validate(ValidCommand() with { AdditionalVacationBenefitDaysDefault = value });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.PropertyName == nameof(UpdateCompanyPreferencesCommand.AdditionalVacationBenefitDaysDefault) &&
            error.ErrorMessage == "Additional vacation benefit days must be between 0 and 365.");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(7)]
    public void Validator_WhenCompanyRestDayOfWeekOutOfRange_Fails(int value)
    {
        var result = new UpdateCompanyPreferencesCommandValidator()
            .Validate(ValidCommand() with { CompanyRestDayOfWeek = value });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.PropertyName == nameof(UpdateCompanyPreferencesCommand.CompanyRestDayOfWeek) &&
            error.ErrorMessage == "Company rest day of week must be between 0 (Sunday) and 6 (Saturday).");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(366)]
    public void Validator_WhenEmployerCoveredIncapacityDaysOutOfRange_Fails(int value)
    {
        var result = new UpdateCompanyPreferencesCommandValidator()
            .Validate(ValidCommand() with { EmployerCoveredIncapacityDaysPerYear = value });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.PropertyName == nameof(UpdateCompanyPreferencesCommand.EmployerCoveredIncapacityDaysPerYear) &&
            error.ErrorMessage == "Employer-covered incapacity days per year must be between 0 and 365.");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(366)]
    public void Validator_WhenAdditionalIncapacityBenefitDaysOutOfRange_Fails(int value)
    {
        var result = new UpdateCompanyPreferencesCommandValidator()
            .Validate(ValidCommand() with { AdditionalIncapacityBenefitDaysPerYear = value });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.PropertyName == nameof(UpdateCompanyPreferencesCommand.AdditionalIncapacityBenefitDaysPerYear) &&
            error.ErrorMessage == "Additional incapacity benefit days per year must be between 0 and 365.");
    }

    private static UpdateCompanyPreferencesCommand ValidCommand() =>
        new(
            Guid.NewGuid(),
            "USD",
            "UTC",
            HrFunctionalAreaCode: null,
            FileUpToDateThresholdMonths: null,
            MinimumSeniorityMonthsForEconomicAid: null,
            AnnualVacationDaysDefault: null,
            AdditionalVacationBenefitDaysDefault: null,
            AllowVacationStartOnHoliday: null,
            AllowVacationEndOnHoliday: null,
            AllowVacationStartOnRestDay: null,
            DefaultUseAnniversary: null,
            CompanyRestDayOfWeek: null,
            EmployerCoveredIncapacityDaysPerYear: null,
            AdditionalIncapacityBenefitDaysPerYear: null,
            IncapacityRequiresDocument: null,
            CompensatoryTimeStandardDailyHours: null,
            CompensatoryTimeMaxBalanceHours: null,
            CompensatoryTimeCreditRequiresDocument: null,
            CompensatoryTimeSettlementRateFactor: null,
            OvertimeSelfServiceEnabled: null,
            OvertimeMaxDailyMinutes: null,
            RecurringDeductionDefaultInterestRatePercent: null,
            MaxIndebtednessPercent: null,
            ConcurrencyToken: Guid.NewGuid());
}

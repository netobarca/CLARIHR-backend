using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the off-payroll-transaction pure rules (correction-reference requirement, imputation
/// period) and the <see cref="OffPayrollTransactionInputValidator"/> (non-zero amount, month/year range,
/// currency length, future date, negative ⇒ correction reference — D-04/D-05/D-12).
/// </summary>
public sealed class OffPayrollTransactionRulesAndValidatorTests
{
    private static OffPayrollTransactionInput ValidInput() =>
        new(
            "HERRAMIENTAS",                     // TransactionTypeCode
            DateTime.UtcNow.Date.AddDays(-5),   // TransactionDateUtc (past)
            "USD",                              // CurrencyCode
            100m,                               // Amount
            2026,                               // Year
            6,                                  // Month
            "tool purchase",                    // Comment
            null,                               // AssetAccessPublicId
            null);                              // CorrectsTransactionPublicId

    private static bool IsValid(OffPayrollTransactionInput input) =>
        new OffPayrollTransactionInputValidator().Validate(input).IsValid;

    // ── RequiresCorrectionReference (D-12) ────────────────────────────────────

    [Fact]
    public void RequiresCorrectionReference_NegativeWithoutReference_True() =>
        Assert.True(OffPayrollTransactionRules.RequiresCorrectionReference(-50m, null));

    [Fact]
    public void RequiresCorrectionReference_NegativeWithReference_False() =>
        Assert.False(OffPayrollTransactionRules.RequiresCorrectionReference(-50m, Guid.NewGuid()));

    [Fact]
    public void RequiresCorrectionReference_Positive_False() =>
        Assert.False(OffPayrollTransactionRules.RequiresCorrectionReference(50m, null));

    // ── IsValidPeriod (D-05) ──────────────────────────────────────────────────

    [Theory]
    [InlineData(2026, 1, true)]
    [InlineData(2026, 12, true)]
    [InlineData(2026, 0, false)]
    [InlineData(2026, 13, false)]
    [InlineData(1999, 6, false)]
    [InlineData(2101, 6, false)]
    public void IsValidPeriod_BoundsChecked(int year, int month, bool expected) =>
        Assert.Equal(expected, OffPayrollTransactionRules.IsValidPeriod(year, month));

    // ── Validator (D-04/D-05/D-12) ────────────────────────────────────────────

    [Fact]
    public void Validator_ValidInput_Passes() => Assert.True(IsValid(ValidInput()));

    [Fact]
    public void Validator_EmptyType_Fails() =>
        Assert.False(IsValid(ValidInput() with { TransactionTypeCode = "" }));

    [Fact]
    public void Validator_ZeroAmount_Fails() =>
        Assert.False(IsValid(ValidInput() with { Amount = 0m }));

    [Fact]
    public void Validator_NegativeAmountWithReference_Passes() =>
        Assert.True(IsValid(ValidInput() with { Amount = -100m, CorrectsTransactionPublicId = Guid.NewGuid() }));

    [Fact]
    public void Validator_NegativeAmountWithoutReference_Fails() =>
        Assert.False(IsValid(ValidInput() with { Amount = -100m, CorrectsTransactionPublicId = null }));

    [Fact]
    public void Validator_MonthOutOfRange_Fails() =>
        Assert.False(IsValid(ValidInput() with { Month = 13 }));

    [Fact]
    public void Validator_MonthZero_Fails() =>
        Assert.False(IsValid(ValidInput() with { Month = 0 }));

    [Fact]
    public void Validator_YearOutOfRange_Fails() =>
        Assert.False(IsValid(ValidInput() with { Year = 1999 }));

    [Fact]
    public void Validator_FutureDate_Fails() =>
        Assert.False(IsValid(ValidInput() with { TransactionDateUtc = DateTime.UtcNow.Date.AddDays(10) }));

    [Fact]
    public void Validator_CurrencyWrongLength_Fails() =>
        Assert.False(IsValid(ValidInput() with { CurrencyCode = "US" }));

    [Fact]
    public void Validator_NullCurrency_Passes_DefaultResolvedInHandler() =>
        Assert.True(IsValid(ValidInput() with { CurrencyCode = null }));
}

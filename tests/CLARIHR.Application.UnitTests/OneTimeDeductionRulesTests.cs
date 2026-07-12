using CLARIHR.Application.Features.PersonnelFiles.Compensation;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// The golden suite of the one-time-deduction value arithmetic (REQ-009 PR-2 — the gate). The load-bearing rule:
/// the AMOUNT of a computed value belongs to the SERVER. The components are the truth, and a client-declared
/// amount that does not follow from them is rejected with the expected figure — otherwise anyone could charge an
/// employee an arbitrary sum while showing innocent-looking components.
/// </summary>
public sealed class OneTimeDeductionRulesTests
{
    // ── CANTIDAD_POR_VALOR: quantity × unit value × multiplier ────────────────────────────────────────
    [Fact]
    public void ComputeAmount_QuantityTimesValue_MultipliesTheThreeComponents()
    {
        var result = OneTimeDeductionRules.ComputeAmount(
            OneTimeDeductionCalculationMethods.QuantityTimesValue,
            quantity: 3m, unitValue: 25.50m, multiplier: 2m, percentage: null, baseAmount: null);

        Assert.True(result.IsValid);
        Assert.Equal(153.00m, result.Amount);
    }

    [Fact]
    public void ComputeAmount_QuantityTimesValue_TheMultiplierDefaultsToOne()
    {
        var result = OneTimeDeductionRules.ComputeAmount(
            OneTimeDeductionCalculationMethods.QuantityTimesValue,
            quantity: 4m, unitValue: 12.25m, multiplier: null, percentage: null, baseAmount: null);

        Assert.True(result.IsValid);
        Assert.Equal(49.00m, result.Amount);
    }

    [Fact]
    public void ComputeAmount_QuantityTimesValue_RoundsHalfUpToTwoDecimals()
    {
        // 3 × 3.335 = 10.005 → 10.01 (half-up away from zero, the single rounding rule).
        var result = OneTimeDeductionRules.ComputeAmount(
            OneTimeDeductionCalculationMethods.QuantityTimesValue,
            quantity: 3m, unitValue: 3.335m, multiplier: null, percentage: null, baseAmount: null);

        Assert.True(result.IsValid);
        Assert.Equal(10.01m, result.Amount);
    }

    [Fact]
    public void ComputeAmount_QuantityTimesValue_RejectsTheComponentsOfTheOtherMethod()
    {
        var result = OneTimeDeductionRules.ComputeAmount(
            OneTimeDeductionCalculationMethods.QuantityTimesValue,
            quantity: 3m, unitValue: 10m, multiplier: null, percentage: 50m, baseAmount: 100m);

        Assert.False(result.IsValid);
        Assert.Equal(OneTimeDeductionRules.ValueComponentsInvalidCode, result.ErrorCode);
    }

    // ── PORCENTAJE_SOBRE_BASE: percentage % of the base ───────────────────────────────────────────────
    [Fact]
    public void ComputeAmount_PercentageOnBase_TakesThePercentageOfTheBase()
    {
        var result = OneTimeDeductionRules.ComputeAmount(
            OneTimeDeductionCalculationMethods.PercentageOnBase,
            quantity: null, unitValue: null, multiplier: null, percentage: 15m, baseAmount: 600m);

        Assert.True(result.IsValid);
        Assert.Equal(90.00m, result.Amount);
    }

    [Fact]
    public void ComputeAmount_PercentageOnBase_RoundsHalfUpToTwoDecimals()
    {
        // 33.33% of 100 = 33.33 exactly; 12.5% of 33.33 = 4.16625 → 4.17.
        var result = OneTimeDeductionRules.ComputeAmount(
            OneTimeDeductionCalculationMethods.PercentageOnBase,
            quantity: null, unitValue: null, multiplier: null, percentage: 12.5m, baseAmount: 33.33m);

        Assert.True(result.IsValid);
        Assert.Equal(4.17m, result.Amount);
    }

    [Fact]
    public void ComputeAmount_PercentageOnBase_RejectsTheComponentsOfTheOtherMethod()
    {
        var result = OneTimeDeductionRules.ComputeAmount(
            OneTimeDeductionCalculationMethods.PercentageOnBase,
            quantity: 2m, unitValue: null, multiplier: null, percentage: 10m, baseAmount: 100m);

        Assert.False(result.IsValid);
        Assert.Equal(OneTimeDeductionRules.ValueComponentsInvalidCode, result.ErrorCode);
    }

    [Fact]
    public void ComputeAmount_RejectsAnUnknownMethod()
    {
        var result = OneTimeDeductionRules.ComputeAmount(
            "REGLA_DE_TRES", quantity: 1m, unitValue: 1m, multiplier: null, percentage: null, baseAmount: null);

        Assert.False(result.IsValid);
        Assert.Equal(OneTimeDeductionRules.ValueMethodInvalidCode, result.ErrorCode);
    }

    // ── ValidateValue: the SERVER owns the amount ─────────────────────────────────────────────────────
    [Fact]
    public void ValidateValue_AComputedValueMayOmitTheAmount_AndTheServersStands()
    {
        var result = OneTimeDeductionRules.ValidateValue(
            isFixedValue: false,
            OneTimeDeductionCalculationMethods.PercentageOnBase,
            quantity: null, unitValue: null, multiplier: null, percentage: 10m, baseAmount: 250m,
            amount: null);

        Assert.True(result.IsValid);
        Assert.Equal(25.00m, result.Amount);
    }

    [Fact]
    public void ValidateValue_ALyingAmountIsRejectedWithTheExpectedFigure()
    {
        // The client says "charge $500" but its own components say $25. The server wins, and it says so.
        var result = OneTimeDeductionRules.ValidateValue(
            isFixedValue: false,
            OneTimeDeductionCalculationMethods.PercentageOnBase,
            quantity: null, unitValue: null, multiplier: null, percentage: 10m, baseAmount: 250m,
            amount: 500m);

        Assert.False(result.IsValid);
        Assert.Equal(OneTimeDeductionRules.AmountMismatchCode, result.ErrorCode);
        Assert.Equal(25.00m, result.ExpectedAmount);
    }

    [Fact]
    public void ValidateValue_AnAmountThatMatchesItsComponentsIsAccepted()
    {
        var result = OneTimeDeductionRules.ValidateValue(
            isFixedValue: false,
            OneTimeDeductionCalculationMethods.QuantityTimesValue,
            quantity: 2m, unitValue: 40m, multiplier: null, percentage: null, baseAmount: null,
            amount: 80m);

        Assert.True(result.IsValid);
        Assert.Equal(80.00m, result.Amount);
    }

    [Fact]
    public void ValidateValue_AFixedValueCarriesNoComponents()
    {
        var withComponents = OneTimeDeductionRules.ValidateValue(
            isFixedValue: true,
            OneTimeDeductionCalculationMethods.PercentageOnBase,
            quantity: null, unitValue: null, multiplier: null, percentage: 10m, baseAmount: 100m,
            amount: 10m);

        Assert.False(withComponents.IsValid);
        Assert.Equal(OneTimeDeductionRules.ValueFixedWithComponentsCode, withComponents.ErrorCode);

        var clean = OneTimeDeductionRules.ValidateValue(
            isFixedValue: true,
            calculationMethod: null,
            quantity: null, unitValue: null, multiplier: null, percentage: null, baseAmount: null,
            amount: 75.50m);

        Assert.True(clean.IsValid);
        Assert.Equal(75.50m, clean.Amount);
    }

    [Fact]
    public void ValidateValue_AFixedValueRequiresAPositiveAmount()
    {
        var result = OneTimeDeductionRules.ValidateValue(
            isFixedValue: true,
            calculationMethod: null,
            quantity: null, unitValue: null, multiplier: null, percentage: null, baseAmount: null,
            amount: 0m);

        Assert.False(result.IsValid);
        Assert.Equal(OneTimeDeductionRules.ValueAmountInvalidCode, result.ErrorCode);
    }

    [Fact]
    public void ValidateValue_AComputedValueRequiresAMethod()
    {
        var result = OneTimeDeductionRules.ValidateValue(
            isFixedValue: false,
            calculationMethod: null,
            quantity: 2m, unitValue: 10m, multiplier: null, percentage: null, baseAmount: null,
            amount: 20m);

        Assert.False(result.IsValid);
        Assert.Equal(OneTimeDeductionRules.ValueMethodRequiredCode, result.ErrorCode);
    }

    // ── State machine: APLICADO is REVERSIBLE ─────────────────────────────────────────────────────────
    [Fact]
    public void CanTransition_MirrorsTheDocumentedStateMachine()
    {
        Assert.True(OneTimeDeductionRules.CanTransition(OneTimeDeductionStatuses.EnRevision, OneTimeDeductionStatuses.Autorizado));
        Assert.True(OneTimeDeductionRules.CanTransition(OneTimeDeductionStatuses.EnRevision, OneTimeDeductionStatuses.Rechazado));
        Assert.True(OneTimeDeductionRules.CanTransition(OneTimeDeductionStatuses.Autorizado, OneTimeDeductionStatuses.Aplicado));
        Assert.True(OneTimeDeductionRules.CanTransition(OneTimeDeductionStatuses.Autorizado, OneTimeDeductionStatuses.Anulado));

        // The REVERSAL: unlike the recurring FINALIZADO, an APLICADO deduction goes back to AUTORIZADO.
        Assert.True(OneTimeDeductionRules.CanTransition(OneTimeDeductionStatuses.Aplicado, OneTimeDeductionStatuses.Autorizado));

        Assert.False(OneTimeDeductionRules.CanTransition(OneTimeDeductionStatuses.Rechazado, OneTimeDeductionStatuses.Autorizado));
        Assert.False(OneTimeDeductionRules.CanTransition(OneTimeDeductionStatuses.Anulado, OneTimeDeductionStatuses.Autorizado));
        Assert.False(OneTimeDeductionRules.CanTransition(OneTimeDeductionStatuses.EnRevision, OneTimeDeductionStatuses.Aplicado));
    }

    [Fact]
    public void CanApply_OnlyOnceAndOnlyWhenAuthorized()
    {
        Assert.True(OneTimeDeductionRules.CanApply(OneTimeDeductionStatuses.Autorizado, hasActiveApplication: false).IsValid);

        var notAuthorized = OneTimeDeductionRules.CanApply(OneTimeDeductionStatuses.EnRevision, hasActiveApplication: false);
        Assert.False(notAuthorized.IsValid);
        Assert.Equal(OneTimeDeductionRules.NotApplicableCode, notAuthorized.ErrorCode);

        var alreadyApplied = OneTimeDeductionRules.CanApply(OneTimeDeductionStatuses.Autorizado, hasActiveApplication: true);
        Assert.False(alreadyApplied.IsValid);
        Assert.Equal(OneTimeDeductionRules.AlreadyAppliedCode, alreadyApplied.ErrorCode);
    }

    [Fact]
    public void CanRetarget_OnlyWhileAuthorized()
    {
        Assert.True(OneTimeDeductionRules.CanRetarget(OneTimeDeductionStatuses.Autorizado).IsValid);

        var applied = OneTimeDeductionRules.CanRetarget(OneTimeDeductionStatuses.Aplicado);
        Assert.False(applied.IsValid);
        Assert.Equal(OneTimeDeductionRules.NotRetargetableCode, applied.ErrorCode);
    }

    [Fact]
    public void CanRevertApplication_OnlyWhenApplied()
    {
        Assert.True(OneTimeDeductionRules.CanRevertApplication(OneTimeDeductionStatuses.Aplicado).IsValid);

        var notApplied = OneTimeDeductionRules.CanRevertApplication(OneTimeDeductionStatuses.Autorizado);
        Assert.False(notApplied.IsValid);
        Assert.Equal(OneTimeDeductionRules.ApplicationNotRevertibleCode, notApplied.ErrorCode);
    }
}

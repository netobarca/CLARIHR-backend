using CLARIHR.Application.Features.PersonnelFiles.PersonnelTransactions;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// The "otras transacciones de personal" flow/validation arithmetic's critical golden suite (REQ-003 PR-2,
/// the wave gate) — the RATIFIED Anexo A.4 rule/domain cases encoded as blocking assertions. The module is
/// 100% pure so these fully pin the one-decision state machine, the double anti-self-approval check, the
/// suspension/deduction block validation, the calendar-inclusive suspension days, the range-intersection
/// primitive (suspension overlap RN-18 / availability intersection RN-15) and the availability-window
/// normalization. Reference country: El Salvador.
/// </summary>
public sealed class PersonnelTransactionRulesTests
{
    // ── A.4-2: double anti-self-approval (subject OR registrar → self; third party → not) ─────────────

    [Fact]
    public void IsSelfDecision_CurrentUserIsSubject_IsSelf()
    {
        Assert.True(PersonnelTransactionRules.IsSelfDecision(
            linkedUserId: "user-subject",
            registeredByUserId: "user-hr",
            currentUserId: "user-subject"));
    }

    [Fact]
    public void IsSelfDecision_CurrentUserIsRegistrar_IsSelf()
    {
        Assert.True(PersonnelTransactionRules.IsSelfDecision(
            linkedUserId: "user-subject",
            registeredByUserId: "user-hr",
            currentUserId: "user-hr"));
    }

    [Fact]
    public void IsSelfDecision_ThirdParty_IsNotSelf()
    {
        Assert.False(PersonnelTransactionRules.IsSelfDecision(
            linkedUserId: "user-subject",
            registeredByUserId: "user-hr",
            currentUserId: "user-authorizer"));
    }

    [Fact]
    public void IsSelfDecision_IgnoresCaseAndWhitespace_AndNullReferences()
    {
        Assert.True(PersonnelTransactionRules.IsSelfDecision("USER-SUBJECT", null, "  user-subject  "));
        Assert.False(PersonnelTransactionRules.IsSelfDecision(null, null, "user-authorizer"));
    }

    // ── A.4-4: suspension block validation (four combinations) + calendar-inclusive days ──────────────

    [Fact]
    public void ValidateSuspensionBlock_DatesOnTypeWithoutFlag_NotAllowed()
    {
        var result = PersonnelTransactionRules.ValidateSuspensionBlock(
            typeAppliesSuspension: false,
            start: new DateOnly(2026, 4, 28),
            end: new DateOnly(2026, 5, 3));

        Assert.Equal(SuspensionBlockValidation.NotAllowedForType, result);
    }

    [Fact]
    public void ValidateSuspensionBlock_FlagWithoutDates_DatesRequired()
    {
        Assert.Equal(
            SuspensionBlockValidation.DatesRequired,
            PersonnelTransactionRules.ValidateSuspensionBlock(true, start: null, end: null));
        Assert.Equal(
            SuspensionBlockValidation.DatesRequired,
            PersonnelTransactionRules.ValidateSuspensionBlock(true, start: new DateOnly(2026, 4, 28), end: null));
    }

    [Fact]
    public void ValidateSuspensionBlock_StartAfterEnd_RangeInvalid()
    {
        var result = PersonnelTransactionRules.ValidateSuspensionBlock(
            typeAppliesSuspension: true,
            start: new DateOnly(2026, 5, 3),
            end: new DateOnly(2026, 4, 28));

        Assert.Equal(SuspensionBlockValidation.RangeInvalid, result);
    }

    [Fact]
    public void ValidateSuspensionBlock_FlagWithCoherentDates_Valid()
    {
        var result = PersonnelTransactionRules.ValidateSuspensionBlock(
            typeAppliesSuspension: true,
            start: new DateOnly(2026, 4, 28),
            end: new DateOnly(2026, 5, 3));

        Assert.Equal(SuspensionBlockValidation.Valid, result);
    }

    [Fact]
    public void ValidateSuspensionBlock_NoFlagNoDates_Valid()
    {
        Assert.Equal(
            SuspensionBlockValidation.Valid,
            PersonnelTransactionRules.ValidateSuspensionBlock(false, start: null, end: null));
    }

    [Fact]
    public void SuspensionDays_IsCalendarInclusive()
    {
        // 28-Apr → 3-May 2026 inclusive: Apr 28, 29, 30, May 1, 2, 3 = 6 days.
        Assert.Equal(6, PersonnelTransactionRules.SuspensionDays(new DateOnly(2026, 4, 28), new DateOnly(2026, 5, 3)));
        // Single-day suspension = 1.
        Assert.Equal(1, PersonnelTransactionRules.SuspensionDays(new DateOnly(2026, 5, 3), new DateOnly(2026, 5, 3)));
    }

    [Fact]
    public void SuspensionDays_EndBeforeStart_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            PersonnelTransactionRules.SuspensionDays(new DateOnly(2026, 5, 3), new DateOnly(2026, 4, 28)));
    }

    // ── A.4-4: deduction block validation ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(true, 25.0, DeductionValidation.Valid)]
    [InlineData(true, 0.0, DeductionValidation.AmountRequired)]
    [InlineData(true, null, DeductionValidation.AmountRequired)]
    [InlineData(false, null, DeductionValidation.Valid)]
    [InlineData(false, 25.0, DeductionValidation.Valid)]
    public void ValidateDeduction_FlagRequiresPositiveAmount(bool hasDeduction, double? amount, DeductionValidation expected)
    {
        var result = PersonnelTransactionRules.ValidateDeduction(hasDeduction, amount is null ? null : (decimal)amount.Value);
        Assert.Equal(expected, result);
    }

    // ── A.4-6: range intersection (RN-18 overlap / RN-15 availability) ────────────────────────────────

    [Fact]
    public void RangesOverlap_SuspensionFromPreviousMonth_Intersects()
    {
        // Query range 01–15 Jan 2026 vs suspension 28 Dec 2025 → 03 Jan 2026 → intersects.
        Assert.True(PersonnelTransactionRules.RangesOverlap(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15),
            new DateOnly(2025, 12, 28), new DateOnly(2026, 1, 3)));
    }

    [Fact]
    public void RangesOverlap_SingleDayContractEnd_Intersects()
    {
        // Query range 01–15 Jan vs a plaza EndDate on the 10th (single-day range) → intersects.
        Assert.True(PersonnelTransactionRules.RangesOverlap(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15),
            new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 10)));
    }

    [Fact]
    public void RangesOverlap_NonTouchingRanges_DoNotIntersect()
    {
        Assert.False(PersonnelTransactionRules.RangesOverlap(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15),
            new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 5)));
    }

    [Fact]
    public void RangesOverlap_AdjacentBoundaries_AreInclusive()
    {
        // Touching at a single boundary day counts as an overlap (inclusive).
        Assert.True(PersonnelTransactionRules.RangesOverlap(
            new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 10),
            new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 20)));
    }

    // ── CanTransition: the one-decision state machine (RN-01) ─────────────────────────────────────────

    [Theory]
    [InlineData(PersonnelTransactionStatuses.EnRevision, PersonnelTransactionStatuses.Aplicada, true)]
    [InlineData(PersonnelTransactionStatuses.EnRevision, PersonnelTransactionStatuses.Rechazada, true)]
    [InlineData(PersonnelTransactionStatuses.EnRevision, PersonnelTransactionStatuses.Anulada, false)]
    [InlineData(PersonnelTransactionStatuses.Aplicada, PersonnelTransactionStatuses.Aplicada, false)]
    [InlineData(PersonnelTransactionStatuses.Aplicada, PersonnelTransactionStatuses.Rechazada, false)]
    [InlineData(PersonnelTransactionStatuses.Rechazada, PersonnelTransactionStatuses.Aplicada, false)]
    [InlineData(PersonnelTransactionStatuses.Anulada, PersonnelTransactionStatuses.Aplicada, false)]
    public void CanTransition_ViaDecision(string from, string to, bool expected)
    {
        Assert.Equal(expected, PersonnelTransactionRules.CanTransition(from, to, PersonnelTransactionTransitionVia.Decision));
    }

    [Theory]
    [InlineData(PersonnelTransactionStatuses.EnRevision, PersonnelTransactionStatuses.Anulada, true)]
    [InlineData(PersonnelTransactionStatuses.Aplicada, PersonnelTransactionStatuses.Anulada, true)]
    [InlineData(PersonnelTransactionStatuses.Rechazada, PersonnelTransactionStatuses.Anulada, false)]
    [InlineData(PersonnelTransactionStatuses.Anulada, PersonnelTransactionStatuses.Anulada, false)]
    [InlineData(PersonnelTransactionStatuses.Aplicada, PersonnelTransactionStatuses.Rechazada, false)]
    public void CanTransition_ViaAnnulment(string from, string to, bool expected)
    {
        Assert.Equal(expected, PersonnelTransactionRules.CanTransition(from, to, PersonnelTransactionTransitionVia.Annulment));
    }

    [Fact]
    public void CanTransition_EmptyStatus_IsFalse()
    {
        Assert.False(PersonnelTransactionRules.CanTransition("", PersonnelTransactionStatuses.Aplicada, PersonnelTransactionTransitionVia.Decision));
        Assert.False(PersonnelTransactionRules.CanTransition(PersonnelTransactionStatuses.EnRevision, "  ", PersonnelTransactionTransitionVia.Decision));
    }

    // ── BuildAvailabilityWindow (RF-013) ──────────────────────────────────────────────────────────────

    [Fact]
    public void BuildAvailabilityWindow_CoherentRange_IsValid()
    {
        var result = PersonnelTransactionRules.BuildAvailabilityWindow(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 15));

        Assert.True(result.IsValid);
        Assert.NotNull(result.Window);
        Assert.Equal(new DateOnly(2026, 1, 1), result.Window!.Start);
        Assert.Equal(new DateOnly(2026, 1, 15), result.Window.End);
    }

    [Fact]
    public void BuildAvailabilityWindow_StartAfterEnd_IsInvalid()
    {
        var result = PersonnelTransactionRules.BuildAvailabilityWindow(new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 1));

        Assert.False(result.IsValid);
        Assert.Null(result.Window);
    }

    [Fact]
    public void BuildAvailabilityWindow_SingleDay_IsValid()
    {
        var result = PersonnelTransactionRules.BuildAvailabilityWindow(new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 10));

        Assert.True(result.IsValid);
        Assert.Equal(new DateOnly(2026, 1, 10), result.Window!.Start);
    }
}

using CLARIHR.Application.Features.PersonnelFiles.Compensation;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// The golden suite of the recurring-deduction arithmetic (REQ-008 §5 — the GATE of PR-2). The numbers come from
/// the RATIFIED analysis (Anexo A.3, cases 1–6); the accountant's sign-off on A.3 is a business ratification that
/// remains on the deployment checklist — the arithmetic itself is standard French amortization and is pinned here.
/// </summary>
public sealed class RecurringDeductionRulesTests
{
    private const string Mensual = RecurringDeductionFrequencies.Mensual;
    private const string Quincenal = RecurringDeductionFrequencies.Quincenal;

    // ── A.3 case 1 — French amortization ──────────────────────────────────────────────────────────────
    [Fact]
    public void BuildAmortizationSchedule_GoldenCase1_MatchesTheAccountantsTable()
    {
        var schedule = RecurringDeductionRules.BuildAmortizationSchedule(1000.00m, 12.00m, 12, Mensual);

        Assert.Equal(12, schedule.Count);

        // A fixed quota of $88.85 (P·i / (1 − (1+i)^−n) with i = 12% / 12 = 1%).
        Assert.Equal(88.85m, schedule[0].Amount);

        // Installment 1 = $10.00 interest + $78.85 capital.
        Assert.Equal(10.00m, schedule[0].InterestAmount);
        Assert.Equal(78.85m, schedule[0].CapitalAmount);

        // The balance decreases to exactly $0.00 and Σ capital == the principal, to the cent.
        Assert.Equal(0m, schedule[^1].ClosingBalance);
        Assert.Equal(1000.00m, schedule.Sum(row => row.CapitalAmount));
    }

    [Fact]
    public void BuildAmortizationSchedule_EveryInstallmentSplitsIntoCapitalPlusInterest()
    {
        var schedule = RecurringDeductionRules.BuildAmortizationSchedule(1000.00m, 12.00m, 12, Mensual);

        Assert.All(schedule, row =>
            Assert.Equal(row.Amount, row.CapitalAmount + row.InterestAmount));
    }

    [Fact]
    public void BuildAmortizationSchedule_QuincenalHalvesThePeriodRate()
    {
        // Nominal annual ÷ periods of the installment frequency (P-03): QUINCENAL = 24 periods → i = 0.5%.
        var schedule = RecurringDeductionRules.BuildAmortizationSchedule(1000.00m, 12.00m, 12, Quincenal);

        Assert.Equal(5.00m, schedule[0].InterestAmount);
        Assert.Equal(1000.00m, schedule.Sum(row => row.CapitalAmount));
    }

    [Fact]
    public void BuildAmortizationSchedule_RejectsAQuotaThatCannotAmortize()
    {
        // A rate so high the fixed quota never covers the interest is not amortizable.
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            RecurringDeductionRules.BuildAmortizationSchedule(1000.00m, 0m, 12, Mensual));
    }

    // ── A.3 case 2 — segment plan without interest ────────────────────────────────────────────────────
    [Fact]
    public void NormalizePlan_GoldenCase2_DerivesCountAndTotalFromTheSegments()
    {
        var segments = new List<RecurringDeductionSegment>
        {
            new(1, 6, 50.00m),
            new(7, 12, 75.00m),
        };

        var normalization = RecurringDeductionRules.NormalizePlan(
            segments,
            isIndefinite: false,
            usesCompoundInterest: false,
            principalAmount: null,
            annualRatePercent: null,
            plannedInstallments: null,
            Mensual);

        Assert.True(normalization.IsValid);
        Assert.Equal(12, normalization.Plan!.InstallmentCount);
        Assert.Equal(750.00m, normalization.Plan.TotalAmount);

        // The value depends on the segment the installment falls in.
        Assert.Equal(50.00m, RecurringDeductionRules.InstallmentAmountFor(6, normalization.Plan));
        Assert.Equal(75.00m, RecurringDeductionRules.InstallmentAmountFor(7, normalization.Plan));
    }

    [Fact]
    public void SettlementBalance_GoldenCase2_ChargedAndOutstandingAreExactAfterFourApplications()
    {
        var plan = FinitePlan([new(1, 6, 50.00m), new(7, 12, 75.00m)]);

        // Four applications of $50 = $200 charged; $550 left of the $750 plan.
        var balance = RecurringDeductionRules.SettlementBalance(plan, chargedAmount: 200.00m, chargedCapital: 0m);

        Assert.Equal(550.00m, balance);
    }

    // ── A.3 case 3 — an extraordinary payment shortens the term ───────────────────────────────────────
    [Fact]
    public void RecomputeFromBalance_GoldenCase3_ExtraordinaryPaymentCutsTheTermNotTheQuota()
    {
        var schedule = RecurringDeductionRules.BuildAmortizationSchedule(1000.00m, 12.00m, 12, Mensual);

        // Three regular installments applied, then a $200 abono.
        var balanceAfterThree = schedule[2].ClosingBalance;
        var balanceAfterAbono = balanceAfterThree - 200.00m;

        var rebuilt = RecurringDeductionRules.RecomputeFromBalance(balanceAfterAbono, 88.85m, 12.00m, Mensual);

        // The term falls: 9 installments were left, fewer remain — and the quota is untouched (P-04).
        Assert.True(rebuilt.Count < 9);
        Assert.Equal(88.85m, rebuilt[0].Amount);

        // Σ capital across everything charged still equals the principal, to the cent.
        var capitalCharged = schedule.Take(3).Sum(row => row.CapitalAmount) + 200.00m;
        Assert.Equal(1000.00m, capitalCharged + rebuilt.Sum(row => row.CapitalAmount));
    }

    // ── A.3 case 4 — payoff ───────────────────────────────────────────────────────────────────────────
    [Fact]
    public void CanApplyExtraordinary_GoldenCase4_PayingExactlyTheBalanceIsAllowedButOverpayingIsNot()
    {
        var today = new DateOnly(2026, 7, 12);

        var payoff = RecurringDeductionRules.CanApplyExtraordinary(
            RecurringDeductionStatuses.Vigente, today, today, isIndefinite: false, amount: 300.00m, outstandingBalance: 300.00m);
        Assert.True(payoff.IsValid);

        var overpay = RecurringDeductionRules.CanApplyExtraordinary(
            RecurringDeductionStatuses.Vigente, today, today, isIndefinite: false, amount: 300.01m, outstandingBalance: 300.00m);
        Assert.False(overpay.IsValid);
        Assert.Equal(RecurringDeductionRules.ExtraordinaryExceedsBalanceCode, overpay.ErrorCode);
    }

    [Fact]
    public void CanApplyExtraordinary_IsRejectedOnASuspendedCredit()
    {
        var today = new DateOnly(2026, 7, 12);

        var result = RecurringDeductionRules.CanApplyExtraordinary(
            RecurringDeductionStatuses.Suspendido, today, today, isIndefinite: false, amount: 100.00m, outstandingBalance: 300.00m);

        Assert.False(result.IsValid);
        Assert.Equal(RecurringDeductionRules.ExtraordinaryNotApplicableCode, result.ErrorCode);
    }

    [Fact]
    public void IsPlanComplete_APayoffClosesThePlanEvenWithInstallmentsLeftInTheSequence()
    {
        var plan = FinitePlan([new(1, 12, 50.00m)]);

        // $600 plan: two regular installments ($100) plus a $500 abono leaves nothing owed.
        var complete = RecurringDeductionRules.IsPlanComplete(
            plan, chargedAmount: 600.00m, chargedCapital: 0m, appliedNumbers: new HashSet<int> { 1, 2 });

        Assert.True(complete);
    }

    // ── A.3 case 5 — exception months push the plan forward ───────────────────────────────────────────
    [Fact]
    public void BuildProjection_GoldenCase5_AnExceptedMonthPushesTheInstallmentToTheNextPeriod()
    {
        var plan = FinitePlan([new(1, 6, 50.00m)]);
        var exceptionMonths = new HashSet<int> { 12 };

        var projection = RecurringDeductionRules.BuildProjection(
            plan,
            installmentStartDate: new DateOnly(2026, 10, 1),
            exceptionMonths,
            appliedNumbers: new HashSet<int>(),
            today: new DateOnly(2026, 10, 1));

        // October, November, then December is skipped → installment 3 lands in January (the plan runs longer).
        Assert.Equal(new DateOnly(2026, 10, 1), projection[0].TheoreticalDueDate);
        Assert.Equal(new DateOnly(2026, 11, 1), projection[1].TheoreticalDueDate);
        Assert.Equal(new DateOnly(2027, 1, 1), projection[2].TheoreticalDueDate);

        // Nothing is lost: the plan still has all six installments.
        Assert.Equal(6, projection.Count);
        Assert.DoesNotContain(projection, item => item.TheoreticalDueDate.Month == 12);
    }

    // ── A.3 case 6 — application frequency divides the installment ────────────────────────────────────
    [Fact]
    public void SplitByApplicationFrequency_GoldenCase6_SplitsEvenlyAndTheLastPartAbsorbsTheRemainder()
    {
        Assert.Equal([50.00m, 50.00m], RecurringDeductionRules.SplitByApplicationFrequency(100.00m, 2));
        Assert.Equal([33.33m, 33.33m, 33.34m], RecurringDeductionRules.SplitByApplicationFrequency(100.00m, 3));
    }

    [Fact]
    public void ValidateFrequencyPair_AcceptsAFasterApplicationCadenceAndRejectsASlowerOne()
    {
        // A monthly quota applied fortnightly = 2 parts.
        Assert.True(RecurringDeductionRules.ValidateFrequencyPair(Mensual, Quincenal).IsValid);
        Assert.Equal(2, RecurringDeductionRules.ApplicationPartsPerInstallment(Mensual, Quincenal));

        // The inverse (a fortnightly quota applied monthly) is rejected (P-06).
        var inverse = RecurringDeductionRules.ValidateFrequencyPair(Quincenal, Mensual);
        Assert.False(inverse.IsValid);
        Assert.Equal(RecurringDeductionRules.ApplicationFrequencyInvalidCode, inverse.ErrorCode);
    }

    // ── Settlement balance with interest = outstanding CAPITAL (§0.9) ─────────────────────────────────
    [Fact]
    public void SettlementBalance_WithInterest_IsTheOutstandingCapitalNotTheSumOfFutureInstallments()
    {
        var schedule = RecurringDeductionRules.BuildAmortizationSchedule(1000.00m, 12.00m, 12, Mensual);
        var plan = InterestPlan(1000.00m, 12.00m, 12);

        // After three installments the charged capital is the sum of their capital portions.
        var chargedCapital = schedule.Take(3).Sum(row => row.CapitalAmount);
        var chargedAmount = schedule.Take(3).Sum(row => row.Amount);

        var balance = RecurringDeductionRules.SettlementBalance(plan, chargedAmount, chargedCapital);

        // The payoff is the capital still owed — NOT the sum of the nine remaining quotas (which carries interest).
        Assert.Equal(RecurringDeductionRules.Round2(1000.00m - chargedCapital), balance);
        Assert.True(balance < schedule.Skip(3).Sum(row => row.Amount));
    }

    [Fact]
    public void SettlementBalance_IsNullForAnIndefinitePlan()
    {
        var plan = IndefinitePlan(50.00m);

        Assert.Null(RecurringDeductionRules.SettlementBalance(plan, chargedAmount: 100.00m, chargedCapital: 0m));
    }

    // ── Segment validation ────────────────────────────────────────────────────────────────────────────
    [Fact]
    public void ValidateSegments_RejectsAGapInTheSequence()
    {
        var segments = new List<RecurringDeductionSegment> { new(1, 6, 50.00m), new(8, 12, 75.00m) };

        var result = RecurringDeductionRules.ValidateSegments(segments, isIndefinite: false, usesCompoundInterest: false);

        Assert.False(result.IsValid);
        Assert.Equal(RecurringDeductionRules.SegmentsNotContiguousCode, result.ErrorCode);
    }

    [Fact]
    public void ValidateSegments_RejectsAnOverlap()
    {
        var segments = new List<RecurringDeductionSegment> { new(1, 6, 50.00m), new(5, 12, 75.00m) };

        var result = RecurringDeductionRules.ValidateSegments(segments, isIndefinite: false, usesCompoundInterest: false);

        Assert.False(result.IsValid);
        Assert.Equal(RecurringDeductionRules.SegmentsNotContiguousCode, result.ErrorCode);
    }

    [Fact]
    public void ValidateSegments_RejectsASequenceThatDoesNotStartAtOne()
    {
        var segments = new List<RecurringDeductionSegment> { new(2, 6, 50.00m) };

        var result = RecurringDeductionRules.ValidateSegments(segments, isIndefinite: false, usesCompoundInterest: false);

        Assert.False(result.IsValid);
        Assert.Equal(RecurringDeductionRules.SegmentsNotContiguousCode, result.ErrorCode);
    }

    [Fact]
    public void ValidateSegments_AnIndefinitePlanIsExactlyOneOpenSegment()
    {
        var open = new List<RecurringDeductionSegment> { new(1, null, 50.00m) };
        Assert.True(RecurringDeductionRules.ValidateSegments(open, isIndefinite: true, usesCompoundInterest: false).IsValid);

        // A closed segment list cannot describe an indefinite plan.
        var closed = new List<RecurringDeductionSegment> { new(1, 12, 50.00m) };
        var result = RecurringDeductionRules.ValidateSegments(closed, isIndefinite: true, usesCompoundInterest: false);
        Assert.False(result.IsValid);
        Assert.Equal(RecurringDeductionRules.SegmentsIndefiniteShapeCode, result.ErrorCode);
    }

    [Fact]
    public void ValidateSegments_AFinitePlanCannotEndInAnOpenSegment()
    {
        var segments = new List<RecurringDeductionSegment> { new(1, null, 50.00m) };

        var result = RecurringDeductionRules.ValidateSegments(segments, isIndefinite: false, usesCompoundInterest: false);

        Assert.False(result.IsValid);
        Assert.Equal(RecurringDeductionRules.SegmentsNotContiguousCode, result.ErrorCode);
    }

    [Fact]
    public void ValidateSegments_ACompoundInterestCreditCarriesNoSegments()
    {
        var segments = new List<RecurringDeductionSegment> { new(1, 12, 50.00m) };

        var result = RecurringDeductionRules.ValidateSegments(segments, isIndefinite: false, usesCompoundInterest: true);

        Assert.False(result.IsValid);
        Assert.Equal(RecurringDeductionRules.SegmentsWithInterestCode, result.ErrorCode);
    }

    // ── Plan normalization guards ─────────────────────────────────────────────────────────────────────
    [Fact]
    public void NormalizePlan_ACompoundInterestCreditCannotBeIndefinite()
    {
        var normalization = RecurringDeductionRules.NormalizePlan(
            segments: [],
            isIndefinite: true,
            usesCompoundInterest: true,
            principalAmount: 1000.00m,
            annualRatePercent: 12.00m,
            plannedInstallments: 12,
            Mensual);

        Assert.False(normalization.IsValid);
        Assert.Equal(RecurringDeductionRules.InterestIndefiniteCode, normalization.ErrorCode);
    }

    [Fact]
    public void NormalizePlan_WithInterest_DerivesTheCountAndTheTotalFromTheAmortization()
    {
        var normalization = RecurringDeductionRules.NormalizePlan(
            segments: [],
            isIndefinite: false,
            usesCompoundInterest: true,
            principalAmount: 1000.00m,
            annualRatePercent: 12.00m,
            plannedInstallments: 12,
            Mensual);

        Assert.True(normalization.IsValid);
        Assert.Equal(12, normalization.Plan!.InstallmentCount);

        // The total is what the employee ends up paying: principal + interest (> the principal).
        Assert.True(normalization.Plan.TotalAmount > 1000.00m);
    }

    // ── Settlement action + application guards ────────────────────────────────────────────────────────
    [Fact]
    public void ValidateSettlementAction_DescontarSaldoIsMeaninglessForAnIndefinitePlan()
    {
        var result = RecurringDeductionRules.ValidateSettlementAction(
            RecurringDeductionSettlementActions.DescontarSaldo, isIndefinite: true);

        Assert.False(result.IsValid);
        Assert.Equal(RecurringDeductionRules.SettlementActionIndefiniteCode, result.ErrorCode);

        Assert.True(RecurringDeductionRules.ValidateSettlementAction(
            RecurringDeductionSettlementActions.Cancelar, isIndefinite: true).IsValid);
    }

    [Fact]
    public void CanApplyInstallment_AFutureDatedCreditCannotBeChargedYet()
    {
        var plan = FinitePlan([new(1, 12, 50.00m)]);

        var result = RecurringDeductionRules.CanApplyInstallment(
            RecurringDeductionStatuses.Vigente,
            effectiveDate: new DateOnly(2026, 9, 1),
            today: new DateOnly(2026, 7, 12),
            installmentNumber: 1,
            plan,
            appliedNumbers: new HashSet<int>());

        Assert.False(result.IsValid);
        Assert.Equal(RecurringDeductionRules.InstallmentNotDueYetCode, result.ErrorCode);
    }

    [Fact]
    public void CanApplyInstallment_EnforcesTheStrictSequenceAndThePlanCeiling()
    {
        var plan = FinitePlan([new(1, 2, 50.00m)]);
        var today = new DateOnly(2026, 7, 12);

        var outOfSequence = RecurringDeductionRules.CanApplyInstallment(
            RecurringDeductionStatuses.Vigente, today, today, installmentNumber: 2, plan, new HashSet<int>());
        Assert.False(outOfSequence.IsValid);
        Assert.Equal(RecurringDeductionRules.InstallmentSequenceInvalidCode, outOfSequence.ErrorCode);

        var beyondPlan = RecurringDeductionRules.CanApplyInstallment(
            RecurringDeductionStatuses.Vigente, today, today, installmentNumber: 3, plan, new HashSet<int> { 1, 2 });
        Assert.False(beyondPlan.IsValid);
        Assert.Equal(RecurringDeductionRules.InstallmentExceedsPlanCode, beyondPlan.ErrorCode);
    }

    [Fact]
    public void CanTransition_MirrorsTheDocumentedStateMachine()
    {
        Assert.True(RecurringDeductionRules.CanTransition(RecurringDeductionStatuses.EnRevision, RecurringDeductionStatuses.Vigente));
        Assert.True(RecurringDeductionRules.CanTransition(RecurringDeductionStatuses.Vigente, RecurringDeductionStatuses.Suspendido));
        Assert.True(RecurringDeductionRules.CanTransition(RecurringDeductionStatuses.Suspendido, RecurringDeductionStatuses.Vigente));
        Assert.True(RecurringDeductionRules.CanTransition(RecurringDeductionStatuses.Finalizado, RecurringDeductionStatuses.Vigente));

        Assert.False(RecurringDeductionRules.CanTransition(RecurringDeductionStatuses.Rechazado, RecurringDeductionStatuses.Vigente));
        Assert.False(RecurringDeductionRules.CanTransition(RecurringDeductionStatuses.Anulado, RecurringDeductionStatuses.Vigente));
        Assert.False(RecurringDeductionRules.CanTransition(RecurringDeductionStatuses.EnRevision, RecurringDeductionStatuses.EnRevision));
    }

    // ── Charge level — the unit of the LEDGER when the application cadence is faster (D-10) ───────────
    [Fact]
    public void ChargeCount_AMonthlyQuotaChargedFortnightlyDoublesTheLedgerRows()
    {
        var plan = FinitePlan([new(1, 12, 100.00m)]);

        Assert.Equal(12, RecurringDeductionRules.ChargeCount(plan, Mensual));
        Assert.Equal(24, RecurringDeductionRules.ChargeCount(plan, Quincenal));
    }

    [Fact]
    public void ChargeSplitFor_SplitsEachQuotaAcrossItsApplicationParts()
    {
        var plan = FinitePlan([new(1, 12, 100.00m)]);

        // Charges 1 and 2 are the two halves of quota 1; charge 3 opens quota 2.
        Assert.Equal(50.00m, RecurringDeductionRules.ChargeSplitFor(1, plan, Quincenal).Amount);
        Assert.Equal(50.00m, RecurringDeductionRules.ChargeSplitFor(2, plan, Quincenal).Amount);
        Assert.Equal(50.00m, RecurringDeductionRules.ChargeSplitFor(3, plan, Quincenal).Amount);

        // Every charge of the plan adds up to the plan total, to the cent.
        var total = Enumerable.Range(1, 24).Sum(number => RecurringDeductionRules.ChargeSplitFor(number, plan, Quincenal).Amount);
        Assert.Equal(1200.00m, total);
    }

    [Fact]
    public void ChargeSplitFor_WithInterest_SplitsTheCapitalAndTheInterestOfEachQuota()
    {
        var plan = InterestPlan(1000.00m, 12.00m, 12);

        // Quota 1 of the golden table is $88.85 = $10.00 interest + $78.85 capital; charged fortnightly it lands
        // as two halves whose capital and interest each add up to the quota's exactly.
        var first = RecurringDeductionRules.ChargeSplitFor(1, plan, Quincenal);
        var second = RecurringDeductionRules.ChargeSplitFor(2, plan, Quincenal);

        Assert.Equal(88.85m, first.Amount + second.Amount);
        Assert.Equal(10.00m, first.InterestAmount + second.InterestAmount);
        Assert.Equal(78.85m, first.CapitalAmount + second.CapitalAmount);
    }

    [Fact]
    public void BuildChargeProjection_AdvancesByTheApplicationCadenceAndSkipsTheExceptionMonths()
    {
        var plan = FinitePlan([new(1, 3, 100.00m)]);
        var exceptionMonths = new HashSet<int> { 12 };

        var projection = RecurringDeductionRules.BuildChargeProjection(
            plan,
            Quincenal,
            installmentStartDate: new DateOnly(2026, 11, 1),
            exceptionMonths,
            appliedNumbers: new HashSet<int>(),
            today: new DateOnly(2026, 11, 1));

        // 3 monthly quotas charged fortnightly = 6 charges of $50.
        Assert.Equal(6, projection.Count);
        Assert.All(projection, item => Assert.Equal(50.00m, item.Amount));

        // The fortnightly dates skip December entirely (the plan runs longer, nothing is lost).
        Assert.DoesNotContain(projection, item => item.TheoreticalDueDate.Month == 12);
    }

    [Fact]
    public void CanApplyCharge_TheCeilingIsTheCHARGECountNotTheQuotaCount()
    {
        var plan = FinitePlan([new(1, 2, 100.00m)]);
        var today = new DateOnly(2026, 7, 12);
        var applied = new HashSet<int> { 1, 2, 3 };

        // 2 monthly quotas charged fortnightly = 4 charges: number 4 is still inside the plan...
        var inside = RecurringDeductionRules.CanApplyCharge(
            RecurringDeductionStatuses.Vigente, today, today, 4, plan, Quincenal, applied);
        Assert.True(inside.IsValid);

        // ...but number 5 exceeds it.
        var beyond = RecurringDeductionRules.CanApplyCharge(
            RecurringDeductionStatuses.Vigente, today, today, 5, plan, Quincenal, new HashSet<int> { 1, 2, 3, 4 });
        Assert.False(beyond.IsValid);
        Assert.Equal(RecurringDeductionRules.InstallmentExceedsPlanCode, beyond.ErrorCode);
    }

    private static RecurringDeductionPlan FinitePlan(List<RecurringDeductionSegment> segments)
    {
        var normalization = RecurringDeductionRules.NormalizePlan(
            segments, isIndefinite: false, usesCompoundInterest: false, null, null, null, Mensual);

        return normalization.Plan!;
    }

    private static RecurringDeductionPlan IndefinitePlan(decimal value)
    {
        var normalization = RecurringDeductionRules.NormalizePlan(
            [new RecurringDeductionSegment(1, null, value)],
            isIndefinite: true,
            usesCompoundInterest: false,
            null,
            null,
            null,
            Mensual);

        return normalization.Plan!;
    }

    private static RecurringDeductionPlan InterestPlan(decimal principal, decimal rate, int installments)
    {
        var normalization = RecurringDeductionRules.NormalizePlan(
            segments: [],
            isIndefinite: false,
            usesCompoundInterest: true,
            principal,
            rate,
            installments,
            Mensual);

        return normalization.Plan!;
    }
}

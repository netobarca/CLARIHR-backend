using CLARIHR.Application.Features.PersonnelFiles.Compensation;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// The recurring-income plan + lifecycle critical golden suite (PR-2, the REQ-005 wave gate) — the RATIFIED
/// Anexo A.3 rule/domain cases encoded as blocking assertions (the e2e cases are PR-3/PR-4/PR-6). The rules
/// module is 100% pure so these fully pin the plan normalization, the last-installment rounding adjustment,
/// the theoretical projection by frequency, the remaining/completion arithmetic, the state machine and the
/// settlement-action rule; the domain guards pin the custodied mutators. Reference country: El Salvador.
/// </summary>
public sealed class RecurringIncomeRulesTests
{
    private static readonly DateOnly Today = new(2026, 7, 9);

    // ── A.3-1: finite plan 6 × $50 = $300 ──────────────────────────────────────────────────────────

    [Fact]
    public void NormalizePlan_FiniteValueAndCount_DerivesTotal()
    {
        var result = RecurringIncomeRules.NormalizePlan(installmentValue: 50m, installmentCount: 6, totalAmount: null, isIndefinite: false);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Plan);
        Assert.Equal(6, result.Plan!.InstallmentCount);
        Assert.Equal(300m, result.Plan.TotalAmount);
        Assert.False(result.Plan.IsIndefinite);
    }

    [Fact]
    public void NormalizePlan_FiniteValueCountAndTotalCoherent_IsValid()
    {
        var result = RecurringIncomeRules.NormalizePlan(50m, 6, 300m, isIndefinite: false);

        Assert.True(result.IsValid);
        Assert.Equal(6, result.Plan!.InstallmentCount);
        Assert.Equal(300m, result.Plan.TotalAmount);
    }

    [Fact]
    public void IsPlanComplete_AfterAllSixInstallments_IsTrue()
    {
        var plan = RecurringIncomeRules.NormalizePlan(50m, 6, 300m, false).Plan!;

        Assert.False(RecurringIncomeRules.IsPlanComplete(plan, new HashSet<int> { 1, 2, 3, 4, 5 }));
        Assert.True(RecurringIncomeRules.IsPlanComplete(plan, new HashSet<int> { 1, 2, 3, 4, 5, 6 }));
    }

    [Fact]
    public void RemainingAmount_DecreasesWithEachApplication()
    {
        var plan = RecurringIncomeRules.NormalizePlan(50m, 6, 300m, false).Plan!;

        Assert.Equal(300m, RecurringIncomeRules.RemainingAmount(plan, new HashSet<int>()));
        Assert.Equal(250m, RecurringIncomeRules.RemainingAmount(plan, new HashSet<int> { 1 }));
        Assert.Equal(100m, RecurringIncomeRules.RemainingAmount(plan, new HashSet<int> { 1, 2, 3, 4 }));
        Assert.Equal(0m, RecurringIncomeRules.RemainingAmount(plan, new HashSet<int> { 1, 2, 3, 4, 5, 6 }));
    }

    // ── A.3-2: total $100 in 3 installments → 33.33 / 33.33 / 33.34 (last absorbs the remainder) ───────

    [Fact]
    public void InstallmentAmountFor_NonDivisibleTotal_LastAbsorbsRemainder()
    {
        var plan = RecurringIncomeRules.NormalizePlan(installmentValue: 33.33m, installmentCount: 3, totalAmount: 100m, isIndefinite: false).Plan!;

        Assert.Equal(33.33m, RecurringIncomeRules.InstallmentAmountFor(1, plan));
        Assert.Equal(33.33m, RecurringIncomeRules.InstallmentAmountFor(2, plan));
        Assert.Equal(33.34m, RecurringIncomeRules.InstallmentAmountFor(3, plan));

        var sum = RecurringIncomeRules.InstallmentAmountFor(1, plan)
            + RecurringIncomeRules.InstallmentAmountFor(2, plan)
            + RecurringIncomeRules.InstallmentAmountFor(3, plan);
        Assert.Equal(100m, sum);
    }

    [Fact]
    public void NormalizePlan_ValueAndTotalOnly_DerivesCount()
    {
        var result = RecurringIncomeRules.NormalizePlan(installmentValue: 33.33m, installmentCount: null, totalAmount: 100m, isIndefinite: false);

        Assert.True(result.IsValid);
        Assert.Equal(3, result.Plan!.InstallmentCount);
        Assert.Equal(100m, result.Plan.TotalAmount);
    }

    [Fact]
    public void RemainingAmount_UsesLastInstallmentAdjustment()
    {
        var plan = RecurringIncomeRules.NormalizePlan(33.33m, 3, 100m, false).Plan!;

        Assert.Equal(100m, RecurringIncomeRules.RemainingAmount(plan, new HashSet<int>()));
        Assert.Equal(33.34m, RecurringIncomeRules.RemainingAmount(plan, new HashSet<int> { 1, 2 }));
        Assert.Equal(0m, RecurringIncomeRules.RemainingAmount(plan, new HashSet<int> { 1, 2, 3 }));
    }

    // ── Plan-coherence rejections (RN-05) ──────────────────────────────────────────────────────────

    [Fact]
    public void NormalizePlan_IndefiniteWithLimits_Fails()
    {
        var withCount = RecurringIncomeRules.NormalizePlan(50m, 6, null, isIndefinite: true);
        var withTotal = RecurringIncomeRules.NormalizePlan(50m, null, 300m, isIndefinite: true);

        Assert.False(withCount.IsValid);
        Assert.Equal(RecurringIncomeRules.PlanIndefiniteWithLimitsCode, withCount.ErrorCode);
        Assert.False(withTotal.IsValid);
        Assert.Equal(RecurringIncomeRules.PlanIndefiniteWithLimitsCode, withTotal.ErrorCode);
    }

    [Fact]
    public void NormalizePlan_FiniteWithoutLimits_Fails()
    {
        var result = RecurringIncomeRules.NormalizePlan(50m, null, null, isIndefinite: false);

        Assert.False(result.IsValid);
        Assert.Equal(RecurringIncomeRules.PlanFiniteWithoutLimitsCode, result.ErrorCode);
    }

    [Fact]
    public void NormalizePlan_NonPositiveValue_Fails()
    {
        var result = RecurringIncomeRules.NormalizePlan(0m, 6, null, isIndefinite: false);

        Assert.False(result.IsValid);
        Assert.Equal(RecurringIncomeRules.PlanValueInvalidCode, result.ErrorCode);
    }

    [Fact]
    public void NormalizePlan_IncoherentCountValueTotal_Fails()
    {
        // value×(count−1) = 100 already exceeds the total → the last installment would be non-positive.
        var result = RecurringIncomeRules.NormalizePlan(installmentValue: 50m, installmentCount: 3, totalAmount: 100m, isIndefinite: false);

        Assert.False(result.IsValid);
        Assert.Equal(RecurringIncomeRules.PlanIncoherentCode, result.ErrorCode);
    }

    // ── A.3-7: indefinite plan (value + frequency only) ────────────────────────────────────────────

    [Fact]
    public void NormalizePlan_Indefinite_IsValidWithoutLimits()
    {
        var result = RecurringIncomeRules.NormalizePlan(75m, null, null, isIndefinite: true);

        Assert.True(result.IsValid);
        Assert.True(result.Plan!.IsIndefinite);
        Assert.Null(result.Plan.InstallmentCount);
        Assert.Null(result.Plan.TotalAmount);
    }

    [Fact]
    public void RemainingAmount_IndefinitePlan_IsNull()
    {
        var plan = RecurringIncomeRules.NormalizePlan(75m, null, null, true).Plan!;

        Assert.Null(RecurringIncomeRules.RemainingAmount(plan, new HashSet<int> { 1, 2, 3 }));
        Assert.False(RecurringIncomeRules.IsPlanComplete(plan, new HashSet<int> { 1, 2, 3 }));
    }

    [Fact]
    public void ValidateSettlementAction_PagarSaldoOnIndefinite_Fails()
    {
        var result = RecurringIncomeRules.ValidateSettlementAction(RecurringIncomeSettlementActions.PagarSaldo, isIndefinite: true);

        Assert.False(result.IsValid);
        Assert.Equal(RecurringIncomeRules.SettlementActionIndefiniteCode, result.ErrorCode);
    }

    [Fact]
    public void ValidateSettlementAction_CancelarOnIndefinite_Ok()
    {
        var result = RecurringIncomeRules.ValidateSettlementAction(RecurringIncomeSettlementActions.Cancelar, isIndefinite: true);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void ValidateSettlementAction_PagarSaldoOnFinite_Ok()
    {
        var result = RecurringIncomeRules.ValidateSettlementAction(RecurringIncomeSettlementActions.PagarSaldo, isIndefinite: false);

        Assert.True(result.IsValid);
    }

    // ── Projection by frequency + overdue marking (D-07) ───────────────────────────────────────────

    [Fact]
    public void BuildProjection_Monthly_StepsOneMonthPerInstallment()
    {
        var plan = RecurringIncomeRules.NormalizePlan(50m, 3, 150m, false).Plan!;
        var start = new DateOnly(2026, 1, 15);

        var projection = RecurringIncomeRules.BuildProjection(
            plan, RecurringIncomeFrequencies.Mensual, start, new HashSet<int>(), Today);

        Assert.Equal(3, projection.Count);
        Assert.Equal(new DateOnly(2026, 1, 15), projection[0].TheoreticalDueDate);
        Assert.Equal(new DateOnly(2026, 2, 15), projection[1].TheoreticalDueDate);
        Assert.Equal(new DateOnly(2026, 3, 15), projection[2].TheoreticalDueDate);
    }

    [Fact]
    public void BuildProjection_Fortnightly_StepsFifteenDays()
    {
        var plan = RecurringIncomeRules.NormalizePlan(50m, 3, 150m, false).Plan!;
        var start = new DateOnly(2026, 1, 1);

        var projection = RecurringIncomeRules.BuildProjection(
            plan, RecurringIncomeFrequencies.Quincenal, start, new HashSet<int>(), Today);

        Assert.Equal(new DateOnly(2026, 1, 1), projection[0].TheoreticalDueDate);
        Assert.Equal(new DateOnly(2026, 1, 16), projection[1].TheoreticalDueDate);
        Assert.Equal(new DateOnly(2026, 1, 31), projection[2].TheoreticalDueDate);
    }

    [Fact]
    public void BuildProjection_Weekly_StepsSevenDays()
    {
        var plan = RecurringIncomeRules.NormalizePlan(50m, 2, 100m, false).Plan!;
        var start = new DateOnly(2026, 1, 1);

        var projection = RecurringIncomeRules.BuildProjection(
            plan, RecurringIncomeFrequencies.Semanal, start, new HashSet<int>(), Today);

        Assert.Equal(new DateOnly(2026, 1, 1), projection[0].TheoreticalDueDate);
        Assert.Equal(new DateOnly(2026, 1, 8), projection[1].TheoreticalDueDate);
    }

    [Fact]
    public void BuildProjection_Single_ProducesOneInstallment()
    {
        var plan = RecurringIncomeRules.NormalizePlan(50m, 1, 50m, false).Plan!;
        var start = new DateOnly(2026, 1, 1);

        var projection = RecurringIncomeRules.BuildProjection(
            plan, RecurringIncomeFrequencies.Unica, start, new HashSet<int>(), Today);

        Assert.Single(projection);
        Assert.Equal(start, projection[0].TheoreticalDueDate);
    }

    // ── TheoreticalDueDateFor (PR-4) — the single-installment due-date helper used by the applier ──────

    [Theory]
    [InlineData("MENSUAL", 1, 2026, 1, 15)]
    [InlineData("MENSUAL", 3, 2026, 3, 15)]
    [InlineData("QUINCENAL", 2, 2026, 1, 30)]
    [InlineData("SEMANAL", 3, 2026, 1, 29)]
    [InlineData("UNICA", 5, 2026, 1, 15)]
    public void TheoreticalDueDateFor_MatchesTheCadence(string frequency, int number, int year, int month, int day)
    {
        var start = new DateOnly(2026, 1, 15);

        var due = RecurringIncomeRules.TheoreticalDueDateFor(frequency, start, number);

        Assert.Equal(new DateOnly(year, month, day), due);
    }

    [Fact]
    public void TheoreticalDueDateFor_MonthlyBeyondProjectionHorizon_KeepsStepping()
    {
        // An indefinite plan's next installment number can exceed the 12-installment projection horizon; the
        // helper still steps monthly (installment 15 = start + 14 months).
        var start = new DateOnly(2026, 1, 10);

        var due = RecurringIncomeRules.TheoreticalDueDateFor(RecurringIncomeFrequencies.Mensual, start, 15);

        Assert.Equal(new DateOnly(2027, 3, 10), due);
    }

    [Fact]
    public void BuildProjection_MarksPastUnappliedInstallmentsOverdue()
    {
        var plan = RecurringIncomeRules.NormalizePlan(50m, 3, 150m, false).Plan!;
        var start = new DateOnly(2026, 1, 1);

        var projection = RecurringIncomeRules.BuildProjection(
            plan, RecurringIncomeFrequencies.Mensual, start, new HashSet<int> { 1 }, Today);

        Assert.False(projection[0].IsOverdue); // applied, never overdue
        Assert.True(projection[0].IsApplied);
        Assert.True(projection[1].IsOverdue);  // 2026-02-01 < today, unapplied
        Assert.True(projection[2].IsOverdue);  // 2026-03-01 < today, unapplied
    }

    // ── State machine (RN-01/RN-02) ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(RecurringIncomeStatuses.EnRevision, RecurringIncomeStatuses.Vigente, true)]
    [InlineData(RecurringIncomeStatuses.EnRevision, RecurringIncomeStatuses.Rechazado, true)]
    [InlineData(RecurringIncomeStatuses.EnRevision, RecurringIncomeStatuses.Anulado, true)]
    [InlineData(RecurringIncomeStatuses.Vigente, RecurringIncomeStatuses.Suspendido, true)]
    [InlineData(RecurringIncomeStatuses.Vigente, RecurringIncomeStatuses.Anulado, true)]
    [InlineData(RecurringIncomeStatuses.Vigente, RecurringIncomeStatuses.Finalizado, true)]
    [InlineData(RecurringIncomeStatuses.Suspendido, RecurringIncomeStatuses.Vigente, true)]
    [InlineData(RecurringIncomeStatuses.Finalizado, RecurringIncomeStatuses.Vigente, true)]
    [InlineData(RecurringIncomeStatuses.EnRevision, RecurringIncomeStatuses.Suspendido, false)]
    [InlineData(RecurringIncomeStatuses.EnRevision, RecurringIncomeStatuses.Finalizado, false)]
    [InlineData(RecurringIncomeStatuses.Rechazado, RecurringIncomeStatuses.Vigente, false)]
    [InlineData(RecurringIncomeStatuses.Anulado, RecurringIncomeStatuses.Vigente, false)]
    [InlineData(RecurringIncomeStatuses.Vigente, RecurringIncomeStatuses.Vigente, false)]
    public void CanTransition_EnforcesStateMachine(string from, string to, bool expected)
    {
        Assert.Equal(expected, RecurringIncomeRules.CanTransition(from, to));
    }

    // ── CanApplyInstallment sequence + bounds ──────────────────────────────────────────────────────

    [Fact]
    public void CanApplyInstallment_NextInSequenceOnVigente_Ok()
    {
        var plan = RecurringIncomeRules.NormalizePlan(50m, 6, 300m, false).Plan!;
        var result = RecurringIncomeRules.CanApplyInstallment(RecurringIncomeStatuses.Vigente, 3, plan, new HashSet<int> { 1, 2 });

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CanApplyInstallment_NotVigente_Fails()
    {
        var plan = RecurringIncomeRules.NormalizePlan(50m, 6, 300m, false).Plan!;
        var result = RecurringIncomeRules.CanApplyInstallment(RecurringIncomeStatuses.Suspendido, 1, plan, new HashSet<int>());

        Assert.False(result.IsValid);
        Assert.Equal(RecurringIncomeRules.InstallmentNotApplicableCode, result.ErrorCode);
    }

    [Fact]
    public void CanApplyInstallment_OutOfSequence_Fails()
    {
        var plan = RecurringIncomeRules.NormalizePlan(50m, 6, 300m, false).Plan!;
        var result = RecurringIncomeRules.CanApplyInstallment(RecurringIncomeStatuses.Vigente, 4, plan, new HashSet<int> { 1, 2 });

        Assert.False(result.IsValid);
        Assert.Equal(RecurringIncomeRules.InstallmentSequenceInvalidCode, result.ErrorCode);
    }

    [Fact]
    public void CanApplyInstallment_ExceedsFinitePlan_Fails()
    {
        var plan = RecurringIncomeRules.NormalizePlan(50m, 2, 100m, false).Plan!;
        var result = RecurringIncomeRules.CanApplyInstallment(RecurringIncomeStatuses.Vigente, 3, plan, new HashSet<int> { 1, 2 });

        Assert.False(result.IsValid);
        Assert.Equal(RecurringIncomeRules.InstallmentExceedsPlanCode, result.ErrorCode);
    }

    [Fact]
    public void NextInstallmentNumber_FillsAnnulledGapFirst()
    {
        Assert.Equal(1, RecurringIncomeRules.NextInstallmentNumber(new HashSet<int>()));
        Assert.Equal(6, RecurringIncomeRules.NextInstallmentNumber(new HashSet<int> { 1, 2, 3, 4, 5 }));
        Assert.Equal(3, RecurringIncomeRules.NextInstallmentNumber(new HashSet<int> { 1, 2, 4, 5 }));
    }

    // ── Domain guards: custodied mutators ──────────────────────────────────────────────────────────

    [Fact]
    public void Create_FiniteMissingLimits_Throws()
    {
        Assert.Throws<ArgumentException>(() => BuildFinite(installmentCount: null, totalAmount: null));
    }

    [Fact]
    public void Create_IndefiniteWithLimits_Throws()
    {
        Assert.Throws<ArgumentException>(() => Build(isIndefinite: true, value: 50m, count: 6, total: null));
    }

    [Fact]
    public void Update_OnlyAllowedWhileEnRevision()
    {
        var income = BuildFinite();
        income.Approve(Guid.NewGuid(), DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            income.Update(
                Today, null, "AYUDA_ALIMENTACION", "OTRO_INGRESO", "Ayuda", null,
                Guid.NewGuid(), Guid.NewGuid(), "CC-1",
                Today, "USD", "MENSUAL", "QUINCENAL", false, 50m, 6, 300m, "PAGAR_SALDO"));
    }

    [Fact]
    public void Approve_MovesToVigente()
    {
        var income = BuildFinite();
        var decidedBy = Guid.NewGuid();

        income.Approve(decidedBy, DateTime.UtcNow);

        Assert.Equal(RecurringIncomeStatuses.Vigente, income.StatusCode);
        Assert.Equal(decidedBy, income.DecidedByUserId);
    }

    [Fact]
    public void Approve_FromNonEnRevision_Throws()
    {
        var income = BuildFinite();
        income.Approve(Guid.NewGuid(), DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => income.Approve(Guid.NewGuid(), DateTime.UtcNow));
    }

    [Fact]
    public void Reject_RequiresNote_AndIsTerminal()
    {
        var income = BuildFinite();

        Assert.Throws<ArgumentException>(() => income.Reject(Guid.NewGuid(), DateTime.UtcNow, "   "));

        income.Reject(Guid.NewGuid(), DateTime.UtcNow, "Sin respaldo");
        Assert.Equal(RecurringIncomeStatuses.Rechazado, income.StatusCode);
        Assert.False(income.IsActive);
    }

    [Fact]
    public void Annul_FromEnRevisionOrVigente_RequiresReason()
    {
        var fromReview = BuildFinite();
        Assert.Throws<ArgumentException>(() => fromReview.Annul("  ", Guid.NewGuid(), DateTime.UtcNow));
        fromReview.Annul("Duplicado", Guid.NewGuid(), DateTime.UtcNow);
        Assert.Equal(RecurringIncomeStatuses.Anulado, fromReview.StatusCode);

        var fromVigente = BuildFinite();
        fromVigente.Approve(Guid.NewGuid(), DateTime.UtcNow);
        fromVigente.Annul("Revocado", Guid.NewGuid(), DateTime.UtcNow);
        Assert.Equal(RecurringIncomeStatuses.Anulado, fromVigente.StatusCode);
    }

    [Fact]
    public void Annul_FromTerminal_Throws()
    {
        var income = BuildFinite();
        income.Reject(Guid.NewGuid(), DateTime.UtcNow, "Rechazado");

        Assert.Throws<InvalidOperationException>(() => income.Annul("x", Guid.NewGuid(), DateTime.UtcNow));
    }

    [Fact]
    public void SuspendAndResume_ToggleBetweenVigenteAndSuspendido()
    {
        var income = BuildFinite();
        income.Approve(Guid.NewGuid(), DateTime.UtcNow);

        income.Suspend("En pausa", DateTime.UtcNow);
        Assert.Equal(RecurringIncomeStatuses.Suspendido, income.StatusCode);

        income.Resume(DateTime.UtcNow);
        Assert.Equal(RecurringIncomeStatuses.Vigente, income.StatusCode);
    }

    [Fact]
    public void Suspend_FromNonVigente_Throws()
    {
        var income = BuildFinite();

        Assert.Throws<InvalidOperationException>(() => income.Suspend("x", DateTime.UtcNow));
    }

    [Fact]
    public void CloseManually_OnlyIndefiniteVigente_RequiresReason()
    {
        var finite = BuildFinite();
        finite.Approve(Guid.NewGuid(), DateTime.UtcNow);
        Assert.Throws<InvalidOperationException>(() => finite.CloseManually("x", Guid.NewGuid(), DateTime.UtcNow));

        var indefinite = BuildIndefinite();
        indefinite.Approve(Guid.NewGuid(), DateTime.UtcNow);
        Assert.Throws<ArgumentException>(() => indefinite.CloseManually("  ", Guid.NewGuid(), DateTime.UtcNow));

        indefinite.CloseManually("Fin de acuerdo", Guid.NewGuid(), DateTime.UtcNow);
        Assert.Equal(RecurringIncomeStatuses.Finalizado, indefinite.StatusCode);
    }

    [Fact]
    public void ApplyInstallment_OnlyOnVigente()
    {
        var income = BuildFinite();

        Assert.Throws<InvalidOperationException>(() => ApplyNext(income, 1));
    }

    [Fact]
    public void ApplyInstallment_EnforcesStrictSequence()
    {
        var income = BuildFinite();
        income.Approve(Guid.NewGuid(), DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => ApplyNext(income, 2));
    }

    [Fact]
    public void ApplyInstallment_CannotExceedFinitePlan()
    {
        var income = Build(isIndefinite: false, value: 50m, count: 2, total: 100m);
        income.Approve(Guid.NewGuid(), DateTime.UtcNow);
        ApplyNext(income, 1);
        ApplyNext(income, 2);

        // A fixed amount is passed so the domain guard (not the rules amount helper) is what rejects #3.
        Assert.Throws<InvalidOperationException>(() => income.ApplyInstallment(
            3, Today, Today, 50m, income.CurrencyCode, income.PayrollTypeCode,
            null, null, RecurringIncomeInstallmentOrigins.Manual, Guid.NewGuid(), null));
    }

    // ── A.3-6: annul the 6/6 installment of a FINALIZADO → reopen to VIGENTE; re-apply → FINALIZADO ──

    [Fact]
    public void FinalizeByPlanCompletion_ThenAnnulLast_ReopensAndReapplyRefinalizes()
    {
        var income = BuildFinite(); // 6 × $50
        income.Approve(Guid.NewGuid(), DateTime.UtcNow);

        for (var number = 1; number <= 6; number++)
        {
            ApplyNext(income, number);
        }

        income.FinalizeByPlanCompletion(DateTime.UtcNow);
        Assert.Equal(RecurringIncomeStatuses.Finalizado, income.StatusCode);

        var sixth = income.Installments.Single(item =>
            item.InstallmentNumber == 6 && item.StatusCode == RecurringIncomeInstallmentStatuses.Aplicada);

        income.AnnulInstallment(sixth.PublicId, "Corrección", Guid.NewGuid(), DateTime.UtcNow);
        Assert.Equal(RecurringIncomeStatuses.Vigente, income.StatusCode);
        Assert.Equal(6, income.NextInstallmentNumber());

        ApplyNext(income, 6);
        income.FinalizeByPlanCompletion(DateTime.UtcNow);
        Assert.Equal(RecurringIncomeStatuses.Finalizado, income.StatusCode);
    }

    [Fact]
    public void FinalizeByPlanCompletion_IncompletePlan_Throws()
    {
        var income = BuildFinite();
        income.Approve(Guid.NewGuid(), DateTime.UtcNow);
        ApplyNext(income, 1);

        Assert.Throws<InvalidOperationException>(() => income.FinalizeByPlanCompletion(DateTime.UtcNow));
    }

    [Fact]
    public void FinalizeBySettlement_ThenReopenBySameSettlement_IsSymmetric()
    {
        var settlement = Guid.NewGuid();
        var income = BuildIndefinite();
        income.Approve(Guid.NewGuid(), DateTime.UtcNow);

        income.FinalizeBySettlement(settlement, DateTime.UtcNow);
        Assert.Equal(RecurringIncomeStatuses.Finalizado, income.StatusCode);

        // A different settlement's reopen is a no-op.
        income.ReopenFromSettlement(Guid.NewGuid(), DateTime.UtcNow);
        Assert.Equal(RecurringIncomeStatuses.Finalizado, income.StatusCode);

        income.ReopenFromSettlement(settlement, DateTime.UtcNow);
        Assert.Equal(RecurringIncomeStatuses.Vigente, income.StatusCode);
    }

    [Fact]
    public void FinalizeBySettlement_OnNonVigente_IsIdempotentNoOp()
    {
        var income = BuildFinite(); // still EN_REVISION
        income.FinalizeBySettlement(Guid.NewGuid(), DateTime.UtcNow);

        Assert.Equal(RecurringIncomeStatuses.EnRevision, income.StatusCode);
    }

    [Fact]
    public void AnnulInstallment_RequiresReason()
    {
        var income = BuildFinite();
        income.Approve(Guid.NewGuid(), DateTime.UtcNow);
        var first = ApplyNext(income, 1);

        Assert.Throws<ArgumentException>(() => income.AnnulInstallment(first.PublicId, "  ", Guid.NewGuid(), DateTime.UtcNow));
    }

    // ── Builders ───────────────────────────────────────────────────────────────────────────────────

    private static PersonnelFileRecurringIncomeInstallment ApplyNext(PersonnelFileRecurringIncome income, int number)
    {
        var plan = new RecurringIncomePlan(income.InstallmentValue, income.InstallmentCount, income.TotalAmount, income.IsIndefinite);
        var amount = RecurringIncomeRules.InstallmentAmountFor(number, plan);
        return income.ApplyInstallment(
            number,
            Today,
            Today,
            amount,
            income.CurrencyCode,
            income.PayrollTypeCode,
            payrollPeriodId: null,
            payrollPeriodLabel: null,
            RecurringIncomeInstallmentOrigins.Manual,
            Guid.NewGuid(),
            notes: null);
    }

    private static PersonnelFileRecurringIncome BuildFinite(int? installmentCount = 6, decimal? totalAmount = 300m) =>
        Build(isIndefinite: false, value: 50m, count: installmentCount, total: totalAmount);

    private static PersonnelFileRecurringIncome BuildIndefinite() =>
        Build(isIndefinite: true, value: 75m, count: null, total: null);

    private static PersonnelFileRecurringIncome Build(bool isIndefinite, decimal value, int? count, decimal? total) =>
        PersonnelFileRecurringIncome.Create(
            registrationDate: Today,
            reference: "REF-1",
            recurringIncomeTypeCode: "AYUDA_ALIMENTACION",
            conceptTypeCode: "OTRO_INGRESO",
            conceptNameSnapshot: "Ayuda para alimentación",
            observations: null,
            assignedPositionPublicId: Guid.NewGuid(),
            costCenterPublicId: Guid.NewGuid(),
            costCenterNameSnapshot: "Centro de costo 1",
            installmentStartDate: Today,
            currencyCode: "USD",
            payrollTypeCode: "MENSUAL",
            installmentFrequencyCode: "QUINCENAL",
            isIndefinite: isIndefinite,
            installmentValue: value,
            installmentCount: count,
            totalAmount: total,
            settlementActionCode: isIndefinite ? RecurringIncomeSettlementActions.Cancelar : RecurringIncomeSettlementActions.PagarSaldo,
            registeredByUserId: Guid.NewGuid());
}

using System.Reflection;
using System.Xml.Linq;
using CLARIHR.Application.Features.PersonnelFiles.Compensation;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// The one-time-income value + lifecycle critical golden suite (PR-2, the REQ-006 wave gate) — the RATIFIED
/// Anexo A.3 rule/domain cases encoded as blocking assertions (the e2e cases are PR-3…PR-6). The rules module is
/// 100% pure so these fully pin the amount derivation (quantity × unit value × multiplier, or a percentage over
/// a base), the last-decimal rounding, the fixed/computed coherence + amount mismatch, the concept eligibility,
/// the state machine, the application rule and the overdue derivation; the domain guards pin the custodied
/// mutators; a localization-parity assertion pins the bilingual error catalog. Reference country: El Salvador.
/// </summary>
public sealed class OneTimeIncomeRulesTests
{
    private static readonly DateOnly Today = new(2026, 7, 9);

    // ── A.3-1: computed amount = quantity × unit value × multiplier (10 × $2.50 × 1.5 = $37.50) ────────

    [Fact]
    public void ComputeAmount_QuantityTimesValue_MultipliesComponents()
    {
        var result = OneTimeIncomeRules.ComputeAmount(
            OneTimeIncomeCalculationMethods.QuantityTimesValue,
            quantity: 10m, unitValue: 2.50m, multiplier: 1.5m, percentage: null, baseAmount: null);

        Assert.True(result.IsValid);
        Assert.Equal(37.50m, result.Amount);
    }

    [Fact]
    public void ComputeAmount_QuantityTimesValue_MultiplierDefaultsToOne()
    {
        var result = OneTimeIncomeRules.ComputeAmount(
            OneTimeIncomeCalculationMethods.QuantityTimesValue,
            quantity: 10m, unitValue: 2.50m, multiplier: null, percentage: null, baseAmount: null);

        Assert.True(result.IsValid);
        Assert.Equal(25.00m, result.Amount);
    }

    // ── A.3-2: computed amount = percentage % × base (3% × $10,000 = $300) ─────────────────────────────

    [Fact]
    public void ComputeAmount_PercentageOnBase_AppliesPercentage()
    {
        var result = OneTimeIncomeRules.ComputeAmount(
            OneTimeIncomeCalculationMethods.PercentageOnBase,
            quantity: null, unitValue: null, multiplier: null, percentage: 3m, baseAmount: 10000m);

        Assert.True(result.IsValid);
        Assert.Equal(300m, result.Amount);
    }

    [Fact]
    public void ComputeAmount_RoundsHalfUpAwayFromZero()
    {
        // 1 × 2.345 × 1 = 2.345 → 2.35 (half-up away-from-zero, the single rounding point).
        var result = OneTimeIncomeRules.ComputeAmount(
            OneTimeIncomeCalculationMethods.QuantityTimesValue,
            quantity: 1m, unitValue: 2.345m, multiplier: 1m, percentage: null, baseAmount: null);

        Assert.True(result.IsValid);
        Assert.Equal(2.35m, result.Amount);
    }

    // ── ComputeAmount: method ↔ component pairing (missing / surplus) + positivity ─────────────────────

    [Fact]
    public void ComputeAmount_QuantityTimesValue_WithPercentageComponents_Fails()
    {
        var result = OneTimeIncomeRules.ComputeAmount(
            OneTimeIncomeCalculationMethods.QuantityTimesValue,
            quantity: 10m, unitValue: 2.50m, multiplier: null, percentage: 5m, baseAmount: null);

        Assert.False(result.IsValid);
        Assert.Equal(OneTimeIncomeRules.ValueComponentsInvalidCode, result.ErrorCode);
    }

    [Fact]
    public void ComputeAmount_QuantityTimesValue_MissingUnitValue_Fails()
    {
        var result = OneTimeIncomeRules.ComputeAmount(
            OneTimeIncomeCalculationMethods.QuantityTimesValue,
            quantity: 10m, unitValue: null, multiplier: null, percentage: null, baseAmount: null);

        Assert.False(result.IsValid);
        Assert.Equal(OneTimeIncomeRules.ValueComponentsInvalidCode, result.ErrorCode);
    }

    [Fact]
    public void ComputeAmount_PercentageOnBase_WithQuantityComponents_Fails()
    {
        var result = OneTimeIncomeRules.ComputeAmount(
            OneTimeIncomeCalculationMethods.PercentageOnBase,
            quantity: 10m, unitValue: null, multiplier: null, percentage: 3m, baseAmount: 10000m);

        Assert.False(result.IsValid);
        Assert.Equal(OneTimeIncomeRules.ValueComponentsInvalidCode, result.ErrorCode);
    }

    [Fact]
    public void ComputeAmount_PercentageOnBase_MissingBase_Fails()
    {
        var result = OneTimeIncomeRules.ComputeAmount(
            OneTimeIncomeCalculationMethods.PercentageOnBase,
            quantity: null, unitValue: null, multiplier: null, percentage: 3m, baseAmount: null);

        Assert.False(result.IsValid);
        Assert.Equal(OneTimeIncomeRules.ValueComponentsInvalidCode, result.ErrorCode);
    }

    [Fact]
    public void ComputeAmount_NonPositiveComponent_Fails()
    {
        var result = OneTimeIncomeRules.ComputeAmount(
            OneTimeIncomeCalculationMethods.QuantityTimesValue,
            quantity: 0m, unitValue: 2.50m, multiplier: 1m, percentage: null, baseAmount: null);

        Assert.False(result.IsValid);
        Assert.Equal(OneTimeIncomeRules.ValueComponentsInvalidCode, result.ErrorCode);
    }

    [Fact]
    public void ComputeAmount_UnknownMethod_Fails()
    {
        var result = OneTimeIncomeRules.ComputeAmount(
            "OTRO_METODO",
            quantity: 10m, unitValue: 2.50m, multiplier: 1m, percentage: null, baseAmount: null);

        Assert.False(result.IsValid);
        Assert.Equal(OneTimeIncomeRules.ValueMethodInvalidCode, result.ErrorCode);
    }

    // ── ValidateValue: fixed vs computed coherence + amount mismatch with the expected breakdown ───────

    [Fact]
    public void ValidateValue_FixedWithoutComponents_Succeeds()
    {
        var result = OneTimeIncomeRules.ValidateValue(
            isFixedValue: true, calculationMethod: null,
            quantity: null, unitValue: null, multiplier: null, percentage: null, baseAmount: null,
            amount: 150m);

        Assert.True(result.IsValid);
        Assert.Equal(150m, result.ExpectedAmount);
    }

    [Fact]
    public void ValidateValue_FixedWithComponents_Fails()
    {
        var result = OneTimeIncomeRules.ValidateValue(
            isFixedValue: true, calculationMethod: OneTimeIncomeCalculationMethods.QuantityTimesValue,
            quantity: 10m, unitValue: 2.50m, multiplier: 1m, percentage: null, baseAmount: null,
            amount: 25m);

        Assert.False(result.IsValid);
        Assert.Equal(OneTimeIncomeRules.ValueFixedWithComponentsCode, result.ErrorCode);
    }

    [Fact]
    public void ValidateValue_FixedNonPositiveAmount_Fails()
    {
        var result = OneTimeIncomeRules.ValidateValue(
            isFixedValue: true, calculationMethod: null,
            quantity: null, unitValue: null, multiplier: null, percentage: null, baseAmount: null,
            amount: 0m);

        Assert.False(result.IsValid);
        Assert.Equal(OneTimeIncomeRules.ValueAmountInvalidCode, result.ErrorCode);
    }

    [Fact]
    public void ValidateValue_ComputedWithoutMethod_Fails()
    {
        var result = OneTimeIncomeRules.ValidateValue(
            isFixedValue: false, calculationMethod: null,
            quantity: 10m, unitValue: 2.50m, multiplier: 1m, percentage: null, baseAmount: null,
            amount: 25m);

        Assert.False(result.IsValid);
        Assert.Equal(OneTimeIncomeRules.ValueMethodRequiredCode, result.ErrorCode);
    }

    [Fact]
    public void ValidateValue_ComputedCoherentAmount_Succeeds()
    {
        var result = OneTimeIncomeRules.ValidateValue(
            isFixedValue: false, calculationMethod: OneTimeIncomeCalculationMethods.QuantityTimesValue,
            quantity: 10m, unitValue: 2.50m, multiplier: 1.5m, percentage: null, baseAmount: null,
            amount: 37.50m);

        Assert.True(result.IsValid);
        Assert.Equal(37.50m, result.ExpectedAmount);
    }

    [Fact]
    public void ValidateValue_ComputedAmountOmitted_DerivesAmount()
    {
        // The request may omit the amount for a computed value; the server derives it (plan §0.11, HU-002).
        var result = OneTimeIncomeRules.ValidateValue(
            isFixedValue: false, calculationMethod: OneTimeIncomeCalculationMethods.QuantityTimesValue,
            quantity: 10m, unitValue: 2.50m, multiplier: 1.5m, percentage: null, baseAmount: null,
            amount: null);

        Assert.True(result.IsValid);
        Assert.Equal(37.50m, result.ExpectedAmount);
    }

    [Fact]
    public void ValidateValue_ComputedAmountMismatch_FailsWithExpectedBreakdown()
    {
        var result = OneTimeIncomeRules.ValidateValue(
            isFixedValue: false, calculationMethod: OneTimeIncomeCalculationMethods.QuantityTimesValue,
            quantity: 10m, unitValue: 2.50m, multiplier: 1.5m, percentage: null, baseAmount: null,
            amount: 40m);

        Assert.False(result.IsValid);
        Assert.Equal(OneTimeIncomeRules.AmountMismatchCode, result.ErrorCode);
        Assert.Equal(37.50m, result.ExpectedAmount);
    }

    // ── Concept eligibility (D-03) ─────────────────────────────────────────────────────────────────

    [Fact]
    public void ValidateConcept_ActiveIncome_NotBaseSalary_Ok()
    {
        var result = OneTimeIncomeRules.ValidateConcept(CompensationNature.Ingreso, isBaseSalary: false);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorCode);
    }

    [Fact]
    public void ValidateConcept_Deduction_Fails()
    {
        var result = OneTimeIncomeRules.ValidateConcept(CompensationNature.Egreso, isBaseSalary: false);

        Assert.False(result.IsValid);
        Assert.Equal(OneTimeIncomeRules.ConceptNotIncomeCode, result.ErrorCode);
    }

    [Fact]
    public void ValidateConcept_BaseSalary_Fails()
    {
        var result = OneTimeIncomeRules.ValidateConcept(CompensationNature.Ingreso, isBaseSalary: true);

        Assert.False(result.IsValid);
        Assert.Equal(OneTimeIncomeRules.ConceptIsBaseSalaryCode, result.ErrorCode);
    }

    // ── State machine (RN-01/RN-02) ────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(OneTimeIncomeStatuses.EnRevision, OneTimeIncomeStatuses.Autorizado, true)]
    [InlineData(OneTimeIncomeStatuses.EnRevision, OneTimeIncomeStatuses.Rechazado, true)]
    [InlineData(OneTimeIncomeStatuses.EnRevision, OneTimeIncomeStatuses.Anulado, true)]
    [InlineData(OneTimeIncomeStatuses.Autorizado, OneTimeIncomeStatuses.Aplicado, true)]
    [InlineData(OneTimeIncomeStatuses.Autorizado, OneTimeIncomeStatuses.Anulado, true)]
    [InlineData(OneTimeIncomeStatuses.Aplicado, OneTimeIncomeStatuses.Autorizado, true)]
    [InlineData(OneTimeIncomeStatuses.EnRevision, OneTimeIncomeStatuses.Aplicado, false)]
    [InlineData(OneTimeIncomeStatuses.Autorizado, OneTimeIncomeStatuses.Rechazado, false)]
    [InlineData(OneTimeIncomeStatuses.Aplicado, OneTimeIncomeStatuses.Anulado, false)]
    [InlineData(OneTimeIncomeStatuses.Rechazado, OneTimeIncomeStatuses.Autorizado, false)]
    [InlineData(OneTimeIncomeStatuses.Anulado, OneTimeIncomeStatuses.Autorizado, false)]
    [InlineData(OneTimeIncomeStatuses.Autorizado, OneTimeIncomeStatuses.Autorizado, false)]
    public void CanTransition_EnforcesStateMachine(string from, string to, bool expected)
    {
        Assert.Equal(expected, OneTimeIncomeRules.CanTransition(from, to));
    }

    // ── CanApply / CanRetarget / CanRevertApplication ──────────────────────────────────────────────

    [Fact]
    public void CanApply_AutorizadoWithoutActiveApplication_Ok()
    {
        var result = OneTimeIncomeRules.CanApply(OneTimeIncomeStatuses.Autorizado, hasActiveApplication: false);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CanApply_WithActiveApplication_Fails()
    {
        var result = OneTimeIncomeRules.CanApply(OneTimeIncomeStatuses.Autorizado, hasActiveApplication: true);

        Assert.False(result.IsValid);
        Assert.Equal(OneTimeIncomeRules.AlreadyAppliedCode, result.ErrorCode);
    }

    [Fact]
    public void CanApply_NotAutorizado_Fails()
    {
        var result = OneTimeIncomeRules.CanApply(OneTimeIncomeStatuses.EnRevision, hasActiveApplication: false);

        Assert.False(result.IsValid);
        Assert.Equal(OneTimeIncomeRules.NotApplicableCode, result.ErrorCode);
    }

    [Fact]
    public void CanRetarget_OnlyAutorizado()
    {
        Assert.True(OneTimeIncomeRules.CanRetarget(OneTimeIncomeStatuses.Autorizado).IsValid);

        var blocked = OneTimeIncomeRules.CanRetarget(OneTimeIncomeStatuses.EnRevision);
        Assert.False(blocked.IsValid);
        Assert.Equal(OneTimeIncomeRules.NotRetargetableCode, blocked.ErrorCode);
    }

    [Fact]
    public void CanRevertApplication_OnlyAplicado()
    {
        Assert.True(OneTimeIncomeRules.CanRevertApplication(OneTimeIncomeStatuses.Aplicado).IsValid);

        var blocked = OneTimeIncomeRules.CanRevertApplication(OneTimeIncomeStatuses.Autorizado);
        Assert.False(blocked.IsValid);
        Assert.Equal(OneTimeIncomeRules.ApplicationNotRevertibleCode, blocked.ErrorCode);
    }

    // ── IsOverdue (with and without an end date) ───────────────────────────────────────────────────

    [Fact]
    public void IsOverdue_NullEndDate_IsNeverOverdue()
    {
        Assert.False(OneTimeIncomeRules.IsOverdue(null, Today));
    }

    [Fact]
    public void IsOverdue_PastEndDate_IsOverdue()
    {
        Assert.True(OneTimeIncomeRules.IsOverdue(Today.AddDays(-1), Today));
    }

    [Fact]
    public void IsOverdue_TodayOrFutureEndDate_IsNotOverdue()
    {
        Assert.False(OneTimeIncomeRules.IsOverdue(Today, Today));
        Assert.False(OneTimeIncomeRules.IsOverdue(Today.AddDays(1), Today));
    }

    // ── Domain guards: custodied mutators ──────────────────────────────────────────────────────────

    [Fact]
    public void Create_FixedWithComponents_Throws()
    {
        Assert.Throws<ArgumentException>(() => Build(
            isFixedValue: true,
            calculationMethod: OneTimeIncomeCalculationMethods.QuantityTimesValue,
            quantity: 10m, unitValue: 2.50m, multiplier: 1m, percentage: null, baseAmount: null,
            amount: 25m));
    }

    [Fact]
    public void Create_ComputedMissingComponent_Throws()
    {
        Assert.Throws<ArgumentException>(() => Build(
            isFixedValue: false,
            calculationMethod: OneTimeIncomeCalculationMethods.QuantityTimesValue,
            quantity: 10m, unitValue: null, multiplier: null, percentage: null, baseAmount: null,
            amount: 25m));
    }

    [Fact]
    public void Create_NonPositiveAmount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BuildFixed(amount: 0m));
    }

    [Fact]
    public void Create_ComputedQuantityValue_DefaultsMultiplierToOne()
    {
        var income = Build(
            isFixedValue: false,
            calculationMethod: OneTimeIncomeCalculationMethods.QuantityTimesValue,
            quantity: 10m, unitValue: 2.50m, multiplier: null, percentage: null, baseAmount: null,
            amount: 25m);

        Assert.Equal(PersonnelFileOneTimeIncome.DefaultMultiplier, income.Multiplier);
        Assert.Equal(OneTimeIncomeStatuses.EnRevision, income.StatusCode);
    }

    [Fact]
    public void Update_OnlyAllowedWhileEnRevision()
    {
        var income = BuildFixed();
        income.Approve(Guid.NewGuid(), DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => income.Update(
            Today, null, "BONO_EXTRA", "Bono extraordinario", null,
            isFixedValue: true, calculationMethod: null,
            quantity: null, unitValue: null, multiplier: null, percentage: null, baseAmount: null,
            amount: 200m, currencyCode: "USD",
            assignedPositionPublicId: Guid.NewGuid(), costCenterPublicId: Guid.NewGuid(), costCenterNameSnapshot: "CC-1",
            requesterFilePublicId: Guid.NewGuid(), requesterNameSnapshot: "Solicitante",
            payrollTypeCode: "MENSUAL",
            payrollPeriodId: null, payrollPeriodPublicId: null, payrollPeriodLabel: "Julio 2026", payrollPeriodEndDate: null));
    }

    [Fact]
    public void Approve_MovesToAutorizado()
    {
        var income = BuildFixed();
        var decidedBy = Guid.NewGuid();

        income.Approve(decidedBy, DateTime.UtcNow);

        Assert.Equal(OneTimeIncomeStatuses.Autorizado, income.StatusCode);
        Assert.Equal(decidedBy, income.DecidedByUserId);
    }

    [Fact]
    public void Approve_FromNonEnRevision_Throws()
    {
        var income = BuildFixed();
        income.Approve(Guid.NewGuid(), DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => income.Approve(Guid.NewGuid(), DateTime.UtcNow));
    }

    [Fact]
    public void Reject_RequiresNote_AndIsTerminal()
    {
        var income = BuildFixed();

        Assert.Throws<ArgumentException>(() => income.Reject(Guid.NewGuid(), DateTime.UtcNow, "   "));

        income.Reject(Guid.NewGuid(), DateTime.UtcNow, "Sin respaldo");
        Assert.Equal(OneTimeIncomeStatuses.Rechazado, income.StatusCode);
        Assert.False(income.IsActive);
    }

    [Fact]
    public void Annul_FromEnRevisionOrAutorizado_RequiresReason()
    {
        var fromReview = BuildFixed();
        Assert.Throws<ArgumentException>(() => fromReview.Annul("  ", Guid.NewGuid(), DateTime.UtcNow));
        fromReview.Annul("Duplicado", Guid.NewGuid(), DateTime.UtcNow);
        Assert.Equal(OneTimeIncomeStatuses.Anulado, fromReview.StatusCode);
        Assert.False(fromReview.IsActive);

        var fromAutorizado = BuildFixed();
        fromAutorizado.Approve(Guid.NewGuid(), DateTime.UtcNow);
        fromAutorizado.Annul("Revocado", Guid.NewGuid(), DateTime.UtcNow);
        Assert.Equal(OneTimeIncomeStatuses.Anulado, fromAutorizado.StatusCode);
    }

    [Fact]
    public void Annul_FromTerminal_Throws()
    {
        var income = BuildFixed();
        income.Reject(Guid.NewGuid(), DateTime.UtcNow, "Rechazado");

        Assert.Throws<InvalidOperationException>(() => income.Annul("x", Guid.NewGuid(), DateTime.UtcNow));
    }

    [Fact]
    public void Annul_FromAplicado_Throws()
    {
        var income = BuildFixed();
        income.Approve(Guid.NewGuid(), DateTime.UtcNow);
        Apply(income);

        Assert.Throws<InvalidOperationException>(() => income.Annul("x", Guid.NewGuid(), DateTime.UtcNow));
    }

    [Fact]
    public void RetargetPeriod_OnlyWhileAutorizado()
    {
        var enRevision = BuildFixed();
        Assert.Throws<InvalidOperationException>(() => enRevision.RetargetPeriod(
            "QUINCENAL", null, null, "Quincena 2", null, DateTime.UtcNow));

        var income = BuildFixed();
        income.Approve(Guid.NewGuid(), DateTime.UtcNow);
        income.RetargetPeriod("QUINCENAL", null, null, "Quincena 2", new DateOnly(2026, 7, 31), DateTime.UtcNow);

        Assert.Equal("QUINCENAL", income.PayrollTypeCode);
        Assert.Equal("Quincena 2", income.PayrollPeriodLabel);
        Assert.Equal(new DateOnly(2026, 7, 31), income.PayrollPeriodEndDate);
    }

    [Fact]
    public void Apply_OnlyOnAutorizado_MovesToAplicado_AndCreatesApplication()
    {
        var income = BuildFixed();
        Assert.Throws<InvalidOperationException>(() => Apply(income)); // EN_REVISION

        income.Approve(Guid.NewGuid(), DateTime.UtcNow);
        var application = Apply(income);

        Assert.Equal(OneTimeIncomeStatuses.Aplicado, income.StatusCode);
        Assert.True(income.HasActiveApplication);
        Assert.Equal(OneTimeIncomeApplicationStatuses.Aplicada, application.StatusCode);
    }

    [Fact]
    public void Apply_WhenAlreadyApplied_Throws()
    {
        var income = BuildFixed();
        income.Approve(Guid.NewGuid(), DateTime.UtcNow);
        Apply(income);

        Assert.Throws<InvalidOperationException>(() => Apply(income));
    }

    [Fact]
    public void AnnulApplication_ReopensToAutorizado_AndAllowsReapply()
    {
        var income = BuildFixed();
        income.Approve(Guid.NewGuid(), DateTime.UtcNow);
        var application = Apply(income);

        Assert.Throws<ArgumentException>(() => income.AnnulApplication(application.PublicId, "  ", Guid.NewGuid(), DateTime.UtcNow));

        income.AnnulApplication(application.PublicId, "Corrección", Guid.NewGuid(), DateTime.UtcNow);
        Assert.Equal(OneTimeIncomeStatuses.Autorizado, income.StatusCode);
        Assert.False(income.HasActiveApplication);

        var reapplied = Apply(income);
        Assert.Equal(OneTimeIncomeStatuses.Aplicado, income.StatusCode);
        Assert.NotEqual(application.PublicId, reapplied.PublicId);
    }

    [Fact]
    public void MarkAppliedBySettlement_ThenReopenBySameSettlement_IsSymmetric()
    {
        var settlement = Guid.NewGuid();
        var income = BuildFixed();
        income.Approve(Guid.NewGuid(), DateTime.UtcNow);

        income.MarkAppliedBySettlement(settlement, DateTime.UtcNow);
        Assert.Equal(OneTimeIncomeStatuses.Aplicado, income.StatusCode);

        // A different settlement's reopen is a no-op.
        income.ReopenFromSettlement(Guid.NewGuid(), DateTime.UtcNow);
        Assert.Equal(OneTimeIncomeStatuses.Aplicado, income.StatusCode);

        income.ReopenFromSettlement(settlement, DateTime.UtcNow);
        Assert.Equal(OneTimeIncomeStatuses.Autorizado, income.StatusCode);
    }

    [Fact]
    public void MarkAppliedBySettlement_OnNonAutorizado_IsIdempotentNoOp()
    {
        var income = BuildFixed(); // still EN_REVISION
        income.MarkAppliedBySettlement(Guid.NewGuid(), DateTime.UtcNow);

        Assert.Equal(OneTimeIncomeStatuses.EnRevision, income.StatusCode);
    }

    // ── Localization parity: every new rule error code is bilingual (EN + ES) ──────────────────────

    [Fact]
    public void RuleErrorCodes_ArePresentInBothResourceCatalogs()
    {
        var codes = typeof(OneTimeIncomeRules)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(string)
                && field.IsLiteral
                && field.Name.EndsWith("Code", StringComparison.Ordinal))
            .Select(field => (string)field.GetValue(null)!)
            .ToArray();

        Assert.NotEmpty(codes);

        var repositoryRoot = ResolveRepositoryRoot();
        var englishKeys = LoadResourceKeys(Path.Combine(repositoryRoot, "src", "CLARIHR.Infrastructure", "Localization", "BackendMessages.resx"));
        var spanishKeys = LoadResourceKeys(Path.Combine(repositoryRoot, "src", "CLARIHR.Infrastructure", "Localization", "BackendMessages.es.resx"));

        var missingInEnglish = codes.Where(code => !englishKeys.Contains(code)).OrderBy(code => code, StringComparer.Ordinal).ToArray();
        var missingInSpanish = codes.Where(code => !spanishKeys.Contains(code)).OrderBy(code => code, StringComparer.Ordinal).ToArray();

        Assert.True(missingInEnglish.Length == 0, $"Missing in BackendMessages.resx: {string.Join(", ", missingInEnglish)}");
        Assert.True(missingInSpanish.Length == 0, $"Missing in BackendMessages.es.resx: {string.Join(", ", missingInSpanish)}");
    }

    // ── Builders ───────────────────────────────────────────────────────────────────────────────────

    private static PersonnelFileOneTimeIncomeApplication Apply(PersonnelFileOneTimeIncome income) =>
        income.Apply(
            appliedDate: Today,
            payrollTypeCode: income.PayrollTypeCode,
            payrollPeriodId: null,
            payrollPeriodPublicId: null,
            payrollPeriodLabel: income.PayrollPeriodLabel,
            originCode: OneTimeIncomeApplicationOrigins.Manual,
            appliedByUserId: Guid.NewGuid(),
            settlementPublicId: null,
            notes: null);

    private static PersonnelFileOneTimeIncome BuildFixed(decimal amount = 200m) =>
        Build(
            isFixedValue: true,
            calculationMethod: null,
            quantity: null, unitValue: null, multiplier: null, percentage: null, baseAmount: null,
            amount: amount);

    private static PersonnelFileOneTimeIncome Build(
        bool isFixedValue,
        string? calculationMethod,
        decimal? quantity,
        decimal? unitValue,
        decimal? multiplier,
        decimal? percentage,
        decimal? baseAmount,
        decimal amount) =>
        PersonnelFileOneTimeIncome.Create(
            incomeDate: Today,
            reference: "REF-1",
            conceptTypeCode: "BONO_EXTRA",
            conceptNameSnapshot: "Bono extraordinario",
            observations: null,
            isFixedValue: isFixedValue,
            calculationMethod: calculationMethod,
            quantity: quantity,
            unitValue: unitValue,
            multiplier: multiplier,
            percentage: percentage,
            baseAmount: baseAmount,
            amount: amount,
            currencyCode: "USD",
            assignedPositionPublicId: Guid.NewGuid(),
            costCenterPublicId: Guid.NewGuid(),
            costCenterNameSnapshot: "Centro de costo 1",
            requesterFilePublicId: Guid.NewGuid(),
            requesterNameSnapshot: "Jefe solicitante",
            payrollTypeCode: "MENSUAL",
            payrollPeriodId: null,
            payrollPeriodPublicId: null,
            payrollPeriodLabel: "Julio 2026",
            payrollPeriodEndDate: new DateOnly(2026, 7, 31),
            requestedByUserId: Guid.NewGuid());

    private static HashSet<string> LoadResourceKeys(string path)
    {
        var document = XDocument.Load(path);
        return document.Root?
            .Elements("data")
            .Select(static element => element.Attribute("name")?.Value)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal)
            ?? [];
    }

    private static string ResolveRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src"))
                && Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root for localization tests.");
    }
}

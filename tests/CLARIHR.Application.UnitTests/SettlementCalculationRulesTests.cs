using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage of the settlement calculation engine (PR-4) — the module's critical suite: per-plaza
/// seniority (P-01), legal caps (D-09), each SV formula (RF-008), the affectation bases and the
/// system-controlled taxable excess (RN-009.4), ISSS/AFP caps, the 2026 Renta brackets, the value-0 rule
/// (RN-008.4), adjustments that survive recalculation (D-14) and the totals identities (RF-010).
/// NOTE — golden cases: the [Theory] block at the end holds synthetic reference cases; replace/extend them
/// with the contador-signed historical settlements before building Ola 2 (analysis §18.1, blocking gate).
/// </summary>
public sealed class SettlementCalculationRulesTests
{
    private static readonly DateTime PlazaStart = new(2022, 3, 1);
    private static readonly DateTime Retirement = new(2026, 6, 15);

    // Tabla oficial vigente (MENSUAL) — same values as the DevSeed reference table.
    private static readonly TaxBracketInput[] RentaBrackets2026 =
    [
        new(0.01m, 472.00m, 0.00m, 0.00m, 0.00m),
        new(472.01m, 895.24m, 17.67m, 10.00m, 472.00m),
        new(895.25m, 2038.10m, 60.00m, 20.00m, 895.24m),
        new(2038.11m, null, 288.57m, 30.00m, 2038.10m),
    ];

    private static IReadOnlyList<SettlementConceptConfig> SvConcepts() =>
    [
        Concept(SettlementConceptCodes.Salario, "Salario pendiente", SettlementConceptClass.Ingreso, true, true, true, SettlementExemptionRule.Ninguna, null, true, null, 10),
        Concept(SettlementConceptCodes.VacacionProporcional, "Vacación proporcional", SettlementConceptClass.Ingreso, true, true, true, SettlementExemptionRule.Ninguna, null, true, null, 20),
        Concept(SettlementConceptCodes.AguinaldoProporcional, "Aguinaldo proporcional", SettlementConceptClass.Ingreso, false, false, true, SettlementExemptionRule.HastaLimitePorMinimo, 2.00m, true, null, 30),
        Concept(SettlementConceptCodes.Indemnizacion, "Indemnización", SettlementConceptClass.Ingreso, false, false, true, SettlementExemptionRule.HastaMontoLegal, null, true, null, 40),
        Concept(SettlementConceptCodes.RenunciaVoluntaria, "Renuncia voluntaria", SettlementConceptClass.Ingreso, false, false, true, SettlementExemptionRule.HastaMontoLegal, null, true, null, 50),
        Concept(SettlementConceptCodes.BonoPendiente, "Bono pendiente", SettlementConceptClass.Ingreso, true, true, true, SettlementExemptionRule.Ninguna, null, true, null, 60),
        Concept(SettlementConceptCodes.ComisionPendiente, "Comisión pendiente", SettlementConceptClass.Ingreso, true, true, true, SettlementExemptionRule.Ninguna, null, true, null, 70),
        Concept(SettlementConceptCodes.HorasExtrasPendientes, "Horas extras", SettlementConceptClass.Ingreso, true, true, true, SettlementExemptionRule.Ninguna, null, true, null, 80),
        Concept(SettlementConceptCodes.OtroIngreso, "Otro ingreso", SettlementConceptClass.Ingreso, true, true, true, SettlementExemptionRule.Ninguna, null, false, null, 90),
        Concept(SettlementConceptCodes.Isss, "ISSS", SettlementConceptClass.Descuento, false, false, false, SettlementExemptionRule.Ninguna, null, true, null, 100),
        Concept(SettlementConceptCodes.Afp, "AFP", SettlementConceptClass.Descuento, false, false, false, SettlementExemptionRule.Ninguna, null, true, null, 110),
        Concept(SettlementConceptCodes.Renta, "Renta", SettlementConceptClass.Descuento, false, false, false, SettlementExemptionRule.Ninguna, null, true, null, 120),
        Concept(SettlementConceptCodes.DescuentoExterno, "Descuento externo", SettlementConceptClass.Descuento, false, false, false, SettlementExemptionRule.Ninguna, null, true, null, 130),
        Concept(SettlementConceptCodes.OtroDescuento, "Otro descuento", SettlementConceptClass.Descuento, false, false, false, SettlementExemptionRule.Ninguna, null, false, null, 140),
        Concept(SettlementConceptCodes.IsssPatronal, "ISSS patronal", SettlementConceptClass.PagoPatronal, false, false, false, SettlementExemptionRule.Ninguna, null, true, 7.50m, 200),
        Concept(SettlementConceptCodes.AfpPatronal, "AFP patronal", SettlementConceptClass.PagoPatronal, false, false, false, SettlementExemptionRule.Ninguna, null, true, 8.75m, 210),
        Concept(SettlementConceptCodes.Incaf, "INCAF", SettlementConceptClass.PagoPatronal, false, false, false, SettlementExemptionRule.Ninguna, null, true, 1.00m, 220),
    ];

    private static SettlementConceptConfig Concept(
        string code, string name, SettlementConceptClass conceptClass, bool isss, bool afp, bool renta,
        SettlementExemptionRule rule, decimal? multiplier, bool systemCalculated, decimal? ratePercent, int sort) =>
        new(code, name, conceptClass, isss, afp, renta, rule, multiplier, systemCalculated, ratePercent, sort);

    private static SettlementCalculationInput Input(
        decimal monthlySalary = 600m,
        RetirementSeparationType separation = RetirementSeparationType.Voluntaria,
        DateTime? plazaStart = null,
        DateTime? retirement = null,
        decimal minimumWage = 365m,
        IReadOnlyList<SuggestedPlazaItem>? plazaItems = null,
        IReadOnlyList<TaxBracketInput>? brackets = null,
        IReadOnlyList<SettlementLineState>? existing = null,
        decimal? pendingVacationDays = null,
        CompensatoryTimeInput? compensatoryTime = null) =>
        new(
            SettlementKind.Liquidacion,
            separation,
            plazaStart ?? PlazaStart,
            retirement ?? Retirement,
            monthlySalary,
            SettlementParametersInput.SvDefaults(minimumWage),
            SvConcepts(),
            plazaItems ?? [],
            new ContributionSchemeInput(3.00m, 7.50m, 1000.00m),
            new ContributionSchemeInput(7.25m, 8.75m, 7045.06m),
            brackets ?? RentaBrackets2026,
            existing ?? [],
            pendingVacationDays,
            compensatoryTime);

    private static SettlementLineResult Line(SettlementCalculationResult result, string code) =>
        Assert.Single(result.Lines, line => line.ConceptCode == code);

    // ── [1] Seniority per plaza (P-01) ───────────────────────────────────────────

    [Fact]
    public void Seniority_IsAnchoredOnPlazaStartDate()
    {
        var result = SettlementCalculationRules.Calculate(Input());

        // 2022-03-01 → 2026-06-15 = 1567 days = 4 years + 107 days (÷365).
        Assert.Equal(4, result.Derived.SeniorityYears);
        Assert.Equal(107, result.Derived.SeniorityDays);
    }

    // ── [2] Legal caps (D-09: "salario máximo" = min(salario, N × mínimo)) ─────────

    [Fact]
    public void Caps_SalaryAboveLegalCaps_IsCapped()
    {
        var result = SettlementCalculationRules.Calculate(Input(monthlySalary: 2000m));

        Assert.Equal(4m * 365m, result.Derived.CappedMonthlySalaryIndemnity);   // 1460 < 2000
        Assert.Equal(2m * 365m, result.Derived.CappedMonthlySalaryResignation); // 730 < 2000
    }

    [Fact]
    public void Caps_SalaryBelowLegalCaps_UsesSalary()
    {
        var result = SettlementCalculationRules.Calculate(Input(monthlySalary: 600m));

        Assert.Equal(600m, result.Derived.CappedMonthlySalaryIndemnity);
        Assert.Equal(600m, result.Derived.CappedMonthlySalaryResignation);
    }

    // ── [3] Income formulas (RF-008) ────────────────────────────────────────────

    [Fact]
    public void Salario_DefaultPendingDays_AreTheRetirementDayOfMonth()
    {
        var result = SettlementCalculationRules.Calculate(Input());
        var line = Line(result, SettlementConceptCodes.Salario);

        // 15 days × (600/30 = 20.00/day) = 300.00
        Assert.Equal(15m, line.UnitsOrDays);
        Assert.Equal(300.00m, line.CalculatedAmount);
    }

    [Fact]
    public void Vacacion_UsesDaysSinceAnniversary_WithPremium()
    {
        var result = SettlementCalculationRules.Calculate(Input());
        var line = Line(result, SettlementConceptCodes.VacacionProporcional);

        // Anniversary 2026-03-01 → 2026-06-15 = 106 days; 20.00 × 15 × 1.30 × 106/365 = 113.26
        Assert.Equal(106m, line.UnitsOrDays);
        Assert.Equal(113.26m, line.CalculatedAmount);
    }

    [Fact]
    public void Vacacion_WithPendingFundDays_SuggestsFundInsteadOfAnniversary()
    {
        // RF-019: an employee with a live vacation fund → the initial suggestion uses the pending fund days
        // (12), NOT the 106 anniversary days. Amount = 20.00 × 15 × 1.30 × 12/365 = 12.82.
        var result = SettlementCalculationRules.Calculate(Input(pendingVacationDays: 12m));
        var line = Line(result, SettlementConceptCodes.VacacionProporcional);

        Assert.Equal(12m, line.UnitsOrDays);
        Assert.Equal(12.82m, line.CalculatedAmount);
    }

    [Fact]
    public void Vacacion_WithZeroPendingFundDays_FallsBackToAnniversary()
    {
        // Retrocompatibility: a null/0 fund keeps the legacy DaysSinceAnniversary default untouched.
        var result = SettlementCalculationRules.Calculate(Input(pendingVacationDays: 0m));
        var line = Line(result, SettlementConceptCodes.VacacionProporcional);

        Assert.Equal(106m, line.UnitsOrDays);
        Assert.Equal(113.26m, line.CalculatedAmount);
    }

    // ── HORAS_EXTRAS_PENDIENTES: compensatory-time pay-off line (REQ-002 RF-013/D-19) ─────────────

    [Fact]
    public void HorasExtras_WithFund_ProducesLine_GoldenA410()
    {
        // A.4-10: saldo 12 h, salario diario 20 (600/30), 8 h/día, factor 1.00 → base 2.50, monto 30.00.
        var result = SettlementCalculationRules.Calculate(
            Input(compensatoryTime: new CompensatoryTimeInput(PendingHours: 12m, StandardDailyHours: 8m, RateFactor: 1.00m)));
        var line = Line(result, SettlementConceptCodes.HorasExtrasPendientes);

        Assert.Equal(12m, line.UnitsOrDays);
        Assert.Equal(2.50m, line.CalculationBase);
        Assert.Equal(30.00m, line.CalculatedAmount);
        Assert.True(line.IsSystemCalculated);
        Assert.Contains("12 h", line.CalculationDetail);
        Assert.Contains("2.50/h", line.CalculationDetail);
        Assert.Contains("factor 1", line.CalculationDetail);
    }

    [Fact]
    public void HorasExtras_WithRateFactorTwo_DoublesTheAmount()
    {
        // A.4-10 variant: tarifa 2.00 → 12 × 2.50 × 2.00 = 60.00.
        var result = SettlementCalculationRules.Calculate(
            Input(compensatoryTime: new CompensatoryTimeInput(12m, 8m, 2.00m)));
        var line = Line(result, SettlementConceptCodes.HorasExtrasPendientes);

        Assert.Equal(12m, line.UnitsOrDays);
        Assert.Equal(2.50m, line.CalculationBase);
        Assert.Equal(60.00m, line.CalculatedAmount);
    }

    [Fact]
    public void HorasExtras_WithoutFund_ProducesNoLine_Retrocompatible()
    {
        // Retrocompatibility (both directions): a null fund emits NO line and leaves every other line and the
        // totals exactly as they were before the feature.
        var withoutFund = SettlementCalculationRules.Calculate(Input());
        Assert.DoesNotContain(withoutFund.Lines, line => line.ConceptCode == SettlementConceptCodes.HorasExtrasPendientes);

        // Adding a fund is purely additive: every pre-existing line keeps its amount; only the new line + the
        // affectation bases move.
        var withFund = SettlementCalculationRules.Calculate(Input(compensatoryTime: new CompensatoryTimeInput(12m, 8m, 1.00m)));
        foreach (var before in withoutFund.Lines)
        {
            var after = withFund.Lines.Single(line => line.ConceptCode == before.ConceptCode);
            if (before.ConceptClass == SettlementConceptClass.Ingreso)
            {
                Assert.Equal(before.CalculatedAmount, after.CalculatedAmount); // incomes are independent of the new line
            }
        }

        Assert.Equal(
            SettlementCalculationRules.Round2(withoutFund.Totals.TotalIncomes + 30.00m),
            withFund.Totals.TotalIncomes);
    }

    [Fact]
    public void HorasExtras_EditedHours_FeedTheFormula()
    {
        // The liquidator edits the hours to 10 → 10 × 2.50 × 1.00 = 25.00 (edited units survive recalculation).
        var fund = new CompensatoryTimeInput(12m, 8m, 1.00m);
        var baseline = SettlementCalculationRules.Calculate(Input(compensatoryTime: fund));
        var existing = ExistingFrom(baseline, line =>
            line.ConceptCode == SettlementConceptCodes.HorasExtrasPendientes
                ? StateOf(line, Guid.NewGuid()) with { UnitsOrDays = 10m }
                : StateOf(line, Guid.NewGuid()));

        var result = SettlementCalculationRules.Calculate(Input(compensatoryTime: fund, existing: existing));
        var line = Line(result, SettlementConceptCodes.HorasExtrasPendientes);

        Assert.Equal(10m, line.UnitsOrDays);
        Assert.Equal(25.00m, line.CalculatedAmount);
    }

    [Fact]
    public void HorasExtras_OverrideAmount_Survives()
    {
        // An audited override on the pay-off line survives recalculation (FinalAmount = override, RN-002.2).
        var fund = new CompensatoryTimeInput(12m, 8m, 1.00m);
        var baseline = SettlementCalculationRules.Calculate(Input(compensatoryTime: fund));
        var existing = ExistingFrom(baseline, line =>
            line.ConceptCode == SettlementConceptCodes.HorasExtrasPendientes
                ? StateOf(line, Guid.NewGuid()) with { OverrideAmount = 100m }
                : StateOf(line, Guid.NewGuid()));

        var result = SettlementCalculationRules.Calculate(Input(compensatoryTime: fund, existing: existing));
        var line = Line(result, SettlementConceptCodes.HorasExtrasPendientes);

        Assert.Equal(100m, line.OverrideAmount);
        Assert.Equal(100m, line.FinalAmount);
        Assert.Equal(30.00m, line.CalculatedAmount); // the underlying calculation is preserved
    }

    [Fact]
    public void HorasExtras_Line_JoinsIsssAfpRentaBases()
    {
        // The pay-off concept affects ISSS/AFP/Renta (exención Ninguna) → its 30.00 joins the three bases:
        // salario 300 + vacación 113.26 + horas extras 30 = 443.26.
        var result = SettlementCalculationRules.Calculate(Input(compensatoryTime: new CompensatoryTimeInput(12m, 8m, 1.00m)));

        Assert.Equal(443.26m, Line(result, SettlementConceptCodes.Isss).CalculationBase);
        Assert.Equal(443.26m, Line(result, SettlementConceptCodes.Afp).CalculationBase);
        Assert.Equal(443.26m, Line(result, SettlementConceptCodes.Renta).CalculationBase);
    }

    [Fact]
    public void Aguinaldo_TierAndPeriodDays_AreApplied()
    {
        var result = SettlementCalculationRules.Calculate(Input());
        var line = Line(result, SettlementConceptCodes.AguinaldoProporcional);

        // 4 full years → 19-day tier; period 2025-12-12 → 2026-06-15 = 185 days; 20.00 × 19 × 185/365 = 192.60
        Assert.Equal(19m, result.Derived.AguinaldoDays);
        Assert.Equal(185m, line.UnitsOrDays);
        Assert.Equal(192.60m, line.CalculatedAmount);
    }

    [Theory]
    [InlineData(1, 15)]
    [InlineData(3, 19)]
    [InlineData(9, 19)]
    [InlineData(10, 21)]
    public void Aguinaldo_Tiers_FollowSeniority(int years, int expectedDays)
    {
        var start = Retirement.AddDays(-(years * 365) - 10);
        var result = SettlementCalculationRules.Calculate(Input(plazaStart: start));

        Assert.Equal(expectedDays, result.Derived.AguinaldoDays);
    }

    [Fact]
    public void Aguinaldo_PlazaJoinedMidPeriod_ClipsThePeriod()
    {
        var start = new DateTime(2026, 2, 1);
        var result = SettlementCalculationRules.Calculate(Input(plazaStart: start, retirement: new DateTime(2026, 6, 15)));
        var line = Line(result, SettlementConceptCodes.AguinaldoProporcional);

        // Period clipped to the plaza start: 2026-02-01 → 2026-06-15 = 134 days.
        Assert.Equal(134m, line.UnitsOrDays);
    }

    [Fact]
    public void Indemnizacion_InvoluntarySeparation_YearsPlusFraction_Capped()
    {
        var result = SettlementCalculationRules.Calculate(Input(monthlySalary: 2000m, separation: RetirementSeparationType.Involuntaria));
        var line = Line(result, SettlementConceptCodes.Indemnizacion);

        // Capped monthly = 1460; 1460 × 4 + 1460 × 107/365 = 5840 + 428.00 = 6268.00
        Assert.Equal(6268.00m, line.CalculatedAmount);
        Assert.Null(result.Lines.FirstOrDefault(item => item.ConceptCode == SettlementConceptCodes.RenunciaVoluntaria));
    }

    [Fact]
    public void Renuncia_VoluntarySeparation_FifteenDaysPerYear_Capped()
    {
        var result = SettlementCalculationRules.Calculate(Input(monthlySalary: 2000m));
        var line = Line(result, SettlementConceptCodes.RenunciaVoluntaria);

        // Capped monthly = 730; (730/30) × 15 × (1567/365 = 4.2932…) = 1567.00
        Assert.Equal(1567.00m, line.CalculatedAmount);
        Assert.False(line.IsZeroByLaw);
    }

    [Fact]
    public void Renuncia_UnderMinimumService_IsZeroByLaw_NotDropped()
    {
        var result = SettlementCalculationRules.Calculate(Input(plazaStart: Retirement.AddDays(-400)));
        var line = Line(result, SettlementConceptCodes.RenunciaVoluntaria);

        Assert.True(line.IsZeroByLaw);
        Assert.Equal(SettlementCalculationRules.ZeroReasonMinimumService, line.ZeroReasonCode);
        Assert.Equal(0m, line.CalculatedAmount);
        Assert.Contains(result.Warnings, warning => warning.Code == SettlementCalculationRules.WarningZeroByLaw);
    }

    // ── [4]/[5] Bases and taxable excess (RN-009.4) ─────────────────────────────

    [Fact]
    public void Aguinaldo_OverExemptionLimit_ExcessJoinsRentaBase()
    {
        // Force a large aguinaldo via an override: exemption limit = 2 × 365 = 730.
        var baseline = SettlementCalculationRules.Calculate(Input());
        var aguinaldoId = Guid.NewGuid();
        var existing = ExistingFrom(baseline, line =>
            line.ConceptCode == SettlementConceptCodes.AguinaldoProporcional
                ? StateOf(line, aguinaldoId) with { OverrideAmount = 1000m }
                : StateOf(line, Guid.NewGuid()));

        var result = SettlementCalculationRules.Calculate(Input(existing: existing));
        var aguinaldo = Line(result, SettlementConceptCodes.AguinaldoProporcional);

        Assert.Equal(730m, aguinaldo.ExemptAmount);
        Assert.Equal(270m, aguinaldo.TaxableExcessAmount);
    }

    [Fact]
    public void Indemnizacion_UpwardOverride_OnlyTheExcessIsTaxable()
    {
        var baseline = SettlementCalculationRules.Calculate(Input(separation: RetirementSeparationType.Involuntaria));
        var existing = ExistingFrom(baseline, line =>
            line.ConceptCode == SettlementConceptCodes.Indemnizacion
                ? StateOf(line, Guid.NewGuid()) with { OverrideAmount = line.CalculatedAmount + 500m }
                : StateOf(line, Guid.NewGuid()));

        var result = SettlementCalculationRules.Calculate(Input(separation: RetirementSeparationType.Involuntaria, existing: existing));
        var line = Line(result, SettlementConceptCodes.Indemnizacion);

        Assert.Equal(line.CalculatedAmount, line.ExemptAmount);
        Assert.Equal(500m, line.TaxableExcessAmount);
    }

    // ── [6]/[7] Employee deductions ─────────────────────────────────────────────

    [Fact]
    public void Isss_BaseAboveCap_IsCappedAtThirtyDollars()
    {
        // Salary 2400 → salario line = 1200 (15 days × 80/day); vacación adds more → base > 1000 cap.
        var result = SettlementCalculationRules.Calculate(Input(monthlySalary: 2400m));
        var isss = Line(result, SettlementConceptCodes.Isss);

        Assert.Equal(1000.00m, isss.CalculationBase);
        Assert.Equal(30.00m, isss.CalculatedAmount);
    }

    [Fact]
    public void Renta_UsesTheBracketOfTheTaxableBase()
    {
        // Salary 1200 (daily 40), plaza 400 días: salario 600 + vacación 74.79 (35 días desde aniversario)
        // = gravable 674.79 (aguinaldo exento; renuncia en 0 legal) → tramo 2: 17.67 + 10% × (674.79 − 472) = 37.95
        var result = SettlementCalculationRules.Calculate(Input(monthlySalary: 1200m, plazaStart: Retirement.AddDays(-400)));
        var renta = Line(result, SettlementConceptCodes.Renta);

        Assert.Equal(37.95m, renta.CalculatedAmount);
    }

    [Fact]
    public void Renta_WithoutBrackets_IsZeroWithWarning()
    {
        var result = SettlementCalculationRules.Calculate(Input(brackets: []));
        var renta = Line(result, SettlementConceptCodes.Renta);

        Assert.Equal(0m, renta.CalculatedAmount);
        Assert.Contains(result.Warnings, warning => warning.Code == SettlementCalculationRules.WarningRentaBracketsMissing);
    }

    // ── [9]/[10] Employer charges + provision ────────────────────────────────────

    [Fact]
    public void EmployerCharges_UseEmployerRates_AndIncafOverIsssBase()
    {
        var result = SettlementCalculationRules.Calculate(Input(monthlySalary: 600m));
        var isssPatronal = Line(result, SettlementConceptCodes.IsssPatronal);
        var afpPatronal = Line(result, SettlementConceptCodes.AfpPatronal);
        var incaf = Line(result, SettlementConceptCodes.Incaf);

        // Base afecta ISSS/AFP = salario 300 + vacación 113.26 = 413.26
        Assert.Equal(Math.Round(0.075m * 413.26m, 2), isssPatronal.CalculatedAmount);
        Assert.Equal(Math.Round(0.0875m * 413.26m, 2), afpPatronal.CalculatedAmount);
        Assert.Equal(Math.Round(0.01m * 413.26m, 2), incaf.CalculatedAmount);
    }

    [Fact]
    public void Totals_HonorTheIdentities()
    {
        var result = SettlementCalculationRules.Calculate(Input(monthlySalary: 1200m, plazaItems:
        [
            new SuggestedPlazaItem(SettlementConceptCodes.BonoPendiente, "Bono trimestral", 150m, null),
            new SuggestedPlazaItem(SettlementConceptCodes.DescuentoExterno, "Préstamo bancario", 85.50m, "Banco Agrícola"),
        ]));

        var incomes = result.Lines.Where(line => line is { ConceptClass: SettlementConceptClass.Ingreso, IsIncluded: true }).Sum(line => line.FinalAmount);
        var deductions = result.Lines.Where(line => line is { ConceptClass: SettlementConceptClass.Descuento, IsIncluded: true }).Sum(line => line.FinalAmount);
        var employer = result.Lines.Where(line => line is { ConceptClass: SettlementConceptClass.PagoPatronal, IsIncluded: true }).Sum(line => line.FinalAmount);

        Assert.Equal(SettlementCalculationRules.Round2(incomes), result.Totals.TotalIncomes);
        Assert.Equal(SettlementCalculationRules.Round2(deductions), result.Totals.TotalDeductions);
        Assert.Equal(SettlementCalculationRules.Round2(incomes - deductions), result.Totals.NetPay);
        Assert.Equal(SettlementCalculationRules.Round2(incomes + employer), result.Totals.ProvisionTotal);

        var externo = Line(result, SettlementConceptCodes.DescuentoExterno);
        Assert.Equal(85.50m, externo.CalculatedAmount);
        Assert.Equal("Banco Agrícola", externo.CounterpartyName);
    }

    // ── Adjustments survive recalculation (D-14 / RN-002.2) ─────────────────────

    [Fact]
    public void ExcludedLine_LeavesBasesAndTotals()
    {
        var baseline = SettlementCalculationRules.Calculate(Input());
        var withVacacion = baseline.Totals.TotalIncomes;

        var existing = ExistingFrom(baseline, line =>
            line.ConceptCode == SettlementConceptCodes.VacacionProporcional
                ? StateOf(line, Guid.NewGuid()) with { IsIncluded = false }
                : StateOf(line, Guid.NewGuid()));
        var result = SettlementCalculationRules.Calculate(Input(existing: existing));

        Assert.True(result.Totals.TotalIncomes < withVacacion);
        var isss = Line(result, SettlementConceptCodes.Isss);
        Assert.Equal(300.00m, isss.CalculationBase); // only salario remains in the base
    }

    [Fact]
    public void EditedDays_FeedTheFormula()
    {
        var baseline = SettlementCalculationRules.Calculate(Input());
        var existing = ExistingFrom(baseline, line =>
            line.ConceptCode == SettlementConceptCodes.Salario
                ? StateOf(line, Guid.NewGuid()) with { UnitsOrDays = 10m }
                : StateOf(line, Guid.NewGuid()));

        var result = SettlementCalculationRules.Calculate(Input(existing: existing));
        var salario = Line(result, SettlementConceptCodes.Salario);

        Assert.Equal(10m, salario.UnitsOrDays);
        Assert.Equal(200.00m, salario.CalculatedAmount);
    }

    [Fact]
    public void BothCompensationsIncluded_RaisesWarning()
    {
        var baseline = SettlementCalculationRules.Calculate(Input(separation: RetirementSeparationType.Involuntaria));
        var existing = baseline.Lines.Select(line => StateOf(line, Guid.NewGuid())).ToList();
        existing.Add(new SettlementLineState(
            Guid.NewGuid(), SettlementConceptCodes.RenunciaVoluntaria, SettlementConceptClass.Ingreso,
            IsIncluded: true, UnitsOrDays: null, OverrideAmount: null, IsManual: false, ManualAmount: 0m, null, null));

        var result = SettlementCalculationRules.Calculate(Input(separation: RetirementSeparationType.Involuntaria, existing: existing));

        Assert.Contains(result.Warnings, warning => warning.Code == SettlementCalculationRules.WarningBothCompensations);
    }

    // ── Golden reference cases (synthetic until the contador-signed ones land — §18.1) ─────────

    [Theory]
    // salario, categoría, neto esperado — caso sintético de referencia integral (verificado a mano):
    // renuncia 4a+107d, salario 600 (diario 20): ingresos 300 (salario 15d) + 113.26 (vacación 106d)
    // + 192.60 (aguinaldo 19d×185/365) + 1287.95 (prestación: 20 × 15d × 4.293151 años) = 1893.81
    // ISSS 3%×413.26=12.40; AFP 7.25%×413.26=29.96; renta base 413.26 → tramo 1 (0%) = 0
    // neto = 1893.81 − 42.36 = 1851.45
    [InlineData(600, "VOLUNTARIA", 1851.45)]
    public void GoldenCase_Synthetic_EndToEnd(decimal salary, string category, decimal expectedNet)
    {
        var separation = category == "VOLUNTARIA" ? RetirementSeparationType.Voluntaria : RetirementSeparationType.Involuntaria;
        var result = SettlementCalculationRules.Calculate(Input(monthlySalary: salary, separation: separation));

        Assert.Equal(expectedNet, result.Totals.NetPay);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private static SettlementLineState StateOf(SettlementLineResult line, Guid id) => new(
        line.LinePublicId ?? id,
        line.ConceptCode,
        line.ConceptClass,
        line.IsIncluded,
        UnitsOrDays: null,
        OverrideAmount: line.OverrideAmount,
        IsManual: !line.IsSystemCalculated,
        ManualAmount: line.IsSystemCalculated ? 0m : line.CalculatedAmount,
        line.Description,
        line.CounterpartyName);

    private static List<SettlementLineState> ExistingFrom(
        SettlementCalculationResult baseline,
        Func<SettlementLineResult, SettlementLineState> projector) =>
        baseline.Lines.Select(projector).ToList();
}

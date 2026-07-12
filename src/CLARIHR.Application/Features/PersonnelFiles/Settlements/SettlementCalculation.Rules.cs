using CLARIHR.Application.Features.PersonnelFiles.CompensatoryTime;
using CLARIHR.Application.Features.PersonnelFiles.Overtime;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// Pure input model of the settlement engine: EVERYTHING external is resolved beforehand by the
// data provider (no I/O here). Amount conventions: monthly figures, USD, rates in percent.
// ─────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>Legal parameters snapshot the engine applies (RF-011; SV defaults, all ratified).</summary>
internal sealed record SettlementParametersInput(
    decimal MinimumMonthlyWage,
    decimal IndemnityCapMultiplier,
    decimal ResignationCapMultiplier,
    decimal VacationDays,
    decimal VacationPremiumPercent,
    decimal? AguinaldoDaysOverride,
    decimal ResignationBenefitDays,
    int ResignationMinimumServiceYears,
    decimal AguinaldoExemptionMultiplier,
    int MonthDivisorDays,
    int YearDivisorDays)
{
    public static SettlementParametersInput SvDefaults(decimal minimumMonthlyWage) => new(
        MinimumMonthlyWage: minimumMonthlyWage,
        IndemnityCapMultiplier: 4m,
        ResignationCapMultiplier: 2m,
        VacationDays: 15m,
        VacationPremiumPercent: 30m,
        AguinaldoDaysOverride: null,
        ResignationBenefitDays: 15m,
        ResignationMinimumServiceYears: 2,
        AguinaldoExemptionMultiplier: 2m,
        MonthDivisorDays: 30,
        YearDivisorDays: 365);
}

/// <summary>ISSS/AFP scheme effective for the employee (instance rates → catalog defaults).</summary>
internal sealed record ContributionSchemeInput(
    decimal EmployeeRatePercent,
    decimal EmployerRatePercent,
    decimal? ContributionCap);

/// <summary>One income-tax withholding bracket (tabla oficial vigente 2026, MENSUAL).</summary>
internal sealed record TaxBracketInput(
    decimal LowerBound,
    decimal? UpperBound,
    decimal FixedFee,
    decimal RatePercent,
    decimal ExcessOver);

/// <summary>Catalog config of one settlement concept (class, affectation matrix, exemption rule — D-07).</summary>
internal sealed record SettlementConceptConfig(
    string Code,
    string Name,
    SettlementConceptClass ConceptClass,
    bool AffectsIsss,
    bool AffectsAfp,
    bool AffectsRenta,
    SettlementExemptionRule ExemptionRule,
    decimal? ExemptionMultiplier,
    bool IsSystemCalculated,
    decimal? DefaultRatePercent,
    int SortOrder);

/// <summary>
/// A suggested line sourced from the plaza's compensation config (D-08 ampliada): pending bonus/commission
/// incomes and external-deduction last installments (with counterparty).
/// </summary>
internal sealed record SuggestedPlazaItem(
    string ConceptCode,
    string Description,
    decimal Amount,
    string? CounterpartyName);

/// <summary>
/// The persisted state of one line fed back into a recalculation: which adjustments survive (inclusion,
/// edited days, audited override — D-14) and the manual lines the user appended.
/// </summary>
internal sealed record SettlementLineState(
    Guid LinePublicId,
    string ConceptCode,
    SettlementConceptClass ConceptClass,
    bool IsIncluded,
    decimal? UnitsOrDays,
    decimal? OverrideAmount,
    bool IsManual,
    decimal ManualAmount,
    string? Description,
    string? CounterpartyName);

/// <summary>
/// The employee's compensatory-time fund at retirement (REQ-002 RF-013/D-19) mirrored into the pure engine:
/// pending balance in hours, the standard daily hours used to value the hour, and the settlement rate factor
/// (P-15). Null ⇒ no HORAS_EXTRAS_PENDIENTES line is emitted (retrocompatible).
/// </summary>
internal sealed record CompensatoryTimeInput(
    decimal PendingHours,
    decimal StandardDailyHours,
    decimal RateFactor);

/// <summary>
/// The employee's pending overtime at retirement (REQ-007 RF-014/§0.15) mirrored into the pure engine: the
/// AUTORIZADA overtime records of the plaza being settled (each an hour/factor pair) and the standard daily
/// hours used to value the hour. Null ⇒ no HORAS_EXTRAS_PENDIENTES_PAGO line is emitted (retrocompatible).
/// </summary>
internal sealed record OvertimeInput(
    IReadOnlyList<OvertimeFactoredRecord> Records,
    decimal StandardDailyHours);

/// <summary>Everything the engine needs, resolved (pure — pre-development clarification №2: snapshot at create/regenerate).</summary>
internal sealed record SettlementCalculationInput(
    SettlementKind Kind,
    RetirementSeparationType SeparationType,
    DateTime PlazaStartDate,
    DateTime RetirementDate,
    decimal MonthlyBaseSalary,
    SettlementParametersInput Parameters,
    IReadOnlyList<SettlementConceptConfig> Concepts,
    IReadOnlyList<SuggestedPlazaItem> PlazaItems,
    ContributionSchemeInput Isss,
    ContributionSchemeInput Afp,
    IReadOnlyList<TaxBracketInput> RentaBrackets,
    IReadOnlyList<SettlementLineState> ExistingLines,
    decimal? PendingVacationDays = null,
    CompensatoryTimeInput? CompensatoryTime = null,
    OvertimeInput? Overtime = null);

// ── Output model ─────────────────────────────────────────────────────────────────────────────

internal sealed record SettlementLineResult(
    Guid? LinePublicId,
    string ConceptCode,
    string ConceptName,
    SettlementConceptClass ConceptClass,
    bool IsSystemCalculated,
    int SortOrder,
    string? Description,
    string? CounterpartyName,
    decimal? CalculationBase,
    decimal? UnitsOrDays,
    decimal CalculatedAmount,
    decimal ExemptAmount,
    decimal TaxableExcessAmount,
    decimal? OverrideAmount,
    bool IsIncluded,
    bool IsZeroByLaw,
    string? ZeroReasonCode,
    string? CalculationDetail)
{
    /// <summary>The amount that joins the totals: the audited override when present (D-14).</summary>
    public decimal FinalAmount => OverrideAmount ?? CalculatedAmount;
}

internal sealed record SettlementDerivedResult(
    int SeniorityYears,
    int SeniorityDays,
    decimal SeniorityFractionalYears,
    decimal DailySalary,
    decimal CappedMonthlySalaryIndemnity,
    decimal CappedMonthlySalaryResignation,
    decimal AguinaldoDays);

internal sealed record SettlementTotalsResult(
    decimal TotalIncomes,
    decimal TotalDeductions,
    decimal NetPay,
    decimal TotalEmployerCharges,
    decimal ProvisionTotal);

/// <summary>Non-blocking warning surfaced in the response (`warnings[]` — pre-development clarification №4).</summary>
internal sealed record SettlementWarning(string Code, string ConceptCode);

internal sealed record SettlementCalculationResult(
    SettlementDerivedResult Derived,
    IReadOnlyList<SettlementLineResult> Lines,
    SettlementTotalsResult Totals,
    IReadOnlyList<SettlementWarning> Warnings);

/// <summary>
/// The settlement calculation engine (RF-008…RF-010) — 100% pure and deterministic (the module's real risk
/// is legal exactness, so every step is unit-testable without infrastructure and traced per line). Steps:
/// [1] per-plaza seniority from the assignment StartDate (P-01) → [2] legal caps min(salary, N × minimum
/// wage) (D-09) → [3] income lines by SeparationType + plaza suggestions + adjustments; unmet legal
/// requirement ⇒ value 0 (RN-008.4) → [4] affectation bases → [5] exemption split per line, excess joins the
/// Renta base (RN-009.4, ratified: the system controls it) → [6] ISSS/AFP employee → [7] Renta by brackets
/// (tabla 2026; empty ⇒ 0 + warning) → [8] external/manual deductions → [9] employer charges ISSS/AFP/INCAF
/// → [10] provision = incomes + employer charges (D-13/P-02) → [11] totals (rounding per line, clarification №3).
/// </summary>
internal static class SettlementCalculationRules
{
    // Warning codes (bilingual texts in resx; surfaced as `warnings[]`, never ProblemDetails).
    public const string WarningRentaBracketsMissing = "SETTLEMENT_WARNING_RENTA_BRACKETS_MISSING";
    public const string WarningZeroByLaw = "SETTLEMENT_WARNING_ZERO_BY_LAW";
    public const string WarningNetNegative = "SETTLEMENT_WARNING_NET_NEGATIVE";
    public const string WarningBothCompensations = "SETTLEMENT_WARNING_BOTH_COMPENSATIONS";

    public const string ZeroReasonMinimumService = "SERVICIO_MINIMO_NO_CUMPLIDO";

    /// <summary>Single rounding rule of the module (clarification №3): half-up, 2 decimals, per line.</summary>
    public static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    public static SettlementCalculationResult Calculate(SettlementCalculationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var parameters = input.Parameters;

        // [1] Per-plaza seniority (P-01 ratified: from the assignment StartDate to the retirement date).
        var totalDays = Math.Max(0, (input.RetirementDate.Date - input.PlazaStartDate.Date).Days);
        var years = totalDays / parameters.YearDivisorDays;
        var remainderDays = totalDays - (years * parameters.YearDivisorDays);
        var fractionalYears = parameters.YearDivisorDays == 0 ? 0m : (decimal)totalDays / parameters.YearDivisorDays;

        // [2] Daily salary and legal caps (D-09: "salario máximo" = min(salario, N × mínimo), system-computed).
        var dailySalary = parameters.MonthDivisorDays == 0 ? 0m : input.MonthlyBaseSalary / parameters.MonthDivisorDays;
        var cappedIndemnity = Math.Min(input.MonthlyBaseSalary, parameters.IndemnityCapMultiplier * parameters.MinimumMonthlyWage);
        var cappedResignation = Math.Min(input.MonthlyBaseSalary, parameters.ResignationCapMultiplier * parameters.MinimumMonthlyWage);

        // Aguinaldo days by seniority tier (ratified: 15 / 19 / 21 — parameters allow an override).
        var aguinaldoDays = parameters.AguinaldoDaysOverride ?? years switch
        {
            >= 10 => 21m,
            >= 3 => 19m,
            _ => 15m,
        };

        var derived = new SettlementDerivedResult(
            years, remainderDays, fractionalYears, dailySalary, cappedIndemnity, cappedResignation, aguinaldoDays);

        var concepts = input.Concepts.ToDictionary(concept => concept.Code, StringComparer.OrdinalIgnoreCase);
        var warnings = new List<SettlementWarning>();
        var lines = new List<SettlementLineResult>();

        // [3] Income lines: from the existing state (recalculation) or freshly suggested (initial/regenerate).
        var specs = input.ExistingLines.Count > 0
            ? BuildSpecsFromExisting(input)
            : BuildSuggestedSpecs(input);

        foreach (var spec in specs.Where(spec => spec.ConceptClass == SettlementConceptClass.Ingreso))
        {
            lines.Add(ComputeIncomeLine(spec, input, derived, concepts, warnings));
        }

        // [4] Affectation bases over the INCLUDED income lines' final amounts (an override moves the bases too).
        decimal isssBase = 0m, afpBase = 0m, rentaBase = 0m;
        foreach (var line in lines.Where(line => line.IsIncluded))
        {
            if (!concepts.TryGetValue(line.ConceptCode, out var concept))
            {
                continue;
            }

            var final = line.FinalAmount;
            if (concept.AffectsIsss)
            {
                isssBase += final;
            }

            if (concept.AffectsAfp)
            {
                afpBase += final;
            }

            if (concept.AffectsRenta)
            {
                rentaBase += line.TaxableExcessAmount;
            }
        }

        // [6]-[8] Deduction lines (employee): ISSS, AFP, Renta, external installments and manual deductions.
        foreach (var spec in specs.Where(spec => spec.ConceptClass == SettlementConceptClass.Descuento))
        {
            lines.Add(ComputeDeductionLine(spec, input, concepts, isssBase, afpBase, rentaBase, warnings));
        }

        // [9] Employer charges (pagos patronales): ISSS/AFP employer rates + INCAF over the ISSS base (P-02).
        foreach (var spec in specs.Where(spec => spec.ConceptClass == SettlementConceptClass.PagoPatronal))
        {
            lines.Add(ComputeEmployerLine(spec, input, concepts, isssBase, afpBase));
        }

        // [10]-[11] Totals: sum of per-line-rounded FINAL amounts of the included lines.
        var totalIncomes = lines.Where(line => line is { ConceptClass: SettlementConceptClass.Ingreso, IsIncluded: true }).Sum(line => line.FinalAmount);
        var totalDeductions = lines.Where(line => line is { ConceptClass: SettlementConceptClass.Descuento, IsIncluded: true }).Sum(line => line.FinalAmount);
        var totalEmployer = lines.Where(line => line is { ConceptClass: SettlementConceptClass.PagoPatronal, IsIncluded: true }).Sum(line => line.FinalAmount);
        var netPay = totalIncomes - totalDeductions;
        var provision = totalIncomes + totalEmployer;

        if (netPay < 0)
        {
            warnings.Add(new SettlementWarning(WarningNetNegative, string.Empty));
        }

        var includedCodes = lines.Where(line => line.IsIncluded).Select(line => line.ConceptCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (includedCodes.Contains(SettlementConceptCodes.Indemnizacion) && includedCodes.Contains(SettlementConceptCodes.RenunciaVoluntaria))
        {
            warnings.Add(new SettlementWarning(WarningBothCompensations, string.Empty));
        }

        return new SettlementCalculationResult(
            derived,
            lines.OrderBy(line => line.SortOrder).ThenBy(line => line.ConceptCode, StringComparer.Ordinal).ToArray(),
            new SettlementTotalsResult(Round2(totalIncomes), Round2(totalDeductions), Round2(netPay), Round2(totalEmployer), Round2(provision)),
            warnings);
    }

    // ── Line-spec assembly ───────────────────────────────────────────────────────────────────

    private sealed record LineSpec(
        Guid? LinePublicId,
        string ConceptCode,
        SettlementConceptClass ConceptClass,
        bool IsIncluded,
        decimal? UnitsOrDays,
        decimal? OverrideAmount,
        bool IsManual,
        decimal ManualAmount,
        string? Description,
        string? CounterpartyName);

    /// <summary>Initial generation / explicit regenerate: suggested lines per SeparationType + plaza config (D-08).</summary>
    private static List<LineSpec> BuildSuggestedSpecs(SettlementCalculationInput input)
    {
        var specs = new List<LineSpec>
        {
            EngineSpec(SettlementConceptCodes.Salario),

            // VACACION_PROPORCIONAL (RF-019): seed the suggested units with the employee's pending fund days
            // when a fund exists (> 0); otherwise leave null so ComputeIncomeLine keeps the legacy
            // DaysSinceAnniversary default (retrocompatible — no fund ⇒ identical to the prior behaviour).
            new LineSpec(
                null, SettlementConceptCodes.VacacionProporcional, ResolveClass(SettlementConceptCodes.VacacionProporcional),
                IsIncluded: true, UnitsOrDays: input.PendingVacationDays > 0 ? input.PendingVacationDays : null,
                OverrideAmount: null, IsManual: false, ManualAmount: 0m, Description: null, CounterpartyName: null),

            EngineSpec(SettlementConceptCodes.AguinaldoProporcional),
        };

        // Mutually exclusive by default (RN-002.3): involuntary → indemnización; voluntary → prestación.
        switch (input.SeparationType)
        {
            case RetirementSeparationType.Involuntaria:
                specs.Add(EngineSpec(SettlementConceptCodes.Indemnizacion));
                break;
            case RetirementSeparationType.Voluntaria:
                specs.Add(EngineSpec(SettlementConceptCodes.RenunciaVoluntaria));
                break;
        }

        // HORAS_EXTRAS_PENDIENTES (REQ-002 RF-013/D-19): the employee's positive compensatory-time balance on
        // the principal plaza becomes an automatic pay-off line. No fund (context null / balance 0) ⇒ no spec ⇒
        // the settlement suite stays identical (retrocompatible). The seed is IsSystemCalculated=true so the case
        // below runs (and the manual path is closed — no double auto+manual pay-off).
        if (input.CompensatoryTime is { PendingHours: > 0m })
        {
            specs.Add(EngineSpec(SettlementConceptCodes.HorasExtrasPendientes));
        }

        // HORAS_EXTRAS_PENDIENTES_PAGO (REQ-007 RF-014/§0.15): the AUTORIZADA overtime records of the plaza being
        // settled become an automatic pay-off line (Σ hours × factor × hourly rate). No records ⇒ no spec ⇒ the
        // settlement suite stays identical (retrocompatible). The seed is IsSystemCalculated=true so the case below
        // runs (and the manual path is closed — no double auto+manual pay-off).
        if (input.Overtime is { Records.Count: > 0 })
        {
            specs.Add(EngineSpec(SettlementConceptCodes.HorasExtrasPendientesPago));
        }

        // Plaza-config suggestions: pending bonus/commission incomes and external-deduction installments.
        specs.AddRange(input.PlazaItems.Select(item => new LineSpec(
            null, item.ConceptCode, ResolveClass(item.ConceptCode), IsIncluded: true, UnitsOrDays: null,
            OverrideAmount: null, IsManual: true, ManualAmount: item.Amount, item.Description, item.CounterpartyName)));

        specs.Add(EngineSpec(SettlementConceptCodes.Isss));
        specs.Add(EngineSpec(SettlementConceptCodes.Afp));
        specs.Add(EngineSpec(SettlementConceptCodes.Renta));
        specs.Add(EngineSpec(SettlementConceptCodes.IsssPatronal));
        specs.Add(EngineSpec(SettlementConceptCodes.AfpPatronal));
        specs.Add(EngineSpec(SettlementConceptCodes.Incaf));
        return specs;

        static LineSpec EngineSpec(string code) => new(
            null, code, ResolveClass(code), IsIncluded: true, UnitsOrDays: null, OverrideAmount: null,
            IsManual: false, ManualAmount: 0m, Description: null, CounterpartyName: null);
    }

    /// <summary>Recalculation over the persisted lines: every adjustment survives (D-14/RN-002.2).</summary>
    private static List<LineSpec> BuildSpecsFromExisting(SettlementCalculationInput input) =>
        input.ExistingLines
            .Select(line => new LineSpec(
                line.LinePublicId, line.ConceptCode, line.ConceptClass, line.IsIncluded, line.UnitsOrDays,
                line.OverrideAmount, line.IsManual, line.ManualAmount, line.Description, line.CounterpartyName))
            .ToList();

    // This switch is CLOSED and defaults to Ingreso: every deduction concept must be listed explicitly, or its
    // amount would be PAID to the employee instead of discounted from the settlement.
    private static SettlementConceptClass ResolveClass(string conceptCode) => conceptCode.ToUpperInvariant() switch
    {
        SettlementConceptCodes.Isss or SettlementConceptCodes.Afp or SettlementConceptCodes.Renta
            or SettlementConceptCodes.DescuentoExterno or SettlementConceptCodes.DescuentoCiclicoPendiente
            or SettlementConceptCodes.DescuentoEventualPendiente
            or SettlementConceptCodes.OtroDescuento => SettlementConceptClass.Descuento,
        SettlementConceptCodes.IsssPatronal or SettlementConceptCodes.AfpPatronal or SettlementConceptCodes.Incaf => SettlementConceptClass.PagoPatronal,
        _ => SettlementConceptClass.Ingreso,
    };

    // ── Income computation (RF-008) ──────────────────────────────────────────────────────────

    private static SettlementLineResult ComputeIncomeLine(
        LineSpec spec,
        SettlementCalculationInput input,
        SettlementDerivedResult derived,
        IReadOnlyDictionary<string, SettlementConceptConfig> concepts,
        List<SettlementWarning> warnings)
    {
        var concept = ResolveConcept(concepts, spec.ConceptCode, SettlementConceptClass.Ingreso);
        var parameters = input.Parameters;

        decimal? units = null;
        decimal? calculationBase = null;
        var calculated = 0m;
        string? detail = null;
        var zeroByLaw = false;
        string? zeroReason = null;

        if (spec.IsManual || !concept.IsSystemCalculated)
        {
            calculated = Round2(spec.ManualAmount);
            detail = "Monto manual";
        }
        else
        {
            switch (spec.ConceptCode.ToUpperInvariant())
            {
                case SettlementConceptCodes.Salario:
                    // Pending salary days: the user rules (the external payroll knows what was paid — S-07);
                    // default = days from the 1st of the retirement month through the retirement date.
                    units = spec.UnitsOrDays ?? input.RetirementDate.Day;
                    calculationBase = derived.DailySalary;
                    calculated = Round2(units.Value * derived.DailySalary);
                    detail = $"{units:0.##} días × {derived.DailySalary:0.00}/día";
                    break;

                case SettlementConceptCodes.VacacionProporcional:
                    units = spec.UnitsOrDays ?? DaysSinceAnniversary(input.PlazaStartDate, input.RetirementDate);
                    calculationBase = derived.DailySalary;
                    calculated = Round2(derived.DailySalary * parameters.VacationDays * (1 + (parameters.VacationPremiumPercent / 100m)) * units.Value / parameters.YearDivisorDays);
                    detail = $"{derived.DailySalary:0.00} × {parameters.VacationDays:0.##} × {(1 + (parameters.VacationPremiumPercent / 100m)):0.00} × {units:0.##}/{parameters.YearDivisorDays}";
                    break;

                case SettlementConceptCodes.AguinaldoProporcional:
                    units = spec.UnitsOrDays ?? DaysInAguinaldoPeriod(input.PlazaStartDate, input.RetirementDate);
                    calculationBase = derived.DailySalary;
                    calculated = Round2(derived.DailySalary * derived.AguinaldoDays * units.Value / parameters.YearDivisorDays);
                    detail = $"{derived.DailySalary:0.00} × {derived.AguinaldoDays:0.##} días × {units:0.##}/{parameters.YearDivisorDays}";
                    break;

                case SettlementConceptCodes.HorasExtrasPendientes:
                    // Compensatory-time pay-off (REQ-002 RF-013/D-19): hours = the liquidator's edited units when
                    // present, else the pending fund balance; valued at Round2(dailySalary / standardDailyHours) ×
                    // rateFactor. ISSS/AFP/Renta affectations are automatic (the concept is configured Ingreso,
                    // affects the 3 bases, exención Ninguna). Defaults 8 h/day and factor 1.00 (P-15).
                    var standardDailyHours = input.CompensatoryTime?.StandardDailyHours ?? 8m;
                    var rateFactor = input.CompensatoryTime?.RateFactor ?? 1m;
                    units = spec.UnitsOrDays ?? input.CompensatoryTime?.PendingHours ?? 0m;
                    calculationBase = CompensatoryTimeRules.HourlyRate(derived.DailySalary, standardDailyHours);
                    calculated = CompensatoryTimeRules.SettlementAmount(units.Value, calculationBase.Value, rateFactor);
                    detail = $"{units:0.##} h × {calculationBase:0.00}/h × factor {rateFactor:0.##}";
                    break;

                case SettlementConceptCodes.HorasExtrasPendientesPago:
                    // Overtime pay-off (REQ-007 RF-014/§0.15): factored hours = the liquidator's edited units when
                    // present, else Σ(decimalHours × factor) of the plaza's AUTORIZADA records; valued at
                    // Round2(dailySalary / standardDailyHours). ISSS/AFP/Renta affectations are automatic (the concept
                    // is configured Ingreso, affects the 3 bases, exención Ninguna). Default 8 h/day (§0.15/№3).
                    var overtimeRecords = input.Overtime?.Records ?? [];
                    var overtimeStandardDailyHours = input.Overtime?.StandardDailyHours ?? 8m;
                    units = spec.UnitsOrDays ?? OvertimeRecordRules.FactoredHours(overtimeRecords);
                    calculationBase = OvertimeRecordRules.HourlyRate(derived.DailySalary, overtimeStandardDailyHours);
                    calculated = OvertimeRecordRules.SettlementAmount(units.Value, calculationBase.Value);
                    detail = $"{overtimeRecords.Count} jornada(s) · {units:0.##} h-factor × {calculationBase:0.00}/h";
                    break;

                case SettlementConceptCodes.Indemnizacion:
                    units = Round2(derived.SeniorityFractionalYears);
                    calculationBase = derived.CappedMonthlySalaryIndemnity;
                    calculated = Round2(
                        (derived.CappedMonthlySalaryIndemnity * derived.SeniorityYears) +
                        (derived.CappedMonthlySalaryIndemnity * derived.SeniorityDays / parameters.YearDivisorDays));
                    detail = $"{derived.CappedMonthlySalaryIndemnity:0.00} (tope {parameters.IndemnityCapMultiplier:0.##}× mínimo) × {derived.SeniorityYears} años + {derived.SeniorityDays}/{parameters.YearDivisorDays}";
                    break;

                case SettlementConceptCodes.RenunciaVoluntaria:
                    calculationBase = derived.CappedMonthlySalaryResignation;
                    if (derived.SeniorityYears < parameters.ResignationMinimumServiceYears)
                    {
                        // RN-008.4 (ratified §17.6): unmet legal requirement ⇒ the line is recorded at 0.
                        zeroByLaw = true;
                        zeroReason = ZeroReasonMinimumService;
                        calculated = 0m;
                        detail = $"No cumple {parameters.ResignationMinimumServiceYears} años de servicio continuo (antigüedad: {derived.SeniorityYears} años)";
                        warnings.Add(new SettlementWarning(WarningZeroByLaw, concept.Code));
                    }
                    else
                    {
                        units = Round2(derived.SeniorityFractionalYears);
                        var dailyCapped = derived.CappedMonthlySalaryResignation / parameters.MonthDivisorDays;
                        calculated = Round2(dailyCapped * parameters.ResignationBenefitDays * derived.SeniorityFractionalYears);
                        detail = $"({derived.CappedMonthlySalaryResignation:0.00} tope {parameters.ResignationCapMultiplier:0.##}× mínimo / {parameters.MonthDivisorDays}) × {parameters.ResignationBenefitDays:0.##} días × {derived.SeniorityFractionalYears:0.####} años";
                    }

                    break;

                default:
                    calculated = Round2(spec.ManualAmount);
                    detail = "Monto manual";
                    break;
            }
        }

        // [5] Exemption split (RN-009.4, ratified §17.3: the SYSTEM controls the taxable excess).
        var final = spec.OverrideAmount ?? calculated;
        var (exempt, excess) = SplitExemption(concept, final, calculated, input.Parameters);

        return new SettlementLineResult(
            spec.LinePublicId, concept.Code, concept.Name, SettlementConceptClass.Ingreso,
            concept.IsSystemCalculated && !spec.IsManual, concept.SortOrder, spec.Description, spec.CounterpartyName,
            calculationBase is null ? null : Round2(calculationBase.Value), units, calculated,
            exempt, excess, spec.OverrideAmount, spec.IsIncluded, zeroByLaw, zeroReason, detail);
    }

    /// <summary>
    /// Splits an income's FINAL amount into its Renta-exempt portion and the taxable excess that joins the
    /// Renta base: fully taxable (Ninguna), exempt up to N × minimum wage (aguinaldo), or exempt up to the
    /// legally computed amount (indemnización/renuncia — an upward override becomes taxable).
    /// </summary>
    private static (decimal Exempt, decimal Excess) SplitExemption(
        SettlementConceptConfig concept,
        decimal finalAmount,
        decimal calculatedAmount,
        SettlementParametersInput parameters)
    {
        if (!concept.AffectsRenta)
        {
            return (Round2(finalAmount), 0m);
        }

        switch (concept.ExemptionRule)
        {
            case SettlementExemptionRule.HastaLimitePorMinimo:
                var limit = (concept.ExemptionMultiplier ?? parameters.AguinaldoExemptionMultiplier) * parameters.MinimumMonthlyWage;
                var exempt = Math.Min(finalAmount, limit);
                return (Round2(exempt), Round2(Math.Max(0m, finalAmount - exempt)));

            case SettlementExemptionRule.HastaMontoLegal:
                var legalExempt = Math.Min(finalAmount, calculatedAmount);
                return (Round2(legalExempt), Round2(Math.Max(0m, finalAmount - legalExempt)));

            default:
                return (0m, Round2(finalAmount));
        }
    }

    // ── Deductions (RF-009) ──────────────────────────────────────────────────────────────────

    private static SettlementLineResult ComputeDeductionLine(
        LineSpec spec,
        SettlementCalculationInput input,
        IReadOnlyDictionary<string, SettlementConceptConfig> concepts,
        decimal isssBase,
        decimal afpBase,
        decimal rentaBase,
        List<SettlementWarning> warnings)
    {
        var concept = ResolveConcept(concepts, spec.ConceptCode, SettlementConceptClass.Descuento);

        decimal calculated;
        decimal? calculationBase = null;
        string? detail;

        switch (spec.ConceptCode.ToUpperInvariant())
        {
            case SettlementConceptCodes.Isss:
                var isssCappedBase = CapBase(isssBase, input.Isss.ContributionCap);
                calculationBase = isssCappedBase;
                calculated = Round2(input.Isss.EmployeeRatePercent / 100m * isssCappedBase);
                detail = $"{input.Isss.EmployeeRatePercent:0.##}% × {isssCappedBase:0.00} (base afecta{CapNote(isssBase, input.Isss.ContributionCap)})";
                break;

            case SettlementConceptCodes.Afp:
                var afpCappedBase = CapBase(afpBase, input.Afp.ContributionCap);
                calculationBase = afpCappedBase;
                calculated = Round2(input.Afp.EmployeeRatePercent / 100m * afpCappedBase);
                detail = $"{input.Afp.EmployeeRatePercent:0.##}% × {afpCappedBase:0.00} (base afecta{CapNote(afpBase, input.Afp.ContributionCap)})";
                break;

            case SettlementConceptCodes.Renta:
                calculationBase = Round2(rentaBase);
                if (input.RentaBrackets.Count == 0)
                {
                    calculated = 0m;
                    detail = "Sin tramos de Renta vigentes configurados";
                    warnings.Add(new SettlementWarning(WarningRentaBracketsMissing, concept.Code));
                }
                else
                {
                    var bracket = input.RentaBrackets
                        .OrderBy(item => item.LowerBound)
                        .LastOrDefault(item => rentaBase >= item.LowerBound && (item.UpperBound is null || rentaBase <= item.UpperBound));
                    if (bracket is null)
                    {
                        calculated = 0m;
                        detail = $"Base gravable {rentaBase:0.00} bajo el primer tramo";
                    }
                    else
                    {
                        calculated = Round2(bracket.FixedFee + (bracket.RatePercent / 100m * (rentaBase - bracket.ExcessOver)));
                        detail = $"{bracket.FixedFee:0.00} + {bracket.RatePercent:0.##}% × ({rentaBase:0.00} − {bracket.ExcessOver:0.00}) — tabla vigente";
                    }
                }

                break;

            case SettlementConceptCodes.DescuentoEventualPendiente:
                // A one-off charge the employee still owes (REQ-009): a suggested manual amount, editable and
                // excludable. "Última cuota" would be a lie — there is no instalment plan behind it.
                calculated = Round2(spec.ManualAmount);
                detail = "Descuento eventual pendiente";
                break;

            case SettlementConceptCodes.DescuentoCiclicoPendiente:
                // The outstanding balance of a credit (REQ-008): a suggested manual amount, like the cases below,
                // but "última cuota" would be a lie — this is the WHOLE remaining balance (the capital still owed
                // when the credit bears interest, since paying early does not owe the future interest).
                calculated = Round2(spec.ManualAmount);
                detail = spec.CounterpartyName is null
                    ? "Saldo pendiente del descuento cíclico"
                    : $"Saldo pendiente — {spec.CounterpartyName}";
                break;

            default:
                // DESCUENTO_EXTERNO (última cuota sugerida) and OTRO_DESCUENTO (manual).
                calculated = Round2(spec.ManualAmount);
                detail = spec.CounterpartyName is null ? "Monto manual" : $"Última cuota — {spec.CounterpartyName}";
                break;
        }

        return new SettlementLineResult(
            spec.LinePublicId, concept.Code, concept.Name, SettlementConceptClass.Descuento,
            concept.IsSystemCalculated && !spec.IsManual, concept.SortOrder, spec.Description, spec.CounterpartyName,
            calculationBase, spec.UnitsOrDays, calculated, 0m, 0m,
            spec.OverrideAmount, spec.IsIncluded, IsZeroByLaw: false, ZeroReasonCode: null, detail);
    }

    // ── Employer charges (RF-010, D-13/P-02) ─────────────────────────────────────────────────

    private static SettlementLineResult ComputeEmployerLine(
        LineSpec spec,
        SettlementCalculationInput input,
        IReadOnlyDictionary<string, SettlementConceptConfig> concepts,
        decimal isssBase,
        decimal afpBase)
    {
        var concept = ResolveConcept(concepts, spec.ConceptCode, SettlementConceptClass.PagoPatronal);

        var (rate, baseAmount, cap) = spec.ConceptCode.ToUpperInvariant() switch
        {
            SettlementConceptCodes.IsssPatronal => (input.Isss.EmployerRatePercent, isssBase, input.Isss.ContributionCap),
            SettlementConceptCodes.AfpPatronal => (input.Afp.EmployerRatePercent, afpBase, input.Afp.ContributionCap),
            // INCAF (ex-INSAFORP): concept rate (1.00) over the ISSS base with the ISSS cap (P-02).
            _ => (concept.DefaultRatePercent ?? 0m, isssBase, input.Isss.ContributionCap),
        };

        var cappedBase = CapBase(baseAmount, cap);
        var calculated = Round2(rate / 100m * cappedBase);

        return new SettlementLineResult(
            spec.LinePublicId, concept.Code, concept.Name, SettlementConceptClass.PagoPatronal,
            IsSystemCalculated: true, concept.SortOrder, spec.Description, spec.CounterpartyName,
            cappedBase, spec.UnitsOrDays, calculated, 0m, 0m,
            spec.OverrideAmount, spec.IsIncluded, IsZeroByLaw: false, ZeroReasonCode: null,
            $"{rate:0.##}% × {cappedBase:0.00} (base afecta{CapNote(baseAmount, cap)})");
    }

    // ── Date helpers ─────────────────────────────────────────────────────────────────────────

    /// <summary>Days since the last plaza anniversary (vacation proportionality default — G-04, editable).</summary>
    public static int DaysSinceAnniversary(DateTime plazaStartDate, DateTime retirementDate)
    {
        var start = plazaStartDate.Date;
        var end = retirementDate.Date;
        if (end <= start)
        {
            return 0;
        }

        var anniversary = SafeDate(end.Year, start.Month, start.Day);
        if (anniversary > end)
        {
            anniversary = SafeDate(end.Year - 1, start.Month, start.Day);
        }

        return Math.Max(0, (end - anniversary).Days);
    }

    /// <summary>
    /// Days worked inside the aguinaldo period (Dec 12 → Dec 11, ratified §17.5) up to the retirement date,
    /// clipped to the plaza start when the employee joined mid-period.
    /// </summary>
    public static int DaysInAguinaldoPeriod(DateTime plazaStartDate, DateTime retirementDate)
    {
        var end = retirementDate.Date;
        var periodStart = new DateTime(end >= new DateTime(end.Year, 12, 12) ? end.Year : end.Year - 1, 12, 12);
        if (plazaStartDate.Date > periodStart)
        {
            periodStart = plazaStartDate.Date;
        }

        return Math.Max(0, (end - periodStart).Days);
    }

    private static DateTime SafeDate(int year, int month, int day) =>
        new(year, month, Math.Min(day, DateTime.DaysInMonth(year, month)));

    private static decimal CapBase(decimal baseAmount, decimal? cap) =>
        cap is null ? Round2(baseAmount) : Round2(Math.Min(baseAmount, cap.Value));

    private static string CapNote(decimal baseAmount, decimal? cap) =>
        cap is not null && baseAmount > cap.Value ? $", tope {cap.Value:0.00}" : string.Empty;

    private static SettlementConceptConfig ResolveConcept(
        IReadOnlyDictionary<string, SettlementConceptConfig> concepts,
        string conceptCode,
        SettlementConceptClass fallbackClass) =>
        concepts.TryGetValue(conceptCode, out var concept)
            ? concept
            : new SettlementConceptConfig(
                conceptCode.ToUpperInvariant(), conceptCode.ToUpperInvariant(), fallbackClass,
                AffectsIsss: false, AffectsAfp: false, AffectsRenta: false,
                SettlementExemptionRule.Ninguna, null, IsSystemCalculated: false, null, SortOrder: 999);
}

/// <summary>Canonical settlement concept codes (seed SV of `settlement-concepts`, D-07).</summary>
public static class SettlementConceptCodes
{
    public const string Salario = "SALARIO";
    public const string VacacionProporcional = "VACACION_PROPORCIONAL";
    public const string AguinaldoProporcional = "AGUINALDO_PROPORCIONAL";
    public const string Indemnizacion = "INDEMNIZACION";
    public const string RenunciaVoluntaria = "RENUNCIA_VOLUNTARIA";
    public const string BonoPendiente = "BONO_PENDIENTE";
    public const string ComisionPendiente = "COMISION_PENDIENTE";
    public const string HorasExtrasPendientes = "HORAS_EXTRAS_PENDIENTES";
    public const string HorasExtrasPendientesPago = "HORAS_EXTRAS_PENDIENTES_PAGO";
    public const string IngresoCiclicoPendiente = "INGRESO_CICLICO_PENDIENTE";
    public const string IngresoEventualPendiente = "INGRESO_EVENTUAL_PENDIENTE";
    public const string OtroIngreso = "OTRO_INGRESO";
    public const string Isss = "ISSS";
    public const string Afp = "AFP";
    public const string Renta = "RENTA";
    public const string DescuentoExterno = "DESCUENTO_EXTERNO";

    /// <summary>
    /// The outstanding balance of a recurring deduction ("descuento cíclico" — REQ-008): a DEDUCTION, so it MUST be
    /// listed in the <c>Descuento</c> arm of <c>ResolveClass</c> — that switch defaults to <c>Ingreso</c>, and a
    /// credit balance classified as income would PAY the employee their loan instead of discounting it.
    /// </summary>
    public const string DescuentoCiclicoPendiente = "DESCUENTO_CICLICO_PENDIENTE";

    /// <summary>
    /// The AUTORIZADO one-off deductions the employee still owes when settled ("descuentos eventuales" — REQ-009).
    /// Like <see cref="DescuentoCiclicoPendiente"/>, it MUST be listed in the <c>Descuento</c> arm of
    /// <c>ResolveClass</c>: that switch defaults to <c>Ingreso</c>, and REQ-008's fix only covered the CYCLIC
    /// concept — a one-off charge classified as income would PAY the employee their fine instead of charging it.
    /// </summary>
    public const string DescuentoEventualPendiente = "DESCUENTO_EVENTUAL_PENDIENTE";

    public const string OtroDescuento = "OTRO_DESCUENTO";
    public const string IsssPatronal = "ISSS_PATRONAL";
    public const string AfpPatronal = "AFP_PATRONAL";
    public const string Incaf = "INCAF";
}

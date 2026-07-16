using CLARIHR.Application.Features.PersonnelFiles.CompensatoryTime;
using CLARIHR.Domain.Payroll;

namespace CLARIHR.Application.Features.Payroll;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// REQ-012 §2 — the pure payroll engine. Deterministic, no clock, no database: everything arrives
// resolved in the input and the 14 signed golden cases of the A.3 (2026-07-14 + golden 14
// 2026-07-15) are the gate of this module (suite PayrollCalculationRulesTests). The arithmetic is
// FIXED by the sign-off: daily = monthly/30 (commercial month) · ordinary hour = daily/8 (legal
// 44-h week) · the period base uses COMMERCIAL days (month 30 · quincena ALWAYS 15 — «30/2, no
// más no menos» · week 7) · POR_OBRA has no base (fixed values arrive as incomes) · Renta uses
// the OFFICIAL table of the pay frequency (never derived arithmetically) · Round2 AwayFromZero
// exactly once per line. If the accountant ever disputes a figure: the CALCULATION is corrected,
// not the model (precedent: settlement, REQ-008 amortization, REQ-011 séptimo).
// ─────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>Concept codes the engine itself emits (pool/registro lines carry their SOURCE concept).</summary>
public static class PayrollEngineConceptCodes
{
    public const string Salario = "SALARIO";
    public const string HorasExtra = "HORAS_EXTRA";
    public const string Isss = "ISSS";
    public const string Afp = "AFP";
    public const string Renta = "RENTA";
    public const string IsssPatronal = "ISSS_PATRONAL";
    public const string AfpPatronal = "AFP_PATRONAL";
    public const string Incaf = "INCAF";
}

/// <summary>Source modules of a payroll-run line (§1.4 — the line→source traceability of REQ-013/014/015).</summary>
public static class PayrollSourceModules
{
    public const string Salario = "SALARIO";
    public const string RecurringIncome = "RECURRING_INCOME";
    public const string OneTimeIncome = "ONE_TIME_INCOME";
    public const string Overtime = "OVERTIME";
    public const string RecurringDeduction = "RECURRING_DEDUCTION";
    public const string OneTimeDeduction = "ONE_TIME_DEDUCTION";
    public const string NotWorkedTime = "NOT_WORKED_TIME";
    public const string Incapacity = "INCAPACITY";
    public const string Disciplinary = "DISCIPLINARY";
    public const string LeyIsss = "LEY_ISSS";
    public const string LeyAfp = "LEY_AFP";
    public const string LeyRenta = "LEY_RENTA";
    public const string PatronalIsss = "PATRONAL_ISSS";
    public const string PatronalAfp = "PATRONAL_AFP";
    public const string PatronalIncaf = "PATRONAL_INCAF";
}

/// <summary>Stable warning codes of the run (§5). Never invent figures: warn and move on.</summary>
public static class PayrollEngineWarningCodes
{
    public const string RentaBracketsMissing = "PAYROLL_WARNING_RENTA_BRACKETS_MISSING";
    public const string BaseUndefined = "PAYROLL_WARNING_BASE_UNDEFINED";
    public const string InstallmentDeferred = "PAYROLL_WARNING_INSTALLMENT_DEFERRED";
    public const string NoBaseSalary = "PAYROLL_WARNING_NO_BASE_SALARY";
    public const string CarryoverInput = "PAYROLL_WARNING_CARRYOVER_INPUT";
}

/// <summary>ISSS/AFP scheme (rates + MONTHLY contribution cap; the engine prorates the cap by frequency).</summary>
public sealed record PayrollContributionScheme(
    decimal EmployeeRatePercent,
    decimal EmployerRatePercent,
    decimal? MonthlyContributionCap);

/// <summary>One income-tax withholding bracket of the pay frequency's OFFICIAL table (golden 13).</summary>
public sealed record PayrollTaxBracket(
    decimal LowerBound,
    decimal? UpperBound,
    decimal FixedFee,
    decimal RatePercent,
    decimal ExcessOver);

/// <summary>
/// One eligible income item of a plaza (a recurring installment, a one-time income application — the
/// amount is the SOURCE module's, the engine never re-amortizes: golden 3). Affectation flags come from
/// the source concept's configuration (§2.2 afterword).
/// </summary>
public sealed record PayrollIncomeItem(
    string ConceptCode,
    string ConceptName,
    decimal Amount,
    string SourceModule,
    Guid? SourceReferencePublicId,
    bool AffectsIsss,
    bool AffectsAfp,
    bool AffectsRenta);

/// <summary>One AUTORIZADA overtime record of the plaza (hours × factor; valued at the plaza's hour).</summary>
public sealed record PayrollOvertimeItem(
    decimal Hours,
    decimal Factor,
    Guid? SourceReferencePublicId);

/// <summary>
/// One registro-sourced deduction of a plaza (pool installment/application, TNT, disciplinary,
/// incapacity discount — the amount is the SOURCE module's snapshot, golden 4/5/9/14).
/// <paramref name="IsDeferrable"/> marks VOLUNTARY pool installments (the only lines the minimum-income
/// guarantee may defer — the law is NEVER deferred, P-08); <paramref name="DeferralOrder"/> is the
/// registration sequence (higher = more recent = deferred first, LIFO).
/// <paramref name="IsCarryover"/> marks a lagged TNT/disciplinary input pulled from before the period
/// (REQ-014 P-03) — the line is included with the stable warning, excludable in review.
/// </summary>
public sealed record PayrollDeductionItem(
    string ConceptCode,
    string ConceptName,
    decimal Amount,
    string SourceModule,
    Guid? SourceReferencePublicId,
    bool IsDeferrable = false,
    int DeferralOrder = 0,
    bool IsCarryover = false);

/// <summary>An informative employer-side item (e.g. the incapacity's employer amount — golden 5).</summary>
public sealed record PayrollEmployerItem(
    string ConceptCode,
    string ConceptName,
    decimal Amount,
    string SourceModule,
    Guid? SourceReferencePublicId);

/// <summary>One plaza of an employee in the run (multi-plaza: one SALARIO line per plaza — golden 7).</summary>
public sealed record PayrollPlazaInput(
    Guid AssignedPositionPublicId,
    decimal MonthlyBaseSalary,
    IReadOnlyList<PayrollIncomeItem> Incomes,
    IReadOnlyList<PayrollOvertimeItem> OvertimeItems,
    IReadOnlyList<PayrollDeductionItem> Deductions,
    IReadOnlyList<PayrollEmployerItem> EmployerItems);

/// <summary>One employee of the run. The legal lines (ISSS/AFP/Renta) aggregate across the plazas.</summary>
public sealed record PayrollEmployeeInput(
    Guid PersonnelFilePublicId,
    IReadOnlyList<PayrollPlazaInput> Plazas,
    decimal? MinimumMonthlyWage = null);

/// <summary>Everything the engine needs, resolved (pure — the data provider of PR-5 assembles it).</summary>
public sealed record PayrollCalculationInput(
    string PayrollTypeCode,
    string PayPeriodCode,
    bool GuaranteesMinimumIncome,
    PayrollContributionScheme Isss,
    PayrollContributionScheme Afp,
    decimal IncafRatePercent,
    IReadOnlyList<PayrollTaxBracket> RentaBrackets,
    IReadOnlyList<PayrollEmployeeInput> Employees,
    decimal StandardDailyHours = 8m);

public sealed record PayrollCalculatedLine(
    Guid PersonnelFilePublicId,
    Guid? AssignedPositionPublicId,
    string ConceptCode,
    string ConceptName,
    string LineClass,
    decimal? Units,
    decimal? BaseAmount,
    decimal CalculatedAmount,
    bool IsIncluded,
    string SourceModule,
    Guid? SourceReferencePublicId,
    IReadOnlyList<string> WarningCodes,
    int SortOrder);

public sealed record PayrollRunWarningResult(string Code, Guid? PersonnelFilePublicId, string? Context);

public sealed record PayrollTotalsResult(
    int EmployeeCount,
    decimal TotalIncome,
    decimal TotalDeductions,
    decimal TotalEmployerCost,
    decimal TotalNet);

public sealed record PayrollCalculationResult(
    IReadOnlyList<PayrollCalculatedLine> Lines,
    PayrollTotalsResult Totals,
    IReadOnlyList<PayrollRunWarningResult> Warnings);

/// <summary>
/// The pure engine (§2.2): per employee×plaza — [1] SALARIO line (base §2.1) · [2] pool incomes (source
/// amounts) · [3] HORAS_EXTRA = Σ(hours×factor) × HourlyRate(daily, standard hours) reusing the certified
/// <see cref="CompensatoryTimeRules.HourlyRate"/> valuation (one rounding per line — golden 2) · [4]
/// registro deductions (source snapshots — goldens 3/4/5/9/14) · [5] employee law: ISSS/AFP (rate × capped
/// affected base, the MONTHLY cap prorated by commercial days) and Renta (the frequency's official table
/// over taxable incomes minus the employee's ISSS/AFP — SV withholding practice; no table → warning + 0,
/// golden 13) · [6] employer charges (informative, PagoPatronal) · [7] minimum-income guarantee (P-08:
/// defer VOLUNTARY installments LIFO until the prorated minimum is met; the law is never deferred) · [8]
/// net + totals.
/// </summary>
public static class PayrollCalculationRules
{
    public static PayrollCalculationResult Calculate(PayrollCalculationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var lines = new List<MutableLine>();
        var warnings = new List<PayrollRunWarningResult>();
        var sort = 0;

        foreach (var employee in input.Employees)
        {
            CalculateEmployee(input, employee, lines, warnings, ref sort);
        }

        var totalIncome = Round2(lines.Where(l => l is { LineClass: PayrollLineClasses.Ingreso, IsIncluded: true }).Sum(l => l.CalculatedAmount));
        var totalDeductions = Round2(lines.Where(l => l is { LineClass: PayrollLineClasses.Descuento, IsIncluded: true }).Sum(l => l.CalculatedAmount));
        var totalEmployer = Round2(lines.Where(l => l is { LineClass: PayrollLineClasses.PagoPatronal, IsIncluded: true }).Sum(l => l.CalculatedAmount));

        return new PayrollCalculationResult(
            lines.Select(l => l.ToResult()).ToArray(),
            new PayrollTotalsResult(
                input.Employees.Count,
                totalIncome,
                totalDeductions,
                totalEmployer,
                Round2(totalIncome - totalDeductions)),
            warnings);
    }

    /// <summary>Commercial days of the pay frequency (§2.1 — quincena ALWAYS 15, «30/2 no más no menos»).</summary>
    public static int CommercialDays(string payPeriodCode) => payPeriodCode switch
    {
        PayrollFrequencies.Quincenal => 15,
        PayrollFrequencies.Semanal => 7,
        _ => 30,
    };

    /// <summary>Prorates a MONTHLY reference (cap / minimum wage) to the frequency by commercial days.</summary>
    public static decimal ProrateMonthly(decimal monthlyAmount, string payPeriodCode) =>
        Round2(monthlyAmount * CommercialDays(payPeriodCode) / 30m);

    private static void CalculateEmployee(
        PayrollCalculationInput input,
        PayrollEmployeeInput employee,
        List<MutableLine> lines,
        List<PayrollRunWarningResult> warnings,
        ref int sort)
    {
        var employeeLines = new List<MutableLine>();
        var days = CommercialDays(input.PayPeriodCode);
        var isPorObra = input.PayrollTypeCode == "POR_OBRA";
        var isOtro = input.PayrollTypeCode == "OTRO";

        foreach (var plaza in employee.Plazas)
        {
            // [1] SALARIO — base of the period (§2.1). POR_OBRA has no base (golden 12: the pay is the sum
            // of the fixed per-obra values registered as incomes); OTRO has no defined base → warning.
            if (isOtro)
            {
                warnings.Add(new PayrollRunWarningResult(
                    PayrollEngineWarningCodes.BaseUndefined, employee.PersonnelFilePublicId, plaza.AssignedPositionPublicId.ToString()));
            }
            else if (!isPorObra)
            {
                if (plaza.MonthlyBaseSalary <= 0m)
                {
                    warnings.Add(new PayrollRunWarningResult(
                        PayrollEngineWarningCodes.NoBaseSalary, employee.PersonnelFilePublicId, plaza.AssignedPositionPublicId.ToString()));
                }
                else
                {
                    // daily = monthly/30 (unrounded intermediate); the ONLY rounding is on the line amount.
                    var baseAmount = Round2(plaza.MonthlyBaseSalary * days / 30m);
                    employeeLines.Add(new MutableLine(
                        employee.PersonnelFilePublicId, plaza.AssignedPositionPublicId,
                        PayrollEngineConceptCodes.Salario, "Salario del periodo", PayrollLineClasses.Ingreso,
                        Units: days, BaseAmount: plaza.MonthlyBaseSalary, CalculatedAmount: baseAmount,
                        SourceModule: PayrollSourceModules.Salario, SourceReferencePublicId: null,
                        AffectsIsss: true, AffectsAfp: true, AffectsRenta: true, SortOrder: ++sort));
                }
            }

            // [2] Pool incomes — one line per eligible installment/application, source amount (golden 3/12).
            foreach (var income in plaza.Incomes)
            {
                employeeLines.Add(new MutableLine(
                    employee.PersonnelFilePublicId, plaza.AssignedPositionPublicId,
                    income.ConceptCode, income.ConceptName, PayrollLineClasses.Ingreso,
                    Units: null, BaseAmount: null, CalculatedAmount: Round2(income.Amount),
                    income.SourceModule, income.SourceReferencePublicId,
                    income.AffectsIsss, income.AffectsAfp, income.AffectsRenta, SortOrder: ++sort));
            }

            // [3] HORAS_EXTRA — Σ(hours×factor) × HourlyRate(daily, standard) in ONE rounding (golden 2:
            // 2h30m×2.00 + 1h30m×2.50 = 8.75 h-factor; $600 → hour $2.50 → $21.88).
            if (plaza.OvertimeItems.Count > 0 && plaza.MonthlyBaseSalary > 0m)
            {
                var hourFactorSum = plaza.OvertimeItems.Sum(item => item.Hours * item.Factor);
                var hourlyRate = CompensatoryTimeRules.HourlyRate(plaza.MonthlyBaseSalary / 30m, input.StandardDailyHours);
                var amount = Round2(hourFactorSum * hourlyRate);
                employeeLines.Add(new MutableLine(
                    employee.PersonnelFilePublicId, plaza.AssignedPositionPublicId,
                    PayrollEngineConceptCodes.HorasExtra, "Horas extras", PayrollLineClasses.Ingreso,
                    Units: Round2(hourFactorSum), BaseAmount: hourlyRate, CalculatedAmount: amount,
                    PayrollSourceModules.Overtime,
                    plaza.OvertimeItems.Count == 1 ? plaza.OvertimeItems[0].SourceReferencePublicId : null,
                    AffectsIsss: true, AffectsAfp: true, AffectsRenta: true, SortOrder: ++sort));
            }

            // [4] Registro deductions — source snapshots (goldens 3/4/5/9/14; REQ-011 already resolved the
            // hour-mode permit: the engine only takes the DiscountAmount). Carryover inputs (REQ-014 P-03)
            // keep the stable per-line warning.
            foreach (var deduction in plaza.Deductions)
            {
                var lineWarnings = deduction.IsCarryover
                    ? new[] { PayrollEngineWarningCodes.CarryoverInput }
                    : Array.Empty<string>();
                employeeLines.Add(new MutableLine(
                    employee.PersonnelFilePublicId, plaza.AssignedPositionPublicId,
                    deduction.ConceptCode, deduction.ConceptName, PayrollLineClasses.Descuento,
                    Units: null, BaseAmount: null, CalculatedAmount: Round2(deduction.Amount),
                    deduction.SourceModule, deduction.SourceReferencePublicId,
                    AffectsIsss: false, AffectsAfp: false, AffectsRenta: false, SortOrder: ++sort)
                {
                    IsDeferrable = deduction.IsDeferrable,
                    DeferralOrder = deduction.DeferralOrder,
                    WarningCodes = lineWarnings,
                });
                if (deduction.IsCarryover)
                {
                    warnings.Add(new PayrollRunWarningResult(
                        PayrollEngineWarningCodes.CarryoverInput, employee.PersonnelFilePublicId, deduction.SourceReferencePublicId?.ToString()));
                }
            }

            // [6a] Informative employer items of the plaza (incapacity employer amount — golden 5).
            foreach (var employerItem in plaza.EmployerItems)
            {
                employeeLines.Add(new MutableLine(
                    employee.PersonnelFilePublicId, plaza.AssignedPositionPublicId,
                    employerItem.ConceptCode, employerItem.ConceptName, PayrollLineClasses.PagoPatronal,
                    Units: null, BaseAmount: null, CalculatedAmount: Round2(employerItem.Amount),
                    employerItem.SourceModule, employerItem.SourceReferencePublicId,
                    AffectsIsss: false, AffectsAfp: false, AffectsRenta: false, SortOrder: ++sort));
            }
        }

        // [5] Employee law over the INCLUDED income lines of ALL the plazas (multi-plaza aggregates — the
        // caps and the table apply once per employee per period).
        var isssBase = employeeLines.Where(l => l is { LineClass: PayrollLineClasses.Ingreso, IsIncluded: true, AffectsIsss: true }).Sum(l => l.CalculatedAmount);
        var afpBase = employeeLines.Where(l => l is { LineClass: PayrollLineClasses.Ingreso, IsIncluded: true, AffectsAfp: true }).Sum(l => l.CalculatedAmount);
        var rentaIncomeBase = employeeLines.Where(l => l is { LineClass: PayrollLineClasses.Ingreso, IsIncluded: true, AffectsRenta: true }).Sum(l => l.CalculatedAmount);

        var isssCap = input.Isss.MonthlyContributionCap is { } isssMonthlyCap
            ? ProrateMonthly(isssMonthlyCap, input.PayPeriodCode)
            : (decimal?)null;
        var afpCap = input.Afp.MonthlyContributionCap is { } afpMonthlyCap
            ? ProrateMonthly(afpMonthlyCap, input.PayPeriodCode)
            : (decimal?)null;

        var isssCappedBase = CapBase(isssBase, isssCap);
        var afpCappedBase = CapBase(afpBase, afpCap);
        var isssAmount = Round2(input.Isss.EmployeeRatePercent / 100m * isssCappedBase);
        var afpAmount = Round2(input.Afp.EmployeeRatePercent / 100m * afpCappedBase);

        if (isssAmount > 0m)
        {
            employeeLines.Add(new MutableLine(
                employee.PersonnelFilePublicId, null,
                PayrollEngineConceptCodes.Isss, "ISSS", PayrollLineClasses.Descuento,
                Units: null, BaseAmount: isssCappedBase, CalculatedAmount: isssAmount,
                PayrollSourceModules.LeyIsss, null, false, false, false, SortOrder: ++sort));
        }

        if (afpAmount > 0m)
        {
            employeeLines.Add(new MutableLine(
                employee.PersonnelFilePublicId, null,
                PayrollEngineConceptCodes.Afp, "AFP", PayrollLineClasses.Descuento,
                Units: null, BaseAmount: afpCappedBase, CalculatedAmount: afpAmount,
                PayrollSourceModules.LeyAfp, null, false, false, false, SortOrder: ++sort));
        }

        if (rentaIncomeBase > 0m)
        {
            // SV payroll withholding practice: the taxable base is the affected income minus the employee's
            // own social-security contributions of the period; the frequency's OFFICIAL table applies
            // (golden 13 — no table: warning + 0, never a derived figure).
            var rentaBase = Math.Max(0m, rentaIncomeBase - isssAmount - afpAmount);
            decimal rentaAmount;
            if (input.RentaBrackets.Count == 0)
            {
                rentaAmount = 0m;
                warnings.Add(new PayrollRunWarningResult(
                    PayrollEngineWarningCodes.RentaBracketsMissing, employee.PersonnelFilePublicId, input.PayPeriodCode));
            }
            else
            {
                var bracket = input.RentaBrackets
                    .OrderBy(item => item.LowerBound)
                    .LastOrDefault(item => rentaBase >= item.LowerBound && (item.UpperBound is null || rentaBase <= item.UpperBound));
                rentaAmount = bracket is null
                    ? 0m
                    : Round2(bracket.FixedFee + (bracket.RatePercent / 100m * (rentaBase - bracket.ExcessOver)));
            }

            if (rentaAmount > 0m)
            {
                employeeLines.Add(new MutableLine(
                    employee.PersonnelFilePublicId, null,
                    PayrollEngineConceptCodes.Renta, "Renta", PayrollLineClasses.Descuento,
                    Units: null, BaseAmount: Round2(rentaBase), CalculatedAmount: rentaAmount,
                    PayrollSourceModules.LeyRenta, null, false, false, false, SortOrder: ++sort));
            }
        }

        // [6] Employer charges over the same capped bases (informative — never touch the net; zero-rate
        // configurations emit no line).
        var isssPatronal = Round2(input.Isss.EmployerRatePercent / 100m * isssCappedBase);
        if (isssPatronal > 0m)
        {
            employeeLines.Add(new MutableLine(
                employee.PersonnelFilePublicId, null,
                PayrollEngineConceptCodes.IsssPatronal, "ISSS patronal", PayrollLineClasses.PagoPatronal,
                Units: null, BaseAmount: isssCappedBase, CalculatedAmount: isssPatronal,
                PayrollSourceModules.PatronalIsss, null, false, false, false, SortOrder: ++sort));
        }

        var incaf = Round2(input.IncafRatePercent / 100m * isssCappedBase);
        if (incaf > 0m)
        {
            employeeLines.Add(new MutableLine(
                employee.PersonnelFilePublicId, null,
                PayrollEngineConceptCodes.Incaf, "INCAF", PayrollLineClasses.PagoPatronal,
                Units: null, BaseAmount: isssCappedBase, CalculatedAmount: incaf,
                PayrollSourceModules.PatronalIncaf, null, false, false, false, SortOrder: ++sort));
        }

        var afpPatronal = Round2(input.Afp.EmployerRatePercent / 100m * afpCappedBase);
        if (afpPatronal > 0m)
        {
            employeeLines.Add(new MutableLine(
                employee.PersonnelFilePublicId, null,
                PayrollEngineConceptCodes.AfpPatronal, "AFP patronal", PayrollLineClasses.PagoPatronal,
                Units: null, BaseAmount: afpCappedBase, CalculatedAmount: afpPatronal,
                PayrollSourceModules.PatronalAfp, null, false, false, false, SortOrder: ++sort));
        }

        // [7] Minimum-income guarantee (P-08, golden 6): while the net falls under the prorated minimum,
        // defer VOLUNTARY pool installments LIFO (most recent first); the law is NEVER deferred. Each
        // deferral flips the line to IsIncluded=false — PR-5 only applies INCLUDED lines to the pools, so
        // the installment stays pending in its module (§3.5).
        if (input.GuaranteesMinimumIncome && employee.MinimumMonthlyWage is > 0m)
        {
            var minimum = ProrateMonthly(employee.MinimumMonthlyWage.Value, input.PayPeriodCode);
            var deferrables = employeeLines
                .Where(l => l is { LineClass: PayrollLineClasses.Descuento, IsIncluded: true, IsDeferrable: true })
                .OrderByDescending(l => l.DeferralOrder)
                .ToList();

            foreach (var candidate in deferrables)
            {
                if (NetOf(employeeLines) >= minimum)
                {
                    break;
                }

                candidate.IsIncluded = false;
                candidate.WarningCodes = candidate.WarningCodes
                    .Append(PayrollEngineWarningCodes.InstallmentDeferred)
                    .ToArray();
                warnings.Add(new PayrollRunWarningResult(
                    PayrollEngineWarningCodes.InstallmentDeferred, employee.PersonnelFilePublicId, candidate.ConceptCode));
            }
        }

        lines.AddRange(employeeLines);
    }

    private static decimal NetOf(List<MutableLine> employeeLines) =>
        employeeLines.Where(l => l is { LineClass: PayrollLineClasses.Ingreso, IsIncluded: true }).Sum(l => l.CalculatedAmount) -
        employeeLines.Where(l => l is { LineClass: PayrollLineClasses.Descuento, IsIncluded: true }).Sum(l => l.CalculatedAmount);

    private static decimal CapBase(decimal baseAmount, decimal? cap) =>
        cap is { } value && baseAmount > value ? value : baseAmount;

    private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    private sealed record MutableLine(
        Guid PersonnelFilePublicId,
        Guid? AssignedPositionPublicId,
        string ConceptCode,
        string ConceptName,
        string LineClass,
        decimal? Units,
        decimal? BaseAmount,
        decimal CalculatedAmount,
        string SourceModule,
        Guid? SourceReferencePublicId,
        bool AffectsIsss,
        bool AffectsAfp,
        bool AffectsRenta,
        int SortOrder)
    {
        public bool IsIncluded { get; set; } = true;

        public bool IsDeferrable { get; init; }

        public int DeferralOrder { get; init; }

        public IReadOnlyList<string> WarningCodes { get; set; } = Array.Empty<string>();

        public PayrollCalculatedLine ToResult() => new(
            PersonnelFilePublicId,
            AssignedPositionPublicId,
            ConceptCode,
            ConceptName,
            LineClass,
            Units,
            BaseAmount,
            CalculatedAmount,
            IsIncluded,
            SourceModule,
            SourceReferencePublicId,
            WarningCodes,
            SortOrder);
    }
}

using CLARIHR.Application.Features.Payroll;
using CLARIHR.Domain.Payroll;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// REQ-012 §2.3 — THE GOLDEN GATE of the payroll engine. The 14 cases of the signed A.3 (13 signed
/// 2026-07-14 by the business/accountant + golden 14 added 2026-07-15) are codified LITERALLY: the fixed
/// arithmetic is daily = monthly/30 (commercial month) · ordinary hour = daily/8 (legal 44-h week) ·
/// period base in COMMERCIAL days (month 30 · quincena ALWAYS 15 «30/2, no más no menos» · week 7) ·
/// POR_OBRA = fixed values via incomes · Renta by the OFFICIAL table of the frequency (never derived) ·
/// minimum income $408.80/month (quincena $204.40 · week $95.39). If the accountant ever disputes a
/// figure in deployment: the CALCULATION is corrected, not the model.
/// </summary>
public sealed class PayrollCalculationRulesTests
{
    private static readonly Guid Employee1 = Guid.Parse("00000000-0000-0000-0000-0000000000e1");
    private static readonly Guid Employee2 = Guid.Parse("00000000-0000-0000-0000-0000000000e2");
    private static readonly Guid Plaza1 = Guid.Parse("00000000-0000-0000-0000-0000000000a1");
    private static readonly Guid Plaza2 = Guid.Parse("00000000-0000-0000-0000-0000000000a2");

    // SV scheme used across the golden cases: ISSS 3% employee / 7.5% employer, monthly cap $1,000;
    // AFP 7.25% / 8.75%, no cap; INCAF 1% over the ISSS base.
    private static readonly PayrollContributionScheme Isss = new(3.00m, 7.50m, 1000.00m);
    private static readonly PayrollContributionScheme Afp = new(7.25m, 8.75m, null);
    private static readonly PayrollContributionScheme NoScheme = new(0m, 0m, null);

    // Official SV withholding tables (DL 95/2015 — the same figures the DevSeed loads).
    private static IReadOnlyList<PayrollTaxBracket> QuincenalBrackets =>
    [
        new(0.01m, 236.00m, 0.00m, 0.00m, 0.00m),
        new(236.01m, 447.62m, 8.83m, 10.00m, 236.00m),
        new(447.63m, 1019.05m, 30.00m, 20.00m, 447.62m),
        new(1019.06m, null, 144.28m, 30.00m, 1019.05m),
    ];

    private static IReadOnlyList<PayrollTaxBracket> SemanalBrackets =>
    [
        new(0.01m, 118.00m, 0.00m, 0.00m, 0.00m),
        new(118.01m, 223.81m, 4.42m, 10.00m, 118.00m),
        new(223.82m, 509.52m, 15.00m, 20.00m, 223.81m),
        new(509.53m, null, 72.14m, 30.00m, 509.52m),
    ];

    private static PayrollCalculationInput Input(
        IReadOnlyList<PayrollEmployeeInput> employees,
        string payrollTypeCode = "QUINCENAL",
        string payPeriodCode = PayrollFrequencies.Quincenal,
        PayrollContributionScheme? isss = null,
        PayrollContributionScheme? afp = null,
        decimal incafRatePercent = 0m,
        IReadOnlyList<PayrollTaxBracket>? rentaBrackets = null,
        bool guaranteesMinimumIncome = false) =>
        new(
            payrollTypeCode,
            payPeriodCode,
            guaranteesMinimumIncome,
            isss ?? NoScheme,
            afp ?? NoScheme,
            incafRatePercent,
            rentaBrackets ?? [],
            employees);

    private static PayrollEmployeeInput Employee(
        decimal monthlySalary,
        Guid? employeeId = null,
        IReadOnlyList<PayrollIncomeItem>? incomes = null,
        IReadOnlyList<PayrollOvertimeItem>? overtime = null,
        IReadOnlyList<PayrollDeductionItem>? deductions = null,
        IReadOnlyList<PayrollEmployerItem>? employerItems = null,
        decimal? minimumMonthlyWage = null) =>
        new(
            employeeId ?? Employee1,
            [
                new PayrollPlazaInput(
                    Plaza1,
                    monthlySalary,
                    incomes ?? [],
                    overtime ?? [],
                    deductions ?? [],
                    employerItems ?? []),
            ],
            minimumMonthlyWage);

    private static PayrollCalculatedLine Single(PayrollCalculationResult result, string conceptCode) =>
        Assert.Single(result.Lines, line => line.ConceptCode == conceptCode);

    // ── Golden 1 — quincena base: $600/month, QUINCENAL ⇒ base 300.00 = mensual/2, ISSS 9.00,
    //    AFP 21.75, Renta 12.16 (tabla quincenal sobre 269.25), neto 257.09 ─────────────────────
    [Fact]
    public void Golden01_QuincenaBase()
    {
        var result = PayrollCalculationRules.Calculate(Input(
            [Employee(600m)],
            isss: Isss,
            afp: Afp,
            incafRatePercent: 1.00m,
            rentaBrackets: QuincenalBrackets));

        Assert.Equal(300.00m, Single(result, PayrollEngineConceptCodes.Salario).CalculatedAmount);
        Assert.Equal(9.00m, Single(result, PayrollEngineConceptCodes.Isss).CalculatedAmount);
        Assert.Equal(21.75m, Single(result, PayrollEngineConceptCodes.Afp).CalculatedAmount);
        var renta = Single(result, PayrollEngineConceptCodes.Renta);
        Assert.Equal(269.25m, renta.BaseAmount);
        Assert.Equal(12.16m, renta.CalculatedAmount);
        Assert.Equal(22.50m, Single(result, PayrollEngineConceptCodes.IsssPatronal).CalculatedAmount);
        Assert.Equal(26.25m, Single(result, PayrollEngineConceptCodes.AfpPatronal).CalculatedAmount);
        Assert.Equal(3.00m, Single(result, PayrollEngineConceptCodes.Incaf).CalculatedAmount);
        Assert.Equal(300.00m, result.Totals.TotalIncome);
        Assert.Equal(42.91m, result.Totals.TotalDeductions);
        Assert.Equal(257.09m, result.Totals.TotalNet);
        Assert.Equal(51.75m, result.Totals.TotalEmployerCost);
    }

    // ── Golden 2 — HE valoradas: 2h30m ×2.00 + 1h30m ×2.50 = 8.75 h-factor; $600 ⇒ hora $2.50 ⇒
    //    $21.88 en UNA línea con UN solo redondeo ─────────────────────────────────────────────────
    [Fact]
    public void Golden02_OvertimeValuation()
    {
        var result = PayrollCalculationRules.Calculate(Input(
            [
                Employee(
                    600m,
                    overtime:
                    [
                        new PayrollOvertimeItem(2.5m, 2.00m, null),
                        new PayrollOvertimeItem(1.5m, 2.50m, null),
                    ]),
            ]));

        var line = Single(result, PayrollEngineConceptCodes.HorasExtra);
        Assert.Equal(21.88m, line.CalculatedAmount);
        Assert.Equal(8.75m, line.Units);
        Assert.Equal(2.50m, line.BaseAmount);
        Assert.Equal(PayrollSourceModules.Overtime, line.SourceModule);
    }

    // ── Golden 3 — cuota con interés compuesto (REQ-008): el motor NO re-amortiza, toma el monto
    //    de la cuota tal cual ──────────────────────────────────────────────────────────────────────
    [Fact]
    public void Golden03_RecurringDeductionInstallmentIsTakenAsIs()
    {
        var installmentRef = Guid.NewGuid();
        var result = PayrollCalculationRules.Calculate(Input(
            [
                Employee(
                    600m,
                    deductions:
                    [
                        new PayrollDeductionItem(
                            "PRESTAMO_BANCARIO", "Préstamo bancario", 45.67m,
                            PayrollSourceModules.RecurringDeduction, installmentRef),
                    ]),
            ]));

        var line = Single(result, "PRESTAMO_BANCARIO");
        Assert.Equal(45.67m, line.CalculatedAmount);
        Assert.Equal(PayrollLineClasses.Descuento, line.LineClass);
        Assert.Equal(installmentRef, line.SourceReferencePublicId);
    }

    // ── Golden 4 — séptimo: ausencia lunes-viernes sin goce descuenta SEIS días, no cinco. El
    //    monto YA viene resuelto por REQ-011 (6 × $20 = $120 con $600/mes); neto quincenal 180.00 ──
    [Fact]
    public void Golden04_SeventhDayDiscountFlowsFromNotWorkedTime()
    {
        var result = PayrollCalculationRules.Calculate(Input(
            [
                Employee(
                    600m,
                    deductions:
                    [
                        new PayrollDeductionItem(
                            "AUSENCIA_SIN_GOCE", "Ausencia sin goce de sueldo", 120.00m,
                            PayrollSourceModules.NotWorkedTime, Guid.NewGuid()),
                    ]),
            ]));

        Assert.Equal(120.00m, Single(result, "AUSENCIA_SIN_GOCE").CalculatedAmount);
        Assert.Equal(180.00m, result.Totals.TotalNet);
    }

    // ── Golden 5 — incapacidad con tramos: descuento al empleado (snapshot REQ-001) MÁS el aporte
    //    patronal como línea informativa (no toca el neto) ────────────────────────────────────────
    [Fact]
    public void Golden05_IncapacitySnapshotDiscountPlusEmployerAmount()
    {
        var incapacityRef = Guid.NewGuid();
        var result = PayrollCalculationRules.Calculate(Input(
            [
                Employee(
                    600m,
                    deductions:
                    [
                        new PayrollDeductionItem(
                            "INCAPACIDAD", "Incapacidad", 50.00m,
                            PayrollSourceModules.Incapacity, incapacityRef),
                    ],
                    employerItems:
                    [
                        new PayrollEmployerItem(
                            "INCAPACIDAD_PATRONAL", "Aporte patronal de incapacidad", 30.00m,
                            PayrollSourceModules.Incapacity, incapacityRef),
                    ]),
            ]));

        Assert.Equal(50.00m, Single(result, "INCAPACIDAD").CalculatedAmount);
        var employer = Single(result, "INCAPACIDAD_PATRONAL");
        Assert.Equal(PayrollLineClasses.PagoPatronal, employer.LineClass);
        Assert.Equal(30.00m, employer.CalculatedAmount);
        Assert.Equal(250.00m, result.Totals.TotalNet);
        Assert.Equal(30.00m, result.Totals.TotalEmployerCost);
    }

    // ── Golden 6 — mínimo garantizado $408.80 (ley): quincena $204.40 · semana $95.39; las cuotas
    //    VOLUNTARIAS se difieren LIFO, la ley y los descuentos de registro NUNCA ────────────────────
    [Fact]
    public void Golden06_MinimumIncomeGuaranteeDefersVoluntaryInstallmentsOnly()
    {
        Assert.Equal(204.40m, PayrollCalculationRules.ProrateMonthly(408.80m, PayrollFrequencies.Quincenal));
        Assert.Equal(95.39m, PayrollCalculationRules.ProrateMonthly(408.80m, PayrollFrequencies.Semanal));

        var result = PayrollCalculationRules.Calculate(Input(
            [
                Employee(
                    408.80m,
                    deductions:
                    [
                        new PayrollDeductionItem(
                            "AUSENCIA_SIN_GOCE", "Ausencia sin goce", 20.00m,
                            PayrollSourceModules.NotWorkedTime, Guid.NewGuid()),
                        new PayrollDeductionItem(
                            "PRESTAMO_BANCARIO", "Préstamo bancario", 50.00m,
                            PayrollSourceModules.RecurringDeduction, Guid.NewGuid(),
                            IsDeferrable: true, DeferralOrder: 1),
                    ],
                    minimumMonthlyWage: 408.80m),
            ],
            guaranteesMinimumIncome: true));

        var deferred = Single(result, "PRESTAMO_BANCARIO");
        Assert.False(deferred.IsIncluded);
        Assert.Contains(PayrollEngineWarningCodes.InstallmentDeferred, deferred.WarningCodes);
        Assert.True(Single(result, "AUSENCIA_SIN_GOCE").IsIncluded);
        Assert.Equal(184.40m, result.Totals.TotalNet);
        Assert.Contains(result.Warnings, warning => warning.Code == PayrollEngineWarningCodes.InstallmentDeferred);
    }

    // ── Golden 7 — multi-plaza: una línea SALARIO por plaza; la ley se calcula UNA vez por
    //    empleado sobre la base agregada de sus plazas ───────────────────────────────────────────
    [Fact]
    public void Golden07_MultiPlazaOneSalaryLinePerPlazaSingleLawLine()
    {
        var employee = new PayrollEmployeeInput(
            Employee1,
            [
                new PayrollPlazaInput(Plaza1, 600m, [], [], [], []),
                new PayrollPlazaInput(Plaza2, 300m, [], [], [], []),
            ]);

        var result = PayrollCalculationRules.Calculate(Input([employee], isss: Isss));

        var salaryLines = result.Lines.Where(line => line.ConceptCode == PayrollEngineConceptCodes.Salario).ToArray();
        Assert.Equal(2, salaryLines.Length);
        Assert.Contains(salaryLines, line => line is { AssignedPositionPublicId: not null, CalculatedAmount: 300.00m });
        Assert.Contains(salaryLines, line => line is { AssignedPositionPublicId: not null, CalculatedAmount: 150.00m });

        var isss = Single(result, PayrollEngineConceptCodes.Isss);
        Assert.Equal(450.00m, isss.BaseAmount);
        Assert.Equal(13.50m, isss.CalculatedAmount);
    }

    // ── Golden 8 — retiro a media quincena: el empleado queda FUERA de la población (el finiquito
    //    paga; la exclusión vive en el data provider — PR-5 la prueba e2e). Sin población, sin líneas ─
    [Fact]
    public void Golden08_ExcludedEmployeeProducesNothing()
    {
        var result = PayrollCalculationRules.Calculate(Input([]));

        Assert.Empty(result.Lines);
        Assert.Equal(0, result.Totals.EmployeeCount);
        Assert.Equal(0.00m, result.Totals.TotalNet);
    }

    // ── Golden 9 — amonestación aplicada con concepto de egreso: línea de descuento con snapshot ──
    [Fact]
    public void Golden09_DisciplinaryDeductionWithConceptSnapshot()
    {
        var result = PayrollCalculationRules.Calculate(Input(
            [
                Employee(
                    600m,
                    deductions:
                    [
                        new PayrollDeductionItem(
                            "MULTA_DISCIPLINARIA", "Multa disciplinaria", 25.00m,
                            PayrollSourceModules.Disciplinary, Guid.NewGuid()),
                    ]),
            ]));

        var line = Single(result, "MULTA_DISCIPLINARIA");
        Assert.Equal(PayrollLineClasses.Descuento, line.LineClass);
        Assert.Equal(25.00m, line.CalculatedAmount);
        Assert.Equal(PayrollSourceModules.Disciplinary, line.SourceModule);
    }

    // ── Golden 10 — cuadre: los totales persistidos son EXACTAMENTE la suma de las líneas
    //    incluidas por clase (el cuadre contra los insumos reales por los 5 pools es de PR-5) ─────
    [Fact]
    public void Golden10_TotalsSquareWithIncludedLines()
    {
        var result = PayrollCalculationRules.Calculate(Input(
            [
                Employee(600m, incomes:
                [
                    new PayrollIncomeItem("BONO", "Bono", 75.50m, PayrollSourceModules.OneTimeIncome, Guid.NewGuid(), true, true, true),
                ]),
                new PayrollEmployeeInput(Employee2, [new PayrollPlazaInput(Plaza2, 300m, [], [], [], [])]),
            ],
            isss: Isss,
            afp: Afp,
            incafRatePercent: 1.00m,
            rentaBrackets: QuincenalBrackets));

        Assert.Equal(
            result.Lines.Where(line => line is { LineClass: PayrollLineClasses.Ingreso, IsIncluded: true }).Sum(line => line.CalculatedAmount),
            result.Totals.TotalIncome);
        Assert.Equal(
            result.Lines.Where(line => line is { LineClass: PayrollLineClasses.Descuento, IsIncluded: true }).Sum(line => line.CalculatedAmount),
            result.Totals.TotalDeductions);
        Assert.Equal(
            result.Lines.Where(line => line is { LineClass: PayrollLineClasses.PagoPatronal, IsIncluded: true }).Sum(line => line.CalculatedAmount),
            result.Totals.TotalEmployerCost);
        Assert.Equal(result.Totals.TotalIncome - result.Totals.TotalDeductions, result.Totals.TotalNet);
        Assert.Equal(2, result.Totals.EmployeeCount);
    }

    // ── Golden 11 — POR_DIA: la base usa días COMERCIALES (quincena SIEMPRE 15 — el ejemplo de la
    //    quincena de 16 días quedó SIN efecto); diaria = mensual/30 = $20, hora = diaria/8 = $2.50 ──
    [Fact]
    public void Golden11_PorDiaUsesCommercialDays()
    {
        var result = PayrollCalculationRules.Calculate(Input(
            [Employee(600m)],
            payrollTypeCode: "POR_DIA"));

        var salary = Single(result, PayrollEngineConceptCodes.Salario);
        Assert.Equal(300.00m, salary.CalculatedAmount);
        Assert.Equal(15m, salary.Units);
        Assert.Equal(
            2.50m,
            CLARIHR.Application.Features.PersonnelFiles.CompensatoryTime.CompensatoryTimeRules.HourlyRate(600m / 30m, 8m));
    }

    // ── Golden 12 — POR_OBRA: sin salario base; el pago del periodo = valores fijos registrados
    //    como ingresos, con la ley aplicada sobre ellos ──────────────────────────────────────────
    [Fact]
    public void Golden12_PorObraFixedValuesViaIncomes()
    {
        var result = PayrollCalculationRules.Calculate(Input(
            [
                Employee(0m, incomes:
                [
                    new PayrollIncomeItem("OBRA", "Pago por obra", 500.00m, PayrollSourceModules.OneTimeIncome, Guid.NewGuid(), true, true, true),
                ]),
            ],
            payrollTypeCode: "POR_OBRA",
            isss: Isss,
            afp: Afp,
            rentaBrackets: QuincenalBrackets));

        Assert.DoesNotContain(result.Lines, line => line.ConceptCode == PayrollEngineConceptCodes.Salario);
        Assert.DoesNotContain(result.Warnings, warning => warning.Code == PayrollEngineWarningCodes.NoBaseSalary);
        Assert.Equal(500.00m, Single(result, "OBRA").CalculatedAmount);
        Assert.Equal(15.00m, Single(result, PayrollEngineConceptCodes.Isss).CalculatedAmount);
        Assert.Equal(36.25m, Single(result, PayrollEngineConceptCodes.Afp).CalculatedAmount);
        Assert.Equal(30.23m, Single(result, PayrollEngineConceptCodes.Renta).CalculatedAmount);
        Assert.Equal(418.52m, result.Totals.TotalNet);
    }

    // ── Golden 13 — Renta por frecuencia: la frecuencia ELIGE la tabla oficial; sin tabla →
    //    warning + retención 0 (nunca una derivación inventada) ─────────────────────────────────
    [Fact]
    public void Golden13_RentaTablePerFrequency()
    {
        // (a) SEMANAL: $600/mes → base 140.00 = mensual×7/30; tabla semanal tramo II →
        //     4.42 + 10% × (140.00 − 118.00) = 6.62.
        var weekly = PayrollCalculationRules.Calculate(Input(
            [Employee(600m)],
            payrollTypeCode: "SEMANAL",
            payPeriodCode: PayrollFrequencies.Semanal,
            rentaBrackets: SemanalBrackets));
        Assert.Equal(140.00m, Single(weekly, PayrollEngineConceptCodes.Salario).CalculatedAmount);
        Assert.Equal(6.62m, Single(weekly, PayrollEngineConceptCodes.Renta).CalculatedAmount);

        // (b) Sin tramos configurados → warning estable + SIN línea RENTA.
        var missing = PayrollCalculationRules.Calculate(Input([Employee(600m)]));
        Assert.DoesNotContain(missing.Lines, line => line.ConceptCode == PayrollEngineConceptCodes.Renta);
        Assert.Contains(missing.Warnings, warning => warning.Code == PayrollEngineWarningCodes.RentaBracketsMissing);
    }

    // ── Golden 14 (ejemplo literal del negocio, 2026-07-15) — $1,000/mes quincenal, permiso
    //    personal de 4 horas: base $500.00 − $16.67 (4 h = 0.50 días × $33.33) = $483.33 ≡ 14.5 días ─
    [Fact]
    public void Golden14_FourHourPermitPaysFourteenAndAHalfDays()
    {
        var result = PayrollCalculationRules.Calculate(Input(
            [
                Employee(
                    1000m,
                    deductions:
                    [
                        new PayrollDeductionItem(
                            "PERMISO_PERSONAL", "Permiso personal sin goce", 16.67m,
                            PayrollSourceModules.NotWorkedTime, Guid.NewGuid()),
                    ]),
            ]));

        Assert.Equal(500.00m, Single(result, PayrollEngineConceptCodes.Salario).CalculatedAmount);
        Assert.Equal(16.67m, Single(result, "PERMISO_PERSONAL").CalculatedAmount);
        Assert.Equal(483.33m, result.Totals.TotalNet);
    }

    // ── Invariantes complementarias del §2 ──────────────────────────────────────────────────────

    [Fact]
    public void OtroTypeEmitsBaseUndefinedWarningAndNoSalaryLine()
    {
        var result = PayrollCalculationRules.Calculate(Input([Employee(600m)], payrollTypeCode: "OTRO"));

        Assert.DoesNotContain(result.Lines, line => line.ConceptCode == PayrollEngineConceptCodes.Salario);
        Assert.Contains(result.Warnings, warning => warning.Code == PayrollEngineWarningCodes.BaseUndefined);
    }

    [Fact]
    public void MissingBaseSalaryOnSalariedTypeWarnsAndSkipsTheLine()
    {
        var result = PayrollCalculationRules.Calculate(Input([Employee(0m)]));

        Assert.Empty(result.Lines);
        Assert.Contains(result.Warnings, warning => warning.Code == PayrollEngineWarningCodes.NoBaseSalary);
    }

    [Fact]
    public void CarryoverDeductionCarriesTheStableWarning()
    {
        var lagged = Guid.NewGuid();
        var result = PayrollCalculationRules.Calculate(Input(
            [
                Employee(
                    600m,
                    deductions:
                    [
                        new PayrollDeductionItem(
                            "AUSENCIA_SIN_GOCE", "Ausencia sin goce", 20.00m,
                            PayrollSourceModules.NotWorkedTime, lagged, IsCarryover: true),
                    ]),
            ]));

        var line = Single(result, "AUSENCIA_SIN_GOCE");
        Assert.True(line.IsIncluded);
        Assert.Contains(PayrollEngineWarningCodes.CarryoverInput, line.WarningCodes);
        Assert.Contains(
            result.Warnings,
            warning => warning.Code == PayrollEngineWarningCodes.CarryoverInput && warning.Context == lagged.ToString());
    }

    [Fact]
    public void IsssCapProratesByFrequency()
    {
        // $2,400/month QUINCENAL: base 1,200 > prorated cap 500 → ISSS = 3% × 500 = 15.00.
        var result = PayrollCalculationRules.Calculate(Input([Employee(2400m)], isss: Isss));

        var isss = Single(result, PayrollEngineConceptCodes.Isss);
        Assert.Equal(500.00m, isss.BaseAmount);
        Assert.Equal(15.00m, isss.CalculatedAmount);
    }
}

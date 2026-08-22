using CLARIHR.Application.Features.Payroll;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// El motor del aguinaldo, concepto por concepto. Los números NO se eligen: salen de la ley (Art. 198, tramos
/// 15/19/21) y de la aritmética fijada en REQ-012 (diaria = mensual/30, divisor anual 365), así que cada
/// aserción es comprobable a mano con lápiz.
/// </summary>
public sealed class AguinaldoRulesTests
{
    /// <summary>Un año completo paga el tramo entero: la proporción es 365/365 y desaparece de la fórmula.</summary>
    [Fact]
    public void FullYear_PaysTheWholeTier()
    {
        var result = AguinaldoRules.Calculate(new AguinaldoCalculationInput(
            HireDate: new DateOnly(2020, 1, 1), Year: 2026, MonthlyBaseSalary: 600m, ExemptAmount: null));

        Assert.Equal(365, result.AccruedDays);
        Assert.Equal(6, result.SeniorityYears);
        Assert.Equal(19m, result.TierDays);          // 3–10 años
        Assert.Equal(380m, result.Amount);           // (600/30) × 19 = 20 × 19
    }

    /// <summary>
    /// El caso 2 del requerimiento: ingreso en agosto de 2026 → proporcional. Del 1 de agosto al 12 de
    /// diciembre hay 133 días (30+30+31+30+12), y con menos de un año el tramo es de 15 días.
    /// </summary>
    [Fact]
    public void MidYearHire_AccruesOnlyFromTheHireDate()
    {
        var result = AguinaldoRules.Calculate(new AguinaldoCalculationInput(
            HireDate: new DateOnly(2026, 8, 1), Year: 2026, MonthlyBaseSalary: 600m, ExemptAmount: null));

        Assert.Equal(133, result.AccruedDays);
        Assert.Equal(0, result.SeniorityYears);
        Assert.Equal(15m, result.TierDays);
        // 20 × 15 × 133/365 = 300 × 133/365 = 109.3150…
        Assert.Equal(109.32m, result.Amount);
    }

    /// <summary>Los tres tramos del Art. 198, en sus fronteras exactas.</summary>
    [Theory]
    [InlineData(2026, 12, 13, 0, 15)]    // menos de un año
    [InlineData(2025, 12, 12, 1, 15)]    // un año justo
    [InlineData(2024, 1, 1, 2, 15)]      // dos años
    [InlineData(2023, 12, 12, 3, 19)]    // tres años justos → sube de tramo
    [InlineData(2020, 1, 1, 6, 19)]
    [InlineData(2016, 12, 12, 10, 21)]   // diez años justos → tramo máximo
    [InlineData(2000, 1, 1, 26, 21)]
    public void Tier_FollowsSeniorityAtTheAccrualCutoff(int year, int month, int day, int expectedYears, int expectedTier)
    {
        var hireDate = new DateOnly(year, month, day);

        Assert.Equal(expectedYears, AguinaldoRules.SeniorityYearsAtAccrualEnd(hireDate, 2026));
        Assert.Equal(expectedTier, AguinaldoRules.TierDaysFor(AguinaldoRules.SeniorityYearsAtAccrualEnd(hireDate, 2026)));
    }

    /// <summary>
    /// El caso 4 del requerimiento, con sus números: aguinaldo de 1,600 con 1,500 exentos → la Renta se aplica
    /// sobre 100. El salario sale de despejar: 15 días de (X/30) = 1,600 → X = 3,200.
    /// </summary>
    [Fact]
    public void Exemption_TaxesOnlyTheExcess()
    {
        var result = AguinaldoRules.Calculate(new AguinaldoCalculationInput(
            HireDate: new DateOnly(2025, 12, 1), Year: 2026, MonthlyBaseSalary: 3200m, ExemptAmount: 1500m));

        Assert.Equal(1600m, result.Amount);
        Assert.Equal(1500m, result.ExemptAmount);
        Assert.Equal(100m, result.TaxableAmount);
    }

    /// <summary>La exención es un TOPE: un aguinaldo menor no genera exención «sobrante» ni gravable negativo.</summary>
    [Fact]
    public void Exemption_NeverExceedsTheAmountPaid()
    {
        var result = AguinaldoRules.Calculate(new AguinaldoCalculationInput(
            HireDate: new DateOnly(2025, 12, 1), Year: 2026, MonthlyBaseSalary: 600m, ExemptAmount: 1500m));

        Assert.Equal(300m, result.Amount);           // 20 × 15
        Assert.Equal(300m, result.ExemptAmount);
        Assert.Equal(0m, result.TaxableAmount);
    }

    /// <summary>
    /// Sin parámetro registrado se grava TODO. Es la postura conservadora que el motor ya tiene con una tabla
    /// de Renta ausente: retener de más es visible y corregible; dejar de retener es una contingencia fiscal.
    /// </summary>
    [Fact]
    public void WithoutAnExemptionParameter_TheWholeAguinaldoIsTaxable()
    {
        var result = AguinaldoRules.Calculate(new AguinaldoCalculationInput(
            HireDate: new DateOnly(2025, 12, 1), Year: 2026, MonthlyBaseSalary: 3200m, ExemptAmount: null));

        Assert.Equal(1600m, result.Amount);
        Assert.Equal(0m, result.ExemptAmount);
        Assert.Equal(1600m, result.TaxableAmount);
    }

    /// <summary>
    /// La exención es un tope <b>por persona y por año</b>, no por plaza. Con dos plazas de $2,400 mensuales
    /// —15 días cada una = $1,200 por línea— y una exención de $1,500, lo correcto es eximir 1,500 en total y
    /// gravar 900. Aplicarla por línea eximiría 2,400 y la retención saldría en cero.
    /// <para>
    /// El defecto existió: se midió en el ambiente, donde la corrida real trae <b>60 líneas para 59
    /// personas</b>. No se manifestó ahí solo porque el aguinaldo de esa persona quedaba bajo el tope.
    /// </para>
    /// </summary>
    [Fact]
    public void TheExemption_IsCappedPerEmployee_NotPerPlaza()
    {
        var results = AguinaldoRules.CalculateForEmployee(
            hireDate: new DateOnly(2025, 12, 1),
            year: 2026,
            plazaMonthlyBaseSalaries: [2400m, 2400m],
            exemptAmount: 1500m);

        Assert.Equal(2, results.Count);
        Assert.All(results, result => Assert.Equal(1200m, result.Amount));

        // La primera línea agota 1,200 del tope; a la segunda le quedan 300.
        Assert.Equal(1200m, results[0].ExemptAmount);
        Assert.Equal(300m, results[1].ExemptAmount);

        Assert.Equal(1500m, results.Sum(result => result.ExemptAmount));
        Assert.Equal(900m, results.Sum(result => result.TaxableAmount));
    }

    /// <summary>Con una sola plaza el reparto no cambia nada — la ruta común sigue siendo la de siempre.</summary>
    [Fact]
    public void TheExemption_WithASinglePlaza_BehavesExactlyAsBefore()
    {
        var results = AguinaldoRules.CalculateForEmployee(
            hireDate: new DateOnly(2025, 12, 1),
            year: 2026,
            plazaMonthlyBaseSalaries: [3200m],
            exemptAmount: 1500m);

        var single = Assert.Single(results);
        Assert.Equal(1600m, single.Amount);
        Assert.Equal(1500m, single.ExemptAmount);
        Assert.Equal(100m, single.TaxableAmount);
    }

    /// <summary>Sin parámetro registrado no hay nada que repartir: las dos líneas se gravan completas.</summary>
    [Fact]
    public void WithoutAnExemption_EveryPlazaLineIsFullyTaxable()
    {
        var results = AguinaldoRules.CalculateForEmployee(
            hireDate: new DateOnly(2025, 12, 1),
            year: 2026,
            plazaMonthlyBaseSalaries: [2400m, 2400m],
            exemptAmount: null);

        Assert.All(results, result => Assert.Equal(0m, result.ExemptAmount));
        Assert.Equal(2400m, results.Sum(result => result.TaxableAmount));
    }

    /// <summary>Quien ingresa después del cierre del devengo no devenga nada de ESE año.</summary>
    [Fact]
    public void HiredAfterTheAccrualCutoff_AccruesNothing()
    {
        var result = AguinaldoRules.Calculate(new AguinaldoCalculationInput(
            HireDate: new DateOnly(2026, 12, 20), Year: 2026, MonthlyBaseSalary: 600m, ExemptAmount: null));

        Assert.Equal(0, result.AccruedDays);
        Assert.Equal(0m, result.Amount);
    }

    /// <summary>El devengo cierra el 12 de diciembre y abre el 12 de diciembre anterior — no es el año calendario.</summary>
    [Fact]
    public void TheAccrualPeriod_RunsDecemberTwelfthToDecemberTwelfth()
    {
        Assert.Equal(new DateOnly(2025, 12, 12), AguinaldoRules.AccrualStart(2026));
        Assert.Equal(new DateOnly(2026, 12, 12), AguinaldoRules.AccrualEnd(2026));

        // Quien entró el 1 de diciembre de 2025 —ANTES de que abriera el periodo de 2026— devenga el año
        // completo de 2026, no 376 días: el devengo está topado por el periodo, no por su antigüedad.
        Assert.Equal(365, AguinaldoRules.AccruedDays(new DateOnly(2025, 12, 1), 2026));
    }

    /// <summary>La ventana legal de pago de la reforma de 2025: del 20 de octubre al 20 de diciembre, inclusive.</summary>
    [Theory]
    [InlineData(2026, 10, 19, false)]
    [InlineData(2026, 10, 20, true)]
    [InlineData(2026, 10, 25, true)]     // el ejemplo del requerimiento
    [InlineData(2026, 12, 20, true)]
    [InlineData(2026, 12, 21, false)]
    [InlineData(2026, 1, 15, false)]
    public void ThePaymentWindow_IsOctoberTwentiethToDecemberTwentieth(int year, int month, int day, bool expected) =>
        Assert.Equal(expected, AguinaldoRules.IsWithinPaymentWindow(new DateOnly(year, month, day)));

    /// <summary>
    /// Los dos motores que pagan aguinaldo —éste y el proporcional del finiquito— <b>miden el mismo periodo
    /// desde el mismo ancla</b>: la fecha de INGRESO (H-28) y la frontera del 12 de diciembre. Lo que cambia es
    /// el encuadre: el finiquito trata el 12-dic como la APERTURA del periodo siguiente (su ventana es
    /// [12-dic, 11-dic]) porque liquida lo aún no pagado, mientras la nómina anual lo trata como el CIERRE del
    /// año que está pagando. De ahí el desfase de exactamente un día, que este test fija: si el ancla de
    /// cualquiera de los dos se moviera, la diferencia dejaría de ser 1.
    /// <para>
    /// ⚠️ <b>Punto abierto declarado.</b> El encuadre del finiquito supone que el aguinaldo del periodo aún no
    /// se pagó. Con la reforma de 2025 una empresa puede pagarlo el 25 de octubre y despedir en noviembre: el
    /// finiquito volvería a liquidar el proporcional del mismo periodo. Cerrar eso exige tocar el finiquito, y
    /// el usuario lo excluyó explícitamente de esta pasada («no lo modifiques en esta pasada»).
    /// </para>
    /// </summary>
    [Fact]
    public void ItMeasuresTheSameWindowAsTheSettlementEngine()
    {
        var hireDate = new DateOnly(2026, 8, 1);

        // El último día del periodo según el encuadre del finiquito: 11 de diciembre.
        var settlementDays = CLARIHR.Application.Features.PersonnelFiles.SettlementCalculationRules
            .DaysInAguinaldoPeriod(
                hireDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                new DateTime(2026, 12, 11, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(132, settlementDays);
        Assert.Equal(settlementDays + 1, AguinaldoRules.AccruedDays(hireDate, 2026));

        // Y el ancla es la misma: mover la fecha de ingreso mueve a los dos por igual.
        var laterHire = new DateOnly(2026, 9, 1);
        var settlementLater = CLARIHR.Application.Features.PersonnelFiles.SettlementCalculationRules
            .DaysInAguinaldoPeriod(
                laterHire.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                new DateTime(2026, 12, 11, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(
            settlementDays - settlementLater,
            AguinaldoRules.AccruedDays(hireDate, 2026) - AguinaldoRules.AccruedDays(laterHire, 2026));
    }
}

namespace CLARIHR.Application.Features.Payroll;

/// <summary>
/// Lo que se necesita para liquidar el aguinaldo anual de UNA plaza. Todo resuelto: el módulo es puro y no
/// consulta nada — el proveedor de datos arma esto y el ensamblador lo convierte en una línea de la corrida.
/// </summary>
/// <param name="HireDate">Fecha de ingreso a la EMPRESA (H-28: la antigüedad nunca se ancla en la plaza).</param>
/// <param name="Year">Año calendario del aguinaldo — el que la fecha de pago de la empresa determina.</param>
/// <param name="MonthlyBaseSalary">Salario BÁSICO mensualizado de la plaza (decisión del usuario 2026-08-12).</param>
/// <param name="ExemptAmount">
/// Monto exento de Renta vigente para ese año, ABSOLUTO. <c>null</c> = la empresa no lo tiene registrado: se
/// grava todo y la corrida emite la advertencia, misma postura que el motor ya tiene con una tabla de Renta
/// ausente (retener de más es conservador y visible; dejar de retener es una contingencia).
/// </param>
public sealed record AguinaldoCalculationInput(
    DateOnly HireDate,
    int Year,
    decimal MonthlyBaseSalary,
    decimal? ExemptAmount);

/// <summary>El resultado, con TODO el rastro visible para que una boleta pueda justificar el número.</summary>
/// <param name="AccruedDays">Días laborados dentro del periodo de devengo (tope <see cref="AguinaldoRules.YearDivisorDays"/>).</param>
/// <param name="TierDays">Días de salario que manda el Art. 198 según la antigüedad (15 / 19 / 21).</param>
/// <param name="SeniorityYears">Años completos de antigüedad al cierre del devengo — es lo que elige el tramo.</param>
/// <param name="DailySalary">Salario diario sin redondear (mensual / 30) — el redondeo ocurre UNA vez, en el monto.</param>
/// <param name="Amount">Monto bruto del aguinaldo.</param>
/// <param name="ExemptAmount">Porción exenta efectivamente aplicada (nunca mayor que el monto).</param>
/// <param name="TaxableAmount">El excedente: lo ÚNICO que entra a la base de Renta.</param>
public sealed record AguinaldoCalculationResult(
    int AccruedDays,
    decimal TierDays,
    int SeniorityYears,
    decimal DailySalary,
    decimal Amount,
    decimal ExemptAmount,
    decimal TaxableAmount);

/// <summary>
/// El motor del aguinaldo (Código de Trabajo arts. 196–202, con la reforma de 2025).
/// <para>
/// <b>Devengo.</b> El periodo corre del <b>12 de diciembre del año anterior al 12 de diciembre del año</b>, y
/// quien entró a mitad de camino devenga solo desde su ingreso. La reforma de 2025 adelantó la VENTANA DE PAGO
/// (20-oct → 20-dic) pero el devengo se sigue midiendo al 12 de diciembre —decisión ratificada por el usuario
/// 2026-08-12: «hasta el 12 de diciembre como lo hizo el gobierno»—, así que una empresa que paga el 25 de
/// octubre está pagando por adelantado un devengo que cierra en diciembre. Eso es deliberado y es lo que hizo
/// el Estado en 2025.
/// </para>
/// <para>
/// <b>Tramos (Art. 198).</b> 15 · 19 · 21 días de salario para 1–3 · 3–10 · 10+ años de antigüedad, medida al
/// cierre del devengo y <b>desde la fecha de ingreso a la empresa</b> (H-28) — nunca desde el registro de la
/// plaza, que es el defecto que ya costó una subestimación del 95 % en el finiquito.
/// </para>
/// <para>
/// <b>Es la misma aritmética del finiquito</b> (<c>SettlementCalculation.Rules</c>, concepto
/// <c>AGUINALDO_PROPORCIONAL</c>) y así debe quedar: los dos motores pueden pagar aguinaldo sobre el mismo
/// periodo y tienen que dar el mismo número. Se porta, no se comparte: el finiquito depende del tipo de
/// desvinculación y tiene su propia parametrización por empresa, que esta pasada NO toca (instrucción explícita
/// del usuario).
/// </para>
/// </summary>
public static class AguinaldoRules
{
    /// <summary>Divisor anual — el mismo que el finiquito (<c>SettlementParametersInput.YearDivisorDays</c>).</summary>
    public const int YearDivisorDays = 365;

    /// <summary>Divisor mensual para la diaria — «diaria = mensual / 30», fijado en REQ-012.</summary>
    public const int MonthDivisorDays = 30;

    /// <summary>Primer día de la ventana legal de pago (reforma 2025): 20 de octubre.</summary>
    public const int PaymentWindowStartMonth = 10;

    /// <summary>Primer día de la ventana legal de pago (reforma 2025): 20 de octubre.</summary>
    public const int PaymentWindowStartDay = 20;

    /// <summary>Último día de la ventana legal de pago (reforma 2025): 20 de diciembre.</summary>
    public const int PaymentWindowEndMonth = 12;

    /// <summary>Último día de la ventana legal de pago (reforma 2025): 20 de diciembre.</summary>
    public const int PaymentWindowEndDay = 20;

    /// <summary>Cierre del devengo del año: 12 de diciembre.</summary>
    public static DateOnly AccrualEnd(int year) => new(year, 12, 12);

    /// <summary>Apertura del devengo del año: 12 de diciembre del año anterior.</summary>
    public static DateOnly AccrualStart(int year) => new(year - 1, 12, 12);

    /// <summary>Si una fecha cae dentro de la ventana legal de pago (20-oct → 20-dic) de su año.</summary>
    public static bool IsWithinPaymentWindow(DateOnly date) =>
        date >= new DateOnly(date.Year, PaymentWindowStartMonth, PaymentWindowStartDay) &&
        date <= new DateOnly(date.Year, PaymentWindowEndMonth, PaymentWindowEndDay);

    /// <summary>
    /// Días devengados del año por alguien que ingresó en <paramref name="hireDate"/>. Un ingreso previo al
    /// periodo devenga el año completo; uno posterior al cierre del devengo, cero.
    /// </summary>
    public static int AccruedDays(DateOnly hireDate, int year)
    {
        var end = AccrualEnd(year);
        var start = AccrualStart(year);
        if (hireDate > start)
        {
            start = hireDate;
        }

        return start >= end ? 0 : Math.Min(YearDivisorDays, end.DayNumber - start.DayNumber);
    }

    /// <summary>Años completos de antigüedad al cierre del devengo, desde el INGRESO (H-28).</summary>
    public static int SeniorityYearsAtAccrualEnd(DateOnly hireDate, int year)
    {
        var days = AccrualEnd(year).DayNumber - hireDate.DayNumber;
        return days <= 0 ? 0 : days / YearDivisorDays;
    }

    /// <summary>Días de salario del Art. 198 según los años completos de antigüedad.</summary>
    public static decimal TierDaysFor(int seniorityYears) => seniorityYears switch
    {
        >= 10 => 21m,
        >= 3 => 19m,
        _ => 15m,
    };

    /// <summary>
    /// El aguinaldo de un empleado con <b>una o varias plazas</b>. Cada plaza aporta su propio salario y por
    /// tanto su propio monto, pero la exención es un tope <b>por persona y por año</b>: se reparte entre las
    /// líneas hasta agotarse, no se repite en cada una.
    /// <para>
    /// La distinción no es teórica. Con dos plazas de $1,200 y una exención de $1,500, aplicarla por línea
    /// exime $2,400 y la retención sale en cero; lo correcto es eximir $1,500 y gravar $900. Se midió en el
    /// ambiente: la corrida real trae 60 líneas para 59 personas.
    /// </para>
    /// </summary>
    /// <param name="plazaMonthlyBaseSalaries">Los básicos mensualizados, en el orden en que se emitirán las líneas.</param>
    public static IReadOnlyList<AguinaldoCalculationResult> CalculateForEmployee(
        DateOnly hireDate,
        int year,
        IReadOnlyList<decimal> plazaMonthlyBaseSalaries,
        decimal? exemptAmount)
    {
        ArgumentNullException.ThrowIfNull(plazaMonthlyBaseSalaries);

        var results = new List<AguinaldoCalculationResult>(plazaMonthlyBaseSalaries.Count);
        var remaining = exemptAmount;

        foreach (var salary in plazaMonthlyBaseSalaries)
        {
            var result = Calculate(new AguinaldoCalculationInput(hireDate, year, salary, remaining));
            results.Add(result);

            // Lo consumido por esta línea deja de estar disponible para la siguiente. Con `null` —sin
            // parámetro registrado— no hay nada que consumir y todas las líneas se gravan completas.
            remaining = remaining is { } available
                ? Math.Max(0m, available - result.ExemptAmount)
                : null;
        }

        return results;
    }

    public static AguinaldoCalculationResult Calculate(AguinaldoCalculationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var accruedDays = AccruedDays(input.HireDate, input.Year);
        var seniorityYears = SeniorityYearsAtAccrualEnd(input.HireDate, input.Year);
        var tierDays = TierDaysFor(seniorityYears);
        var dailySalary = input.MonthlyBaseSalary <= 0m ? 0m : input.MonthlyBaseSalary / MonthDivisorDays;

        // Un solo redondeo, sobre el monto — igual que el resto del motor de planilla.
        var amount = Round2(dailySalary * tierDays * accruedDays / YearDivisorDays);

        // La exención es un TOPE, no un descuento: nunca puede volver negativo el gravable ni exceder el pago.
        var exempt = Math.Min(amount, Math.Max(0m, input.ExemptAmount ?? 0m));

        return new AguinaldoCalculationResult(
            accruedDays,
            tierDays,
            seniorityYears,
            dailySalary,
            amount,
            exempt,
            Round2(amount - exempt));
    }

    private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

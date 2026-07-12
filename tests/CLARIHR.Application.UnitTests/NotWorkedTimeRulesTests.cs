using CLARIHR.Application.Features.PersonnelFiles.Absences;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// The golden cases of the not-worked-time engine (REQ-011 PR-2) — these are what the accountant validates.
///
/// The day scan is a copy of the incapacity engine's, so it is not where the risk lives. The risk lives in the
/// SEVENTH DAY (P-18): a rule with no precedent anywhere in the product. An employee who misses a full week loses
/// not only the five worked days but the paid day of rest they no longer earned — six days of discount for five
/// days of absence.
/// </summary>
public sealed class NotWorkedTimeRulesTests
{
    // 2026-07-06 is a Monday.
    private static readonly DateOnly Monday = new(2026, 7, 6);

    private static NotWorkedTimeCalculationInput Input(
        DateOnly start,
        DateOnly end,
        bool countsSeventhDayPenalty = true,
        bool countsHoliday = false,
        bool countsSaturday = false,
        bool countsRestDay = false,
        bool usesWorkSchedule = false,
        decimal? hours = null,
        decimal discountPercent = 100m,
        IReadOnlySet<DateOnly>? holidays = null,
        decimal monthlyBaseSalary = 900m,
        decimal standardDailyHours = 8m) =>
        new(
            start,
            end,
            countsHoliday,
            countsSaturday,
            countsRestDay,
            countsSeventhDayPenalty,
            usesWorkSchedule,
            hours,
            discountPercent,
            holidays ?? new HashSet<DateOnly>(),
            DayOfWeek.Sunday,
            monthlyBaseSalary,
            standardDailyHours);

    // ── El séptimo (P-18) — la regla nueva ────────────────────────────────────────────────────────────

    [Fact]
    public void Calculate_AFullWorkWeek_CostsSixDays_NotFive()
    {
        // Lunes a viernes: 5 días computables + 1 séptimo = 6 días descontados.
        // Salario $900 ⇒ diario $30 ⇒ $180. Descontar solo 5 ($150) le regalaría al empleado el día de descanso
        // que precisamente NO se ganó. ESTE es el caso que valida el contador.
        var result = NotWorkedTimeRules.Calculate(Input(Monday, Monday.AddDays(4)));

        Assert.Equal(5, result.CalendarDays);
        Assert.Equal(5, result.ComputableDays);
        Assert.Equal(1, result.SeventhDayPenaltyDays);
        Assert.Equal(6m, result.DiscountedDays);
        Assert.Equal(30m, result.DailySalary);
        Assert.Equal(180m, result.DiscountAmount);
    }

    [Fact]
    public void Calculate_TwoWorkWeeks_CostTwoSeventhDays()
    {
        // Lunes a viernes de la semana siguiente: 10 computables + 2 séptimos = 12.
        var result = NotWorkedTimeRules.Calculate(Input(Monday, Monday.AddDays(11)));

        Assert.Equal(10, result.ComputableDays);   // se excluyen sábado y domingo intermedios
        Assert.Equal(2, result.SeventhDayPenaltyDays);
        Assert.Equal(12m, result.DiscountedDays);
        Assert.Equal(360m, result.DiscountAmount);
    }

    [Fact]
    public void Calculate_ASingleDay_StillCostsItsSeventh()
    {
        // Un solo día de ausencia YA afecta la semana: 1 + 1 = 2. Es la lectura literal de la regla ratificada.
        var result = NotWorkedTimeRules.Calculate(Input(Monday, Monday));

        Assert.Equal(1, result.ComputableDays);
        Assert.Equal(1, result.SeventhDayPenaltyDays);
        Assert.Equal(60m, result.DiscountAmount);
    }

    [Fact]
    public void Calculate_WithoutThePenaltyFlag_NoSeventhIsAdded()
    {
        var result = NotWorkedTimeRules.Calculate(Input(Monday, Monday.AddDays(4), countsSeventhDayPenalty: false));

        Assert.Equal(5, result.ComputableDays);
        Assert.Equal(0, result.SeventhDayPenaltyDays);
        Assert.Equal(5m, result.DiscountedDays);
        Assert.Equal(150m, result.DiscountAmount);
    }

    // ── El scan (el mismo de incapacidades) ───────────────────────────────────────────────────────────

    [Fact]
    public void Calculate_ExcludesTheRestDay_TheSaturday_AndTheHoliday()
    {
        // Lunes a domingo, con el miércoles de asueto. Computables = lunes, martes, jueves, viernes = 4.
        var holidays = new HashSet<DateOnly> { Monday.AddDays(2) };
        var result = NotWorkedTimeRules.Calculate(
            Input(Monday, Monday.AddDays(6), holidays: holidays, countsSeventhDayPenalty: false));

        Assert.Equal(7, result.CalendarDays);
        Assert.Equal(4, result.ComputableDays);

        var excluded = result.Details.Where(detail => !detail.IsComputable).ToArray();
        Assert.Equal(3, excluded.Length);
        Assert.Contains(excluded, detail => detail.Reason == "ASUETO");
        Assert.Contains(excluded, detail => detail.Reason == "SABADO");
        Assert.Contains(excluded, detail => detail.Reason == "DIA_DESCANSO");
    }

    [Fact]
    public void Calculate_WhenTheFlagsSayTheyCount_NothingIsExcluded()
    {
        var holidays = new HashSet<DateOnly> { Monday.AddDays(2) };
        var result = NotWorkedTimeRules.Calculate(Input(
            Monday, Monday.AddDays(6),
            countsHoliday: true, countsSaturday: true, countsRestDay: true,
            countsSeventhDayPenalty: false, holidays: holidays));

        Assert.Equal(7, result.ComputableDays);
        Assert.All(result.Details, detail => Assert.True(detail.IsComputable));
    }

    [Fact]
    public void IsExcluded_TheRestDayIsNotHardcodedToSunday()
    {
        // Una empresa con descanso en LUNES: el lunes se excluye y el domingo NO. Hardcodear el domingo (una
        // tentación obvia) rompería a cualquier empresa con otro día de descanso.
        var holidays = new HashSet<DateOnly>();

        Assert.True(NotWorkedTimeRules.IsExcluded(
            Monday, countsRestDay: false, countsSaturday: true, countsHoliday: true, DayOfWeek.Monday, holidays));

        Assert.False(NotWorkedTimeRules.IsExcluded(
            Monday.AddDays(6), countsRestDay: false, countsSaturday: true, countsHoliday: true, DayOfWeek.Monday, holidays));
    }

    // ── Modo horas ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Calculate_InHoursMode_TwoLateHours_AreAQuarterOfADay()
    {
        // Llegada tardía de 2 h con jornada de 8 h ⇒ 0.25 días ⇒ $30 × 0.25 = $7.50.
        // Valorarla como un día entero ($30) sería un castigo, no un descuento.
        var result = NotWorkedTimeRules.Calculate(Input(
            Monday, Monday, usesWorkSchedule: true, hours: 2m, countsSeventhDayPenalty: false));

        Assert.Equal(0.25m, result.DiscountedDays);
        Assert.Equal(7.50m, result.DiscountAmount);
    }

    [Fact]
    public void Calculate_InHoursMode_TheSeventhDoesNotApply()
    {
        // Aunque el tipo llevara el flag, en modo horas el descuento son las horas: un retraso de 2 h no le hace
        // perder el descanso semanal.
        var result = NotWorkedTimeRules.Calculate(Input(
            Monday, Monday, usesWorkSchedule: true, hours: 2m, countsSeventhDayPenalty: true));

        Assert.Equal(0.25m, result.DiscountedDays);
        Assert.Equal(7.50m, result.DiscountAmount);
    }

    [Theory]
    [InlineData(8, 8, 1)]
    [InlineData(4, 8, 0.5)]
    [InlineData(3, 6, 0.5)]      // la jornada de la empresa manda, no un 8 hardcodeado
    public void HoursToDays_UsesTheCompanyStandardDay(decimal hours, decimal standardDay, decimal expected) =>
        Assert.Equal(expected, NotWorkedTimeRules.HoursToDays(hours, standardDay));

    // ── Los bordes ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Calculate_WithZeroPercent_DiscountsNothing()
    {
        // «Con goce»: el registro existe (y sus días se computan, para el expediente), pero el dinero no se toca.
        var result = NotWorkedTimeRules.Calculate(Input(Monday, Monday.AddDays(4), discountPercent: 0m));

        Assert.Equal(5, result.ComputableDays);
        Assert.Equal(6m, result.DiscountedDays);
        Assert.Equal(0m, result.DiscountAmount);
    }

    [Fact]
    public void Calculate_WithAPartialPercent_ProratesTheDiscount()
    {
        // 50 % de goce: 6 días × $30 × 50 % = $90.
        var result = NotWorkedTimeRules.Calculate(Input(Monday, Monday.AddDays(4), discountPercent: 50m));

        Assert.Equal(90m, result.DiscountAmount);
    }

    [Fact]
    public void Calculate_WithNoSalaryConfigured_DoesNotBlowUp()
    {
        // Un empleado sin salario no debe romper el registro: los días se cuentan, el monto es 0.
        var result = NotWorkedTimeRules.Calculate(Input(Monday, Monday.AddDays(4), monthlyBaseSalary: 0m));

        Assert.Equal(6m, result.DiscountedDays);
        Assert.Equal(0m, result.DailySalary);
        Assert.Equal(0m, result.DiscountAmount);
    }

    [Fact]
    public void Calculate_TheDailySalaryIsRoundedOnce()
    {
        // $1,000 / 30 = 33.3333… ⇒ diario $33.33 (redondeado UNA vez) ⇒ 6 × 33.33 = $199.98.
        // Redondear al final daría $200.00. La diferencia son centavos, y los centavos son la mitad de los
        // reclamos de planilla.
        var result = NotWorkedTimeRules.Calculate(Input(Monday, Monday.AddDays(4), monthlyBaseSalary: 1000m));

        Assert.Equal(33.33m, result.DailySalary);
        Assert.Equal(199.98m, result.DiscountAmount);
    }

    [Fact]
    public void Calculate_AWeekendOnlyAbsence_CostsNothing()
    {
        // Sábado y domingo, ninguno cuenta ⇒ 0 computables ⇒ 0 semanas afectadas ⇒ 0 séptimos ⇒ $0.
        // Si el séptimo se sumara por semana del RANGO (y no por semana AFECTADA), aquí saldría un día de castigo
        // por una ausencia que no existió.
        var result = NotWorkedTimeRules.Calculate(Input(Monday.AddDays(5), Monday.AddDays(6)));

        Assert.Equal(0, result.ComputableDays);
        Assert.Equal(0, result.SeventhDayPenaltyDays);
        Assert.Equal(0m, result.DiscountAmount);
    }
}

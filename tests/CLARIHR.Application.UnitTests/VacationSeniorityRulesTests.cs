using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// H-28 — cobertura unitaria del ancla de antigüedad de vacaciones, que no tenía NINGUNA (instancia de H-33: las
/// dos reglas que producían el defecto nunca se probaron aisladas, y los tests de integración que existían
/// sembraban el ingreso y el inicio de plaza con la MISMA fecha, así que no podían distinguir los dos anclajes).
/// <para>
/// Lo que se fija acá: el derecho del Art. 177 se mide sobre la antigüedad en la EMPRESA (`hireDate`), la ventana
/// del periodo corre sobre el aniversario de ingreso, y la elegibilidad se evalúa contra el FIN del periodo —no su
/// inicio—, que es lo que arregla el modo año calendario.
/// </para>
/// </summary>
public sealed class VacationSeniorityRulesTests
{
    // El caso del hallazgo: ingresó hace 2.5 años, su plaza se registró la semana pasada.
    private static readonly DateOnly Hire2024 = new(2024, 2, 1);

    [Fact]
    public void IsEligible_WithMoreThanOneYearOfService_IsTrue() =>
        Assert.True(VacationRules.IsEligible(Hire2024, new DateOnly(2026, 2, 1)));

    [Fact]
    public void IsEligible_OnTheExactFirstAnniversary_IsTrue() =>
        // El año se cumple EL día del aniversario, no el siguiente.
        Assert.True(VacationRules.IsEligible(Hire2024, new DateOnly(2025, 2, 1)));

    [Fact]
    public void IsEligible_OneDayBeforeTheFirstAnniversary_IsFalse() =>
        Assert.False(VacationRules.IsEligible(Hire2024, new DateOnly(2025, 1, 31)));

    [Fact]
    public void IsEligible_WithSevenMonthsOfService_IsFalse() =>
        Assert.False(VacationRules.IsEligible(new DateOnly(2026, 1, 16), new DateOnly(2026, 8, 12)));

    [Fact]
    public void IsEligible_WithLeapDayHire_FoldsToFeb28() =>
        // 29-feb + 1 año = 28-feb del año no bisiesto (DateOnly.AddYears).
        Assert.True(VacationRules.IsEligible(new DateOnly(2024, 2, 29), new DateOnly(2025, 2, 28)));

    // ── La ventana del periodo ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void PeriodBounds_WithAnniversary_RunsFromTheHireAnniversary()
    {
        var (start, end) = VacationRules.PeriodBounds(2026, useAnniversary: true, Hire2024);
        Assert.Equal(new DateOnly(2026, 2, 1), start);
        Assert.Equal(new DateOnly(2027, 1, 31), end);
    }

    [Fact]
    public void PeriodBounds_WithoutAnniversary_IsTheCalendarYear()
    {
        var (start, end) = VacationRules.PeriodBounds(2026, useAnniversary: false, Hire2024);
        Assert.Equal(new DateOnly(2026, 1, 1), start);
        Assert.Equal(new DateOnly(2026, 12, 31), end);
    }

    [Fact]
    public void PeriodBounds_WithLeapDayAnniversaryInANonLeapYear_LandsOnFeb28()
    {
        var (start, end) = VacationRules.PeriodBounds(2025, useAnniversary: true, new DateOnly(2024, 2, 29));
        Assert.Equal(new DateOnly(2025, 2, 28), start);
        Assert.Equal(new DateOnly(2026, 2, 27), end);
    }

    // ── La elegibilidad del periodo: contra el FIN, no el inicio ──────────────────────────────────────

    /// <summary>
    /// El arreglo del modo **año calendario**. Alguien que ingresó el 2026-01-16 cumple su año el 2027-01-16, o sea
    /// DENTRO del periodo calendario 2027 — tiene derecho a ese fondo. Midiendo contra el INICIO del periodo
    /// (`2027-01-01 >= 2027-01-16` = false) su primer fondo salía en **2028**: un año tarde. Midiendo contra el fin
    /// sale en 2027, que es lo correcto.
    /// </summary>
    [Fact]
    public void IsEligibleForPeriod_CalendarYearContainingTheAnniversary_IsTrue()
    {
        var hire = new DateOnly(2026, 1, 16);
        var bounds = VacationRules.PeriodBounds(2027, useAnniversary: false, hire);
        Assert.True(VacationRules.IsEligibleForPeriod(hire, bounds));
        // Y el año anterior sigue negado: el aniversario cae fuera del periodo 2026.
        Assert.False(VacationRules.IsEligibleForPeriod(hire, VacationRules.PeriodBounds(2026, useAnniversary: false, hire)));
    }

    /// <summary>
    /// En modo aniversario (el default) medir contra el fin NO cambia ninguna respuesta: la ventana empieza en el
    /// aniversario, así que ambos extremos dan lo mismo. Este test existe para que nadie "simplifique" el cambio
    /// creyendo que afectó al modo que sí estaba bien.
    /// </summary>
    [Theory]
    [InlineData(2024, false)] // el año del ingreso: todavía no cumple
    [InlineData(2025, true)]  // primer aniversario
    [InlineData(2026, true)]
    public void IsEligibleForPeriod_AnniversaryMode_MatchesTheStartBasedAnswer(int year, bool expected)
    {
        var bounds = VacationRules.PeriodBounds(year, useAnniversary: true, Hire2024);
        Assert.Equal(expected, VacationRules.IsEligibleForPeriod(Hire2024, bounds));
        Assert.Equal(expected, VacationRules.IsEligible(Hire2024, bounds.Start));
    }
}

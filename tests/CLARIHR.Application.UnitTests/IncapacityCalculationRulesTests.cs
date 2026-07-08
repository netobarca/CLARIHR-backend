using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.Leave;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// The incapacity calculation engine's critical golden suite (PR-4, the Ola 1 gate) — the CONFIRMED
/// Anexo A.4 cases plus the §6 border cases, all encoded as blocking assertions. The engine is 100%
/// pure so these fully pin the day counting, the chain segmentation, the employer-cap resolution
/// (D-27), the /30 daily salary and the single-rounding money identities (D-21).
/// Reference country: El Salvador. All dates are 2026 (Jan 1, 2026 = Thursday).
/// </summary>
public sealed class IncapacityCalculationRulesTests
{
    private static readonly HashSet<DateOnly> NoHolidays = [];

    // A.2 confirmed risk templates (day range → percent + payer).
    private static readonly IReadOnlyList<IncapacityTrancheParameter> CommonIllness =
    [
        new(1, 3, 75m, IncapacityPayerCodes.Empresa),
        new(4, null, 75m, IncapacityPayerCodes.Isss),
    ];

    private static readonly IReadOnlyList<IncapacityTrancheParameter> WorkAccident =
    [
        new(1, null, 100m, IncapacityPayerCodes.Isss),
    ];

    private static readonly IReadOnlyList<IncapacityTrancheParameter> Maternity =
    [
        new(1, 112, 100m, IncapacityPayerCodes.Isss),
    ];

    private static IncapacityCalculationInput Input(
        DateOnly start,
        DateOnly? end,
        IReadOnlyList<IncapacityTrancheParameter> tranches,
        decimal salary = 600m,
        decimal cap = 9m,
        bool countsSeventh = false,
        bool countsSaturday = true,
        bool countsHoliday = true,
        bool hasSubsidy = true,
        DayOfWeek restDay = DayOfWeek.Sunday,
        int chainOffset = 0,
        IReadOnlySet<DateOnly>? holidays = null) =>
        new(
            start,
            end,
            countsSeventh,
            countsSaturday,
            countsHoliday,
            hasSubsidy,
            tranches,
            holidays ?? NoHolidays,
            restDay,
            chainOffset,
            salary,
            cap);

    // ── A.4-1: common illness, 5 calendar days Wed→Sun, rest day Sunday, no seventh ────────────────
    [Fact]
    public void A41_CommonIllness_FiveCalendarDays_SplitsEmpresaAndIsss()
    {
        // 2026-01-07 = Wednesday, 2026-01-11 = Sunday.
        var result = IncapacityCalculationRules.Calculate(Input(
            new DateOnly(2026, 1, 7),
            new DateOnly(2026, 1, 11),
            CommonIllness,
            salary: 600m,
            cap: 9m,
            restDay: DayOfWeek.Sunday));

        Assert.Equal(5, result.CalendarDays);
        Assert.Equal(4, result.ComputableDays);   // Sunday excluded, Saturday counted.
        Assert.Equal(20.00m, result.DailySalary);  // 600 / 30.
        Assert.Equal(3, result.EmployerDays);
        Assert.Equal(45.00m, result.EmployerAmount); // 3 × 20 × 75%.
        Assert.Equal(1, result.SubsidizedDays);
        Assert.Equal(15.00m, result.SubsidyAmount);  // 1 × 20 × 75%.
        Assert.Equal(0, result.DiscountDays);
        Assert.Equal(0m, result.DiscountAmount);
        Assert.Equal(3, result.EmployerCapConsumed);
        Assert.Empty(result.Warnings);
        Assert.False(result.IsDeferred);
    }

    // ── A.4-2: common illness, 2 days → all EMPRESA; consumes 2 of the cap ─────────────────────────
    [Fact]
    public void A42_TwoDays_AllEmpresa_ConsumesTwoOfCap()
    {
        // 2026-01-05 Monday → 2026-01-06 Tuesday.
        var result = IncapacityCalculationRules.Calculate(Input(
            new DateOnly(2026, 1, 5),
            new DateOnly(2026, 1, 6),
            CommonIllness,
            salary: 600m,
            cap: 9m));

        Assert.Equal(2, result.ComputableDays);
        Assert.Equal(2, result.EmployerDays);
        Assert.Equal(30.00m, result.EmployerAmount); // 2 × 20 × 75%.
        Assert.Equal(0, result.SubsidizedDays);
        Assert.Equal(0, result.DiscountDays);
        Assert.Equal(2, result.EmployerCapConsumed); // caller subtracts → 7 of 9 remain.
        Assert.Empty(result.Warnings);
    }

    // ── A.4-3: chain extension — offset 3, 4 days → starts at absolute day 4 (ISSS), 0 EMPRESA ─────
    [Fact]
    public void A43_ChainExtension_StartsAtAbsoluteDayFour_NoEmpresa()
    {
        // 2026-01-12 Monday → 2026-01-15 Thursday (4 computable days).
        var result = IncapacityCalculationRules.Calculate(Input(
            new DateOnly(2026, 1, 12),
            new DateOnly(2026, 1, 15),
            CommonIllness,
            salary: 600m,
            chainOffset: 3));

        Assert.Equal(4, result.ComputableDays);
        Assert.Equal(0, result.EmployerDays);
        Assert.Equal(4, result.SubsidizedDays);
        Assert.Equal(60.00m, result.SubsidyAmount); // 4 × 20 × 75%.
        Assert.Equal(0, result.EmployerCapConsumed);

        var detail = Assert.Single(result.TrancheDetails);
        Assert.Equal(4, detail.DayFromAbsolute); // the chain continues, it never restarts at 1.
        Assert.Equal(7, detail.DayToAbsolute);
        Assert.Equal(IncapacityPayerCodes.Isss, detail.PayerCode);
    }

    // ── A.4-4: employer cap already exhausted → EMPRESA days reclassified to SIN_PAGO + warning ────
    [Fact]
    public void A44_CapExhausted_EmpresaDaysReclassifiedToUnpaid_WithWarning()
    {
        // 2026-01-19 Monday → 2026-01-21 Wednesday (3 computable days), all in the 1-3 EMPRESA tranche.
        var result = IncapacityCalculationRules.Calculate(Input(
            new DateOnly(2026, 1, 19),
            new DateOnly(2026, 1, 21),
            CommonIllness,
            salary: 600m,
            cap: 0m));

        Assert.Equal(3, result.ComputableDays);
        Assert.Equal(0, result.EmployerDays);
        Assert.Equal(0m, result.EmployerAmount);
        Assert.Equal(3, result.DiscountDays);
        Assert.Equal(60.00m, result.DiscountAmount); // 3 × 20 (full day value, D-21).
        Assert.Equal(0, result.EmployerCapConsumed);
        Assert.Contains(result.Warnings, warning => warning.Code == IncapacityCalculationRules.WarningCapExhausted);
    }

    // ── A.4-5: work accident, 10 days → 100% ISSS from day 1; consumes no cap ──────────────────────
    [Fact]
    public void A45_WorkAccident_TenDays_FullyIsssSubsidized_NoCapConsumed()
    {
        // 2026-02-01 → 2026-02-10 (10 calendar days); the risk counts every day.
        var result = IncapacityCalculationRules.Calculate(Input(
            new DateOnly(2026, 2, 1),
            new DateOnly(2026, 2, 10),
            WorkAccident,
            salary: 600m,
            countsSeventh: true,
            countsSaturday: true,
            countsHoliday: true));

        Assert.Equal(10, result.CalendarDays);
        Assert.Equal(10, result.ComputableDays);
        Assert.Equal(10, result.SubsidizedDays);
        Assert.Equal(200.00m, result.SubsidyAmount); // 10 × 20 × 100%.
        Assert.Equal(0, result.EmployerDays);
        Assert.Equal(0, result.EmployerCapConsumed);
        Assert.Equal(0, result.DiscountDays);
    }

    // ── A.4-6: maternity, 112 days → 100% ISSS; no discount ────────────────────────────────────────
    [Fact]
    public void A46_Maternity_OneHundredTwelveDays_FullySubsidized_NoDiscount()
    {
        // 2026-01-01 + 111 days = 112 calendar days; the risk counts every day.
        var start = new DateOnly(2026, 1, 1);
        var result = IncapacityCalculationRules.Calculate(Input(
            start,
            start.AddDays(111),
            Maternity,
            salary: 600m,
            countsSeventh: true,
            countsSaturday: true,
            countsHoliday: true));

        Assert.Equal(112, result.CalendarDays);
        Assert.Equal(112, result.ComputableDays);
        Assert.Equal(112, result.SubsidizedDays);
        Assert.Equal(2240.00m, result.SubsidyAmount); // 112 × 20 × 100%.
        Assert.Equal(0, result.DiscountDays);
        Assert.Equal(0m, result.DiscountAmount);
        Assert.Equal(0, result.EmployerDays);
    }

    // ── A.4-7: a holiday inside the range with CountsHoliday=false is excluded from count and money ─
    [Fact]
    public void A47_HolidayInRange_NotCounted_ExcludedFromDaysAndAmount()
    {
        // 2026-03-02 Monday → 2026-03-06 Friday (5 weekdays); 2026-03-04 is Wednesday.
        var start = new DateOnly(2026, 3, 2);
        var end = new DateOnly(2026, 3, 6);
        var holiday = new DateOnly(2026, 3, 4);

        var without = IncapacityCalculationRules.Calculate(Input(
            start, end, CommonIllness, salary: 600m, countsHoliday: false));
        var with = IncapacityCalculationRules.Calculate(Input(
            start, end, CommonIllness, salary: 600m, countsHoliday: false,
            holidays: new HashSet<DateOnly> { holiday }));

        Assert.Equal(5, without.ComputableDays);
        Assert.Equal(4, with.ComputableDays); // the holiday Wednesday drops out.
        Assert.Equal(without.ComputableDays - 1, with.ComputableDays);

        // The excluded day removes exactly one ISSS-subsidized day (day 4 of the chain).
        Assert.Equal(30.00m, without.SubsidyAmount); // 2 ISSS days × 20 × 75%.
        Assert.Equal(15.00m, with.SubsidyAmount);    // 1 ISSS day × 20 × 75%.
        Assert.Equal(45.00m, without.EmployerAmount);
        Assert.Equal(45.00m, with.EmployerAmount);   // the 3 EMPRESA days are unaffected.
    }

    // ── Border: a range that contains only excluded days → 0 computable, 0 money ───────────────────
    [Fact]
    public void Border_OnlyExcludedDays_ZeroComputableAndZeroMoney()
    {
        // 2026-01-03 Saturday → 2026-01-04 Sunday; Saturday not counted, Sunday is the rest day.
        var result = IncapacityCalculationRules.Calculate(Input(
            new DateOnly(2026, 1, 3),
            new DateOnly(2026, 1, 4),
            CommonIllness,
            countsSaturday: false,
            restDay: DayOfWeek.Sunday));

        Assert.Equal(2, result.CalendarDays);
        Assert.Equal(0, result.ComputableDays);
        Assert.Empty(result.TrancheDetails);
        Assert.Equal(0m, result.EmployerAmount);
        Assert.Equal(0m, result.SubsidyAmount);
        Assert.Equal(0m, result.DiscountAmount);
        Assert.Equal(0, result.EmployerCapConsumed);
    }

    // ── Border: rest day = Wednesday → the Wednesday inside the range is excluded ───────────────────
    [Fact]
    public void Border_RestDayWednesday_ExcludesTheWednesday()
    {
        // 2026-01-05 Monday → 2026-01-09 Friday; 2026-01-07 is Wednesday.
        var result = IncapacityCalculationRules.Calculate(Input(
            new DateOnly(2026, 1, 5),
            new DateOnly(2026, 1, 9),
            CommonIllness,
            restDay: DayOfWeek.Wednesday));

        Assert.Equal(5, result.CalendarDays);
        Assert.Equal(4, result.ComputableDays); // Mon, Tue, Thu, Fri.
    }

    // ── Border: Saturday and Sunday both excluded inside the range ─────────────────────────────────
    [Fact]
    public void Border_SaturdayAndSundayBothExcluded()
    {
        // 2026-01-09 Friday → 2026-01-12 Monday; Saturday not counted, Sunday is the rest day.
        var result = IncapacityCalculationRules.Calculate(Input(
            new DateOnly(2026, 1, 9),
            new DateOnly(2026, 1, 12),
            CommonIllness,
            countsSaturday: false,
            restDay: DayOfWeek.Sunday));

        Assert.Equal(4, result.CalendarDays);
        Assert.Equal(2, result.ComputableDays); // Friday and Monday only.
    }

    // ── Border: computable days land exactly on a tranche's DayTo cut → all in the first tranche ────
    [Fact]
    public void Border_ExactTrancheCut_AllInFirstTranche()
    {
        // 2026-01-05 Monday → 2026-01-07 Wednesday (3 weekdays); tranche 1-3 covers all of them.
        var result = IncapacityCalculationRules.Calculate(Input(
            new DateOnly(2026, 1, 5),
            new DateOnly(2026, 1, 7),
            CommonIllness,
            salary: 600m));

        Assert.Equal(3, result.EmployerDays);
        Assert.Equal(0, result.SubsidizedDays);
        Assert.Equal(45.00m, result.EmployerAmount);
        var detail = Assert.Single(result.TrancheDetails);
        Assert.Equal(IncapacityPayerCodes.Empresa, detail.PayerCode);
        Assert.Equal(3, detail.Days);
    }

    // ── Border: risk without subsidy → every day SIN_PAGO discounted at the full day value ──────────
    [Fact]
    public void Border_NoSubsidyRisk_AllUnpaidDiscount()
    {
        // 2026-01-05 Monday → 2026-01-07 Wednesday (3 weekdays); the risk pays no subsidy.
        var result = IncapacityCalculationRules.Calculate(Input(
            new DateOnly(2026, 1, 5),
            new DateOnly(2026, 1, 7),
            CommonIllness, // ignored — HasSubsidy=false short-circuits to SIN_PAGO.
            salary: 600m,
            hasSubsidy: false));

        Assert.Equal(3, result.DiscountDays);
        Assert.Equal(60.00m, result.DiscountAmount); // 3 × 20, full day value.
        Assert.Equal(0, result.EmployerDays);
        Assert.Equal(0, result.SubsidizedDays);
        Assert.Empty(result.Warnings); // no cap reclassification — the risk simply has no subsidy.
        var detail = Assert.Single(result.TrancheDetails);
        Assert.Equal(IncapacityPayerCodes.SinPago, detail.PayerCode);
        Assert.Equal(0m, detail.SubsidyPercent);
    }

    // ── Border: cents salary → aggregates equal the sum of per-segment rounded amounts (no re-round) ─
    [Fact]
    public void Border_CentsSalary_AggregatesEqualSumOfRoundedSegments()
    {
        // 2026-01-05 Monday → 2026-01-08 Thursday (4 weekdays); salary 456.78 → daily 15.23.
        var result = IncapacityCalculationRules.Calculate(Input(
            new DateOnly(2026, 1, 5),
            new DateOnly(2026, 1, 8),
            CommonIllness,
            salary: 456.78m));

        Assert.Equal(15.23m, result.DailySalary);   // round(456.78 / 30) = round(15.226).
        Assert.Equal(34.27m, result.EmployerAmount); // round(3 × 15.23 × 75%) = round(34.2675).
        Assert.Equal(11.42m, result.SubsidyAmount);  // round(1 × 15.23 × 75%) = round(11.4225).

        // Property: each aggregate is the exact sum of its category's rounded segments — no global re-round.
        var segmentTotal = result.TrancheDetails.Sum(detail => detail.Amount);
        Assert.Equal(result.EmployerAmount + result.SubsidyAmount + result.DiscountAmount, segmentTotal);
        Assert.Equal(
            result.EmployerAmount,
            result.TrancheDetails.Where(d => d.PayerCode == IncapacityPayerCodes.Empresa).Sum(d => d.Amount));
        Assert.Equal(
            result.SubsidyAmount,
            result.TrancheDetails.Where(d => d.PayerCode == IncapacityPayerCodes.Isss).Sum(d => d.Amount));
    }

    // ── Border: open-ended incapacity (EndDate null) → deferred, no breakdown until closure (D-11) ──
    [Fact]
    public void Border_OpenEnded_IsDeferredWithoutBreakdown()
    {
        var result = IncapacityCalculationRules.Calculate(Input(
            new DateOnly(2026, 1, 5),
            end: null,
            CommonIllness,
            salary: 600m));

        Assert.True(result.IsDeferred);
        Assert.Equal(0, result.CalendarDays);
        Assert.Equal(0, result.ComputableDays);
        Assert.Empty(result.TrancheDetails);
        Assert.Empty(result.Warnings);
        Assert.Equal(20.00m, result.DailySalary); // salary snapshot is still resolved.
        Assert.Equal(0m, result.EmployerAmount);
    }

    // ── Border: single day (close date == start date) ──────────────────────────────────────────────
    [Fact]
    public void Border_SingleDay_StartEqualsEnd()
    {
        var day = new DateOnly(2026, 1, 5); // Monday.
        var result = IncapacityCalculationRules.Calculate(Input(day, day, CommonIllness, salary: 600m));

        Assert.Equal(1, result.CalendarDays);
        Assert.Equal(1, result.ComputableDays);
        Assert.Equal(1, result.EmployerDays);
        Assert.Equal(15.00m, result.EmployerAmount); // 1 × 20 × 75%.
    }

    // ── Border: partial cap — cap 2 against a 3-day EMPRESA tranche → 2 EMPRESA + 1 SIN_PAGO + warn ─
    [Fact]
    public void Border_PartialCap_SplitsEmpresaAndUnpaid_WithWarning()
    {
        // 2026-01-05 Monday → 2026-01-07 Wednesday (3 EMPRESA days), only 2 cap days remaining.
        var result = IncapacityCalculationRules.Calculate(Input(
            new DateOnly(2026, 1, 5),
            new DateOnly(2026, 1, 7),
            CommonIllness,
            salary: 600m,
            cap: 2m));

        Assert.Equal(2, result.EmployerDays);
        Assert.Equal(30.00m, result.EmployerAmount); // 2 × 20 × 75%.
        Assert.Equal(2, result.EmployerCapConsumed);
        Assert.Equal(1, result.DiscountDays);
        Assert.Equal(20.00m, result.DiscountAmount); // 1 × 20, full day value.
        Assert.Contains(result.Warnings, warning => warning.Code == IncapacityCalculationRules.WarningCapExhausted);
    }
}

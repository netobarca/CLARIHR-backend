using CLARIHR.Application.Features.PersonnelFiles.CompensatoryTime;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// The compensatory-time fund arithmetic's critical golden suite (PR-2, the REQ-002 wave gate) — the
/// RATIFIED Anexo A.4 cases encoded as blocking assertions. The module is 100% pure so these fully pin the
/// credited-hours factor, the balance and its running derivation, the debit/annul/cap supports, the
/// absence-hours suggestion and the settlement valuation (D-19, salario/30/8 hourly rate).
/// Reference country: El Salvador. Dates are 2026 (Jan 1, 2026 = Thursday; Mar 9, 2026 = Monday).
/// </summary>
public sealed class CompensatoryTimeRulesTests
{
    // ── A.4-1 / A.4-3: simple credit, accumulation, enjoyment and the anti-overdraft debit check ──────

    [Fact]
    public void CreditedHours_FactorOne_ReturnsWorkedHours()
    {
        Assert.Equal(3.00m, CompensatoryTimeRules.CreditedHours(hoursWorked: 3m, factor: 1.00m));
    }

    [Fact]
    public void Balance_AccumulatesCreditsMinusDebits()
    {
        // A.4-3: credits of 8 h + 4 h → 12; an 8 h absence → 4.
        var accumulated = CompensatoryTimeRules.Balance(totalCredited: 8m + 4m, totalDebited: 0m);
        Assert.Equal(12m, accumulated);

        var afterAbsence = CompensatoryTimeRules.Balance(totalCredited: 12m, totalDebited: 8m);
        Assert.Equal(4m, afterAbsence);
    }

    [Fact]
    public void ValidateDebit_WithinBalance_IsAllowed()
    {
        var check = CompensatoryTimeRules.ValidateDebit(balance: 12m, hoursToDebit: 8m);

        Assert.True(check.IsAllowed);
        Assert.Equal(0m, check.Shortfall);
    }

    [Fact]
    public void ValidateDebit_ExceedingBalance_FailsWithShortfall()
    {
        // A.4-1: debit 8 against balance 3 → fails, missing 5.00.
        var check = CompensatoryTimeRules.ValidateDebit(balance: 3m, hoursToDebit: 8m);

        Assert.False(check.IsAllowed);
        Assert.Equal(5.00m, check.Shortfall);
    }

    // ── A.4-4: factor 2.00 and the single Round2 rule (stable with conflicting cents) ─────────────────

    [Fact]
    public void CreditedHours_FactorTwo_DoublesWorkedHours()
    {
        Assert.Equal(8.00m, CompensatoryTimeRules.CreditedHours(hoursWorked: 4m, factor: 2.00m));
    }

    [Theory]
    [InlineData(2.5, 1.5, 3.75)]   // exact
    [InlineData(1.005, 1, 1.01)]   // half-up away-from-zero
    [InlineData(0.125, 1, 0.13)]   // half-up away-from-zero
    public void CreditedHours_RoundsHalfUpAwayFromZero(decimal worked, decimal factor, decimal expected)
    {
        Assert.Equal(expected, CompensatoryTimeRules.CreditedHours(worked, factor));
    }

    // ── A.4-5: MaxAnnullable — a credit annulment cannot drive the balance negative ───────────────────

    [Fact]
    public void MaxAnnullable_BlocksCreditAnnulmentThatWouldGoNegative()
    {
        // Balance 4 (credit 12 − absence 8). Annulling the 12 h credit removes 12 h → would go negative.
        var maxWithAbsenceLive = CompensatoryTimeRules.MaxAnnullable(balance: 4m);
        Assert.Equal(4m, maxWithAbsenceLive);
        Assert.True(12m > maxWithAbsenceLive); // annulling the 12 h credit violates.

        // After annulling the absence the balance is 12 → annulling the 12 h credit is now allowed.
        var maxAfterAbsenceAnnulled = CompensatoryTimeRules.MaxAnnullable(balance: 12m);
        Assert.Equal(12m, maxAfterAbsenceAnnulled);
        Assert.True(12m <= maxAfterAbsenceAnnulled);
    }

    // ── A.4-7: balance cap (P-10) ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void MaxCreditable_WithCap_LimitsToRemainingHeadroom()
    {
        // Cap 40, balance 38 → crediting 6 h violates; the max creditable is 2.00.
        var max = CompensatoryTimeRules.MaxCreditable(balance: 38m, cap: 40m);

        Assert.Equal(2.00m, max);
        Assert.True(6m > max);
    }

    [Fact]
    public void MaxCreditable_WithoutCap_IsUnlimited()
    {
        Assert.Null(CompensatoryTimeRules.MaxCreditable(balance: 38m, cap: null));
    }

    // ── A.4-8: absence-hours suggestion (calendar present vs degraded) ───────────────────────────────

    [Fact]
    public void SuggestAbsenceHours_ExcludesHolidayAndRestDay()
    {
        // Mon Mar 9 – Wed Mar 11, 2026: Tuesday Mar 10 is a holiday, Sunday is the rest day (not in range).
        var start = new DateOnly(2026, 3, 9);
        var end = new DateOnly(2026, 3, 11);
        var holidays = new HashSet<DateOnly> { new(2026, 3, 10) };

        var hours = CompensatoryTimeRules.SuggestAbsenceHours(
            start, end, DayOfWeek.Sunday, holidays, standardDailyHours: 8m);

        Assert.Equal(16m, hours); // 2 worked days × 8.
    }

    [Fact]
    public void SuggestAbsenceHours_DegradedWithoutCalendar_UsesCalendarDays()
    {
        // No calendar (rest day null + empty holidays): Mon–Wed → 3 days × 8 = 24.
        var start = new DateOnly(2026, 3, 9);
        var end = new DateOnly(2026, 3, 11);

        var hours = CompensatoryTimeRules.SuggestAbsenceHours(
            start, end, restDay: null, holidays: new HashSet<DateOnly>(), standardDailyHours: 8m);

        Assert.Equal(24m, hours);
    }

    // ── A.4-10: settlement valuation (D-19) ───────────────────────────────────────────────────────────

    [Fact]
    public void HourlyRate_DividesDailySalaryByStandardHours()
    {
        // Daily salary 20.00 (= 600 / 30), 8 h/day → 2.50.
        Assert.Equal(2.50m, CompensatoryTimeRules.HourlyRate(dailySalary: 20.00m, standardDailyHours: 8m));
    }

    [Fact]
    public void SettlementAmount_HoursTimesRateTimesFactor()
    {
        Assert.Equal(30.00m, CompensatoryTimeRules.SettlementAmount(hours: 12m, hourlyRate: 2.50m, rateFactor: 1.00m));
        Assert.Equal(60.00m, CompensatoryTimeRules.SettlementAmount(hours: 12m, hourlyRate: 2.50m, rateFactor: 2.00m));
    }

    // ── BuildStatement: chronological order, running balance, annulled excluded ──────────────────────

    [Fact]
    public void BuildStatement_OrdersChronologically_WithRunningBalanceAndTotals()
    {
        var movements = new List<CompensatoryTimeMovement>
        {
            // Deliberately out of order — the rule sorts by date then CreatedUtc.
            new(Guid.NewGuid(), new DateOnly(2026, 1, 20), Utc(2026, 1, 20, 9), CompensatoryTimeMovementKind.Absence, 8m, CompensatoryTimeStatuses.Registrada),
            new(Guid.NewGuid(), new DateOnly(2026, 1, 10), Utc(2026, 1, 10, 9), CompensatoryTimeMovementKind.Credit, 8m, CompensatoryTimeStatuses.Registrada),
            new(Guid.NewGuid(), new DateOnly(2026, 1, 15), Utc(2026, 1, 15, 9), CompensatoryTimeMovementKind.Credit, 4m, CompensatoryTimeStatuses.Registrada),
        };

        var statement = CompensatoryTimeRules.BuildStatement(movements);

        Assert.Equal(3, statement.Lines.Count);
        Assert.Equal(new DateOnly(2026, 1, 10), statement.Lines[0].Date);
        Assert.Equal(new DateOnly(2026, 1, 15), statement.Lines[1].Date);
        Assert.Equal(new DateOnly(2026, 1, 20), statement.Lines[2].Date);

        Assert.Equal(8m, statement.Lines[0].RunningBalance);   // +8
        Assert.Equal(12m, statement.Lines[1].RunningBalance);  // +4
        Assert.Equal(4m, statement.Lines[2].RunningBalance);   // −8

        Assert.Equal(-8m, statement.Lines[2].SignedHours);
        Assert.Equal(12m, statement.TotalCredited);
        Assert.Equal(8m, statement.TotalDebited);
        Assert.Equal(4m, statement.Balance);
    }

    [Fact]
    public void BuildStatement_MarksAndExcludesAnnulledMovements()
    {
        var movements = new List<CompensatoryTimeMovement>
        {
            new(Guid.NewGuid(), new DateOnly(2026, 1, 10), Utc(2026, 1, 10, 9), CompensatoryTimeMovementKind.Credit, 12m, CompensatoryTimeStatuses.Registrada),
            new(Guid.NewGuid(), new DateOnly(2026, 1, 12), Utc(2026, 1, 12, 9), CompensatoryTimeMovementKind.Credit, 5m, CompensatoryTimeStatuses.Anulada),
            new(Guid.NewGuid(), new DateOnly(2026, 1, 20), Utc(2026, 1, 20, 9), CompensatoryTimeMovementKind.Absence, 8m, CompensatoryTimeStatuses.Registrada),
        };

        var statement = CompensatoryTimeRules.BuildStatement(movements);

        Assert.True(statement.Lines[1].IsAnnulled);
        Assert.Equal(12m, statement.Lines[1].RunningBalance); // unchanged by the annulled credit.
        Assert.Equal(4m, statement.Lines[2].RunningBalance);  // 12 − 8.

        Assert.Equal(12m, statement.TotalCredited); // the 5 h annulled credit is excluded.
        Assert.Equal(8m, statement.TotalDebited);
        Assert.Equal(4m, statement.Balance);
    }

    [Fact]
    public void BuildStatement_AppliesOpeningBalanceOffset()
    {
        var movements = new List<CompensatoryTimeMovement>
        {
            new(Guid.NewGuid(), new DateOnly(2026, 2, 1), Utc(2026, 2, 1, 9), CompensatoryTimeMovementKind.Absence, 3m, CompensatoryTimeStatuses.Registrada),
        };

        var statement = CompensatoryTimeRules.BuildStatement(movements, openingBalance: 10m);

        Assert.Equal(7m, statement.Lines[0].RunningBalance); // 10 − 3.
        Assert.Equal(7m, statement.Balance);
    }

    private static DateTime Utc(int year, int month, int day, int hour) =>
        new(year, month, day, hour, 0, 0, DateTimeKind.Utc);
}

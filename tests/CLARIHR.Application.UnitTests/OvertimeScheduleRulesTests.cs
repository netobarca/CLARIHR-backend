using CLARIHR.Application.Features.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// H-19 + H-20 — the pure rules that make an overtime record's time range mean something. Until now
/// <c>StartTime</c>/<c>EndTime</c> were stored and echoed and nothing else: no comparison, no threshold, no
/// consumer. What the engine pays is <c>Σ(DurationDecimalHours × Factor)</c>, so a record could declare 8 hours
/// with a 10:00–11:00 range and be paid for eight while the authorizer saw one.
///
/// Reference country El Salvador, Labour Code art. 161: daytime work is <c>[06:00, 19:00)</c> and night work is
/// <c>[19:00, 06:00)</c>. The seeded factors follow from arts. 168/169 — HED 2.00, HEN 2.50 (= 2.00 × 1.25 for
/// night), and the holiday variants at double: HEDF 4.00, HENF 5.00.
///
/// The meal break is deliberately NOT carved out of the shift when testing overlap: its length varies by company
/// (1 h, 2 h) and the employee may take it at any agreed hour, so the stored window is nominal and cannot be
/// treated as "outside the shift". The shift is the envelope.
/// </summary>
public sealed class OvertimeScheduleRulesTests
{
    // ── The range itself (H-20: `EndTime > StartTime` was never checked) ────────────────────────────

    [Fact]
    public void DeriveDurationFromRange_NineToEleven_IsTwoHours()
    {
        var result = OvertimeScheduleRules.DeriveDurationFromRange(new TimeOnly(9, 0), new TimeOnly(11, 0));

        Assert.True(result.IsValid);
        Assert.Equal(2, result.Hours);
        Assert.Equal(0, result.Minutes);
    }

    [Fact]
    public void DeriveDurationFromRange_WithMinutes_KeepsThem()
    {
        var result = OvertimeScheduleRules.DeriveDurationFromRange(new TimeOnly(17, 15), new TimeOnly(19, 0));

        Assert.True(result.IsValid);
        Assert.Equal(1, result.Hours);
        Assert.Equal(45, result.Minutes);
    }

    /// <summary>A midnight crossing is legitimate for overtime, exactly as the work schedule already allows.</summary>
    [Fact]
    public void DeriveDurationFromRange_CrossingMidnight_SpansForward()
    {
        var result = OvertimeScheduleRules.DeriveDurationFromRange(new TimeOnly(22, 0), new TimeOnly(2, 0));

        Assert.True(result.IsValid);
        Assert.Equal(4, result.Hours);
        Assert.Equal(0, result.Minutes);
    }

    [Fact]
    public void DeriveDurationFromRange_EqualTimes_IsInvalid()
    {
        var result = OvertimeScheduleRules.DeriveDurationFromRange(new TimeOnly(9, 0), new TimeOnly(9, 0));

        Assert.False(result.IsValid);
        Assert.Equal(OvertimeScheduleRules.RangeEmptyCode, result.ErrorCode);
    }

    // ── Overlap with the shift (H-20: the double-payment path) ──────────────────────────────────────

    /// <summary>The finding's scenario: 09:00–11:00 on a Mon-Fri 08:00–17:00 shift is already paid as salary.</summary>
    [Fact]
    public void OverlapsShift_FullyInsideTheShift_IsTrue()
    {
        Assert.True(OvertimeScheduleRules.OverlapsShift(
            new TimeOnly(9, 0), new TimeOnly(11, 0), new TimeOnly(8, 0), new TimeOnly(17, 0)));
    }

    [Fact]
    public void OverlapsShift_StartingInsideAndEndingAfter_IsTrue()
    {
        // 16:30–19:00 has half an hour inside the shift; rejecting the whole record is the decision — trimming
        // silently would change what the person asked for.
        Assert.True(OvertimeScheduleRules.OverlapsShift(
            new TimeOnly(16, 30), new TimeOnly(19, 0), new TimeOnly(8, 0), new TimeOnly(17, 0)));
    }

    [Fact]
    public void OverlapsShift_StartingExactlyAtShiftEnd_IsFalse()
    {
        // 17:00–19:00 against a shift ending at 17:00: touching bounds do not overlap.
        Assert.False(OvertimeScheduleRules.OverlapsShift(
            new TimeOnly(17, 0), new TimeOnly(19, 0), new TimeOnly(8, 0), new TimeOnly(17, 0)));
    }

    [Fact]
    public void OverlapsShift_EndingExactlyAtShiftStart_IsFalse()
    {
        Assert.False(OvertimeScheduleRules.OverlapsShift(
            new TimeOnly(6, 0), new TimeOnly(8, 0), new TimeOnly(8, 0), new TimeOnly(17, 0)));
    }

    /// <summary>
    /// The nominal meal window is NOT a hole in the shift: the employee may take lunch at 11:00, 12:00, 13:00 or
    /// 14:00 by agreement, so 12:00–13:00 overtime on an 08:00–17:00 shift is inside the shift.
    /// </summary>
    [Fact]
    public void OverlapsShift_DuringTheNominalMealWindow_IsTrue()
    {
        Assert.True(OvertimeScheduleRules.OverlapsShift(
            new TimeOnly(12, 0), new TimeOnly(13, 0), new TimeOnly(8, 0), new TimeOnly(17, 0)));
    }

    /// <summary>A night shift 22:00–06:00 wraps midnight; an overtime record at 02:00 is inside it.</summary>
    [Fact]
    public void OverlapsShift_InsideAMidnightCrossingShift_IsTrue()
    {
        Assert.True(OvertimeScheduleRules.OverlapsShift(
            new TimeOnly(2, 0), new TimeOnly(4, 0), new TimeOnly(22, 0), new TimeOnly(6, 0)));
    }

    // ── Overlap between two records of the same day (H-20) ──────────────────────────────────────────

    [Fact]
    public void OverlapsRange_TwoPartlyOverlappingRecords_IsTrue()
    {
        // 14:00–18:00 and 16:00–20:00 sum 8 h against the daily cap while 2 h are paid twice.
        Assert.True(OvertimeScheduleRules.OverlapsRange(
            new TimeOnly(16, 0), new TimeOnly(20, 0), new TimeOnly(14, 0), new TimeOnly(18, 0)));
    }

    [Fact]
    public void OverlapsRange_BackToBackRecords_IsFalse()
    {
        Assert.False(OvertimeScheduleRules.OverlapsRange(
            new TimeOnly(18, 0), new TimeOnly(20, 0), new TimeOnly(14, 0), new TimeOnly(18, 0)));
    }

    // ── The legal day/night boundary (art. 161) ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(6, 0, 12, 0, true)]     // fully daytime
    [InlineData(9, 0, 11, 0, true)]
    [InlineData(18, 0, 18, 59, true)]   // the last daytime minute
    [InlineData(19, 0, 22, 0, false)]   // night starts exactly at 19:00
    [InlineData(22, 0, 2, 0, false)]    // crosses midnight, still night throughout
    [InlineData(3, 0, 5, 59, false)]
    public void ClassifyDaytime_WithinASingleLegalBand_ReportsIt(
        int startHour, int startMinute, int endHour, int endMinute, bool expectedDaytime)
    {
        var result = OvertimeScheduleRules.ClassifyLegalBand(
            new TimeOnly(startHour, startMinute), new TimeOnly(endHour, endMinute));

        Assert.True(result.IsWithinOneBand);
        Assert.Equal(expectedDaytime, result.IsDaytime);
    }

    /// <summary>
    /// 18:00–21:00 is 1 h daytime (×2.00) + 2 h night (×2.50) = 7.00 h-factor. One record carries ONE type, so
    /// either choice misprices it: all-HED pays 6.00 (short), all-HEN pays 7.50 (over). Rejected so the user
    /// splits it, which is the decision taken instead of auto-splitting.
    /// </summary>
    [Fact]
    public void ClassifyLegalBand_CrossingNineteenHundred_IsNotWithinOneBand()
    {
        var result = OvertimeScheduleRules.ClassifyLegalBand(new TimeOnly(18, 0), new TimeOnly(21, 0));

        Assert.False(result.IsWithinOneBand);
    }

    [Fact]
    public void ClassifyLegalBand_CrossingSixHundred_IsNotWithinOneBand()
    {
        var result = OvertimeScheduleRules.ClassifyLegalBand(new TimeOnly(4, 0), new TimeOnly(8, 0));

        Assert.False(result.IsWithinOneBand);
    }

    // ── Type derivation from the band + the holiday calendar ────────────────────────────────────────

    [Theory]
    [InlineData(true, false, "HED")]
    [InlineData(false, false, "HEN")]
    [InlineData(true, true, "HEDF")]
    [InlineData(false, true, "HENF")]
    public void DeriveTypeCode_FromBandAndHoliday_MatchesTheLegalMatrix(
        bool isDaytime, bool isRestOrHoliday, string expected)
    {
        Assert.Equal(expected, OvertimeScheduleRules.DeriveTypeCode(isDaytime, isRestOrHoliday));
    }
}

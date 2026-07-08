using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Domain guards of <see cref="PersonnelFileLactationPeriod"/> (vacaciones/incapacidades PR-3): the
/// required end date (a lactation period is never open-ended), the REGISTRADA-only lifecycle (no
/// EN_REVISION — the record reuses <see cref="IncapacityStatuses"/> minus the review state), the
/// <see cref="PersonnelFileLactationPeriod.ReplaceSchedules"/> invariants (containment in the period,
/// no overlaps, counts ≥ 1, sequential sort order) and the
/// <see cref="PersonnelFileLactationPeriod.UpdatePeriod"/> shrink guard over existing schedules.
/// </summary>
public sealed class LactationDomainTests
{
    private static readonly DateOnly Start = new(2026, 7, 1);
    private static readonly DateOnly End = new(2026, 12, 31);

    // ------------------------------------------------------------------
    // Create
    // ------------------------------------------------------------------

    [Fact]
    public void Create_ShouldStartRegistradaWithNoSchedules()
    {
        var period = CreatePeriod();

        Assert.Equal(IncapacityStatuses.Registrada, period.StatusCode);
        Assert.True(period.IsActive);
        Assert.Empty(period.Schedules);
        Assert.Equal(Start, period.StartDate);
        Assert.Equal(End, period.EndDate);
        Assert.NotEqual(Guid.Empty, period.PublicId);
        Assert.NotEqual(Guid.Empty, period.ConcurrencyToken);
    }

    [Fact]
    public void Create_WithEndDateBeforeStartDate_ShouldThrow() =>
        Assert.Throws<ArgumentException>(() => CreatePeriod(endDate: Start.AddDays(-1)));

    [Fact]
    public void Create_WithoutRequestedByUserId_ShouldThrow() =>
        Assert.Throws<ArgumentException>(() => CreatePeriod(requestedByUserId: " "));

    [Fact]
    public void Create_WithNonPositiveIncapacityTypeId_ShouldThrow() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => CreatePeriod(incapacityTypeId: 0));

    // ------------------------------------------------------------------
    // ReplaceSchedules
    // ------------------------------------------------------------------

    [Fact]
    public void ReplaceSchedules_WithValidRanges_ShouldReplaceSetWithSequentialSortOrder()
    {
        var period = CreatePeriod();
        var originalToken = period.ConcurrencyToken;

        // Deliberately unordered input: the guard re-sorts by start date before numbering.
        period.ReplaceSchedules(
        [
            new(Start.AddMonths(3), End, DailyPermitsCount: 1, MinutesPerPermit: 60),
            new(Start, Start.AddMonths(3).AddDays(-1), DailyPermitsCount: 2, MinutesPerPermit: 30),
        ]);

        Assert.Equal(2, period.Schedules.Count);

        var first = period.Schedules.First();
        Assert.Equal(Start, first.StartDate);
        Assert.Equal(2, first.DailyPermitsCount);
        Assert.Equal(30, first.MinutesPerPermit);
        Assert.Equal(1, first.SortOrder);

        var last = period.Schedules.Last();
        Assert.Equal(Start.AddMonths(3), last.StartDate);
        Assert.Equal(End, last.EndDate);
        Assert.Equal(2, last.SortOrder);

        Assert.NotEqual(originalToken, period.ConcurrencyToken);
    }

    [Fact]
    public void ReplaceSchedules_WithEmptySet_ShouldClearSchedules()
    {
        var period = CreatePeriod();
        period.ReplaceSchedules([new(Start, End, 1, 60)]);

        period.ReplaceSchedules([]);

        Assert.Empty(period.Schedules);
    }

    [Fact]
    public void ReplaceSchedules_StartingBeforePeriod_ShouldThrow()
    {
        var period = CreatePeriod();

        Assert.Throws<ArgumentException>(() =>
            period.ReplaceSchedules([new(Start.AddDays(-1), End, 1, 60)]));
    }

    [Fact]
    public void ReplaceSchedules_EndingAfterPeriod_ShouldThrow()
    {
        var period = CreatePeriod();

        Assert.Throws<ArgumentException>(() =>
            period.ReplaceSchedules([new(Start, End.AddDays(1), 1, 60)]));
    }

    [Fact]
    public void ReplaceSchedules_WithOverlappingRanges_ShouldThrow()
    {
        var period = CreatePeriod();

        Assert.Throws<ArgumentException>(() =>
            period.ReplaceSchedules(
            [
                new(Start, Start.AddMonths(2), 1, 60),
                new(Start.AddMonths(2), End, 2, 30), // starts on the previous range's end day
            ]));
    }

    [Fact]
    public void ReplaceSchedules_WithInvertedScheduleDates_ShouldThrow()
    {
        var period = CreatePeriod();

        Assert.Throws<ArgumentException>(() =>
            period.ReplaceSchedules([new(Start.AddDays(10), Start, 1, 60)]));
    }

    [Fact]
    public void ReplaceSchedules_WithZeroDailyPermits_ShouldThrow()
    {
        var period = CreatePeriod();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            period.ReplaceSchedules([new(Start, End, DailyPermitsCount: 0, MinutesPerPermit: 60)]));
    }

    [Fact]
    public void ReplaceSchedules_WithZeroMinutesPerPermit_ShouldThrow()
    {
        var period = CreatePeriod();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            period.ReplaceSchedules([new(Start, End, DailyPermitsCount: 1, MinutesPerPermit: 0)]));
    }

    [Fact]
    public void ReplaceSchedules_OnAnnulledPeriod_ShouldThrow()
    {
        var period = CreatePeriod();
        period.Annul("registro duplicado", DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            period.ReplaceSchedules([new(Start, End, 1, 60)]));
    }

    [Fact]
    public void ReplaceSchedules_WithNullCollection_ShouldThrow()
    {
        var period = CreatePeriod();

        Assert.Throws<ArgumentNullException>(() => period.ReplaceSchedules(null!));
    }

    // ------------------------------------------------------------------
    // UpdatePeriod
    // ------------------------------------------------------------------

    [Fact]
    public void UpdatePeriod_ShouldRewriteDatesAndNotesAndRotateToken()
    {
        var period = CreatePeriod();
        var originalToken = period.ConcurrencyToken;

        period.UpdatePeriod(Start.AddDays(5), End.AddDays(-5), "recalendarizado");

        Assert.Equal(Start.AddDays(5), period.StartDate);
        Assert.Equal(End.AddDays(-5), period.EndDate);
        Assert.Equal("recalendarizado", period.Notes);
        Assert.NotEqual(originalToken, period.ConcurrencyToken);
    }

    [Fact]
    public void UpdatePeriod_WithEndDateBeforeStart_ShouldThrow()
    {
        var period = CreatePeriod();

        Assert.Throws<ArgumentException>(() => period.UpdatePeriod(End, Start, null));
    }

    [Fact]
    public void UpdatePeriod_ShrinkingBelowExistingSchedules_ShouldThrow()
    {
        var period = CreatePeriod();
        period.ReplaceSchedules([new(Start, End, 1, 60)]);

        // Shrinking the period leaves the schedule out of range on both ends.
        Assert.Throws<InvalidOperationException>(() =>
            period.UpdatePeriod(Start.AddDays(10), End.AddDays(-10), null));
    }

    [Fact]
    public void UpdatePeriod_KeepingSchedulesContained_ShouldSucceed()
    {
        var period = CreatePeriod();
        period.ReplaceSchedules([new(Start.AddMonths(1), End.AddMonths(-1), 1, 60)]);

        period.UpdatePeriod(Start.AddDays(15), End.AddDays(-15), null);

        Assert.Equal(Start.AddDays(15), period.StartDate);
        Assert.Single(period.Schedules);
    }

    [Fact]
    public void UpdatePeriod_OnAnnulledPeriod_ShouldThrow()
    {
        var period = CreatePeriod();
        period.Annul("registro duplicado", DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => period.UpdatePeriod(Start, End, null));
    }

    // ------------------------------------------------------------------
    // Annul
    // ------------------------------------------------------------------

    [Fact]
    public void Annul_WithReason_ShouldSetAnuladaAndTurnOffIsActive()
    {
        var period = CreatePeriod();
        var originalToken = period.ConcurrencyToken;
        var annulledAt = new DateTime(2026, 7, 9, 12, 0, 0, DateTimeKind.Utc);

        period.Annul("registro duplicado", annulledAt);

        Assert.Equal(IncapacityStatuses.Anulada, period.StatusCode);
        Assert.Equal("registro duplicado", period.AnnulmentReason);
        Assert.Equal(annulledAt, period.AnnulledAtUtc);
        Assert.False(period.IsActive);
        Assert.NotEqual(originalToken, period.ConcurrencyToken);
    }

    [Fact]
    public void Annul_WithoutReason_ShouldThrow()
    {
        var period = CreatePeriod();

        Assert.Throws<ArgumentException>(() => period.Annul("  ", DateTime.UtcNow));
    }

    [Fact]
    public void Annul_OnAnnulledPeriod_ShouldThrow()
    {
        var period = CreatePeriod();
        period.Annul("registro duplicado", DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() => period.Annul("otra vez", DateTime.UtcNow));
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static PersonnelFileLactationPeriod CreatePeriod(
        string requestedByUserId = "user-1",
        long incapacityTypeId = 31,
        DateOnly? endDate = null) =>
        PersonnelFileLactationPeriod.Create(
            requesterFilePublicId: Guid.NewGuid(),
            requesterNameSnapshot: "Empleada Uno",
            requestedByUserId: requestedByUserId,
            incapacityTypeId: incapacityTypeId,
            startDate: Start,
            endDate: endDate ?? End,
            notes: null);
}

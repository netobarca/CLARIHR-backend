using CLARIHR.Domain.Leave;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Domain guards of the vacation aggregates (PR-7 builds the whole domain; the request/plan guards are wired by
/// PR-8/PR-9). Pins the fund period edits, the request lifecycle (approve/reject/cancel/return with the Σ and
/// accumulated-return invariants) and the plan no-overlap guard.
/// </summary>
public sealed class VacationDomainTests
{
    private static PersonnelFileVacationPeriod NewPeriod(int legal = 15, int benefit = 0, string source = "MANUAL") =>
        PersonnelFileVacationPeriod.Create(
            2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31),
            legal, benefit, generatesEnjoymentDays: true, usedAnniversary: false, source);

    private static PersonnelFileVacationRequest NewRequest(int days = 5) =>
        PersonnelFileVacationRequest.Create(
            Guid.NewGuid(), "Jane Doe", "user-1",
            new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 7), days, planLinePublicId: null, notes: null);

    [Fact]
    public void Period_Create_IsActiveWithTotalGrant()
    {
        var period = NewPeriod(legal: 15, benefit: 5);

        Assert.True(period.IsActive);
        Assert.Equal(20, period.TotalDaysGranted);
        Assert.Equal(VacationPeriodSources.Manual, period.SourceCode);
    }

    [Fact]
    public void Period_UpdateGrants_RejectsNonPositiveLegalDays()
    {
        var period = NewPeriod();
        Assert.Throws<ArgumentOutOfRangeException>(() => period.UpdateGrants(0, 0));
    }

    [Fact]
    public void Period_Deactivate_ClearsActiveFlag()
    {
        var period = NewPeriod();
        period.Deactivate();
        Assert.False(period.IsActive);
    }

    [Fact]
    public void Period_Create_RejectsUnknownSource()
    {
        Assert.Throws<ArgumentException>(() => NewPeriod(source: "NOPE"));
    }

    [Fact]
    public void Request_Create_IsBornSolicitada()
    {
        var request = NewRequest();
        Assert.Equal(VacationRequestStatuses.Solicitada, request.StatusCode);
        Assert.Equal(0, request.ConsumedDays);
    }

    [Fact]
    public void Request_Approve_RequiresAllocationsToSumRequestedDays()
    {
        var request = NewRequest(days: 5);

        Assert.Throws<InvalidOperationException>(() =>
            request.Approve([new VacationAllocationInput(1, 3)], "hr-user", DateTime.UtcNow));

        request.Approve([new VacationAllocationInput(1, 2), new VacationAllocationInput(2, 3)], "hr-user", DateTime.UtcNow);

        Assert.Equal(VacationRequestStatuses.Aprobada, request.StatusCode);
        Assert.Equal(5, request.ConsumedDays);
        Assert.Equal(2, request.Allocations.Count);
    }

    [Fact]
    public void Request_Approve_OnlyFromSolicitada()
    {
        var request = NewRequest(days: 3);
        request.Reject("hr-user", DateTime.UtcNow, "no");

        Assert.Throws<InvalidOperationException>(() =>
            request.Approve([new VacationAllocationInput(1, 3)], "hr-user", DateTime.UtcNow));
    }

    [Fact]
    public void Request_Cancel_OnlyFromSolicitada()
    {
        var request = NewRequest(days: 3);
        request.Cancel();
        Assert.Equal(VacationRequestStatuses.Anulada, request.StatusCode);
        Assert.False(request.IsActive);
    }

    [Fact]
    public void Request_Return_PartialThenFull_WalksThroughDevueltaParcialToDevuelta()
    {
        var request = NewRequest(days: 5);
        request.Approve([new VacationAllocationInput(1, 5)], "hr-user", DateTime.UtcNow);

        request.Return(2, "trip cancelled", "hr-user", DateTime.UtcNow, [new VacationReturnDistributionInput(1, 2)]);
        Assert.Equal(VacationRequestStatuses.DevueltaParcial, request.StatusCode);
        Assert.Equal(2, request.ReturnedDays);
        Assert.Equal(3, request.NetConsumedDays);

        request.Return(3, null, "hr-user", DateTime.UtcNow, [new VacationReturnDistributionInput(1, 3)]);
        Assert.Equal(VacationRequestStatuses.Devuelta, request.StatusCode);
        Assert.Equal(0, request.NetConsumedDays);
    }

    [Fact]
    public void Request_Return_CannotExceedConsumed()
    {
        var request = NewRequest(days: 4);
        request.Approve([new VacationAllocationInput(1, 4)], "hr-user", DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            request.Return(5, null, "hr-user", DateTime.UtcNow, [new VacationReturnDistributionInput(1, 5)]));
    }

    [Fact]
    public void Request_Return_DistributionMustSumToReturnedDays()
    {
        var request = NewRequest(days: 4);
        request.Approve([new VacationAllocationInput(1, 4)], "hr-user", DateTime.UtcNow);

        Assert.Throws<InvalidOperationException>(() =>
            request.Return(2, null, "hr-user", DateTime.UtcNow, [new VacationReturnDistributionInput(1, 1)]));
    }

    [Fact]
    public void Request_Return_OnlyFromApprovedOrPartiallyReturned()
    {
        var request = NewRequest(days: 3);

        Assert.Throws<InvalidOperationException>(() =>
            request.Return(1, null, "hr-user", DateTime.UtcNow, [new VacationReturnDistributionInput(1, 1)]));
    }

    [Fact]
    public void Plan_ReplaceLines_RejectsOverlappingLinesForSameEmployee()
    {
        var plan = VacationPlan.Create(2026, new DateOnly(2026, 1, 5), "user-1", "HR Admin");
        var employee = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => plan.ReplaceLines(
        [
            new VacationPlanLineInput(employee, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 10), 8),
            new VacationPlanLineInput(employee, new DateOnly(2026, 7, 8), new DateOnly(2026, 7, 15), 6),
        ]));
    }

    [Fact]
    public void Plan_ReplaceLines_AllowsNonOverlappingLines_AndAnnul()
    {
        var plan = VacationPlan.Create(2026, new DateOnly(2026, 1, 5), "user-1", "HR Admin");
        var employee = Guid.NewGuid();

        plan.ReplaceLines(
        [
            new VacationPlanLineInput(employee, new DateOnly(2026, 7, 1), new DateOnly(2026, 7, 10), 8),
            new VacationPlanLineInput(employee, new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 5), 4),
        ]);
        Assert.Equal(2, plan.Lines.Count);

        plan.Annul();
        Assert.Equal(VacationPlanStatuses.Anulado, plan.StatusCode);
        Assert.False(plan.IsActive);
    }
}

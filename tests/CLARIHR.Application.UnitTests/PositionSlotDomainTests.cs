using CLARIHR.Application.Features.PositionSlots;
using CLARIHR.Domain.PositionSlots;

namespace CLARIHR.Application.UnitTests;

public sealed class PositionSlotDomainTests
{
    [Fact]
    public void PositionSlot_Create_ShouldNormalizeCodeAndSetStatus()
    {
        var slot = PositionSlot.Create(
            code: "  ps-001  ",
            title: "  Plaza Analista  ",
            jobProfileId: 1,
            workCenterId: null,
            directDependencyPositionSlotId: null,
            functionalDependencyPositionSlotId: null,
            status: PositionSlotStatus.Vacant,
            maxEmployees: 2,
            occupiedEmployees: 0,
            isFixedTerm: false,
            effectiveFromUtc: DateTime.UtcNow.Date,
            effectiveToUtc: null,
            notes: "  note  ");

        Assert.Equal("PS-001", slot.Code);
        Assert.Equal("PS-001", slot.NormalizedCode);
        Assert.Equal(PositionSlotStatus.Vacant, slot.Status);
        Assert.True(slot.IsActive);
    }

    [Fact]
    public void PositionSlot_UpdateOccupancy_ShouldRecalculateStatus()
    {
        var slot = PositionSlot.Create(
            code: "PS-001",
            title: "Plaza",
            jobProfileId: 1,
            workCenterId: null,
            directDependencyPositionSlotId: null,
            functionalDependencyPositionSlotId: null,
            status: PositionSlotStatus.Vacant,
            maxEmployees: 2,
            occupiedEmployees: 0,
            isFixedTerm: false,
            effectiveFromUtc: DateTime.UtcNow.Date,
            effectiveToUtc: null,
            notes: null);

        var beforeToken = slot.ConcurrencyToken;

        slot.UpdateOccupancy(1);

        Assert.Equal(PositionSlotStatus.Occupied, slot.Status);
        Assert.Equal(1, slot.OccupiedEmployees);
        Assert.NotEqual(beforeToken, slot.ConcurrencyToken);
    }

    [Fact]
    public void PositionSlot_UpdateOccupancy_WhenSuspended_ShouldThrow()
    {
        var slot = PositionSlot.Create(
            code: "PS-001",
            title: "Plaza",
            jobProfileId: 1,
            workCenterId: null,
            directDependencyPositionSlotId: null,
            functionalDependencyPositionSlotId: null,
            status: PositionSlotStatus.Vacant,
            maxEmployees: 2,
            occupiedEmployees: 0,
            isFixedTerm: false,
            effectiveFromUtc: DateTime.UtcNow.Date,
            effectiveToUtc: null,
            notes: null);

        slot.ChangeStatus(PositionSlotStatus.Suspended);

        Assert.Throws<InvalidOperationException>(() => slot.UpdateOccupancy(1));
    }

    [Fact]
    public void PositionSlot_ChangeStatus_ShouldAutoCorrectOccupancy()
    {
        var slot = PositionSlot.Create(
            code: "PS-001",
            title: "Plaza",
            jobProfileId: 1,
            workCenterId: null,
            directDependencyPositionSlotId: null,
            functionalDependencyPositionSlotId: null,
            status: PositionSlotStatus.Occupied,
            maxEmployees: 2,
            occupiedEmployees: 1,
            isFixedTerm: false,
            effectiveFromUtc: DateTime.UtcNow.Date,
            effectiveToUtc: null,
            notes: null);

        slot.ChangeStatus(PositionSlotStatus.Vacant);

        Assert.Equal(PositionSlotStatus.Vacant, slot.Status);
        Assert.Equal(0, slot.OccupiedEmployees);

        slot.ChangeStatus(PositionSlotStatus.Occupied);

        Assert.Equal(PositionSlotStatus.Occupied, slot.Status);
        Assert.Equal(1, slot.OccupiedEmployees);
    }

    [Fact]
    public void PositionSlotDependencyAnalyzer_WouldCreateDirectCycle_ShouldReturnTrue()
    {
        var a = new PositionSlotGraphNodeData(
            1,
            Guid.NewGuid(),
            "PS-A",
            "A",
            PositionSlotStatus.Vacant,
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            true);

        var b = new PositionSlotGraphNodeData(
            2,
            Guid.NewGuid(),
            "PS-B",
            "B",
            PositionSlotStatus.Vacant,
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            1,
            a.Id,
            null,
            null,
            null,
            null,
            true);

        var c = new PositionSlotGraphNodeData(
            3,
            Guid.NewGuid(),
            "PS-C",
            "C",
            PositionSlotStatus.Vacant,
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            2,
            b.Id,
            null,
            null,
            null,
            null,
            true);

        var graph = new[] { a, b, c }.ToDictionary(static node => node.InternalId);

        var createsCycle = PositionSlotDependencyAnalyzer.WouldCreateDirectCycle(
            sourceInternalId: 1,
            candidateDirectDependencyInternalId: 3,
            graph);

        Assert.True(createsCycle);
    }
}

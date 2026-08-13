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
            roleId: null,
            workCenterId: null,
            directDependencyPositionSlotId: null,
            functionalDependencyPositionSlotId: null,
            status: PositionSlotStatus.Vacant,
            maxEmployees: 2,
            isFixedTerm: false,
            generatesOvertime: true,
            effectiveFromUtc: DateTime.UtcNow.Date,
            effectiveToUtc: null,
            notes: "  note  ");

        Assert.Equal("PS-001", slot.Code);
        Assert.Equal("PS-001", slot.NormalizedCode);
        // H-23 — a non-suspended status only means the slot is in force; Vacant/Occupied are derived on read.
        Assert.True(slot.IsActive);
    }

    /// <summary>
    /// H-23 — the four tests that used to live here pinned the occupancy counter and its coercions:
    /// `UpdateOccupancy` recalculating the status, its refusal while suspended, the create-time
    /// status/occupancy contradiction, and `ChangeStatus` AUTO-CORRECTING the counter (Occupied fabricated a `1`).
    /// All four described a number no writer maintained and that the HR dashboard was adding up. The occupant
    /// count and Vacant/Occupied are now derived from the active assignments, so there is nothing left to
    /// coerce — the behaviour is covered end to end in ApiIntegrationTests.PositionSlotOccupancy.cs.
    /// </summary>
    [Fact]
    public void PositionSlot_ChangeStatus_ShouldOnlyDecideWhetherTheSlotIsInForce()
    {
        var slot = PositionSlot.Create(
            "PS-STATUS", "Plaza", jobProfileId: 10, roleId: null, workCenterId: null,
            directDependencyPositionSlotId: null, functionalDependencyPositionSlotId: null,
            status: PositionSlotStatus.Vacant, maxEmployees: 2, isFixedTerm: false, generatesOvertime: true,
            effectiveFromUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), effectiveToUtc: null, notes: null);

        Assert.True(slot.IsActive);

        slot.ChangeStatus(PositionSlotStatus.Suspended);
        Assert.False(slot.IsActive);

        // Asking for Occupied cannot invent an occupant any more: it just puts the slot back in force.
        slot.ChangeStatus(PositionSlotStatus.Occupied);
        Assert.True(slot.IsActive);
    }

    [Fact]
    public void PositionSlotDependencyAnalyzer_WouldCreateDirectCycle_ShouldReturnTrue()
    {
        // Direct chain c(3) -> b(2) -> a(1). Making a(1) depend on c(3) would close an a->c->b->a cycle.
        var adjacency = new[]
        {
            new PositionSlotDependencyAdjacency(1, DirectDependencyInternalId: null, FunctionalDependencyInternalId: null),
            new PositionSlotDependencyAdjacency(2, DirectDependencyInternalId: 1, FunctionalDependencyInternalId: null),
            new PositionSlotDependencyAdjacency(3, DirectDependencyInternalId: 2, FunctionalDependencyInternalId: null),
        }.ToDictionary(static node => node.InternalId);

        var createsCycle = PositionSlotDependencyAnalyzer.WouldCreateDirectCycle(
            sourceInternalId: 1,
            candidateInternalId: 3,
            adjacency);

        Assert.True(createsCycle);
    }

    [Fact]
    public void PositionSlotDependencyAnalyzer_WouldCreateFunctionalCycle_ShouldReturnTrue()
    {
        // PS-D: the functional chain is validated symmetrically with the direct chain.
        var adjacency = new[]
        {
            new PositionSlotDependencyAdjacency(1, DirectDependencyInternalId: null, FunctionalDependencyInternalId: null),
            new PositionSlotDependencyAdjacency(2, DirectDependencyInternalId: null, FunctionalDependencyInternalId: 1),
            new PositionSlotDependencyAdjacency(3, DirectDependencyInternalId: null, FunctionalDependencyInternalId: 2),
        }.ToDictionary(static node => node.InternalId);

        var createsCycle = PositionSlotDependencyAnalyzer.WouldCreateFunctionalCycle(
            sourceInternalId: 1,
            candidateInternalId: 3,
            adjacency);

        Assert.True(createsCycle);
    }

    [Fact]
    public void PositionSlotDependencyAnalyzer_WouldCreateFunctionalCycle_WhenOnlyDirectChain_ShouldReturnFalse()
    {
        // Relation types are independent: a direct-dependency chain must NOT trip the functional check.
        var adjacency = new[]
        {
            new PositionSlotDependencyAdjacency(1, DirectDependencyInternalId: null, FunctionalDependencyInternalId: null),
            new PositionSlotDependencyAdjacency(2, DirectDependencyInternalId: 1, FunctionalDependencyInternalId: null),
            new PositionSlotDependencyAdjacency(3, DirectDependencyInternalId: 2, FunctionalDependencyInternalId: null),
        }.ToDictionary(static node => node.InternalId);

        var createsCycle = PositionSlotDependencyAnalyzer.WouldCreateFunctionalCycle(
            sourceInternalId: 1,
            candidateInternalId: 3,
            adjacency);

        Assert.False(createsCycle);
    }
}

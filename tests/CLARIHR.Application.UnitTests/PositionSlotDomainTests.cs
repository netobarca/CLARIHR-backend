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
            roleId: null,
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
            roleId: null,
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

        var exception = Assert.Throws<PositionSlotDomainException>(() => slot.UpdateOccupancy(1));
        Assert.Equal(PositionSlotDomainErrorCode.SuspendedOccupancyConflict, exception.Code);
    }

    [Fact]
    public void PositionSlot_Create_WhenVacantWithOccupants_ShouldThrowStatusOccupancyMismatch()
    {
        var exception = Assert.Throws<PositionSlotDomainException>(() => PositionSlot.Create(
            code: "PS-001",
            title: "Plaza",
            jobProfileId: 1,
            roleId: null,
            workCenterId: null,
            directDependencyPositionSlotId: null,
            functionalDependencyPositionSlotId: null,
            status: PositionSlotStatus.Vacant,
            maxEmployees: 5,
            occupiedEmployees: 3,
            isFixedTerm: false,
            effectiveFromUtc: DateTime.UtcNow.Date,
            effectiveToUtc: null,
            notes: null));

        // §PS6: create no longer silently coerces occupancy to 0 — it rejects the contradiction.
        Assert.Equal(PositionSlotDomainErrorCode.StatusOccupancyMismatch, exception.Code);
    }

    [Fact]
    public void PositionSlot_Create_WhenOccupiedWithZeroOccupants_ShouldThrowStatusOccupancyMismatch()
    {
        var exception = Assert.Throws<PositionSlotDomainException>(() => PositionSlot.Create(
            code: "PS-001",
            title: "Plaza",
            jobProfileId: 1,
            roleId: null,
            workCenterId: null,
            directDependencyPositionSlotId: null,
            functionalDependencyPositionSlotId: null,
            status: PositionSlotStatus.Occupied,
            maxEmployees: 5,
            occupiedEmployees: 0,
            isFixedTerm: false,
            effectiveFromUtc: DateTime.UtcNow.Date,
            effectiveToUtc: null,
            notes: null));

        // §PS6: create no longer silently coerces occupancy to 1 — it rejects the contradiction.
        Assert.Equal(PositionSlotDomainErrorCode.StatusOccupancyMismatch, exception.Code);
    }

    // §PS6: ChangeStatus is a status-only transition (no caller-supplied occupancy to
    // contradict), so it KEEPS the intentional auto-correction — distinct from Create.
    [Fact]
    public void PositionSlot_ChangeStatus_ShouldAutoCorrectOccupancy()
    {
        var slot = PositionSlot.Create(
            code: "PS-001",
            title: "Plaza",
            jobProfileId: 1,
            roleId: null,
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

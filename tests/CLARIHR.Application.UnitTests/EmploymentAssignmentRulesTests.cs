using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.PositionSlots;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the pure multi-position ("múltiples plazas") rule engine
/// <see cref="EmploymentAssignmentRules"/>: single active primary with auto-degrade (RF-002),
/// same-slot dedup + overlapping vigencia (RF-007), and capacity-by-vigencia + slot assignability (RF-005).
/// </summary>
public sealed class EmploymentAssignmentRulesTests
{
    private static readonly DateTime SlotFrom = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Start = new(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);
    private static readonly Guid SlotA = Guid.NewGuid();
    private static readonly Guid SlotB = Guid.NewGuid();

    private static EmploymentAssignmentRules.Candidate Candidate(
        Guid? slot = null,
        bool isPrimary = true,
        bool isActive = true,
        Guid? publicId = null,
        DateTime? start = null,
        DateTime? end = null) =>
        new(publicId, slot, start ?? Start, end, isPrimary, isActive);

    private static EmploymentAssignmentRules.ExistingAssignment Existing(
        Guid? slot = null,
        bool isPrimary = false,
        bool isActive = true,
        Guid? publicId = null,
        DateTime? start = null,
        DateTime? end = null) =>
        new(publicId ?? Guid.NewGuid(), slot, start ?? Start, end, isPrimary, isActive);

    private static EmploymentAssignmentRules.PositionSlotFacts Slot(
        bool exists = true,
        PositionSlotStatus status = PositionSlotStatus.Vacant,
        int max = 2,
        int overlapping = 0,
        DateTime? from = null,
        DateTime? to = null) =>
        new(exists, status, from ?? SlotFrom, to, max, overlapping);

    [Fact]
    public void Evaluate_NewActivePrimary_NoOthers_SucceedsWithoutDemotion()
    {
        var result = EmploymentAssignmentRules.Evaluate(Candidate(SlotA), [], Slot());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.PrimariesToDemote);
    }

    [Fact]
    public void Evaluate_NewActivePrimary_WithExistingActivePrimary_DemotesExisting()
    {
        var existingPrimaryId = Guid.NewGuid();
        var others = new[] { Existing(SlotB, isPrimary: true, isActive: true, publicId: existingPrimaryId) };

        var result = EmploymentAssignmentRules.Evaluate(Candidate(SlotA), others, Slot());

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { existingPrimaryId }, result.Value.PrimariesToDemote);
    }

    [Fact]
    public void Evaluate_NewActiveSecondary_DoesNotDemote()
    {
        var others = new[] { Existing(SlotB, isPrimary: true, isActive: true) };

        var result = EmploymentAssignmentRules.Evaluate(Candidate(SlotA, isPrimary: false), others, Slot());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.PrimariesToDemote);
    }

    [Fact]
    public void Evaluate_ActivePrimary_DemotesOnlyOtherActivePrimaries()
    {
        var activePrimary = Guid.NewGuid();
        var others = new[]
        {
            Existing(SlotB, isPrimary: true, isActive: true, publicId: activePrimary),
            Existing(Guid.NewGuid(), isPrimary: true, isActive: false), // inactive primary — left alone
            Existing(Guid.NewGuid(), isPrimary: false, isActive: true), // active secondary — left alone
        };

        var result = EmploymentAssignmentRules.Evaluate(Candidate(SlotA), others, Slot());

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { activePrimary }, result.Value.PrimariesToDemote);
    }

    [Fact]
    public void Evaluate_InactiveCandidate_SkipsSlotAndCapacityChecks()
    {
        // Inactive assignment to a full, suspended, non-existent-fact slot still succeeds: it does not occupy.
        var result = EmploymentAssignmentRules.Evaluate(
            Candidate(SlotA, isPrimary: false, isActive: false),
            [],
            slot: null);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.PrimariesToDemote);
    }

    [Fact]
    public void Evaluate_SlotNotFound_Fails()
    {
        var result = EmploymentAssignmentRules.Evaluate(Candidate(SlotA), [], Slot(exists: false));

        Assert.True(result.IsFailure);
        Assert.Equal("EMPLOYMENT_ASSIGNMENT_POSITION_SLOT_NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public void Evaluate_SlotNullFacts_Fails()
    {
        var result = EmploymentAssignmentRules.Evaluate(Candidate(SlotA), [], slot: null);

        Assert.True(result.IsFailure);
        Assert.Equal("EMPLOYMENT_ASSIGNMENT_POSITION_SLOT_NOT_FOUND", result.Error.Code);
    }

    [Fact]
    public void Evaluate_SuspendedSlot_NotAssignable()
    {
        var result = EmploymentAssignmentRules.Evaluate(Candidate(SlotA), [], Slot(status: PositionSlotStatus.Suspended));

        Assert.True(result.IsFailure);
        Assert.Equal("EMPLOYMENT_ASSIGNMENT_POSITION_SLOT_NOT_ASSIGNABLE", result.Error.Code);
    }

    [Fact]
    public void Evaluate_CandidateBeforeSlotEffectiveFrom_NotAssignable()
    {
        var candidate = Candidate(SlotA, start: new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = EmploymentAssignmentRules.Evaluate(candidate, [], Slot());

        Assert.True(result.IsFailure);
        Assert.Equal("EMPLOYMENT_ASSIGNMENT_POSITION_SLOT_NOT_ASSIGNABLE", result.Error.Code);
    }

    [Fact]
    public void Evaluate_CandidateAfterSlotEffectiveTo_NotAssignable()
    {
        var candidate = Candidate(SlotA, start: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));
        var slot = Slot(to: new DateTime(2026, 8, 31, 0, 0, 0, DateTimeKind.Utc));

        var result = EmploymentAssignmentRules.Evaluate(candidate, [], slot);

        Assert.True(result.IsFailure);
        Assert.Equal("EMPLOYMENT_ASSIGNMENT_POSITION_SLOT_NOT_ASSIGNABLE", result.Error.Code);
    }

    [Fact]
    public void Evaluate_DuplicateActiveSameSlot_NonOverlapping_Fails()
    {
        // Existing active on SlotA ends before the candidate starts (no overlap) — still a duplicate active slot.
        var others = new[]
        {
            Existing(SlotA, isActive: true,
                start: new DateTime(2025, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                end: new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc)),
        };

        var result = EmploymentAssignmentRules.Evaluate(Candidate(SlotA), others, Slot());

        Assert.True(result.IsFailure);
        Assert.Equal("EMPLOYMENT_ASSIGNMENT_DUPLICATE_POSITION_SLOT", result.Error.Code);
    }

    [Fact]
    public void Evaluate_OverlappingActiveSameSlot_Fails()
    {
        var others = new[] { Existing(SlotA, isActive: true, start: Start, end: End) };

        var result = EmploymentAssignmentRules.Evaluate(Candidate(SlotA, start: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc)), others, Slot());

        Assert.True(result.IsFailure);
        Assert.Equal("EMPLOYMENT_ASSIGNMENT_OVERLAPPING_DATES", result.Error.Code);
    }

    [Fact]
    public void Evaluate_OverlapOnDifferentSlot_IsAllowed()
    {
        // Working two different plazas in the same period is the whole point of multi-plaza (P-08).
        var others = new[] { Existing(SlotB, isActive: true, start: Start, end: End) };

        var result = EmploymentAssignmentRules.Evaluate(Candidate(SlotA, start: Start, end: End), others, Slot());

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Evaluate_CapacityFull_Fails()
    {
        var result = EmploymentAssignmentRules.Evaluate(Candidate(SlotA), [], Slot(max: 2, overlapping: 2));

        Assert.True(result.IsFailure);
        Assert.Equal("EMPLOYMENT_ASSIGNMENT_CAPACITY_EXCEEDED", result.Error.Code);
    }

    [Fact]
    public void Evaluate_CapacityAvailable_Succeeds()
    {
        var result = EmploymentAssignmentRules.Evaluate(Candidate(SlotA), [], Slot(max: 2, overlapping: 1));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Evaluate_UpdateExcludesSelfRow_NotFlaggedAsDuplicate()
    {
        // Editing an existing assignment: its own row (same PublicId, same slot) must not count as a duplicate.
        var selfId = Guid.NewGuid();
        var others = new[] { Existing(SlotA, isActive: true, publicId: selfId, start: Start, end: End) };

        var result = EmploymentAssignmentRules.Evaluate(
            Candidate(SlotA, publicId: selfId, start: Start, end: End),
            others,
            Slot());

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData("2026-01-01", "2026-06-30", "2026-06-01", "2026-12-31", true)]   // partial overlap
    [InlineData("2026-01-01", "2026-03-31", "2026-04-01", "2026-06-30", false)]  // adjacent, no overlap
    [InlineData("2026-01-01", null, "2026-09-01", "2026-12-31", true)]           // open-ended A covers B
    [InlineData("2026-01-01", "2026-02-01", "2026-09-01", null, false)]          // open-ended B after A
    public void RangesOverlap_Cases(string aStart, string? aEnd, string bStart, string? bEnd, bool expected)
    {
        var result = EmploymentAssignmentRules.RangesOverlap(
            DateTime.Parse(aStart),
            aEnd is null ? null : DateTime.Parse(aEnd),
            DateTime.Parse(bStart),
            bEnd is null ? null : DateTime.Parse(bEnd));

        Assert.Equal(expected, result);
    }
}

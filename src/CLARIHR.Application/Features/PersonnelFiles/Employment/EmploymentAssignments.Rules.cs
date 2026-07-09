using CLARIHR.Application.Common.Errors;
using CLARIHR.Domain.PositionSlots;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Coded errors for the multi-position ("múltiples plazas") rules enforced on employment assignments.
/// Every code must have a matching entry in BackendMessages.resx and BackendMessages.es.resx
/// (parity is enforced by <c>BackendMessageLocalizationTests</c>).
/// </summary>
internal static class EmploymentAssignmentErrors
{
    public static readonly Error PositionSlotRequired = new(
        "EMPLOYMENT_ASSIGNMENT_POSITION_SLOT_REQUIRED",
        "A position slot is required for an employment assignment.",
        ErrorType.UnprocessableEntity);

    public static readonly Error PositionSlotNotFound = new(
        "EMPLOYMENT_ASSIGNMENT_POSITION_SLOT_NOT_FOUND",
        "The selected position slot could not be found.",
        ErrorType.NotFound);

    public static readonly Error PositionSlotNotAssignable = new(
        "EMPLOYMENT_ASSIGNMENT_POSITION_SLOT_NOT_ASSIGNABLE",
        "The selected position slot is suspended or outside its effective dates and cannot be assigned.",
        ErrorType.UnprocessableEntity);

    public static readonly Error CapacityExceeded = new(
        "EMPLOYMENT_ASSIGNMENT_CAPACITY_EXCEEDED",
        "The selected position slot has no available capacity for the requested period.",
        ErrorType.UnprocessableEntity);

    public static readonly Error DuplicatePositionSlot = new(
        "EMPLOYMENT_ASSIGNMENT_DUPLICATE_POSITION_SLOT",
        "The employee already has an active assignment to this position slot.",
        ErrorType.Conflict);

    public static readonly Error OverlappingDates = new(
        "EMPLOYMENT_ASSIGNMENT_OVERLAPPING_DATES",
        "The employee already has an assignment to this position slot with an overlapping effective period.",
        ErrorType.Conflict);

    public static readonly Error PrimaryRequired = new(
        "EMPLOYMENT_ASSIGNMENT_PRIMARY_REQUIRED",
        "The employee must keep one primary assignment; designate another primary before removing this one.",
        ErrorType.UnprocessableEntity);

    public static readonly Error TypeCodeInvalid = new(
        "EMPLOYMENT_ASSIGNMENT_TYPE_CODE_INVALID",
        "The assignment type code is not valid for the active catalog.",
        ErrorType.UnprocessableEntity);

    // REQ-004: the optional payrollTypeCode (contractual pay modality) must resolve to an ACTIVE item of the
    // country-scoped payroll-types catalog when supplied. Distinct from contractTypeCode (contract nature).
    public static readonly Error PayrollTypeCodeInvalid = new(
        "PAYROLL_TYPE_INVALID",
        "The payroll type code is not valid for the active catalog.",
        ErrorType.UnprocessableEntity);
}

/// <summary>
/// Pure, stateful business rules for the multi-position model. The handlers load the employee's other
/// assignments and the resolved position-slot facts (cross-feature), then call <see cref="Evaluate"/>;
/// keeping the rules in a pure module mirrors <see cref="PersonnelFileEmploymentAssignmentPatchApplier"/>
/// and makes every invariant unit-testable without a database.
/// </summary>
internal static class EmploymentAssignmentRules
{
    /// <summary>One of the employee's existing assignments (the candidate row itself may be included; it is excluded by PublicId).</summary>
    internal sealed record ExistingAssignment(
        Guid PublicId,
        Guid? PositionSlotPublicId,
        DateTime StartDate,
        DateTime? EndDate,
        bool IsPrimary,
        bool IsActive);

    /// <summary>The proposed assignment after applying the command (PublicId is null on Add).</summary>
    internal sealed record Candidate(
        Guid? PublicId,
        Guid? PositionSlotPublicId,
        DateTime StartDate,
        DateTime? EndDate,
        bool IsPrimary,
        bool IsActive);

    /// <summary>Facts about the referenced slot, resolved by the handler from the PositionSlots feature.</summary>
    /// <param name="OverlappingActiveCountExcludingSelf">
    /// Count of active assignments (across all employees) on this slot whose effective window overlaps the
    /// candidate's window, excluding the candidate's own row. Used for the capacity-by-vigencia check.
    /// </param>
    internal sealed record PositionSlotFacts(
        bool Exists,
        PositionSlotStatus Status,
        DateTime EffectiveFromUtc,
        DateTime? EffectiveToUtc,
        int MaxEmployees,
        int OverlappingActiveCountExcludingSelf);

    /// <summary>Outcome of a successful evaluation: the other active primaries that must be demoted (auto-degrade, P-03).</summary>
    internal sealed record Evaluation(IReadOnlyCollection<Guid> PrimariesToDemote);

    /// <summary>Inclusive date-range overlap; a null end date is treated as open-ended (<see cref="DateTime.MaxValue"/>).</summary>
    internal static bool RangesOverlap(DateTime startA, DateTime? endA, DateTime startB, DateTime? endB) =>
        startA <= (endB ?? DateTime.MaxValue) && startB <= (endA ?? DateTime.MaxValue);

    /// <summary>True when the candidate's start date falls within the slot's effective window.</summary>
    internal static bool SlotIsEffectiveFor(PositionSlotFacts slot, Candidate candidate) =>
        candidate.StartDate >= slot.EffectiveFromUtc
        && (slot.EffectiveToUtc is null || candidate.StartDate <= slot.EffectiveToUtc);

    public static Result<Evaluation> Evaluate(
        Candidate candidate,
        IReadOnlyCollection<ExistingAssignment> otherAssignments,
        PositionSlotFacts? slot)
    {
        // Slot-bound checks only matter for an active assignment that references a slot: only active and
        // currently-effective assignments occupy capacity (P-06). Inactive/historical rows are skipped so
        // they can still be edited even if their slot was later suspended or removed.
        if (candidate is { IsActive: true, PositionSlotPublicId: { } slotId })
        {
            if (slot is null || !slot.Exists)
            {
                return Result<Evaluation>.Failure(EmploymentAssignmentErrors.PositionSlotNotFound);
            }

            if (slot.Status == PositionSlotStatus.Suspended || !SlotIsEffectiveFor(slot, candidate))
            {
                return Result<Evaluation>.Failure(EmploymentAssignmentErrors.PositionSlotNotAssignable);
            }

            var sameSlotActive = otherAssignments
                .Where(other => other.IsActive
                    && other.PublicId != candidate.PublicId
                    && other.PositionSlotPublicId == slotId)
                .ToArray();

            if (sameSlotActive.Any(other => RangesOverlap(candidate.StartDate, candidate.EndDate, other.StartDate, other.EndDate)))
            {
                return Result<Evaluation>.Failure(EmploymentAssignmentErrors.OverlappingDates);
            }

            if (sameSlotActive.Length > 0)
            {
                return Result<Evaluation>.Failure(EmploymentAssignmentErrors.DuplicatePositionSlot);
            }

            if (slot.OverlappingActiveCountExcludingSelf >= slot.MaxEmployees)
            {
                return Result<Evaluation>.Failure(EmploymentAssignmentErrors.CapacityExceeded);
            }
        }

        // Single active primary (RN-03): when the candidate is the active primary, every other active
        // primary is demoted to secondary in the same transaction (auto-degrade, P-03).
        IReadOnlyCollection<Guid> demote = candidate is { IsActive: true, IsPrimary: true }
            ? otherAssignments
                .Where(other => other.IsActive && other.IsPrimary && other.PublicId != candidate.PublicId)
                .Select(other => other.PublicId)
                .ToArray()
            : [];

        return Result<Evaluation>.Success(new Evaluation(demote));
    }
}

namespace CLARIHR.Domain.PositionSlots;

// §PS5: stable error-code contract for domain → Application classification.
// Inherits InvalidOperationException so existing `catch (InvalidOperationException)`
// sites keep working, but the Application layer now dispatches on Code, not
// on Message text — see PositionSlotCommandSupport.MapDomainValidation.
public enum PositionSlotDomainErrorCode
{
    DirectDependencySelfReference,
    FunctionalDependencySelfReference,
    SuspendedOccupancyConflict,
    MaxEmployeesInvalid,
    OccupiedEmployeesNegative,
    OccupiedExceedsCapacity,
    EffectiveFromRequired,
    EffectiveDateRangeInvalid,

    // §PS6: the caller explicitly supplied a status AND an occupancy that contradict
    // each other on create (e.g. Vacant with occupants, or Occupied with zero). The
    // value used to be coerced silently; it is now rejected. Note this is distinct from
    // the intentional auto-correction on the status-only ChangeStatus transition, which
    // has no caller-supplied occupancy to contradict.
    StatusOccupancyMismatch
}

public sealed class PositionSlotDomainException(PositionSlotDomainErrorCode code, string message)
    : InvalidOperationException(message)
{
    public PositionSlotDomainErrorCode Code { get; } = code;
}

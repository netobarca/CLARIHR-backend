namespace CLARIHR.Domain.PositionSlots;

// §PS5: stable error-code contract for domain → Application classification.
// Inherits InvalidOperationException so existing `catch (InvalidOperationException)`
// sites keep working, but the Application layer now dispatches on Code, not
// on Message text — see PositionSlotCommandSupport.MapDomainValidation.
public enum PositionSlotDomainErrorCode
{
    DirectDependencySelfReference,
    FunctionalDependencySelfReference,
    MaxEmployeesInvalid,
    EffectiveFromRequired,
    // H-23 — `SuspendedOccupancyConflict`, `OccupiedEmployeesNegative`, `OccupiedExceedsCapacity` and
    // `StatusOccupancyMismatch` are gone with the occupancy counter: `Vacant`/`Occupied` and the occupant count
    // are derived from the assignments, so there is no second number left to contradict the first.
    EffectiveDateRangeInvalid
}

public sealed class PositionSlotDomainException(PositionSlotDomainErrorCode code, string message)
    : InvalidOperationException(message)
{
    public PositionSlotDomainErrorCode Code { get; } = code;
}

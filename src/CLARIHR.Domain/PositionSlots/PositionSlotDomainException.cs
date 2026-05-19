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
    EffectiveDateRangeInvalid
}

public sealed class PositionSlotDomainException(PositionSlotDomainErrorCode code, string message)
    : InvalidOperationException(message)
{
    public PositionSlotDomainErrorCode Code { get; } = code;
}

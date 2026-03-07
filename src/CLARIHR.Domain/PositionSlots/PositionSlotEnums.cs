namespace CLARIHR.Domain.PositionSlots;

public enum PositionSlotStatus
{
    Vacant = 1,
    Occupied = 2,
    Suspended = 3
}

public enum PositionSlotDependencyRelationType
{
    Direct = 1,
    Functional = 2
}

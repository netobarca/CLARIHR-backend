namespace CLARIHR.Domain.Common;

public interface IDomainEvent
{
    DateTime OccurredUtc { get; }
}

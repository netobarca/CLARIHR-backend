namespace CLARIHR.Domain.Common;

public abstract record DomainEvent(DateTime OccurredUtc) : IDomainEvent;

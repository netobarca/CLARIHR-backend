namespace CLARIHR.Domain.Common;

public abstract class AuditableEntity : AggregateRoot
{
    public DateTime CreatedUtc { get; private set; }
    public DateTime? ModifiedUtc { get; private set; }

    public void MarkCreated(DateTime utcNow)
    {
        CreatedUtc = utcNow;
        ModifiedUtc = utcNow;
    }

    public void MarkModified(DateTime utcNow)
    {
        if (CreatedUtc == default)
        {
            CreatedUtc = utcNow;
        }

        ModifiedUtc = utcNow;
    }
}

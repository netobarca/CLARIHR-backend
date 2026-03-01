namespace CLARIHR.Domain.Common;

public interface ITenantScopedEntity
{
    Guid TenantId { get; }
}

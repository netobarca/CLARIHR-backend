namespace CLARIHR.Domain.Common;

public abstract class TenantEntity : AuditableEntity, ITenantScopedEntity
{
    public Guid TenantId { get; private set; }

    public void SetTenantId(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        }

        TenantId = tenantId;
    }
}

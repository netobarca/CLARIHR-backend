namespace CLARIHR.Application.Abstractions.Tenancy;

public interface ITenantContext
{
    Guid? TenantId { get; }
}

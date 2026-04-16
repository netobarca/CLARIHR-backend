namespace CLARIHR.Application.Abstractions.Tenancy;

public interface ITenantLocaleResolver
{
    Task<string?> ResolveDefaultLocaleAsync(Guid tenantId, CancellationToken cancellationToken);
}

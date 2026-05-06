namespace CLARIHR.Application.Abstractions.OrgStructureCatalogs;

public interface IOrgStructureCatalogSeedService
{
    Task InitializeDefaultsAsync(Guid tenantId, CancellationToken cancellationToken);
}

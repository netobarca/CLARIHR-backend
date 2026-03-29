namespace CLARIHR.Application.Abstractions.OrgStructureCatalogs;

public interface ICompanyTypeCatalogSeedService
{
    Task EnsureSeededAsync(Guid ownerUserPublicId, CancellationToken cancellationToken);
}

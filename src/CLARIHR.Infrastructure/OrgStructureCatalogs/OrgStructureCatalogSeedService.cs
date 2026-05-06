using CLARIHR.Application.Abstractions.OrgStructureCatalogs;
using CLARIHR.Domain.OrgStructureCatalogs;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.OrgStructureCatalogs;

internal sealed class OrgStructureCatalogSeedService(ApplicationDbContext dbContext) : IOrgStructureCatalogSeedService
{
    public async Task InitializeDefaultsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var hasExistingOrgUnitTypes = await dbContext.OrgUnitTypeCatalogItems
            .AnyAsync(item => item.TenantId == tenantId, cancellationToken);

        if (!hasExistingOrgUnitTypes)
        {
            var orgUnitTypes = new[]
            {
                OrgUnitTypeCatalogItem.Create("GERENCIA", "Gerencia", null, 10),
                OrgUnitTypeCatalogItem.Create("DEPARTAMENTO", "Departamento", null, 20),
                OrgUnitTypeCatalogItem.Create("UNIDAD", "Unidad", null, 30),
            };

            foreach (var t in orgUnitTypes)
            {
                t.SetTenantId(tenantId);
                dbContext.OrgUnitTypeCatalogItems.Add(t);
            }
        }

        var hasExistingFunctionalAreas = await dbContext.FunctionalAreaCatalogItems
            .AnyAsync(item => item.TenantId == tenantId, cancellationToken);

        if (!hasExistingFunctionalAreas)
        {
            var functionalAreas = new[]
            {
                FunctionalAreaCatalogItem.Create("ADMIN", "Administracion", null, 10),
                FunctionalAreaCatalogItem.Create("OPS", "Operaciones", null, 20),
                FunctionalAreaCatalogItem.Create("SALES", "Ventas", null, 30),
                FunctionalAreaCatalogItem.Create("HR", "Recursos Humanos", null, 40),
            };

            foreach (var fa in functionalAreas)
            {
                fa.SetTenantId(tenantId);
                dbContext.FunctionalAreaCatalogItems.Add(fa);
            }
        }

        if (!hasExistingOrgUnitTypes || !hasExistingFunctionalAreas)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

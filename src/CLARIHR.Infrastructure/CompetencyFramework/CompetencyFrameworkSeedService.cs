using CLARIHR.Application.Abstractions.CompetencyFramework;
using CLARIHR.Domain.CompetencyFramework;
using CLARIHR.Domain.JobProfiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.CompetencyFramework;

/// <summary>
/// Per-tenant seed of the competency-framework defaults (D-03): the competency-type catalog
/// (gestión / organizacional / técnica) and a default active rating scale (discrete 1–5). Mirrors
/// <see cref="OrgStructureCatalogs.OrgStructureCatalogSeedService"/>: guarded existence checks make it
/// idempotent so it is safe to run on every provisioning and to backfill existing tenants.
/// </summary>
internal sealed class CompetencyFrameworkSeedService(ApplicationDbContext dbContext) : ICompetencyFrameworkSeedService
{
    public async Task InitializeDefaultsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var changed = false;

        var hasCompetencyTypes = await dbContext.Set<JobCatalogItem>()
            .AnyAsync(item => item.TenantId == tenantId && item.Category == JobCatalogCategory.CompetencyType, cancellationToken);

        if (!hasCompetencyTypes)
        {
            var types = new[]
            {
                JobCatalogItem.Create(JobCatalogCategory.CompetencyType, "GESTION", "Gestión", isSystem: true),
                JobCatalogItem.Create(JobCatalogCategory.CompetencyType, "ORGANIZACIONAL", "Organizacional", isSystem: true),
                JobCatalogItem.Create(JobCatalogCategory.CompetencyType, "TECNICA", "Técnica", isSystem: true),
            };

            foreach (var type in types)
            {
                type.SetTenantId(tenantId);
                dbContext.Set<JobCatalogItem>().Add(type);
            }

            changed = true;
        }

        var hasScale = await dbContext.Set<CompetencyRatingScale>()
            .AnyAsync(scale => scale.TenantId == tenantId, cancellationToken);

        if (!hasScale)
        {
            var levels = new[]
            {
                CompetencyRatingScaleLevel.Create("1", "Deficiente", 1m, 10),
                CompetencyRatingScaleLevel.Create("2", "Regular", 2m, 20),
                CompetencyRatingScaleLevel.Create("3", "Bueno", 3m, 30),
                CompetencyRatingScaleLevel.Create("4", "Muy bueno", 4m, 40),
                CompetencyRatingScaleLevel.Create("5", "Excelente", 5m, 50),
            };

            var scale = CompetencyRatingScale.CreateDiscrete("ESCALA_1_5", "Escala 1 a 5", levels);
            scale.SetTenantId(tenantId);
            foreach (var level in scale.Levels)
            {
                level.SetTenantId(tenantId);
            }

            dbContext.Set<CompetencyRatingScale>().Add(scale);
            changed = true;
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}

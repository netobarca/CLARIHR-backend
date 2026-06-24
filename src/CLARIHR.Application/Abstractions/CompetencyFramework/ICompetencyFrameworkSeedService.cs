namespace CLARIHR.Application.Abstractions.CompetencyFramework;

/// <summary>
/// Seeds the per-tenant competency-framework defaults required for the "Competencias del puesto" feature
/// (decision D-03): the competency-type catalog (gestión / organizacional / técnica) and a default active
/// rating scale. Tenant-scoped (JobCatalogItem and CompetencyRatingScale are tenant entities, not
/// country-scoped), so it runs per company at provisioning time. Idempotent.
/// </summary>
public interface ICompetencyFrameworkSeedService
{
    Task InitializeDefaultsAsync(Guid tenantId, CancellationToken cancellationToken);
}

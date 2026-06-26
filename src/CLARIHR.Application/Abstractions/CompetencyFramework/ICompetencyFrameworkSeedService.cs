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

    /// <summary>
    /// Backfills the competency-framework defaults for every existing tenant (company) that lacks them.
    /// Idempotent and safe to run on every startup; it brings tenants provisioned before this feature
    /// shipped up to the same defaults (active discrete 1–5 rating scale + competency-type catalog) as
    /// newly provisioned ones, so <c>GET .../competency-rating-scale</c> reports <c>isConfigured = true</c>.
    /// </summary>
    Task EnsureSeededAsync(CancellationToken cancellationToken);
}

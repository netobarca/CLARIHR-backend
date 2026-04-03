namespace CLARIHR.Application.Abstractions.Companies;

public sealed record EffectiveCommercialModuleGrant(
    string ModuleKey,
    bool GrantedByPlan,
    bool GrantedByAddon);

public interface IPlanEntitlementService
{
    Task EnsureFreePlanDefaultsAsync(CancellationToken cancellationToken);

    Task<bool> IsModuleEnabledAsync(Guid companyPublicId, string moduleKey, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<EffectiveCommercialModuleGrant>> GetEffectiveModulesAsync(
        Guid companyPublicId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<EffectiveCommercialModuleGrant>>(Array.Empty<EffectiveCommercialModuleGrant>());
}

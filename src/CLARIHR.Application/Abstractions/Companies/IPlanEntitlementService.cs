namespace CLARIHR.Application.Abstractions.Companies;

public sealed record EffectiveCommercialModuleGrant(
    string ModuleKey,
    bool GrantedByPlan,
    bool GrantedByAddon);

public sealed record EffectiveCommercialCapabilityGrant(
    string CapabilityCode,
    string ModuleKey,
    bool GrantedByPlan,
    bool GrantedByAddon);

public interface IPlanEntitlementService
{
    Task EnsureSystemPlanDefaultsAsync(CancellationToken cancellationToken);

    Task<bool> IsModuleEnabledAsync(Guid companyPublicId, string moduleKey, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<EffectiveCommercialCapabilityGrant>> GetEffectiveCapabilitiesAsync(
        Guid companyPublicId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<EffectiveCommercialModuleGrant>> GetEffectiveModulesAsync(
        Guid companyPublicId,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<EffectiveCommercialModuleGrant>>(Array.Empty<EffectiveCommercialModuleGrant>());
}

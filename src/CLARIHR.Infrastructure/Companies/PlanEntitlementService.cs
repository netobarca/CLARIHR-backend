using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Features.Provisioning.Common;
using CLARIHR.Domain.Companies;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Companies;

internal sealed class PlanEntitlementService(ApplicationDbContext dbContext) : IPlanEntitlementService
{
    public async Task EnsureFreePlanDefaultsAsync(CancellationToken cancellationToken)
    {
        var normalizedPlanCode = ProvisioningConstants.FreePlanCode.ToUpperInvariant();

        var commercialPlan = await dbContext.CommercialPlans
            .Include(plan => plan.Entitlements)
            .SingleOrDefaultAsync(
            plan => plan.NormalizedCode == normalizedPlanCode,
            cancellationToken);

        if (commercialPlan is null)
        {
            commercialPlan = CommercialPlan.Create(
                ProvisioningConstants.FreePlanCode,
                "Free",
                "Canonical free commercial plan used during provisioning.",
                baseMonthlyFee: 0m,
                pricePerActiveEmployee: 0m,
                CommercialPlanStatus.Active,
                isSystemPlan: true,
                CommercialModuleCatalog.DefaultFreeModuleKeys,
                limits: []);
            dbContext.CommercialPlans.Add(commercialPlan);
            _ = await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        if (commercialPlan.Entitlements.Count > 0)
        {
            return;
        }

        foreach (var moduleKey in CommercialModuleCatalog.DefaultFreeModuleKeys)
        {
            dbContext.PlanEntitlements.Add(PlanEntitlement.Create(commercialPlan.Code, moduleKey, isEnabled: true));
        }

        _ = await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsModuleEnabledAsync(Guid companyPublicId, string moduleKey, CancellationToken cancellationToken)
    {
        var normalizedModuleKey = CommercialModuleCatalog.NormalizeKnownKey(moduleKey);
        var effectiveModules = await GetEffectiveModulesAsync(companyPublicId, cancellationToken);
        return effectiveModules.Any(
            entitlement => string.Equals(entitlement.ModuleKey, normalizedModuleKey, StringComparison.Ordinal));
    }

    public async Task<IReadOnlyCollection<EffectiveCommercialModuleGrant>> GetEffectiveModulesAsync(
        Guid companyPublicId,
        CancellationToken cancellationToken)
    {
        var subscriptionContext = await (
            from company in dbContext.Companies.AsNoTracking()
            join subscription in dbContext.CompanySubscriptions.AsNoTracking()
                on company.Id equals subscription.CompanyId
            where company.PublicId == companyPublicId &&
                  (subscription.Status == SubscriptionStatus.Active || subscription.Status == SubscriptionStatus.Trial)
            select new
            {
                company.Id,
                subscription.CommercialPlanId
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (subscriptionContext is null)
        {
            return Array.Empty<EffectiveCommercialModuleGrant>();
        }

        var planModuleKeys = await dbContext.PlanEntitlements
            .AsNoTracking()
            .Where(entitlement =>
                entitlement.CommercialPlanId == subscriptionContext.CommercialPlanId &&
                entitlement.IsEnabled)
            .Select(entitlement => entitlement.ModuleKey)
            .ToListAsync(cancellationToken);

        var addonModuleKeys = await (
            from companyAddon in dbContext.CompanyCommercialAddons.AsNoTracking()
            join entitlement in dbContext.CommercialAddonEntitlements.AsNoTracking()
                on companyAddon.CommercialAddonId equals entitlement.CommercialAddonId
            where companyAddon.CompanyId == subscriptionContext.Id &&
                  companyAddon.Status == CompanyAddonStatus.Active &&
                  entitlement.IsEnabled
            select entitlement.ModuleKey)
            .Distinct()
            .ToListAsync(cancellationToken);

        var grants = new Dictionary<string, EffectiveCommercialModuleGrant>(StringComparer.Ordinal);

        foreach (var moduleKey in planModuleKeys)
        {
            grants[moduleKey] = new EffectiveCommercialModuleGrant(moduleKey, GrantedByPlan: true, GrantedByAddon: false);
        }

        foreach (var moduleKey in addonModuleKeys)
        {
            if (grants.TryGetValue(moduleKey, out var currentGrant))
            {
                grants[moduleKey] = currentGrant with { GrantedByAddon = true };
                continue;
            }

            grants[moduleKey] = new EffectiveCommercialModuleGrant(moduleKey, GrantedByPlan: false, GrantedByAddon: true);
        }

        return grants.Values
            .OrderBy(grant => grant.ModuleKey, StringComparer.Ordinal)
            .ToArray();
    }
}

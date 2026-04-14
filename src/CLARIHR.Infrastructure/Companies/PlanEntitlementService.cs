using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Features.Provisioning.Common;
using CLARIHR.Domain.Companies;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Companies;

internal sealed class PlanEntitlementService(ApplicationDbContext dbContext) : IPlanEntitlementService
{
    public async Task EnsureSystemPlanDefaultsAsync(CancellationToken cancellationToken)
    {
        await EnsureFreePlanDefaultsAsync(cancellationToken);
        await EnsureMasterPlanDefaultsAsync(cancellationToken);
    }

    private async Task EnsureFreePlanDefaultsAsync(CancellationToken cancellationToken)
    {
        var normalizedPlanCode = ProvisioningConstants.FreePlanCode.ToUpperInvariant();
        var desiredModuleKeys = CommercialModuleCatalog.DefaultFreeModuleKeys
            .OrderBy(static moduleKey => moduleKey, StringComparer.Ordinal)
            .ToArray();

        var commercialPlan = await dbContext.CommercialPlans
            .AsSplitQuery()
            .Include(plan => plan.Entitlements)
            .Include(plan => plan.Limits)
            .Include(plan => plan.Versions)
            .SingleOrDefaultAsync(
            plan => plan.NormalizedCode == normalizedPlanCode,
            cancellationToken);

        if (commercialPlan is null)
        {
            commercialPlan = CommercialPlan.Create(
                ProvisioningConstants.FreePlanCode,
                ProvisioningConstants.FreePlanName,
                ProvisioningConstants.FreePlanDescription,
                baseMonthlyFee: 0m,
                pricePerActiveEmployee: 0m,
                CommercialPlanStatus.Active,
                isSystemPlan: true,
                desiredModuleKeys,
                limits: []);
            dbContext.CommercialPlans.Add(commercialPlan);
            _ = await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var hasChanges = false;
        if (!commercialPlan.IsSystemPlan)
        {
            dbContext.Entry(commercialPlan).Property(plan => plan.IsSystemPlan).CurrentValue = true;
            hasChanges = true;
        }

        if (commercialPlan.Status != CommercialPlanStatus.Active)
        {
            commercialPlan.Activate();
            hasChanges = true;
        }

        var currentEnabledModuleKeys = commercialPlan.Entitlements
            .Where(entitlement => entitlement.IsEnabled)
            .Select(entitlement => entitlement.ModuleKey)
            .OrderBy(static moduleKey => moduleKey, StringComparer.Ordinal)
            .ToArray();

        var requiresCatalogSync =
            !string.Equals(commercialPlan.Name, ProvisioningConstants.FreePlanName, StringComparison.Ordinal) ||
            !string.Equals(commercialPlan.Description, ProvisioningConstants.FreePlanDescription, StringComparison.Ordinal) ||
            commercialPlan.BaseMonthlyFee != 0m ||
            commercialPlan.PricePerActiveEmployee != 0m ||
            commercialPlan.Limits.Count > 0 ||
            !currentEnabledModuleKeys.SequenceEqual(desiredModuleKeys, StringComparer.Ordinal);

        if (!requiresCatalogSync && !hasChanges)
        {
            return;
        }

        commercialPlan.Update(
            ProvisioningConstants.FreePlanCode,
            ProvisioningConstants.FreePlanName,
            ProvisioningConstants.FreePlanDescription,
            baseMonthlyFee: 0m,
            pricePerActiveEmployee: 0m,
            desiredModuleKeys,
            limits: []);

        _ = await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureMasterPlanDefaultsAsync(CancellationToken cancellationToken)
    {
        var normalizedPlanCode = ProvisioningConstants.MasterPlanCode.ToUpperInvariant();
        var desiredModuleKeys = CommercialModuleCatalog.DefaultMasterModuleKeys
            .OrderBy(static moduleKey => moduleKey, StringComparer.Ordinal)
            .ToArray();

        var commercialPlan = await dbContext.CommercialPlans
            .AsSplitQuery()
            .Include(plan => plan.Entitlements)
            .Include(plan => plan.Limits)
            .Include(plan => plan.Versions)
            .SingleOrDefaultAsync(
                plan => plan.NormalizedCode == normalizedPlanCode,
                cancellationToken);

        if (commercialPlan is null)
        {
            commercialPlan = CommercialPlan.Create(
                ProvisioningConstants.MasterPlanCode,
                ProvisioningConstants.MasterPlanName,
                ProvisioningConstants.MasterPlanDescription,
                baseMonthlyFee: 0m,
                pricePerActiveEmployee: 0m,
                CommercialPlanStatus.Active,
                isSystemPlan: true,
                desiredModuleKeys,
                limits: []);
            dbContext.CommercialPlans.Add(commercialPlan);
            _ = await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        var hasChanges = false;
        if (!commercialPlan.IsSystemPlan)
        {
            dbContext.Entry(commercialPlan).Property(plan => plan.IsSystemPlan).CurrentValue = true;
            hasChanges = true;
        }

        if (commercialPlan.Status != CommercialPlanStatus.Active)
        {
            commercialPlan.Activate();
            hasChanges = true;
        }

        var currentEnabledModuleKeys = commercialPlan.Entitlements
            .Where(entitlement => entitlement.IsEnabled)
            .Select(entitlement => entitlement.ModuleKey)
            .OrderBy(static moduleKey => moduleKey, StringComparer.Ordinal)
            .ToArray();

        var requiresCatalogSync =
            !string.Equals(commercialPlan.Name, ProvisioningConstants.MasterPlanName, StringComparison.Ordinal) ||
            !string.Equals(commercialPlan.Description, ProvisioningConstants.MasterPlanDescription, StringComparison.Ordinal) ||
            commercialPlan.BaseMonthlyFee != 0m ||
            commercialPlan.PricePerActiveEmployee != 0m ||
            commercialPlan.Limits.Count > 0 ||
            !currentEnabledModuleKeys.SequenceEqual(desiredModuleKeys, StringComparer.Ordinal);

        if (!requiresCatalogSync && !hasChanges)
        {
            return;
        }

        commercialPlan.Update(
            ProvisioningConstants.MasterPlanCode,
            ProvisioningConstants.MasterPlanName,
            ProvisioningConstants.MasterPlanDescription,
            baseMonthlyFee: 0m,
            pricePerActiveEmployee: 0m,
            desiredModuleKeys,
            limits: []);

        _ = await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> IsModuleEnabledAsync(Guid companyPublicId, string moduleKey, CancellationToken cancellationToken)
    {
        var normalizedModuleKey = CommercialModuleCatalog.NormalizeKnownKey(moduleKey);
        var effectiveModules = await GetEffectiveModulesAsync(companyPublicId, cancellationToken);
        return effectiveModules.Any(
            entitlement => string.Equals(entitlement.ModuleKey, normalizedModuleKey, StringComparison.Ordinal));
    }

    public async Task<IReadOnlyCollection<EffectiveCommercialCapabilityGrant>> GetEffectiveCapabilitiesAsync(
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
            return Array.Empty<EffectiveCommercialCapabilityGrant>();
        }

        var planCapabilities = await dbContext.PlanEntitlements
            .AsNoTracking()
            .Where(entitlement =>
                entitlement.CommercialPlanId == subscriptionContext.CommercialPlanId &&
                entitlement.IsEnabled)
            .Select(entitlement => new
            {
                entitlement.CapabilityCode,
                entitlement.ModuleKey
            })
            .ToListAsync(cancellationToken);

        var addonCapabilities = await (
            from companyAddon in dbContext.CompanyCommercialAddons.AsNoTracking()
            join entitlement in dbContext.CommercialAddonEntitlements.AsNoTracking()
                on companyAddon.CommercialAddonId equals entitlement.CommercialAddonId
            where companyAddon.CompanyId == subscriptionContext.Id &&
                  companyAddon.Status == CompanyAddonStatus.Active &&
                  entitlement.IsEnabled
            select new
            {
                entitlement.CapabilityCode,
                entitlement.ModuleKey
            })
            .Distinct()
            .ToListAsync(cancellationToken);

        var grants = new Dictionary<string, EffectiveCommercialCapabilityGrant>(StringComparer.Ordinal);

        foreach (var entitlement in planCapabilities)
        {
            var capabilityCode = ResolveCapabilityCode(entitlement.CapabilityCode, entitlement.ModuleKey);
            var moduleKey = ResolveModuleKey(capabilityCode, entitlement.ModuleKey);
            grants[capabilityCode] = new EffectiveCommercialCapabilityGrant(
                capabilityCode,
                moduleKey,
                GrantedByPlan: true,
                GrantedByAddon: false);
        }

        foreach (var entitlement in addonCapabilities)
        {
            var capabilityCode = ResolveCapabilityCode(entitlement.CapabilityCode, entitlement.ModuleKey);
            var moduleKey = ResolveModuleKey(capabilityCode, entitlement.ModuleKey);
            if (grants.TryGetValue(capabilityCode, out var currentGrant))
            {
                grants[capabilityCode] = currentGrant with { GrantedByAddon = true };
                continue;
            }

            grants[capabilityCode] = new EffectiveCommercialCapabilityGrant(
                capabilityCode,
                moduleKey,
                GrantedByPlan: false,
                GrantedByAddon: true);
        }

        return grants.Values
            .OrderBy(grant => grant.CapabilityCode, StringComparer.Ordinal)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<EffectiveCommercialModuleGrant>> GetEffectiveModulesAsync(
        Guid companyPublicId,
        CancellationToken cancellationToken)
    {
        var effectiveCapabilities = await GetEffectiveCapabilitiesAsync(companyPublicId, cancellationToken);
        var grants = new Dictionary<string, EffectiveCommercialModuleGrant>(StringComparer.Ordinal);

        foreach (var capability in effectiveCapabilities.Where(static capability => capability.GrantedByPlan))
        {
            grants[capability.ModuleKey] = new EffectiveCommercialModuleGrant(
                capability.ModuleKey,
                GrantedByPlan: true,
                GrantedByAddon: false);
        }

        foreach (var capability in effectiveCapabilities.Where(static capability => capability.GrantedByAddon))
        {
            if (grants.TryGetValue(capability.ModuleKey, out var currentGrant))
            {
                grants[capability.ModuleKey] = currentGrant with { GrantedByAddon = true };
                continue;
            }

            grants[capability.ModuleKey] = new EffectiveCommercialModuleGrant(
                capability.ModuleKey,
                GrantedByPlan: false,
                GrantedByAddon: true);
        }

        return grants.Values
            .OrderBy(grant => grant.ModuleKey, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ResolveCapabilityCode(string capabilityCode, string moduleKey) =>
        string.IsNullOrWhiteSpace(capabilityCode)
            ? CommercialCapabilityCatalog.GetByModuleKey(moduleKey).Code
            : CommercialCapabilityCatalog.NormalizeKnownCode(capabilityCode);

    private static string ResolveModuleKey(string capabilityCode, string moduleKey) =>
        string.IsNullOrWhiteSpace(moduleKey)
            ? CommercialCapabilityCatalog.Get(capabilityCode).ModuleKey
            : CommercialModuleCatalog.NormalizeKnownKey(moduleKey);
}

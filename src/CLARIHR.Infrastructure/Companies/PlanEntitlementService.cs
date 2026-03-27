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

        var commercialPlanExists = await dbContext.CommercialPlans.AnyAsync(
            plan => plan.NormalizedCode == normalizedPlanCode,
            cancellationToken);

        if (!commercialPlanExists)
        {
            dbContext.CommercialPlans.Add(CommercialPlan.Create(
                ProvisioningConstants.FreePlanCode,
                "Free",
                "Canonical free commercial plan used during provisioning.",
                baseMonthlyFee: 0m,
                pricePerActiveEmployee: 0m,
                CommercialPlanStatus.Active,
                isSystemPlan: true,
                limits: []));
        }

        foreach (var moduleKey in ProvisioningConstants.FreePlanEnabledModules)
        {
            var normalizedModuleKey = moduleKey.ToUpperInvariant();

            var exists = await dbContext.PlanEntitlements.AnyAsync(
                entitlement =>
                    entitlement.PlanCode == normalizedPlanCode &&
                    entitlement.ModuleKey == normalizedModuleKey,
                cancellationToken);

            if (exists)
            {
                continue;
            }

            dbContext.PlanEntitlements.Add(PlanEntitlement.Create(ProvisioningConstants.FreePlanCode, moduleKey, isEnabled: true));
        }

        _ = await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> IsModuleEnabledAsync(Guid companyPublicId, string moduleKey, CancellationToken cancellationToken)
    {
        var normalizedModuleKey = moduleKey.Trim().ToUpperInvariant();

        return dbContext.CompanySubscriptions
            .Where(subscription => subscription.Status == SubscriptionStatus.Active)
            .Join(
                dbContext.Companies.Where(company => company.PublicId == companyPublicId),
                subscription => subscription.CompanyId,
                company => company.Id,
                (subscription, _) => subscription.PlanCode)
            .Join(
                dbContext.PlanEntitlements.Where(entitlement => entitlement.IsEnabled),
                planCode => new { PlanCode = planCode, ModuleKey = normalizedModuleKey },
                entitlement => new { entitlement.PlanCode, entitlement.ModuleKey },
                (_, _) => true)
            .AnyAsync(cancellationToken);
    }
}

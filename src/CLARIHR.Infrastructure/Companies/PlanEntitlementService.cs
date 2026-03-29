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

        var commercialPlan = await dbContext.CommercialPlans.SingleOrDefaultAsync(
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
                limits: []);
            dbContext.CommercialPlans.Add(commercialPlan);
            _ = await dbContext.SaveChangesAsync(cancellationToken);
        }

        foreach (var moduleKey in ProvisioningConstants.FreePlanEnabledModules)
        {
            var normalizedModuleKey = moduleKey.ToUpperInvariant();

            var exists = await dbContext.PlanEntitlements.AnyAsync(
                entitlement =>
                    entitlement.CommercialPlanId == commercialPlan.Id &&
                    entitlement.ModuleKey == normalizedModuleKey,
                cancellationToken);

            if (exists)
            {
                continue;
            }

            dbContext.PlanEntitlements.Add(PlanEntitlement.Create(commercialPlan.Id, commercialPlan.Code, moduleKey, isEnabled: true));
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
                (subscription, _) => subscription.CommercialPlanId)
            .Join(
                dbContext.PlanEntitlements.Where(entitlement => entitlement.IsEnabled),
                commercialPlanId => new { CommercialPlanId = commercialPlanId, ModuleKey = normalizedModuleKey },
                entitlement => new { entitlement.CommercialPlanId, entitlement.ModuleKey },
                (_, _) => true)
            .AnyAsync(cancellationToken);
    }
}

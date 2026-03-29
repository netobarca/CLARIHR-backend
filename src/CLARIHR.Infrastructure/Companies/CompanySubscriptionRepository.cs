using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PlatformSubscriptions;
using CLARIHR.Domain.Companies;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Companies;

internal sealed class CompanySubscriptionRepository(ApplicationDbContext dbContext) : ICompanySubscriptionRepository
{
    public void Add(CompanySubscription subscription) => dbContext.CompanySubscriptions.Add(subscription);

    public Task<CompanySubscription?> GetActiveByCompanyIdAsync(long companyId, CancellationToken cancellationToken) =>
        dbContext.CompanySubscriptions
            .SingleOrDefaultAsync(
                subscription => subscription.CompanyId == companyId && subscription.Status == SubscriptionStatus.Active,
                cancellationToken);

    public Task<CompanySubscription?> GetActiveByCompanyPublicIdAsync(Guid companyPublicId, CancellationToken cancellationToken) =>
        dbContext.CompanySubscriptions
            .SingleOrDefaultAsync(
                subscription =>
                    subscription.Status == SubscriptionStatus.Active &&
                    dbContext.Companies.Any(company => company.Id == subscription.CompanyId && company.PublicId == companyPublicId),
                cancellationToken);

    public Task<PlatformCompanySubscriptionResponse?> GetCurrentByCompanyPublicIdAsync(Guid companyPublicId, CancellationToken cancellationToken) =>
        (from subscription in dbContext.CompanySubscriptions.AsNoTracking()
         join company in dbContext.Companies.AsNoTracking() on subscription.CompanyId equals company.Id
         join plan in dbContext.CommercialPlans.AsNoTracking() on subscription.CommercialPlanId equals plan.Id
         where company.PublicId == companyPublicId &&
               subscription.Status == SubscriptionStatus.Active
         orderby subscription.StartDateUtc descending
         select new PlatformCompanySubscriptionResponse(
             subscription.PublicId,
             company.PublicId,
             plan.PublicId,
             subscription.PlanCode,
             subscription.PlanName,
             subscription.BaseMonthlyFee,
             subscription.PricePerActiveEmployee,
             subscription.Status,
             subscription.StartDateUtc,
             subscription.EndDateUtc,
             subscription.CreatedUtc,
             subscription.ModifiedUtc))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PagedResponse<PlatformCompanySubscriptionResponse>> SearchByCompanyPublicIdAsync(
        Guid companyPublicId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query =
            from subscription in dbContext.CompanySubscriptions.AsNoTracking()
            join company in dbContext.Companies.AsNoTracking() on subscription.CompanyId equals company.Id
            join plan in dbContext.CommercialPlans.AsNoTracking() on subscription.CommercialPlanId equals plan.Id
            where company.PublicId == companyPublicId
            select new
            {
                Subscription = subscription,
                CompanyPublicId = company.PublicId,
                CommercialPlanPublicId = plan.PublicId
            };

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(row => row.Subscription.StartDateUtc)
            .ThenByDescending(row => row.Subscription.CreatedUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(row => new PlatformCompanySubscriptionResponse(
                row.Subscription.PublicId,
                row.CompanyPublicId,
                row.CommercialPlanPublicId,
                row.Subscription.PlanCode,
                row.Subscription.PlanName,
                row.Subscription.BaseMonthlyFee,
                row.Subscription.PricePerActiveEmployee,
                row.Subscription.Status,
                row.Subscription.StartDateUtc,
                row.Subscription.EndDateUtc,
                row.Subscription.CreatedUtc,
                row.Subscription.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<PlatformCompanySubscriptionResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<string?> GetActivePlanCodeAsync(Guid companyPublicId, CancellationToken cancellationToken) =>
        dbContext.CompanySubscriptions
            .Where(subscription =>
                subscription.Status == SubscriptionStatus.Active &&
                dbContext.Companies.Any(company => company.Id == subscription.CompanyId && company.PublicId == companyPublicId))
            .Select(subscription => subscription.PlanCode)
            .SingleOrDefaultAsync(cancellationToken);

}

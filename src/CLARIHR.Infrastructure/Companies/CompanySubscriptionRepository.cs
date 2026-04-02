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

    public Task<CompanySubscription?> GetScheduledByCompanyIdAsync(long companyId, CancellationToken cancellationToken) =>
        dbContext.CompanySubscriptions
            .SingleOrDefaultAsync(
                subscription => subscription.CompanyId == companyId && subscription.Status == SubscriptionStatus.Scheduled,
                cancellationToken);

    public Task<CompanySubscription?> GetActiveByCompanyPublicIdAsync(Guid companyPublicId, CancellationToken cancellationToken) =>
        dbContext.CompanySubscriptions
            .SingleOrDefaultAsync(
                subscription =>
                    subscription.Status == SubscriptionStatus.Active &&
                    dbContext.Companies.Any(company => company.Id == subscription.CompanyId && company.PublicId == companyPublicId),
                cancellationToken);

    public async Task<PlatformCompanySubscriptionOverviewResponse?> GetOverviewByCompanyPublicIdAsync(
        Guid companyPublicId,
        CancellationToken cancellationToken)
    {
        var company = await dbContext.Companies
            .AsNoTracking()
            .Where(item => item.PublicId == companyPublicId)
            .Select(item => new
            {
                item.PublicId,
                item.Name,
                item.Slug,
                item.Status,
                item.IsBillable,
                item.BillableSinceUtc
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (company is null)
        {
            return null;
        }

        var subscriptions = await (
            from subscription in dbContext.CompanySubscriptions.AsNoTracking()
            join persistedCompany in dbContext.Companies.AsNoTracking() on subscription.CompanyId equals persistedCompany.Id
            join plan in dbContext.CommercialPlans.AsNoTracking() on subscription.CommercialPlanId equals plan.Id
            join version in dbContext.CommercialPlanVersions.AsNoTracking() on subscription.CommercialPlanVersionId equals version.Id
            where persistedCompany.PublicId == companyPublicId &&
                  (subscription.Status == SubscriptionStatus.Active || subscription.Status == SubscriptionStatus.Scheduled)
            orderby subscription.Status == SubscriptionStatus.Active ? 0 : 1, subscription.StartDateUtc descending
            select new PlatformCompanySubscriptionResponse(
                subscription.PublicId,
                persistedCompany.PublicId,
                plan.PublicId,
                version.PublicId,
                subscription.PlanCode,
                subscription.PlanName,
                subscription.PlanVersionNumber,
                subscription.BaseMonthlyFee,
                subscription.PricePerActiveEmployee,
                subscription.Periodicity,
                subscription.CurrencyCode,
                subscription.Status,
                subscription.StartDateUtc,
                subscription.EndDateUtc,
                subscription.ActivatedByUserPublicId,
                subscription.ActivatedAtUtc,
                subscription.CreatedUtc,
                subscription.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PlatformCompanySubscriptionOverviewResponse(
            company.PublicId,
            company.Name,
            company.Slug,
            company.Status,
            company.IsBillable,
            company.BillableSinceUtc,
            subscriptions.FirstOrDefault(item => item.Status == SubscriptionStatus.Active),
            subscriptions.FirstOrDefault(item => item.Status == SubscriptionStatus.Scheduled));
    }

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
            join version in dbContext.CommercialPlanVersions.AsNoTracking() on subscription.CommercialPlanVersionId equals version.Id
            where company.PublicId == companyPublicId
            select new
            {
                Subscription = subscription,
                CompanyPublicId = company.PublicId,
                CommercialPlanPublicId = plan.PublicId,
                CommercialPlanVersionPublicId = version.PublicId
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
                row.CommercialPlanVersionPublicId,
                row.Subscription.PlanCode,
                row.Subscription.PlanName,
                row.Subscription.PlanVersionNumber,
                row.Subscription.BaseMonthlyFee,
                row.Subscription.PricePerActiveEmployee,
                row.Subscription.Periodicity,
                row.Subscription.CurrencyCode,
                row.Subscription.Status,
                row.Subscription.StartDateUtc,
                row.Subscription.EndDateUtc,
                row.Subscription.ActivatedByUserPublicId,
                row.Subscription.ActivatedAtUtc,
                row.Subscription.CreatedUtc,
                row.Subscription.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<PlatformCompanySubscriptionResponse>(items, pageNumber, pageSize, totalCount);
    }

    public async Task<PagedResponse<PlatformCompanySubscriptionListItemResponse>> SearchAsync(
        SubscriptionStatus? status,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query =
            from subscription in dbContext.CompanySubscriptions.AsNoTracking()
            join company in dbContext.Companies.AsNoTracking() on subscription.CompanyId equals company.Id
            join plan in dbContext.CommercialPlans.AsNoTracking() on subscription.CommercialPlanId equals plan.Id
            join version in dbContext.CommercialPlanVersions.AsNoTracking() on subscription.CommercialPlanVersionId equals version.Id
            select new
            {
                Subscription = subscription,
                Company = company,
                Plan = plan,
                VersionPublicId = version.PublicId
            };

        if (status.HasValue)
        {
            query = query.Where(row => row.Subscription.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(row =>
                row.Company.Name.ToUpper().Contains(normalizedSearch) ||
                row.Company.Slug.ToUpper().Contains(normalizedSearch) ||
                row.Subscription.PlanCode.Contains(normalizedSearch) ||
                row.Subscription.PlanName.ToUpper().Contains(normalizedSearch) ||
                row.Plan.Code.Contains(normalizedSearch) ||
                row.Plan.Name.ToUpper().Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(row => row.Company.Name)
            .ThenByDescending(row => row.Subscription.StartDateUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(row => new PlatformCompanySubscriptionListItemResponse(
                row.Company.PublicId,
                row.Company.Name,
                row.Company.Slug,
                row.Company.IsBillable,
                row.Subscription.PublicId,
                row.Plan.PublicId,
                row.VersionPublicId,
                row.Subscription.PlanCode,
                row.Subscription.PlanName,
                row.Subscription.PlanVersionNumber,
                row.Subscription.StartDateUtc,
                row.Subscription.Periodicity,
                row.Subscription.CurrencyCode,
                row.Subscription.Status))
            .ToListAsync(cancellationToken);

        return new PagedResponse<PlatformCompanySubscriptionListItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    public async Task<IReadOnlyCollection<Guid>> GetDueScheduledSubscriptionIdsAsync(
        DateTime utcNow,
        int take,
        CancellationToken cancellationToken) =>
        await dbContext.CompanySubscriptions
            .AsNoTracking()
            .Where(subscription =>
                subscription.Status == SubscriptionStatus.Scheduled &&
                subscription.StartDateUtc <= utcNow)
            .OrderBy(subscription => subscription.StartDateUtc)
            .Select(subscription => subscription.PublicId)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<CompanySubscription?> GetByPublicIdAsync(Guid subscriptionPublicId, CancellationToken cancellationToken) =>
        dbContext.CompanySubscriptions
            .SingleOrDefaultAsync(subscription => subscription.PublicId == subscriptionPublicId, cancellationToken);

    public Task<string?> GetActivePlanCodeAsync(Guid companyPublicId, CancellationToken cancellationToken) =>
        dbContext.CompanySubscriptions
            .Where(subscription =>
                subscription.Status == SubscriptionStatus.Active &&
                dbContext.Companies.Any(company => company.Id == subscription.CompanyId && company.PublicId == companyPublicId))
            .Select(subscription => subscription.PlanCode)
            .SingleOrDefaultAsync(cancellationToken);
}

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

    public Task<CompanySubscription?> GetCurrentByCompanyIdAsync(long companyId, CancellationToken cancellationToken) =>
        dbContext.CompanySubscriptions
            .Where(subscription =>
                subscription.CompanyId == companyId &&
                (subscription.Status == SubscriptionStatus.Draft ||
                 subscription.Status == SubscriptionStatus.Trial ||
                 subscription.Status == SubscriptionStatus.Active ||
                 subscription.Status == SubscriptionStatus.Suspended))
            .OrderByDescending(subscription => subscription.StatusChangedAtUtc)
            .ThenByDescending(subscription => subscription.CreatedUtc)
            .Include(subscription => subscription.StatusTransitions)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<CompanySubscription?> GetScheduledByCompanyIdAsync(long companyId, CancellationToken cancellationToken) =>
        dbContext.CompanySubscriptions
            .Include(subscription => subscription.StatusTransitions)
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

    public Task<CompanySubscription?> GetByCompanyAndSubscriptionPublicIdAsync(
        Guid companyPublicId,
        Guid subscriptionPublicId,
        CancellationToken cancellationToken) =>
        dbContext.CompanySubscriptions
            .Include(subscription => subscription.StatusTransitions)
            .SingleOrDefaultAsync(
                subscription =>
                    subscription.PublicId == subscriptionPublicId &&
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

        var subscriptions = await LoadSubscriptionsByCompanyPublicIdAsync(companyPublicId, cancellationToken);
        var currentSubscription = subscriptions
            .Where(item => item.Status != SubscriptionStatus.Scheduled)
            .OrderByDescending(item => item.StatusChangedAtUtc)
            .ThenByDescending(item => item.CreatedAtUtc)
            .FirstOrDefault();
        var scheduledReplacement = subscriptions
            .Where(item => item.Status == SubscriptionStatus.Scheduled)
            .OrderByDescending(item => item.StartDateUtc)
            .ThenByDescending(item => item.CreatedAtUtc)
            .FirstOrDefault();

        return new PlatformCompanySubscriptionOverviewResponse(
            company.PublicId,
            company.Name,
            company.Slug,
            company.Status,
            company.IsBillable,
            company.BillableSinceUtc,
            currentSubscription,
            scheduledReplacement);
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
                Company = company,
                Plan = plan,
                Version = version
            };

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderByDescending(row => row.Subscription.StartDateUtc)
            .ThenByDescending(row => row.Subscription.StatusChangedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(row => new SubscriptionProjection(
                row.Subscription.PublicId,
                row.Company.PublicId,
                row.Plan.PublicId,
                row.Version.PublicId,
                row.Subscription.PlanCode,
                row.Subscription.PlanName,
                row.Subscription.PlanVersionNumber,
                row.Subscription.BaseMonthlyFee,
                row.Subscription.PricePerActiveEmployee,
                row.Subscription.Periodicity,
                row.Subscription.CurrencyCode,
                row.Subscription.Status,
                row.Subscription.StartDateUtc,
                row.Subscription.ExpiresAtUtc,
                row.Subscription.EndDateUtc,
                row.Subscription.StatusChangedAtUtc,
                row.Subscription.CurrentStatusReasonCode,
                row.Subscription.CurrentStatusObservations,
                row.Subscription.CurrentStatusOrigin,
                row.Subscription.ActivatedByUserPublicId,
                row.Subscription.ActivatedAtUtc,
                row.Subscription.CreatedUtc,
                row.Subscription.ModifiedUtc,
                row.Company.Name,
                row.Company.Slug,
                row.Company.IsBillable))
            .ToListAsync(cancellationToken);

        var items = rows.Select(MapResponse).ToList();
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
                Version = version
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
                row.Subscription.PlanName.ToUpper().Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(row => row.Company.Name)
            .ThenByDescending(row => row.Subscription.StatusChangedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(row => new SubscriptionProjection(
                row.Subscription.PublicId,
                row.Company.PublicId,
                row.Plan.PublicId,
                row.Version.PublicId,
                row.Subscription.PlanCode,
                row.Subscription.PlanName,
                row.Subscription.PlanVersionNumber,
                row.Subscription.BaseMonthlyFee,
                row.Subscription.PricePerActiveEmployee,
                row.Subscription.Periodicity,
                row.Subscription.CurrencyCode,
                row.Subscription.Status,
                row.Subscription.StartDateUtc,
                row.Subscription.ExpiresAtUtc,
                row.Subscription.EndDateUtc,
                row.Subscription.StatusChangedAtUtc,
                row.Subscription.CurrentStatusReasonCode,
                row.Subscription.CurrentStatusObservations,
                row.Subscription.CurrentStatusOrigin,
                row.Subscription.ActivatedByUserPublicId,
                row.Subscription.ActivatedAtUtc,
                row.Subscription.CreatedUtc,
                row.Subscription.ModifiedUtc,
                row.Company.Name,
                row.Company.Slug,
                row.Company.IsBillable))
            .ToListAsync(cancellationToken);

        var items = rows.Select(MapListItem).ToList();
        return new PagedResponse<PlatformCompanySubscriptionListItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    public async Task<PagedResponse<PlatformCompanySubscriptionStatusTransitionResponse>> SearchStatusHistoryAsync(
        Guid companyPublicId,
        Guid subscriptionPublicId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query =
            from transition in dbContext.CompanySubscriptionStatusTransitions.AsNoTracking()
            join subscription in dbContext.CompanySubscriptions.AsNoTracking() on transition.CompanySubscriptionId equals subscription.Id
            join company in dbContext.Companies.AsNoTracking() on subscription.CompanyId equals company.Id
            where company.PublicId == companyPublicId && subscription.PublicId == subscriptionPublicId
            select transition;

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(transition => transition.ChangedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(transition => new PlatformCompanySubscriptionStatusTransitionResponse(
                transition.PreviousStatus,
                transition.NewStatus,
                transition.ChangedAtUtc,
                transition.Origin,
                transition.ActorUserPublicId,
                transition.ReasonCode,
                transition.Observations))
            .ToListAsync(cancellationToken);

        return new PagedResponse<PlatformCompanySubscriptionStatusTransitionResponse>(items, pageNumber, pageSize, totalCount);
    }

    public async Task<IReadOnlyCollection<Guid>> GetDueScheduledSubscriptionIdsAsync(
        DateTime utcNow,
        int take,
        CancellationToken cancellationToken) =>
        await dbContext.CompanySubscriptions
            .AsNoTracking()
            .Where(subscription =>
                subscription.Status == SubscriptionStatus.Scheduled &&
                subscription.StartDateUtc <= utcNow.Date)
            .OrderBy(subscription => subscription.StartDateUtc)
            .Select(subscription => subscription.PublicId)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Guid>> GetDueExpiringSubscriptionIdsAsync(
        DateTime utcNow,
        int take,
        CancellationToken cancellationToken) =>
        await dbContext.CompanySubscriptions
            .AsNoTracking()
            .Where(subscription =>
                subscription.Status == SubscriptionStatus.Active &&
                subscription.ExpiresAtUtc.HasValue &&
                subscription.ExpiresAtUtc <= utcNow.Date)
            .OrderBy(subscription => subscription.ExpiresAtUtc)
            .ThenBy(subscription => subscription.StartDateUtc)
            .Select(subscription => subscription.PublicId)
            .Take(take)
            .ToListAsync(cancellationToken);

    public Task<CompanySubscription?> GetByPublicIdAsync(Guid subscriptionPublicId, CancellationToken cancellationToken) =>
        dbContext.CompanySubscriptions
            .Include(subscription => subscription.StatusTransitions)
            .SingleOrDefaultAsync(subscription => subscription.PublicId == subscriptionPublicId, cancellationToken);

    public async Task<PlatformCompanySubscriptionResponse?> GetResponseByPublicIdAsync(
        Guid companyPublicId,
        Guid subscriptionPublicId,
        CancellationToken cancellationToken)
    {
        var row =
            await (
                from subscription in dbContext.CompanySubscriptions.AsNoTracking()
                join company in dbContext.Companies.AsNoTracking() on subscription.CompanyId equals company.Id
                join plan in dbContext.CommercialPlans.AsNoTracking() on subscription.CommercialPlanId equals plan.Id
                join version in dbContext.CommercialPlanVersions.AsNoTracking() on subscription.CommercialPlanVersionId equals version.Id
                where company.PublicId == companyPublicId && subscription.PublicId == subscriptionPublicId
                select new SubscriptionProjection(
                    subscription.PublicId,
                    company.PublicId,
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
                    subscription.ExpiresAtUtc,
                    subscription.EndDateUtc,
                    subscription.StatusChangedAtUtc,
                    subscription.CurrentStatusReasonCode,
                    subscription.CurrentStatusObservations,
                    subscription.CurrentStatusOrigin,
                    subscription.ActivatedByUserPublicId,
                    subscription.ActivatedAtUtc,
                    subscription.CreatedUtc,
                    subscription.ModifiedUtc,
                    company.Name,
                    company.Slug,
                    company.IsBillable))
                .SingleOrDefaultAsync(cancellationToken);

        return row is null ? null : MapResponse(row);
    }

    public Task<string?> GetActivePlanCodeAsync(Guid companyPublicId, CancellationToken cancellationToken) =>
        dbContext.CompanySubscriptions
            .Where(subscription =>
                subscription.Status == SubscriptionStatus.Active &&
                dbContext.Companies.Any(company => company.Id == subscription.CompanyId && company.PublicId == companyPublicId))
            .Select(subscription => subscription.PlanCode)
            .SingleOrDefaultAsync(cancellationToken);

    private async Task<List<PlatformCompanySubscriptionResponse>> LoadSubscriptionsByCompanyPublicIdAsync(
        Guid companyPublicId,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from subscription in dbContext.CompanySubscriptions.AsNoTracking()
            join company in dbContext.Companies.AsNoTracking() on subscription.CompanyId equals company.Id
            join plan in dbContext.CommercialPlans.AsNoTracking() on subscription.CommercialPlanId equals plan.Id
            join version in dbContext.CommercialPlanVersions.AsNoTracking() on subscription.CommercialPlanVersionId equals version.Id
            where company.PublicId == companyPublicId
            select new SubscriptionProjection(
                subscription.PublicId,
                company.PublicId,
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
                subscription.ExpiresAtUtc,
                subscription.EndDateUtc,
                subscription.StatusChangedAtUtc,
                subscription.CurrentStatusReasonCode,
                subscription.CurrentStatusObservations,
                subscription.CurrentStatusOrigin,
                subscription.ActivatedByUserPublicId,
                subscription.ActivatedAtUtc,
                subscription.CreatedUtc,
                subscription.ModifiedUtc,
                company.Name,
                company.Slug,
                company.IsBillable))
            .ToListAsync(cancellationToken);

        return rows.Select(MapResponse).ToList();
    }

    private static PlatformCompanySubscriptionResponse MapResponse(SubscriptionProjection row) =>
        new(
            row.SubscriptionId,
            row.CompanyId,
            row.CommercialPlanId,
            row.CommercialPlanVersionId,
            row.PlanCode,
            row.PlanName,
            row.PlanVersionNumber,
            row.BaseMonthlyFee,
            row.PricePerActiveEmployee,
            row.Periodicity,
            row.CurrencyCode,
            row.Status,
            row.StartDateUtc,
            row.ExpiresAtUtc,
            row.EndDateUtc,
            row.StatusChangedAtUtc,
            row.CurrentStatusReasonCode,
            row.CurrentStatusObservations,
            row.CurrentStatusOrigin,
            SubscriptionStatusPolicy.CanOperate(row.Status),
            SubscriptionStatusPolicy.CanGenerateCharges(row.Status),
            row.ActivatedByUserId,
            row.ActivatedAtUtc,
            row.CreatedAtUtc,
            row.ModifiedAtUtc);

    private static PlatformCompanySubscriptionListItemResponse MapListItem(SubscriptionProjection row) =>
        new(
            row.CompanyId,
            row.CompanyName,
            row.CompanySlug,
            row.IsBillable,
            row.SubscriptionId,
            row.CommercialPlanId,
            row.CommercialPlanVersionId,
            row.PlanCode,
            row.PlanName,
            row.PlanVersionNumber,
            row.StartDateUtc,
            row.ExpiresAtUtc,
            row.Periodicity,
            row.CurrencyCode,
            row.Status,
            row.StatusChangedAtUtc,
            row.CurrentStatusReasonCode,
            row.CurrentStatusOrigin,
            SubscriptionStatusPolicy.CanOperate(row.Status),
            SubscriptionStatusPolicy.CanGenerateCharges(row.Status));

    private sealed record SubscriptionProjection(
        Guid SubscriptionId,
        Guid CompanyId,
        Guid CommercialPlanId,
        Guid CommercialPlanVersionId,
        string PlanCode,
        string PlanName,
        int PlanVersionNumber,
        decimal BaseMonthlyFee,
        decimal PricePerActiveEmployee,
        CompanySubscriptionPeriodicity Periodicity,
        string CurrencyCode,
        SubscriptionStatus Status,
        DateTime StartDateUtc,
        DateTime? ExpiresAtUtc,
        DateTime? EndDateUtc,
        DateTime StatusChangedAtUtc,
        SubscriptionStatusChangeReasonCode CurrentStatusReasonCode,
        string? CurrentStatusObservations,
        SubscriptionStatusChangeOrigin CurrentStatusOrigin,
        Guid ActivatedByUserId,
        DateTime ActivatedAtUtc,
        DateTime CreatedAtUtc,
        DateTime? ModifiedAtUtc,
        string CompanyName,
        string CompanySlug,
        bool IsBillable);
}

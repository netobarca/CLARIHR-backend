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

    public void AddStatusChangeRequest(CompanySubscriptionStatusChangeRequest statusChangeRequest) =>
        dbContext.CompanySubscriptionStatusChangeRequests.Add(statusChangeRequest);

    public void AddPlanChange(CompanySubscriptionPlanChange planChange) => dbContext.CompanySubscriptionPlanChanges.Add(planChange);

    public void AddCompanyAddon(CompanyCommercialAddon companyAddon) => dbContext.CompanyCommercialAddons.Add(companyAddon);

    public void AddCompanyAddonChange(CompanyCommercialAddonChange companyAddonChange) => dbContext.CompanyCommercialAddonChanges.Add(companyAddonChange);

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
            join statusChangeRequest in dbContext.CompanySubscriptionStatusChangeRequests.AsNoTracking()
                    .Where(request => request.Status == SubscriptionStatusChangeRequestStatus.Scheduled)
                on subscription.Id equals statusChangeRequest.CompanySubscriptionId into statusChangeRequests
            from statusChangeRequest in statusChangeRequests.DefaultIfEmpty()
            where company.PublicId == companyPublicId
            select new
            {
                Subscription = subscription,
                Company = company,
                Plan = plan,
                Version = version,
                StatusChangeRequest = statusChangeRequest
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
                row.StatusChangeRequest != null ? row.StatusChangeRequest.TargetStatus : null,
                row.StatusChangeRequest != null ? row.StatusChangeRequest.EffectiveDateUtc : null,
                row.StatusChangeRequest != null ? row.StatusChangeRequest.ReasonCode : null,
                row.StatusChangeRequest != null ? row.StatusChangeRequest.Observations : null,
                row.StatusChangeRequest != null ? row.StatusChangeRequest.RequestedAtUtc : null,
                row.StatusChangeRequest != null ? row.StatusChangeRequest.RequestedByUserPublicId : null,
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
                null,
                null,
                null,
                null,
                null,
                null,
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

    public async Task<PagedResponse<PlatformCompanySubscriptionPlanChangeResponse>> SearchPlanChangesByCompanyPublicIdAsync(
        Guid companyPublicId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query =
            from planChange in dbContext.CompanySubscriptionPlanChanges.AsNoTracking()
            join company in dbContext.Companies.AsNoTracking() on planChange.CompanyId equals company.Id
            join currentSubscription in dbContext.CompanySubscriptions.AsNoTracking() on planChange.CompanySubscriptionId equals currentSubscription.Id
            join targetPlan in dbContext.CommercialPlans.AsNoTracking() on planChange.TargetCommercialPlanId equals targetPlan.Id
            join targetVersion in dbContext.CommercialPlanVersions.AsNoTracking() on planChange.TargetCommercialPlanVersionId equals targetVersion.Id
            join currentPlan in dbContext.CommercialPlans.AsNoTracking()
                on planChange.CurrentCommercialPlanId equals (long?)currentPlan.Id into currentPlans
            from currentPlan in currentPlans.DefaultIfEmpty()
            join currentVersion in dbContext.CommercialPlanVersions.AsNoTracking()
                on planChange.CurrentCommercialPlanVersionId equals (long?)currentVersion.Id into currentVersions
            from currentVersion in currentVersions.DefaultIfEmpty()
            where company.PublicId == companyPublicId
            select new
            {
                PlanChange = planChange,
                CompanyPublicId = company.PublicId,
                CurrentSubscriptionPublicId = currentSubscription.PublicId,
                CurrentPlanPublicId = currentPlan != null ? currentPlan.PublicId : (Guid?)null,
                CurrentPlanVersionPublicId = currentVersion != null ? currentVersion.PublicId : (Guid?)null,
                TargetPlanPublicId = targetPlan.PublicId,
                TargetPlanVersionPublicId = targetVersion.PublicId
            };

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(row => row.PlanChange.RequestedAtUtc)
            .ThenByDescending(row => row.PlanChange.EffectiveDateUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(row => new PlanChangeProjection(
                row.PlanChange.PublicId,
                row.CompanyPublicId,
                row.CurrentSubscriptionPublicId,
                row.CurrentPlanPublicId,
                row.CurrentPlanVersionPublicId,
                row.PlanChange.CurrentPlanCode,
                row.PlanChange.CurrentPlanName,
                row.PlanChange.CurrentPlanVersionNumber,
                row.PlanChange.CurrentBaseMonthlyFee,
                row.PlanChange.CurrentPricePerActiveEmployee,
                row.PlanChange.CurrentPeriodicity,
                row.PlanChange.CurrentCurrencyCode,
                row.TargetPlanPublicId,
                row.TargetPlanVersionPublicId,
                row.PlanChange.TargetPlanCode,
                row.PlanChange.TargetPlanName,
                row.PlanChange.TargetPlanVersionNumber,
                row.PlanChange.TargetBaseMonthlyFee,
                row.PlanChange.TargetPricePerActiveEmployee,
                row.PlanChange.TargetPeriodicity,
                row.PlanChange.TargetCurrencyCode,
                row.PlanChange.Mode,
                row.PlanChange.Status,
                row.PlanChange.ReasonCode,
                row.PlanChange.RequestedAtUtc,
                row.PlanChange.EffectiveDateUtc,
                row.PlanChange.RequestedByUserPublicId,
                row.PlanChange.Observations,
                row.PlanChange.ActiveEmployeeCount,
                row.PlanChange.EstimatedNextCharge,
                row.PlanChange.AppliedAtUtc,
                row.PlanChange.AppliedSubscriptionPublicId,
                row.PlanChange.CancelledAtUtc,
                row.PlanChange.CancelledByUserPublicId,
                row.PlanChange.CancellationObservations,
                row.PlanChange.RejectedAtUtc,
                row.PlanChange.RejectionReason))
            .ToListAsync(cancellationToken);

        return new PagedResponse<PlatformCompanySubscriptionPlanChangeResponse>(
            items.Select(MapPlanChangeResponse).ToList(),
            pageNumber,
            pageSize,
            totalCount);
    }

    public async Task<PagedResponse<PlatformCompanyAddonResponse>> SearchCompanyAddonsByCompanyPublicIdAsync(
        Guid companyPublicId,
        CompanyAddonStatus? status,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query =
            from companyAddon in dbContext.CompanyCommercialAddons.AsNoTracking()
            join company in dbContext.Companies.AsNoTracking() on companyAddon.CompanyId equals company.Id
            join subscription in dbContext.CompanySubscriptions.AsNoTracking() on companyAddon.CompanySubscriptionId equals subscription.Id
            join addon in dbContext.CommercialAddons.AsNoTracking() on companyAddon.CommercialAddonId equals addon.Id
            where company.PublicId == companyPublicId
            select new
            {
                CompanyAddon = companyAddon,
                CompanyPublicId = company.PublicId,
                SubscriptionPublicId = subscription.PublicId,
                AddonPublicId = addon.PublicId
            };

        if (status.HasValue)
        {
            query = query.Where(row => row.CompanyAddon.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(row =>
                row.CompanyAddon.AddonCode.Contains(normalizedSearch) ||
                row.CompanyAddon.AddonName.ToUpper().Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(row => row.CompanyAddon.AddonName)
            .ThenBy(row => row.CompanyAddon.AddonCode)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(row => new CompanyAddonProjection(
                row.CompanyAddon.PublicId,
                row.CompanyPublicId,
                row.SubscriptionPublicId,
                row.AddonPublicId,
                row.CompanyAddon.AddonCode,
                row.CompanyAddon.AddonName,
                row.CompanyAddon.AddonType,
                row.CompanyAddon.BillingModel,
                row.CompanyAddon.MeasurementUnit,
                row.CompanyAddon.UnitPrice,
                row.CompanyAddon.MinimumQuantity,
                row.CompanyAddon.MinimumMonthlyFee,
                row.CompanyAddon.Periodicity,
                row.CompanyAddon.CurrencyCode,
                row.CompanyAddon.Status,
                row.CompanyAddon.StatusEffectiveDateUtc,
                row.CompanyAddon.CreatedUtc,
                row.CompanyAddon.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<PlatformCompanyAddonResponse>(
            items.Select(MapCompanyAddonResponse).ToList(),
            pageNumber,
            pageSize,
            totalCount);
    }

    public async Task<PagedResponse<PlatformCompanyEligibleAddonResponse>> SearchEligibleAddonsByCompanyPublicIdAsync(
        Guid companyPublicId,
        CommercialAddonType? type,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query =
            from company in dbContext.Companies.AsNoTracking()
            from addon in dbContext.CommercialAddons.AsNoTracking()
            where company.PublicId == companyPublicId &&
                  addon.Status == CommercialAddonStatus.Active &&
                  !dbContext.CompanyCommercialAddons.Any(state =>
                      state.CompanyId == company.Id &&
                      state.CommercialAddonId == addon.Id &&
                      state.Status != CompanyAddonStatus.Inactive)
            select addon;

        if (type.HasValue)
        {
            query = query.Where(addon => addon.Type == type.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(addon =>
                addon.Code.Contains(normalizedSearch) ||
                addon.Name.ToUpper().Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(addon => addon.Name)
            .ThenBy(addon => addon.Code)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(addon => new PlatformCompanyEligibleAddonResponse(
                addon.PublicId,
                addon.Code,
                addon.Name,
                addon.Description,
                addon.Type,
                addon.BillingModel,
                addon.MeasurementUnit,
                addon.UnitPrice,
                addon.MinimumQuantity,
                addon.MinimumMonthlyFee,
                addon.Periodicity,
                addon.Status,
                addon.CreatedUtc,
                addon.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<PlatformCompanyEligibleAddonResponse>(items, pageNumber, pageSize, totalCount);
    }

    public async Task<PagedResponse<PlatformCompanyAddonChangeResponse>> SearchAddonChangesByCompanyPublicIdAsync(
        Guid companyPublicId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query =
            from addonChange in dbContext.CompanyCommercialAddonChanges.AsNoTracking()
            join company in dbContext.Companies.AsNoTracking() on addonChange.CompanyId equals company.Id
            join subscription in dbContext.CompanySubscriptions.AsNoTracking() on addonChange.CompanySubscriptionId equals subscription.Id
            join addon in dbContext.CommercialAddons.AsNoTracking() on addonChange.CommercialAddonId equals addon.Id
            where company.PublicId == companyPublicId
            select new
            {
                AddonChange = addonChange,
                CompanyPublicId = company.PublicId,
                SubscriptionPublicId = subscription.PublicId,
                AddonPublicId = addon.PublicId
            };

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(row => row.AddonChange.RequestedAtUtc)
            .ThenByDescending(row => row.AddonChange.EffectiveDateUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(row => new CompanyAddonChangeProjection(
                row.AddonChange.PublicId,
                row.CompanyPublicId,
                row.SubscriptionPublicId,
                row.AddonPublicId,
                row.AddonChange.AddonCode,
                row.AddonChange.AddonName,
                row.AddonChange.AddonType,
                row.AddonChange.BillingModel,
                row.AddonChange.MeasurementUnit,
                row.AddonChange.UnitPrice,
                row.AddonChange.MinimumQuantity,
                row.AddonChange.MinimumMonthlyFee,
                row.AddonChange.Periodicity,
                row.AddonChange.CurrencyCode,
                row.AddonChange.Action,
                row.AddonChange.Mode,
                row.AddonChange.Status,
                row.AddonChange.ReasonCode,
                row.AddonChange.PreviousStatus,
                row.AddonChange.ResultingStatus,
                row.AddonChange.RequestedAtUtc,
                row.AddonChange.EffectiveDateUtc,
                row.AddonChange.RequestedByUserPublicId,
                row.AddonChange.Observations,
                row.AddonChange.QuantityBasis,
                row.AddonChange.EstimatedNextChargeImpact,
                row.AddonChange.AppliedAtUtc,
                row.AddonChange.AppliedSubscriptionPublicId,
                row.AddonChange.CancelledAtUtc,
                row.AddonChange.CancelledByUserPublicId,
                row.AddonChange.CancellationObservations,
                row.AddonChange.RejectedAtUtc,
                row.AddonChange.RejectionReason))
            .ToListAsync(cancellationToken);

        return new PagedResponse<PlatformCompanyAddonChangeResponse>(
            items.Select(MapCompanyAddonChangeResponse).ToList(),
            pageNumber,
            pageSize,
            totalCount);
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

    public async Task<IReadOnlyCollection<Guid>> GetDueScheduledStatusChangeRequestIdsAsync(
        DateTime utcNow,
        int take,
        CancellationToken cancellationToken) =>
        await dbContext.CompanySubscriptionStatusChangeRequests
            .AsNoTracking()
            .Where(request =>
                request.Status == SubscriptionStatusChangeRequestStatus.Scheduled &&
                request.EffectiveDateUtc <= utcNow.Date)
            .OrderBy(request => request.EffectiveDateUtc)
            .ThenBy(request => request.RequestedAtUtc)
            .Select(request => request.PublicId)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Guid>> GetDueScheduledPlanChangeIdsAsync(
        DateTime utcNow,
        int take,
        CancellationToken cancellationToken) =>
        await dbContext.CompanySubscriptionPlanChanges
            .AsNoTracking()
            .Where(planChange =>
                planChange.Status == SubscriptionPlanChangeStatus.Scheduled &&
                planChange.EffectiveDateUtc <= utcNow.Date)
            .OrderBy(planChange => planChange.EffectiveDateUtc)
            .ThenBy(planChange => planChange.RequestedAtUtc)
            .Select(planChange => planChange.PublicId)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<Guid>> GetDueScheduledAddonChangeIdsAsync(
        DateTime utcNow,
        int take,
        CancellationToken cancellationToken) =>
        await dbContext.CompanyCommercialAddonChanges
            .AsNoTracking()
            .Where(change =>
                change.Status == SubscriptionAddonChangeStatus.Scheduled &&
                change.EffectiveDateUtc <= utcNow.Date)
            .OrderBy(change => change.EffectiveDateUtc)
            .ThenBy(change => change.RequestedAtUtc)
            .Select(change => change.PublicId)
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

    public Task<CompanySubscriptionStatusChangeRequest?> GetStatusChangeRequestByPublicIdAsync(
        Guid statusChangeRequestPublicId,
        CancellationToken cancellationToken) =>
        dbContext.CompanySubscriptionStatusChangeRequests
            .SingleOrDefaultAsync(request => request.PublicId == statusChangeRequestPublicId, cancellationToken);

    public Task<CompanySubscriptionStatusChangeRequest?> GetScheduledStatusChangeRequestBySubscriptionIdAsync(
        long companySubscriptionId,
        CancellationToken cancellationToken) =>
        dbContext.CompanySubscriptionStatusChangeRequests
            .SingleOrDefaultAsync(
                request =>
                    request.CompanySubscriptionId == companySubscriptionId &&
                    request.Status == SubscriptionStatusChangeRequestStatus.Scheduled,
                cancellationToken);

    public Task<CompanySubscriptionPlanChange?> GetPlanChangeByPublicIdAsync(Guid planChangePublicId, CancellationToken cancellationToken) =>
        dbContext.CompanySubscriptionPlanChanges
            .SingleOrDefaultAsync(planChange => planChange.PublicId == planChangePublicId, cancellationToken);

    public Task<CompanySubscriptionPlanChange?> GetPlanChangeByCompanyAndPublicIdAsync(
        Guid companyPublicId,
        Guid planChangePublicId,
        CancellationToken cancellationToken) =>
        dbContext.CompanySubscriptionPlanChanges
            .SingleOrDefaultAsync(
                planChange =>
                    planChange.PublicId == planChangePublicId &&
                    dbContext.Companies.Any(company => company.Id == planChange.CompanyId && company.PublicId == companyPublicId),
                cancellationToken);

    public Task<CompanySubscriptionPlanChange?> GetScheduledPlanChangeByCompanyIdAsync(long companyId, CancellationToken cancellationToken) =>
        dbContext.CompanySubscriptionPlanChanges
            .SingleOrDefaultAsync(
                planChange =>
                    planChange.CompanyId == companyId &&
                    planChange.Status == SubscriptionPlanChangeStatus.Scheduled,
                cancellationToken);

    public Task<CompanyCommercialAddon?> GetCompanyAddonByCompanyAndAddonPublicIdAsync(
        Guid companyPublicId,
        Guid commercialAddonPublicId,
        CancellationToken cancellationToken) =>
        dbContext.CompanyCommercialAddons
            .SingleOrDefaultAsync(
                companyAddon =>
                    dbContext.Companies.Any(company => company.Id == companyAddon.CompanyId && company.PublicId == companyPublicId) &&
                    dbContext.CommercialAddons.Any(addon => addon.Id == companyAddon.CommercialAddonId && addon.PublicId == commercialAddonPublicId),
                cancellationToken);

    public Task<CompanyCommercialAddon?> GetCompanyAddonByCompanyIdAndAddonIdAsync(
        long companyId,
        long commercialAddonId,
        CancellationToken cancellationToken) =>
        dbContext.CompanyCommercialAddons
            .SingleOrDefaultAsync(
                companyAddon => companyAddon.CompanyId == companyId && companyAddon.CommercialAddonId == commercialAddonId,
                cancellationToken);

    public Task<CompanyCommercialAddonChange?> GetAddonChangeByPublicIdAsync(Guid addonChangePublicId, CancellationToken cancellationToken) =>
        dbContext.CompanyCommercialAddonChanges
            .SingleOrDefaultAsync(change => change.PublicId == addonChangePublicId, cancellationToken);

    public Task<CompanyCommercialAddonChange?> GetAddonChangeByCompanyAndPublicIdAsync(
        Guid companyPublicId,
        Guid addonChangePublicId,
        CancellationToken cancellationToken) =>
        dbContext.CompanyCommercialAddonChanges
            .SingleOrDefaultAsync(
                change =>
                    change.PublicId == addonChangePublicId &&
                    dbContext.Companies.Any(company => company.Id == change.CompanyId && company.PublicId == companyPublicId),
                cancellationToken);

    public Task<CompanyCommercialAddonChange?> GetScheduledAddonChangeByCompanyAndAddonIdAsync(
        long companyId,
        long commercialAddonId,
        CancellationToken cancellationToken) =>
        dbContext.CompanyCommercialAddonChanges
            .SingleOrDefaultAsync(
                change =>
                    change.CompanyId == companyId &&
                    change.CommercialAddonId == commercialAddonId &&
                    change.Status == SubscriptionAddonChangeStatus.Scheduled,
                cancellationToken);

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
                join statusChangeRequest in dbContext.CompanySubscriptionStatusChangeRequests.AsNoTracking()
                        .Where(request => request.Status == SubscriptionStatusChangeRequestStatus.Scheduled)
                    on subscription.Id equals statusChangeRequest.CompanySubscriptionId into statusChangeRequests
                from statusChangeRequest in statusChangeRequests.DefaultIfEmpty()
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
                    statusChangeRequest != null ? statusChangeRequest.TargetStatus : null,
                    statusChangeRequest != null ? statusChangeRequest.EffectiveDateUtc : null,
                    statusChangeRequest != null ? statusChangeRequest.ReasonCode : null,
                    statusChangeRequest != null ? statusChangeRequest.Observations : null,
                    statusChangeRequest != null ? statusChangeRequest.RequestedAtUtc : null,
                    statusChangeRequest != null ? statusChangeRequest.RequestedByUserPublicId : null,
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

    public async Task<PlatformCompanySubscriptionPlanChangeResponse?> GetPlanChangeResponseByPublicIdAsync(
        Guid companyPublicId,
        Guid planChangePublicId,
        CancellationToken cancellationToken)
    {
        var row =
            await (
                from planChange in dbContext.CompanySubscriptionPlanChanges.AsNoTracking()
                join company in dbContext.Companies.AsNoTracking() on planChange.CompanyId equals company.Id
                join currentSubscription in dbContext.CompanySubscriptions.AsNoTracking() on planChange.CompanySubscriptionId equals currentSubscription.Id
                join targetPlan in dbContext.CommercialPlans.AsNoTracking() on planChange.TargetCommercialPlanId equals targetPlan.Id
                join targetVersion in dbContext.CommercialPlanVersions.AsNoTracking() on planChange.TargetCommercialPlanVersionId equals targetVersion.Id
                join currentPlan in dbContext.CommercialPlans.AsNoTracking()
                    on planChange.CurrentCommercialPlanId equals (long?)currentPlan.Id into currentPlans
                from currentPlan in currentPlans.DefaultIfEmpty()
                join currentVersion in dbContext.CommercialPlanVersions.AsNoTracking()
                    on planChange.CurrentCommercialPlanVersionId equals (long?)currentVersion.Id into currentVersions
                from currentVersion in currentVersions.DefaultIfEmpty()
                where company.PublicId == companyPublicId && planChange.PublicId == planChangePublicId
                select new PlanChangeProjection(
                    planChange.PublicId,
                    company.PublicId,
                    currentSubscription.PublicId,
                    currentPlan != null ? currentPlan.PublicId : (Guid?)null,
                    currentVersion != null ? currentVersion.PublicId : (Guid?)null,
                    planChange.CurrentPlanCode,
                    planChange.CurrentPlanName,
                    planChange.CurrentPlanVersionNumber,
                    planChange.CurrentBaseMonthlyFee,
                    planChange.CurrentPricePerActiveEmployee,
                    planChange.CurrentPeriodicity,
                    planChange.CurrentCurrencyCode,
                    targetPlan.PublicId,
                    targetVersion.PublicId,
                    planChange.TargetPlanCode,
                    planChange.TargetPlanName,
                    planChange.TargetPlanVersionNumber,
                    planChange.TargetBaseMonthlyFee,
                    planChange.TargetPricePerActiveEmployee,
                    planChange.TargetPeriodicity,
                    planChange.TargetCurrencyCode,
                    planChange.Mode,
                    planChange.Status,
                    planChange.ReasonCode,
                    planChange.RequestedAtUtc,
                    planChange.EffectiveDateUtc,
                    planChange.RequestedByUserPublicId,
                    planChange.Observations,
                    planChange.ActiveEmployeeCount,
                    planChange.EstimatedNextCharge,
                    planChange.AppliedAtUtc,
                    planChange.AppliedSubscriptionPublicId,
                    planChange.CancelledAtUtc,
                    planChange.CancelledByUserPublicId,
                    planChange.CancellationObservations,
                    planChange.RejectedAtUtc,
                    planChange.RejectionReason))
                .SingleOrDefaultAsync(cancellationToken);

        return row is null ? null : MapPlanChangeResponse(row);
    }

    public async Task<PlatformCompanyAddonChangeResponse?> GetAddonChangeResponseByPublicIdAsync(
        Guid companyPublicId,
        Guid addonChangePublicId,
        CancellationToken cancellationToken)
    {
        var row =
            await (
                from addonChange in dbContext.CompanyCommercialAddonChanges.AsNoTracking()
                join company in dbContext.Companies.AsNoTracking() on addonChange.CompanyId equals company.Id
                join subscription in dbContext.CompanySubscriptions.AsNoTracking() on addonChange.CompanySubscriptionId equals subscription.Id
                join addon in dbContext.CommercialAddons.AsNoTracking() on addonChange.CommercialAddonId equals addon.Id
                where company.PublicId == companyPublicId && addonChange.PublicId == addonChangePublicId
                select new CompanyAddonChangeProjection(
                    addonChange.PublicId,
                    company.PublicId,
                    subscription.PublicId,
                    addon.PublicId,
                    addonChange.AddonCode,
                    addonChange.AddonName,
                    addonChange.AddonType,
                    addonChange.BillingModel,
                    addonChange.MeasurementUnit,
                    addonChange.UnitPrice,
                    addonChange.MinimumQuantity,
                    addonChange.MinimumMonthlyFee,
                    addonChange.Periodicity,
                    addonChange.CurrencyCode,
                    addonChange.Action,
                    addonChange.Mode,
                    addonChange.Status,
                    addonChange.ReasonCode,
                    addonChange.PreviousStatus,
                    addonChange.ResultingStatus,
                    addonChange.RequestedAtUtc,
                    addonChange.EffectiveDateUtc,
                    addonChange.RequestedByUserPublicId,
                    addonChange.Observations,
                    addonChange.QuantityBasis,
                    addonChange.EstimatedNextChargeImpact,
                    addonChange.AppliedAtUtc,
                    addonChange.AppliedSubscriptionPublicId,
                    addonChange.CancelledAtUtc,
                    addonChange.CancelledByUserPublicId,
                    addonChange.CancellationObservations,
                    addonChange.RejectedAtUtc,
                    addonChange.RejectionReason))
                .SingleOrDefaultAsync(cancellationToken);

        return row is null ? null : MapCompanyAddonChangeResponse(row);
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
            join statusChangeRequest in dbContext.CompanySubscriptionStatusChangeRequests.AsNoTracking()
                    .Where(request => request.Status == SubscriptionStatusChangeRequestStatus.Scheduled)
                on subscription.Id equals statusChangeRequest.CompanySubscriptionId into statusChangeRequests
            from statusChangeRequest in statusChangeRequests.DefaultIfEmpty()
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
                statusChangeRequest != null ? statusChangeRequest.TargetStatus : null,
                statusChangeRequest != null ? statusChangeRequest.EffectiveDateUtc : null,
                statusChangeRequest != null ? statusChangeRequest.ReasonCode : null,
                statusChangeRequest != null ? statusChangeRequest.Observations : null,
                statusChangeRequest != null ? statusChangeRequest.RequestedAtUtc : null,
                statusChangeRequest != null ? statusChangeRequest.RequestedByUserPublicId : null,
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
            row.PendingTargetStatus.HasValue
                ? new PlatformCompanySubscriptionPendingStatusChangeResponse(
                    row.PendingTargetStatus.Value,
                    row.PendingEffectiveDateUtc!.Value,
                    row.PendingReasonCode!.Value,
                    row.PendingObservations,
                    row.PendingRequestedAtUtc!.Value,
                    row.PendingRequestedByUserId)
                : null,
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

    private static PlatformCompanySubscriptionPlanChangeResponse MapPlanChangeResponse(PlanChangeProjection row) =>
        new(
            row.PlanChangeId,
            row.CompanyId,
            row.CurrentSubscriptionId,
            row.CurrentCommercialPlanId ?? Guid.Empty,
            row.CurrentCommercialPlanVersionId ?? Guid.Empty,
            row.CurrentPlanCode,
            row.CurrentPlanName,
            row.CurrentPlanVersionNumber,
            row.CurrentBaseMonthlyFee,
            row.CurrentPricePerActiveEmployee,
            row.CurrentPeriodicity,
            row.CurrentCurrencyCode,
            row.TargetCommercialPlanId,
            row.TargetCommercialPlanVersionId,
            row.TargetPlanCode,
            row.TargetPlanName,
            row.TargetPlanVersionNumber,
            row.TargetBaseMonthlyFee,
            row.TargetPricePerActiveEmployee,
            row.TargetPeriodicity,
            row.TargetCurrencyCode,
            row.Mode,
            row.Status,
            row.ReasonCode,
            row.RequestedAtUtc,
            row.EffectiveDateUtc,
            row.RequestedByUserId,
            row.Observations,
            row.ActiveEmployeeCount,
            row.EstimatedNextCharge,
            row.AppliedAtUtc,
            row.AppliedSubscriptionId,
            row.CancelledAtUtc,
            row.CancelledByUserId,
            row.CancellationObservations,
            row.RejectedAtUtc,
            row.RejectionReason);

    private static PlatformCompanyAddonResponse MapCompanyAddonResponse(CompanyAddonProjection row) =>
        new(
            row.CompanyAddonId,
            row.CompanyId,
            row.CompanySubscriptionId,
            row.CommercialAddonId,
            row.AddonCode,
            row.AddonName,
            row.AddonType,
            row.BillingModel,
            row.MeasurementUnit,
            row.UnitPrice,
            row.MinimumQuantity,
            row.MinimumMonthlyFee,
            row.Periodicity,
            row.CurrencyCode,
            row.Status,
            row.StatusEffectiveDateUtc,
            row.CreatedAtUtc,
            row.ModifiedAtUtc);

    private static PlatformCompanyAddonChangeResponse MapCompanyAddonChangeResponse(CompanyAddonChangeProjection row) =>
        new(
            row.AddonChangeId,
            row.CompanyId,
            row.CompanySubscriptionId,
            row.CommercialAddonId,
            row.AddonCode,
            row.AddonName,
            row.AddonType,
            row.BillingModel,
            row.MeasurementUnit,
            row.UnitPrice,
            row.MinimumQuantity,
            row.MinimumMonthlyFee,
            row.Periodicity,
            row.CurrencyCode,
            row.Action,
            row.Mode,
            row.Status,
            row.ReasonCode,
            row.PreviousStatus,
            row.ResultingStatus,
            row.RequestedAtUtc,
            row.EffectiveDateUtc,
            row.RequestedByUserId,
            row.Observations,
            row.QuantityBasis,
            row.EstimatedNextChargeImpact,
            row.AppliedAtUtc,
            row.AppliedSubscriptionId,
            row.CancelledAtUtc,
            row.CancelledByUserId,
            row.CancellationObservations,
            row.RejectedAtUtc,
            row.RejectionReason);

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
        SubscriptionStatus? PendingTargetStatus,
        DateTime? PendingEffectiveDateUtc,
        SubscriptionStatusChangeReasonCode? PendingReasonCode,
        string? PendingObservations,
        DateTime? PendingRequestedAtUtc,
        Guid? PendingRequestedByUserId,
        Guid ActivatedByUserId,
        DateTime ActivatedAtUtc,
        DateTime CreatedAtUtc,
        DateTime? ModifiedAtUtc,
        string CompanyName,
        string CompanySlug,
        bool IsBillable);

    private sealed record PlanChangeProjection(
        Guid PlanChangeId,
        Guid CompanyId,
        Guid CurrentSubscriptionId,
        Guid? CurrentCommercialPlanId,
        Guid? CurrentCommercialPlanVersionId,
        string CurrentPlanCode,
        string CurrentPlanName,
        int CurrentPlanVersionNumber,
        decimal CurrentBaseMonthlyFee,
        decimal CurrentPricePerActiveEmployee,
        CompanySubscriptionPeriodicity CurrentPeriodicity,
        string CurrentCurrencyCode,
        Guid TargetCommercialPlanId,
        Guid TargetCommercialPlanVersionId,
        string TargetPlanCode,
        string TargetPlanName,
        int TargetPlanVersionNumber,
        decimal TargetBaseMonthlyFee,
        decimal TargetPricePerActiveEmployee,
        CompanySubscriptionPeriodicity TargetPeriodicity,
        string TargetCurrencyCode,
        SubscriptionPlanChangeMode Mode,
        SubscriptionPlanChangeStatus Status,
        SubscriptionPlanChangeReasonCode ReasonCode,
        DateTime RequestedAtUtc,
        DateTime EffectiveDateUtc,
        Guid? RequestedByUserId,
        string? Observations,
        int ActiveEmployeeCount,
        decimal EstimatedNextCharge,
        DateTime? AppliedAtUtc,
        Guid? AppliedSubscriptionId,
        DateTime? CancelledAtUtc,
        Guid? CancelledByUserId,
        string? CancellationObservations,
        DateTime? RejectedAtUtc,
        string? RejectionReason);

    private sealed record CompanyAddonProjection(
        Guid CompanyAddonId,
        Guid CompanyId,
        Guid CompanySubscriptionId,
        Guid CommercialAddonId,
        string AddonCode,
        string AddonName,
        CommercialAddonType AddonType,
        CommercialAddonBillingModel BillingModel,
        string MeasurementUnit,
        decimal UnitPrice,
        int? MinimumQuantity,
        decimal? MinimumMonthlyFee,
        CommercialAddonPeriodicity Periodicity,
        string CurrencyCode,
        CompanyAddonStatus Status,
        DateTime StatusEffectiveDateUtc,
        DateTime CreatedAtUtc,
        DateTime? ModifiedAtUtc);

    private sealed record CompanyAddonChangeProjection(
        Guid AddonChangeId,
        Guid CompanyId,
        Guid CompanySubscriptionId,
        Guid CommercialAddonId,
        string AddonCode,
        string AddonName,
        CommercialAddonType AddonType,
        CommercialAddonBillingModel BillingModel,
        string MeasurementUnit,
        decimal UnitPrice,
        int? MinimumQuantity,
        decimal? MinimumMonthlyFee,
        CommercialAddonPeriodicity Periodicity,
        string CurrencyCode,
        SubscriptionAddonChangeAction Action,
        SubscriptionAddonChangeMode Mode,
        SubscriptionAddonChangeStatus Status,
        SubscriptionAddonChangeReasonCode ReasonCode,
        CompanyAddonStatus PreviousStatus,
        CompanyAddonStatus ResultingStatus,
        DateTime RequestedAtUtc,
        DateTime EffectiveDateUtc,
        Guid? RequestedByUserId,
        string? Observations,
        int QuantityBasis,
        decimal EstimatedNextChargeImpact,
        DateTime? AppliedAtUtc,
        Guid? AppliedSubscriptionId,
        DateTime? CancelledAtUtc,
        Guid? CancelledByUserId,
        string? CancellationObservations,
        DateTime? RejectedAtUtc,
        string? RejectionReason);
}

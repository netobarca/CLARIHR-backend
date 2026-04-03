using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PlatformSubscriptions;
using CLARIHR.Domain.Companies;

namespace CLARIHR.Application.Abstractions.Companies;

public interface ICompanySubscriptionRepository
{
    void Add(CompanySubscription subscription);

    void AddStatusChangeRequest(CompanySubscriptionStatusChangeRequest statusChangeRequest);

    void AddPlanChange(CompanySubscriptionPlanChange planChange);

    void AddCompanyAddon(CompanyCommercialAddon companyAddon);

    void AddCompanyAddonChange(CompanyCommercialAddonChange companyAddonChange);

    Task<CompanySubscription?> GetActiveByCompanyIdAsync(long companyId, CancellationToken cancellationToken);

    Task<CompanySubscription?> GetCurrentByCompanyIdAsync(long companyId, CancellationToken cancellationToken);

    Task<CompanySubscription?> GetScheduledByCompanyIdAsync(long companyId, CancellationToken cancellationToken);

    Task<CompanySubscription?> GetActiveByCompanyPublicIdAsync(Guid companyPublicId, CancellationToken cancellationToken);

    Task<CompanySubscription?> GetByCompanyAndSubscriptionPublicIdAsync(
        Guid companyPublicId,
        Guid subscriptionPublicId,
        CancellationToken cancellationToken);

    Task<PlatformCompanySubscriptionOverviewResponse?> GetOverviewByCompanyPublicIdAsync(Guid companyPublicId, CancellationToken cancellationToken);

    Task<PagedResponse<PlatformCompanySubscriptionResponse>> SearchByCompanyPublicIdAsync(
        Guid companyPublicId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<PagedResponse<PlatformCompanySubscriptionListItemResponse>> SearchAsync(
        SubscriptionStatus? status,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<PagedResponse<PlatformCompanySubscriptionStatusTransitionResponse>> SearchStatusHistoryAsync(
        Guid companyPublicId,
        Guid subscriptionPublicId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<PagedResponse<PlatformCompanySubscriptionPlanChangeResponse>> SearchPlanChangesByCompanyPublicIdAsync(
        Guid companyPublicId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<PagedResponse<PlatformCompanyAddonResponse>> SearchCompanyAddonsByCompanyPublicIdAsync(
        Guid companyPublicId,
        CompanyAddonStatus? status,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<PagedResponse<PlatformCompanyEligibleAddonResponse>> SearchEligibleAddonsByCompanyPublicIdAsync(
        Guid companyPublicId,
        CommercialAddonType? type,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<PagedResponse<PlatformCompanyAddonChangeResponse>> SearchAddonChangesByCompanyPublicIdAsync(
        Guid companyPublicId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Guid>> GetDueScheduledSubscriptionIdsAsync(
        DateTime utcNow,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Guid>> GetDueScheduledStatusChangeRequestIdsAsync(
        DateTime utcNow,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Guid>> GetDueScheduledPlanChangeIdsAsync(
        DateTime utcNow,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Guid>> GetDueScheduledAddonChangeIdsAsync(
        DateTime utcNow,
        int take,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Guid>> GetDueExpiringSubscriptionIdsAsync(
        DateTime utcNow,
        int take,
        CancellationToken cancellationToken);

    Task<CompanySubscription?> GetByPublicIdAsync(Guid subscriptionPublicId, CancellationToken cancellationToken);

    Task<CompanySubscriptionStatusChangeRequest?> GetStatusChangeRequestByPublicIdAsync(
        Guid statusChangeRequestPublicId,
        CancellationToken cancellationToken);

    Task<CompanySubscriptionStatusChangeRequest?> GetScheduledStatusChangeRequestBySubscriptionIdAsync(
        long companySubscriptionId,
        CancellationToken cancellationToken);

    Task<CompanySubscriptionPlanChange?> GetPlanChangeByPublicIdAsync(Guid planChangePublicId, CancellationToken cancellationToken);

    Task<CompanySubscriptionPlanChange?> GetPlanChangeByCompanyAndPublicIdAsync(
        Guid companyPublicId,
        Guid planChangePublicId,
        CancellationToken cancellationToken);

    Task<CompanySubscriptionPlanChange?> GetScheduledPlanChangeByCompanyIdAsync(long companyId, CancellationToken cancellationToken);

    Task<CompanyCommercialAddon?> GetCompanyAddonByCompanyAndAddonPublicIdAsync(
        Guid companyPublicId,
        Guid commercialAddonPublicId,
        CancellationToken cancellationToken);

    Task<CompanyCommercialAddon?> GetCompanyAddonByCompanyIdAndAddonIdAsync(
        long companyId,
        long commercialAddonId,
        CancellationToken cancellationToken);

    Task<CompanyCommercialAddonChange?> GetAddonChangeByPublicIdAsync(Guid addonChangePublicId, CancellationToken cancellationToken);

    Task<CompanyCommercialAddonChange?> GetAddonChangeByCompanyAndPublicIdAsync(
        Guid companyPublicId,
        Guid addonChangePublicId,
        CancellationToken cancellationToken);

    Task<CompanyCommercialAddonChange?> GetScheduledAddonChangeByCompanyAndAddonIdAsync(
        long companyId,
        long commercialAddonId,
        CancellationToken cancellationToken);

    Task<PlatformCompanySubscriptionResponse?> GetResponseByPublicIdAsync(
        Guid companyPublicId,
        Guid subscriptionPublicId,
        CancellationToken cancellationToken);

    Task<PlatformCompanySubscriptionPlanChangeResponse?> GetPlanChangeResponseByPublicIdAsync(
        Guid companyPublicId,
        Guid planChangePublicId,
        CancellationToken cancellationToken);

    Task<PlatformCompanyAddonChangeResponse?> GetAddonChangeResponseByPublicIdAsync(
        Guid companyPublicId,
        Guid addonChangePublicId,
        CancellationToken cancellationToken);

    Task<string?> GetActivePlanCodeAsync(Guid companyPublicId, CancellationToken cancellationToken);
}

using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PlatformSubscriptions;
using CLARIHR.Domain.Companies;

namespace CLARIHR.Application.Abstractions.Companies;

public interface ICompanySubscriptionRepository
{
    void Add(CompanySubscription subscription);

    Task<CompanySubscription?> GetActiveByCompanyIdAsync(long companyId, CancellationToken cancellationToken);

    Task<CompanySubscription?> GetScheduledByCompanyIdAsync(long companyId, CancellationToken cancellationToken);

    Task<CompanySubscription?> GetActiveByCompanyPublicIdAsync(Guid companyPublicId, CancellationToken cancellationToken);

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

    Task<IReadOnlyCollection<Guid>> GetDueScheduledSubscriptionIdsAsync(
        DateTime utcNow,
        int take,
        CancellationToken cancellationToken);

    Task<CompanySubscription?> GetByPublicIdAsync(Guid subscriptionPublicId, CancellationToken cancellationToken);

    Task<string?> GetActivePlanCodeAsync(Guid companyPublicId, CancellationToken cancellationToken);
}

using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Domain.JobProfiles;

namespace CLARIHR.Application.Abstractions.JobProfiles;

public interface IJobCatalogRepository
{
    void Add(JobCatalogItem item);

    void Remove(JobCatalogItem item);

    Task<JobCatalogItem?> GetByIdAsync(Guid itemId, CancellationToken cancellationToken);

    Task<bool> ExistsOutsideTenantAsync(Guid itemId, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(
        Guid tenantId,
        JobCatalogCategory category,
        string normalizedCode,
        long? excludingItemId,
        CancellationToken cancellationToken);

    Task<bool> HasUsageAsync(long catalogItemId, CancellationToken cancellationToken);

    Task<PagedResponse<JobCatalogItemResponse>> SearchAsync(
        Guid tenantId,
        JobCatalogCategory category,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<JobCatalogItemResponse?> GetResponseByIdAsync(Guid itemId, CancellationToken cancellationToken);

    Task<JobCatalogItem?> ResolveActiveItemAsync(
        Guid tenantId,
        JobCatalogCategory category,
        Guid itemId,
        CancellationToken cancellationToken);

    Task<JobCatalogItem?> FindActiveByNameAsync(
        Guid tenantId,
        JobCatalogCategory category,
        string normalizedName,
        CancellationToken cancellationToken);

    void InvalidateCategoryCache(Guid tenantId, JobCatalogCategory category);
}

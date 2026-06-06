using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.CostCenters;
using CLARIHR.Domain.CostCenters;

namespace CLARIHR.Application.Abstractions.CostCenters;

public interface ICostCenterRepository
{
    void Add(CostCenter costCenter);

    Task<CostCenter?> GetByIdAsync(Guid costCenterId, CancellationToken cancellationToken);

    Task<bool> ExistsOutsideTenantAsync(Guid costCenterId, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(Guid tenantId, string normalizedCode, long? excludingCostCenterId, CancellationToken cancellationToken);

    Task<bool> ExistsActiveByCodeAsync(Guid tenantId, string normalizedCode, CancellationToken cancellationToken);

    Task<PagedResponse<CostCenterListItemResponse>> SearchAsync(
        Guid tenantId,
        CostCenterType? type,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<CostCenterResponse?> GetResponseByIdAsync(Guid costCenterId, CancellationToken cancellationToken);

    Task<CostCenterUsageResponse?> GetUsageByIdAsync(Guid costCenterId, CancellationToken cancellationToken);

    Task<bool> HasActiveUsageAsync(long costCenterId, CancellationToken cancellationToken);

    /// <summary>
    /// Boolean usage probe by (tenant, normalized code) — whether the cost center is referenced by
    /// any ACTIVE org unit or position slot. Cheaper than <see cref="GetUsageByIdAsync"/> (which
    /// computes the full reference breakdown); used by the detail GET to set the allowedActions
    /// <c>hasActiveUsage</c> flag.
    /// </summary>
    Task<bool> HasActiveUsageAsync(Guid tenantId, string normalizedCode, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CostCenterExportRow>> GetExportRowsAsync(
        Guid tenantId,
        CostCenterType? type,
        bool? isActive,
        string? search,
        int? maxRows,
        CancellationToken cancellationToken);
}

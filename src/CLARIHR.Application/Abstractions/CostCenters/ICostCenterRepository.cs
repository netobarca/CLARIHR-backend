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

    Task<IReadOnlyCollection<CostCenterExportRow>> GetExportRowsAsync(
        Guid tenantId,
        CostCenterType? type,
        bool? isActive,
        string? search,
        int? maxRows,
        CancellationToken cancellationToken);
}

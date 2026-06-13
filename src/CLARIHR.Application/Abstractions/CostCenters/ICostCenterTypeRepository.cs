using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.CostCenters.Types;
using CLARIHR.Domain.CostCenters;

namespace CLARIHR.Application.Abstractions.CostCenters;

public interface ICostCenterTypeRepository
{
    void Add(CostCenterType costCenterType);

    Task<CostCenterType?> GetByIdAsync(Guid costCenterTypeId, CancellationToken cancellationToken);

    Task<bool> ExistsOutsideTenantAsync(Guid costCenterTypeId, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(Guid tenantId, string normalizedCode, long? excludingCostCenterTypeId, CancellationToken cancellationToken);

    Task<PagedResponse<CostCenterTypeListItemResponse>> SearchAsync(
        Guid tenantId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<bool> HasActiveCostCentersAsync(long costCenterTypeId, CancellationToken cancellationToken);
}

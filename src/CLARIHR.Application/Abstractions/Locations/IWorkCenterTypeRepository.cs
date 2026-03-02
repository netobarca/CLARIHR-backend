using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Locations.WorkCenterTypes;
using CLARIHR.Domain.Locations;

namespace CLARIHR.Application.Abstractions.Locations;

public interface IWorkCenterTypeRepository
{
    void Add(WorkCenterType workCenterType);

    Task<WorkCenterType?> GetByIdAsync(Guid workCenterTypeId, CancellationToken cancellationToken);

    Task<bool> ExistsOutsideTenantAsync(Guid workCenterTypeId, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(Guid tenantId, string normalizedCode, long? excludingWorkCenterTypeId, CancellationToken cancellationToken);

    Task<PagedResponse<WorkCenterTypeResponse>> SearchAsync(
        Guid tenantId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<bool> HasActiveWorkCentersAsync(long workCenterTypeId, CancellationToken cancellationToken);
}

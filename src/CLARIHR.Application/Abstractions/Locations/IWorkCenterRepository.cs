using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Locations.WorkCenters;
using CLARIHR.Domain.Locations;

namespace CLARIHR.Application.Abstractions.Locations;

public interface IWorkCenterRepository
{
    void Add(WorkCenter workCenter);

    Task<WorkCenter?> GetByIdAsync(Guid workCenterId, CancellationToken cancellationToken);

    Task<bool> ExistsOutsideTenantAsync(Guid workCenterId, CancellationToken cancellationToken);

    Task<bool> HasAnyActiveWorkCentersAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(Guid tenantId, string normalizedCode, long? excludingWorkCenterId, CancellationToken cancellationToken);

    Task<PagedResponse<WorkCenterResponse>> SearchAsync(
        Guid tenantId,
        Guid? groupId,
        Guid? typeId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<WorkCenterResponse?> GetResponseByIdAsync(Guid workCenterId, CancellationToken cancellationToken);

    Task<bool> HasActiveWorkCentersInGroupAsync(long locationGroupId, long? excludingWorkCenterId, CancellationToken cancellationToken);

    Task<bool> HasActiveWorkCentersForTypeAsync(long workCenterTypeId, long? excludingWorkCenterId, CancellationToken cancellationToken);
}

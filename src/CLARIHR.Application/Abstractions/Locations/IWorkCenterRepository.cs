using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Locations.WorkCenters;
using CLARIHR.Domain.Locations;

namespace CLARIHR.Application.Abstractions.Locations;

public interface IWorkCenterRepository
{
    void Add(WorkCenter workCenter);

    /// <summary>00003 / B-04 — borrado duro, condicionado por <see cref="GetUsageByIdAsync"/>.</summary>
    void Remove(WorkCenter workCenter);

    /// <summary>
    /// Que referencia al centro, separado por origen. Incluye las asignaciones de expediente, que
    /// apuntan por <c>WorkCenterPublicId</c> y NO tienen clave foranea.
    /// </summary>
    Task<WorkCenterUsageResponse?> GetUsageByIdAsync(Guid workCenterId, CancellationToken cancellationToken);

    Task<WorkCenter?> GetByIdAsync(Guid workCenterId, CancellationToken cancellationToken);

    Task<bool> ExistsOutsideTenantAsync(Guid workCenterId, CancellationToken cancellationToken);

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

using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PositionSlots;
using CLARIHR.Domain.PositionSlots;

namespace CLARIHR.Application.Abstractions.PositionSlots;

public interface IPositionSlotRepository
{
    void Add(PositionSlot slot);

    Task<PositionSlot?> GetByIdAsync(Guid slotId, CancellationToken cancellationToken);

    Task<bool> ExistsOutsideTenantAsync(Guid slotId, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(Guid tenantId, string normalizedCode, long? excludingSlotId, CancellationToken cancellationToken);

    Task<long?> ResolveJobProfileIdAsync(Guid tenantId, Guid jobProfileId, CancellationToken cancellationToken);

    Task<bool> JobProfileExistsOutsideTenantAsync(Guid jobProfileId, CancellationToken cancellationToken);

    Task<long?> ResolveOrgUnitIdAsync(Guid tenantId, Guid orgUnitId, CancellationToken cancellationToken);

    Task<bool> OrgUnitExistsOutsideTenantAsync(Guid orgUnitId, CancellationToken cancellationToken);

    Task<long?> ResolveWorkCenterIdAsync(Guid tenantId, Guid workCenterId, CancellationToken cancellationToken);

    Task<bool> WorkCenterExistsOutsideTenantAsync(Guid workCenterId, CancellationToken cancellationToken);

    Task<long?> ResolvePositionSlotIdAsync(Guid tenantId, Guid slotId, CancellationToken cancellationToken);

    Task<PagedResponse<PositionSlotListItemResponse>> SearchAsync(
        Guid tenantId,
        PositionSlotStatus? status,
        Guid? jobProfileId,
        Guid? orgUnitId,
        Guid? workCenterId,
        bool? isFixedTerm,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<PositionSlotResponse?> GetResponseByIdAsync(Guid slotId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PositionSlotGraphNodeData>> GetGraphNodesAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PositionSlotExportRow>> GetExportRowsAsync(
        Guid tenantId,
        PositionSlotStatus? status,
        Guid? jobProfileId,
        Guid? orgUnitId,
        Guid? workCenterId,
        bool? isFixedTerm,
        string? search,
        CancellationToken cancellationToken);
}

using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PositionSlots;
using CLARIHR.Domain.PositionSlots;

namespace CLARIHR.Application.Abstractions.PositionSlots;

public interface IPositionSlotRepository
{
    void Add(PositionSlot slot);

    Task<PositionSlot?> GetByIdAsync(Guid slotId, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves the salary band of a position slot from its job profile's active tabulator line
    /// (PositionSlot → JobProfileCompensation → SalaryTabulatorLine). Null when the slot/profile has no
    /// active tabulator line (no band configured); used to block out-of-range negotiated salaries (R-3).
    /// </summary>
    // Default no-op (no band) so test doubles need not implement it; the production repository overrides
    // it with the real PositionSlot → JobProfileCompensation → SalaryTabulatorLine query.
    Task<PositionSlotSalaryRange?> GetSalaryRangeAsync(Guid slotPublicId, CancellationToken cancellationToken) =>
        Task.FromResult<PositionSlotSalaryRange?>(null);

    Task<bool> ExistsOutsideTenantAsync(Guid slotId, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(Guid tenantId, string normalizedCode, long? excludingSlotId, CancellationToken cancellationToken);

    Task<bool> JobProfileExistsOutsideTenantAsync(Guid jobProfileId, CancellationToken cancellationToken);

    Task<long?> ResolveWorkCenterIdAsync(Guid tenantId, Guid workCenterId, CancellationToken cancellationToken);

    Task<bool> WorkCenterExistsOutsideTenantAsync(Guid workCenterId, CancellationToken cancellationToken);

    Task<long?> ResolvePositionSlotIdAsync(Guid tenantId, Guid slotId, CancellationToken cancellationToken);

    Task<PagedResponse<PositionSlotListItemResponse>> SearchAsync(
        Guid tenantId,
        PositionSlotStatus? status,
        Guid? jobProfileId,
        Guid? orgUnitId,
        Guid? workCenterId,
        Guid? contractTypeId,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<PositionSlotResponse?> GetResponseByIdAsync(Guid slotId, CancellationToken cancellationToken);

    Task<int> CountSlotsAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PositionSlotGraphNodeData>> GetGraphNodesAsync(Guid tenantId, CancellationToken cancellationToken);

    // PS-G: lean dependency adjacency (internal ids only) for the dependency-mutation cycle check —
    // avoids the wide 8-table join that GetGraphNodesAsync performs for the read/diagram path.
    Task<IReadOnlyCollection<PositionSlotDependencyAdjacency>> GetDependencyAdjacencyAsync(Guid tenantId, CancellationToken cancellationToken);

    // RA-1: acquire a transaction-scoped, per-tenant lock that serializes dependency mutations so the
    // cross-slot acyclicity check reads a consistent adjacency snapshot (the per-slot concurrency token
    // does not cover an invariant that spans two slots). MUST be called inside an open transaction; the
    // lock releases automatically on commit/rollback.
    Task AcquireDependencyMutationLockAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<PositionSlotExportRow>> GetExportRowsAsync(
        Guid tenantId,
        PositionSlotStatus? status,
        Guid? jobProfileId,
        Guid? orgUnitId,
        Guid? workCenterId,
        Guid? contractTypeId,
        string? search,
        int? maxRows,
        CancellationToken cancellationToken);

    Task<PositionSlotJobProfileLookup?> GetJobProfileLookupAsync(
        Guid tenantId,
        Guid jobProfileId,
        CancellationToken cancellationToken);
}

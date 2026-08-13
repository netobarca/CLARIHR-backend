using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PositionSlots;
using CLARIHR.Domain.PositionSlots;

namespace CLARIHR.Application.Abstractions.PositionSlots;

public interface IPositionSlotRepository
{
    void Add(PositionSlot slot);

    /// <summary>
    /// H-15 — removes the slot. Only ever called after <see cref="GetUsageAsync"/> clears it: nothing has a
    /// foreign key to <c>position_slots</c> except its own self-references, so the database would NOT stop a
    /// delete that orphans employment history.
    /// </summary>
    void Remove(PositionSlot slot);

    /// <summary>
    /// H-15 — everything that would be broken by deleting this slot, in one round trip. Deliberately NO default
    /// implementation (unlike <see cref="GetSalaryRangeAsync"/> above): a default would make the guard fail
    /// OPEN in any repository that forgets it, and this one stands between a `DELETE` and silently orphaned
    /// employment history.
    /// </summary>
    /// <remarks>
    /// Takes both ids because the two dependency columns are shadow FKs holding the INTERNAL id (there are
    /// no navigation properties), while the four unprotected references hold the PUBLIC id.
    /// </remarks>
    Task<PositionSlotUsage> GetUsageAsync(Guid slotPublicId, long slotInternalId, CancellationToken cancellationToken);

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
        bool? isActive,
        Guid? directDependencyPositionSlotId,
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
        bool? isActive,
        int? maxRows,
        CancellationToken cancellationToken);

    Task<PositionSlotJobProfileLookup?> GetJobProfileLookupAsync(
        Guid tenantId,
        Guid jobProfileId,
        CancellationToken cancellationToken);
}

/// <summary>
/// H-15 — the four tables that reference a slot by <c>public_id</c> WITHOUT a foreign key, plus the slots that
/// depend on it (those two are real FKs, and they are <c>RESTRICT</c>, so an unguarded delete surfaced as a raw
/// Postgres 500).
/// <para>
/// <see cref="ActiveAssignments"/> is separate from <see cref="Assignments"/> on purpose: deleting is blocked by
/// ANY assignment — a historical row orphans just as badly — while suspending is only blocked by the ones still
/// in force.
/// </para>
/// <para>
/// None of these counts read <c>PositionSlot.OccupiedEmployees</c>. That counter is only written by the slot's
/// own <c>/occupancy</c> endpoint — creating an assignment does not touch it — so a guard trusting it would miss
/// exactly the case it exists for.
/// </para>
/// </summary>
public sealed record PositionSlotUsage(
    int Assignments,
    int ActiveAssignments,
    int ContractHistories,
    int AuthorizationSubstitutions,
    int ExitInterviewSubmissions,
    int DependentSlots)
{
    public bool BlocksDeletion =>
        Assignments > 0 ||
        ContractHistories > 0 ||
        AuthorizationSubstitutions > 0 ||
        ExitInterviewSubmissions > 0 ||
        DependentSlots > 0;
}

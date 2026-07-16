using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Payroll;
using CLARIHR.Domain.Payroll;

namespace CLARIHR.Application.Abstractions.Payroll;

public interface IWorkScheduleRepository
{
    void Add(WorkSchedule schedule);

    /// <summary>Loads the aggregate WITH its day collection (the parent replaces the full set on edit).</summary>
    Task<WorkSchedule?> GetByIdAsync(Guid workScheduleId, CancellationToken cancellationToken);

    Task<bool> ExistsOutsideTenantAsync(Guid workScheduleId, CancellationToken cancellationToken);

    /// <summary>
    /// Duplicate probe on the filtered (TenantId, NormalizedCode) WHERE is_active unique key. Only ACTIVE
    /// schedules conflict; pass the public id of the schedule being edited in
    /// <paramref name="excludingWorkScheduleId"/> to exclude itself.
    /// </summary>
    Task<bool> CodeExistsAsync(
        Guid tenantId,
        string normalizedCode,
        Guid? excludingWorkScheduleId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether an ACTIVE employment assignment references the schedule by its code (the plaza's
    /// <c>WorkdayCode</c> IS the link — no FK, REQ-012 §3.3). Blocks logical inactivation with
    /// <c>WORK_SCHEDULE_IN_USE</c>.
    /// </summary>
    Task<bool> IsInUseAsync(Guid tenantId, string normalizedCode, CancellationToken cancellationToken);

    /// <summary>Whether an ACTIVE schedule with the code exists — the plaza WorkdayCode validation (D-06).</summary>
    Task<bool> ActiveCodeExistsAsync(Guid tenantId, string normalizedCode, CancellationToken cancellationToken);

    Task<PagedResponse<WorkScheduleListItemResponse>> SearchAsync(
        Guid tenantId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<WorkScheduleResponse?> GetResponseByIdAsync(Guid workScheduleId, CancellationToken cancellationToken);
}

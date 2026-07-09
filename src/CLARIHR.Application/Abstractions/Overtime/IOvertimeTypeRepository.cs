using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PersonnelFiles.Overtime;
using CLARIHR.Domain.Overtime;

namespace CLARIHR.Application.Abstractions.Overtime;

public interface IOvertimeTypeRepository
{
    void Add(OvertimeType type);

    Task<OvertimeType?> GetByIdAsync(Guid overtimeTypeId, CancellationToken cancellationToken);

    Task<bool> ExistsOutsideTenantAsync(Guid overtimeTypeId, CancellationToken cancellationToken);

    /// <summary>
    /// Duplicate probe on the filtered (TenantId, NormalizedCode) WHERE is_active unique key. Only ACTIVE
    /// types conflict; pass the public id of the type being edited in
    /// <paramref name="excludingOvertimeTypeId"/> to exclude itself.
    /// </summary>
    Task<bool> CodeExistsAsync(
        Guid tenantId,
        string normalizedCode,
        Guid? excludingOvertimeTypeId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether the type is referenced by an active overtime record (logical inactivation is blocked with
    /// <c>OVERTIME_TYPE_IN_USE</c>). PR-1 has no overtime record table yet (M2/PR-2), so this always returns
    /// <c>false</c> today; the real query is wired in PR-3 once that table exists.
    /// </summary>
    Task<bool> IsInUseAsync(Guid tenantId, Guid overtimeTypeId, CancellationToken cancellationToken);

    Task<PagedResponse<OvertimeTypeListItemResponse>> SearchAsync(
        Guid tenantId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<OvertimeTypeResponse?> GetResponseByIdAsync(Guid overtimeTypeId, CancellationToken cancellationToken);
}

using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.EmployeeRelations;
using CLARIHR.Domain.EmployeeRelations;

namespace CLARIHR.Application.Abstractions.EmployeeRelations;

public interface IDisciplinaryActionTypeRepository
{
    void Add(DisciplinaryActionType type);

    Task<DisciplinaryActionType?> GetByIdAsync(Guid disciplinaryActionTypeId, CancellationToken cancellationToken);

    Task<bool> ExistsOutsideTenantAsync(Guid disciplinaryActionTypeId, CancellationToken cancellationToken);

    /// <summary>
    /// Duplicate probe on the filtered (TenantId, NormalizedCode) WHERE is_active unique key. Only
    /// ACTIVE types conflict; pass the public id of the type being edited in
    /// <paramref name="excludingDisciplinaryActionTypeId"/> to exclude itself.
    /// </summary>
    Task<bool> CodeExistsAsync(
        Guid tenantId,
        string normalizedCode,
        Guid? excludingDisciplinaryActionTypeId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether the type is referenced by an active disciplinary-action record (logical inactivation is
    /// blocked with <c>DISCIPLINARY_ACTION_TYPE_IN_USE</c>). PR-1 has no disciplinary-action table yet
    /// (M2/PR-2), so this always returns <c>false</c> today; the real query is wired in PR-4.
    /// </summary>
    Task<bool> IsInUseAsync(Guid tenantId, Guid disciplinaryActionTypeId, CancellationToken cancellationToken);

    Task<PagedResponse<DisciplinaryActionTypeListItemResponse>> SearchAsync(
        Guid tenantId,
        bool? isActive,
        bool? appliesSuspension,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<DisciplinaryActionTypeResponse?> GetResponseByIdAsync(Guid disciplinaryActionTypeId, CancellationToken cancellationToken);
}

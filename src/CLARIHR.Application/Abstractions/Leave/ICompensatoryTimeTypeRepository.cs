using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Leave;
using CLARIHR.Domain.Leave;

namespace CLARIHR.Application.Abstractions.Leave;

public interface ICompensatoryTimeTypeRepository
{
    void Add(CompensatoryTimeType type);

    Task<CompensatoryTimeType?> GetByIdAsync(Guid compensatoryTimeTypeId, CancellationToken cancellationToken);

    Task<bool> ExistsOutsideTenantAsync(Guid compensatoryTimeTypeId, CancellationToken cancellationToken);

    /// <summary>
    /// Duplicate probe on the filtered (TenantId, NormalizedCode) WHERE is_active unique key. Only
    /// ACTIVE types conflict; pass the public id of the type being edited in
    /// <paramref name="excludingCompensatoryTimeTypeId"/> to exclude itself.
    /// </summary>
    Task<bool> CodeExistsAsync(
        Guid tenantId,
        string normalizedCode,
        Guid? excludingCompensatoryTimeTypeId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether the type is referenced by an active compensatory-time credit or absence (RN — logical
    /// inactivation is blocked with <c>COMPENSATORY_TIME_TYPE_IN_USE</c>). PR-1 has no credit/absence
    /// tables yet (M2/PR-2), so this always returns <c>false</c> today; the real query is wired in
    /// PR-3/PR-4 when those tables exist.
    /// </summary>
    Task<bool> IsInUseAsync(Guid tenantId, Guid compensatoryTimeTypeId, CancellationToken cancellationToken);

    Task<PagedResponse<CompensatoryTimeTypeListItemResponse>> SearchAsync(
        Guid tenantId,
        bool? isActive,
        string? operationCode,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<CompensatoryTimeTypeResponse?> GetResponseByIdAsync(Guid compensatoryTimeTypeId, CancellationToken cancellationToken);
}

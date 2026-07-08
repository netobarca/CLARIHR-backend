using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.EmployeeRelations;
using CLARIHR.Domain.EmployeeRelations;

namespace CLARIHR.Application.Abstractions.EmployeeRelations;

public interface IRecognitionTypeRepository
{
    void Add(RecognitionType type);

    Task<RecognitionType?> GetByIdAsync(Guid recognitionTypeId, CancellationToken cancellationToken);

    Task<bool> ExistsOutsideTenantAsync(Guid recognitionTypeId, CancellationToken cancellationToken);

    /// <summary>
    /// Duplicate probe on the filtered (TenantId, NormalizedCode) WHERE is_active unique key. Only
    /// ACTIVE types conflict; pass the public id of the type being edited in
    /// <paramref name="excludingRecognitionTypeId"/> to exclude itself.
    /// </summary>
    Task<bool> CodeExistsAsync(
        Guid tenantId,
        string normalizedCode,
        Guid? excludingRecognitionTypeId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Whether the type is referenced by an active recognition record (logical inactivation is blocked
    /// with <c>RECOGNITION_TYPE_IN_USE</c>). PR-1 has no recognition table yet (M2/PR-2), so this always
    /// returns <c>false</c> today; the real query is wired in PR-3 when that table exists.
    /// </summary>
    Task<bool> IsInUseAsync(Guid tenantId, Guid recognitionTypeId, CancellationToken cancellationToken);

    Task<PagedResponse<RecognitionTypeListItemResponse>> SearchAsync(
        Guid tenantId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<RecognitionTypeResponse?> GetResponseByIdAsync(Guid recognitionTypeId, CancellationToken cancellationToken);
}

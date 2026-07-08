using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Leave;
using CLARIHR.Domain.Leave;

namespace CLARIHR.Application.Abstractions.Leave;

public interface IIncapacityTypeRepository
{
    void Add(IncapacityType incapacityType);

    Task<IncapacityType?> GetByIdAsync(Guid incapacityTypeId, CancellationToken cancellationToken);

    Task<bool> ExistsOutsideTenantAsync(Guid incapacityTypeId, CancellationToken cancellationToken);

    /// <summary>
    /// Duplicate probe on the (TenantId, NormalizedCode) unique key. Pass the public id of the
    /// type being edited in <paramref name="excludingIncapacityTypeId"/> to exclude itself.
    /// </summary>
    Task<bool> CodeExistsAsync(
        Guid tenantId,
        string normalizedCode,
        Guid? excludingIncapacityTypeId,
        CancellationToken cancellationToken);

    Task<PagedResponse<IncapacityTypeListItemResponse>> SearchAsync(
        Guid tenantId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<IncapacityTypeResponse?> GetResponseByIdAsync(Guid incapacityTypeId, CancellationToken cancellationToken);
}

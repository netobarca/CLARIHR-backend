using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Leave;
using CLARIHR.Domain.Leave;

namespace CLARIHR.Application.Abstractions.Leave;

public interface IIncapacityRiskRepository
{
    void Add(IncapacityRisk incapacityRisk);

    /// <summary>
    /// Loads the aggregate WITH its subsidy parameters (the domain guards in
    /// <c>Update</c>/<c>ReplaceParameters</c> reason over the loaded child set).
    /// </summary>
    Task<IncapacityRisk?> GetByIdAsync(Guid incapacityRiskId, CancellationToken cancellationToken);

    Task<bool> ExistsOutsideTenantAsync(Guid incapacityRiskId, CancellationToken cancellationToken);

    /// <summary>
    /// Duplicate probe on the (TenantId, NormalizedCode) unique key. Pass the public id of the
    /// risk being edited in <paramref name="excludingIncapacityRiskId"/> to exclude itself.
    /// </summary>
    Task<bool> CodeExistsAsync(
        Guid tenantId,
        string normalizedCode,
        Guid? excludingIncapacityRiskId,
        CancellationToken cancellationToken);

    Task<PagedResponse<IncapacityRiskListItemResponse>> SearchAsync(
        Guid tenantId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<IncapacityRiskResponse?> GetResponseByIdAsync(Guid incapacityRiskId, CancellationToken cancellationToken);
}

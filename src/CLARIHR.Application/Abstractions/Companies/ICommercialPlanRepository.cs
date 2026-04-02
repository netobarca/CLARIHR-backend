using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.CommercialPlans;
using CLARIHR.Domain.Companies;

namespace CLARIHR.Application.Abstractions.Companies;

public interface ICommercialPlanRepository
{
    void Add(CommercialPlan plan);

    Task<CommercialPlan?> GetByIdAsync(Guid commercialPlanId, CancellationToken cancellationToken);

    Task<CommercialPlanVersion?> GetEffectiveVersionAsync(
        Guid commercialPlanId,
        DateTime effectiveAtUtc,
        CancellationToken cancellationToken);

    Task<CommercialPlan?> GetByNormalizedCodeAsync(string normalizedCode, CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(string normalizedCode, long? excludingId, CancellationToken cancellationToken);

    Task<PagedResponse<CommercialPlanSummaryResponse>> SearchAsync(
        CommercialPlanStatus? status,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);
}

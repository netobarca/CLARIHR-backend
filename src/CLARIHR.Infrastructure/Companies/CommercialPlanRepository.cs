using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.CommercialPlans;
using CLARIHR.Domain.Companies;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Companies;

internal sealed class CommercialPlanRepository(ApplicationDbContext dbContext) : ICommercialPlanRepository
{
    public void Add(CommercialPlan plan) => dbContext.CommercialPlans.Add(plan);

    public Task<CommercialPlan?> GetByIdAsync(Guid commercialPlanId, CancellationToken cancellationToken) =>
        dbContext.CommercialPlans
            .Include(plan => plan.Limits)
            .SingleOrDefaultAsync(plan => plan.PublicId == commercialPlanId, cancellationToken);

    public Task<bool> CodeExistsAsync(string normalizedCode, long? excludingId, CancellationToken cancellationToken) =>
        dbContext.CommercialPlans.AnyAsync(
            plan => plan.NormalizedCode == normalizedCode &&
                    (!excludingId.HasValue || plan.Id != excludingId.Value),
            cancellationToken);

    public async Task<PagedResponse<CommercialPlanSummaryResponse>> SearchAsync(
        CommercialPlanStatus? status,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.CommercialPlans.AsNoTracking();

        if (status.HasValue)
        {
            query = query.Where(plan => plan.Status == status.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(plan =>
                plan.NormalizedCode.Contains(normalizedSearch) ||
                plan.NormalizedName.Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(plan => plan.Name)
            .ThenBy(plan => plan.Code)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(plan => new CommercialPlanSummaryResponse(
                plan.PublicId,
                plan.Code,
                plan.Name,
                plan.Description,
                plan.BaseMonthlyFee,
                plan.PricePerActiveEmployee,
                plan.Status,
                plan.IsSystemPlan,
                plan.CreatedUtc,
                plan.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<CommercialPlanSummaryResponse>(items, pageNumber, pageSize, totalCount);
    }
}

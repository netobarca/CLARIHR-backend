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

    public async Task<IReadOnlyCollection<CommercialPlan>> ListActiveAsync(CancellationToken cancellationToken) =>
        await dbContext.CommercialPlans
            .AsNoTracking()
            .AsSplitQuery()
            .Include(plan => plan.Entitlements)
            .Include(plan => plan.Limits)
            .Include(plan => plan.Versions)
            .Where(plan => plan.Status == CommercialPlanStatus.Active)
            .OrderBy(plan => plan.Name)
            .ThenBy(plan => plan.Code)
            .ToListAsync(cancellationToken);

    public Task<CommercialPlan?> GetByInternalIdAsync(long commercialPlanId, CancellationToken cancellationToken) =>
        dbContext.CommercialPlans
            .AsSplitQuery()
            .Include(plan => plan.Entitlements)
            .Include(plan => plan.Limits)
            .Include(plan => plan.Versions)
            .SingleOrDefaultAsync(plan => plan.Id == commercialPlanId, cancellationToken);

    public Task<CommercialPlan?> GetByIdAsync(Guid commercialPlanId, CancellationToken cancellationToken) =>
        dbContext.CommercialPlans
            .AsSplitQuery()
            .Include(plan => plan.Entitlements)
            .Include(plan => plan.Limits)
            .Include(plan => plan.Versions)
            .SingleOrDefaultAsync(plan => plan.PublicId == commercialPlanId, cancellationToken);

    public Task<CommercialPlanVersion?> GetEffectiveVersionAsync(
        Guid commercialPlanId,
        DateTime effectiveAtUtc,
        CancellationToken cancellationToken) =>
        dbContext.CommercialPlanVersions
            .AsNoTracking()
            .Join(
                dbContext.CommercialPlans.AsNoTracking().Where(plan => plan.PublicId == commercialPlanId),
                version => version.CommercialPlanId,
                plan => plan.Id,
                (version, _) => version)
            .Where(version => version.EffectiveFromUtc <= effectiveAtUtc &&
                              (!version.EffectiveToUtc.HasValue || effectiveAtUtc < version.EffectiveToUtc.Value))
            .OrderByDescending(version => version.VersionNumber)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<CommercialPlan?> GetByNormalizedCodeAsync(string normalizedCode, CancellationToken cancellationToken) =>
        dbContext.CommercialPlans
            .AsSplitQuery()
            .Include(plan => plan.Entitlements)
            .Include(plan => plan.Limits)
            .Include(plan => plan.Versions)
            .SingleOrDefaultAsync(plan => plan.NormalizedCode == normalizedCode, cancellationToken);

    public Task<bool> IsSystemPlanAsync(long commercialPlanId, CancellationToken cancellationToken) =>
        dbContext.CommercialPlans
            .AsNoTracking()
            .Where(plan => plan.Id == commercialPlanId)
            .Select(plan => plan.IsSystemPlan)
            .SingleAsync(cancellationToken);

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
                plan.Versions
                    .OrderByDescending(version => version.VersionNumber)
                    .Select(version => version.VersionNumber)
                    .FirstOrDefault(),
                plan.Versions
                    .OrderByDescending(version => version.VersionNumber)
                    .Select(version => version.CurrencyCode)
                    .FirstOrDefault() ?? "USD",
                plan.Status,
                plan.IsSystemPlan,
                plan.Entitlements.Count(entitlement => entitlement.IsEnabled),
                plan.CreatedUtc,
                plan.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<CommercialPlanSummaryResponse>(items, pageNumber, pageSize, totalCount);
    }
}

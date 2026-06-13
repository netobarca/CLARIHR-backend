using CLARIHR.Application.Abstractions.CostCenters;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.CostCenters.Types;
using CLARIHR.Domain.CostCenters;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.CostCenters;

internal sealed class CostCenterTypeRepository(ApplicationDbContext dbContext) : ICostCenterTypeRepository
{
    public void Add(CostCenterType costCenterType) => dbContext.CostCenterTypes.Add(costCenterType);

    public Task<CostCenterType?> GetByIdAsync(Guid costCenterTypeId, CancellationToken cancellationToken) =>
        dbContext.CostCenterTypes.SingleOrDefaultAsync(type => type.PublicId == costCenterTypeId, cancellationToken);

    public Task<bool> ExistsOutsideTenantAsync(Guid costCenterTypeId, CancellationToken cancellationToken) =>
        dbContext.CostCenterTypes
            // Intentional tenant filter bypass: checks cross-tenant existence only for tenant-mismatch errors.
            .IgnoreQueryFilters()
            .AnyAsync(type => type.PublicId == costCenterTypeId, cancellationToken);

    public Task<bool> CodeExistsAsync(Guid tenantId, string normalizedCode, long? excludingCostCenterTypeId, CancellationToken cancellationToken) =>
        dbContext.CostCenterTypes.AnyAsync(
            type => type.TenantId == tenantId &&
                    type.NormalizedCode == normalizedCode &&
                    (!excludingCostCenterTypeId.HasValue || type.Id != excludingCostCenterTypeId.Value),
            cancellationToken);

    public async Task<PagedResponse<CostCenterTypeListItemResponse>> SearchAsync(
        Guid tenantId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.CostCenterTypes
            .AsNoTracking()
            .Where(type => type.TenantId == tenantId);

        if (isActive.HasValue)
        {
            query = query.Where(type => type.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(type =>
                type.NormalizedCode.Contains(normalizedSearch) ||
                type.NormalizedName.Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(type => type.Name)
            .ThenBy(type => type.Code)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(type => new CostCenterTypeListItemResponse(
                type.PublicId,
                type.Code,
                type.Name,
                type.IsActive,
                type.ConcurrencyToken,
                type.CreatedUtc,
                type.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<CostCenterTypeListItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<bool> HasActiveCostCentersAsync(long costCenterTypeId, CancellationToken cancellationToken) =>
        dbContext.CostCenters.AnyAsync(
            costCenter => costCenter.CostCenterTypeId == costCenterTypeId && costCenter.IsActive,
            cancellationToken);
}

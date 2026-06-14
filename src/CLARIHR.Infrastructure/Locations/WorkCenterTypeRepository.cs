using CLARIHR.Application.Abstractions.Locations;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Locations.WorkCenterTypes;
using CLARIHR.Domain.Locations;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Locations;

internal sealed class WorkCenterTypeRepository(ApplicationDbContext dbContext) : IWorkCenterTypeRepository
{
    public void Add(WorkCenterType workCenterType) => dbContext.WorkCenterTypes.Add(workCenterType);

    public Task<WorkCenterType?> GetByIdAsync(Guid workCenterTypeId, CancellationToken cancellationToken) =>
        dbContext.WorkCenterTypes.SingleOrDefaultAsync(type => type.PublicId == workCenterTypeId, cancellationToken);

    public Task<bool> ExistsOutsideTenantAsync(Guid workCenterTypeId, CancellationToken cancellationToken) =>
        dbContext.WorkCenterTypes
            // Intentional tenant filter bypass: checks cross-tenant existence only for tenant-mismatch errors.
            .IgnoreQueryFilters()
            .AnyAsync(type => type.PublicId == workCenterTypeId, cancellationToken);

    public Task<bool> CodeExistsAsync(Guid tenantId, string normalizedCode, long? excludingWorkCenterTypeId, CancellationToken cancellationToken) =>
        dbContext.WorkCenterTypes.AnyAsync(
            type => type.TenantId == tenantId &&
                    type.NormalizedCode == normalizedCode &&
                    (!excludingWorkCenterTypeId.HasValue || type.Id != excludingWorkCenterTypeId.Value),
            cancellationToken);

    public async Task<PagedResponse<WorkCenterTypeResponse>> SearchAsync(
        Guid tenantId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.WorkCenterTypes
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
            .Select(type => new WorkCenterTypeResponse(
                type.PublicId,
                type.Code,
                type.Name,
                type.Description,
                type.RequiresAddress,
                type.RequiresGeo,
                type.AllowsBiometric,
                type.IsActive,
                type.ConcurrencyToken,
                type.CreatedUtc,
                type.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<WorkCenterTypeResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<bool> HasActiveWorkCentersAsync(long workCenterTypeId, CancellationToken cancellationToken) =>
        dbContext.WorkCenters.AnyAsync(
            center => center.WorkCenterTypeId == workCenterTypeId && center.IsActive,
            cancellationToken);
}

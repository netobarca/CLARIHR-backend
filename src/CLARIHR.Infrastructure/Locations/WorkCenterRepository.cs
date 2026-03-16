using CLARIHR.Application.Abstractions.Locations;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Locations.WorkCenters;
using CLARIHR.Domain.Locations;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Locations;

internal sealed class WorkCenterRepository(ApplicationDbContext dbContext) : IWorkCenterRepository
{
    public void Add(WorkCenter workCenter) => dbContext.WorkCenters.Add(workCenter);

    public Task<WorkCenter?> GetByIdAsync(Guid workCenterId, CancellationToken cancellationToken) =>
        dbContext.WorkCenters.SingleOrDefaultAsync(center => center.PublicId == workCenterId, cancellationToken);

    public Task<bool> ExistsOutsideTenantAsync(Guid workCenterId, CancellationToken cancellationToken) =>
        dbContext.WorkCenters
            .IgnoreQueryFilters()
            .AnyAsync(center => center.PublicId == workCenterId, cancellationToken);

    public Task<bool> CodeExistsAsync(Guid tenantId, string normalizedCode, long? excludingWorkCenterId, CancellationToken cancellationToken) =>
        dbContext.WorkCenters.AnyAsync(
            center => center.TenantId == tenantId &&
                      center.NormalizedCode == normalizedCode &&
                      (!excludingWorkCenterId.HasValue || center.Id != excludingWorkCenterId.Value),
            cancellationToken);

    public async Task<PagedResponse<WorkCenterResponse>> SearchAsync(
        Guid tenantId,
        Guid? groupId,
        Guid? typeId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query =
            from center in dbContext.WorkCenters.AsNoTracking()
            join type in dbContext.WorkCenterTypes.AsNoTracking() on center.WorkCenterTypeId equals type.Id
            join locationGroup in dbContext.LocationGroups.AsNoTracking() on center.LocationGroupId equals locationGroup.Id
            where center.TenantId == tenantId
            select new
            {
                Center = center,
                Type = type,
                Group = locationGroup
            };

        if (groupId.HasValue)
        {
            query = query.Where(item => item.Group.PublicId == groupId.Value);
        }

        if (typeId.HasValue)
        {
            query = query.Where(item => item.Type.PublicId == typeId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(item => item.Center.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(item =>
                item.Center.NormalizedCode.Contains(normalizedSearch) ||
                item.Center.NormalizedName.Contains(normalizedSearch) ||
                item.Group.NormalizedName.Contains(normalizedSearch) ||
                item.Type.NormalizedName.Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.Center.Name)
            .ThenBy(item => item.Center.Code)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new WorkCenterResponse(
                item.Center.PublicId,
                item.Center.Code,
                item.Center.Name,
                item.Type.PublicId,
                item.Type.Code,
                item.Type.Name,
                item.Group.PublicId,
                item.Group.Code,
                item.Group.Name,
                item.Group.LevelOrder,
                item.Center.Address,
                item.Center.GeoLat,
                item.Center.GeoLong,
                item.Center.Phone,
                item.Center.Email,
                item.Center.Notes,
                item.Center.IsActive,
                item.Center.ConcurrencyToken,
                item.Center.CreatedUtc,
                item.Center.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<WorkCenterResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<WorkCenterResponse?> GetResponseByIdAsync(Guid workCenterId, CancellationToken cancellationToken) =>
        (from center in dbContext.WorkCenters.AsNoTracking()
         join type in dbContext.WorkCenterTypes.AsNoTracking() on center.WorkCenterTypeId equals type.Id
         join locationGroup in dbContext.LocationGroups.AsNoTracking() on center.LocationGroupId equals locationGroup.Id
         where center.PublicId == workCenterId
         select new WorkCenterResponse(
             center.PublicId,
             center.Code,
             center.Name,
             type.PublicId,
             type.Code,
             type.Name,
             locationGroup.PublicId,
             locationGroup.Code,
             locationGroup.Name,
             locationGroup.LevelOrder,
             center.Address,
             center.GeoLat,
             center.GeoLong,
             center.Phone,
             center.Email,
             center.Notes,
             center.IsActive,
             center.ConcurrencyToken,
             center.CreatedUtc,
             center.ModifiedUtc))
        .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> HasActiveWorkCentersInGroupAsync(long locationGroupId, long? excludingWorkCenterId, CancellationToken cancellationToken) =>
        dbContext.WorkCenters.AnyAsync(
            center => center.LocationGroupId == locationGroupId &&
                      center.IsActive &&
                      (!excludingWorkCenterId.HasValue || center.Id != excludingWorkCenterId.Value),
            cancellationToken);

    public Task<bool> HasActiveWorkCentersForTypeAsync(long workCenterTypeId, long? excludingWorkCenterId, CancellationToken cancellationToken) =>
        dbContext.WorkCenters.AnyAsync(
            center => center.WorkCenterTypeId == workCenterTypeId &&
                      center.IsActive &&
                      (!excludingWorkCenterId.HasValue || center.Id != excludingWorkCenterId.Value),
            cancellationToken);
}

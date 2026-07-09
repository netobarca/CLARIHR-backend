using CLARIHR.Application.Abstractions.Overtime;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PersonnelFiles.Overtime;
using CLARIHR.Domain.Overtime;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Overtime;

internal sealed class OvertimeTypeRepository(ApplicationDbContext dbContext) : IOvertimeTypeRepository
{
    public void Add(OvertimeType type) => dbContext.Set<OvertimeType>().Add(type);

    public Task<OvertimeType?> GetByIdAsync(Guid overtimeTypeId, CancellationToken cancellationToken) =>
        dbContext.Set<OvertimeType>().SingleOrDefaultAsync(type => type.PublicId == overtimeTypeId, cancellationToken);

    public Task<bool> ExistsOutsideTenantAsync(Guid overtimeTypeId, CancellationToken cancellationToken) =>
        dbContext.Set<OvertimeType>()
            // Intentional tenant filter bypass: checks cross-tenant existence only for tenant-mismatch errors.
            .IgnoreQueryFilters()
            .AnyAsync(type => type.PublicId == overtimeTypeId, cancellationToken);

    public Task<bool> CodeExistsAsync(
        Guid tenantId,
        string normalizedCode,
        Guid? excludingOvertimeTypeId,
        CancellationToken cancellationToken) =>
        dbContext.Set<OvertimeType>().AnyAsync(
            type => type.TenantId == tenantId &&
                    type.IsActive &&
                    type.NormalizedCode == normalizedCode &&
                    (!excludingOvertimeTypeId.HasValue || type.PublicId != excludingOvertimeTypeId.Value),
            cancellationToken);

    // PR-1 has no overtime record table yet (M2/PR-2), so nothing can reference a type — the real reference
    // probe is wired in PR-3 once that table exists.
    public Task<bool> IsInUseAsync(Guid tenantId, Guid overtimeTypeId, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public async Task<PagedResponse<OvertimeTypeListItemResponse>> SearchAsync(
        Guid tenantId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<OvertimeType>()
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
            .OrderBy(type => type.SortOrder)
            .ThenBy(type => type.Name)
            .ThenBy(type => type.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(type => new OvertimeTypeListItemResponse(
                type.PublicId,
                type.Code,
                type.Name,
                type.DefaultFactor,
                type.PayrollEffectDescription,
                type.SortOrder,
                type.IsActive,
                type.ConcurrencyToken,
                type.CreatedUtc,
                type.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<OvertimeTypeListItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<OvertimeTypeResponse?> GetResponseByIdAsync(Guid overtimeTypeId, CancellationToken cancellationToken) =>
        dbContext.Set<OvertimeType>()
            .AsNoTracking()
            .Where(type => type.PublicId == overtimeTypeId)
            .Select(type => new OvertimeTypeResponse(
                type.PublicId,
                type.Code,
                type.Name,
                type.DefaultFactor,
                type.PayrollEffectDescription,
                type.SortOrder,
                type.IsActive,
                type.ConcurrencyToken,
                type.CreatedUtc,
                type.ModifiedUtc,
                null))
            .SingleOrDefaultAsync(cancellationToken);
}

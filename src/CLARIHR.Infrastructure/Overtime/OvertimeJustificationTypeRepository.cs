using CLARIHR.Application.Abstractions.Overtime;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PersonnelFiles.Overtime;
using CLARIHR.Domain.Overtime;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Overtime;

internal sealed class OvertimeJustificationTypeRepository(ApplicationDbContext dbContext) : IOvertimeJustificationTypeRepository
{
    public void Add(OvertimeJustificationType type) => dbContext.Set<OvertimeJustificationType>().Add(type);

    public Task<OvertimeJustificationType?> GetByIdAsync(Guid justificationTypeId, CancellationToken cancellationToken) =>
        dbContext.Set<OvertimeJustificationType>().SingleOrDefaultAsync(type => type.PublicId == justificationTypeId, cancellationToken);

    public Task<bool> ExistsOutsideTenantAsync(Guid justificationTypeId, CancellationToken cancellationToken) =>
        dbContext.Set<OvertimeJustificationType>()
            // Intentional tenant filter bypass: checks cross-tenant existence only for tenant-mismatch errors.
            .IgnoreQueryFilters()
            .AnyAsync(type => type.PublicId == justificationTypeId, cancellationToken);

    public Task<bool> CodeExistsAsync(
        Guid tenantId,
        string normalizedCode,
        Guid? excludingJustificationTypeId,
        CancellationToken cancellationToken) =>
        dbContext.Set<OvertimeJustificationType>().AnyAsync(
            type => type.TenantId == tenantId &&
                    type.IsActive &&
                    type.NormalizedCode == normalizedCode &&
                    (!excludingJustificationTypeId.HasValue || type.PublicId != excludingJustificationTypeId.Value),
            cancellationToken);

    // PR-1 has no overtime record table yet (M2/PR-2), so nothing can reference a type — the real reference
    // probe is wired in PR-3 once that table exists.
    public Task<bool> IsInUseAsync(Guid tenantId, Guid justificationTypeId, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public async Task<PagedResponse<OvertimeJustificationTypeListItemResponse>> SearchAsync(
        Guid tenantId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<OvertimeJustificationType>()
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
            .Select(type => new OvertimeJustificationTypeListItemResponse(
                type.PublicId,
                type.Code,
                type.Name,
                type.Description,
                type.SortOrder,
                type.IsActive,
                type.ConcurrencyToken,
                type.CreatedUtc,
                type.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<OvertimeJustificationTypeListItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<OvertimeJustificationTypeResponse?> GetResponseByIdAsync(Guid justificationTypeId, CancellationToken cancellationToken) =>
        dbContext.Set<OvertimeJustificationType>()
            .AsNoTracking()
            .Where(type => type.PublicId == justificationTypeId)
            .Select(type => new OvertimeJustificationTypeResponse(
                type.PublicId,
                type.Code,
                type.Name,
                type.Description,
                type.SortOrder,
                type.IsActive,
                type.ConcurrencyToken,
                type.CreatedUtc,
                type.ModifiedUtc,
                null))
            .SingleOrDefaultAsync(cancellationToken);
}

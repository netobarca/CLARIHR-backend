using CLARIHR.Application.Abstractions.Leave;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Leave;
using CLARIHR.Domain.Leave;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Leave;

internal sealed class IncapacityTypeRepository(ApplicationDbContext dbContext) : IIncapacityTypeRepository
{
    public void Add(IncapacityType incapacityType) => dbContext.IncapacityTypes.Add(incapacityType);

    public Task<IncapacityType?> GetByIdAsync(Guid incapacityTypeId, CancellationToken cancellationToken) =>
        dbContext.IncapacityTypes.SingleOrDefaultAsync(type => type.PublicId == incapacityTypeId, cancellationToken);

    public Task<bool> ExistsOutsideTenantAsync(Guid incapacityTypeId, CancellationToken cancellationToken) =>
        dbContext.IncapacityTypes
            // Intentional tenant filter bypass: checks cross-tenant existence only for tenant-mismatch errors.
            .IgnoreQueryFilters()
            .AnyAsync(type => type.PublicId == incapacityTypeId, cancellationToken);

    public Task<bool> CodeExistsAsync(
        Guid tenantId,
        string normalizedCode,
        Guid? excludingIncapacityTypeId,
        CancellationToken cancellationToken) =>
        dbContext.IncapacityTypes.AnyAsync(
            type => type.TenantId == tenantId &&
                    type.NormalizedCode == normalizedCode &&
                    (!excludingIncapacityTypeId.HasValue || type.PublicId != excludingIncapacityTypeId.Value),
            cancellationToken);

    public async Task<PagedResponse<IncapacityTypeListItemResponse>> SearchAsync(
        Guid tenantId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.IncapacityTypes
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
            .Select(type => new IncapacityTypeListItemResponse(
                type.PublicId,
                type.Code,
                type.Name,
                type.DeductionTypeText,
                type.IncomeTypeText,
                type.AppliesToWorkAccident,
                type.IsActive,
                type.ConcurrencyToken,
                type.CreatedUtc,
                type.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<IncapacityTypeListItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<IncapacityTypeResponse?> GetResponseByIdAsync(Guid incapacityTypeId, CancellationToken cancellationToken) =>
        dbContext.IncapacityTypes
            .AsNoTracking()
            .Where(type => type.PublicId == incapacityTypeId)
            .Select(type => new IncapacityTypeResponse(
                type.PublicId,
                type.Code,
                type.Name,
                type.DeductionTypeText,
                type.IncomeTypeText,
                type.AppliesToWorkAccident,
                type.IsActive,
                type.ConcurrencyToken,
                type.CreatedUtc,
                type.ModifiedUtc,
                null))
            .SingleOrDefaultAsync(cancellationToken);
}

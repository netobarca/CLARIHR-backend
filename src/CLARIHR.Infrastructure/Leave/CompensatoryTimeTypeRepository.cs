using CLARIHR.Application.Abstractions.Leave;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Leave;
using CLARIHR.Domain.Leave;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Leave;

internal sealed class CompensatoryTimeTypeRepository(ApplicationDbContext dbContext) : ICompensatoryTimeTypeRepository
{
    public void Add(CompensatoryTimeType type) => dbContext.CompensatoryTimeTypes.Add(type);

    public Task<CompensatoryTimeType?> GetByIdAsync(Guid compensatoryTimeTypeId, CancellationToken cancellationToken) =>
        dbContext.CompensatoryTimeTypes.SingleOrDefaultAsync(type => type.PublicId == compensatoryTimeTypeId, cancellationToken);

    public Task<bool> ExistsOutsideTenantAsync(Guid compensatoryTimeTypeId, CancellationToken cancellationToken) =>
        dbContext.CompensatoryTimeTypes
            // Intentional tenant filter bypass: checks cross-tenant existence only for tenant-mismatch errors.
            .IgnoreQueryFilters()
            .AnyAsync(type => type.PublicId == compensatoryTimeTypeId, cancellationToken);

    public Task<bool> CodeExistsAsync(
        Guid tenantId,
        string normalizedCode,
        Guid? excludingCompensatoryTimeTypeId,
        CancellationToken cancellationToken) =>
        dbContext.CompensatoryTimeTypes.AnyAsync(
            type => type.TenantId == tenantId &&
                    type.IsActive &&
                    type.NormalizedCode == normalizedCode &&
                    (!excludingCompensatoryTimeTypeId.HasValue || type.PublicId != excludingCompensatoryTimeTypeId.Value),
            cancellationToken);

    // PR-1: the credit/absence tables do not exist yet (M2/PR-2). The usage guard is wired but always
    // returns false until PR-3/PR-4 join against those tables.
    public Task<bool> IsInUseAsync(Guid tenantId, Guid compensatoryTimeTypeId, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public async Task<PagedResponse<CompensatoryTimeTypeListItemResponse>> SearchAsync(
        Guid tenantId,
        bool? isActive,
        string? operationCode,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.CompensatoryTimeTypes
            .AsNoTracking()
            .Where(type => type.TenantId == tenantId);

        if (isActive.HasValue)
        {
            query = query.Where(type => type.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(operationCode))
        {
            var normalizedOperationCode = operationCode.Trim().ToUpperInvariant();
            query = query.Where(type => type.OperationCode == normalizedOperationCode);
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
            .Select(type => new CompensatoryTimeTypeListItemResponse(
                type.PublicId,
                type.Code,
                type.Name,
                type.OperationCode,
                type.CreditFactor,
                type.SortOrder,
                type.IsActive,
                type.ConcurrencyToken,
                type.CreatedUtc,
                type.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<CompensatoryTimeTypeListItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<CompensatoryTimeTypeResponse?> GetResponseByIdAsync(Guid compensatoryTimeTypeId, CancellationToken cancellationToken) =>
        dbContext.CompensatoryTimeTypes
            .AsNoTracking()
            .Where(type => type.PublicId == compensatoryTimeTypeId)
            .Select(type => new CompensatoryTimeTypeResponse(
                type.PublicId,
                type.Code,
                type.Name,
                type.OperationCode,
                type.CreditFactor,
                type.SortOrder,
                type.IsActive,
                type.ConcurrencyToken,
                type.CreatedUtc,
                type.ModifiedUtc,
                null))
            .SingleOrDefaultAsync(cancellationToken);
}

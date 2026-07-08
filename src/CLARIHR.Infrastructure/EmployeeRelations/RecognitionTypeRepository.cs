using CLARIHR.Application.Abstractions.EmployeeRelations;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.EmployeeRelations;
using CLARIHR.Domain.EmployeeRelations;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.EmployeeRelations;

internal sealed class RecognitionTypeRepository(ApplicationDbContext dbContext) : IRecognitionTypeRepository
{
    public void Add(RecognitionType type) => dbContext.Set<RecognitionType>().Add(type);

    public Task<RecognitionType?> GetByIdAsync(Guid recognitionTypeId, CancellationToken cancellationToken) =>
        dbContext.Set<RecognitionType>().SingleOrDefaultAsync(type => type.PublicId == recognitionTypeId, cancellationToken);

    public Task<bool> ExistsOutsideTenantAsync(Guid recognitionTypeId, CancellationToken cancellationToken) =>
        dbContext.Set<RecognitionType>()
            // Intentional tenant filter bypass: checks cross-tenant existence only for tenant-mismatch errors.
            .IgnoreQueryFilters()
            .AnyAsync(type => type.PublicId == recognitionTypeId, cancellationToken);

    public Task<bool> CodeExistsAsync(
        Guid tenantId,
        string normalizedCode,
        Guid? excludingRecognitionTypeId,
        CancellationToken cancellationToken) =>
        dbContext.Set<RecognitionType>().AnyAsync(
            type => type.TenantId == tenantId &&
                    type.IsActive &&
                    type.NormalizedCode == normalizedCode &&
                    (!excludingRecognitionTypeId.HasValue || type.PublicId != excludingRecognitionTypeId.Value),
            cancellationToken);

    // PR-1 has no recognition record table yet (M2/PR-2), so nothing can reference a type — the real
    // reference probe is wired in PR-3 once that table exists.
    public Task<bool> IsInUseAsync(Guid tenantId, Guid recognitionTypeId, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public async Task<PagedResponse<RecognitionTypeListItemResponse>> SearchAsync(
        Guid tenantId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<RecognitionType>()
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
            .Select(type => new RecognitionTypeListItemResponse(
                type.PublicId,
                type.Code,
                type.Name,
                type.SortOrder,
                type.IsActive,
                type.ConcurrencyToken,
                type.CreatedUtc,
                type.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<RecognitionTypeListItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<RecognitionTypeResponse?> GetResponseByIdAsync(Guid recognitionTypeId, CancellationToken cancellationToken) =>
        dbContext.Set<RecognitionType>()
            .AsNoTracking()
            .Where(type => type.PublicId == recognitionTypeId)
            .Select(type => new RecognitionTypeResponse(
                type.PublicId,
                type.Code,
                type.Name,
                type.SortOrder,
                type.IsActive,
                type.ConcurrencyToken,
                type.CreatedUtc,
                type.ModifiedUtc,
                null))
            .SingleOrDefaultAsync(cancellationToken);
}

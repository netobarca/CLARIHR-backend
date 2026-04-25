using CLARIHR.Application.Abstractions.Banks;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Banks;
using CLARIHR.Domain.Banks;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Banks;

internal sealed class BankCatalogRepository(ApplicationDbContext dbContext) : IBankCatalogRepository
{
    public void Add(BankCatalogItem item) => dbContext.BankCatalogItems.Add(item);

    public Task<BankCatalogItem?> GetByIdAsync(Guid publicId, CancellationToken cancellationToken) =>
        dbContext.BankCatalogItems.SingleOrDefaultAsync(item => item.PublicId == publicId, cancellationToken);

    public Task<bool> ExistsByCodeAsync(
        long countryCatalogItemId,
        string normalizedCode,
        long? excludingId,
        CancellationToken cancellationToken) =>
        dbContext.BankCatalogItems
            .AsNoTracking()
            .AnyAsync(
                item => item.CountryCatalogItemId == countryCatalogItemId &&
                        item.NormalizedCode == normalizedCode &&
                        (!excludingId.HasValue || item.Id != excludingId.Value),
                cancellationToken);

    public async Task<PagedResponse<BankCatalogItemResponse>> SearchAsync(
        long countryCatalogItemId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.BankCatalogItems
            .AsNoTracking()
            .Where(item => item.CountryCatalogItemId == countryCatalogItemId);

        if (isActive.HasValue)
        {
            query = query.Where(item => item.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(item =>
                item.NormalizedCode.Contains(normalizedSearch) ||
                item.NormalizedName.Contains(normalizedSearch) ||
                (item.NormalizedAlias != null && item.NormalizedAlias.Contains(normalizedSearch)) ||
                (item.NormalizedSwiftCode != null && item.NormalizedSwiftCode.Contains(normalizedSearch)) ||
                (item.NormalizedRoutingCode != null && item.NormalizedRoutingCode.Contains(normalizedSearch)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new BankCatalogItemResponse(
                item.PublicId,
                item.CountryCode,
                item.Code,
                item.Name,
                item.Alias,
                item.SwiftCode,
                item.RoutingCode,
                item.IsActive,
                item.SortOrder,
                item.ConcurrencyToken,
                item.CreatedUtc,
                item.ModifiedUtc))
            .ToArrayAsync(cancellationToken);

        return new PagedResponse<BankCatalogItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<BankCatalogItemResponse?> GetResponseByIdAsync(Guid publicId, CancellationToken cancellationToken) =>
        dbContext.BankCatalogItems
            .AsNoTracking()
            .Where(item => item.PublicId == publicId)
            .Select(item => new BankCatalogItemResponse(
                item.PublicId,
                item.CountryCode,
                item.Code,
                item.Name,
                item.Alias,
                item.SwiftCode,
                item.RoutingCode,
                item.IsActive,
                item.SortOrder,
                item.ConcurrencyToken,
                item.CreatedUtc,
                item.ModifiedUtc))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PagedResponse<CompanyBankCatalogItemResponse>> SearchActiveByCompanyAsync(
        Guid companyId,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query =
            from bank in dbContext.BankCatalogItems.AsNoTracking()
            join company in dbContext.Companies.AsNoTracking() on bank.CountryCatalogItemId equals company.CountryCatalogItemId
            where company.PublicId == companyId && bank.IsActive
            select bank;

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(item =>
                item.NormalizedCode.Contains(normalizedSearch) ||
                item.NormalizedName.Contains(normalizedSearch) ||
                (item.NormalizedAlias != null && item.NormalizedAlias.Contains(normalizedSearch)) ||
                (item.NormalizedSwiftCode != null && item.NormalizedSwiftCode.Contains(normalizedSearch)) ||
                (item.NormalizedRoutingCode != null && item.NormalizedRoutingCode.Contains(normalizedSearch)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new CompanyBankCatalogItemResponse(
                item.PublicId,
                item.Code,
                item.Name,
                item.Alias,
                item.SwiftCode,
                item.RoutingCode,
                item.SortOrder))
            .ToArrayAsync(cancellationToken);

        return new PagedResponse<CompanyBankCatalogItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<BankCatalogLookup?> GetActiveLookupByCountryAsync(
        string countryCode,
        Guid publicId,
        CancellationToken cancellationToken)
    {
        var normalizedCountryCode = countryCode.Trim().ToUpperInvariant();

        return dbContext.BankCatalogItems
            .AsNoTracking()
            .Where(item => item.PublicId == publicId && item.IsActive && item.CountryCode == normalizedCountryCode)
            .Select(item => new BankCatalogLookup(
                item.Id,
                item.PublicId,
                item.CountryCode,
                item.Code,
                item.Name,
                item.Alias,
                item.SwiftCode,
                item.RoutingCode,
                item.IsActive))
            .SingleOrDefaultAsync(cancellationToken);
    }
}

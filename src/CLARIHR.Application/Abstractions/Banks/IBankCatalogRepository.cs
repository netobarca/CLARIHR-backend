using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Banks;
using CLARIHR.Domain.Banks;

namespace CLARIHR.Application.Abstractions.Banks;

public interface IBankCatalogRepository
{
    void Add(BankCatalogItem item);

    Task<BankCatalogItem?> GetByIdAsync(Guid publicId, CancellationToken cancellationToken);

    Task<bool> ExistsByCodeAsync(
        long countryCatalogItemId,
        string normalizedCode,
        long? excludingId,
        CancellationToken cancellationToken);

    Task<PagedResponse<BankCatalogItemResponse>> SearchAsync(
        long countryCatalogItemId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<BankCatalogItemResponse?> GetResponseByIdAsync(Guid publicId, CancellationToken cancellationToken);

    Task<PagedResponse<CompanyBankCatalogItemResponse>> SearchActiveByCompanyAsync(
        Guid companyId,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<BankCatalogLookup?> GetActiveLookupByCountryAsync(
        string countryCode,
        Guid publicId,
        CancellationToken cancellationToken);
}

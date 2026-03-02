using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.AccountCompanies;
using CLARIHR.Domain.Companies;

namespace CLARIHR.Application.Abstractions.Companies;

public interface ICompanyRepository
{
    void Add(Company company);

    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken);

    Task<Company?> FindByPublicIdAsync(Guid companyPublicId, CancellationToken cancellationToken);

    Task<AccountCompanyDetailResponse?> FindOwnedByUserAsync(
        Guid companyPublicId,
        Guid ownerUserPublicId,
        Guid? activeTenantId,
        CancellationToken cancellationToken);

    Task<PagedResponse<AccountCompanySummaryResponse>> GetOwnedByUserAsync(
        Guid ownerUserPublicId,
        CompanyListFilter filter,
        CancellationToken cancellationToken);

    Task<int> CountOwnedByUserAsync(
        Guid ownerUserPublicId,
        CompanyOwnershipCountFilter filter,
        CancellationToken cancellationToken);
}

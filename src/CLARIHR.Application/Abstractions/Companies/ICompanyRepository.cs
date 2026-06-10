using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.AccountCompanies;
using CLARIHR.Domain.Companies;

namespace CLARIHR.Application.Abstractions.Companies;

public interface ICompanyRepository
{
    void Add(Company company);

    Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken);

    Task<Company?> FindByPublicIdAsync(Guid companyPublicId, CancellationToken cancellationToken);

    /// <summary>
    /// AC-7: boolean existence probe used by GetById to distinguish 404 (unknown) from 403 (owned by
    /// another user) without materializing the whole <see cref="Company"/> aggregate. The default falls back
    /// to <see cref="FindByPublicIdAsync"/>; the EF repository overrides it with a set-based existence query.
    /// </summary>
    async Task<bool> ExistsByPublicIdAsync(Guid companyPublicId, CancellationToken cancellationToken)
        => await FindByPublicIdAsync(companyPublicId, cancellationToken) is not null;

    /// <summary>
    /// AC-3: serializes capacity-bounded mutations (create / reactivate) per owner with a transaction-scoped
    /// PostgreSQL advisory lock, closing the check-then-act TOCTOU on the active-company quota. Must run
    /// inside an open transaction (the handler opens one); the lock releases on commit/rollback. The default
    /// is a no-op (test fakes have no PostgreSQL); the EF repository takes the real advisory lock.
    /// </summary>
    Task AcquireOwnerCapacityLockAsync(Guid ownerUserPublicId, CancellationToken cancellationToken)
        => Task.CompletedTask;

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

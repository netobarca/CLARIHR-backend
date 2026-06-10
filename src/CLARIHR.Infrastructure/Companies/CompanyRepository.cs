using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.AccountCompanies;
using CLARIHR.Domain.Companies;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Companies;

internal sealed class CompanyRepository(ApplicationDbContext dbContext) : ICompanyRepository
{
    public void Add(Company company) => dbContext.Companies.Add(company);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) =>
        dbContext.Companies.AnyAsync(company => company.Slug == slug, cancellationToken);

    public Task<Company?> FindByPublicIdAsync(Guid companyPublicId, CancellationToken cancellationToken) =>
        dbContext.Companies.SingleOrDefaultAsync(company => company.PublicId == companyPublicId, cancellationToken);

    // AC-7: boolean existence probe for GetById's 404-vs-403 disambiguation (no aggregate materialization).
    public Task<bool> ExistsByPublicIdAsync(Guid companyPublicId, CancellationToken cancellationToken) =>
        dbContext.Companies.AnyAsync(company => company.PublicId == companyPublicId, cancellationToken);

    // AC-3: a fixed class id namespaces this advisory lock; the object id is derived deterministically from
    // the owner's public id so every capacity-bounded mutation (create/reactivate) of one owner contends on
    // the same lock. Executed on the context's current transaction (the handler opens one), so
    // pg_advisory_xact_lock holds until that transaction commits/rolls back, serializing the quota check.
    private const int OwnerCapacityLockClassId = 0x41_43_43_50; // "ACCP" — account-company capacity

    public Task AcquireOwnerCapacityLockAsync(Guid ownerUserPublicId, CancellationToken cancellationToken)
    {
        var ownerKey = BitConverter.ToInt32(ownerUserPublicId.ToByteArray(), 0);
        return dbContext.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock({0}, {1})",
            new object[] { OwnerCapacityLockClassId, ownerKey },
            cancellationToken);
    }

    public Task<AccountCompanyDetailResponse?> FindOwnedByUserAsync(
        Guid companyPublicId,
        Guid ownerUserPublicId,
        Guid? activeTenantId,
        CancellationToken cancellationToken) =>
        BuildOwnedCompanyQuery(ownerUserPublicId, activeTenantId)
            .Where(company => company.CompanyId == companyPublicId)
            .Select(company => new AccountCompanyDetailResponse(
                company.CompanyId,
                company.Name,
                company.Slug,
                company.CountryCode,
                company.Status,
                company.PlanCode,
                company.IsActiveContext,
                IsOwnedByCurrentUser: true,
                company.CreatedAtUtc,
                company.ModifiedAtUtc,
                company.ConcurrencyToken,
                dbContext.LegalRepresentatives
                    .AsNoTracking()
                    // AC-2: the owned-company detail can be read for any company the caller owns, not only the
                    // active tenant. LegalRepresentative is tenant-scoped, so the ambient tenant filter would
                    // otherwise return empty for every company that is not the active tenant (and for the 201
                    // of a just-created company).
                    // Intentional tenant filter bypass: re-anchored explicitly to this company's TenantId below.
                    .IgnoreQueryFilters()
                    .Where(legalRepresentative =>
                        legalRepresentative.TenantId == company.CompanyId &&
                        legalRepresentative.IsActive)
                    .OrderByDescending(legalRepresentative => legalRepresentative.IsPrimary == true)
                    .ThenBy(legalRepresentative => legalRepresentative.FullName)
                    .Select(legalRepresentative => new ActiveLegalRepresentativeSummaryResponse(
                        legalRepresentative.PublicId,
                        legalRepresentative.FullName,
                        legalRepresentative.RepresentationType,
                        legalRepresentative.PositionTitle,
                        legalRepresentative.IsPrimary))
                    .ToArray(),
                company.CompanyType))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PagedResponse<AccountCompanySummaryResponse>> GetOwnedByUserAsync(
        Guid ownerUserPublicId,
        CompanyListFilter filter,
        CancellationToken cancellationToken)
    {
        var query = BuildOwnedCompanyQuery(ownerUserPublicId, filter.ActiveTenantId);

        if (filter.Status.HasValue)
        {
            query = query.Where(company => company.Status == filter.Status.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(company => company.Name)
            .ThenBy(company => company.CompanyId)
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(company => new AccountCompanySummaryResponse(
                company.CompanyId,
                company.Name,
                company.Slug,
                company.CountryCode,
                company.Status,
                company.PlanCode,
                company.IsActiveContext,
                IsOwnedByCurrentUser: true,
                company.CreatedAtUtc,
                company.CompanyType))
            .ToListAsync(cancellationToken);

        return new PagedResponse<AccountCompanySummaryResponse>(items, filter.PageNumber, filter.PageSize, totalCount);
    }

    public Task<int> CountOwnedByUserAsync(
        Guid ownerUserPublicId,
        CompanyOwnershipCountFilter filter,
        CancellationToken cancellationToken) =>
        CountOwnedByUserInternalAsync(ownerUserPublicId, filter, cancellationToken);

    private Task<int> CountOwnedByUserInternalAsync(
        Guid ownerUserPublicId,
        CompanyOwnershipCountFilter filter,
        CancellationToken cancellationToken)
    {
        if (filter.Statuses.Length == 0)
        {
            return Task.FromResult(0);
        }

        // AC-7: count set-based (WHERE created_by AND status IN (...)) instead of materializing every owned
        // company's status and counting in memory. Bind a List (not the array) so the predicate resolves to
        // Enumerable.Contains → SQL IN, not the ReadOnlySpan MemoryExtensions.Contains overload that .NET
        // prefers for arrays and that EF cannot translate.
        var statuses = filter.Statuses.ToList();
        return dbContext.Companies
            .AsNoTracking()
            .Where(company =>
                company.CreatedByUserPublicId == ownerUserPublicId &&
                statuses.Contains(company.Status))
            .CountAsync(cancellationToken);
    }

    private IQueryable<OwnedCompanyProjection> BuildOwnedCompanyQuery(Guid ownerUserPublicId, Guid? activeTenantId) =>
        dbContext.Companies
            .AsNoTracking()
            .Where(company => company.CreatedByUserPublicId == ownerUserPublicId)
            .Select(company => new OwnedCompanyProjection
            {
                CompanyId = company.PublicId,
                Name = company.Name,
                Slug = company.Slug,
                CountryCode = company.CountryCode,
                Status = company.Status,
                PlanCode = dbContext.CompanySubscriptions
                    .AsNoTracking()
                    .Where(subscription =>
                        subscription.CompanyId == company.Id &&
                        (subscription.Status == SubscriptionStatus.Active ||
                         subscription.Status == SubscriptionStatus.Trial))
                    .OrderByDescending(subscription => subscription.StartDateUtc)
                    .Select(subscription => subscription.PlanCode)
                    .FirstOrDefault() ?? string.Empty,
                IsActiveContext = activeTenantId.HasValue && company.PublicId == activeTenantId.Value,
                // AC-7: a single correlated subquery projecting the metadata object, instead of four separate
                // subqueries to the same catalog item per row (left-join semantics: null when unset).
                CompanyType = dbContext.CompanyTypeCatalogItems
                    .AsNoTracking()
                    .Where(item => item.Id == company.CompanyTypeCatalogItemId)
                    .Select(item => new CompanyTypeMetadataResponse(item.PublicId, item.Code, item.Name, item.IsActive))
                    .FirstOrDefault(),
                CreatedAtUtc = company.CreatedUtc,
                ModifiedAtUtc = company.ModifiedUtc,
                ConcurrencyToken = company.ConcurrencyToken
            });

    private sealed class OwnedCompanyProjection
    {
        public Guid CompanyId { get; init; }

        public string Name { get; init; } = string.Empty;

        public string Slug { get; init; } = string.Empty;

        public string CountryCode { get; init; } = string.Empty;

        public CompanyStatus Status { get; init; }

        public string PlanCode { get; init; } = string.Empty;

        public bool IsActiveContext { get; init; }

        public CompanyTypeMetadataResponse? CompanyType { get; init; }

        public DateTime CreatedAtUtc { get; init; }

        public DateTime? ModifiedAtUtc { get; init; }

        public Guid ConcurrencyToken { get; init; }
    }
}

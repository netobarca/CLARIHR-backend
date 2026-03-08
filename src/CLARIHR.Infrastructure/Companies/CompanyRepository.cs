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
                company.Status,
                company.PlanCode,
                company.IsActiveContext,
                IsOwnedByCurrentUser: true,
                company.CreatedAtUtc,
                company.ModifiedAtUtc,
                dbContext.LegalRepresentatives
                    .AsNoTracking()
                    .Where(legalRepresentative =>
                        legalRepresentative.TenantId == company.CompanyId &&
                        legalRepresentative.IsActive)
                    .OrderByDescending(legalRepresentative => legalRepresentative.IsPrimary)
                    .ThenBy(legalRepresentative => legalRepresentative.FullName)
                    .Select(legalRepresentative => new ActiveLegalRepresentativeSummaryResponse(
                        legalRepresentative.PublicId,
                        legalRepresentative.FullName,
                        legalRepresentative.RepresentationType,
                        legalRepresentative.PositionTitle,
                        legalRepresentative.IsPrimary))
                    .ToArray()))
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
                company.Status,
                company.PlanCode,
                company.IsActiveContext,
                IsOwnedByCurrentUser: true,
                company.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<AccountCompanySummaryResponse>(items, filter.PageNumber, filter.PageSize, totalCount);
    }

    public Task<int> CountOwnedByUserAsync(
        Guid ownerUserPublicId,
        CompanyOwnershipCountFilter filter,
        CancellationToken cancellationToken) =>
        CountOwnedByUserInternalAsync(ownerUserPublicId, filter, cancellationToken);

    private async Task<int> CountOwnedByUserInternalAsync(
        Guid ownerUserPublicId,
        CompanyOwnershipCountFilter filter,
        CancellationToken cancellationToken)
    {
        var statuses = filter.Statuses.ToHashSet();
        if (statuses.Count == 0)
        {
            return 0;
        }

        var ownedStatuses = await dbContext.Companies
            .AsNoTracking()
            .Where(company => company.CreatedByUserPublicId == ownerUserPublicId)
            .Select(company => company.Status)
            .ToListAsync(cancellationToken);

        return ownedStatuses.Count(statuses.Contains);
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
                Status = company.Status,
                PlanCode = dbContext.CompanySubscriptions
                    .AsNoTracking()
                    .Where(subscription =>
                        subscription.CompanyId == company.Id &&
                        subscription.Status == SubscriptionStatus.Active)
                    .OrderByDescending(subscription => subscription.StartDateUtc)
                    .Select(subscription => subscription.PlanCode)
                    .FirstOrDefault() ?? string.Empty,
                IsActiveContext = activeTenantId.HasValue && company.PublicId == activeTenantId.Value,
                CreatedAtUtc = company.CreatedUtc,
                ModifiedAtUtc = company.ModifiedUtc
            });

    private sealed class OwnedCompanyProjection
    {
        public Guid CompanyId { get; init; }

        public string Name { get; init; } = string.Empty;

        public string Slug { get; init; } = string.Empty;

        public CompanyStatus Status { get; init; }

        public string PlanCode { get; init; } = string.Empty;

        public bool IsActiveContext { get; init; }

        public DateTime CreatedAtUtc { get; init; }

        public DateTime? ModifiedAtUtc { get; init; }
    }
}

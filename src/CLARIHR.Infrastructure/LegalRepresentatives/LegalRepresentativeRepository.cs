using CLARIHR.Application.Abstractions.LegalRepresentatives;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.LegalRepresentatives;
using CLARIHR.Domain.LegalRepresentatives;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.LegalRepresentatives;

internal sealed class LegalRepresentativeRepository(ApplicationDbContext dbContext) : ILegalRepresentativeRepository
{
    public void Add(LegalRepresentative legalRepresentative) => dbContext.Set<LegalRepresentative>().Add(legalRepresentative);

    public Task<LegalRepresentative?> GetByIdAsync(Guid legalRepresentativeId, CancellationToken cancellationToken) =>
        dbContext.Set<LegalRepresentative>().SingleOrDefaultAsync(
            legalRepresentative => legalRepresentative.PublicId == legalRepresentativeId,
            cancellationToken);

    public Task<bool> ExistsOutsideTenantAsync(Guid legalRepresentativeId, CancellationToken cancellationToken) =>
        dbContext.Set<LegalRepresentative>()
            .IgnoreQueryFilters()
            .AnyAsync(legalRepresentative => legalRepresentative.PublicId == legalRepresentativeId, cancellationToken);

    public Task<bool> DocumentExistsAsync(
        Guid tenantId,
        LegalRepresentativeDocumentType documentType,
        string normalizedDocumentNumber,
        long? excludingLegalRepresentativeId,
        CancellationToken cancellationToken) =>
        dbContext.Set<LegalRepresentative>().AnyAsync(
            legalRepresentative => legalRepresentative.TenantId == tenantId &&
                                  legalRepresentative.DocumentType == documentType &&
                                  legalRepresentative.NormalizedDocumentNumber == normalizedDocumentNumber &&
                                  (!excludingLegalRepresentativeId.HasValue || legalRepresentative.Id != excludingLegalRepresentativeId.Value),
            cancellationToken);

    public async Task<PagedResponse<LegalRepresentativeListItemResponse>> SearchAsync(
        Guid tenantId,
        bool? isActive,
        bool? isPrimary,
        LegalRepresentativeRepresentationType? representationType,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<LegalRepresentative>()
            .AsNoTracking()
            .Where(legalRepresentative => legalRepresentative.TenantId == tenantId);

        if (isActive.HasValue)
        {
            query = query.Where(legalRepresentative => legalRepresentative.IsActive == isActive.Value);
        }

        if (isPrimary.HasValue)
        {
            query = query.Where(legalRepresentative => legalRepresentative.IsPrimary == isPrimary.Value);
        }

        if (representationType.HasValue)
        {
            query = query.Where(legalRepresentative => legalRepresentative.RepresentationType == representationType.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(legalRepresentative =>
                legalRepresentative.NormalizedFullName.Contains(normalizedSearch) ||
                legalRepresentative.PositionTitle.ToUpper().Contains(normalizedSearch) ||
                legalRepresentative.NormalizedDocumentNumber.Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(legalRepresentative => legalRepresentative.IsPrimary)
            .ThenBy(legalRepresentative => legalRepresentative.FullName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(legalRepresentative => new LegalRepresentativeListItemResponse(
                legalRepresentative.PublicId,
                legalRepresentative.TenantId,
                legalRepresentative.FirstName,
                legalRepresentative.LastName,
                legalRepresentative.FullName,
                legalRepresentative.DocumentType,
                legalRepresentative.DocumentNumber,
                legalRepresentative.PositionTitle,
                legalRepresentative.RepresentationType,
                legalRepresentative.IsPrimary,
                legalRepresentative.IsActive,
                legalRepresentative.EffectiveFromUtc,
                legalRepresentative.EffectiveToUtc,
                legalRepresentative.ConcurrencyToken,
                legalRepresentative.CreatedUtc,
                legalRepresentative.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<LegalRepresentativeListItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<LegalRepresentativeResponse?> GetResponseByIdAsync(Guid legalRepresentativeId, CancellationToken cancellationToken) =>
        dbContext.Set<LegalRepresentative>()
            .AsNoTracking()
            .Where(legalRepresentative => legalRepresentative.PublicId == legalRepresentativeId)
            .Select(legalRepresentative => new LegalRepresentativeResponse(
                legalRepresentative.PublicId,
                legalRepresentative.TenantId,
                legalRepresentative.FirstName,
                legalRepresentative.LastName,
                legalRepresentative.FullName,
                legalRepresentative.DocumentType,
                legalRepresentative.DocumentNumber,
                legalRepresentative.PositionTitle,
                legalRepresentative.RepresentationType,
                legalRepresentative.AuthorityDescription,
                legalRepresentative.AppointmentInstrument,
                legalRepresentative.AppointmentDateUtc,
                legalRepresentative.EffectiveFromUtc,
                legalRepresentative.EffectiveToUtc,
                legalRepresentative.Email,
                legalRepresentative.Phone,
                legalRepresentative.IsPrimary,
                legalRepresentative.IsActive,
                legalRepresentative.ConcurrencyToken,
                legalRepresentative.CreatedUtc,
                legalRepresentative.ModifiedUtc))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<LegalRepresentativeUsageResponse?> GetUsageByIdAsync(Guid legalRepresentativeId, CancellationToken cancellationToken)
    {
        var legalRepresentative = await dbContext.Set<LegalRepresentative>()
            .AsNoTracking()
            .Where(item => item.PublicId == legalRepresentativeId)
            .Select(item => new
            {
                item.PublicId,
                item.TenantId,
                item.IsActive
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (legalRepresentative is null)
        {
            return null;
        }

        var activeCount = await dbContext.Set<LegalRepresentative>()
            .AsNoTracking()
            .Where(item => item.TenantId == legalRepresentative.TenantId && item.IsActive)
            .CountAsync(cancellationToken);

        var canInactivate = !legalRepresentative.IsActive || activeCount > 1;

        return new LegalRepresentativeUsageResponse(
            legalRepresentative.PublicId,
            ActiveDocumentReferencesCount: 0,
            CanInactivate: canInactivate);
    }

    public async Task<IReadOnlyCollection<LegalRepresentativeDocumentTypeCatalogItemResponse>> GetDocumentTypeCatalogItemsAsync(
        CancellationToken cancellationToken)
    {
        return await dbContext.LegalRepresentativeDocumentTypeCatalogItems
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Id)
            .Select(item => new LegalRepresentativeDocumentTypeCatalogItemResponse(
                (int)item.Id,
                item.Code,
                item.Name,
                item.SortOrder))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<LegalRepresentativePositionTitleCatalogItemResponse>> GetPositionTitleCatalogItemsAsync(
        CancellationToken cancellationToken)
    {
        return await dbContext.LegalRepresentativePositionTitleCatalogItems
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Id)
            .Select(item => new LegalRepresentativePositionTitleCatalogItemResponse(
                (int)item.Id,
                item.Code,
                item.Name,
                item.SortOrder))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<LegalRepresentativeRepresentationTypeCatalogItemResponse>> GetRepresentationTypeCatalogItemsAsync(
        CancellationToken cancellationToken)
    {
        return await dbContext.LegalRepresentativeRepresentationTypeCatalogItems
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Id)
            .Select(item => new LegalRepresentativeRepresentationTypeCatalogItemResponse(
                (int)item.Id,
                item.Code,
                item.Name,
                item.SortOrder))
            .ToArrayAsync(cancellationToken);
    }

    public Task<int> GetActiveCountAsync(Guid tenantId, CancellationToken cancellationToken) =>
        dbContext.Set<LegalRepresentative>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.IsActive)
            .CountAsync(cancellationToken);

    public Task<LegalRepresentative?> GetActivePrimaryAsync(
        Guid tenantId,
        Guid? excludingLegalRepresentativePublicId,
        CancellationToken cancellationToken) =>
        dbContext.Set<LegalRepresentative>()
            .Where(item => item.TenantId == tenantId && item.IsActive && item.IsPrimary)
            .Where(item => !excludingLegalRepresentativePublicId.HasValue || item.PublicId != excludingLegalRepresentativePublicId.Value)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<LegalRepresentativeExportRow>> GetExportRowsAsync(
        Guid tenantId,
        bool? isActive,
        bool? isPrimary,
        LegalRepresentativeRepresentationType? representationType,
        string? search,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<LegalRepresentative>()
            .AsNoTracking()
            .Where(legalRepresentative => legalRepresentative.TenantId == tenantId);

        if (isActive.HasValue)
        {
            query = query.Where(legalRepresentative => legalRepresentative.IsActive == isActive.Value);
        }

        if (isPrimary.HasValue)
        {
            query = query.Where(legalRepresentative => legalRepresentative.IsPrimary == isPrimary.Value);
        }

        if (representationType.HasValue)
        {
            query = query.Where(legalRepresentative => legalRepresentative.RepresentationType == representationType.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(legalRepresentative =>
                legalRepresentative.NormalizedFullName.Contains(normalizedSearch) ||
                legalRepresentative.PositionTitle.ToUpper().Contains(normalizedSearch) ||
                legalRepresentative.NormalizedDocumentNumber.Contains(normalizedSearch));
        }

        return await query
            .OrderByDescending(legalRepresentative => legalRepresentative.IsPrimary)
            .ThenBy(legalRepresentative => legalRepresentative.FullName)
            .Select(legalRepresentative => new LegalRepresentativeExportRow(
                legalRepresentative.PublicId,
                legalRepresentative.FirstName,
                legalRepresentative.LastName,
                legalRepresentative.FullName,
                legalRepresentative.DocumentType,
                legalRepresentative.DocumentNumber,
                legalRepresentative.PositionTitle,
                legalRepresentative.RepresentationType,
                legalRepresentative.AuthorityDescription,
                legalRepresentative.AppointmentInstrument,
                legalRepresentative.AppointmentDateUtc,
                legalRepresentative.EffectiveFromUtc,
                legalRepresentative.EffectiveToUtc,
                legalRepresentative.Email,
                legalRepresentative.Phone,
                legalRepresentative.IsPrimary,
                legalRepresentative.IsActive,
                legalRepresentative.CreatedUtc,
                legalRepresentative.ModifiedUtc))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ActiveLegalRepresentativeSummary>> GetActiveSummariesByCompanyAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Set<LegalRepresentative>()
            .AsNoTracking()
            .Where(legalRepresentative => legalRepresentative.TenantId == companyId && legalRepresentative.IsActive)
            .OrderByDescending(legalRepresentative => legalRepresentative.IsPrimary)
            .ThenBy(legalRepresentative => legalRepresentative.FullName)
            .Select(legalRepresentative => new ActiveLegalRepresentativeSummary(
                legalRepresentative.PublicId,
                legalRepresentative.FullName,
                legalRepresentative.RepresentationType,
                legalRepresentative.PositionTitle,
                legalRepresentative.IsPrimary))
            .ToArrayAsync(cancellationToken);
    }
}

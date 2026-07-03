using CLARIHR.Application.Abstractions.EducationCatalogs;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.EducationCatalogs;
using CLARIHR.Application.Features.EducationCatalogs.Common;
using CLARIHR.Domain.EducationCatalogs;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.EducationCatalogs;

internal sealed class EducationCatalogRepository(ApplicationDbContext dbContext) : IEducationCatalogRepository
{
    public void Add(EducationCatalogItem item)
    {
        switch (item)
        {
            case EducationStatusCatalogItem status:
                dbContext.EducationStatusCatalogItems.Add(status);
                return;
            case EducationStudyTypeCatalogItem studyType:
                dbContext.EducationStudyTypeCatalogItems.Add(studyType);
                return;
            case EducationLevelCatalogItem level:
                dbContext.EducationLevelCatalogItems.Add(level);
                return;
            case EducationShiftCatalogItem shift:
                dbContext.EducationShiftCatalogItems.Add(shift);
                return;
            case EducationModalityCatalogItem modality:
                dbContext.EducationModalityCatalogItems.Add(modality);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(item), item.GetType().Name, "Unsupported education catalog item type.");
        }
    }

    public Task<EducationCatalogItem?> GetByIdAsync(
        EducationCatalogType catalogType,
        Guid id,
        CancellationToken cancellationToken) =>
        catalogType switch
        {
            EducationCatalogType.EducationStatus => GetByIdAsync<EducationStatusCatalogItem>(id, cancellationToken),
            EducationCatalogType.StudyType => GetByIdAsync<EducationStudyTypeCatalogItem>(id, cancellationToken),
            EducationCatalogType.Career => throw new NotSupportedException("Career catalog administration is seed-only in this phase (country-scoped, RF-009/DP-06)."),
            EducationCatalogType.Shift => GetByIdAsync<EducationShiftCatalogItem>(id, cancellationToken),
            EducationCatalogType.Modality => GetByIdAsync<EducationModalityCatalogItem>(id, cancellationToken),
            EducationCatalogType.Level => GetByIdAsync<EducationLevelCatalogItem>(id, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(catalogType), catalogType, "Unsupported education catalog type.")
        };

    public Task<bool> CodeExistsAsync(
        EducationCatalogType catalogType,
        string normalizedCode,
        long? excludingId,
        CancellationToken cancellationToken) =>
        catalogType switch
        {
            EducationCatalogType.EducationStatus => CodeExistsAsync<EducationStatusCatalogItem>(normalizedCode, excludingId, cancellationToken),
            EducationCatalogType.StudyType => CodeExistsAsync<EducationStudyTypeCatalogItem>(normalizedCode, excludingId, cancellationToken),
            EducationCatalogType.Career => throw new NotSupportedException("Career catalog administration is seed-only in this phase (country-scoped, RF-009/DP-06)."),
            EducationCatalogType.Shift => CodeExistsAsync<EducationShiftCatalogItem>(normalizedCode, excludingId, cancellationToken),
            EducationCatalogType.Modality => CodeExistsAsync<EducationModalityCatalogItem>(normalizedCode, excludingId, cancellationToken),
            EducationCatalogType.Level => CodeExistsAsync<EducationLevelCatalogItem>(normalizedCode, excludingId, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(catalogType), catalogType, "Unsupported education catalog type.")
        };

    public Task<PagedResponse<EducationCatalogItemResponse>> SearchAsync(
        EducationCatalogType catalogType,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken) =>
        catalogType switch
        {
            EducationCatalogType.EducationStatus => SearchAsync<EducationStatusCatalogItem>(catalogType, isActive, search, pageNumber, pageSize, cancellationToken),
            EducationCatalogType.StudyType => SearchAsync<EducationStudyTypeCatalogItem>(catalogType, isActive, search, pageNumber, pageSize, cancellationToken),
            EducationCatalogType.Career => throw new NotSupportedException("Career catalog administration is seed-only in this phase (country-scoped, RF-009/DP-06)."),
            EducationCatalogType.Shift => SearchAsync<EducationShiftCatalogItem>(catalogType, isActive, search, pageNumber, pageSize, cancellationToken),
            EducationCatalogType.Modality => SearchAsync<EducationModalityCatalogItem>(catalogType, isActive, search, pageNumber, pageSize, cancellationToken),
            EducationCatalogType.Level => SearchAsync<EducationLevelCatalogItem>(catalogType, isActive, search, pageNumber, pageSize, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(catalogType), catalogType, "Unsupported education catalog type.")
        };

    public Task<EducationCatalogItemResponse?> GetResponseByIdAsync(
        EducationCatalogType catalogType,
        Guid id,
        CancellationToken cancellationToken) =>
        catalogType switch
        {
            EducationCatalogType.EducationStatus => GetResponseByIdAsync<EducationStatusCatalogItem>(catalogType, id, cancellationToken),
            EducationCatalogType.StudyType => GetResponseByIdAsync<EducationStudyTypeCatalogItem>(catalogType, id, cancellationToken),
            EducationCatalogType.Career => throw new NotSupportedException("Career catalog administration is seed-only in this phase (country-scoped, RF-009/DP-06)."),
            EducationCatalogType.Shift => GetResponseByIdAsync<EducationShiftCatalogItem>(catalogType, id, cancellationToken),
            EducationCatalogType.Modality => GetResponseByIdAsync<EducationModalityCatalogItem>(catalogType, id, cancellationToken),
            EducationCatalogType.Level => GetResponseByIdAsync<EducationLevelCatalogItem>(catalogType, id, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(catalogType), catalogType, "Unsupported education catalog type.")
        };

    public Task<EducationCatalogLookupInternal?> GetActiveLookupByIdAsync(
        EducationCatalogType catalogType,
        Guid id,
        CancellationToken cancellationToken) =>
        catalogType switch
        {
            EducationCatalogType.EducationStatus => GetActiveLookupByIdAsync<EducationStatusCatalogItem>(id, cancellationToken),
            EducationCatalogType.StudyType => GetActiveLookupByIdAsync<EducationStudyTypeCatalogItem>(id, cancellationToken),
            // Careers left the education base type (country-scoped, RF-009) but the personnel-file
            // education flow still resolves them by publicId — dedicated query below.
            EducationCatalogType.Career => GetActiveCareerLookupByIdAsync(id, cancellationToken),
            EducationCatalogType.Shift => GetActiveLookupByIdAsync<EducationShiftCatalogItem>(id, cancellationToken),
            EducationCatalogType.Modality => GetActiveLookupByIdAsync<EducationModalityCatalogItem>(id, cancellationToken),
            EducationCatalogType.Level => GetActiveLookupByIdAsync<EducationLevelCatalogItem>(id, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(catalogType), catalogType, "Unsupported education catalog type.")
        };

    public Task<bool> IsInUseAsync(
        EducationCatalogType catalogType,
        long catalogItemId,
        CancellationToken cancellationToken) =>
        catalogType switch
        {
            EducationCatalogType.EducationStatus => dbContext.PersonnelFileEducations.AnyAsync(
                item => item.EducationStatusCatalogItemId == catalogItemId, cancellationToken),
            EducationCatalogType.StudyType => dbContext.PersonnelFileEducations.AnyAsync(
                item => item.EducationStudyTypeCatalogItemId == catalogItemId, cancellationToken),
            EducationCatalogType.Career => dbContext.PersonnelFileEducations.AnyAsync(
                item => item.EducationCareerCatalogItemId == catalogItemId, cancellationToken),
            EducationCatalogType.Shift => dbContext.PersonnelFileEducations.AnyAsync(
                item => item.EducationShiftCatalogItemId == catalogItemId, cancellationToken),
            EducationCatalogType.Modality => dbContext.PersonnelFileEducations.AnyAsync(
                item => item.EducationModalityCatalogItemId == catalogItemId, cancellationToken),
            // A level is "in use" when a study type references it (not personnel-file educations).
            EducationCatalogType.Level => dbContext.EducationStudyTypeCatalogItems.AnyAsync(
                item => item.EducationLevelCatalogItemId == catalogItemId, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(catalogType), catalogType, "Unsupported education catalog type.")
        };

    // ─── Private helpers ─────────────────────────────────────────────────────

    private async Task<EducationCatalogItem?> GetByIdAsync<TCatalogItem>(Guid id, CancellationToken cancellationToken)
        where TCatalogItem : EducationCatalogItem =>
        await dbContext.Set<TCatalogItem>().SingleOrDefaultAsync(item => item.PublicId == id, cancellationToken);

    private Task<bool> CodeExistsAsync<TCatalogItem>(
        string normalizedCode,
        long? excludingId,
        CancellationToken cancellationToken)
        where TCatalogItem : EducationCatalogItem =>
        dbContext.Set<TCatalogItem>().AnyAsync(
            item => item.NormalizedCode == normalizedCode &&
                    (!excludingId.HasValue || item.Id != excludingId.Value),
            cancellationToken);

    private async Task<PagedResponse<EducationCatalogItemResponse>> SearchAsync<TCatalogItem>(
        EducationCatalogType catalogType,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
        where TCatalogItem : EducationCatalogItem
    {
        var query = dbContext.Set<TCatalogItem>().AsNoTracking();

        if (isActive.HasValue)
        {
            query = query.Where(item => item.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(item =>
                item.NormalizedCode.Contains(normalizedSearch) ||
                item.NormalizedName.Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .ThenBy(item => item.Code)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new EducationCatalogItemResponse(
                item.PublicId,
                catalogType,
                item.Code,
                item.Name,
                item.SortOrder,
                item.IsActive,
                item.ConcurrencyToken,
                item.CreatedUtc,
                item.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<EducationCatalogItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    private Task<EducationCatalogItemResponse?> GetResponseByIdAsync<TCatalogItem>(
        EducationCatalogType catalogType,
        Guid id,
        CancellationToken cancellationToken)
        where TCatalogItem : EducationCatalogItem =>
        dbContext.Set<TCatalogItem>()
            .AsNoTracking()
            .Where(item => item.PublicId == id)
            .Select(item => new EducationCatalogItemResponse(
                item.PublicId,
                catalogType,
                item.Code,
                item.Name,
                item.SortOrder,
                item.IsActive,
                item.ConcurrencyToken,
                item.CreatedUtc,
                item.ModifiedUtc))
            .SingleOrDefaultAsync(cancellationToken);

    private Task<EducationCatalogLookupInternal?> GetActiveCareerLookupByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.EducationCareerCatalogItems
            .AsNoTracking()
            .Where(item => item.PublicId == id && item.IsActive)
            .Select(item => new EducationCatalogLookupInternal(item.Id, item.PublicId, item.Code, item.Name, item.IsActive))
            .SingleOrDefaultAsync(cancellationToken);

    private Task<EducationCatalogLookupInternal?> GetActiveLookupByIdAsync<TCatalogItem>(Guid id, CancellationToken cancellationToken)
        where TCatalogItem : EducationCatalogItem =>
        dbContext.Set<TCatalogItem>()
            .AsNoTracking()
            .Where(item => item.PublicId == id && item.IsActive)
            .Select(item => new EducationCatalogLookupInternal(item.Id, item.PublicId, item.Code, item.Name, item.IsActive))
            .SingleOrDefaultAsync(cancellationToken);
}

using CLARIHR.Application.Abstractions.PersonnelEducationCatalogs;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PersonnelEducationCatalogs;
using CLARIHR.Application.Features.PersonnelEducationCatalogs.Common;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.PersonnelEducationCatalogs;

internal sealed class PersonnelEducationCatalogRepository(ApplicationDbContext dbContext) : IPersonnelEducationCatalogRepository
{
    public void Add(PersonnelEducationCatalogItem item)
    {
        switch (item)
        {
            case EducationStatusCatalogItem status:
                dbContext.EducationStatusCatalogItems.Add(status);
                return;
            case EducationStudyTypeCatalogItem studyType:
                dbContext.EducationStudyTypeCatalogItems.Add(studyType);
                return;
            case EducationCareerCatalogItem career:
                dbContext.EducationCareerCatalogItems.Add(career);
                return;
            case EducationShiftCatalogItem shift:
                dbContext.EducationShiftCatalogItems.Add(shift);
                return;
            case EducationModalityCatalogItem modality:
                dbContext.EducationModalityCatalogItems.Add(modality);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(item), item.GetType().Name, "Unsupported catalog item type.");
        }
    }

    public Task<PersonnelEducationCatalogItem?> GetByIdAsync(
        PersonnelEducationCatalogType catalogType,
        Guid id,
        CancellationToken cancellationToken) =>
        catalogType switch
        {
            PersonnelEducationCatalogType.EducationStatus => GetByIdAsync<EducationStatusCatalogItem>(id, cancellationToken),
            PersonnelEducationCatalogType.StudyType => GetByIdAsync<EducationStudyTypeCatalogItem>(id, cancellationToken),
            PersonnelEducationCatalogType.Career => GetByIdAsync<EducationCareerCatalogItem>(id, cancellationToken),
            PersonnelEducationCatalogType.Shift => GetByIdAsync<EducationShiftCatalogItem>(id, cancellationToken),
            PersonnelEducationCatalogType.Modality => GetByIdAsync<EducationModalityCatalogItem>(id, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(catalogType), catalogType, "Unsupported catalog type.")
        };

    public Task<bool> ExistsOutsideTenantAsync(
        PersonnelEducationCatalogType catalogType,
        Guid id,
        CancellationToken cancellationToken) =>
        catalogType switch
        {
            PersonnelEducationCatalogType.EducationStatus => ExistsOutsideTenantAsync<EducationStatusCatalogItem>(id, cancellationToken),
            PersonnelEducationCatalogType.StudyType => ExistsOutsideTenantAsync<EducationStudyTypeCatalogItem>(id, cancellationToken),
            PersonnelEducationCatalogType.Career => ExistsOutsideTenantAsync<EducationCareerCatalogItem>(id, cancellationToken),
            PersonnelEducationCatalogType.Shift => ExistsOutsideTenantAsync<EducationShiftCatalogItem>(id, cancellationToken),
            PersonnelEducationCatalogType.Modality => ExistsOutsideTenantAsync<EducationModalityCatalogItem>(id, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(catalogType), catalogType, "Unsupported catalog type.")
        };

    public Task<bool> CodeExistsAsync(
        Guid tenantId,
        PersonnelEducationCatalogType catalogType,
        string normalizedCode,
        long? excludingId,
        CancellationToken cancellationToken) =>
        catalogType switch
        {
            PersonnelEducationCatalogType.EducationStatus => CodeExistsAsync<EducationStatusCatalogItem>(tenantId, normalizedCode, excludingId, cancellationToken),
            PersonnelEducationCatalogType.StudyType => CodeExistsAsync<EducationStudyTypeCatalogItem>(tenantId, normalizedCode, excludingId, cancellationToken),
            PersonnelEducationCatalogType.Career => CodeExistsAsync<EducationCareerCatalogItem>(tenantId, normalizedCode, excludingId, cancellationToken),
            PersonnelEducationCatalogType.Shift => CodeExistsAsync<EducationShiftCatalogItem>(tenantId, normalizedCode, excludingId, cancellationToken),
            PersonnelEducationCatalogType.Modality => CodeExistsAsync<EducationModalityCatalogItem>(tenantId, normalizedCode, excludingId, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(catalogType), catalogType, "Unsupported catalog type.")
        };

    public Task<PagedResponse<PersonnelEducationCatalogItemResponse>> SearchAsync(
        Guid tenantId,
        PersonnelEducationCatalogType catalogType,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken) =>
        catalogType switch
        {
            PersonnelEducationCatalogType.EducationStatus => SearchAsync<EducationStatusCatalogItem>(tenantId, catalogType, isActive, search, pageNumber, pageSize, cancellationToken),
            PersonnelEducationCatalogType.StudyType => SearchAsync<EducationStudyTypeCatalogItem>(tenantId, catalogType, isActive, search, pageNumber, pageSize, cancellationToken),
            PersonnelEducationCatalogType.Career => SearchAsync<EducationCareerCatalogItem>(tenantId, catalogType, isActive, search, pageNumber, pageSize, cancellationToken),
            PersonnelEducationCatalogType.Shift => SearchAsync<EducationShiftCatalogItem>(tenantId, catalogType, isActive, search, pageNumber, pageSize, cancellationToken),
            PersonnelEducationCatalogType.Modality => SearchAsync<EducationModalityCatalogItem>(tenantId, catalogType, isActive, search, pageNumber, pageSize, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(catalogType), catalogType, "Unsupported catalog type.")
        };

    public Task<PersonnelEducationCatalogItemResponse?> GetResponseByIdAsync(
        Guid tenantId,
        PersonnelEducationCatalogType catalogType,
        Guid id,
        CancellationToken cancellationToken) =>
        catalogType switch
        {
            PersonnelEducationCatalogType.EducationStatus => GetResponseByIdAsync<EducationStatusCatalogItem>(tenantId, catalogType, id, cancellationToken),
            PersonnelEducationCatalogType.StudyType => GetResponseByIdAsync<EducationStudyTypeCatalogItem>(tenantId, catalogType, id, cancellationToken),
            PersonnelEducationCatalogType.Career => GetResponseByIdAsync<EducationCareerCatalogItem>(tenantId, catalogType, id, cancellationToken),
            PersonnelEducationCatalogType.Shift => GetResponseByIdAsync<EducationShiftCatalogItem>(tenantId, catalogType, id, cancellationToken),
            PersonnelEducationCatalogType.Modality => GetResponseByIdAsync<EducationModalityCatalogItem>(tenantId, catalogType, id, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(catalogType), catalogType, "Unsupported catalog type.")
        };

    public Task<PersonnelEducationCatalogLookup?> GetActiveLookupByIdAsync(
        Guid tenantId,
        PersonnelEducationCatalogType catalogType,
        Guid id,
        CancellationToken cancellationToken) =>
        catalogType switch
        {
            PersonnelEducationCatalogType.EducationStatus => GetActiveLookupByIdAsync<EducationStatusCatalogItem>(tenantId, id, cancellationToken),
            PersonnelEducationCatalogType.StudyType => GetActiveLookupByIdAsync<EducationStudyTypeCatalogItem>(tenantId, id, cancellationToken),
            PersonnelEducationCatalogType.Career => GetActiveLookupByIdAsync<EducationCareerCatalogItem>(tenantId, id, cancellationToken),
            PersonnelEducationCatalogType.Shift => GetActiveLookupByIdAsync<EducationShiftCatalogItem>(tenantId, id, cancellationToken),
            PersonnelEducationCatalogType.Modality => GetActiveLookupByIdAsync<EducationModalityCatalogItem>(tenantId, id, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(catalogType), catalogType, "Unsupported catalog type.")
        };

    public Task<bool> IsInUseAsync(
        PersonnelEducationCatalogType catalogType,
        long catalogItemId,
        CancellationToken cancellationToken) =>
        catalogType switch
        {
            PersonnelEducationCatalogType.EducationStatus => dbContext.PersonnelFileEducations.AnyAsync(
                item => item.EducationStatusCatalogItemId == catalogItemId,
                cancellationToken),
            PersonnelEducationCatalogType.StudyType => dbContext.PersonnelFileEducations.AnyAsync(
                item => item.EducationStudyTypeCatalogItemId == catalogItemId,
                cancellationToken),
            PersonnelEducationCatalogType.Career => dbContext.PersonnelFileEducations.AnyAsync(
                item => item.EducationCareerCatalogItemId == catalogItemId,
                cancellationToken),
            PersonnelEducationCatalogType.Shift => dbContext.PersonnelFileEducations.AnyAsync(
                item => item.EducationShiftCatalogItemId == catalogItemId,
                cancellationToken),
            PersonnelEducationCatalogType.Modality => dbContext.PersonnelFileEducations.AnyAsync(
                item => item.EducationModalityCatalogItemId == catalogItemId,
                cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(catalogType), catalogType, "Unsupported catalog type.")
        };

    private async Task<PersonnelEducationCatalogItem?> GetByIdAsync<TCatalogItem>(
        Guid id,
        CancellationToken cancellationToken)
        where TCatalogItem : PersonnelEducationCatalogItem =>
        await dbContext.Set<TCatalogItem>().SingleOrDefaultAsync(item => item.PublicId == id, cancellationToken);

    private Task<bool> ExistsOutsideTenantAsync<TCatalogItem>(
        Guid id,
        CancellationToken cancellationToken)
        where TCatalogItem : PersonnelEducationCatalogItem =>
        dbContext.Set<TCatalogItem>()
            .IgnoreQueryFilters()
            .AnyAsync(item => item.PublicId == id, cancellationToken);

    private Task<bool> CodeExistsAsync<TCatalogItem>(
        Guid tenantId,
        string normalizedCode,
        long? excludingId,
        CancellationToken cancellationToken)
        where TCatalogItem : PersonnelEducationCatalogItem =>
        dbContext.Set<TCatalogItem>().AnyAsync(
            item => item.TenantId == tenantId &&
                    item.NormalizedCode == normalizedCode &&
                    (!excludingId.HasValue || item.Id != excludingId.Value),
            cancellationToken);

    private async Task<PagedResponse<PersonnelEducationCatalogItemResponse>> SearchAsync<TCatalogItem>(
        Guid tenantId,
        PersonnelEducationCatalogType catalogType,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
        where TCatalogItem : PersonnelEducationCatalogItem
    {
        var query = dbContext.Set<TCatalogItem>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId);

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
            .Select(item => new PersonnelEducationCatalogItemResponse(
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

        return new PagedResponse<PersonnelEducationCatalogItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    private Task<PersonnelEducationCatalogItemResponse?> GetResponseByIdAsync<TCatalogItem>(
        Guid tenantId,
        PersonnelEducationCatalogType catalogType,
        Guid id,
        CancellationToken cancellationToken)
        where TCatalogItem : PersonnelEducationCatalogItem =>
        dbContext.Set<TCatalogItem>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PublicId == id)
            .Select(item => new PersonnelEducationCatalogItemResponse(
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

    private Task<PersonnelEducationCatalogLookup?> GetActiveLookupByIdAsync<TCatalogItem>(
        Guid tenantId,
        Guid id,
        CancellationToken cancellationToken)
        where TCatalogItem : PersonnelEducationCatalogItem =>
        dbContext.Set<TCatalogItem>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PublicId == id && item.IsActive)
            .Select(item => new PersonnelEducationCatalogLookup(item.Id, item.PublicId, item.Code, item.Name, item.IsActive))
            .SingleOrDefaultAsync(cancellationToken);
}

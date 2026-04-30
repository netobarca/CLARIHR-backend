using CLARIHR.Application.Abstractions.SystemCatalogs;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.SystemCatalogs;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.GeneralCatalogs;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.SystemCatalogs;

internal sealed class SystemCatalogRepository(ApplicationDbContext dbContext) : ISystemCatalogRepository
{
    public void Add(CountryScopedCatalogItem item)
    {
        switch (item)
        {
            case LanguageCatalogItem language:
                dbContext.LanguageCatalogItems.Add(language);
                return;
            case LanguageLevelCatalogItem languageLevel:
                dbContext.LanguageLevelCatalogItems.Add(languageLevel);
                return;
            case TrainingTypeCatalogItem trainingType:
                dbContext.TrainingTypeCatalogItems.Add(trainingType);
                return;
            case DurationUnitCatalogItem durationUnit:
                dbContext.DurationUnitCatalogItems.Add(durationUnit);
                return;
            case ReferenceTypeCatalogItem referenceType:
                dbContext.ReferenceTypeCatalogItems.Add(referenceType);
                return;
            case CurrencyCatalogItem currency:
                dbContext.CurrencyCatalogItems.Add(currency);
                return;
            case IdentificationTypeCatalogItem identificationType:
                dbContext.IdentificationTypeCatalogItems.Add(identificationType);
                return;
            case ProfessionCatalogItem profession:
                dbContext.ProfessionCatalogItems.Add(profession);
                return;
            case MaritalStatusCatalogItem maritalStatus:
                dbContext.MaritalStatusCatalogItems.Add(maritalStatus);
                return;
            case KinshipCatalogItem kinship:
                dbContext.KinshipCatalogItems.Add(kinship);
                return;
            case DepartmentCatalogItem department:
                dbContext.DepartmentCatalogItems.Add(department);
                return;
            case MunicipalityCatalogItem municipality:
                dbContext.MunicipalityCatalogItems.Add(municipality);
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(item), item.GetType().Name, "Unsupported system catalog item type.");
        }
    }

    public Task<CountryScopedCatalogItem?> GetByIdAsync(
        SystemCatalogType catalogType,
        Guid id,
        CancellationToken cancellationToken) =>
        catalogType switch
        {
            SystemCatalogType.Language => GetByIdAsync<LanguageCatalogItem>(id, cancellationToken),
            SystemCatalogType.LanguageLevel => GetByIdAsync<LanguageLevelCatalogItem>(id, cancellationToken),
            SystemCatalogType.TrainingType => GetByIdAsync<TrainingTypeCatalogItem>(id, cancellationToken),
            SystemCatalogType.DurationUnit => GetByIdAsync<DurationUnitCatalogItem>(id, cancellationToken),
            SystemCatalogType.ReferenceType => GetByIdAsync<ReferenceTypeCatalogItem>(id, cancellationToken),
            SystemCatalogType.Currency => GetByIdAsync<CurrencyCatalogItem>(id, cancellationToken),
            SystemCatalogType.IdentificationType => GetByIdAsync<IdentificationTypeCatalogItem>(id, cancellationToken),
            SystemCatalogType.Profession => GetByIdAsync<ProfessionCatalogItem>(id, cancellationToken),
            SystemCatalogType.MaritalStatus => GetByIdAsync<MaritalStatusCatalogItem>(id, cancellationToken),
            SystemCatalogType.Kinship => GetByIdAsync<KinshipCatalogItem>(id, cancellationToken),
            SystemCatalogType.Department => GetByIdAsync<DepartmentCatalogItem>(id, cancellationToken),
            SystemCatalogType.Municipality => GetByIdAsync<MunicipalityCatalogItem>(id, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(catalogType), catalogType, "Unsupported system catalog type.")
        };

    public Task<bool> ExistsByCodeAsync(
        SystemCatalogType catalogType,
        long countryCatalogItemId,
        string normalizedCode,
        long? excludingId,
        CancellationToken cancellationToken) =>
        catalogType switch
        {
            SystemCatalogType.Language => ExistsByCodeAsync<LanguageCatalogItem>(countryCatalogItemId, normalizedCode, excludingId, cancellationToken),
            SystemCatalogType.LanguageLevel => ExistsByCodeAsync<LanguageLevelCatalogItem>(countryCatalogItemId, normalizedCode, excludingId, cancellationToken),
            SystemCatalogType.TrainingType => ExistsByCodeAsync<TrainingTypeCatalogItem>(countryCatalogItemId, normalizedCode, excludingId, cancellationToken),
            SystemCatalogType.DurationUnit => ExistsByCodeAsync<DurationUnitCatalogItem>(countryCatalogItemId, normalizedCode, excludingId, cancellationToken),
            SystemCatalogType.ReferenceType => ExistsByCodeAsync<ReferenceTypeCatalogItem>(countryCatalogItemId, normalizedCode, excludingId, cancellationToken),
            SystemCatalogType.Currency => ExistsByCodeAsync<CurrencyCatalogItem>(countryCatalogItemId, normalizedCode, excludingId, cancellationToken),
            SystemCatalogType.IdentificationType => ExistsByCodeAsync<IdentificationTypeCatalogItem>(countryCatalogItemId, normalizedCode, excludingId, cancellationToken),
            SystemCatalogType.Profession => ExistsByCodeAsync<ProfessionCatalogItem>(countryCatalogItemId, normalizedCode, excludingId, cancellationToken),
            SystemCatalogType.MaritalStatus => ExistsByCodeAsync<MaritalStatusCatalogItem>(countryCatalogItemId, normalizedCode, excludingId, cancellationToken),
            SystemCatalogType.Kinship => ExistsByCodeAsync<KinshipCatalogItem>(countryCatalogItemId, normalizedCode, excludingId, cancellationToken),
            SystemCatalogType.Department => ExistsByCodeAsync<DepartmentCatalogItem>(countryCatalogItemId, normalizedCode, excludingId, cancellationToken),
            SystemCatalogType.Municipality => ExistsByCodeAsync<MunicipalityCatalogItem>(countryCatalogItemId, normalizedCode, excludingId, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(catalogType), catalogType, "Unsupported system catalog type.")
        };

    public Task<PagedResponse<SystemCatalogItemResponse>> SearchAsync(
        SystemCatalogType catalogType,
        long countryCatalogItemId,
        bool? isActive,
        string? search,
        Guid? parentPublicId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken) =>
        catalogType switch
        {
            SystemCatalogType.Language => SearchAsync<LanguageCatalogItem>(catalogType, countryCatalogItemId, isActive, search, pageNumber, pageSize, cancellationToken),
            SystemCatalogType.LanguageLevel => SearchAsync<LanguageLevelCatalogItem>(catalogType, countryCatalogItemId, isActive, search, pageNumber, pageSize, cancellationToken),
            SystemCatalogType.TrainingType => SearchAsync<TrainingTypeCatalogItem>(catalogType, countryCatalogItemId, isActive, search, pageNumber, pageSize, cancellationToken),
            SystemCatalogType.DurationUnit => SearchAsync<DurationUnitCatalogItem>(catalogType, countryCatalogItemId, isActive, search, pageNumber, pageSize, cancellationToken),
            SystemCatalogType.ReferenceType => SearchAsync<ReferenceTypeCatalogItem>(catalogType, countryCatalogItemId, isActive, search, pageNumber, pageSize, cancellationToken),
            SystemCatalogType.Currency => SearchAsync<CurrencyCatalogItem>(catalogType, countryCatalogItemId, isActive, search, pageNumber, pageSize, cancellationToken),
            SystemCatalogType.IdentificationType => SearchAsync<IdentificationTypeCatalogItem>(catalogType, countryCatalogItemId, isActive, search, pageNumber, pageSize, cancellationToken),
            SystemCatalogType.Profession => SearchAsync<ProfessionCatalogItem>(catalogType, countryCatalogItemId, isActive, search, pageNumber, pageSize, cancellationToken),
            SystemCatalogType.MaritalStatus => SearchAsync<MaritalStatusCatalogItem>(catalogType, countryCatalogItemId, isActive, search, pageNumber, pageSize, cancellationToken),
            SystemCatalogType.Kinship => SearchAsync<KinshipCatalogItem>(catalogType, countryCatalogItemId, isActive, search, pageNumber, pageSize, cancellationToken),
            SystemCatalogType.Department => SearchAsync<DepartmentCatalogItem>(catalogType, countryCatalogItemId, isActive, search, pageNumber, pageSize, cancellationToken),
            SystemCatalogType.Municipality => SearchMunicipalitiesAsync(catalogType, countryCatalogItemId, isActive, search, parentPublicId, pageNumber, pageSize, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(catalogType), catalogType, "Unsupported system catalog type.")
        };

    public Task<SystemCatalogItemResponse?> GetResponseByIdAsync(
        SystemCatalogType catalogType,
        Guid id,
        CancellationToken cancellationToken) =>
        catalogType switch
        {
            SystemCatalogType.Municipality => GetMunicipalityResponseByIdAsync(id, cancellationToken),
            SystemCatalogType.Language => GetResponseByIdAsync<LanguageCatalogItem>(catalogType, id, cancellationToken),
            SystemCatalogType.LanguageLevel => GetResponseByIdAsync<LanguageLevelCatalogItem>(catalogType, id, cancellationToken),
            SystemCatalogType.TrainingType => GetResponseByIdAsync<TrainingTypeCatalogItem>(catalogType, id, cancellationToken),
            SystemCatalogType.DurationUnit => GetResponseByIdAsync<DurationUnitCatalogItem>(catalogType, id, cancellationToken),
            SystemCatalogType.ReferenceType => GetResponseByIdAsync<ReferenceTypeCatalogItem>(catalogType, id, cancellationToken),
            SystemCatalogType.Currency => GetResponseByIdAsync<CurrencyCatalogItem>(catalogType, id, cancellationToken),
            SystemCatalogType.IdentificationType => GetResponseByIdAsync<IdentificationTypeCatalogItem>(catalogType, id, cancellationToken),
            SystemCatalogType.Profession => GetResponseByIdAsync<ProfessionCatalogItem>(catalogType, id, cancellationToken),
            SystemCatalogType.MaritalStatus => GetResponseByIdAsync<MaritalStatusCatalogItem>(catalogType, id, cancellationToken),
            SystemCatalogType.Kinship => GetResponseByIdAsync<KinshipCatalogItem>(catalogType, id, cancellationToken),
            SystemCatalogType.Department => GetResponseByIdAsync<DepartmentCatalogItem>(catalogType, id, cancellationToken),
            _ => throw new ArgumentOutOfRangeException(nameof(catalogType), catalogType, "Unsupported system catalog type.")
        };

    public Task<DepartmentCatalogLookup?> GetDepartmentLookupByIdAsync(
        Guid id,
        CancellationToken cancellationToken) =>
        dbContext.DepartmentCatalogItems
            .AsNoTracking()
            .Where(item => item.PublicId == id)
            .Select(item => new DepartmentCatalogLookup(
                item.Id,
                item.PublicId,
                item.CountryCatalogItemId,
                item.CountryCode,
                item.Code,
                item.Name,
                item.IsActive))
            .SingleOrDefaultAsync(cancellationToken);

    private Task<CountryScopedCatalogItem?> GetByIdAsync<TCatalogItem>(Guid id, CancellationToken cancellationToken)
        where TCatalogItem : CountryScopedCatalogItem =>
        dbContext.Set<TCatalogItem>()
            .SingleOrDefaultAsync(item => item.PublicId == id, cancellationToken)
            .ContinueWith(task => (CountryScopedCatalogItem?)task.Result, cancellationToken);

    private Task<bool> ExistsByCodeAsync<TCatalogItem>(
        long countryCatalogItemId,
        string normalizedCode,
        long? excludingId,
        CancellationToken cancellationToken)
        where TCatalogItem : CountryScopedCatalogItem =>
        dbContext.Set<TCatalogItem>()
            .AsNoTracking()
            .AnyAsync(item =>
                item.CountryCatalogItemId == countryCatalogItemId &&
                item.NormalizedCode == normalizedCode &&
                (!excludingId.HasValue || item.Id != excludingId.Value),
                cancellationToken);

    private async Task<PagedResponse<SystemCatalogItemResponse>> SearchAsync<TCatalogItem>(
        SystemCatalogType catalogType,
        long countryCatalogItemId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
        where TCatalogItem : CountryScopedCatalogItem
    {
        var query = dbContext.Set<TCatalogItem>()
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
                item.NormalizedName.Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new SystemCatalogItemResponse(
                item.PublicId,
                catalogType,
                item.CountryCode,
                item.Code,
                item.Name,
                item.IsActive,
                item.SortOrder,
                item.ConcurrencyToken,
                item.CreatedUtc,
                item.ModifiedUtc,
                null,
                null,
                null))
            .ToListAsync(cancellationToken);

        return new PagedResponse<SystemCatalogItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    private async Task<PagedResponse<SystemCatalogItemResponse>> SearchMunicipalitiesAsync(
        SystemCatalogType catalogType,
        long countryCatalogItemId,
        bool? isActive,
        string? search,
        Guid? parentPublicId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.MunicipalityCatalogItems
            .AsNoTracking()
            .Include(item => item.DepartmentCatalogItem)
            .Where(item => item.CountryCatalogItemId == countryCatalogItemId);

        if (isActive.HasValue)
        {
            query = query.Where(item => item.IsActive == isActive.Value);
        }

        if (parentPublicId.HasValue)
        {
            query = query.Where(item => item.DepartmentCatalogItem != null && item.DepartmentCatalogItem.PublicId == parentPublicId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(item =>
                item.NormalizedCode.Contains(normalizedSearch) ||
                item.NormalizedName.Contains(normalizedSearch) ||
                (item.DepartmentCatalogItem != null && item.DepartmentCatalogItem.NormalizedCode.Contains(normalizedSearch)) ||
                (item.DepartmentCatalogItem != null && item.DepartmentCatalogItem.NormalizedName.Contains(normalizedSearch)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new SystemCatalogItemResponse(
                item.PublicId,
                catalogType,
                item.CountryCode,
                item.Code,
                item.Name,
                item.IsActive,
                item.SortOrder,
                item.ConcurrencyToken,
                item.CreatedUtc,
                item.ModifiedUtc,
                item.DepartmentCatalogItem!.PublicId,
                item.DepartmentCatalogItem!.Code,
                item.DepartmentCatalogItem!.Name))
            .ToListAsync(cancellationToken);

        return new PagedResponse<SystemCatalogItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    private Task<SystemCatalogItemResponse?> GetResponseByIdAsync<TCatalogItem>(
        SystemCatalogType catalogType,
        Guid id,
        CancellationToken cancellationToken)
        where TCatalogItem : CountryScopedCatalogItem =>
        dbContext.Set<TCatalogItem>()
            .AsNoTracking()
            .Where(item => item.PublicId == id)
            .Select(item => new SystemCatalogItemResponse(
                item.PublicId,
                catalogType,
                item.CountryCode,
                item.Code,
                item.Name,
                item.IsActive,
                item.SortOrder,
                item.ConcurrencyToken,
                item.CreatedUtc,
                item.ModifiedUtc,
                null,
                null,
                null))
            .SingleOrDefaultAsync(cancellationToken);

    private Task<SystemCatalogItemResponse?> GetMunicipalityResponseByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.MunicipalityCatalogItems
            .AsNoTracking()
            .Include(item => item.DepartmentCatalogItem)
            .Where(item => item.PublicId == id)
            .Select(item => new SystemCatalogItemResponse(
                item.PublicId,
                SystemCatalogType.Municipality,
                item.CountryCode,
                item.Code,
                item.Name,
                item.IsActive,
                item.SortOrder,
                item.ConcurrencyToken,
                item.CreatedUtc,
                item.ModifiedUtc,
                item.DepartmentCatalogItem!.PublicId,
                item.DepartmentCatalogItem!.Code,
                item.DepartmentCatalogItem!.Name))
            .SingleOrDefaultAsync(cancellationToken);
}

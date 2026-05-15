using System.Collections.Concurrent;
using CLARIHR.Application.Abstractions.PositionDescriptionCatalogs;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PositionDescriptionCatalogs;
using CLARIHR.Domain.PositionDescriptionCatalogs;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CLARIHR.Infrastructure.PositionDescriptionCatalogs;

internal sealed class PositionDescriptionCatalogRepository(
    ApplicationDbContext dbContext,
    IMemoryCache memoryCache)
    : IPositionDescriptionCatalogRepository
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(3);
    private static readonly ConcurrentDictionary<string, byte> CacheKeys = new(StringComparer.Ordinal);

    public void AddCatalogItem(PositionDescriptionCatalogItem item) => dbContext.PositionDescriptionCatalogItems.Add(item);

    public void AddClassification(PositionCategoryClassification classification) => dbContext.PositionCategoryClassifications.Add(classification);

    public void AddCategory(PositionCategory category) => dbContext.PositionCategories.Add(category);

    public Task<PositionDescriptionCatalogItem?> GetCatalogItemByIdAsync(Guid itemId, CancellationToken cancellationToken) =>
        dbContext.PositionDescriptionCatalogItems.SingleOrDefaultAsync(item => item.PublicId == itemId, cancellationToken);

    public Task<PositionCategoryClassification?> GetClassificationByIdAsync(Guid classificationId, CancellationToken cancellationToken) =>
        dbContext.PositionCategoryClassifications.SingleOrDefaultAsync(item => item.PublicId == classificationId, cancellationToken);

    public Task<PositionCategory?> GetCategoryByIdAsync(Guid categoryId, CancellationToken cancellationToken) =>
        dbContext.PositionCategories.SingleOrDefaultAsync(item => item.PublicId == categoryId, cancellationToken);

    public Task<bool> ExistsCatalogItemOutsideTenantAsync(Guid itemId, CancellationToken cancellationToken) =>
        dbContext.PositionDescriptionCatalogItems
            // Intentional tenant filter bypass: checks cross-tenant existence only for tenant-mismatch errors.
            .IgnoreQueryFilters()
            .AnyAsync(item => item.PublicId == itemId, cancellationToken);

    public Task<bool> ExistsClassificationOutsideTenantAsync(Guid classificationId, CancellationToken cancellationToken) =>
        dbContext.PositionCategoryClassifications
            // Intentional tenant filter bypass: checks cross-tenant existence only for tenant-mismatch errors.
            .IgnoreQueryFilters()
            .AnyAsync(item => item.PublicId == classificationId, cancellationToken);

    public Task<bool> ExistsCategoryOutsideTenantAsync(Guid categoryId, CancellationToken cancellationToken) =>
        dbContext.PositionCategories
            // Intentional tenant filter bypass: checks cross-tenant existence only for tenant-mismatch errors.
            .IgnoreQueryFilters()
            .AnyAsync(item => item.PublicId == categoryId, cancellationToken);

    public Task<bool> CatalogItemCodeExistsAsync(
        Guid tenantId,
        PositionDescriptionCatalogType catalogType,
        string normalizedCode,
        long? excludingId,
        CancellationToken cancellationToken) =>
        dbContext.PositionDescriptionCatalogItems.AnyAsync(
            item => item.TenantId == tenantId &&
                    item.CatalogType == catalogType &&
                    item.NormalizedCode == normalizedCode &&
                    (!excludingId.HasValue || item.Id != excludingId.Value),
            cancellationToken);

    public Task<bool> ClassificationCodeExistsAsync(
        Guid tenantId,
        string normalizedCode,
        long? excludingId,
        CancellationToken cancellationToken) =>
        dbContext.PositionCategoryClassifications.AnyAsync(
            item => item.TenantId == tenantId &&
                    item.NormalizedCode == normalizedCode &&
                    (!excludingId.HasValue || item.Id != excludingId.Value),
            cancellationToken);

    public Task<bool> CategoryCodeExistsAsync(
        Guid tenantId,
        string normalizedCode,
        long? excludingId,
        CancellationToken cancellationToken) =>
        dbContext.PositionCategories.AnyAsync(
            item => item.TenantId == tenantId &&
                    item.NormalizedCode == normalizedCode &&
                    (!excludingId.HasValue || item.Id != excludingId.Value),
            cancellationToken);

    public Task<bool> ClassificationAxesExistsAsync(
        Guid tenantId,
        long positionFunctionCatalogItemId,
        long positionContractCatalogItemId,
        long orgUnitTypeCatalogItemId,
        long? excludingId,
        CancellationToken cancellationToken) =>
        dbContext.PositionCategoryClassifications.AnyAsync(
            item => item.TenantId == tenantId &&
                    item.PositionFunctionCatalogItemId == positionFunctionCatalogItemId &&
                    item.PositionContractCatalogItemId == positionContractCatalogItemId &&
                    item.OrgUnitTypeCatalogItemId == orgUnitTypeCatalogItemId &&
                    (!excludingId.HasValue || item.Id != excludingId.Value),
            cancellationToken);

    public async Task<PagedResponse<PositionDescriptionCatalogItemResponse>> SearchCatalogItemsAsync(
        Guid tenantId,
        PositionDescriptionCatalogType catalogType,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var cacheKey = BuildSimpleCatalogCacheKey(tenantId, catalogType, isActive, search, pageNumber, pageSize);
        if (memoryCache.TryGetValue(cacheKey, out PagedResponse<PositionDescriptionCatalogItemResponse>? cached) && cached is not null)
        {
            return cached;
        }

        var query = dbContext.PositionDescriptionCatalogItems
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.CatalogType == catalogType);

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
            .Select(item => new PositionDescriptionCatalogItemResponse(
                item.PublicId,
                item.CatalogType,
                item.Code,
                item.Name,
                item.Description,
                item.SortOrder,
                item.IsActive,
                item.ConcurrencyToken,
                item.CreatedUtc,
                item.ModifiedUtc))
            .ToListAsync(cancellationToken);

        var payload = new PagedResponse<PositionDescriptionCatalogItemResponse>(items, pageNumber, pageSize, totalCount);
        memoryCache.Set(cacheKey, payload, CacheTtl);
        _ = CacheKeys.TryAdd(cacheKey, 0);
        return payload;
    }

    public async Task<PagedResponse<PositionCategoryClassificationResponse>> SearchClassificationsAsync(
        Guid tenantId,
        Guid? positionFunctionTypeId,
        Guid? positionContractTypeId,
        Guid? orgUnitTypeId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var cacheKey = BuildClassificationCacheKey(
            tenantId,
            positionFunctionTypeId,
            positionContractTypeId,
            orgUnitTypeId,
            isActive,
            search,
            pageNumber,
            pageSize);
        if (memoryCache.TryGetValue(cacheKey, out PagedResponse<PositionCategoryClassificationResponse>? cached) && cached is not null)
        {
            return cached;
        }

        var query =
            from classification in dbContext.PositionCategoryClassifications.AsNoTracking()
            join positionFunctionType in dbContext.PositionDescriptionCatalogItems.AsNoTracking()
                on classification.PositionFunctionCatalogItemId equals positionFunctionType.Id
            join positionContractType in dbContext.PositionDescriptionCatalogItems.AsNoTracking()
                on classification.PositionContractCatalogItemId equals positionContractType.Id
            join orgUnitType in dbContext.OrgUnitTypeCatalogItems.AsNoTracking()
                on classification.OrgUnitTypeCatalogItemId equals orgUnitType.Id
            where classification.TenantId == tenantId
            select new
            {
                Classification = classification,
                PositionFunctionType = positionFunctionType,
                PositionContractType = positionContractType,
                OrgUnitType = orgUnitType
            };

        if (positionFunctionTypeId.HasValue)
        {
            query = query.Where(item => item.PositionFunctionType.PublicId == positionFunctionTypeId.Value);
        }

        if (positionContractTypeId.HasValue)
        {
            query = query.Where(item => item.PositionContractType.PublicId == positionContractTypeId.Value);
        }

        if (orgUnitTypeId.HasValue)
        {
            query = query.Where(item => item.OrgUnitType.PublicId == orgUnitTypeId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(item => item.Classification.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(item =>
                item.Classification.NormalizedCode.Contains(normalizedSearch) ||
                item.Classification.NormalizedName.Contains(normalizedSearch) ||
                item.PositionFunctionType.NormalizedName.Contains(normalizedSearch) ||
                item.PositionContractType.NormalizedName.Contains(normalizedSearch) ||
                item.OrgUnitType.NormalizedName.Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.Classification.SortOrder)
            .ThenBy(item => item.Classification.Name)
            .ThenBy(item => item.Classification.Code)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new PositionCategoryClassificationResponse(
                item.Classification.PublicId,
                item.Classification.Code,
                item.Classification.Name,
                item.Classification.Description,
                new CatalogReferenceResponse(
                    item.PositionFunctionType.PublicId,
                    item.PositionFunctionType.Code,
                    item.PositionFunctionType.Name,
                    item.PositionFunctionType.IsActive),
                new CatalogReferenceResponse(
                    item.PositionContractType.PublicId,
                    item.PositionContractType.Code,
                    item.PositionContractType.Name,
                    item.PositionContractType.IsActive),
                new CatalogReferenceResponse(
                    item.OrgUnitType.PublicId,
                    item.OrgUnitType.Code,
                    item.OrgUnitType.Name,
                    item.OrgUnitType.IsActive),
                item.Classification.SortOrder,
                item.Classification.IsActive,
                item.Classification.ConcurrencyToken,
                item.Classification.CreatedUtc,
                item.Classification.ModifiedUtc))
            .ToListAsync(cancellationToken);

        var payload = new PagedResponse<PositionCategoryClassificationResponse>(items, pageNumber, pageSize, totalCount);
        memoryCache.Set(cacheKey, payload, CacheTtl);
        _ = CacheKeys.TryAdd(cacheKey, 0);
        return payload;
    }

    public async Task<PagedResponse<PositionCategoryResponse>> SearchCategoriesAsync(
        Guid tenantId,
        Guid? classificationId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var cacheKey = BuildCategoryCacheKey(tenantId, classificationId, isActive, search, pageNumber, pageSize);
        if (memoryCache.TryGetValue(cacheKey, out PagedResponse<PositionCategoryResponse>? cached) && cached is not null)
        {
            return cached;
        }

        var query =
            from category in dbContext.PositionCategories.AsNoTracking()
            join classification in dbContext.PositionCategoryClassifications.AsNoTracking()
                on category.PositionCategoryClassificationId equals classification.Id
            where category.TenantId == tenantId
            select new
            {
                Category = category,
                Classification = classification
            };

        if (classificationId.HasValue)
        {
            query = query.Where(item => item.Classification.PublicId == classificationId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(item => item.Category.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(item =>
                item.Category.NormalizedCode.Contains(normalizedSearch) ||
                item.Category.NormalizedName.Contains(normalizedSearch) ||
                item.Classification.NormalizedCode.Contains(normalizedSearch) ||
                item.Classification.NormalizedName.Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.Category.SortOrder)
            .ThenBy(item => item.Category.Name)
            .ThenBy(item => item.Category.Code)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new PositionCategoryResponse(
                item.Category.PublicId,
                item.Category.Code,
                item.Category.Name,
                item.Category.Description,
                new CatalogReferenceResponse(
                    item.Classification.PublicId,
                    item.Classification.Code,
                    item.Classification.Name,
                    item.Classification.IsActive),
                item.Category.SortOrder,
                item.Category.IsActive,
                item.Category.ConcurrencyToken,
                item.Category.CreatedUtc,
                item.Category.ModifiedUtc))
            .ToListAsync(cancellationToken);

        var payload = new PagedResponse<PositionCategoryResponse>(items, pageNumber, pageSize, totalCount);
        memoryCache.Set(cacheKey, payload, CacheTtl);
        _ = CacheKeys.TryAdd(cacheKey, 0);
        return payload;
    }

    public Task<PositionDescriptionCatalogItemResponse?> GetCatalogItemResponseByIdAsync(Guid itemId, CancellationToken cancellationToken) =>
        dbContext.PositionDescriptionCatalogItems
            .AsNoTracking()
            .Where(item => item.PublicId == itemId)
            .Select(item => new PositionDescriptionCatalogItemResponse(
                item.PublicId,
                item.CatalogType,
                item.Code,
                item.Name,
                item.Description,
                item.SortOrder,
                item.IsActive,
                item.ConcurrencyToken,
                item.CreatedUtc,
                item.ModifiedUtc))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<PositionCategoryClassificationResponse?> GetClassificationResponseByIdAsync(Guid classificationId, CancellationToken cancellationToken) =>
        (from classification in dbContext.PositionCategoryClassifications.AsNoTracking()
         join positionFunctionType in dbContext.PositionDescriptionCatalogItems.AsNoTracking()
             on classification.PositionFunctionCatalogItemId equals positionFunctionType.Id
         join positionContractType in dbContext.PositionDescriptionCatalogItems.AsNoTracking()
             on classification.PositionContractCatalogItemId equals positionContractType.Id
         join orgUnitType in dbContext.OrgUnitTypeCatalogItems.AsNoTracking()
             on classification.OrgUnitTypeCatalogItemId equals orgUnitType.Id
         where classification.PublicId == classificationId
         select new PositionCategoryClassificationResponse(
             classification.PublicId,
             classification.Code,
             classification.Name,
             classification.Description,
             new CatalogReferenceResponse(
                 positionFunctionType.PublicId,
                 positionFunctionType.Code,
                 positionFunctionType.Name,
                 positionFunctionType.IsActive),
             new CatalogReferenceResponse(
                 positionContractType.PublicId,
                 positionContractType.Code,
                 positionContractType.Name,
                 positionContractType.IsActive),
             new CatalogReferenceResponse(
                 orgUnitType.PublicId,
                 orgUnitType.Code,
                 orgUnitType.Name,
                 orgUnitType.IsActive),
             classification.SortOrder,
             classification.IsActive,
             classification.ConcurrencyToken,
             classification.CreatedUtc,
             classification.ModifiedUtc))
        .SingleOrDefaultAsync(cancellationToken);

    public Task<PositionCategoryResponse?> GetCategoryResponseByIdAsync(Guid categoryId, CancellationToken cancellationToken) =>
        (from category in dbContext.PositionCategories.AsNoTracking()
         join classification in dbContext.PositionCategoryClassifications.AsNoTracking()
             on category.PositionCategoryClassificationId equals classification.Id
         where category.PublicId == categoryId
         select new PositionCategoryResponse(
             category.PublicId,
             category.Code,
             category.Name,
             category.Description,
             new CatalogReferenceResponse(
                 classification.PublicId,
                 classification.Code,
                 classification.Name,
                 classification.IsActive),
             category.SortOrder,
             category.IsActive,
             category.ConcurrencyToken,
             category.CreatedUtc,
             category.ModifiedUtc))
        .SingleOrDefaultAsync(cancellationToken);

    public Task<CatalogReferenceInternal?> GetActiveCatalogReferenceAsync(
        Guid tenantId,
        PositionDescriptionCatalogType catalogType,
        Guid catalogItemId,
        CancellationToken cancellationToken) =>
        dbContext.PositionDescriptionCatalogItems
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.CatalogType == catalogType && item.PublicId == catalogItemId && item.IsActive)
            .Select(item => new CatalogReferenceInternal(item.Id, item.PublicId, item.Code, item.Name, item.IsActive))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<CatalogReferenceInternal?> GetActiveOrgUnitTypeReferenceAsync(
        Guid tenantId,
        Guid orgUnitTypeId,
        CancellationToken cancellationToken) =>
        dbContext.OrgUnitTypeCatalogItems
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PublicId == orgUnitTypeId && item.IsActive)
            .Select(item => new CatalogReferenceInternal(item.Id, item.PublicId, item.Code, item.Name, item.IsActive))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> HasCategoriesUsingClassificationAsync(long classificationId, CancellationToken cancellationToken) =>
        dbContext.PositionCategories.AnyAsync(item => item.PositionCategoryClassificationId == classificationId && item.IsActive, cancellationToken);

    public Task<bool> HasJobProfilesUsingCategoryAsync(long categoryId, CancellationToken cancellationToken) =>
        dbContext.JobProfiles.AnyAsync(item => item.PositionCategoryId == categoryId, cancellationToken);

    public Task<bool> HasClassificationsUsingOrgUnitTypeAsync(long orgUnitTypeCatalogItemId, CancellationToken cancellationToken) =>
        dbContext.PositionCategoryClassifications.AnyAsync(item => item.OrgUnitTypeCatalogItemId == orgUnitTypeCatalogItemId, cancellationToken);

    public Task<bool> HasJobProfilesUsingCatalogItemAsync(long catalogItemId, CancellationToken cancellationToken) =>
        dbContext.JobProfiles.AnyAsync(item =>
            item.StrategicObjectiveCatalogItemId == catalogItemId ||
            item.AssignedWorkEquipmentCatalogItemId == catalogItemId ||
            item.ResponsibilityCatalogItemId == catalogItemId,
            cancellationToken);

    public Task<bool> HasClassificationsUsingCatalogItemAsync(long catalogItemId, CancellationToken cancellationToken) =>
        dbContext.PositionCategoryClassifications.AnyAsync(item =>
            item.PositionFunctionCatalogItemId == catalogItemId || item.PositionContractCatalogItemId == catalogItemId,
            cancellationToken);

    public Task<bool> HasFunctionsUsingFrequencyAsync(long frequencyCatalogItemId, CancellationToken cancellationToken) =>
        dbContext.JobProfileFunctions.AnyAsync(item => item.FrequencyCatalogItemId == frequencyCatalogItemId, cancellationToken);

    public Task<bool> HasRequirementsUsingRequirementTypeAsync(long requirementTypeCatalogItemId, CancellationToken cancellationToken) =>
        dbContext.JobProfileRequirements.AnyAsync(item => item.RequirementTypeCatalogItemId == requirementTypeCatalogItemId, cancellationToken);

    public Task<bool> HasWorkConditionsUsingWorkConditionTypeAsync(long workConditionTypeCatalogItemId, CancellationToken cancellationToken) =>
        dbContext.JobProfileWorkingConditions.AnyAsync(item => item.WorkConditionTypeCatalogItemId == workConditionTypeCatalogItemId, cancellationToken);

    public Task<long?> ResolvePositionCategoryIdAsync(Guid tenantId, Guid positionCategoryId, CancellationToken cancellationToken) =>
        dbContext.PositionCategories
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PublicId == positionCategoryId && item.IsActive)
            .Select(item => (long?)item.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<PositionSlotContractTypeLookup?> GetPositionSlotContractTypeLookupAsync(Guid tenantId, Guid positionSlotId, CancellationToken cancellationToken) =>
        (from slot in dbContext.PositionSlots.AsNoTracking()
         join profile in dbContext.JobProfiles.AsNoTracking() on slot.JobProfileId equals profile.Id
         join category in dbContext.PositionCategories.AsNoTracking() on profile.PositionCategoryId equals category.Id into categoryGroup
         from category in categoryGroup.DefaultIfEmpty()
         join classification in dbContext.PositionCategoryClassifications.AsNoTracking() on category.PositionCategoryClassificationId equals classification.Id into classificationGroup
         from classification in classificationGroup.DefaultIfEmpty()
         join contractType in dbContext.PositionDescriptionCatalogItems.AsNoTracking() on classification.PositionContractCatalogItemId equals contractType.Id into contractTypeGroup
         from contractType in contractTypeGroup.DefaultIfEmpty()
         where slot.TenantId == tenantId && slot.PublicId == positionSlotId
         select new PositionSlotContractTypeLookup(
             slot.PublicId,
             profile.PublicId,
             category != null ? category.PublicId : null,
             classification != null ? classification.PublicId : null,
             contractType != null ? contractType.PublicId : null,
             contractType != null ? contractType.Code : null,
             contractType != null ? contractType.Name : null))
        .SingleOrDefaultAsync(cancellationToken);

    public Task<string?> ResolveSalaryClassCodeByCatalogIdAsync(Guid tenantId, Guid salaryClassId, CancellationToken cancellationToken) =>
        dbContext.PositionDescriptionCatalogItems
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId &&
                           item.CatalogType == PositionDescriptionCatalogType.SalaryClass &&
                           item.PublicId == salaryClassId &&
                           item.IsActive)
            .Select(item => item.Code)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<Guid?> ResolveSalaryClassCatalogIdByCodeAsync(Guid tenantId, string salaryClassCode, CancellationToken cancellationToken)
    {
        var normalizedCode = salaryClassCode.Trim().ToUpperInvariant();
        return dbContext.PositionDescriptionCatalogItems
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId &&
                           item.CatalogType == PositionDescriptionCatalogType.SalaryClass &&
                           item.NormalizedCode == normalizedCode &&
                           item.IsActive)
            .Select(item => (Guid?)item.PublicId)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public void InvalidateSimpleCatalogCache(Guid tenantId, PositionDescriptionCatalogType catalogType) =>
        RemoveKeysByPrefix(BuildSimpleCatalogCachePrefix(tenantId, catalogType));

    public void InvalidateClassificationCache(Guid tenantId) =>
        RemoveKeysByPrefix(BuildClassificationCachePrefix(tenantId));

    public void InvalidateCategoryCache(Guid tenantId) =>
        RemoveKeysByPrefix(BuildCategoryCachePrefix(tenantId));

    private void RemoveKeysByPrefix(string prefix)
    {
        var keys = CacheKeys.Keys
            .Where(key => key.StartsWith(prefix, StringComparison.Ordinal))
            .ToArray();

        foreach (var key in keys)
        {
            memoryCache.Remove(key);
            _ = CacheKeys.TryRemove(key, out _);
        }
    }

    private static string BuildSimpleCatalogCacheKey(
        Guid tenantId,
        PositionDescriptionCatalogType catalogType,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize) =>
        $"{BuildSimpleCatalogCachePrefix(tenantId, catalogType)}:{isActive?.ToString() ?? "null"}:{(search ?? string.Empty).Trim().ToUpperInvariant()}:{pageNumber}:{pageSize}";

    private static string BuildClassificationCacheKey(
        Guid tenantId,
        Guid? positionFunctionTypeId,
        Guid? positionContractTypeId,
        Guid? orgUnitTypeId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize) =>
        $"{BuildClassificationCachePrefix(tenantId)}:{positionFunctionTypeId?.ToString("D") ?? "null"}:{positionContractTypeId?.ToString("D") ?? "null"}:{orgUnitTypeId?.ToString("D") ?? "null"}:{isActive?.ToString() ?? "null"}:{(search ?? string.Empty).Trim().ToUpperInvariant()}:{pageNumber}:{pageSize}";

    private static string BuildCategoryCacheKey(
        Guid tenantId,
        Guid? classificationId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize) =>
        $"{BuildCategoryCachePrefix(tenantId)}:{classificationId?.ToString("D") ?? "null"}:{isActive?.ToString() ?? "null"}:{(search ?? string.Empty).Trim().ToUpperInvariant()}:{pageNumber}:{pageSize}";

    private static string BuildSimpleCatalogCachePrefix(Guid tenantId, PositionDescriptionCatalogType catalogType) =>
        $"position-description-catalog:{tenantId:D}:{catalogType}";

    private static string BuildClassificationCachePrefix(Guid tenantId) =>
        $"position-category-classification:{tenantId:D}";

    private static string BuildCategoryCachePrefix(Guid tenantId) =>
        $"position-category:{tenantId:D}";
}

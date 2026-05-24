using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PositionDescriptionCatalogs;
using CLARIHR.Domain.PositionDescriptionCatalogs;

namespace CLARIHR.Application.Abstractions.PositionDescriptionCatalogs;

// §X-ISP: extends the narrow IPositionCatalogLookup role (GetActiveCatalogReference,
// Exists*OutsideTenant, ResolvePositionCategoryId, ResolveSalaryClassCodeByCatalogId)
// so cross-context consumers depend on that role, not on this full surface.
public interface IPositionDescriptionCatalogRepository : IPositionCatalogLookup
{
    void AddCatalogItem(PositionDescriptionCatalogItem item);

    void AddClassification(PositionCategoryClassification classification);

    void AddCategory(PositionCategory category);

    Task<PositionDescriptionCatalogItem?> GetCatalogItemByIdAsync(Guid itemId, CancellationToken cancellationToken);

    Task<PositionCategoryClassification?> GetClassificationByIdAsync(Guid classificationId, CancellationToken cancellationToken);

    Task<PositionCategory?> GetCategoryByIdAsync(Guid categoryId, CancellationToken cancellationToken);

    Task<bool> ExistsClassificationOutsideTenantAsync(Guid classificationId, CancellationToken cancellationToken);

    Task<bool> CatalogItemCodeExistsAsync(
        Guid tenantId,
        PositionDescriptionCatalogType catalogType,
        string normalizedCode,
        long? excludingId,
        CancellationToken cancellationToken);

    Task<bool> ClassificationCodeExistsAsync(
        Guid tenantId,
        string normalizedCode,
        long? excludingId,
        CancellationToken cancellationToken);

    Task<bool> CategoryCodeExistsAsync(
        Guid tenantId,
        string normalizedCode,
        long? excludingId,
        CancellationToken cancellationToken);

    Task<bool> ClassificationAxesExistsAsync(
        Guid tenantId,
        long positionFunctionCatalogItemId,
        long positionContractCatalogItemId,
        long orgUnitTypeCatalogItemId,
        long? excludingId,
        CancellationToken cancellationToken);

    Task<PagedResponse<PositionDescriptionCatalogItemResponse>> SearchCatalogItemsAsync(
        Guid tenantId,
        PositionDescriptionCatalogType catalogType,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<PagedResponse<PositionCategoryClassificationResponse>> SearchClassificationsAsync(
        Guid tenantId,
        Guid? positionFunctionTypeId,
        Guid? positionContractTypeId,
        Guid? orgUnitTypeId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<PagedResponse<PositionCategoryResponse>> SearchCategoriesAsync(
        Guid tenantId,
        Guid? classificationId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<PositionDescriptionCatalogItemResponse?> GetCatalogItemResponseByIdAsync(Guid itemId, CancellationToken cancellationToken);

    Task<PositionCategoryClassificationResponse?> GetClassificationResponseByIdAsync(Guid classificationId, CancellationToken cancellationToken);

    Task<PositionCategoryResponse?> GetCategoryResponseByIdAsync(Guid categoryId, CancellationToken cancellationToken);

    Task<CatalogReferenceInternal?> GetActiveOrgUnitTypeReferenceAsync(
        Guid tenantId,
        Guid orgUnitTypeId,
        CancellationToken cancellationToken);

    Task<bool> HasCategoriesUsingClassificationAsync(long classificationId, CancellationToken cancellationToken);

    Task<bool> HasJobProfilesUsingCategoryAsync(long categoryId, CancellationToken cancellationToken);

    Task<bool> HasJobProfilesUsingCatalogItemAsync(long catalogItemId, CancellationToken cancellationToken);

    Task<bool> HasClassificationsUsingCatalogItemAsync(long catalogItemId, CancellationToken cancellationToken);

    Task<bool> HasFunctionsUsingFrequencyAsync(long frequencyCatalogItemId, CancellationToken cancellationToken);

    Task<bool> HasRequirementsUsingRequirementTypeAsync(long requirementTypeCatalogItemId, CancellationToken cancellationToken);

    Task<bool> HasWorkConditionsUsingWorkConditionTypeAsync(long workConditionTypeCatalogItemId, CancellationToken cancellationToken);

    void InvalidateSimpleCatalogCache(Guid tenantId, PositionDescriptionCatalogType catalogType);

    void InvalidateClassificationCache(Guid tenantId);

    void InvalidateCategoryCache(Guid tenantId);
}

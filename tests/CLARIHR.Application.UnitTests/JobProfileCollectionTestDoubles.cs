using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.JobProfiles;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PositionDescriptionCatalogs;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Application.Features.PositionDescriptionCatalogs;
using CLARIHR.Application.Features.PositionDescriptionCatalogs.Common;
using CLARIHR.Domain.JobProfiles;
using CLARIHR.Domain.PositionDescriptionCatalogs;

namespace CLARIHR.Application.UnitTests;

internal sealed class TestJobProfileRepository : IJobProfileRepository
{
    public Dictionary<Guid, JobProfile> Profiles { get; } = new();
    public Dictionary<Guid, JobProfileResponse> Responses { get; } = new();

    public void Add(JobProfile profile) => Profiles[profile.PublicId] = profile;

    public Task<JobProfile?> GetByIdAsync(Guid profileId, CancellationToken cancellationToken) =>
        Task.FromResult(Profiles.GetValueOrDefault(profileId));

    public Task<bool> ExistsOutsideTenantAsync(Guid profileId, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public Task<bool> CodeExistsAsync(Guid tenantId, string normalizedCode, long? excludingProfileId, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public Task<long?> ResolveOrgUnitIdAsync(Guid tenantId, Guid orgUnitId, CancellationToken cancellationToken) =>
        Task.FromResult((long?)1);

    public Task<bool> OrgUnitExistsOutsideTenantAsync(Guid orgUnitId, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public Task<long?> ResolveProfileIdAsync(Guid tenantId, Guid profileId, CancellationToken cancellationToken) =>
        Task.FromResult((long?)1);

    public Task<bool> ProfileExistsInTenantAsync(Guid tenantId, long profileId, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    public Task<IReadOnlyList<JobProfileDependencyNodeData>> GetDependencyGraphAsync(Guid tenantId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<JobProfileDependencyNodeData>>([]);

    public Task<PagedResponse<JobProfileListItemResponse>> SearchAsync(Guid tenantId, JobProfileStatus? status, Guid? orgUnitId, Guid? salaryClassId, string? search, int pageNumber, int pageSize, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<JobProfileResponse?> GetResponseByIdAsync(Guid profileId, CancellationToken cancellationToken) =>
        Task.FromResult(Responses.GetValueOrDefault(profileId));

    public Task<JobProfileVacancyTemplateResponse?> GetVacancyTemplateByIdAsync(Guid profileId, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<JobProfilePrintResponse?> GetPrintByIdAsync(Guid profileId, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}

internal sealed class TestJobCatalogRepository : IJobCatalogRepository
{
    public void Add(JobCatalogItem item) { }
    public Task<JobCatalogItem?> GetByIdAsync(Guid itemId, CancellationToken cancellationToken) => Task.FromResult<JobCatalogItem?>(null);
    public Task<bool> ExistsOutsideTenantAsync(Guid itemId, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<bool> CodeExistsAsync(Guid tenantId, JobCatalogCategory category, string normalizedCode, long? excludingItemId, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<PagedResponse<JobCatalogItemResponse>> SearchAsync(Guid tenantId, JobCatalogCategory category, bool? isActive, string? search, int pageNumber, int pageSize, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<JobCatalogItemResponse?> GetResponseByIdAsync(Guid itemId, CancellationToken cancellationToken) => Task.FromResult<JobCatalogItemResponse?>(null);
    public Task<JobCatalogItem?> ResolveActiveItemAsync(Guid tenantId, JobCatalogCategory category, Guid itemId, CancellationToken cancellationToken) => Task.FromResult<JobCatalogItem?>(null);
    public Task<JobCatalogItem?> FindActiveByNameAsync(Guid tenantId, JobCatalogCategory category, string normalizedName, CancellationToken cancellationToken) => Task.FromResult<JobCatalogItem?>(null);
    public void InvalidateCategoryCache(Guid tenantId, JobCatalogCategory category) { }
}

internal sealed class TestPositionDescriptionCatalogRepository : IPositionDescriptionCatalogRepository
{
    public void AddCatalogItem(PositionDescriptionCatalogItem item) => throw new NotImplementedException();
    public void AddClassification(PositionCategoryClassification classification) => throw new NotImplementedException();
    public void AddCategory(PositionCategory category) => throw new NotImplementedException();
    public Task<PositionDescriptionCatalogItem?> GetCatalogItemByIdAsync(Guid itemId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<PositionCategoryClassification?> GetClassificationByIdAsync(Guid classificationId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<PositionCategory?> GetCategoryByIdAsync(Guid categoryId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<bool> ExistsCatalogItemOutsideTenantAsync(Guid itemId, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<bool> ExistsClassificationOutsideTenantAsync(Guid classificationId, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<bool> ExistsCategoryOutsideTenantAsync(Guid categoryId, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<bool> CatalogItemCodeExistsAsync(Guid tenantId, PositionDescriptionCatalogType catalogType, string normalizedCode, long? excludingId, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<bool> ClassificationCodeExistsAsync(Guid tenantId, string normalizedCode, long? excludingId, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<bool> CategoryCodeExistsAsync(Guid tenantId, string normalizedCode, long? excludingId, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<bool> ClassificationAxesExistsAsync(Guid tenantId, long positionFunctionCatalogItemId, long positionContractCatalogItemId, long orgUnitTypeCatalogItemId, long? excludingId, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<PagedResponse<PositionDescriptionCatalogItemResponse>> SearchCatalogItemsAsync(Guid tenantId, PositionDescriptionCatalogType catalogType, bool? isActive, string? search, int pageNumber, int pageSize, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<PagedResponse<PositionCategoryClassificationResponse>> SearchClassificationsAsync(Guid tenantId, Guid? positionFunctionTypeId, Guid? positionContractTypeId, Guid? orgUnitTypeId, bool? isActive, string? search, int pageNumber, int pageSize, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<PagedResponse<PositionCategoryResponse>> SearchCategoriesAsync(Guid tenantId, Guid? classificationId, bool? isActive, string? search, int pageNumber, int pageSize, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<PositionDescriptionCatalogItemResponse?> GetCatalogItemResponseByIdAsync(Guid itemId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<PositionCategoryClassificationResponse?> GetClassificationResponseByIdAsync(Guid classificationId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<PositionCategoryResponse?> GetCategoryResponseByIdAsync(Guid categoryId, CancellationToken cancellationToken) => throw new NotImplementedException();
    
    public Task<CatalogReferenceResponse?> GetActiveCatalogReferenceAsync(Guid tenantId, PositionDescriptionCatalogType catalogType, Guid catalogItemId, CancellationToken cancellationToken) => 
        Task.FromResult((CatalogReferenceResponse?)new CatalogReferenceResponse(1, catalogItemId, "Code", "Name", true));

    public Task<CatalogReferenceResponse?> GetActiveOrgUnitTypeReferenceAsync(Guid tenantId, Guid orgUnitTypeId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<bool> HasCategoriesUsingClassificationAsync(long classificationId, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<bool> HasJobProfilesUsingCategoryAsync(long categoryId, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<bool> HasClassificationsUsingOrgUnitTypeAsync(long orgUnitTypeCatalogItemId, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<bool> HasJobProfilesUsingCatalogItemAsync(long catalogItemId, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<bool> HasClassificationsUsingCatalogItemAsync(long catalogItemId, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<bool> HasFunctionsUsingFrequencyAsync(long frequencyCatalogItemId, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<bool> HasRequirementsUsingRequirementTypeAsync(long requirementTypeCatalogItemId, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<bool> HasWorkConditionsUsingWorkConditionTypeAsync(long workConditionTypeCatalogItemId, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<long?> ResolvePositionCategoryIdAsync(Guid tenantId, Guid positionCategoryId, CancellationToken cancellationToken) => Task.FromResult((long?)1);
    public Task<PositionSlotContractTypeLookup?> GetPositionSlotContractTypeLookupAsync(Guid tenantId, Guid positionSlotId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<string?> ResolveSalaryClassCodeByCatalogIdAsync(Guid tenantId, Guid salaryClassId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<Guid?> ResolveSalaryClassCatalogIdByCodeAsync(Guid tenantId, string salaryClassCode, CancellationToken cancellationToken) => throw new NotImplementedException();
    public void InvalidateSimpleCatalogCache(Guid tenantId, PositionDescriptionCatalogType catalogType) { }
    public void InvalidateClassificationCache(Guid tenantId) { }
    public void InvalidateCategoryCache(Guid tenantId) { }
}

internal sealed class TestJobProfileAuthorizationService : IJobProfileAuthorizationService
{
    public Task<Result> EnsureCanReadAsync(Guid companyId, CancellationToken cancellationToken) => Task.FromResult(Result.Success());
    public Task<Result> EnsureCanManageProfilesAsync(Guid companyId, CancellationToken cancellationToken) => Task.FromResult(Result.Success());
    public Task<Result> EnsureCanManageCatalogsAsync(Guid companyId, CancellationToken cancellationToken) => Task.FromResult(Result.Success());
    public Error TenantMismatch(RbacPermissionAction action) => Result.Failure(new Error("TenantMismatch", "Tenant mismatch", ErrorType.Forbidden)).Error;
}

internal sealed class TestAuditService : IAuditService
{
    public Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task LogForTenantAsync(Guid tenantId, AuditLogEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class FixedTenantContext(Guid? tenantId) : ITenantContext
{
    public Guid? TenantId { get; } = tenantId;
}

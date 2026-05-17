using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.InternalCatalogs;
using CLARIHR.Application.Abstractions.JobProfiles;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PositionDescriptionCatalogs;
using CLARIHR.Application.Abstractions.SalaryTabulator;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.InternalCatalogs;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Application.Features.PositionDescriptionCatalogs;
using CLARIHR.Application.Features.PositionDescriptionCatalogs.Common;
using CLARIHR.Application.Features.SalaryTabulator;
using CLARIHR.Domain.InternalCatalogs;
using CLARIHR.Domain.JobProfiles;
using CLARIHR.Domain.PositionDescriptionCatalogs;
using CLARIHR.Domain.SalaryTabulator;

namespace CLARIHR.Application.UnitTests;

internal sealed class TestJobProfileRepository : IJobProfileRepository
{
    public Dictionary<Guid, JobProfile> Profiles { get; } = new();
    public Dictionary<Guid, JobProfileEntityResponse> EntityResponses { get; } = new();
    public Dictionary<Guid, JobProfileResponse> Responses { get; } = new();
    public Dictionary<Guid, JobProfileCompensationResponse?> CoreCompensations { get; } = new();
    public Dictionary<long, Guid> OrgUnitPublicIds { get; } = new();
    public Dictionary<Guid, JobProfilePrintResponse> PrintResponses { get; } = new();

    public void Add(JobProfile profile) => Profiles[profile.PublicId] = profile;

    public Task<JobProfile?> GetByIdAsync(Guid profileId, CancellationToken cancellationToken) =>
        Task.FromResult(Profiles.GetValueOrDefault(profileId));

    public Task<JobProfile?> GetCoreByIdAsync(Guid profileId, CancellationToken cancellationToken) =>
        Task.FromResult(Profiles.GetValueOrDefault(profileId));

    public Task<JobProfile?> GetWithRequirementsOnlyAsync(Guid profileId, CancellationToken cancellationToken) =>
        Task.FromResult(Profiles.GetValueOrDefault(profileId));

    public Task<JobProfile?> GetWithFunctionsOnlyAsync(Guid profileId, CancellationToken cancellationToken) =>
        Task.FromResult(Profiles.GetValueOrDefault(profileId));

    public Task<JobProfile?> GetWithRelationsOnlyAsync(Guid profileId, CancellationToken cancellationToken) =>
        Task.FromResult(Profiles.GetValueOrDefault(profileId));

    public Task<JobProfile?> GetWithCompetenciesOnlyAsync(Guid profileId, CancellationToken cancellationToken) =>
        Task.FromResult(Profiles.GetValueOrDefault(profileId));

    public Task<JobProfile?> GetWithWorkingConditionsOnlyAsync(Guid profileId, CancellationToken cancellationToken) =>
        Task.FromResult(Profiles.GetValueOrDefault(profileId));

    public Task<JobProfile?> GetWithTrainingsOnlyAsync(Guid profileId, CancellationToken cancellationToken) =>
        Task.FromResult(Profiles.GetValueOrDefault(profileId));

    public Task<JobProfile?> GetWithBenefitsOnlyAsync(Guid profileId, CancellationToken cancellationToken) =>
        Task.FromResult(Profiles.GetValueOrDefault(profileId));

    public Task<JobProfile?> GetWithDependentPositionsOnlyAsync(Guid profileId, CancellationToken cancellationToken) =>
        Task.FromResult(Profiles.GetValueOrDefault(profileId));

    public Task<JobProfileCoreResponse?> GetCoreResponseByIdAsync(Guid profileId, CancellationToken cancellationToken)
    {
        if (!Profiles.TryGetValue(profileId, out var profile))
        {
            return Task.FromResult<JobProfileCoreResponse?>(null);
        }

        var orgUnitPublicId = profile.OrgUnitId > 0
            ? ResolvePublicId(OrgUnitPublicIds, profile.OrgUnitId)
            : (Guid?)null;

        return Task.FromResult<JobProfileCoreResponse?>(
            new JobProfileCoreResponse(
                profile.PublicId,
                profile.TenantId,
                profile.Code,
                profile.Title,
                profile.Status,
                profile.Version,
                profile.Objective,
                orgUnitPublicId,
                OrgUnitName: null,
                ReportsToJobProfileId: null,
                ReportsToJobProfileCode: null,
                ReportsToJobProfileTitle: null,
                PositionCategoryId: null,
                StrategicObjectiveCatalogItemId: null,
                AssignedWorkEquipmentCatalogItemId: null,
                ResponsibilityCatalogItemId: null,
                profile.DecisionScope,
                profile.AssignedResources,
                profile.Responsibilities,
                profile.BenefitsSummary,
                profile.WorkingConditionSummary,
                profile.MarketSalaryReference,
                profile.ValuationNotes,
                profile.EffectiveFromUtc,
                profile.EffectiveToUtc,
                profile.IsActive,
                CoreCompensations.GetValueOrDefault(profile.PublicId),
                profile.ConcurrencyToken,
                profile.CreatedUtc,
                profile.ModifiedUtc));
    }

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

    public Task<JobProfileReferenceResponse?> GetReferenceByIdAsync(Guid tenantId, Guid profileId, CancellationToken cancellationToken) =>
        Task.FromResult<JobProfileReferenceResponse?>(new JobProfileReferenceResponse(profileId, "JP-REF", "Referenced profile"));

    public Task<JobProfileReferenceResponse?> GetReferenceByInternalIdAsync(Guid tenantId, long profileId, CancellationToken cancellationToken) =>
        Task.FromResult<JobProfileReferenceResponse?>(new JobProfileReferenceResponse(Guid.NewGuid(), "JP-REF", "Referenced profile"));

    public Task<bool> ProfileExistsInTenantAsync(Guid tenantId, long profileId, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    public Task<IReadOnlyList<JobProfileDependencyNodeData>> GetDependencyGraphAsync(Guid tenantId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<JobProfileDependencyNodeData>>([]);

    public Task<PagedResponse<JobProfileListItemResponse>> SearchAsync(Guid tenantId, JobProfileStatus? status, Guid? orgUnitId, Guid? salaryClassId, string? search, int pageNumber, int pageSize, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<JobProfileEntityResponse?> GetEntityResponseByIdAsync(Guid profileId, CancellationToken cancellationToken) =>
        Task.FromResult(EntityResponses.GetValueOrDefault(profileId));

    public Task<JobProfileResponse?> GetResponseByIdAsync(Guid profileId, CancellationToken cancellationToken) =>
        Task.FromResult(Responses.GetValueOrDefault(profileId));

    public Task<PagedResponse<JobProfileRequirementResponse>?> GetRequirementResponsesByProfileIdAsync(
        Guid profileId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (!Profiles.TryGetValue(profileId, out var profile))
        {
            return Task.FromResult<PagedResponse<JobProfileRequirementResponse>?>(null);
        }

        var orderedItems = profile.Requirements
            .OrderBy(requirement => requirement.SortOrder)
            .ThenBy(requirement => requirement.Description)
            .Select(requirement => new JobProfileRequirementResponse(
                requirement.PublicId,
                null,
                null,
                requirement.RequirementType,
                requirement.Description,
                requirement.SortOrder,
                requirement.ConcurrencyToken))
            .ToArray();

        var pagedItems = orderedItems
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        return Task.FromResult<PagedResponse<JobProfileRequirementResponse>?>(
            new PagedResponse<JobProfileRequirementResponse>(pagedItems, pageNumber, pageSize, orderedItems.Length));
    }

    public Task<JobProfileRequirementResponse?> GetRequirementResponseAsync(Guid profileId, Guid requirementId, CancellationToken cancellationToken)
    {
        if (!Profiles.TryGetValue(profileId, out var profile))
        {
            return Task.FromResult<JobProfileRequirementResponse?>(null);
        }

        var requirement = profile.Requirements.FirstOrDefault(item => item.PublicId == requirementId);
        return Task.FromResult(requirement is null
            ? null
            : new JobProfileRequirementResponse(
                requirement.PublicId,
                null,
                null,
                requirement.RequirementType,
                requirement.Description,
                requirement.SortOrder,
                requirement.ConcurrencyToken));
    }

    public Task<PagedResponse<JobProfileFunctionResponse>?> GetFunctionResponsesByProfileIdAsync(
        Guid profileId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (!Profiles.TryGetValue(profileId, out var profile))
        {
            return Task.FromResult<PagedResponse<JobProfileFunctionResponse>?>(null);
        }

        var orderedItems = profile.Functions
            .OrderBy(function => function.SortOrder)
            .ThenBy(function => function.Description)
            .Select(function => new JobProfileFunctionResponse(
                function.PublicId,
                null,
                function.FunctionType,
                function.Description,
                function.SortOrder,
                function.ConcurrencyToken))
            .ToArray();

        var pagedItems = orderedItems
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        return Task.FromResult<PagedResponse<JobProfileFunctionResponse>?>(
            new PagedResponse<JobProfileFunctionResponse>(pagedItems, pageNumber, pageSize, orderedItems.Length));
    }

    public Task<JobProfileFunctionResponse?> GetFunctionResponseAsync(Guid profileId, Guid functionId, CancellationToken cancellationToken)
    {
        if (!Profiles.TryGetValue(profileId, out var profile))
        {
            return Task.FromResult<JobProfileFunctionResponse?>(null);
        }

        var function = profile.Functions.FirstOrDefault(item => item.PublicId == functionId);
        return Task.FromResult(function is null
            ? null
            : new JobProfileFunctionResponse(
                function.PublicId,
                null,
                function.FunctionType,
                function.Description,
                function.SortOrder,
                function.ConcurrencyToken));
    }

    public Task<PagedResponse<JobProfileRelationResponse>?> GetRelationResponsesByProfileIdAsync(
        Guid profileId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (!Profiles.TryGetValue(profileId, out var profile))
        {
            return Task.FromResult<PagedResponse<JobProfileRelationResponse>?>(null);
        }

        var orderedItems = profile.Relations
            .OrderBy(relation => relation.SortOrder)
            .ThenBy(relation => relation.Counterpart)
            .Select(relation => new JobProfileRelationResponse(
                relation.PublicId,
                null,
                relation.RelationType,
                relation.Counterpart,
                relation.Notes,
                relation.SortOrder,
                relation.ConcurrencyToken))
            .ToArray();

        var pagedItems = orderedItems
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        return Task.FromResult<PagedResponse<JobProfileRelationResponse>?>(
            new PagedResponse<JobProfileRelationResponse>(pagedItems, pageNumber, pageSize, orderedItems.Length));
    }

    public Task<JobProfileRelationResponse?> GetRelationResponseAsync(Guid profileId, Guid relationId, CancellationToken cancellationToken)
    {
        if (!Profiles.TryGetValue(profileId, out var profile))
        {
            return Task.FromResult<JobProfileRelationResponse?>(null);
        }

        var relation = profile.Relations.FirstOrDefault(item => item.PublicId == relationId);
        return Task.FromResult(relation is null
            ? null
            : new JobProfileRelationResponse(
                relation.PublicId,
                null,
                relation.RelationType,
                relation.Counterpart,
                relation.Notes,
                relation.SortOrder,
                relation.ConcurrencyToken));
    }

    public Task<PagedResponse<JobProfileLegacyCompetencyResponse>?> GetLegacyCompetencyResponsesByProfileIdAsync(
        Guid profileId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (!Profiles.TryGetValue(profileId, out var profile))
        {
            return Task.FromResult<PagedResponse<JobProfileLegacyCompetencyResponse>?>(null);
        }

        var orderedItems = profile.Competencies
            .OrderBy(competency => competency.SortOrder)
            .ThenBy(competency => competency.Name)
            .Select(competency => new JobProfileLegacyCompetencyResponse(
                competency.PublicId,
                null,
                competency.Name,
                competency.ExpectedLevel,
                competency.Notes,
                competency.SortOrder,
                competency.ConcurrencyToken))
            .ToArray();

        var pagedItems = orderedItems
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        return Task.FromResult<PagedResponse<JobProfileLegacyCompetencyResponse>?>(
            new PagedResponse<JobProfileLegacyCompetencyResponse>(pagedItems, pageNumber, pageSize, orderedItems.Length));
    }

    public Task<JobProfileLegacyCompetencyResponse?> GetLegacyCompetencyResponseAsync(Guid profileId, Guid competencyId, CancellationToken cancellationToken)
    {
        if (!Profiles.TryGetValue(profileId, out var profile))
        {
            return Task.FromResult<JobProfileLegacyCompetencyResponse?>(null);
        }

        var competency = profile.Competencies.FirstOrDefault(item => item.PublicId == competencyId);
        return Task.FromResult(competency is null
            ? null
            : new JobProfileLegacyCompetencyResponse(
                competency.PublicId,
                null,
                competency.Name,
                competency.ExpectedLevel,
                competency.Notes,
                competency.SortOrder,
                competency.ConcurrencyToken));
    }

    public Task<PagedResponse<JobProfileTrainingResponse>?> GetTrainingResponsesByProfileIdAsync(
        Guid profileId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (!Profiles.TryGetValue(profileId, out var profile))
        {
            return Task.FromResult<PagedResponse<JobProfileTrainingResponse>?>(null);
        }

        var orderedItems = profile.Trainings
            .OrderBy(training => training.SortOrder)
            .ThenBy(training => training.Name)
            .Select(training => new JobProfileTrainingResponse(
                training.PublicId,
                null,
                training.Name,
                training.Notes,
                training.SortOrder,
                training.ConcurrencyToken))
            .ToArray();

        var pagedItems = orderedItems
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        return Task.FromResult<PagedResponse<JobProfileTrainingResponse>?>(
            new PagedResponse<JobProfileTrainingResponse>(pagedItems, pageNumber, pageSize, orderedItems.Length));
    }

    public Task<JobProfileTrainingResponse?> GetTrainingResponseAsync(Guid profileId, Guid trainingId, CancellationToken cancellationToken)
    {
        if (!Profiles.TryGetValue(profileId, out var profile))
        {
            return Task.FromResult<JobProfileTrainingResponse?>(null);
        }

        var training = profile.Trainings.FirstOrDefault(item => item.PublicId == trainingId);
        return Task.FromResult(training is null
            ? null
            : new JobProfileTrainingResponse(
                training.PublicId,
                null,
                training.Name,
                training.Notes,
                training.SortOrder,
                training.ConcurrencyToken));
    }

    public Task<PagedResponse<JobProfileBenefitResponse>?> GetBenefitResponsesByProfileIdAsync(
        Guid profileId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (!Profiles.TryGetValue(profileId, out var profile))
        {
            return Task.FromResult<PagedResponse<JobProfileBenefitResponse>?>(null);
        }

        var orderedItems = profile.Benefits
            .OrderBy(benefit => benefit.SortOrder)
            .ThenBy(benefit => benefit.Name)
            .Select(benefit => new JobProfileBenefitResponse(
                benefit.PublicId,
                null,
                benefit.Name,
                benefit.Notes,
                benefit.SortOrder,
                benefit.ConcurrencyToken))
            .ToArray();

        var pagedItems = orderedItems
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        return Task.FromResult<PagedResponse<JobProfileBenefitResponse>?>(
            new PagedResponse<JobProfileBenefitResponse>(pagedItems, pageNumber, pageSize, orderedItems.Length));
    }

    public Task<JobProfileBenefitResponse?> GetBenefitResponseAsync(Guid profileId, Guid benefitId, CancellationToken cancellationToken)
    {
        if (!Profiles.TryGetValue(profileId, out var profile))
        {
            return Task.FromResult<JobProfileBenefitResponse?>(null);
        }

        var benefit = profile.Benefits.FirstOrDefault(item => item.PublicId == benefitId);
        return Task.FromResult(benefit is null
            ? null
            : new JobProfileBenefitResponse(
                benefit.PublicId,
                null,
                benefit.Name,
                benefit.Notes,
                benefit.SortOrder,
                benefit.ConcurrencyToken));
    }

    public Task<PagedResponse<JobProfileWorkingConditionResponse>?> GetWorkingConditionResponsesByProfileIdAsync(
        Guid profileId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (!Profiles.TryGetValue(profileId, out var profile))
        {
            return Task.FromResult<PagedResponse<JobProfileWorkingConditionResponse>?>(null);
        }

        var orderedItems = profile.WorkingConditions
            .OrderBy(condition => condition.SortOrder)
            .ThenBy(condition => condition.Name)
            .Select(condition => new JobProfileWorkingConditionResponse(
                condition.PublicId,
                null,
                null,
                condition.Name,
                condition.Notes,
                condition.SortOrder,
                condition.ConcurrencyToken))
            .ToArray();

        var pagedItems = orderedItems
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        return Task.FromResult<PagedResponse<JobProfileWorkingConditionResponse>?>(
            new PagedResponse<JobProfileWorkingConditionResponse>(pagedItems, pageNumber, pageSize, orderedItems.Length));
    }

    public Task<JobProfileWorkingConditionResponse?> GetWorkingConditionResponseAsync(Guid profileId, Guid workingConditionId, CancellationToken cancellationToken)
    {
        if (!Profiles.TryGetValue(profileId, out var profile))
        {
            return Task.FromResult<JobProfileWorkingConditionResponse?>(null);
        }

        var condition = profile.WorkingConditions.FirstOrDefault(item => item.PublicId == workingConditionId);
        return Task.FromResult(condition is null
            ? null
            : new JobProfileWorkingConditionResponse(
                condition.PublicId,
                null,
                null,
                condition.Name,
                condition.Notes,
                condition.SortOrder,
                condition.ConcurrencyToken));
    }

    public Task<PagedResponse<JobProfileDependentPositionResponse>?> GetDependentPositionResponsesByProfileIdAsync(
        Guid profileId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        if (!Profiles.TryGetValue(profileId, out var profile))
        {
            return Task.FromResult<PagedResponse<JobProfileDependentPositionResponse>?>(null);
        }

        var orderedItems = profile.DependentPositions
            .Select(position => new JobProfileDependentPositionResponse(
                position.PublicId,
                Guid.NewGuid(),
                "JP-REF",
                "Referenced profile",
                position.Quantity,
                position.Notes,
                position.ConcurrencyToken))
            .OrderBy(position => position.DependentJobProfileTitle)
            .ThenBy(position => position.DependentJobProfileCode)
            .ToArray();

        var pagedItems = orderedItems
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        return Task.FromResult<PagedResponse<JobProfileDependentPositionResponse>?>(
            new PagedResponse<JobProfileDependentPositionResponse>(pagedItems, pageNumber, pageSize, orderedItems.Length));
    }

    public Task<JobProfileDependentPositionResponse?> GetDependentPositionResponseAsync(Guid profileId, Guid dependentPositionId, CancellationToken cancellationToken)
    {
        if (!Profiles.TryGetValue(profileId, out var profile))
        {
            return Task.FromResult<JobProfileDependentPositionResponse?>(null);
        }

        var position = profile.DependentPositions.FirstOrDefault(item => item.PublicId == dependentPositionId);
        return Task.FromResult(position is null
            ? null
            : new JobProfileDependentPositionResponse(
                position.PublicId,
                Guid.NewGuid(),
                "JP-REF",
                "Referenced profile",
                position.Quantity,
                position.Notes,
                position.ConcurrencyToken));
    }

    public Task<JobProfilePrintResponse?> GetPrintByIdAsync(Guid profileId, CancellationToken cancellationToken) =>
        Task.FromResult(PrintResponses.GetValueOrDefault(profileId));

    private static Guid ResolvePublicId(IDictionary<long, Guid> values, long internalId)
    {
        if (!values.TryGetValue(internalId, out var publicId))
        {
            publicId = Guid.NewGuid();
            values[internalId] = publicId;
        }

        return publicId;
    }
}

internal sealed class TestJobCatalogRepository : IJobCatalogRepository
{
    public void Add(JobCatalogItem item) { }
    public void Remove(JobCatalogItem item) { }
    public Task<JobCatalogItem?> GetByIdAsync(Guid itemId, CancellationToken cancellationToken) => Task.FromResult<JobCatalogItem?>(null);
    public Task<bool> ExistsOutsideTenantAsync(Guid itemId, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<bool> CodeExistsAsync(Guid tenantId, JobCatalogCategory category, string normalizedCode, long? excludingItemId, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<bool> HasUsageAsync(long catalogItemId, CancellationToken cancellationToken) => Task.FromResult(false);
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
    
    public Task<CatalogReferenceInternal?> GetActiveCatalogReferenceAsync(Guid tenantId, PositionDescriptionCatalogType catalogType, Guid catalogItemId, CancellationToken cancellationToken) =>
        Task.FromResult((CatalogReferenceInternal?)new CatalogReferenceInternal(1, catalogItemId, "Code", "Name", true));

    public Task<CatalogReferenceInternal?> GetActiveOrgUnitTypeReferenceAsync(Guid tenantId, Guid orgUnitTypeId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<bool> HasCategoriesUsingClassificationAsync(long classificationId, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<bool> HasJobProfilesUsingCategoryAsync(long categoryId, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<bool> HasJobProfilesUsingCatalogItemAsync(long catalogItemId, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<bool> HasClassificationsUsingCatalogItemAsync(long catalogItemId, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<bool> HasFunctionsUsingFrequencyAsync(long frequencyCatalogItemId, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<bool> HasRequirementsUsingRequirementTypeAsync(long requirementTypeCatalogItemId, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<bool> HasWorkConditionsUsingWorkConditionTypeAsync(long workConditionTypeCatalogItemId, CancellationToken cancellationToken) => Task.FromResult(false);
    public Task<long?> ResolvePositionCategoryIdAsync(Guid tenantId, Guid positionCategoryId, CancellationToken cancellationToken) => Task.FromResult((long?)1);
    public Task<string?> ResolveSalaryClassCodeByCatalogIdAsync(Guid tenantId, Guid salaryClassId, CancellationToken cancellationToken) => throw new NotImplementedException();
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
    public List<AuditLogEntry> Entries { get; } = [];
    public List<(Guid TenantId, AuditLogEntry Entry)> TenantEntries { get; } = [];

    public Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken)
    {
        Entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task LogForTenantAsync(Guid tenantId, AuditLogEntry entry, CancellationToken cancellationToken)
    {
        TenantEntries.Add((tenantId, entry));
        return Task.CompletedTask;
    }
}

internal sealed class FixedTenantContext(Guid? tenantId) : ITenantContext
{
    public Guid? TenantId { get; } = tenantId;
}

internal sealed class TestJobProfileCurrentUserService(Guid userId) : ICurrentUserService
{
    public bool IsAuthenticated => true;
    public string? UserId { get; } = userId.ToString("D");
    public IReadOnlyCollection<string> Roles => [];
    public IReadOnlyCollection<string> Permissions => [];
}

internal sealed class TestInternalCatalogRepository : IInternalCatalogRepository
{
    public void Add(InternalCatalogValue value) { }

    public Task<InternalCatalogValue?> GetByIdAsync(Guid valueId, CancellationToken cancellationToken) =>
        Task.FromResult<InternalCatalogValue?>(null);

    public Task<InternalCatalogValue?> FindActiveByExactValueAsync(string catalogKey, string normalizedValue, CancellationToken cancellationToken) =>
        Task.FromResult<InternalCatalogValue?>(null);

    public Task<IReadOnlyCollection<InternalCatalogSearchResult>> SearchAsync(
        string catalogKey,
        string normalizedSearch,
        int limit,
        double minScore,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<InternalCatalogSearchResult>>([]);

    public Task<IReadOnlyCollection<InternalCatalogSearchResult>> FindSimilarAsync(
        string catalogKey,
        string normalizedValue,
        int limit,
        double minScore,
        CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<InternalCatalogSearchResult>>([]);
}

internal sealed class TestSalaryTabulatorRepository : ISalaryTabulatorRepository
{
    public Dictionary<Guid, SalaryTabulatorLine> Lines { get; } = new();
    public int GetLineByIdCalls { get; private set; }

    public void AddLine(SalaryTabulatorLine line) => Lines[line.PublicId] = line;

    public void AddChangeRequest(SalaryTabulatorChangeRequest request) { }

    public Task<SalaryTabulatorLine?> GetLineByIdAsync(Guid lineId, CancellationToken cancellationToken)
    {
        GetLineByIdCalls++;
        return Task.FromResult(Lines.GetValueOrDefault(lineId));
    }

    public Task<bool> LineExistsOutsideTenantAsync(Guid lineId, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public Task<SalaryTabulatorLineResponse?> GetLineResponseByIdAsync(Guid lineId, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<PagedResponse<SalaryTabulatorLineListItemResponse>> SearchLinesAsync(
        Guid tenantId,
        string? salaryClassCode,
        string? salaryScaleCode,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<IReadOnlyCollection<SalaryTabulatorLineExportRow>> GetLineExportRowsAsync(
        Guid tenantId,
        string? salaryClassCode,
        string? salaryScaleCode,
        bool? isActive,
        string? search,
        int? maxRows,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<SalaryTabulatorLineSnapshot?> GetActiveLineSnapshotAsync(
        Guid tenantId,
        string normalizedSalaryClassCode,
        string normalizedSalaryScaleCode,
        DateTime effectiveAtUtc,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<SalaryTabulatorLine?> GetActiveLineEntityAsync(
        Guid tenantId,
        string normalizedSalaryClassCode,
        string normalizedSalaryScaleCode,
        DateTime effectiveAtUtc,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<SalaryTabulatorLine?> FindActiveLineForLegacyCompensationAsync(
        Guid tenantId,
        string normalizedSalaryClassCode,
        string? currencyCode,
        decimal? minAmount,
        decimal? maxAmount,
        DateTime effectiveFromUtc,
        DateTime? effectiveToUtc,
        CancellationToken cancellationToken) =>
        Task.FromResult<SalaryTabulatorLine?>(null);

    public Task<bool> HasLineWithEffectiveFromOnOrAfterAsync(
        Guid tenantId,
        string normalizedSalaryClassCode,
        string normalizedSalaryScaleCode,
        DateTime effectiveFromUtc,
        long? excludingLineId,
        CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public Task<bool> HasUncoveredJobProfileCompensationReferenceAsync(
        Guid tenantId,
        string normalizedSalaryClassCode,
        string normalizedSalaryScaleCode,
        DateTime fallbackEffectiveAtUtc,
        CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public Task<SalaryTabulatorChangeRequest?> GetChangeRequestByIdAsync(Guid requestId, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<bool> ChangeRequestExistsOutsideTenantAsync(Guid requestId, CancellationToken cancellationToken) =>
        Task.FromResult(false);

    public Task<SalaryTabulatorChangeRequestResponse?> GetChangeRequestResponseByIdAsync(Guid requestId, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<SalaryTabulatorChangeRequestImpactResponse?> GetChangeRequestImpactByIdAsync(Guid requestId, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<PagedResponse<SalaryTabulatorChangeRequestListItemResponse>> SearchChangeRequestsAsync(
        Guid tenantId,
        SalaryTabulatorChangeRequestStatus? status,
        Guid? requestedByUserId,
        DateTime? effectiveFromUtc,
        DateTime? effectiveToUtc,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken) =>
        throw new NotImplementedException();
}

internal sealed class TestJobProfileDateTimeProvider(DateTime utcNow) : IDateTimeProvider
{
    public DateTime UtcNow { get; } = utcNow;
}

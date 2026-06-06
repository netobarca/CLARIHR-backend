using CLARIHR.Application.Abstractions.CompetencyFramework;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.CompetencyFramework.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.CompetencyFramework;
using CLARIHR.Domain.JobProfiles;

namespace CLARIHR.Application.Features.CompetencyFramework;

internal static class CompetencyFrameworkPolicyAdapter
{
    public static OccupationalPyramidLevelListItemResponse ApplyAllowedActions(
        OccupationalPyramidLevelListItemResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage)
    {
        return response with { AllowedActions = StandardAllowedActions(resourceActionPolicyService, response.IsActive, canManage) };
    }

    public static OccupationalPyramidLevelResponse ApplyAllowedActions(
        OccupationalPyramidLevelResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage)
    {
        return response with { AllowedActions = StandardAllowedActions(resourceActionPolicyService, response.IsActive, canManage) };
    }

    public static CompetencyConductListItemResponse ApplyAllowedActions(
        CompetencyConductListItemResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage)
    {
        return response with { AllowedActions = StandardAllowedActions(resourceActionPolicyService, response.IsActive, canManage) };
    }

    public static CompetencyConductResponse ApplyAllowedActions(
        CompetencyConductResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage)
    {
        return response with { AllowedActions = StandardAllowedActions(resourceActionPolicyService, response.IsActive, canManage) };
    }

    public static JobProfileCompetencyMatrixResponse ApplyAllowedActions(
        JobProfileCompetencyMatrixResponse response,
        IResourceActionPolicyService resourceActionPolicyService,
        bool canManage)
    {
        var allowedActions = resourceActionPolicyService.Evaluate(new ResourceActionContext(
            CompetencyFrameworkPermissionCodes.ResourceKey,
            response.JobProfileStatus.ToString(),
            IsActive: response.JobProfileStatus != JobProfileStatus.Archived,
            SupportsEdit: true,
            EditAllowed: canManage,
            SupportsDelete: false,
            SupportsArchive: false,
            SupportsActivate: false,
            SupportsInactivate: false,
            NonEditableStates: [JobProfileStatus.Archived.ToString()]));

        return response with { AllowedActions = allowedActions };
    }

    private static AllowedActionsResponse StandardAllowedActions(
        IResourceActionPolicyService resourceActionPolicyService,
        bool isActive,
        bool canManage)
    {
        var state = isActive ? "Active" : "Inactive";
        return resourceActionPolicyService.Evaluate(new ResourceActionContext(
            CompetencyFrameworkPermissionCodes.ResourceKey,
            state,
            isActive,
            SupportsEdit: true,
            EditAllowed: canManage,
            SupportsDelete: false,
            SupportsArchive: false,
            SupportsActivate: true,
            ActivateAllowed: canManage,
            SupportsInactivate: true,
            InactivateAllowed: canManage));
    }
}

internal static class CompetencyFrameworkCatalogResolver
{
    public static async Task<Result<JobCatalogItem>> ResolveCatalogAsync(
        Guid tenantId,
        Guid catalogItemId,
        JobCatalogCategory category,
        ICompetencyFrameworkRepository repository,
        ICompetencyFrameworkAuthorizationService authorizationService,
        RbacPermissionAction action,
        CancellationToken cancellationToken)
    {
        var catalogItem = await repository.ResolveActiveCatalogItemAsync(tenantId, category, catalogItemId, cancellationToken);
        if (catalogItem is not null)
        {
            return Result<JobCatalogItem>.Success(catalogItem);
        }

        if (await repository.CatalogItemExistsOutsideTenantAsync(catalogItemId, cancellationToken))
        {
            return Result<JobCatalogItem>.Failure(authorizationService.TenantMismatch(action));
        }

        return Result<JobCatalogItem>.Failure(category switch
        {
            JobCatalogCategory.Competency => CompetencyFrameworkErrors.CompetencyNotFound,
            JobCatalogCategory.CompetencyType => CompetencyFrameworkErrors.CompetencyTypeNotFound,
            JobCatalogCategory.BehaviorLevel => CompetencyFrameworkErrors.BehaviorLevelNotFound,
            JobCatalogCategory.Behavior => CompetencyFrameworkErrors.BehaviorNotFound,
            _ => CompetencyFrameworkErrors.JobProfileCompetencyMatrixConflict
        });
    }

    public static async Task<Result<JobCatalogItem>> ResolveCatalogFromMapAsync(
        IReadOnlyDictionary<Guid, JobCatalogItem> resolved,
        Guid catalogItemId,
        JobCatalogCategory category,
        ICompetencyFrameworkRepository repository,
        ICompetencyFrameworkAuthorizationService authorizationService,
        RbacPermissionAction action,
        CancellationToken cancellationToken)
    {
        if (resolved.TryGetValue(catalogItemId, out var catalogItem))
        {
            return Result<JobCatalogItem>.Success(catalogItem);
        }

        if (await repository.CatalogItemExistsOutsideTenantAsync(catalogItemId, cancellationToken))
        {
            return Result<JobCatalogItem>.Failure(authorizationService.TenantMismatch(action));
        }

        return Result<JobCatalogItem>.Failure(category switch
        {
            JobCatalogCategory.Competency => CompetencyFrameworkErrors.CompetencyNotFound,
            JobCatalogCategory.CompetencyType => CompetencyFrameworkErrors.CompetencyTypeNotFound,
            JobCatalogCategory.BehaviorLevel => CompetencyFrameworkErrors.BehaviorLevelNotFound,
            JobCatalogCategory.Behavior => CompetencyFrameworkErrors.BehaviorNotFound,
            _ => CompetencyFrameworkErrors.JobProfileCompetencyMatrixConflict
        });
    }

    public static async Task<Result<OccupationalPyramidLevel>> ResolvePyramidLevelFromMapAsync(
        IReadOnlyDictionary<Guid, OccupationalPyramidLevel> resolved,
        Guid levelId,
        ICompetencyFrameworkRepository repository,
        ICompetencyFrameworkAuthorizationService authorizationService,
        RbacPermissionAction action,
        CancellationToken cancellationToken)
    {
        if (resolved.TryGetValue(levelId, out var level))
        {
            return Result<OccupationalPyramidLevel>.Success(level);
        }

        if (await repository.OccupationalPyramidLevelExistsOutsideTenantAsync(levelId, cancellationToken))
        {
            return Result<OccupationalPyramidLevel>.Failure(authorizationService.TenantMismatch(action));
        }

        return Result<OccupationalPyramidLevel>.Failure(CompetencyFrameworkErrors.OccupationalPyramidLevelNotFound);
    }

    public static async Task<Result<CompetencyConduct>> ResolveConductFromMapAsync(
        IReadOnlyDictionary<Guid, CompetencyConduct> resolved,
        Guid conductId,
        ICompetencyFrameworkRepository repository,
        ICompetencyFrameworkAuthorizationService authorizationService,
        RbacPermissionAction action,
        CancellationToken cancellationToken)
    {
        if (resolved.TryGetValue(conductId, out var conduct))
        {
            return Result<CompetencyConduct>.Success(conduct);
        }

        if (await repository.CompetencyConductExistsOutsideTenantAsync(conductId, cancellationToken))
        {
            return Result<CompetencyConduct>.Failure(authorizationService.TenantMismatch(action));
        }

        return Result<CompetencyConduct>.Failure(CompetencyFrameworkErrors.CompetencyConductNotFound);
    }
}

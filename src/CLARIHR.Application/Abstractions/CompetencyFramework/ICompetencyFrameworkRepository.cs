using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.CompetencyFramework;
using CLARIHR.Domain.CompetencyFramework;
using CLARIHR.Domain.JobProfiles;

namespace CLARIHR.Application.Abstractions.CompetencyFramework;

public interface ICompetencyFrameworkRepository
{
    void AddOccupationalPyramidLevel(OccupationalPyramidLevel level);

    void AddCompetencyConduct(CompetencyConduct conduct);

    void AddExpectations(IEnumerable<JobProfileCompetencyExpectation> expectations);

    void RemoveExpectations(IEnumerable<JobProfileCompetencyExpectation> expectations);

    Task<OccupationalPyramidLevel?> GetOccupationalPyramidLevelByIdAsync(Guid levelId, CancellationToken cancellationToken);

    Task<bool> OccupationalPyramidLevelExistsOutsideTenantAsync(Guid levelId, CancellationToken cancellationToken);

    Task<bool> OccupationalPyramidLevelCodeExistsAsync(
        Guid tenantId,
        string normalizedCode,
        long? excludingInternalId,
        CancellationToken cancellationToken);

    Task<bool> OccupationalPyramidLevelOrderExistsAsync(
        Guid tenantId,
        int levelOrder,
        long? excludingInternalId,
        CancellationToken cancellationToken);

    Task<bool> OccupationalPyramidLevelHasActiveUsageAsync(long levelInternalId, CancellationToken cancellationToken);

    Task<PagedResponse<OccupationalPyramidLevelListItemResponse>> SearchOccupationalPyramidLevelsAsync(
        Guid tenantId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<OccupationalPyramidLevelResponse?> GetOccupationalPyramidLevelResponseByIdAsync(Guid levelId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, OccupationalPyramidLevel>> ResolveActiveOccupationalPyramidLevelsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> levelIds,
        CancellationToken cancellationToken);

    Task<CompetencyConduct?> GetCompetencyConductByIdAsync(
        Guid conductId,
        bool includeBehaviors,
        CancellationToken cancellationToken);

    Task<bool> CompetencyConductExistsOutsideTenantAsync(Guid conductId, CancellationToken cancellationToken);

    Task<bool> CompetencyConductDuplicateExistsAsync(
        Guid tenantId,
        long competencyCatalogItemId,
        long competencyTypeCatalogItemId,
        long behaviorLevelCatalogItemId,
        string normalizedDescription,
        long? excludingInternalId,
        CancellationToken cancellationToken);

    Task<bool> CompetencyConductHasActiveUsageAsync(long conductInternalId, CancellationToken cancellationToken);

    Task<PagedResponse<CompetencyConductListItemResponse>> SearchCompetencyConductsAsync(
        Guid tenantId,
        Guid? competencyId,
        Guid? competencyTypeId,
        Guid? behaviorLevelId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<CompetencyConductResponse?> GetCompetencyConductResponseByIdAsync(Guid conductId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, CompetencyConduct>> ResolveActiveCompetencyConductsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> conductIds,
        CancellationToken cancellationToken);

    Task<JobCatalogItem?> ResolveActiveCatalogItemAsync(
        Guid tenantId,
        JobCatalogCategory category,
        Guid catalogItemId,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, JobCatalogItem>> ResolveActiveCatalogItemsAsync(
        Guid tenantId,
        JobCatalogCategory category,
        IReadOnlyCollection<Guid> catalogItemIds,
        CancellationToken cancellationToken);

    Task<bool> CatalogItemExistsOutsideTenantAsync(Guid catalogItemId, CancellationToken cancellationToken);

    Task<JobProfile?> GetJobProfileAggregateByIdAsync(Guid jobProfileId, CancellationToken cancellationToken);

    Task<bool> JobProfileExistsOutsideTenantAsync(Guid jobProfileId, CancellationToken cancellationToken);

    Task<JobProfileCompetencyExpectation?> GetExpectationAggregateAsync(
        long jobProfileInternalId,
        Guid itemId,
        CancellationToken cancellationToken);

    Task<int> CountExpectationsByJobProfileIdAsync(
        long jobProfileInternalId,
        CancellationToken cancellationToken);

    Task<bool> ExpectationTupleExistsAsync(
        long jobProfileInternalId,
        long occupationalPyramidLevelInternalId,
        long competencyCatalogItemId,
        long competencyTypeCatalogItemId,
        long behaviorLevelCatalogItemId,
        long? excludingExpectationInternalId,
        CancellationToken cancellationToken);

    Task<JobProfileCompetencyMatrixResponse?> GetJobProfileCompetencyMatrixResponseAsync(
        Guid jobProfileId,
        CancellationToken cancellationToken);

    Task<JobProfileCompetencyMatrixItemResponse?> GetJobProfileCompetencyMatrixItemResponseAsync(
        Guid jobProfileId,
        Guid itemId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<JobProfileCompetencyMatrixExportRow>> GetJobProfileCompetencyMatrixExportRowsAsync(
        Guid jobProfileId,
        int? maxRows,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves a competency matrix expectation by public id for the tenant, returning the internal references
    /// needed to record an employee competency result (competency, type, the owning job profile and the expected
    /// value on the company scale). Null when it does not exist for the tenant.
    /// </summary>
    Task<CompetencyExpectationReference?> GetExpectationReferenceAsync(
        Guid tenantId,
        Guid expectationPublicId,
        CancellationToken cancellationToken);

    /// <summary>Returns the company's active competency rating scale (with its levels), or null when none is configured.</summary>
    Task<CompetencyRatingScale?> GetActiveRatingScaleAsync(
        Guid tenantId,
        CancellationToken cancellationToken);

    /// <summary>Tracked load (with levels) of the company's active competency rating scale, for in-place redefinition.</summary>
    Task<CompetencyRatingScale?> GetActiveRatingScaleForUpdateAsync(
        Guid tenantId,
        CancellationToken cancellationToken);

    void AddCompetencyRatingScale(CompetencyRatingScale scale);
}

/// <summary>Internal references of a competency matrix expectation, used to record an employee competency result.</summary>
public sealed record CompetencyExpectationReference(
    long ExpectationInternalId,
    long JobProfileInternalId,
    long CompetencyCatalogItemId,
    long CompetencyTypeCatalogItemId,
    decimal? ExpectedValue);

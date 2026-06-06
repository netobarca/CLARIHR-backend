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

    Task<IReadOnlyCollection<JobProfileCompetencyExpectation>> GetExpectationsByJobProfileIdAsync(
        long jobProfileInternalId,
        CancellationToken cancellationToken);

    Task<JobProfileCompetencyMatrixResponse?> GetJobProfileCompetencyMatrixResponseAsync(
        Guid jobProfileId,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<JobProfileCompetencyMatrixExportRow>> GetJobProfileCompetencyMatrixExportRowsAsync(
        Guid jobProfileId,
        int? maxRows,
        CancellationToken cancellationToken);
}

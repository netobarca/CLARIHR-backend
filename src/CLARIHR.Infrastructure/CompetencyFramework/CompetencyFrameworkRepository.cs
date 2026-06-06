using CLARIHR.Application.Abstractions.CompetencyFramework;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.CompetencyFramework;
using CLARIHR.Domain.CompetencyFramework;
using CLARIHR.Domain.JobProfiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.CompetencyFramework;

internal sealed class CompetencyFrameworkRepository(ApplicationDbContext dbContext) : ICompetencyFrameworkRepository
{
    public void AddOccupationalPyramidLevel(OccupationalPyramidLevel level) =>
        dbContext.Set<OccupationalPyramidLevel>().Add(level);

    public void AddCompetencyConduct(CompetencyConduct conduct) =>
        dbContext.Set<CompetencyConduct>().Add(conduct);

    public void AddExpectations(IEnumerable<JobProfileCompetencyExpectation> expectations) =>
        dbContext.Set<JobProfileCompetencyExpectation>().AddRange(expectations);

    public void RemoveExpectations(IEnumerable<JobProfileCompetencyExpectation> expectations) =>
        dbContext.Set<JobProfileCompetencyExpectation>().RemoveRange(expectations);

    public Task<OccupationalPyramidLevel?> GetOccupationalPyramidLevelByIdAsync(Guid levelId, CancellationToken cancellationToken) =>
        dbContext.Set<OccupationalPyramidLevel>()
            .SingleOrDefaultAsync(level => level.PublicId == levelId, cancellationToken);

    public Task<bool> OccupationalPyramidLevelExistsOutsideTenantAsync(Guid levelId, CancellationToken cancellationToken) =>
        dbContext.Set<OccupationalPyramidLevel>()
            // Intentional tenant filter bypass: checks cross-tenant existence only for tenant-mismatch errors.
            .IgnoreQueryFilters()
            .AnyAsync(level => level.PublicId == levelId, cancellationToken);

    public Task<bool> OccupationalPyramidLevelCodeExistsAsync(
        Guid tenantId,
        string normalizedCode,
        long? excludingInternalId,
        CancellationToken cancellationToken) =>
        dbContext.Set<OccupationalPyramidLevel>()
            .AnyAsync(level =>
                level.TenantId == tenantId &&
                level.NormalizedCode == normalizedCode &&
                (!excludingInternalId.HasValue || level.Id != excludingInternalId.Value),
                cancellationToken);

    public Task<bool> OccupationalPyramidLevelOrderExistsAsync(
        Guid tenantId,
        int levelOrder,
        long? excludingInternalId,
        CancellationToken cancellationToken) =>
        dbContext.Set<OccupationalPyramidLevel>()
            .AnyAsync(level =>
                level.TenantId == tenantId &&
                level.LevelOrder == levelOrder &&
                (!excludingInternalId.HasValue || level.Id != excludingInternalId.Value),
                cancellationToken);

    public Task<bool> OccupationalPyramidLevelHasActiveUsageAsync(long levelInternalId, CancellationToken cancellationToken) =>
        (from expectation in dbContext.Set<JobProfileCompetencyExpectation>().AsNoTracking()
         join jobProfile in dbContext.JobProfiles.AsNoTracking() on expectation.JobProfileId equals jobProfile.Id
         where expectation.OccupationalPyramidLevelId == levelInternalId &&
               jobProfile.IsActive &&
               jobProfile.Status != JobProfileStatus.Archived
         select expectation.Id)
        .AnyAsync(cancellationToken);

    public async Task<PagedResponse<OccupationalPyramidLevelListItemResponse>> SearchOccupationalPyramidLevelsAsync(
        Guid tenantId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<OccupationalPyramidLevel>()
            .AsNoTracking()
            .Where(level => level.TenantId == tenantId);

        if (isActive.HasValue)
        {
            query = query.Where(level => level.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(level =>
                level.NormalizedCode.Contains(normalizedSearch) ||
                level.NormalizedName.Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(level => level.LevelOrder)
            .ThenBy(level => level.Code)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(level => new OccupationalPyramidLevelListItemResponse(
                level.PublicId,
                level.Code,
                level.Name,
                level.LevelOrder,
                level.Description,
                level.IsActive,
                level.ConcurrencyToken,
                level.CreatedUtc,
                level.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<OccupationalPyramidLevelListItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<OccupationalPyramidLevelResponse?> GetOccupationalPyramidLevelResponseByIdAsync(Guid levelId, CancellationToken cancellationToken) =>
        dbContext.Set<OccupationalPyramidLevel>()
            .AsNoTracking()
            .Where(level => level.PublicId == levelId)
            .Select(level => new OccupationalPyramidLevelResponse(
                level.PublicId,
                level.TenantId,
                level.Code,
                level.Name,
                level.LevelOrder,
                level.Description,
                level.IsActive,
                level.ConcurrencyToken,
                level.CreatedUtc,
                level.ModifiedUtc))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, OccupationalPyramidLevel>> ResolveActiveOccupationalPyramidLevelsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> levelIds,
        CancellationToken cancellationToken)
    {
        if (levelIds.Count == 0)
        {
            return new Dictionary<Guid, OccupationalPyramidLevel>();
        }

        var distinctIds = levelIds.Distinct().ToArray();
        var levels = await dbContext.Set<OccupationalPyramidLevel>()
            .AsNoTracking()
            .Where(level => level.TenantId == tenantId && level.IsActive && distinctIds.Contains(level.PublicId))
            .ToListAsync(cancellationToken);

        return levels.ToDictionary(level => level.PublicId);
    }

    public async Task<CompetencyConduct?> GetCompetencyConductByIdAsync(
        Guid conductId,
        bool includeBehaviors,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<CompetencyConduct>()
            .Where(conduct => conduct.PublicId == conductId);

        if (includeBehaviors)
        {
            query = query.Include(conduct => conduct.Behaviors);
        }

        return await query.SingleOrDefaultAsync(cancellationToken);
    }

    public Task<bool> CompetencyConductExistsOutsideTenantAsync(Guid conductId, CancellationToken cancellationToken) =>
        dbContext.Set<CompetencyConduct>()
            // Intentional tenant filter bypass: checks cross-tenant existence only for tenant-mismatch errors.
            .IgnoreQueryFilters()
            .AnyAsync(conduct => conduct.PublicId == conductId, cancellationToken);

    public Task<bool> CompetencyConductDuplicateExistsAsync(
        Guid tenantId,
        long competencyCatalogItemId,
        long competencyTypeCatalogItemId,
        long behaviorLevelCatalogItemId,
        string normalizedDescription,
        long? excludingInternalId,
        CancellationToken cancellationToken) =>
        dbContext.Set<CompetencyConduct>().AnyAsync(conduct =>
            conduct.TenantId == tenantId &&
            conduct.CompetencyCatalogItemId == competencyCatalogItemId &&
            conduct.CompetencyTypeCatalogItemId == competencyTypeCatalogItemId &&
            conduct.BehaviorLevelCatalogItemId == behaviorLevelCatalogItemId &&
            conduct.NormalizedDescription == normalizedDescription &&
            (!excludingInternalId.HasValue || conduct.Id != excludingInternalId.Value),
            cancellationToken);

    public Task<bool> CompetencyConductHasActiveUsageAsync(long conductInternalId, CancellationToken cancellationToken) =>
        (from link in dbContext.Set<JobProfileCompetencyExpectationConduct>().AsNoTracking()
         join expectation in dbContext.Set<JobProfileCompetencyExpectation>().AsNoTracking() on link.JobProfileCompetencyExpectationId equals expectation.Id
         join jobProfile in dbContext.JobProfiles.AsNoTracking() on expectation.JobProfileId equals jobProfile.Id
         where link.CompetencyConductId == conductInternalId &&
               jobProfile.IsActive &&
               jobProfile.Status != JobProfileStatus.Archived
         select link.Id)
        .AnyAsync(cancellationToken);

    public async Task<PagedResponse<CompetencyConductListItemResponse>> SearchCompetencyConductsAsync(
        Guid tenantId,
        Guid? competencyId,
        Guid? competencyTypeId,
        Guid? behaviorLevelId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query =
            from conduct in dbContext.Set<CompetencyConduct>().AsNoTracking()
            join competency in dbContext.JobCatalogItems.AsNoTracking() on conduct.CompetencyCatalogItemId equals competency.Id
            join competencyType in dbContext.JobCatalogItems.AsNoTracking() on conduct.CompetencyTypeCatalogItemId equals competencyType.Id
            join behaviorLevel in dbContext.JobCatalogItems.AsNoTracking() on conduct.BehaviorLevelCatalogItemId equals behaviorLevel.Id
            where conduct.TenantId == tenantId
            select new
            {
                Conduct = conduct,
                Competency = competency,
                CompetencyType = competencyType,
                BehaviorLevel = behaviorLevel
            };

        if (competencyId.HasValue)
        {
            query = query.Where(item => item.Competency.PublicId == competencyId.Value);
        }

        if (competencyTypeId.HasValue)
        {
            query = query.Where(item => item.CompetencyType.PublicId == competencyTypeId.Value);
        }

        if (behaviorLevelId.HasValue)
        {
            query = query.Where(item => item.BehaviorLevel.PublicId == behaviorLevelId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(item => item.Conduct.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(item =>
                item.Conduct.NormalizedDescription.Contains(normalizedSearch) ||
                item.Competency.NormalizedCode.Contains(normalizedSearch) ||
                item.Competency.NormalizedName.Contains(normalizedSearch) ||
                item.CompetencyType.NormalizedCode.Contains(normalizedSearch) ||
                item.BehaviorLevel.NormalizedCode.Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.Conduct.SortOrder)
            .ThenBy(item => item.Conduct.Description)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new CompetencyConductListItemResponse(
                item.Conduct.PublicId,
                item.Conduct.TenantId,
                item.Competency.PublicId,
                item.Competency.Code,
                item.Competency.Name,
                item.CompetencyType.PublicId,
                item.CompetencyType.Code,
                item.CompetencyType.Name,
                item.BehaviorLevel.PublicId,
                item.BehaviorLevel.Code,
                item.BehaviorLevel.Name,
                item.Conduct.Description,
                item.Conduct.SortOrder,
                item.Conduct.IsActive,
                item.Conduct.ConcurrencyToken,
                item.Conduct.CreatedUtc,
                item.Conduct.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<CompetencyConductListItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    public async Task<CompetencyConductResponse?> GetCompetencyConductResponseByIdAsync(Guid conductId, CancellationToken cancellationToken)
    {
        var baseRow = await
            (from conduct in dbContext.Set<CompetencyConduct>().AsNoTracking()
             join competency in dbContext.JobCatalogItems.AsNoTracking() on conduct.CompetencyCatalogItemId equals competency.Id
             join competencyType in dbContext.JobCatalogItems.AsNoTracking() on conduct.CompetencyTypeCatalogItemId equals competencyType.Id
             join behaviorLevel in dbContext.JobCatalogItems.AsNoTracking() on conduct.BehaviorLevelCatalogItemId equals behaviorLevel.Id
             where conduct.PublicId == conductId
             select new
             {
                 Conduct = conduct,
                 Competency = competency,
                 CompetencyType = competencyType,
                 BehaviorLevel = behaviorLevel
             })
            .SingleOrDefaultAsync(cancellationToken);

        if (baseRow is null)
        {
            return null;
        }

        var behaviors = await
            (from behaviorLink in dbContext.Set<CompetencyConductBehavior>().AsNoTracking()
             join behavior in dbContext.JobCatalogItems.AsNoTracking() on behaviorLink.BehaviorCatalogItemId equals behavior.Id
             where behaviorLink.CompetencyConductId == baseRow.Conduct.Id
             orderby behaviorLink.SortOrder, behavior.Name
             select new CompetencyConductBehaviorResponse(
                 behavior.PublicId,
                 behavior.Code,
                 behavior.Name,
                 behaviorLink.Notes,
                 behaviorLink.SortOrder))
            .ToArrayAsync(cancellationToken);

        return new CompetencyConductResponse(
            baseRow.Conduct.PublicId,
            baseRow.Conduct.TenantId,
            baseRow.Competency.PublicId,
            baseRow.Competency.Code,
            baseRow.Competency.Name,
            baseRow.CompetencyType.PublicId,
            baseRow.CompetencyType.Code,
            baseRow.CompetencyType.Name,
            baseRow.BehaviorLevel.PublicId,
            baseRow.BehaviorLevel.Code,
            baseRow.BehaviorLevel.Name,
            baseRow.Conduct.Description,
            baseRow.Conduct.SortOrder,
            baseRow.Conduct.IsActive,
            behaviors,
            baseRow.Conduct.ConcurrencyToken,
            baseRow.Conduct.CreatedUtc,
            baseRow.Conduct.ModifiedUtc);
    }

    public async Task<IReadOnlyDictionary<Guid, CompetencyConduct>> ResolveActiveCompetencyConductsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> conductIds,
        CancellationToken cancellationToken)
    {
        if (conductIds.Count == 0)
        {
            return new Dictionary<Guid, CompetencyConduct>();
        }

        var distinctIds = conductIds.Distinct().ToArray();
        var conducts = await dbContext.Set<CompetencyConduct>()
            .AsNoTracking()
            .Where(conduct => conduct.TenantId == tenantId && conduct.IsActive && distinctIds.Contains(conduct.PublicId))
            .ToListAsync(cancellationToken);

        return conducts.ToDictionary(conduct => conduct.PublicId);
    }

    public Task<JobCatalogItem?> ResolveActiveCatalogItemAsync(
        Guid tenantId,
        JobCatalogCategory category,
        Guid catalogItemId,
        CancellationToken cancellationToken) =>
        dbContext.JobCatalogItems
            .SingleOrDefaultAsync(item =>
                item.TenantId == tenantId &&
                item.PublicId == catalogItemId &&
                item.Category == category &&
                item.IsActive,
                cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, JobCatalogItem>> ResolveActiveCatalogItemsAsync(
        Guid tenantId,
        JobCatalogCategory category,
        IReadOnlyCollection<Guid> catalogItemIds,
        CancellationToken cancellationToken)
    {
        if (catalogItemIds.Count == 0)
        {
            return new Dictionary<Guid, JobCatalogItem>();
        }

        var distinctIds = catalogItemIds.Distinct().ToArray();
        var items = await dbContext.JobCatalogItems
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId &&
                item.Category == category &&
                item.IsActive &&
                distinctIds.Contains(item.PublicId))
            .ToListAsync(cancellationToken);

        return items.ToDictionary(item => item.PublicId);
    }

    public Task<bool> CatalogItemExistsOutsideTenantAsync(Guid catalogItemId, CancellationToken cancellationToken) =>
        dbContext.JobCatalogItems
            // Intentional tenant filter bypass: checks cross-tenant existence only for tenant-mismatch errors.
            .IgnoreQueryFilters()
            .AnyAsync(item => item.PublicId == catalogItemId, cancellationToken);

    public Task<JobProfile?> GetJobProfileAggregateByIdAsync(Guid jobProfileId, CancellationToken cancellationToken) =>
        dbContext.JobProfiles.SingleOrDefaultAsync(profile => profile.PublicId == jobProfileId, cancellationToken);

    public Task<bool> JobProfileExistsOutsideTenantAsync(Guid jobProfileId, CancellationToken cancellationToken) =>
        dbContext.JobProfiles
            // Intentional tenant filter bypass: checks cross-tenant existence only for tenant-mismatch errors.
            .IgnoreQueryFilters()
            .AnyAsync(profile => profile.PublicId == jobProfileId, cancellationToken);

    public async Task<IReadOnlyCollection<JobProfileCompetencyExpectation>> GetExpectationsByJobProfileIdAsync(
        long jobProfileInternalId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Set<JobProfileCompetencyExpectation>()
            .Include(expectation => expectation.Conducts)
            .Where(expectation => expectation.JobProfileId == jobProfileInternalId)
            .ToListAsync(cancellationToken);
    }

    public async Task<JobProfileCompetencyMatrixResponse?> GetJobProfileCompetencyMatrixResponseAsync(
        Guid jobProfileId,
        CancellationToken cancellationToken)
    {
        var profile = await dbContext.JobProfiles
            .AsNoTracking()
            .Where(item => item.PublicId == jobProfileId)
            .Select(item => new
            {
                item.Id,
                item.PublicId,
                item.Code,
                item.Title,
                item.Status,
                item.Version,
                item.ConcurrencyToken
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (profile is null)
        {
            return null;
        }

        var rows = await
            (from expectation in dbContext.Set<JobProfileCompetencyExpectation>().AsNoTracking()
             join level in dbContext.Set<OccupationalPyramidLevel>().AsNoTracking() on expectation.OccupationalPyramidLevelId equals level.Id
             join competency in dbContext.JobCatalogItems.AsNoTracking() on expectation.CompetencyCatalogItemId equals competency.Id
             join competencyType in dbContext.JobCatalogItems.AsNoTracking() on expectation.CompetencyTypeCatalogItemId equals competencyType.Id
             join behaviorLevel in dbContext.JobCatalogItems.AsNoTracking() on expectation.BehaviorLevelCatalogItemId equals behaviorLevel.Id
             join link in dbContext.Set<JobProfileCompetencyExpectationConduct>().AsNoTracking()
                 on expectation.Id equals link.JobProfileCompetencyExpectationId into conductLinks
             from link in conductLinks.DefaultIfEmpty()
             join conduct in dbContext.Set<CompetencyConduct>().AsNoTracking()
                 on link.CompetencyConductId equals conduct.Id into conductJoin
             from conduct in conductJoin.DefaultIfEmpty()
             where expectation.JobProfileId == profile.Id
             orderby expectation.SortOrder, link.SortOrder
             select new
             {
                 ExpectationId = expectation.Id,
                 expectation.SortOrder,
                 expectation.ExpectedEvidence,
                 LevelId = level.PublicId,
                 LevelCode = level.Code,
                 LevelName = level.Name,
                 level.LevelOrder,
                 CompetencyId = competency.PublicId,
                 CompetencyCode = competency.Code,
                 CompetencyName = competency.Name,
                 CompetencyTypeId = competencyType.PublicId,
                 CompetencyTypeCode = competencyType.Code,
                 CompetencyTypeName = competencyType.Name,
                 BehaviorLevelId = behaviorLevel.PublicId,
                 BehaviorLevelCode = behaviorLevel.Code,
                 BehaviorLevelName = behaviorLevel.Name,
                 ConductId = conduct != null ? conduct.PublicId : (Guid?)null,
                 ConductDescription = conduct != null ? conduct.Description : null,
                 ConductSortOrder = link != null ? link.SortOrder : (int?)null
             })
            .ToListAsync(cancellationToken);

        var items = rows
            .GroupBy(row => row.ExpectationId)
            .OrderBy(group => group.First().SortOrder)
            .Select(group =>
            {
                var head = group.First();
                var conducts = group
                    .Where(row => row.ConductId.HasValue)
                    .Select(row => new JobProfileCompetencyMatrixItemConductResponse(
                        row.ConductId!.Value,
                        row.ConductDescription!,
                        row.ConductSortOrder ?? 0))
                    .ToArray();

                return new JobProfileCompetencyMatrixItemResponse(
                    head.LevelId,
                    head.LevelCode,
                    head.LevelName,
                    head.LevelOrder,
                    head.CompetencyId,
                    head.CompetencyCode,
                    head.CompetencyName,
                    head.CompetencyTypeId,
                    head.CompetencyTypeCode,
                    head.CompetencyTypeName,
                    head.BehaviorLevelId,
                    head.BehaviorLevelCode,
                    head.BehaviorLevelName,
                    head.ExpectedEvidence,
                    head.SortOrder,
                    conducts);
            })
            .ToArray();

        return new JobProfileCompetencyMatrixResponse(
            profile.PublicId,
            profile.Code,
            profile.Title,
            profile.Status,
            profile.Version,
            profile.ConcurrencyToken,
            items);
    }

    public async Task<IReadOnlyCollection<JobProfileCompetencyMatrixExportRow>> GetJobProfileCompetencyMatrixExportRowsAsync(
        Guid jobProfileId,
        int? maxRows,
        CancellationToken cancellationToken)
    {
        var query =
            from profile in dbContext.JobProfiles.AsNoTracking()
             join expectation in dbContext.Set<JobProfileCompetencyExpectation>().AsNoTracking()
                 on profile.Id equals expectation.JobProfileId
             join level in dbContext.Set<OccupationalPyramidLevel>().AsNoTracking() on expectation.OccupationalPyramidLevelId equals level.Id
             join competency in dbContext.JobCatalogItems.AsNoTracking() on expectation.CompetencyCatalogItemId equals competency.Id
             join competencyType in dbContext.JobCatalogItems.AsNoTracking() on expectation.CompetencyTypeCatalogItemId equals competencyType.Id
             join behaviorLevel in dbContext.JobCatalogItems.AsNoTracking() on expectation.BehaviorLevelCatalogItemId equals behaviorLevel.Id
             join link in dbContext.Set<JobProfileCompetencyExpectationConduct>().AsNoTracking()
                 on expectation.Id equals link.JobProfileCompetencyExpectationId into conductLinks
             from link in conductLinks.DefaultIfEmpty()
             join conduct in dbContext.Set<CompetencyConduct>().AsNoTracking()
                 on link.CompetencyConductId equals conduct.Id into conductJoin
             from conduct in conductJoin.DefaultIfEmpty()
             where profile.PublicId == jobProfileId
             orderby expectation.SortOrder, link.SortOrder
             select new JobProfileCompetencyMatrixExportRow(
                 profile.PublicId,
                 profile.Code,
                 profile.Title,
                 profile.Status.ToString(),
                 profile.Version,
                 level.PublicId,
                 level.Code,
                 level.Name,
                 level.LevelOrder,
                 competency.PublicId,
                 competency.Code,
                 competency.Name,
                 competencyType.PublicId,
                 competencyType.Code,
                 competencyType.Name,
                 behaviorLevel.PublicId,
                 behaviorLevel.Code,
                 behaviorLevel.Name,
                 conduct != null ? conduct.PublicId : null,
                 conduct != null ? conduct.Description : null,
                 link != null ? link.SortOrder : null,
                 expectation.ExpectedEvidence,
                 expectation.SortOrder);

        if (maxRows.HasValue)
        {
            query = query.Take(maxRows.Value);
        }

        return await query
            .ToArrayAsync(cancellationToken);
    }
}

using CLARIHR.Application.Abstractions.JobProfiles;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.JobProfiles;
using CLARIHR.Domain.CompetencyFramework;
using CLARIHR.Domain.JobProfiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.JobProfiles;

internal sealed class JobProfileRepository(ApplicationDbContext dbContext) : IJobProfileRepository
{
    public void Add(JobProfile profile) => dbContext.JobProfiles.Add(profile);

    public Task<JobProfile?> GetByIdAsync(Guid profileId, CancellationToken cancellationToken) =>
        dbContext.JobProfiles
            .AsSplitQuery()
            .Include(profile => profile.Requirements)
                .ThenInclude(requirement => requirement.CatalogItem)
            .Include(profile => profile.Functions)
            .Include(profile => profile.Relations)
                .ThenInclude(relation => relation.CatalogItem)
            .Include(profile => profile.Competencies)
                .ThenInclude(competency => competency.CatalogItem)
            .Include(profile => profile.Trainings)
                .ThenInclude(training => training.CatalogItem)
            .Include(profile => profile.Benefits)
                .ThenInclude(benefit => benefit.CatalogItem)
            .Include(profile => profile.WorkingConditions)
                .ThenInclude(condition => condition.CatalogItem)
            .Include(profile => profile.DependentPositions)
                .ThenInclude(position => position.DependentJobProfile)
            .SingleOrDefaultAsync(profile => profile.PublicId == profileId, cancellationToken);

    public Task<JobProfile?> GetCoreByIdAsync(Guid profileId, CancellationToken cancellationToken) =>
        dbContext.JobProfiles
            .SingleOrDefaultAsync(profile => profile.PublicId == profileId, cancellationToken);

    public Task<JobProfile?> GetWithRequirementsOnlyAsync(Guid profileId, CancellationToken cancellationToken) =>
        dbContext.JobProfiles
            .Include(profile => profile.Requirements)
                .ThenInclude(requirement => requirement.CatalogItem)
            .SingleOrDefaultAsync(profile => profile.PublicId == profileId, cancellationToken);

    public Task<JobProfile?> GetWithFunctionsOnlyAsync(Guid profileId, CancellationToken cancellationToken) =>
        dbContext.JobProfiles
            .Include(profile => profile.Functions)
            .SingleOrDefaultAsync(profile => profile.PublicId == profileId, cancellationToken);

    public Task<JobProfile?> GetWithRelationsOnlyAsync(Guid profileId, CancellationToken cancellationToken) =>
        dbContext.JobProfiles
            .Include(profile => profile.Relations)
                .ThenInclude(relation => relation.CatalogItem)
            .SingleOrDefaultAsync(profile => profile.PublicId == profileId, cancellationToken);

    public Task<JobProfile?> GetWithCompetenciesOnlyAsync(Guid profileId, CancellationToken cancellationToken) =>
        dbContext.JobProfiles
            .Include(profile => profile.Competencies)
                .ThenInclude(competency => competency.CatalogItem)
            .SingleOrDefaultAsync(profile => profile.PublicId == profileId, cancellationToken);

    public Task<JobProfile?> GetWithWorkingConditionsOnlyAsync(Guid profileId, CancellationToken cancellationToken) =>
        dbContext.JobProfiles
            .Include(profile => profile.WorkingConditions)
            .SingleOrDefaultAsync(profile => profile.PublicId == profileId, cancellationToken);

    public Task<JobProfile?> GetWithTrainingsOnlyAsync(Guid profileId, CancellationToken cancellationToken) =>
        dbContext.JobProfiles
            .Include(profile => profile.Trainings)
                .ThenInclude(training => training.CatalogItem)
            .SingleOrDefaultAsync(profile => profile.PublicId == profileId, cancellationToken);

    public Task<JobProfile?> GetWithBenefitsOnlyAsync(Guid profileId, CancellationToken cancellationToken) =>
        dbContext.JobProfiles
            .Include(profile => profile.Benefits)
                .ThenInclude(benefit => benefit.CatalogItem)
            .SingleOrDefaultAsync(profile => profile.PublicId == profileId, cancellationToken);

    public Task<JobProfile?> GetWithDependentPositionsOnlyAsync(Guid profileId, CancellationToken cancellationToken) =>
        dbContext.JobProfiles
            .Include(profile => profile.DependentPositions)
                .ThenInclude(position => position.DependentJobProfile)
            .SingleOrDefaultAsync(profile => profile.PublicId == profileId, cancellationToken);

    public Task<bool> ExistsOutsideTenantAsync(Guid profileId, CancellationToken cancellationToken) =>
        dbContext.JobProfiles
            // Intentional tenant filter bypass: checks cross-tenant existence only for tenant-mismatch errors.
            .IgnoreQueryFilters()
            .AnyAsync(profile => profile.PublicId == profileId, cancellationToken);

    public Task<bool> CodeExistsAsync(Guid tenantId, string normalizedCode, long? excludingProfileId, CancellationToken cancellationToken) =>
        dbContext.JobProfiles.AnyAsync(
            profile => profile.TenantId == tenantId &&
                       profile.NormalizedCode == normalizedCode &&
                       (!excludingProfileId.HasValue || profile.Id != excludingProfileId.Value),
            cancellationToken);

    public Task<long?> ResolveOrgUnitIdAsync(Guid tenantId, Guid orgUnitId, CancellationToken cancellationToken) =>
        dbContext.OrgUnits
            .AsNoTracking()
            .Where(unit => unit.TenantId == tenantId && unit.PublicId == orgUnitId)
            .Select(unit => (long?)unit.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> OrgUnitExistsOutsideTenantAsync(Guid orgUnitId, CancellationToken cancellationToken) =>
        dbContext.OrgUnits
            // Intentional tenant filter bypass: checks cross-tenant existence only for tenant-mismatch errors.
            .IgnoreQueryFilters()
            .AnyAsync(unit => unit.PublicId == orgUnitId, cancellationToken);

    public Task<long?> ResolveProfileIdAsync(Guid tenantId, Guid profileId, CancellationToken cancellationToken) =>
        dbContext.JobProfiles
            .AsNoTracking()
            .Where(profile => profile.TenantId == tenantId && profile.PublicId == profileId)
            .Select(profile => (long?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<JobProfileReferenceResponse?> GetReferenceByIdAsync(Guid tenantId, Guid profileId, CancellationToken cancellationToken) =>
        dbContext.JobProfiles
            .AsNoTracking()
            .Where(profile => profile.TenantId == tenantId && profile.PublicId == profileId)
            .Select(profile => new JobProfileReferenceResponse(
                profile.PublicId,
                profile.Code,
                profile.Title))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<JobProfileReferenceResponse?> GetReferenceByInternalIdAsync(Guid tenantId, long profileId, CancellationToken cancellationToken) =>
        dbContext.JobProfiles
            .AsNoTracking()
            .Where(profile => profile.TenantId == tenantId && profile.Id == profileId)
            .Select(profile => new JobProfileReferenceResponse(
                profile.PublicId,
                profile.Code,
                profile.Title))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> ProfileExistsInTenantAsync(Guid tenantId, long profileId, CancellationToken cancellationToken) =>
        dbContext.JobProfiles
            .AsNoTracking()
            .AnyAsync(profile => profile.TenantId == tenantId && profile.Id == profileId, cancellationToken);

    public async Task<IReadOnlyList<JobProfileDependencyNodeData>> GetDependencyGraphAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var profiles = await dbContext.JobProfiles
            .AsNoTracking()
            .Where(profile => profile.TenantId == tenantId)
            .Select(profile => new
            {
                profile.Id,
                profile.PublicId,
                profile.ReportsToJobProfileId
            })
            .ToListAsync(cancellationToken);

        var dependentEdges = await dbContext.JobProfileDependentPositions
            .AsNoTracking()
            .Where(position => position.TenantId == tenantId)
            .Select(position => new
            {
                position.JobProfileId,
                position.DependentJobProfileId
            })
            .ToListAsync(cancellationToken);

        var byProfile = dependentEdges
            .GroupBy(edge => edge.JobProfileId)
            .ToDictionary(
                static group => group.Key,
                static group => (IReadOnlyCollection<long>)group
                    .Select(edge => edge.DependentJobProfileId)
                    .Distinct()
                    .ToArray());

        return profiles
            .Select(profile => new JobProfileDependencyNodeData(
                profile.Id,
                profile.PublicId,
                profile.ReportsToJobProfileId,
                byProfile.TryGetValue(profile.Id, out var dependentIds)
                    ? dependentIds
                    : []))
            .ToArray();
    }

    public async Task<PagedResponse<JobProfileListItemResponse>> SearchAsync(
        Guid tenantId,
        JobProfileStatus? status,
        Guid? orgUnitId,
        Guid? salaryClassId,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = dbContext.JobProfiles
            .AsNoTracking()
            .Where(profile => profile.TenantId == tenantId);

        if (status.HasValue)
        {
            query = query.Where(profile => profile.Status == status.Value);
        }

        if (orgUnitId.HasValue)
        {
            var orgUnitInternalId = await dbContext.OrgUnits
                .AsNoTracking()
                .Where(unit => unit.TenantId == tenantId && unit.PublicId == orgUnitId.Value)
                .Select(unit => (long?)unit.Id)
                .SingleOrDefaultAsync(cancellationToken);

            if (!orgUnitInternalId.HasValue)
            {
                return new PagedResponse<JobProfileListItemResponse>([], pageNumber, pageSize, 0);
            }

            query = query.Where(profile => profile.OrgUnitId == orgUnitInternalId.Value);
        }

        if (salaryClassId.HasValue)
        {
            var salaryClassInternalId = await dbContext.PositionDescriptionCatalogItems
                .AsNoTracking()
                .Where(item => item.TenantId == tenantId &&
                               item.CatalogType == Domain.PositionDescriptionCatalogs.PositionDescriptionCatalogType.SalaryClass &&
                               item.PublicId == salaryClassId.Value)
                .Select(item => (long?)item.Id)
                .SingleOrDefaultAsync(cancellationToken);

            if (!salaryClassInternalId.HasValue)
            {
                return new PagedResponse<JobProfileListItemResponse>([], pageNumber, pageSize, 0);
            }

            var matchingClassCode = await dbContext.PositionDescriptionCatalogItems
                .AsNoTracking()
                .Where(item => item.Id == salaryClassInternalId.Value)
                .Select(item => item.NormalizedCode)
                .SingleOrDefaultAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(matchingClassCode))
            {
                return new PagedResponse<JobProfileListItemResponse>([], pageNumber, pageSize, 0);
            }

            query = from profile in query
                    join compensation in dbContext.JobProfileCompensations.AsNoTracking()
                        on profile.Id equals compensation.JobProfileId
                    join line in dbContext.SalaryTabulatorLines.AsNoTracking()
                        on compensation.SalaryTabulatorLineId equals line.Id
                    where line.NormalizedSalaryClassCode == matchingClassCode
                    select profile;
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(profile =>
                profile.NormalizedCode.Contains(normalizedSearch) ||
                profile.NormalizedTitle.Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await
            (from profile in query
             join orgUnit in dbContext.OrgUnits.AsNoTracking()
                 on profile.OrgUnitId equals orgUnit.Id
             orderby profile.Title, profile.Code
             select new JobProfileListItemResponse(
                 profile.PublicId,
                 profile.Code,
                 profile.Title,
                 profile.Status,
                 profile.Version,
                 orgUnit.PublicId,
                 orgUnit.Name,
                 profile.IsActive,
                 profile.ConcurrencyToken,
                 profile.CreatedUtc,
                 profile.ModifiedUtc))
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResponse<JobProfileListItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    public async Task<JobProfileCoreResponse?> GetCoreResponseByIdAsync(Guid profileId, CancellationToken cancellationToken)
    {
        var profile = await dbContext.JobProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.PublicId == profileId, cancellationToken);

        if (profile is null)
        {
            return null;
        }

        var orgUnitLookup = await dbContext.OrgUnits
            .AsNoTracking()
            .Where(unit => unit.Id == profile.OrgUnitId)
            .Select(unit => new { unit.PublicId, unit.Name })
            .SingleOrDefaultAsync(cancellationToken);

        var reportsToLookup = profile.ReportsToJobProfileId.HasValue
            ? await dbContext.JobProfiles
                .AsNoTracking()
                .Where(item => item.Id == profile.ReportsToJobProfileId.Value)
                .Select(item => new { item.PublicId, item.Code, item.Title })
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        var positionCategoryLookup = profile.PositionCategoryId.HasValue
            ? await dbContext.PositionCategories
                .AsNoTracking()
                .Where(item => item.Id == profile.PositionCategoryId.Value)
                .Select(item => new { item.PublicId })
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        var positionDescriptionCatalogItemIds = new HashSet<long>();
        AddIfPresent(positionDescriptionCatalogItemIds, profile.StrategicObjectiveCatalogItemId);
        AddIfPresent(positionDescriptionCatalogItemIds, profile.AssignedWorkEquipmentCatalogItemId);
        AddIfPresent(positionDescriptionCatalogItemIds, profile.ResponsibilityCatalogItemId);

        var positionDescriptionCatalogLookup = positionDescriptionCatalogItemIds.Count == 0
            ? new Dictionary<long, Guid>()
            : await dbContext.PositionDescriptionCatalogItems
                .AsNoTracking()
                .Where(item => positionDescriptionCatalogItemIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.PublicId, cancellationToken);

        var compensationItem = await GetCompensationResponseAsync(profile.Id, profile.TenantId, cancellationToken);

        return new JobProfileCoreResponse(
            profile.PublicId,
            profile.TenantId,
            profile.Code,
            profile.Title,
            profile.Status,
            profile.Version,
            profile.Objective,
            orgUnitLookup?.PublicId,
            orgUnitLookup?.Name,
            reportsToLookup?.PublicId,
            reportsToLookup?.Code,
            reportsToLookup?.Title,
            positionCategoryLookup?.PublicId,
            ResolveCatalogPublicId(profile.StrategicObjectiveCatalogItemId, positionDescriptionCatalogLookup),
            ResolveCatalogPublicId(profile.AssignedWorkEquipmentCatalogItemId, positionDescriptionCatalogLookup),
            ResolveCatalogPublicId(profile.ResponsibilityCatalogItemId, positionDescriptionCatalogLookup),
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
            compensationItem,
            profile.ConcurrencyToken,
            profile.CreatedUtc,
            profile.ModifiedUtc);
    }

    public async Task<JobProfileEntityResponse?> GetEntityResponseByIdAsync(Guid profileId, CancellationToken cancellationToken)
    {
        var profile = await dbContext.JobProfiles
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.PublicId == profileId, cancellationToken);

        if (profile is null)
        {
            return null;
        }

        var orgUnitPublicId = await dbContext.OrgUnits
            .AsNoTracking()
            .Where(unit => unit.Id == profile.OrgUnitId)
            .Select(unit => (Guid?)unit.PublicId)
            .SingleOrDefaultAsync(cancellationToken);

        var reportsToPublicId = profile.ReportsToJobProfileId.HasValue
            ? await dbContext.JobProfiles
                .AsNoTracking()
                .Where(item => item.Id == profile.ReportsToJobProfileId.Value)
                .Select(item => (Guid?)item.PublicId)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        var positionCategoryPublicId = profile.PositionCategoryId.HasValue
            ? await dbContext.PositionCategories
                .AsNoTracking()
                .Where(item => item.Id == profile.PositionCategoryId.Value)
                .Select(item => (Guid?)item.PublicId)
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        var positionDescriptionCatalogItemIds = new HashSet<long>();
        AddIfPresent(positionDescriptionCatalogItemIds, profile.StrategicObjectiveCatalogItemId);
        AddIfPresent(positionDescriptionCatalogItemIds, profile.AssignedWorkEquipmentCatalogItemId);
        AddIfPresent(positionDescriptionCatalogItemIds, profile.ResponsibilityCatalogItemId);

        var positionDescriptionCatalogLookup = positionDescriptionCatalogItemIds.Count == 0
            ? new Dictionary<long, Guid>()
            : await dbContext.PositionDescriptionCatalogItems
                .AsNoTracking()
                .Where(item => positionDescriptionCatalogItemIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.PublicId, cancellationToken);

        return new JobProfileEntityResponse(
            profile.PublicId,
            profile.TenantId,
            profile.Code,
            profile.Title,
            profile.Status,
            profile.Version,
            profile.Objective,
            orgUnitPublicId ?? Guid.Empty,
            reportsToPublicId,
            positionCategoryPublicId,
            ResolveCatalogPublicId(profile.StrategicObjectiveCatalogItemId, positionDescriptionCatalogLookup),
            ResolveCatalogPublicId(profile.AssignedWorkEquipmentCatalogItemId, positionDescriptionCatalogLookup),
            ResolveCatalogPublicId(profile.ResponsibilityCatalogItemId, positionDescriptionCatalogLookup),
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
            profile.ConcurrencyToken,
            profile.CreatedUtc,
            profile.ModifiedUtc);
    }

    public async Task<PagedResponse<JobProfileRequirementResponse>?> GetRequirementResponsesByProfileIdAsync(
        Guid profileId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var profileInternalId = await dbContext.JobProfiles
            .AsNoTracking()
            .Where(profile => profile.PublicId == profileId)
            .Select(profile => (long?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (!profileInternalId.HasValue)
        {
            return null;
        }

        var query =
            from requirement in dbContext.JobProfileRequirements.AsNoTracking()
            where requirement.JobProfileId == profileInternalId.Value
            join catalogItem in dbContext.JobCatalogItems.AsNoTracking()
                on requirement.CatalogItemId equals (long?)catalogItem.Id into catalogItems
            from catalogItem in catalogItems.DefaultIfEmpty()
            join requirementTypeItem in dbContext.PositionDescriptionCatalogItems.AsNoTracking()
                on requirement.RequirementTypeCatalogItemId equals (long?)requirementTypeItem.Id into requirementTypeItems
            from requirementTypeItem in requirementTypeItems.DefaultIfEmpty()
            orderby requirement.SortOrder, requirement.Description
            select new JobProfileRequirementResponse(
                requirement.PublicId,
                catalogItem == null ? null : catalogItem.PublicId,
                requirementTypeItem == null ? null : requirementTypeItem.PublicId,
                requirement.RequirementType,
                requirement.Description,
                requirement.SortOrder,
                requirement.ConcurrencyToken);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        return new PagedResponse<JobProfileRequirementResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<JobProfileRequirementResponse?> GetRequirementResponseAsync(
        Guid profileId,
        Guid requirementId,
        CancellationToken cancellationToken) =>
        (from profile in dbContext.JobProfiles.AsNoTracking()
         where profile.PublicId == profileId
         join requirement in dbContext.JobProfileRequirements.AsNoTracking()
             on profile.Id equals requirement.JobProfileId
         where requirement.PublicId == requirementId
         join catalogItem in dbContext.JobCatalogItems.AsNoTracking()
             on requirement.CatalogItemId equals (long?)catalogItem.Id into catalogItems
         from catalogItem in catalogItems.DefaultIfEmpty()
         join requirementTypeItem in dbContext.PositionDescriptionCatalogItems.AsNoTracking()
             on requirement.RequirementTypeCatalogItemId equals (long?)requirementTypeItem.Id into requirementTypeItems
         from requirementTypeItem in requirementTypeItems.DefaultIfEmpty()
         select new JobProfileRequirementResponse(
             requirement.PublicId,
             catalogItem == null ? null : catalogItem.PublicId,
             requirementTypeItem == null ? null : requirementTypeItem.PublicId,
             requirement.RequirementType,
             requirement.Description,
             requirement.SortOrder,
             requirement.ConcurrencyToken))
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<PagedResponse<JobProfileFunctionResponse>?> GetFunctionResponsesByProfileIdAsync(
        Guid profileId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var profileInternalId = await dbContext.JobProfiles
            .AsNoTracking()
            .Where(profile => profile.PublicId == profileId)
            .Select(profile => (long?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (!profileInternalId.HasValue)
        {
            return null;
        }

        var query =
            from function in dbContext.JobProfileFunctions.AsNoTracking()
            where function.JobProfileId == profileInternalId.Value
            join frequencyItem in dbContext.PositionDescriptionCatalogItems.AsNoTracking()
                on function.FrequencyCatalogItemId equals (long?)frequencyItem.Id into frequencyItems
            from frequencyItem in frequencyItems.DefaultIfEmpty()
            orderby function.SortOrder, function.Description
            select new JobProfileFunctionResponse(
                function.PublicId,
                frequencyItem == null ? null : frequencyItem.PublicId,
                function.FunctionType,
                function.Description,
                function.SortOrder,
                function.ConcurrencyToken);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        return new PagedResponse<JobProfileFunctionResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<JobProfileFunctionResponse?> GetFunctionResponseAsync(
        Guid profileId,
        Guid functionId,
        CancellationToken cancellationToken) =>
        (from profile in dbContext.JobProfiles.AsNoTracking()
         where profile.PublicId == profileId
         join function in dbContext.JobProfileFunctions.AsNoTracking()
             on profile.Id equals function.JobProfileId
         where function.PublicId == functionId
         join frequencyItem in dbContext.PositionDescriptionCatalogItems.AsNoTracking()
             on function.FrequencyCatalogItemId equals (long?)frequencyItem.Id into frequencyItems
         from frequencyItem in frequencyItems.DefaultIfEmpty()
         select new JobProfileFunctionResponse(
             function.PublicId,
             frequencyItem == null ? null : frequencyItem.PublicId,
             function.FunctionType,
             function.Description,
             function.SortOrder,
             function.ConcurrencyToken))
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<JobProfileRelationResponse>?> GetRelationResponsesByProfileIdAsync(
        Guid profileId,
        CancellationToken cancellationToken)
    {
        var profileInternalId = await dbContext.JobProfiles
            .AsNoTracking()
            .Where(profile => profile.PublicId == profileId)
            .Select(profile => (long?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (!profileInternalId.HasValue)
        {
            return null;
        }

        return await
            (from relation in dbContext.JobProfileRelations.AsNoTracking()
             where relation.JobProfileId == profileInternalId.Value
             join catalogItem in dbContext.JobCatalogItems.AsNoTracking()
                 on relation.CatalogItemId equals (long?)catalogItem.Id into catalogItems
             from catalogItem in catalogItems.DefaultIfEmpty()
             orderby relation.SortOrder, relation.Counterpart
             select new JobProfileRelationResponse(
                 relation.PublicId,
                 catalogItem == null ? null : catalogItem.PublicId,
                 relation.RelationType,
                 relation.Counterpart,
                 relation.Notes,
                 relation.SortOrder,
                 relation.ConcurrencyToken))
            .ToArrayAsync(cancellationToken);
    }

    public Task<JobProfileRelationResponse?> GetRelationResponseAsync(
        Guid profileId,
        Guid relationId,
        CancellationToken cancellationToken) =>
        (from profile in dbContext.JobProfiles.AsNoTracking()
         where profile.PublicId == profileId
         join relation in dbContext.JobProfileRelations.AsNoTracking()
             on profile.Id equals relation.JobProfileId
         where relation.PublicId == relationId
         join catalogItem in dbContext.JobCatalogItems.AsNoTracking()
             on relation.CatalogItemId equals (long?)catalogItem.Id into catalogItems
         from catalogItem in catalogItems.DefaultIfEmpty()
         select new JobProfileRelationResponse(
             relation.PublicId,
             catalogItem == null ? null : catalogItem.PublicId,
             relation.RelationType,
             relation.Counterpart,
             relation.Notes,
             relation.SortOrder,
             relation.ConcurrencyToken))
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<PagedResponse<JobProfileLegacyCompetencyResponse>?> GetLegacyCompetencyResponsesByProfileIdAsync(
        Guid profileId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var profileInternalId = await dbContext.JobProfiles
            .AsNoTracking()
            .Where(profile => profile.PublicId == profileId)
            .Select(profile => (long?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (!profileInternalId.HasValue)
        {
            return null;
        }

        var query =
            from competency in dbContext.JobProfileCompetencies.AsNoTracking()
            where competency.JobProfileId == profileInternalId.Value
            join catalogItem in dbContext.JobCatalogItems.AsNoTracking()
                on competency.CatalogItemId equals (long?)catalogItem.Id into catalogItems
            from catalogItem in catalogItems.DefaultIfEmpty()
            orderby competency.SortOrder, competency.Name
            select new JobProfileLegacyCompetencyResponse(
                competency.PublicId,
                catalogItem == null ? null : catalogItem.PublicId,
                competency.Name,
                competency.ExpectedLevel,
                competency.Notes,
                competency.SortOrder,
                competency.ConcurrencyToken);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        return new PagedResponse<JobProfileLegacyCompetencyResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<JobProfileLegacyCompetencyResponse?> GetLegacyCompetencyResponseAsync(
        Guid profileId,
        Guid competencyId,
        CancellationToken cancellationToken) =>
        (from profile in dbContext.JobProfiles.AsNoTracking()
         where profile.PublicId == profileId
         join competency in dbContext.JobProfileCompetencies.AsNoTracking()
             on profile.Id equals competency.JobProfileId
         where competency.PublicId == competencyId
         join catalogItem in dbContext.JobCatalogItems.AsNoTracking()
             on competency.CatalogItemId equals (long?)catalogItem.Id into catalogItems
         from catalogItem in catalogItems.DefaultIfEmpty()
         select new JobProfileLegacyCompetencyResponse(
             competency.PublicId,
             catalogItem == null ? null : catalogItem.PublicId,
             competency.Name,
             competency.ExpectedLevel,
             competency.Notes,
             competency.SortOrder,
             competency.ConcurrencyToken))
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<JobProfileTrainingResponse>?> GetTrainingResponsesByProfileIdAsync(
        Guid profileId,
        CancellationToken cancellationToken)
    {
        var profileInternalId = await dbContext.JobProfiles
            .AsNoTracking()
            .Where(profile => profile.PublicId == profileId)
            .Select(profile => (long?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (!profileInternalId.HasValue)
        {
            return null;
        }

        return await
            (from training in dbContext.Set<Domain.JobProfiles.JobProfileTraining>().AsNoTracking()
             where training.JobProfileId == profileInternalId.Value
             join catalogItem in dbContext.JobCatalogItems.AsNoTracking()
                 on training.CatalogItemId equals (long?)catalogItem.Id into catalogItems
             from catalogItem in catalogItems.DefaultIfEmpty()
             orderby training.SortOrder, training.Name
             select new JobProfileTrainingResponse(
                 training.PublicId,
                 catalogItem == null ? null : catalogItem.PublicId,
                 training.Name,
                 training.Notes,
                 training.SortOrder,
                 training.ConcurrencyToken))
            .ToArrayAsync(cancellationToken);
    }

    public Task<JobProfileTrainingResponse?> GetTrainingResponseAsync(
        Guid profileId,
        Guid trainingId,
        CancellationToken cancellationToken) =>
        (from profile in dbContext.JobProfiles.AsNoTracking()
         where profile.PublicId == profileId
         join training in dbContext.Set<Domain.JobProfiles.JobProfileTraining>().AsNoTracking()
             on profile.Id equals training.JobProfileId
         where training.PublicId == trainingId
         join catalogItem in dbContext.JobCatalogItems.AsNoTracking()
             on training.CatalogItemId equals (long?)catalogItem.Id into catalogItems
         from catalogItem in catalogItems.DefaultIfEmpty()
         select new JobProfileTrainingResponse(
             training.PublicId,
             catalogItem == null ? null : catalogItem.PublicId,
             training.Name,
             training.Notes,
             training.SortOrder,
             training.ConcurrencyToken))
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<JobProfileBenefitResponse>?> GetBenefitResponsesByProfileIdAsync(
        Guid profileId,
        CancellationToken cancellationToken)
    {
        var profileInternalId = await dbContext.JobProfiles
            .AsNoTracking()
            .Where(profile => profile.PublicId == profileId)
            .Select(profile => (long?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (!profileInternalId.HasValue)
        {
            return null;
        }

        return await
            (from benefit in dbContext.Set<Domain.JobProfiles.JobProfileBenefit>().AsNoTracking()
             where benefit.JobProfileId == profileInternalId.Value
             join catalogItem in dbContext.JobCatalogItems.AsNoTracking()
                 on benefit.CatalogItemId equals (long?)catalogItem.Id into catalogItems
             from catalogItem in catalogItems.DefaultIfEmpty()
             orderby benefit.SortOrder, benefit.Name
             select new JobProfileBenefitResponse(
                 benefit.PublicId,
                 catalogItem == null ? null : catalogItem.PublicId,
                 benefit.Name,
                 benefit.Notes,
                 benefit.SortOrder,
                 benefit.ConcurrencyToken))
            .ToArrayAsync(cancellationToken);
    }

    public Task<JobProfileBenefitResponse?> GetBenefitResponseAsync(
        Guid profileId,
        Guid benefitId,
        CancellationToken cancellationToken) =>
        (from profile in dbContext.JobProfiles.AsNoTracking()
         where profile.PublicId == profileId
         join benefit in dbContext.Set<Domain.JobProfiles.JobProfileBenefit>().AsNoTracking()
             on profile.Id equals benefit.JobProfileId
         where benefit.PublicId == benefitId
         join catalogItem in dbContext.JobCatalogItems.AsNoTracking()
             on benefit.CatalogItemId equals (long?)catalogItem.Id into catalogItems
         from catalogItem in catalogItems.DefaultIfEmpty()
         select new JobProfileBenefitResponse(
             benefit.PublicId,
             catalogItem == null ? null : catalogItem.PublicId,
             benefit.Name,
             benefit.Notes,
             benefit.SortOrder,
             benefit.ConcurrencyToken))
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<JobProfileWorkingConditionResponse>?> GetWorkingConditionResponsesByProfileIdAsync(
        Guid profileId,
        CancellationToken cancellationToken)
    {
        var profileInternalId = await dbContext.JobProfiles
            .AsNoTracking()
            .Where(profile => profile.PublicId == profileId)
            .Select(profile => (long?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (!profileInternalId.HasValue)
        {
            return null;
        }

        return await
            (from condition in dbContext.Set<Domain.JobProfiles.JobProfileWorkingCondition>().AsNoTracking()
             where condition.JobProfileId == profileInternalId.Value
             join catalogItem in dbContext.JobCatalogItems.AsNoTracking()
                 on condition.CatalogItemId equals (long?)catalogItem.Id into catalogItems
             from catalogItem in catalogItems.DefaultIfEmpty()
             join workConditionTypeItem in dbContext.PositionDescriptionCatalogItems.AsNoTracking()
                 on condition.WorkConditionTypeCatalogItemId equals (long?)workConditionTypeItem.Id into workConditionTypeItems
             from workConditionTypeItem in workConditionTypeItems.DefaultIfEmpty()
             orderby condition.SortOrder, condition.Name
             select new JobProfileWorkingConditionResponse(
                 condition.PublicId,
                 catalogItem == null ? null : catalogItem.PublicId,
                 workConditionTypeItem == null ? null : workConditionTypeItem.PublicId,
                 condition.Name,
                 condition.Notes,
                 condition.SortOrder,
                 condition.ConcurrencyToken))
            .ToArrayAsync(cancellationToken);
    }

    public Task<JobProfileWorkingConditionResponse?> GetWorkingConditionResponseAsync(
        Guid profileId,
        Guid workingConditionId,
        CancellationToken cancellationToken) =>
        (from profile in dbContext.JobProfiles.AsNoTracking()
         where profile.PublicId == profileId
         join condition in dbContext.Set<Domain.JobProfiles.JobProfileWorkingCondition>().AsNoTracking()
             on profile.Id equals condition.JobProfileId
         where condition.PublicId == workingConditionId
         join catalogItem in dbContext.JobCatalogItems.AsNoTracking()
             on condition.CatalogItemId equals (long?)catalogItem.Id into catalogItems
         from catalogItem in catalogItems.DefaultIfEmpty()
         join workConditionTypeItem in dbContext.PositionDescriptionCatalogItems.AsNoTracking()
             on condition.WorkConditionTypeCatalogItemId equals (long?)workConditionTypeItem.Id into workConditionTypeItems
         from workConditionTypeItem in workConditionTypeItems.DefaultIfEmpty()
         select new JobProfileWorkingConditionResponse(
             condition.PublicId,
             catalogItem == null ? null : catalogItem.PublicId,
             workConditionTypeItem == null ? null : workConditionTypeItem.PublicId,
             condition.Name,
             condition.Notes,
             condition.SortOrder,
             condition.ConcurrencyToken))
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<JobProfileDependentPositionResponse>?> GetDependentPositionResponsesByProfileIdAsync(
        Guid profileId,
        CancellationToken cancellationToken)
    {
        var profileInternalId = await dbContext.JobProfiles
            .AsNoTracking()
            .Where(profile => profile.PublicId == profileId)
            .Select(profile => (long?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken);

        if (!profileInternalId.HasValue)
        {
            return null;
        }

        return await
            (from position in dbContext.JobProfileDependentPositions.AsNoTracking()
             where position.JobProfileId == profileInternalId.Value
             join dependent in dbContext.JobProfiles.AsNoTracking()
                 on position.DependentJobProfileId equals dependent.Id
             orderby dependent.Title, dependent.Code
             select new JobProfileDependentPositionResponse(
                 position.PublicId,
                 dependent.PublicId,
                 dependent.Code,
                 dependent.Title,
                 position.Quantity,
                 position.Notes,
                 position.ConcurrencyToken))
            .ToArrayAsync(cancellationToken);
    }

    public Task<JobProfileDependentPositionResponse?> GetDependentPositionResponseAsync(
        Guid profileId,
        Guid dependentPositionId,
        CancellationToken cancellationToken) =>
        (from profile in dbContext.JobProfiles.AsNoTracking()
         where profile.PublicId == profileId
         join position in dbContext.JobProfileDependentPositions.AsNoTracking()
             on profile.Id equals position.JobProfileId
         where position.PublicId == dependentPositionId
         join dependent in dbContext.JobProfiles.AsNoTracking()
             on position.DependentJobProfileId equals dependent.Id
         select new JobProfileDependentPositionResponse(
             position.PublicId,
             dependent.PublicId,
             dependent.Code,
             dependent.Title,
             position.Quantity,
             position.Notes,
             position.ConcurrencyToken))
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<JobProfileResponse?> GetResponseByIdAsync(Guid profileId, CancellationToken cancellationToken)
    {
        var profile = await dbContext.JobProfiles
            .AsNoTracking()
            .AsSplitQuery()
            .Include(item => item.Requirements)
                .ThenInclude(item => item.CatalogItem)
            .Include(item => item.Functions)
            .Include(item => item.Relations)
                .ThenInclude(item => item.CatalogItem)
            .Include(item => item.Competencies)
                .ThenInclude(item => item.CatalogItem)
            .Include(item => item.Trainings)
                .ThenInclude(item => item.CatalogItem)
            .Include(item => item.Benefits)
                .ThenInclude(item => item.CatalogItem)
            .Include(item => item.WorkingConditions)
                .ThenInclude(item => item.CatalogItem)
            .Include(item => item.DependentPositions)
                .ThenInclude(item => item.DependentJobProfile)
            .SingleOrDefaultAsync(item => item.PublicId == profileId, cancellationToken);

        if (profile is null)
        {
            return null;
        }

        var orgUnitLookup = await dbContext.OrgUnits
            .AsNoTracking()
            .Where(unit => unit.Id == profile.OrgUnitId)
            .Select(unit => new { unit.PublicId, unit.Name })
            .SingleOrDefaultAsync(cancellationToken);

        var reportsToLookup = profile.ReportsToJobProfileId.HasValue
            ? await dbContext.JobProfiles
                .AsNoTracking()
                .Where(item => item.Id == profile.ReportsToJobProfileId.Value)
                .Select(item => new { item.PublicId, item.Code, item.Title })
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        var positionCategoryLookup = profile.PositionCategoryId.HasValue
            ? await dbContext.PositionCategories
                .AsNoTracking()
                .Where(item => item.Id == profile.PositionCategoryId.Value)
                .Select(item => new { item.PublicId })
                .SingleOrDefaultAsync(cancellationToken)
            : null;

        var positionDescriptionCatalogItemIds = new HashSet<long>();
        AddIfPresent(positionDescriptionCatalogItemIds, profile.StrategicObjectiveCatalogItemId);
        AddIfPresent(positionDescriptionCatalogItemIds, profile.AssignedWorkEquipmentCatalogItemId);
        AddIfPresent(positionDescriptionCatalogItemIds, profile.ResponsibilityCatalogItemId);

        foreach (var requirement in profile.Requirements)
        {
            AddIfPresent(positionDescriptionCatalogItemIds, requirement.RequirementTypeCatalogItemId);
        }

        foreach (var function in profile.Functions)
        {
            AddIfPresent(positionDescriptionCatalogItemIds, function.FrequencyCatalogItemId);
        }

        foreach (var workingCondition in profile.WorkingConditions)
        {
            AddIfPresent(positionDescriptionCatalogItemIds, workingCondition.WorkConditionTypeCatalogItemId);
        }

        var positionDescriptionCatalogLookup = positionDescriptionCatalogItemIds.Count == 0
            ? new Dictionary<long, Guid>()
            : await dbContext.PositionDescriptionCatalogItems
                .AsNoTracking()
                .Where(item => positionDescriptionCatalogItemIds.Contains(item.Id))
                .ToDictionaryAsync(item => item.Id, item => item.PublicId, cancellationToken);

        var requirementItems = profile.Requirements
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Description)
            .Select(item => new JobProfileRequirementResponse(
                item.PublicId,
                item.CatalogItem?.PublicId,
                ResolveCatalogPublicId(item.RequirementTypeCatalogItemId, positionDescriptionCatalogLookup),
                item.RequirementType,
                item.Description,
                item.SortOrder,
                item.ConcurrencyToken))
            .ToArray();

        var functionItems = profile.Functions
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Description)
            .Select(item => new JobProfileFunctionResponse(
                item.PublicId,
                ResolveCatalogPublicId(item.FrequencyCatalogItemId, positionDescriptionCatalogLookup),
                item.FunctionType,
                item.Description,
                item.SortOrder,
                item.ConcurrencyToken))
            .ToArray();

        var relationItems = profile.Relations
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Counterpart)
            .Select(item => new JobProfileRelationResponse(
                item.PublicId,
                item.CatalogItem?.PublicId,
                item.RelationType,
                item.Counterpart,
                item.Notes,
                item.SortOrder,
                item.ConcurrencyToken))
            .ToArray();

        var competencyItems = await GetCompetencyMatrixItemsAsync(profile.Id, cancellationToken);

        var trainingItems = profile.Trainings
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .Select(item => new JobProfileTrainingResponse(
                item.PublicId,
                item.CatalogItem?.PublicId,
                item.Name,
                item.Notes,
                item.SortOrder,
                item.ConcurrencyToken))
            .ToArray();

        var compensationItem = await GetCompensationResponseAsync(profile.Id, profile.TenantId, cancellationToken);

        var benefitItems = profile.Benefits
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .Select(item => new JobProfileBenefitResponse(
                item.PublicId,
                item.CatalogItem?.PublicId,
                item.Name,
                item.Notes,
                item.SortOrder,
                item.ConcurrencyToken))
            .ToArray();

        var workingConditionItems = profile.WorkingConditions
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .Select(item => new JobProfileWorkingConditionResponse(
                item.PublicId,
                item.CatalogItem?.PublicId,
                ResolveCatalogPublicId(item.WorkConditionTypeCatalogItemId, positionDescriptionCatalogLookup),
                item.Name,
                item.Notes,
                item.SortOrder,
                item.ConcurrencyToken))
            .ToArray();

        var dependentPositionItems = profile.DependentPositions
            .OrderBy(item => item.DependentJobProfile.Title)
            .ThenBy(item => item.DependentJobProfile.Code)
            .Select(item => new JobProfileDependentPositionResponse(
                item.PublicId,
                item.DependentJobProfile.PublicId,
                item.DependentJobProfile.Code,
                item.DependentJobProfile.Title,
                item.Quantity,
                item.Notes,
                item.ConcurrencyToken))
            .ToArray();

        return new JobProfileResponse(
            profile.PublicId,
            profile.TenantId,
            profile.Code,
            profile.Title,
            profile.Status,
            profile.Version,
            profile.Objective,
            orgUnitLookup?.PublicId,
            orgUnitLookup?.Name,
            reportsToLookup?.PublicId,
            reportsToLookup?.Code,
            reportsToLookup?.Title,
            positionCategoryLookup?.PublicId,
            ResolveCatalogPublicId(profile.StrategicObjectiveCatalogItemId, positionDescriptionCatalogLookup),
            ResolveCatalogPublicId(profile.AssignedWorkEquipmentCatalogItemId, positionDescriptionCatalogLookup),
            ResolveCatalogPublicId(profile.ResponsibilityCatalogItemId, positionDescriptionCatalogLookup),
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
            requirementItems,
            functionItems,
            relationItems,
            competencyItems,
            trainingItems,
            compensationItem,
            benefitItems,
            workingConditionItems,
            dependentPositionItems,
            profile.ConcurrencyToken,
            profile.CreatedUtc,
            profile.ModifiedUtc);
    }

    private static void AddIfPresent(ISet<long> target, long? value)
    {
        if (value.HasValue)
        {
            _ = target.Add(value.Value);
        }
    }

    private static Guid? ResolveCatalogPublicId(long? internalId, IReadOnlyDictionary<long, Guid> lookup) =>
        internalId.HasValue && lookup.TryGetValue(internalId.Value, out var publicId) ? publicId : null;

    private async Task<IReadOnlyCollection<JobProfileCompetencyResponse>> GetCompetencyMatrixItemsAsync(
        long jobProfileInternalId,
        CancellationToken cancellationToken)
    {
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
             where expectation.JobProfileId == jobProfileInternalId
             orderby expectation.SortOrder, link.SortOrder
             select new
             {
                 ExpectationId = expectation.Id,
                 ExpectationPublicId = expectation.PublicId,
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

        return rows
            .GroupBy(row => row.ExpectationId)
            .OrderBy(group => group.First().SortOrder)
            .Select(group =>
            {
                var head = group.First();
                var conducts = group
                    .Where(row => row.ConductId.HasValue)
                    .Select(row => new JobProfileCompetencyConductResponse(
                        row.ConductId!.Value,
                        row.ConductDescription!,
                        row.ConductSortOrder ?? 0))
                    .ToArray();

                return new JobProfileCompetencyResponse(
                    head.ExpectationPublicId,
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
    }



    public async Task<JobProfilePrintResponse?> GetPrintByIdAsync(Guid profileId, CancellationToken cancellationToken)
    {
        var profile = await GetResponseByIdAsync(profileId, cancellationToken);
        return profile is null
            ? null
            : new JobProfilePrintResponse(profile, DateTime.UtcNow);
    }

    private async Task<JobProfileCompensationResponse?> GetCompensationResponseAsync(
        long profileInternalId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var compensation = await
            (from item in dbContext.JobProfileCompensations.AsNoTracking()
             where item.JobProfileId == profileInternalId
             join line in dbContext.SalaryTabulatorLines.AsNoTracking()
                 on item.SalaryTabulatorLineId equals line.Id
             select new
             {
                 line.PublicId,
                 line.SalaryClassCode,
                 line.NormalizedSalaryClassCode,
                 line.SalaryScaleCode,
                 line.CurrencyCode,
                 line.BaseAmount,
                 line.MinAmount,
                 line.MaxAmount,
                 line.EffectiveFromUtc,
                 line.EffectiveToUtc
             })
            .SingleOrDefaultAsync(cancellationToken);

        if (compensation is null)
        {
            return null;
        }

        var salaryClassLookup = await dbContext.PositionDescriptionCatalogItems
            .AsNoTracking()
            .Where(item =>
                item.TenantId == tenantId &&
                item.CatalogType == Domain.PositionDescriptionCatalogs.PositionDescriptionCatalogType.SalaryClass &&
                item.NormalizedCode == compensation.NormalizedSalaryClassCode)
            .Select(item => new { item.PublicId, item.Name })
            .FirstOrDefaultAsync(cancellationToken);

        return new JobProfileCompensationResponse(
            salaryClassLookup?.PublicId,
            salaryClassLookup?.Name,
            compensation.SalaryScaleCode,
            compensation.PublicId,
            compensation.CurrencyCode,
            compensation.BaseAmount,
            compensation.MinAmount,
            compensation.MaxAmount,
            compensation.EffectiveFromUtc,
            compensation.EffectiveToUtc);
    }
}

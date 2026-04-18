using CLARIHR.Application.Abstractions.JobProfiles;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.JobProfiles;
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
            .Include(profile => profile.SalaryClassCatalogItem)
            .Include(profile => profile.Benefits)
                .ThenInclude(benefit => benefit.CatalogItem)
            .Include(profile => profile.WorkingConditions)
                .ThenInclude(condition => condition.CatalogItem)
            .Include(profile => profile.DependentPositions)
                .ThenInclude(position => position.DependentJobProfile)
            .SingleOrDefaultAsync(profile => profile.PublicId == profileId, cancellationToken);

    public Task<bool> ExistsOutsideTenantAsync(Guid profileId, CancellationToken cancellationToken) =>
        dbContext.JobProfiles
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
            .IgnoreQueryFilters()
            .AnyAsync(unit => unit.PublicId == orgUnitId, cancellationToken);

    public Task<long?> ResolveProfileIdAsync(Guid tenantId, Guid profileId, CancellationToken cancellationToken) =>
        dbContext.JobProfiles
            .AsNoTracking()
            .Where(profile => profile.TenantId == tenantId && profile.PublicId == profileId)
            .Select(profile => (long?)profile.Id)
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

            query = query.Where(profile => profile.SalaryClassCatalogItemId == salaryClassInternalId.Value);
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
            .Include(item => item.SalaryClassCatalogItem)
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
        AddIfPresent(positionDescriptionCatalogItemIds, profile.SalaryClassCatalogItemId);

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
                item.CatalogItem?.PublicId,
                ResolveCatalogPublicId(item.RequirementTypeCatalogItemId, positionDescriptionCatalogLookup),
                item.RequirementType,
                item.Description,
                item.SortOrder))
            .ToArray();

        var functionItems = profile.Functions
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Description)
            .Select(item => new JobProfileFunctionResponse(
                item.FunctionType,
                ResolveCatalogPublicId(item.FrequencyCatalogItemId, positionDescriptionCatalogLookup),
                item.Description,
                item.SortOrder))
            .ToArray();

        var relationItems = profile.Relations
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Counterpart)
            .Select(item => new JobProfileRelationResponse(
                item.CatalogItem?.PublicId,
                item.RelationType,
                item.Counterpart,
                item.Notes,
                item.SortOrder))
            .ToArray();

        var competencyItems = profile.Competencies
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .Select(item => new JobProfileCompetencyResponse(
                item.CatalogItem?.PublicId,
                item.Name,
                item.ExpectedLevel,
                item.Notes,
                item.SortOrder))
            .ToArray();

        var trainingItems = profile.Trainings
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .Select(item => new JobProfileTrainingResponse(
                item.CatalogItem?.PublicId,
                item.Name,
                item.Notes,
                item.SortOrder))
            .ToArray();

        JobProfileCompensationResponse? compensationItem = null;
        if (profile.SalaryClassCatalogItemId.HasValue &&
            !string.IsNullOrWhiteSpace(profile.SalaryScaleCode) &&
            !string.IsNullOrWhiteSpace(profile.NormalizedSalaryScaleCode))
        {
            var normalizedSalaryClassCode = profile.SalaryClassCatalogItem?.Code.Trim().ToUpperInvariant();
            SalaryTabulatorResolution? resolution = null;
            if (!string.IsNullOrWhiteSpace(normalizedSalaryClassCode))
            {
                var effectiveAtUtc = (profile.EffectiveFromUtc ?? DateTime.UtcNow).Date;
                resolution = await dbContext.SalaryTabulatorLines
                    .AsNoTracking()
                    .Where(line =>
                        line.TenantId == profile.TenantId &&
                        line.NormalizedSalaryClassCode == normalizedSalaryClassCode &&
                        line.NormalizedSalaryScaleCode == profile.NormalizedSalaryScaleCode &&
                        line.EffectiveFromUtc <= effectiveAtUtc &&
                        (!line.EffectiveToUtc.HasValue || line.EffectiveToUtc.Value >= effectiveAtUtc))
                    .OrderByDescending(line => line.EffectiveFromUtc)
                    .Select(line => new SalaryTabulatorResolution(
                        line.PublicId,
                        line.CurrencyCode,
                        line.BaseAmount,
                        line.MinAmount,
                        line.MaxAmount,
                        line.EffectiveFromUtc,
                        line.EffectiveToUtc))
                    .FirstOrDefaultAsync(cancellationToken);
            }

            compensationItem = new JobProfileCompensationResponse(
                ResolveCatalogPublicId(profile.SalaryClassCatalogItemId, positionDescriptionCatalogLookup),
                profile.SalaryClassCatalogItem?.Name,
                profile.SalaryScaleCode,
                resolution?.Id,
                resolution?.CurrencyCode,
                resolution?.BaseAmount,
                resolution?.MinAmount,
                resolution?.MaxAmount,
                resolution?.EffectiveFromUtc,
                resolution?.EffectiveToUtc);
        }

        var benefitItems = profile.Benefits
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .Select(item => new JobProfileBenefitResponse(
                item.CatalogItem?.PublicId,
                item.Name,
                item.Notes,
                item.SortOrder))
            .ToArray();

        var workingConditionItems = profile.WorkingConditions
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .Select(item => new JobProfileWorkingConditionResponse(
                item.CatalogItem?.PublicId,
                ResolveCatalogPublicId(item.WorkConditionTypeCatalogItemId, positionDescriptionCatalogLookup),
                item.Name,
                item.Notes,
                item.SortOrder))
            .ToArray();

        var dependentPositionItems = profile.DependentPositions
            .OrderBy(item => item.DependentJobProfile.Title)
            .ThenBy(item => item.DependentJobProfile.Code)
            .Select(item => new JobProfileDependentPositionResponse(
                item.DependentJobProfile.PublicId,
                item.DependentJobProfile.Code,
                item.DependentJobProfile.Title,
                item.Quantity,
                item.Notes))
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

    public async Task<JobProfileVacancyTemplateResponse?> GetVacancyTemplateByIdAsync(Guid profileId, CancellationToken cancellationToken)
    {
        var profile = await GetResponseByIdAsync(profileId, cancellationToken);
        if (profile is null)
        {
            return null;
        }

        return new JobProfileVacancyTemplateResponse(
            profile.Id,
            profile.Code,
            profile.Title,
            profile.Objective,
            profile.Responsibilities,
            profile.WorkingConditionSummary,
            profile.BenefitsSummary,
            profile.Requirements,
            profile.Functions,
            profile.Competencies,
            profile.Trainings);
    }

    public async Task<JobProfilePrintResponse?> GetPrintByIdAsync(Guid profileId, CancellationToken cancellationToken)
    {
        var profile = await GetResponseByIdAsync(profileId, cancellationToken);
        return profile is null
            ? null
            : new JobProfilePrintResponse(profile, DateTime.UtcNow);
    }

    private sealed record SalaryTabulatorResolution(
        Guid Id,
        string CurrencyCode,
        decimal BaseAmount,
        decimal? MinAmount,
        decimal? MaxAmount,
        DateTime EffectiveFromUtc,
        DateTime? EffectiveToUtc);
}

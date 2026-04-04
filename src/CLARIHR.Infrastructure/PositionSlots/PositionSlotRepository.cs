using CLARIHR.Application.Abstractions.PositionSlots;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PositionSlots;
using CLARIHR.Domain.PositionSlots;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.PositionSlots;

internal sealed class PositionSlotRepository(ApplicationDbContext dbContext) : IPositionSlotRepository
{
    public void Add(PositionSlot slot) => dbContext.Add(slot);

    public Task<PositionSlot?> GetByIdAsync(Guid slotId, CancellationToken cancellationToken) =>
        dbContext.Set<PositionSlot>().SingleOrDefaultAsync(slot => slot.PublicId == slotId, cancellationToken);

    public Task<bool> ExistsOutsideTenantAsync(Guid slotId, CancellationToken cancellationToken) =>
        dbContext.Set<PositionSlot>()
            .IgnoreQueryFilters()
            .AnyAsync(slot => slot.PublicId == slotId, cancellationToken);

    public Task<bool> CodeExistsAsync(Guid tenantId, string normalizedCode, long? excludingSlotId, CancellationToken cancellationToken) =>
        dbContext.Set<PositionSlot>().AnyAsync(
            slot => slot.TenantId == tenantId &&
                    slot.NormalizedCode == normalizedCode &&
                    (!excludingSlotId.HasValue || slot.Id != excludingSlotId.Value),
            cancellationToken);

    public Task<long?> ResolveJobProfileIdAsync(Guid tenantId, Guid jobProfileId, CancellationToken cancellationToken) =>
        dbContext.JobProfiles
            .AsNoTracking()
            .Where(profile => profile.TenantId == tenantId && profile.PublicId == jobProfileId)
            .Select(profile => (long?)profile.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> JobProfileExistsOutsideTenantAsync(Guid jobProfileId, CancellationToken cancellationToken) =>
        dbContext.JobProfiles
            .IgnoreQueryFilters()
            .AnyAsync(profile => profile.PublicId == jobProfileId, cancellationToken);

    public Task<long?> ResolveWorkCenterIdAsync(Guid tenantId, Guid workCenterId, CancellationToken cancellationToken) =>
        dbContext.WorkCenters
            .AsNoTracking()
            .Where(center => center.TenantId == tenantId && center.PublicId == workCenterId)
            .Select(center => (long?)center.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<bool> WorkCenterExistsOutsideTenantAsync(Guid workCenterId, CancellationToken cancellationToken) =>
        dbContext.WorkCenters
            .IgnoreQueryFilters()
            .AnyAsync(center => center.PublicId == workCenterId, cancellationToken);

    public Task<long?> ResolvePositionSlotIdAsync(Guid tenantId, Guid slotId, CancellationToken cancellationToken) =>
        dbContext.Set<PositionSlot>()
            .AsNoTracking()
            .Where(slot => slot.TenantId == tenantId && slot.PublicId == slotId)
            .Select(slot => (long?)slot.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PagedResponse<PositionSlotListItemResponse>> SearchAsync(
        Guid tenantId,
        PositionSlotStatus? status,
        Guid? jobProfileId,
        Guid? orgUnitId,
        Guid? workCenterId,
        Guid? contractTypeId,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query =
            from slot in dbContext.Set<PositionSlot>().AsNoTracking()
            join jobProfile in dbContext.JobProfiles.AsNoTracking() on slot.JobProfileId equals jobProfile.Id
            join orgUnit in dbContext.OrgUnits.AsNoTracking() on jobProfile.OrgUnitId equals orgUnit.Id
            join workCenter in dbContext.WorkCenters.AsNoTracking() on slot.WorkCenterId equals workCenter.Id into workCenterGroup
            from workCenter in workCenterGroup.DefaultIfEmpty()
            join positionCategory in dbContext.PositionCategories.AsNoTracking()
                on jobProfile.PositionCategoryId equals positionCategory.Id into positionCategoryGroup
            from positionCategory in positionCategoryGroup.DefaultIfEmpty()
            join classification in dbContext.PositionCategoryClassifications.AsNoTracking()
                on positionCategory.PositionCategoryClassificationId equals classification.Id into classificationGroup
            from classification in classificationGroup.DefaultIfEmpty()
            join contractType in dbContext.PositionDescriptionCatalogItems.AsNoTracking()
                on classification.PositionContractCatalogItemId equals contractType.Id into contractTypeGroup
            from contractType in contractTypeGroup.DefaultIfEmpty()
            where slot.TenantId == tenantId
            select new
            {
                Slot = slot,
                JobProfile = jobProfile,
                OrgUnit = orgUnit,
                WorkCenter = workCenter,
                PositionCategory = positionCategory,
                Classification = classification,
                ContractType = contractType
            };

        if (status.HasValue)
        {
            query = query.Where(item => item.Slot.Status == status.Value);
        }

        if (jobProfileId.HasValue)
        {
            query = query.Where(item => item.JobProfile.PublicId == jobProfileId.Value);
        }

        if (contractTypeId.HasValue)
        {
            query = query.Where(item => item.ContractType != null && item.ContractType.PublicId == contractTypeId.Value);
        }

        if (orgUnitId.HasValue)
        {
            query = query.Where(item => item.OrgUnit.PublicId == orgUnitId.Value);
        }

        if (workCenterId.HasValue)
        {
            query = query.Where(item => item.WorkCenter != null && item.WorkCenter.PublicId == workCenterId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(item =>
                item.Slot.NormalizedCode.Contains(normalizedSearch) ||
                (item.Slot.Title != null && item.Slot.Title.ToUpper().Contains(normalizedSearch)) ||
                item.JobProfile.NormalizedCode.Contains(normalizedSearch) ||
                item.JobProfile.NormalizedTitle.Contains(normalizedSearch) ||
                item.OrgUnit.NormalizedCode.Contains(normalizedSearch) ||
                item.OrgUnit.NormalizedName.Contains(normalizedSearch) ||
                (item.WorkCenter != null &&
                 (item.WorkCenter.NormalizedCode.Contains(normalizedSearch) || item.WorkCenter.NormalizedName.Contains(normalizedSearch))));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.Slot.Code)
            .ThenBy(item => item.Slot.Title)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new PositionSlotListItemResponse(
                item.Slot.PublicId,
                item.Slot.Code,
                item.Slot.Title,
                item.Slot.Status,
                item.JobProfile.PublicId,
                item.JobProfile.Code,
                item.JobProfile.Title,
                item.OrgUnit.PublicId,
                item.OrgUnit.Name,
                item.WorkCenter != null ? item.WorkCenter.PublicId : null,
                item.WorkCenter != null ? item.WorkCenter.Name : null,
                item.ContractType != null ? item.ContractType.PublicId : null,
                item.ContractType != null ? item.ContractType.Code : null,
                item.ContractType != null ? item.ContractType.Name : null,
                item.Slot.MaxEmployees,
                item.Slot.OccupiedEmployees,
                item.Slot.EffectiveFromUtc,
                item.Slot.EffectiveToUtc,
                item.Slot.IsActive,
                item.Slot.ConcurrencyToken,
                item.Slot.CreatedUtc,
                item.Slot.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<PositionSlotListItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<PositionSlotResponse?> GetResponseByIdAsync(Guid slotId, CancellationToken cancellationToken) =>
        (from slot in dbContext.Set<PositionSlot>().AsNoTracking()
         join jobProfile in dbContext.JobProfiles.AsNoTracking() on slot.JobProfileId equals jobProfile.Id
         join orgUnit in dbContext.OrgUnits.AsNoTracking() on jobProfile.OrgUnitId equals orgUnit.Id
         join workCenter in dbContext.WorkCenters.AsNoTracking() on slot.WorkCenterId equals workCenter.Id into workCenterGroup
         from workCenter in workCenterGroup.DefaultIfEmpty()
         join directDependency in dbContext.Set<PositionSlot>().AsNoTracking() on slot.DirectDependencyPositionSlotId equals directDependency.Id into directGroup
         from directDependency in directGroup.DefaultIfEmpty()
         join functionalDependency in dbContext.Set<PositionSlot>().AsNoTracking() on slot.FunctionalDependencyPositionSlotId equals functionalDependency.Id into functionalGroup
         from functionalDependency in functionalGroup.DefaultIfEmpty()
         join positionCategory in dbContext.PositionCategories.AsNoTracking()
            on jobProfile.PositionCategoryId equals positionCategory.Id into positionCategoryGroup
         from positionCategory in positionCategoryGroup.DefaultIfEmpty()
         join classification in dbContext.PositionCategoryClassifications.AsNoTracking()
            on positionCategory.PositionCategoryClassificationId equals classification.Id into classificationGroup
         from classification in classificationGroup.DefaultIfEmpty()
         join contractType in dbContext.PositionDescriptionCatalogItems.AsNoTracking()
            on classification.PositionContractCatalogItemId equals contractType.Id into contractTypeGroup
         from contractType in contractTypeGroup.DefaultIfEmpty()
         where slot.PublicId == slotId
         select new PositionSlotResponse(
             slot.PublicId,
             slot.TenantId,
             slot.Code,
             slot.Title,
             slot.Status,
             jobProfile.PublicId,
             jobProfile.Code,
             jobProfile.Title,
             orgUnit.PublicId,
             orgUnit.Name,
             workCenter != null ? workCenter.PublicId : null,
             workCenter != null ? workCenter.Name : null,
             orgUnit.CostCenterCode,
             directDependency != null ? directDependency.PublicId : null,
             directDependency != null ? directDependency.Code : null,
             functionalDependency != null ? functionalDependency.PublicId : null,
             functionalDependency != null ? functionalDependency.Code : null,
             positionCategory != null ? positionCategory.PublicId : null,
             classification != null ? classification.PublicId : null,
             contractType != null ? contractType.PublicId : null,
             contractType != null ? contractType.Code : null,
             contractType != null ? contractType.Name : null,
             slot.MaxEmployees,
             slot.OccupiedEmployees,
             slot.EffectiveFromUtc,
             slot.EffectiveToUtc,
             slot.Notes,
             slot.IsActive,
             slot.ConcurrencyToken,
             slot.CreatedUtc,
             slot.ModifiedUtc))
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<PositionSlotGraphNodeData>> GetGraphNodesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var nodes = await
            (from slot in dbContext.Set<PositionSlot>().AsNoTracking()
             join jobProfile in dbContext.JobProfiles.AsNoTracking() on slot.JobProfileId equals jobProfile.Id
             join orgUnit in dbContext.OrgUnits.AsNoTracking() on jobProfile.OrgUnitId equals orgUnit.Id
             join workCenter in dbContext.WorkCenters.AsNoTracking() on slot.WorkCenterId equals workCenter.Id into workCenterGroup
            from workCenter in workCenterGroup.DefaultIfEmpty()
             join positionCategory in dbContext.PositionCategories.AsNoTracking()
                on jobProfile.PositionCategoryId equals positionCategory.Id into positionCategoryGroup
             from positionCategory in positionCategoryGroup.DefaultIfEmpty()
             join classification in dbContext.PositionCategoryClassifications.AsNoTracking()
                on positionCategory.PositionCategoryClassificationId equals classification.Id into classificationGroup
             from classification in classificationGroup.DefaultIfEmpty()
             join contractType in dbContext.PositionDescriptionCatalogItems.AsNoTracking()
                on classification.PositionContractCatalogItemId equals contractType.Id into contractTypeGroup
             from contractType in contractTypeGroup.DefaultIfEmpty()
             where slot.TenantId == tenantId
             select new PositionSlotGraphNodeData(
                 slot.Id,
                 slot.PublicId,
                 slot.Code,
                 slot.Title ?? jobProfile.Title,
                 slot.Status,
                 jobProfile.PublicId,
                 orgUnit.PublicId,
                 workCenter != null ? workCenter.PublicId : null,
                 slot.DirectDependencyPositionSlotId,
                 null,
                 slot.FunctionalDependencyPositionSlotId,
                 null,
                 contractType != null ? contractType.PublicId : null,
                 contractType != null ? contractType.Code : null,
                 slot.IsActive))
            .ToListAsync(cancellationToken);

        var idByInternalId = nodes.ToDictionary(static node => node.InternalId, static node => node.Id);

        return nodes
            .Select(node => node with
            {
                DirectDependencyId = node.DirectDependencyInternalId.HasValue && idByInternalId.TryGetValue(node.DirectDependencyInternalId.Value, out var directId)
                    ? directId
                    : null,
                FunctionalDependencyId = node.FunctionalDependencyInternalId.HasValue && idByInternalId.TryGetValue(node.FunctionalDependencyInternalId.Value, out var functionalId)
                    ? functionalId
                    : null
            })
            .ToArray();
    }

    public async Task<IReadOnlyCollection<PositionSlotExportRow>> GetExportRowsAsync(
        Guid tenantId,
        PositionSlotStatus? status,
        Guid? jobProfileId,
        Guid? orgUnitId,
        Guid? workCenterId,
        Guid? contractTypeId,
        string? search,
        CancellationToken cancellationToken)
    {
        var query =
            from slot in dbContext.Set<PositionSlot>().AsNoTracking()
            join jobProfile in dbContext.JobProfiles.AsNoTracking() on slot.JobProfileId equals jobProfile.Id
            join orgUnit in dbContext.OrgUnits.AsNoTracking() on jobProfile.OrgUnitId equals orgUnit.Id
            join workCenter in dbContext.WorkCenters.AsNoTracking() on slot.WorkCenterId equals workCenter.Id into workCenterGroup
            from workCenter in workCenterGroup.DefaultIfEmpty()
            join directDependency in dbContext.Set<PositionSlot>().AsNoTracking() on slot.DirectDependencyPositionSlotId equals directDependency.Id into directGroup
            from directDependency in directGroup.DefaultIfEmpty()
            join functionalDependency in dbContext.Set<PositionSlot>().AsNoTracking() on slot.FunctionalDependencyPositionSlotId equals functionalDependency.Id into functionalGroup
            from functionalDependency in functionalGroup.DefaultIfEmpty()
            join positionCategory in dbContext.PositionCategories.AsNoTracking()
                on jobProfile.PositionCategoryId equals positionCategory.Id into positionCategoryGroup
            from positionCategory in positionCategoryGroup.DefaultIfEmpty()
            join classification in dbContext.PositionCategoryClassifications.AsNoTracking()
                on positionCategory.PositionCategoryClassificationId equals classification.Id into classificationGroup
            from classification in classificationGroup.DefaultIfEmpty()
            join contractType in dbContext.PositionDescriptionCatalogItems.AsNoTracking()
                on classification.PositionContractCatalogItemId equals contractType.Id into contractTypeGroup
            from contractType in contractTypeGroup.DefaultIfEmpty()
            where slot.TenantId == tenantId
            select new
            {
                Slot = slot,
                JobProfile = jobProfile,
                OrgUnit = orgUnit,
                WorkCenter = workCenter,
                DirectDependency = directDependency,
                FunctionalDependency = functionalDependency,
                ContractType = contractType
            };

        if (status.HasValue)
        {
            query = query.Where(item => item.Slot.Status == status.Value);
        }

        if (jobProfileId.HasValue)
        {
            query = query.Where(item => item.JobProfile.PublicId == jobProfileId.Value);
        }

        if (contractTypeId.HasValue)
        {
            query = query.Where(item => item.ContractType != null && item.ContractType.PublicId == contractTypeId.Value);
        }

        if (orgUnitId.HasValue)
        {
            query = query.Where(item => item.OrgUnit.PublicId == orgUnitId.Value);
        }

        if (workCenterId.HasValue)
        {
            query = query.Where(item => item.WorkCenter != null && item.WorkCenter.PublicId == workCenterId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(item =>
                item.Slot.NormalizedCode.Contains(normalizedSearch) ||
                (item.Slot.Title != null && item.Slot.Title.ToUpper().Contains(normalizedSearch)) ||
                item.JobProfile.NormalizedCode.Contains(normalizedSearch) ||
                item.JobProfile.NormalizedTitle.Contains(normalizedSearch) ||
                item.OrgUnit.NormalizedCode.Contains(normalizedSearch) ||
                item.OrgUnit.NormalizedName.Contains(normalizedSearch) ||
                (item.WorkCenter != null &&
                 (item.WorkCenter.NormalizedCode.Contains(normalizedSearch) || item.WorkCenter.NormalizedName.Contains(normalizedSearch))));
        }

        return await query
            .OrderBy(item => item.Slot.Code)
            .ThenBy(item => item.Slot.Title)
            .Select(item => new PositionSlotExportRow(
                item.Slot.PublicId,
                item.Slot.Code,
                item.Slot.Title,
                item.Slot.Status,
                item.JobProfile.Code,
                item.JobProfile.Title,
                item.OrgUnit.Code,
                item.OrgUnit.Name,
                item.WorkCenter != null ? item.WorkCenter.Code : null,
                item.WorkCenter != null ? item.WorkCenter.Name : null,
                item.OrgUnit.CostCenterCode,
                item.DirectDependency != null ? item.DirectDependency.Code : null,
                item.FunctionalDependency != null ? item.FunctionalDependency.Code : null,
                item.ContractType != null ? item.ContractType.PublicId : null,
                item.ContractType != null ? item.ContractType.Code : null,
                item.ContractType != null ? item.ContractType.Name : null,
                item.Slot.MaxEmployees,
                item.Slot.OccupiedEmployees,
                item.Slot.EffectiveFromUtc,
                item.Slot.EffectiveToUtc,
                item.Slot.IsActive,
                item.Slot.CreatedUtc,
                item.Slot.ModifiedUtc))
            .ToArrayAsync(cancellationToken);
    }

    public Task<PositionSlotJobProfileLookup?> GetJobProfileLookupAsync(
        Guid tenantId,
        Guid jobProfileId,
        CancellationToken cancellationToken) =>
        (from profile in dbContext.JobProfiles.AsNoTracking()
         join orgUnit in dbContext.OrgUnits.AsNoTracking()
             on profile.OrgUnitId equals orgUnit.Id into orgUnitGroup
         from orgUnit in orgUnitGroup.DefaultIfEmpty()
         join positionCategory in dbContext.PositionCategories.AsNoTracking()
             on profile.PositionCategoryId equals positionCategory.Id into positionCategoryGroup
         from positionCategory in positionCategoryGroup.DefaultIfEmpty()
         join classification in dbContext.PositionCategoryClassifications.AsNoTracking()
             on positionCategory.PositionCategoryClassificationId equals classification.Id into classificationGroup
         from classification in classificationGroup.DefaultIfEmpty()
         join contractType in dbContext.PositionDescriptionCatalogItems.AsNoTracking()
             on classification.PositionContractCatalogItemId equals contractType.Id into contractTypeGroup
         from contractType in contractTypeGroup.DefaultIfEmpty()
         where profile.TenantId == tenantId && profile.PublicId == jobProfileId
         select new PositionSlotJobProfileLookup(
             profile.Id,
             profile.PublicId,
             orgUnit != null ? orgUnit.PublicId : null,
             orgUnit != null ? orgUnit.Name : null,
             orgUnit != null ? orgUnit.CostCenterCode : null,
             positionCategory != null ? positionCategory.PublicId : null,
             classification != null ? classification.PublicId : null,
             contractType != null ? contractType.PublicId : null,
             contractType != null ? contractType.Code : null,
             contractType != null ? contractType.Name : null))
        .SingleOrDefaultAsync(cancellationToken);
}

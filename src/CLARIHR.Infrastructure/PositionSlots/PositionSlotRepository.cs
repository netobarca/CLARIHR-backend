using System.Linq.Expressions;
using CLARIHR.Application.Abstractions.PositionSlots;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PositionSlots;
using CLARIHR.Domain.IdentityAccess;
using CLARIHR.Domain.JobProfiles;
using CLARIHR.Domain.Locations;
using CLARIHR.Domain.OrgUnits;
using CLARIHR.Domain.PositionDescriptionCatalogs;
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
            // Intentional tenant filter bypass: checks cross-tenant existence only for tenant-mismatch errors.
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
            // Intentional tenant filter bypass: checks cross-tenant existence only for tenant-mismatch errors.
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
            // Intentional tenant filter bypass: checks cross-tenant existence only for tenant-mismatch errors.
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
        var query = BuildJoinedQuery().Where(row => row.Slot.TenantId == tenantId);
        query = ApplyListFilters(query, status, jobProfileId, contractTypeId, orgUnitId, workCenterId);
        query = ApplySearchFilter(query, search);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(row => row.Slot.Code)
            .ThenBy(row => row.Slot.Title)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(row => new PositionSlotListItemResponse(
                row.Slot.PublicId,
                row.Slot.Code,
                row.Slot.Title,
                row.Slot.Status,
                row.JobProfile.PublicId,
                row.JobProfile.Code,
                row.JobProfile.Title,
                row.Role != null ? row.Role.PublicId : null,
                row.Role != null ? row.Role.Name : null,
                row.OrgUnit.PublicId,
                row.OrgUnit.Code,
                row.OrgUnit.Name,
                row.WorkCenter != null ? row.WorkCenter.PublicId : null,
                row.WorkCenter != null ? row.WorkCenter.Code : null,
                row.WorkCenter != null ? row.WorkCenter.Name : null,
                row.PositionCategory != null ? row.PositionCategory.Code : null,
                row.PositionCategory != null ? row.PositionCategory.Name : null,
                row.Classification != null ? row.Classification.Code : null,
                row.Classification != null ? row.Classification.Name : null,
                row.ContractType != null ? row.ContractType.PublicId : null,
                row.ContractType != null ? row.ContractType.Code : null,
                row.ContractType != null ? row.ContractType.Name : null,
                row.Slot.MaxEmployees,
                row.Slot.OccupiedEmployees,
                row.Slot.EffectiveFromUtc,
                row.Slot.EffectiveToUtc,
                row.Slot.IsActive,
                row.Slot.ConcurrencyToken,
                row.Slot.CreatedUtc,
                row.Slot.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<PositionSlotListItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<PositionSlotResponse?> GetResponseByIdAsync(Guid slotId, CancellationToken cancellationToken) =>
        BuildJoinedQuery()
            .Where(row => row.Slot.PublicId == slotId)
            .Select(row => new PositionSlotResponse(
                row.Slot.PublicId,
                row.Slot.TenantId,
                row.Slot.Code,
                row.Slot.Title,
                row.Slot.Status,
                row.JobProfile.PublicId,
                row.JobProfile.Code,
                row.JobProfile.Title,
                row.Role != null ? row.Role.PublicId : null,
                row.Role != null ? row.Role.Name : null,
                row.OrgUnit.PublicId,
                row.OrgUnit.Code,
                row.OrgUnit.Name,
                row.WorkCenter != null ? row.WorkCenter.PublicId : null,
                row.WorkCenter != null ? row.WorkCenter.Code : null,
                row.WorkCenter != null ? row.WorkCenter.Name : null,
                row.OrgUnit.CostCenterCode,
                row.DirectDependency != null ? row.DirectDependency.PublicId : null,
                row.DirectDependency != null ? row.DirectDependency.Code : null,
                row.FunctionalDependency != null ? row.FunctionalDependency.PublicId : null,
                row.FunctionalDependency != null ? row.FunctionalDependency.Code : null,
                row.PositionCategory != null ? row.PositionCategory.PublicId : null,
                row.PositionCategory != null ? row.PositionCategory.Code : null,
                row.PositionCategory != null ? row.PositionCategory.Name : null,
                row.Classification != null ? row.Classification.PublicId : null,
                row.Classification != null ? row.Classification.Code : null,
                row.Classification != null ? row.Classification.Name : null,
                row.ContractType != null ? row.ContractType.PublicId : null,
                row.ContractType != null ? row.ContractType.Code : null,
                row.ContractType != null ? row.ContractType.Name : null,
                row.Slot.MaxEmployees,
                row.Slot.OccupiedEmployees,
                row.Slot.EffectiveFromUtc,
                row.Slot.EffectiveToUtc,
                row.Slot.Notes,
                row.Slot.IsActive,
                row.Slot.ConcurrencyToken,
                row.Slot.CreatedUtc,
                row.Slot.ModifiedUtc))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<int> CountSlotsAsync(Guid tenantId, CancellationToken cancellationToken) =>
        dbContext.Set<PositionSlot>()
            .AsNoTracking()
            .Where(slot => slot.TenantId == tenantId)
            .CountAsync(cancellationToken);

    public async Task<IReadOnlyCollection<PositionSlotGraphNodeData>> GetGraphNodesAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var nodes = await BuildJoinedQuery()
            .Where(row => row.Slot.TenantId == tenantId)
            .Select(row => new PositionSlotGraphNodeData(
                row.Slot.Id,
                row.Slot.PublicId,
                row.Slot.Code,
                row.Slot.Title ?? row.JobProfile.Title,
                row.Slot.Status,
                row.JobProfile.PublicId,
                row.OrgUnit.PublicId,
                row.WorkCenter != null ? row.WorkCenter.PublicId : null,
                row.Slot.DirectDependencyPositionSlotId,
                null,
                row.Slot.FunctionalDependencyPositionSlotId,
                null,
                row.ContractType != null ? row.ContractType.PublicId : null,
                row.ContractType != null ? row.ContractType.Code : null,
                row.Slot.IsActive))
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
        int? maxRows,
        CancellationToken cancellationToken)
    {
        var query = BuildJoinedQuery().Where(row => row.Slot.TenantId == tenantId);
        query = ApplyListFilters(query, status, jobProfileId, contractTypeId, orgUnitId, workCenterId);
        query = ApplySearchFilter(query, search);

        var ordered = query
            .OrderBy(row => row.Slot.Code)
            .ThenBy(row => row.Slot.Title);

        IQueryable<SlotJoinedRow> limited = ordered;
        if (maxRows.HasValue)
        {
            limited = limited.Take(maxRows.Value);
        }

        return await limited
            .Select(row => new PositionSlotExportRow(
                row.Slot.PublicId,
                row.Slot.Code,
                row.Slot.Title,
                row.Slot.Status,
                row.JobProfile.Code,
                row.JobProfile.Title,
                row.Role != null ? row.Role.PublicId : null,
                row.Role != null ? row.Role.Name : null,
                row.OrgUnit.Code,
                row.OrgUnit.Name,
                row.WorkCenter != null ? row.WorkCenter.Code : null,
                row.WorkCenter != null ? row.WorkCenter.Name : null,
                row.OrgUnit.CostCenterCode,
                row.DirectDependency != null ? row.DirectDependency.Code : null,
                row.FunctionalDependency != null ? row.FunctionalDependency.Code : null,
                row.PositionCategory != null ? row.PositionCategory.Code : null,
                row.PositionCategory != null ? row.PositionCategory.Name : null,
                row.Classification != null ? row.Classification.Code : null,
                row.Classification != null ? row.Classification.Name : null,
                row.ContractType != null ? row.ContractType.PublicId : null,
                row.ContractType != null ? row.ContractType.Code : null,
                row.ContractType != null ? row.ContractType.Name : null,
                row.Slot.MaxEmployees,
                row.Slot.OccupiedEmployees,
                row.Slot.EffectiveFromUtc,
                row.Slot.EffectiveToUtc,
                row.Slot.IsActive,
                row.Slot.CreatedUtc,
                row.Slot.ModifiedUtc))
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

    // §PS3: single source of truth for the wide slot join. The 4 read endpoints
    // (Search / GetById / GraphNodes / Export) previously duplicated this ~8-table
    // shape with subtle drift risk (e.g., a future tenant-filter change had to be
    // mirrored in 4 places). Dependencies are LEFT JOIN-ed so EF prunes them out
    // of projections that don't reference them.
    private IQueryable<SlotJoinedRow> BuildJoinedQuery() =>
        from slot in dbContext.Set<PositionSlot>().AsNoTracking()
        join jobProfile in dbContext.JobProfiles.AsNoTracking() on slot.JobProfileId equals jobProfile.Id
        join orgUnit in dbContext.OrgUnits.AsNoTracking() on jobProfile.OrgUnitId equals orgUnit.Id
        join role in dbContext.IamRoles.AsNoTracking() on slot.RoleId equals role.Id into roleGroup
        from role in roleGroup.DefaultIfEmpty()
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
        select new SlotJoinedRow
        {
            Slot = slot,
            JobProfile = jobProfile,
            OrgUnit = orgUnit,
            Role = role,
            WorkCenter = workCenter,
            DirectDependency = directDependency,
            FunctionalDependency = functionalDependency,
            PositionCategory = positionCategory,
            Classification = classification,
            ContractType = contractType
        };

    private static IQueryable<SlotJoinedRow> ApplyListFilters(
        IQueryable<SlotJoinedRow> query,
        PositionSlotStatus? status,
        Guid? jobProfileId,
        Guid? contractTypeId,
        Guid? orgUnitId,
        Guid? workCenterId)
    {
        if (status.HasValue)
        {
            query = query.Where(row => row.Slot.Status == status.Value);
        }

        if (jobProfileId.HasValue)
        {
            query = query.Where(row => row.JobProfile.PublicId == jobProfileId.Value);
        }

        if (contractTypeId.HasValue)
        {
            query = query.Where(row => row.ContractType != null && row.ContractType.PublicId == contractTypeId.Value);
        }

        if (orgUnitId.HasValue)
        {
            query = query.Where(row => row.OrgUnit.PublicId == orgUnitId.Value);
        }

        if (workCenterId.HasValue)
        {
            query = query.Where(row => row.WorkCenter != null && row.WorkCenter.PublicId == workCenterId.Value);
        }

        return query;
    }

    private static IQueryable<SlotJoinedRow> ApplySearchFilter(IQueryable<SlotJoinedRow> query, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return query;
        }

        var normalizedSearch = search.Trim().ToUpperInvariant();
        return query.Where(MatchesNormalizedSearch(normalizedSearch));
    }

    // §PS3 + §PS2: single source of truth for the free-text predicate (multi-column
    // LIKE '%x%') previously duplicated verbatim in Search and Export. The min-length
    // guard lives in the validators (PositionSlotAdministration.cs).
    private static Expression<Func<SlotJoinedRow, bool>> MatchesNormalizedSearch(string normalizedSearch) =>
        row =>
            row.Slot.NormalizedCode.Contains(normalizedSearch) ||
            (row.Slot.Title != null && row.Slot.Title.ToUpper().Contains(normalizedSearch)) ||
            row.JobProfile.NormalizedCode.Contains(normalizedSearch) ||
            row.JobProfile.NormalizedTitle.Contains(normalizedSearch) ||
            row.OrgUnit.NormalizedCode.Contains(normalizedSearch) ||
            row.OrgUnit.NormalizedName.Contains(normalizedSearch) ||
            (row.WorkCenter != null &&
             (row.WorkCenter.NormalizedCode.Contains(normalizedSearch) || row.WorkCenter.NormalizedName.Contains(normalizedSearch)));

    // Plain class with init properties (NOT a positional record): EF Core can
    // translate property access on a MemberInit-constructed object via the
    // pending-selector rewrite, but cannot see through a positional-record
    // constructor — using `new SlotJoinedRow(...)` broke composition of `.Where`
    // / `.Select` over the joined query.
    private sealed class SlotJoinedRow
    {
        public required PositionSlot Slot { get; init; }
        public required JobProfile JobProfile { get; init; }
        public required OrgUnit OrgUnit { get; init; }
        public required IamRole? Role { get; init; }
        public required WorkCenter? WorkCenter { get; init; }
        public required PositionSlot? DirectDependency { get; init; }
        public required PositionSlot? FunctionalDependency { get; init; }
        public required PositionCategory? PositionCategory { get; init; }
        public required PositionCategoryClassification? Classification { get; init; }
        public required PositionDescriptionCatalogItem? ContractType { get; init; }
    }
}

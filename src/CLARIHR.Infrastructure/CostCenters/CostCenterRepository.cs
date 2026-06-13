using CLARIHR.Application.Abstractions.CostCenters;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.CostCenters;
using CLARIHR.Domain.CostCenters;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.CostCenters;

internal sealed class CostCenterRepository(ApplicationDbContext dbContext) : ICostCenterRepository
{
    public void Add(CostCenter costCenter) => dbContext.CostCenters.Add(costCenter);

    public Task<CostCenter?> GetByIdAsync(Guid costCenterId, CancellationToken cancellationToken) =>
        dbContext.CostCenters.SingleOrDefaultAsync(costCenter => costCenter.PublicId == costCenterId, cancellationToken);

    public Task<bool> ExistsOutsideTenantAsync(Guid costCenterId, CancellationToken cancellationToken) =>
        dbContext.CostCenters
            // Intentional tenant filter bypass: checks cross-tenant existence only for tenant-mismatch errors.
            .IgnoreQueryFilters()
            .AnyAsync(costCenter => costCenter.PublicId == costCenterId, cancellationToken);

    public Task<bool> CodeExistsAsync(
        Guid tenantId,
        string normalizedCode,
        long? excludingCostCenterId,
        CancellationToken cancellationToken) =>
        dbContext.CostCenters.AnyAsync(
            costCenter => costCenter.TenantId == tenantId &&
                          costCenter.NormalizedCode == normalizedCode &&
                          (!excludingCostCenterId.HasValue || costCenter.Id != excludingCostCenterId.Value),
            cancellationToken);

    public Task<bool> ExistsActiveByCodeAsync(Guid tenantId, string normalizedCode, CancellationToken cancellationToken) =>
        dbContext.CostCenters.AnyAsync(
            costCenter => costCenter.TenantId == tenantId &&
                          costCenter.NormalizedCode == normalizedCode &&
                          costCenter.IsActive,
            cancellationToken);

    public async Task<PagedResponse<CostCenterListItemResponse>> SearchAsync(
        Guid tenantId,
        Guid? typeId,
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query =
            from costCenter in dbContext.CostCenters.AsNoTracking()
            join type in dbContext.CostCenterTypes.AsNoTracking() on costCenter.CostCenterTypeId equals type.Id
            where costCenter.TenantId == tenantId
            select new
            {
                CostCenter = costCenter,
                Type = type
            };

        if (typeId.HasValue)
        {
            query = query.Where(item => item.Type.PublicId == typeId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(item => item.CostCenter.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(item =>
                item.CostCenter.NormalizedCode.Contains(normalizedSearch) ||
                item.CostCenter.NormalizedName.Contains(normalizedSearch));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(item => item.CostCenter.Name)
            .ThenBy(item => item.CostCenter.Code)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new CostCenterListItemResponse(
                item.CostCenter.PublicId,
                item.CostCenter.Code,
                item.CostCenter.Name,
                item.Type.PublicId,
                item.Type.Code,
                item.Type.Name,
                item.CostCenter.PayrollExpenseAccountCode,
                item.CostCenter.EmployerContributionAccountCode,
                item.CostCenter.ProvisionAccountCode,
                item.CostCenter.IsActive,
                item.CostCenter.ConcurrencyToken,
                item.CostCenter.CreatedUtc,
                item.CostCenter.ModifiedUtc))
            .ToListAsync(cancellationToken);

        return new PagedResponse<CostCenterListItemResponse>(items, pageNumber, pageSize, totalCount);
    }

    public Task<CostCenterResponse?> GetResponseByIdAsync(Guid costCenterId, CancellationToken cancellationToken) =>
        (from costCenter in dbContext.CostCenters.AsNoTracking()
         join type in dbContext.CostCenterTypes.AsNoTracking() on costCenter.CostCenterTypeId equals type.Id
         where costCenter.PublicId == costCenterId
         select new CostCenterResponse(
             costCenter.PublicId,
             costCenter.TenantId,
             costCenter.Code,
             costCenter.Name,
             type.PublicId,
             type.Code,
             type.Name,
             costCenter.PayrollExpenseAccountCode,
             costCenter.EmployerContributionAccountCode,
             costCenter.ProvisionAccountCode,
             costCenter.Description,
             costCenter.IsActive,
             costCenter.ConcurrencyToken,
             costCenter.CreatedUtc,
             costCenter.ModifiedUtc))
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<CostCenterUsageResponse?> GetUsageByIdAsync(Guid costCenterId, CancellationToken cancellationToken)
    {
        var costCenter = await dbContext.CostCenters
            .AsNoTracking()
            .Where(item => item.PublicId == costCenterId)
            .Select(item => new
            {
                item.PublicId,
                item.TenantId,
                item.Code,
                item.Name,
                item.NormalizedCode
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (costCenter is null)
        {
            return null;
        }

        var orgUnitActiveReferences = await dbContext.OrgUnits
            .AsNoTracking()
            .Where(orgUnit =>
                orgUnit.TenantId == costCenter.TenantId &&
                orgUnit.IsActive &&
                orgUnit.CostCenterCode != null &&
                orgUnit.CostCenterCode.Trim().ToUpper() == costCenter.NormalizedCode)
            .CountAsync(cancellationToken);

        var orgUnitInactiveReferences = await dbContext.OrgUnits
            .AsNoTracking()
            .Where(orgUnit =>
                orgUnit.TenantId == costCenter.TenantId &&
                !orgUnit.IsActive &&
                orgUnit.CostCenterCode != null &&
                orgUnit.CostCenterCode.Trim().ToUpper() == costCenter.NormalizedCode)
            .CountAsync(cancellationToken);

        var positionSlotActiveReferences = await
            (from slot in dbContext.PositionSlots.AsNoTracking()
             join profile in dbContext.JobProfiles.AsNoTracking() on slot.JobProfileId equals profile.Id
             join orgUnit in dbContext.OrgUnits.AsNoTracking() on profile.OrgUnitId equals orgUnit.Id
             where slot.TenantId == costCenter.TenantId &&
                   slot.IsActive &&
                   orgUnit.CostCenterCode != null &&
                   orgUnit.CostCenterCode.Trim().ToUpper() == costCenter.NormalizedCode
             select slot.Id)
            .CountAsync(cancellationToken);

        var positionSlotInactiveReferences = await
            (from slot in dbContext.PositionSlots.AsNoTracking()
             join profile in dbContext.JobProfiles.AsNoTracking() on slot.JobProfileId equals profile.Id
             join orgUnit in dbContext.OrgUnits.AsNoTracking() on profile.OrgUnitId equals orgUnit.Id
             where slot.TenantId == costCenter.TenantId &&
                   !slot.IsActive &&
                   orgUnit.CostCenterCode != null &&
                   orgUnit.CostCenterCode.Trim().ToUpper() == costCenter.NormalizedCode
             select slot.Id)
            .CountAsync(cancellationToken);

        return new CostCenterUsageResponse(
            costCenter.PublicId,
            costCenter.Code,
            costCenter.Name,
            orgUnitActiveReferences,
            orgUnitInactiveReferences,
            positionSlotActiveReferences,
            positionSlotInactiveReferences,
            orgUnitActiveReferences > 0 || positionSlotActiveReferences > 0);
    }

    // Boolean usage probe by (tenant, normalized code): used by the detail GET to populate the
    // `hasActiveUsage` flag of allowedActions without paying for the full breakdown of
    // GetUsageByIdAsync (5 queries). Early-exits after the org-unit check, so it costs 1-2 queries.
    public async Task<bool> HasActiveUsageAsync(Guid tenantId, string normalizedCode, CancellationToken cancellationToken)
    {
        var hasActiveOrgUnitUsage = await dbContext.OrgUnits
            .AsNoTracking()
            .AnyAsync(
                orgUnit =>
                    orgUnit.TenantId == tenantId &&
                    orgUnit.IsActive &&
                    orgUnit.CostCenterCode != null &&
                    orgUnit.CostCenterCode.Trim().ToUpper() == normalizedCode,
                cancellationToken);

        if (hasActiveOrgUnitUsage)
        {
            return true;
        }

        return await
            (from slot in dbContext.PositionSlots.AsNoTracking()
             join profile in dbContext.JobProfiles.AsNoTracking() on slot.JobProfileId equals profile.Id
             join orgUnit in dbContext.OrgUnits.AsNoTracking() on profile.OrgUnitId equals orgUnit.Id
             where slot.TenantId == tenantId &&
                   slot.IsActive &&
                   orgUnit.CostCenterCode != null &&
                   orgUnit.CostCenterCode.Trim().ToUpper() == normalizedCode
             select slot.Id)
            .AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<CostCenterExportRow>> GetExportRowsAsync(
        Guid tenantId,
        Guid? typeId,
        bool? isActive,
        string? search,
        int? maxRows,
        CancellationToken cancellationToken)
    {
        var query =
            from costCenter in dbContext.CostCenters.AsNoTracking()
            join type in dbContext.CostCenterTypes.AsNoTracking() on costCenter.CostCenterTypeId equals type.Id
            where costCenter.TenantId == tenantId
            select new
            {
                CostCenter = costCenter,
                Type = type
            };

        if (typeId.HasValue)
        {
            query = query.Where(item => item.Type.PublicId == typeId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(item => item.CostCenter.IsActive == isActive.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim().ToUpperInvariant();
            query = query.Where(item =>
                item.CostCenter.NormalizedCode.Contains(normalizedSearch) ||
                item.CostCenter.NormalizedName.Contains(normalizedSearch));
        }

        var ordered = query
            .OrderBy(item => item.CostCenter.Name)
            .ThenBy(item => item.CostCenter.Code);

        var limited = maxRows.HasValue
            ? ordered.Take(maxRows.Value)
            : ordered;

        return await limited
            .Select(item => new CostCenterExportRow(
                item.CostCenter.PublicId,
                item.CostCenter.Code,
                item.CostCenter.Name,
                item.Type.Code,
                item.Type.Name,
                item.CostCenter.PayrollExpenseAccountCode,
                item.CostCenter.EmployerContributionAccountCode,
                item.CostCenter.ProvisionAccountCode,
                item.CostCenter.Description,
                item.CostCenter.IsActive,
                item.CostCenter.CreatedUtc,
                item.CostCenter.ModifiedUtc))
            .ToArrayAsync(cancellationToken);
    }
}

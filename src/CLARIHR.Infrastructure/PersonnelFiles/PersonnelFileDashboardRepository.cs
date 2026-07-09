using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Reporting;
using CLARIHR.Domain.CostCenters;
using CLARIHR.Domain.GeneralCatalogs;
using CLARIHR.Domain.JobProfiles;
using CLARIHR.Domain.Locations;
using CLARIHR.Domain.OrgStructureCatalogs;
using CLARIHR.Domain.OrgUnits;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Domain.PositionDescriptionCatalogs;
using CLARIHR.Domain.PositionSlots;
using CLARIHR.Domain.Preferences;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.PersonnelFiles;

/// <summary>
/// Read-only data source for the HR analytics dashboard. Resolves the dimensional rows (PersonnelFile → active
/// assignment → org/position/work-center) and the structural indicators using in-memory lookups over the
/// bounded org-structure tables (the assignment stores PublicIds, so joins are by PublicId/Id). See the
/// technical plan §3.1 / risk T-01 (a single materialized query per dimension table; optimize via a view if it scales).
/// </summary>
internal sealed class PersonnelFileDashboardRepository(ApplicationDbContext dbContext) : IPersonnelFileDashboardRepository
{
    public async Task<DashboardDataSet> GetDashboardDataSetAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var lookups = await BuildDimensionLookupsAsync(tenantId, cancellationToken);
        var bundle = await BuildRowBundleAsync(tenantId, lookups, cancellationToken);

        // NOTE: only SV ranges are seeded today; multi-country filtering by the company's country is a Fase-2
        // refinement (R-02-adjacent). Active ranges ordered by sort order back the bucketization.
        var ageRanges = await dbContext.Set<AgeRangeCatalogItem>()
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.SortOrder)
            .Select(item => new RangeBucket(item.Code, item.Name, item.LowerBoundYears, item.UpperBoundYears))
            .ToArrayAsync(cancellationToken);

        var seniorityRanges = await dbContext.Set<SeniorityRangeCatalogItem>()
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.SortOrder)
            .Select(item => new RangeBucket(item.Code, item.Name, item.LowerBoundMonths, item.UpperBoundMonths))
            .ToArrayAsync(cancellationToken);

        var preference = await dbContext.Set<CompanyPreference>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .Select(item => new { item.HrFunctionalAreaCode, item.FileUpToDateThresholdMonths })
            .FirstOrDefaultAsync(cancellationToken);

        // Payroll-type catalog labels back the `byPayrollType` breakdown (aclaración №12 — labels from the
        // catalog, never hardcoded). Only SV items are seeded today (same Fase-2 caveat as the range catalogs).
        var payrollTypes = await dbContext.Set<PayrollTypeCatalogItem>()
            .AsNoTracking()
            .Where(item => item.IsActive)
            .Select(item => new { item.Code, item.Name })
            .ToArrayAsync(cancellationToken);
        var payrollTypeLabels = payrollTypes
            .GroupBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.OrdinalIgnoreCase);

        return new DashboardDataSet(
            bundle.Rows,
            ageRanges,
            seniorityRanges,
            preference?.HrFunctionalAreaCode,
            preference?.FileUpToDateThresholdMonths,
            payrollTypeLabels);
    }

    public async Task<DashboardMetadata> GetDashboardMetadataAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var ageRanges = await dbContext.Set<AgeRangeCatalogItem>()
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.SortOrder)
            .Select(item => new RangeBucket(item.Code, item.Name, item.LowerBoundYears, item.UpperBoundYears))
            .ToArrayAsync(cancellationToken);

        var seniorityRanges = await dbContext.Set<SeniorityRangeCatalogItem>()
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.SortOrder)
            .Select(item => new RangeBucket(item.Code, item.Name, item.LowerBoundMonths, item.UpperBoundMonths))
            .ToArrayAsync(cancellationToken);

        var preference = await dbContext.Set<CompanyPreference>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .Select(item => new { item.HrFunctionalAreaCode, item.FileUpToDateThresholdMonths })
            .FirstOrDefaultAsync(cancellationToken);

        return new DashboardMetadata(
            ageRanges,
            seniorityRanges,
            preference?.HrFunctionalAreaCode,
            preference?.FileUpToDateThresholdMonths);
    }

    public async Task<DashboardPositionOccupancy> GetPositionOccupancyAsync(
        Guid tenantId,
        DashboardDimensionFilter filter,
        CancellationToken cancellationToken)
    {
        var lookups = await BuildDimensionLookupsAsync(tenantId, cancellationToken);

        var maxPositions = 0;
        var occupied = 0;

        foreach (var slot in lookups.Slots)
        {
            lookups.JobProfilesById.TryGetValue(slot.JobProfileId, out var jobProfile);

            Guid? jobProfileId = jobProfile?.PublicId;
            Guid? orgUnitId = null;
            Guid? functionalAreaId = null;
            Guid? positionCategoryId = null;

            if (jobProfile is not null && lookups.OrgUnitsById.TryGetValue(jobProfile.OrgUnitId, out var orgUnit))
            {
                orgUnitId = orgUnit.PublicId;
                if (orgUnit.FunctionalAreaId is long faId && lookups.FunctionalAreasById.TryGetValue(faId, out var functionalArea))
                {
                    functionalAreaId = functionalArea.PublicId;
                }
            }

            if (jobProfile?.PositionCategoryId is long pcId && lookups.PositionCategoriesById.TryGetValue(pcId, out var category))
            {
                positionCategoryId = category.PublicId;
            }

            Guid? workCenterId = slot.WorkCenterId is long wcId && lookups.WorkCentersById.TryGetValue(wcId, out var workCenter)
                ? workCenter.PublicId
                : null;

            if (filter.JobProfileId.HasValue && jobProfileId != filter.JobProfileId)
            {
                continue;
            }

            if (filter.OrgUnitId.HasValue && orgUnitId != filter.OrgUnitId)
            {
                continue;
            }

            if (filter.FunctionalAreaId.HasValue && functionalAreaId != filter.FunctionalAreaId)
            {
                continue;
            }

            if (filter.PositionCategoryId.HasValue && positionCategoryId != filter.PositionCategoryId)
            {
                continue;
            }

            if (filter.WorkCenterId.HasValue && workCenterId != filter.WorkCenterId)
            {
                continue;
            }

            maxPositions += slot.MaxEmployees;
            occupied += slot.OccupiedEmployees;
        }

        return new DashboardPositionOccupancy(maxPositions, occupied, Math.Max(0, maxPositions - occupied));
    }

    public async Task<IReadOnlyCollection<DashboardManagerSpan>> GetSpanOfControlAsync(
        Guid tenantId,
        DashboardDimensionFilter filter,
        CancellationToken cancellationToken)
    {
        var lookups = await BuildDimensionLookupsAsync(tenantId, cancellationToken);
        var bundle = await BuildRowBundleAsync(tenantId, lookups, cancellationToken);
        var referenceDate = PersonnelFileDashboardRules.ResolveReferenceDate(filter.Year, DateTime.UtcNow);

        // First active occupant per slot (a slot with MaxEmployees > 1 may have several; we attribute the
        // managerial role to the first, deterministically by full name).
        var occupantBySlot = new Dictionary<Guid, (Guid FileId, string Name)>();
        foreach (var fileId in bundle.SlotPublicIdByFileId.Keys.OrderBy(id => bundle.NameByFileId.GetValueOrDefault(id, string.Empty), StringComparer.OrdinalIgnoreCase))
        {
            if (bundle.SlotPublicIdByFileId[fileId] is Guid slotPublicId && !occupantBySlot.ContainsKey(slotPublicId))
            {
                occupantBySlot[slotPublicId] = (fileId, bundle.NameByFileId.GetValueOrDefault(fileId, string.Empty));
            }
        }

        var spanByManager = new Dictionary<Guid, (string Name, string? Title, int Count)>();
        foreach (var report in bundle.Rows.Where(row => PersonnelFileDashboardRules.MatchesFilter(row, filter, referenceDate)))
        {
            if (!bundle.SlotPublicIdByFileId.TryGetValue(report.FileId, out var slotPublicId) || slotPublicId is null)
            {
                continue;
            }

            if (!lookups.SlotsByPublicId.TryGetValue(slotPublicId.Value, out var slot) ||
                slot.DirectDependencyPositionSlotId is not long managerSlotId ||
                !lookups.SlotsById.TryGetValue(managerSlotId, out var managerSlot) ||
                !occupantBySlot.TryGetValue(managerSlot.PublicId, out var manager) ||
                manager.FileId == report.FileId)
            {
                continue;
            }

            if (spanByManager.TryGetValue(manager.FileId, out var existing))
            {
                spanByManager[manager.FileId] = (existing.Name, existing.Title, existing.Count + 1);
            }
            else
            {
                var title = lookups.JobProfilesById.TryGetValue(managerSlot.JobProfileId, out var managerJobProfile)
                    ? managerJobProfile.Title
                    : null;
                spanByManager[manager.FileId] = (manager.Name, title, 1);
            }
        }

        return spanByManager
            .Select(entry => new DashboardManagerSpan(entry.Key, entry.Value.Name, entry.Value.Title, entry.Value.Count))
            .OrderByDescending(span => span.DirectReports)
            .ThenBy(span => span.ManagerName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<IReadOnlyList<ActionFactRow>> GetPersonnelActionFactsAsync(
        Guid tenantId,
        int year,
        int? month,
        bool includeAllStatuses,
        CancellationToken cancellationToken)
    {
        var start = new DateTime(year, month ?? 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = month.HasValue ? start.AddMonths(1) : start.AddYears(1);

        // Filter by (TenantId, ActionDateUtc) — served by ix_personnel_file_personnel_actions_tenant_action_date
        // (PR-1), whose INCLUDE columns (action_type_code/action_status_code/is_system_generated/personnel_file_id)
        // cover the journal side of this projection. The full status universe is returned regardless of
        // includeAllStatuses because byStatus must span every status (RN-04); the APLICADA items split is applied
        // by PersonnelActionsDashboardRules. The file public id (joined from personnel_files) is the join key to
        // the dimensional row bundle. No monetary fields are projected (aclaración №8).
        return await dbContext.Set<PersonnelFilePersonnelAction>()
            .AsNoTracking()
            .Where(action => action.TenantId == tenantId
                && action.ActionDateUtc >= start
                && action.ActionDateUtc < end)
            .Select(action => new ActionFactRow(
                action.ActionTypeCode,
                action.ActionStatusCode,
                action.ActionDateUtc,
                action.IsSystemGenerated,
                action.PersonnelFile.PublicId))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<PersonnelActionCatalogLabels> GetPersonnelActionCatalogLabelsAsync(CancellationToken cancellationToken)
    {
        // Only SV items are seeded today (same Fase-2 caveat as the range/payroll-type catalogs); labels always
        // come from the catalog, never hardcoded (aclaración №12).
        var typeItems = await dbContext.Set<ActionTypeCatalogItem>()
            .AsNoTracking()
            .Where(item => item.IsActive)
            .Select(item => new { item.Code, item.Name })
            .ToArrayAsync(cancellationToken);
        var statusItems = await dbContext.Set<ActionStatusCatalogItem>()
            .AsNoTracking()
            .Where(item => item.IsActive)
            .Select(item => new { item.Code, item.Name })
            .ToArrayAsync(cancellationToken);

        return new PersonnelActionCatalogLabels(
            typeItems
                .GroupBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.OrdinalIgnoreCase),
            statusItems
                .GroupBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.OrdinalIgnoreCase));
    }

    public async Task<MovementsCatalogLabels> GetMovementsCatalogLabelsAsync(CancellationToken cancellationToken)
    {
        // Labels for the movements breakdowns always come from the catalogs, never hardcoded (aclaración №12).
        // Only SV items are seeded today (same Fase-2 caveat as the range/action catalogs).
        var categoryItems = await dbContext.Set<RetirementCategoryCatalogItem>()
            .AsNoTracking()
            .Where(item => item.IsActive)
            .Select(item => new { item.Code, item.Name })
            .ToArrayAsync(cancellationToken);
        var reasonItems = await dbContext.Set<RetirementReasonCatalogItem>()
            .AsNoTracking()
            .Where(item => item.IsActive)
            .Select(item => new { item.Code, item.Name })
            .ToArrayAsync(cancellationToken);
        var statusItems = await dbContext.Set<SettlementStatusCatalogItem>()
            .AsNoTracking()
            .Where(item => item.IsActive)
            .Select(item => new { item.Code, item.Name })
            .ToArrayAsync(cancellationToken);

        return new MovementsCatalogLabels(
            ToLabelDictionary(categoryItems.Select(item => (item.Code, item.Name))),
            ToLabelDictionary(reasonItems.Select(item => (item.Code, item.Name))),
            ToLabelDictionary(statusItems.Select(item => (item.Code, item.Name))));
    }

    public async Task<IReadOnlyCollection<Guid>> GetCompletedExitInterviewFilePublicIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> separationFilePublicIds,
        CancellationToken cancellationToken)
    {
        if (separationFilePublicIds.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        // The numerator of the coverage ratio: separation files with a COMPLETED (Submitted) exit interview.
        // The submission stores the internal PersonnelFileId, so we join to resolve the public id. Anonymous
        // submissions have a null PersonnelFileId and never match.
        var publicIds = separationFilePublicIds.Distinct().ToArray();
        return await (
            from submission in dbContext.Set<ExitInterviewSubmission>().AsNoTracking()
            where submission.TenantId == tenantId
                && submission.Status == ExitInterviewSubmissionStatus.Submitted
                && submission.PersonnelFileId != null
            join file in dbContext.Set<PersonnelFile>().AsNoTracking()
                on submission.PersonnelFileId equals file.Id
            where publicIds.Contains(file.PublicId)
            select file.PublicId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, int>> GetSettlementStatusCountsAsync(
        Guid tenantId,
        int year,
        int? month,
        CancellationToken cancellationToken)
    {
        var start = new DateTime(year, month ?? 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = month.HasValue ? start.AddMonths(1) : start.AddYears(1);

        // Real settlements only (scenarios carry a null status) whose RetirementDate falls in the period, grouped
        // by StatusCode. Counts only — no Amount/NetPay projected (aclaración №8).
        var grouped = await dbContext.Set<PersonnelFileSettlement>()
            .AsNoTracking()
            .Where(settlement => settlement.TenantId == tenantId
                && settlement.Kind == SettlementKind.Liquidacion
                && settlement.RetirementDate >= start
                && settlement.RetirementDate < end)
            .GroupBy(settlement => settlement.StatusCode!)
            .Select(group => new { StatusCode = group.Key, Count = group.Count() })
            .ToArrayAsync(cancellationToken);

        return grouped.ToDictionary(entry => entry.StatusCode, entry => entry.Count, StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string> ToLabelDictionary(IEnumerable<(string Code, string Name)> items) =>
        items
            .GroupBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.OrdinalIgnoreCase);

    private async Task<RowBundle> BuildRowBundleAsync(Guid tenantId, DimensionLookups lookups, CancellationToken cancellationToken)
    {
        var employees = await dbContext.Set<PersonnelFile>()
            .AsNoTracking()
            .Where(file => file.TenantId == tenantId && file.RecordType == PersonnelFileRecordType.Employee)
            .Select(file => new
            {
                file.Id,
                file.PublicId,
                file.IsActive,
                file.LifecycleStatus,
                file.ModifiedUtc,
                file.BirthDate,
                file.MaritalStatus,
                file.FullName,
                file.OrgUnitPublicId
            })
            .ToArrayAsync(cancellationToken);

        var profiles = await dbContext.Set<PersonnelFileEmployeeProfile>()
            .AsNoTracking()
            .Where(profile => profile.TenantId == tenantId)
            .Select(profile => new
            {
                profile.PersonnelFileId,
                profile.HireDate,
                profile.EmploymentStatusCode,
                profile.RetirementDate,
                profile.RetirementCategoryCode,
                profile.RetirementReasonCode
            })
            .ToArrayAsync(cancellationToken);
        var profileByFileId = profiles.ToDictionary(profile => profile.PersonnelFileId);

        var assignments = await dbContext.Set<PersonnelFileEmploymentAssignment>()
            .AsNoTracking()
            .Where(assignment => assignment.TenantId == tenantId && assignment.IsActive)
            .Select(assignment => new
            {
                assignment.PersonnelFileId,
                assignment.IsPrimary,
                assignment.StartDate,
                assignment.OrgUnitPublicId,
                assignment.WorkCenterPublicId,
                assignment.PositionSlotPublicId,
                assignment.ContractTypeCode,
                assignment.PayrollTypeCode,
                assignment.CostCenterPublicId
            })
            .ToArrayAsync(cancellationToken);
        var assignmentByFileId = assignments
            .GroupBy(assignment => assignment.PersonnelFileId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(item => item.IsPrimary).ThenBy(item => item.StartDate).First());

        var rows = new List<EmployeeDimensionRow>(employees.Length);
        var slotByFileId = new Dictionary<Guid, Guid?>(employees.Length);
        var nameByFileId = new Dictionary<Guid, string>(employees.Length);

        foreach (var employee in employees)
        {
            assignmentByFileId.TryGetValue(employee.Id, out var assignment);
            profileByFileId.TryGetValue(employee.Id, out var profile);

            var orgUnitPublicId = assignment?.OrgUnitPublicId ?? employee.OrgUnitPublicId;
            string? orgUnitName = null;
            Guid? functionalAreaId = null;
            string? functionalAreaCode = null;
            string? functionalAreaName = null;

            if (orgUnitPublicId.HasValue && lookups.OrgUnitsByPublicId.TryGetValue(orgUnitPublicId.Value, out var orgUnit))
            {
                orgUnitName = orgUnit.Name;
                if (orgUnit.FunctionalAreaId is long faId && lookups.FunctionalAreasById.TryGetValue(faId, out var functionalArea))
                {
                    functionalAreaId = functionalArea.PublicId;
                    functionalAreaCode = functionalArea.Code;
                    functionalAreaName = functionalArea.Name;
                }
            }

            Guid? workCenterPublicId = assignment?.WorkCenterPublicId;
            string? workCenterName = workCenterPublicId.HasValue && lookups.WorkCentersByPublicId.TryGetValue(workCenterPublicId.Value, out var workCenter)
                ? workCenter.Name
                : null;

            Guid? costCenterPublicId = assignment?.CostCenterPublicId;
            string? costCenterName = costCenterPublicId.HasValue && lookups.CostCenterNamesByPublicId.TryGetValue(costCenterPublicId.Value, out var costCenter)
                ? costCenter
                : null;

            Guid? jobProfilePublicId = null;
            string? jobProfileTitle = null;
            Guid? positionCategoryId = null;
            string? positionCategoryName = null;
            Guid? slotPublicId = assignment?.PositionSlotPublicId;

            if (slotPublicId.HasValue && lookups.SlotsByPublicId.TryGetValue(slotPublicId.Value, out var slot) &&
                lookups.JobProfilesById.TryGetValue(slot.JobProfileId, out var jobProfile))
            {
                jobProfilePublicId = jobProfile.PublicId;
                jobProfileTitle = jobProfile.Title;
                if (jobProfile.PositionCategoryId is long pcId && lookups.PositionCategoriesById.TryGetValue(pcId, out var category))
                {
                    positionCategoryId = category.PublicId;
                    positionCategoryName = category.Name;
                }
            }

            rows.Add(new EmployeeDimensionRow(
                employee.PublicId,
                employee.IsActive,
                employee.LifecycleStatus.ToString(),
                employee.ModifiedUtc,
                employee.BirthDate,
                profile?.HireDate,
                profile?.RetirementDate,
                employee.MaritalStatus,
                PersonnelFileRecordType.Employee.ToString(),
                profile?.EmploymentStatusCode,
                assignment?.ContractTypeCode,
                orgUnitPublicId,
                orgUnitName,
                functionalAreaId,
                functionalAreaCode,
                functionalAreaName,
                workCenterPublicId,
                workCenterName,
                jobProfilePublicId,
                jobProfileTitle,
                positionCategoryId,
                positionCategoryName,
                assignment?.PayrollTypeCode,
                costCenterPublicId,
                costCenterName,
                profile?.RetirementCategoryCode,
                profile?.RetirementReasonCode));

            slotByFileId[employee.PublicId] = slotPublicId;
            nameByFileId[employee.PublicId] = employee.FullName;
        }

        return new RowBundle(rows, slotByFileId, nameByFileId);
    }

    private async Task<DimensionLookups> BuildDimensionLookupsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var orgUnits = await dbContext.Set<OrgUnit>()
            .AsNoTracking()
            .Where(unit => unit.TenantId == tenantId)
            .Select(unit => new { unit.Id, unit.PublicId, unit.Name, unit.FunctionalAreaCatalogItemId })
            .ToArrayAsync(cancellationToken);
        var orgUnitsByPublicId = new Dictionary<Guid, OrgUnitInfo>(orgUnits.Length);
        var orgUnitsById = new Dictionary<long, OrgUnitInfo>(orgUnits.Length);
        foreach (var unit in orgUnits)
        {
            var info = new OrgUnitInfo(unit.PublicId, unit.Name, unit.FunctionalAreaCatalogItemId);
            orgUnitsByPublicId[unit.PublicId] = info;
            orgUnitsById[unit.Id] = info;
        }

        var functionalAreas = await dbContext.Set<FunctionalAreaCatalogItem>()
            .AsNoTracking()
            .Where(area => area.TenantId == tenantId)
            .Select(area => new { area.Id, area.PublicId, area.Code, area.Name })
            .ToArrayAsync(cancellationToken);
        var functionalAreasById = functionalAreas.ToDictionary(
            area => area.Id,
            area => (PublicId: area.PublicId, Code: area.Code, Name: area.Name));

        var workCenters = await dbContext.Set<WorkCenter>()
            .AsNoTracking()
            .Where(center => center.TenantId == tenantId)
            .Select(center => new { center.Id, center.PublicId, center.Name })
            .ToArrayAsync(cancellationToken);
        var workCentersById = workCenters.ToDictionary(center => center.Id, center => (PublicId: center.PublicId, Name: center.Name));
        var workCentersByPublicId = workCenters.ToDictionary(center => center.PublicId, center => (Id: center.Id, Name: center.Name));

        var jobProfiles = await dbContext.Set<JobProfile>()
            .AsNoTracking()
            .Where(profile => profile.TenantId == tenantId)
            .Select(profile => new { profile.Id, profile.PublicId, profile.Title, profile.OrgUnitId, profile.PositionCategoryId })
            .ToArrayAsync(cancellationToken);
        var jobProfilesById = jobProfiles.ToDictionary(
            profile => profile.Id,
            profile => new JobProfileInfo(profile.PublicId, profile.Title, profile.OrgUnitId, profile.PositionCategoryId));

        var categories = await dbContext.Set<PositionCategory>()
            .AsNoTracking()
            .Where(category => category.TenantId == tenantId)
            .Select(category => new { category.Id, category.PublicId, category.Name })
            .ToArrayAsync(cancellationToken);
        var categoriesById = categories.ToDictionary(category => category.Id, category => (PublicId: category.PublicId, Name: category.Name));

        // Cost-center names resolved in memory like the other dimensions (the assignment stores CostCenterPublicId).
        var costCenters = await dbContext.Set<CostCenter>()
            .AsNoTracking()
            .Where(center => center.TenantId == tenantId)
            .Select(center => new { center.PublicId, center.Name })
            .ToArrayAsync(cancellationToken);
        var costCenterNamesByPublicId = costCenters
            .GroupBy(center => center.PublicId)
            .ToDictionary(group => group.Key, group => group.First().Name);

        var slots = await dbContext.Set<PositionSlot>()
            .AsNoTracking()
            .Where(slot => slot.TenantId == tenantId)
            .Select(slot => new
            {
                slot.Id,
                slot.PublicId,
                slot.JobProfileId,
                slot.WorkCenterId,
                slot.DirectDependencyPositionSlotId,
                slot.MaxEmployees,
                slot.OccupiedEmployees
            })
            .ToArrayAsync(cancellationToken);
        var slotInfos = slots
            .Select(slot => new SlotInfo(
                slot.Id,
                slot.PublicId,
                slot.JobProfileId,
                slot.WorkCenterId,
                slot.DirectDependencyPositionSlotId,
                slot.MaxEmployees,
                slot.OccupiedEmployees))
            .ToArray();

        return new DimensionLookups(
            orgUnitsByPublicId,
            orgUnitsById,
            functionalAreasById,
            workCentersByPublicId,
            workCentersById,
            jobProfilesById,
            categoriesById,
            costCenterNamesByPublicId,
            slotInfos.ToDictionary(slot => slot.PublicId),
            slotInfos.ToDictionary(slot => slot.Id),
            slotInfos);
    }

    private sealed record RowBundle(
        IReadOnlyList<EmployeeDimensionRow> Rows,
        IReadOnlyDictionary<Guid, Guid?> SlotPublicIdByFileId,
        IReadOnlyDictionary<Guid, string> NameByFileId);

    private sealed record OrgUnitInfo(Guid PublicId, string Name, long? FunctionalAreaId);

    private sealed record JobProfileInfo(Guid PublicId, string Title, long OrgUnitId, long? PositionCategoryId);

    private sealed record SlotInfo(
        long Id,
        Guid PublicId,
        long JobProfileId,
        long? WorkCenterId,
        long? DirectDependencyPositionSlotId,
        int MaxEmployees,
        int OccupiedEmployees);

    private sealed record DimensionLookups(
        IReadOnlyDictionary<Guid, OrgUnitInfo> OrgUnitsByPublicId,
        IReadOnlyDictionary<long, OrgUnitInfo> OrgUnitsById,
        IReadOnlyDictionary<long, (Guid PublicId, string Code, string Name)> FunctionalAreasById,
        IReadOnlyDictionary<Guid, (long Id, string Name)> WorkCentersByPublicId,
        IReadOnlyDictionary<long, (Guid PublicId, string Name)> WorkCentersById,
        IReadOnlyDictionary<long, JobProfileInfo> JobProfilesById,
        IReadOnlyDictionary<long, (Guid PublicId, string Name)> PositionCategoriesById,
        IReadOnlyDictionary<Guid, string> CostCenterNamesByPublicId,
        IReadOnlyDictionary<Guid, SlotInfo> SlotsByPublicId,
        IReadOnlyDictionary<long, SlotInfo> SlotsById,
        IReadOnlyList<SlotInfo> Slots);
}

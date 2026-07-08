using System.Text.Json;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.Compensation;
using CLARIHR.Domain.Leave;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.PersonnelFiles;

/// <summary>
/// EF-backed persistence port of the vacation fund vertical (leave module PR-7). Period responses are projected;
/// the derived consumption (allocations − returns of fund-consuming requests) is reduced through
/// <see cref="VacationFundMath.NetConsumedByPeriod"/> so the fund detail, the profile balance and the edit/delete
/// guards agree by construction; the base-salary resolution REPLICATES the settlement/leave data-provider
/// criterion (country base-salary codes, primary plaza or employee-level concepts).
/// </summary>
internal sealed class PersonnelFileVacationRepository(ApplicationDbContext dbContext) : IPersonnelFileVacationRepository
{
    private const string LegacyBaseSalaryConceptTypeCode = "SALARIO_BASE";

    public async Task<IReadOnlyCollection<PersonnelFileVacationPeriodResponse>> GetPeriodResponsesAsync(
        Guid personnelFilePublicId, CancellationToken cancellationToken) =>
        await dbContext.PersonnelFileVacationPeriods
            .AsNoTracking()
            .Where(period => period.PersonnelFile.PublicId == personnelFilePublicId && period.IsActive)
            .OrderByDescending(period => period.PeriodYear)
            .Select(period => MapPeriod(period))
            .ToArrayAsync(cancellationToken);

    public async Task<PersonnelFileVacationPeriodResponse?> GetPeriodResponseAsync(
        Guid personnelFilePublicId, Guid vacationPeriodPublicId, CancellationToken cancellationToken) =>
        await dbContext.PersonnelFileVacationPeriods
            .AsNoTracking()
            .Where(period => period.PersonnelFile.PublicId == personnelFilePublicId && period.PublicId == vacationPeriodPublicId)
            .Select(period => MapPeriod(period))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PersonnelFileVacationPeriod?> GetPeriodEntityAsync(
        Guid personnelFilePublicId, Guid vacationPeriodPublicId, CancellationToken cancellationToken) =>
        await dbContext.PersonnelFileVacationPeriods
            .Where(period => period.PersonnelFile.PublicId == personnelFilePublicId && period.PublicId == vacationPeriodPublicId)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<bool> HasActivePeriodForYearAsync(
        long personnelFileId, int year, long? excludePeriodId, CancellationToken cancellationToken) =>
        await dbContext.PersonnelFileVacationPeriods
            .AsNoTracking()
            .AnyAsync(
                period => period.PersonnelFileId == personnelFileId
                    && period.IsActive
                    && period.PeriodYear == year
                    && (!excludePeriodId.HasValue || period.Id != excludePeriodId.Value),
                cancellationToken);

    public async Task<bool> HasConsumptionAsync(long vacationPeriodId, CancellationToken cancellationToken)
    {
        var consuming = VacationRequestStatuses.ConsumesFund.ToArray();
        return await dbContext.VacationRequestAllocations
            .AsNoTracking()
            .Join(
                dbContext.PersonnelFileVacationRequests.AsNoTracking(),
                allocation => allocation.VacationRequestId,
                request => request.Id,
                (allocation, request) => new { allocation.VacationPeriodId, request.StatusCode })
            .AnyAsync(
                row => row.VacationPeriodId == vacationPeriodId && consuming.Contains(row.StatusCode),
                cancellationToken);
    }

    public void AddPeriod(PersonnelFileVacationPeriod entity) => dbContext.PersonnelFileVacationPeriods.Add(entity);

    public async Task<DateOnly?> GetAnchorDateAsync(long personnelFileId, CancellationToken cancellationToken)
    {
        var primaryStart = await dbContext.PersonnelFileEmploymentAssignments
            .AsNoTracking()
            .Where(assignment => assignment.PersonnelFileId == personnelFileId && assignment.IsActive)
            .OrderByDescending(assignment => assignment.IsPrimary)
            .ThenBy(assignment => assignment.StartDate)
            .Select(assignment => (DateTime?)assignment.StartDate)
            .FirstOrDefaultAsync(cancellationToken);
        if (primaryStart is { } start)
        {
            return DateOnly.FromDateTime(start);
        }

        var hireDate = await dbContext.PersonnelFileEmployeeProfiles
            .AsNoTracking()
            .Where(profile => profile.PersonnelFileId == personnelFileId)
            .Select(profile => (DateTime?)profile.HireDate)
            .FirstOrDefaultAsync(cancellationToken);
        return hireDate is { } hire ? DateOnly.FromDateTime(hire) : null;
    }

    public async Task<IReadOnlyCollection<VacationPeriodConsumptionRow>> GetActivePeriodConsumptionsAsync(
        long personnelFileId, CancellationToken cancellationToken)
    {
        var periods = await dbContext.PersonnelFileVacationPeriods
            .AsNoTracking()
            .Where(period => period.PersonnelFileId == personnelFileId && period.IsActive)
            .OrderByDescending(period => period.PeriodYear)
            .Select(period => new
            {
                period.Id,
                period.PublicId,
                period.PeriodYear,
                period.PeriodStartDate,
                period.PeriodEndDate,
                period.LegalDaysGranted,
                period.BenefitDaysGranted,
                period.GeneratesEnjoymentDays,
                period.UsedAnniversary,
                period.SourceCode,
            })
            .ToListAsync(cancellationToken);

        var net = await LoadNetConsumptionAsync([personnelFileId], cancellationToken);

        return periods
            .Select(period => new VacationPeriodConsumptionRow(
                period.Id,
                period.PublicId,
                period.PeriodYear,
                period.PeriodStartDate,
                period.PeriodEndDate,
                period.LegalDaysGranted,
                period.BenefitDaysGranted,
                period.GeneratesEnjoymentDays,
                period.UsedAnniversary,
                period.SourceCode,
                Math.Max(0, net.GetValueOrDefault(period.Id))))
            .ToArray();
    }

    public async Task<decimal?> GetMonthlyBaseSalaryAsync(
        Guid tenantId, long personnelFileId, CancellationToken cancellationToken)
    {
        var salaries = await GetMonthlyBaseSalariesAsync(tenantId, [personnelFileId], cancellationToken);
        return salaries.GetValueOrDefault(personnelFileId);
    }

    public async Task<IReadOnlyCollection<FondoProvisionExportRow>> GetFundProvisionRowsAsync(
        Guid tenantId, int? year, int? maxRows, CancellationToken cancellationToken)
    {
        var query =
            from period in dbContext.PersonnelFileVacationPeriods.AsNoTracking()
            join file in dbContext.PersonnelFiles.AsNoTracking() on period.PersonnelFileId equals file.Id
            join profileEntry in dbContext.PersonnelFileEmployeeProfiles.AsNoTracking()
                on file.Id equals profileEntry.PersonnelFileId into profileGroup
            from profile in profileGroup.DefaultIfEmpty()
            where period.TenantId == tenantId && period.IsActive
            select new
            {
                period.Id,
                period.PersonnelFileId,
                period.PeriodYear,
                period.PeriodStartDate,
                period.PeriodEndDate,
                period.LegalDaysGranted,
                period.BenefitDaysGranted,
                EmployeeFullName = file.FullName,
                EmployeeCode = profile != null ? profile.EmployeeCode : null,
            };

        if (year is { } filterYear)
        {
            query = query.Where(row => row.PeriodYear == filterYear);
        }

        query = query
            .OrderBy(row => row.EmployeeFullName)
            .ThenByDescending(row => row.PeriodYear);

        var periods = maxRows is { } limit
            ? await query.Take(limit + 1).ToListAsync(cancellationToken)
            : await query.ToListAsync(cancellationToken);

        if (periods.Count == 0)
        {
            return [];
        }

        var fileIds = periods.Select(period => period.PersonnelFileId).Distinct().ToArray();
        var net = await LoadNetConsumptionAsync(fileIds, cancellationToken);
        var salaries = await GetMonthlyBaseSalariesAsync(tenantId, fileIds, cancellationToken);

        return periods
            .Select(period =>
            {
                var granted = period.LegalDaysGranted + period.BenefitDaysGranted;
                var enjoyed = Math.Max(0, net.GetValueOrDefault(period.Id));
                var pending = Math.Max(0, granted - enjoyed);
                var daily = VacationFundMath.DailySalary(salaries.GetValueOrDefault(period.PersonnelFileId)) ?? 0m;
                var provision = VacationFundMath.Provision(pending, daily) ?? 0m;
                return new FondoProvisionExportRow(
                    period.EmployeeFullName,
                    period.EmployeeCode,
                    period.PeriodYear,
                    period.PeriodStartDate,
                    period.PeriodEndDate,
                    period.LegalDaysGranted,
                    period.BenefitDaysGranted,
                    enjoyed,
                    pending,
                    daily,
                    provision);
            })
            .ToArray();
    }

    public async Task<IReadOnlyCollection<VacationGenerationCandidate>> GetGenerationCandidatesAsync(
        Guid tenantId, IReadOnlyCollection<Guid>? employeeFilter, CancellationToken cancellationToken)
    {
        var filter = employeeFilter is { Count: > 0 } ? employeeFilter.ToArray() : null;

        var files = await (
            from file in dbContext.PersonnelFiles.AsNoTracking()
            join profile in dbContext.PersonnelFileEmployeeProfiles.AsNoTracking()
                on file.Id equals profile.PersonnelFileId
            where file.TenantId == tenantId
                && file.RecordType == PersonnelFileRecordType.Employee
                && file.IsActive
                && profile.RetirementDate == null
                && profile.EmploymentStatusCode != PersonnelFileEmployeeProfile.RetiredEmploymentStatusCode
                && (filter == null || filter.Contains(file.PublicId))
            select new
            {
                file.Id,
                file.PublicId,
                file.FullName,
                profile.EmployeeCode,
                profile.HireDate,
            }).ToListAsync(cancellationToken);

        if (files.Count == 0)
        {
            return [];
        }

        var fileIds = files.Select(file => file.Id).ToArray();
        var primaryStartByFile = await dbContext.PersonnelFileEmploymentAssignments
            .AsNoTracking()
            .Where(assignment => fileIds.Contains(assignment.PersonnelFileId) && assignment.IsActive)
            .Select(assignment => new { assignment.PersonnelFileId, assignment.IsPrimary, assignment.StartDate })
            .ToListAsync(cancellationToken);

        var anchorByFile = primaryStartByFile
            .GroupBy(assignment => assignment.PersonnelFileId)
            .ToDictionary(
                group => group.Key,
                group => DateOnly.FromDateTime(
                    group.OrderByDescending(item => item.IsPrimary).ThenBy(item => item.StartDate).First().StartDate));

        return files
            .Select(file => new VacationGenerationCandidate(
                file.Id,
                file.PublicId,
                file.FullName,
                file.EmployeeCode,
                anchorByFile.TryGetValue(file.Id, out var start) ? start : DateOnly.FromDateTime(file.HireDate)))
            .ToArray();
    }

    public async Task<IReadOnlySet<long>> GetPersonnelFileIdsWithActivePeriodForYearAsync(
        Guid tenantId, int year, CancellationToken cancellationToken) =>
        (await dbContext.PersonnelFileVacationPeriods
            .AsNoTracking()
            .Where(period => period.TenantId == tenantId && period.IsActive && period.PeriodYear == year)
            .Select(period => period.PersonnelFileId)
            .ToListAsync(cancellationToken))
        .ToHashSet();

    // ── Requests (PR-8) ───────────────────────────────────────────────────────────────────────────

    private static readonly string[] LiveRequestStatuses =
        [VacationRequestStatuses.Solicitada, VacationRequestStatuses.Aprobada, VacationRequestStatuses.DevueltaParcial];

    private static readonly JsonSerializerOptions DistributionOptions = new(JsonSerializerDefaults.Web);

    public async Task<PersonnelFileVacationRequest?> GetRequestEntityAsync(
        Guid personnelFilePublicId, Guid vacationRequestPublicId, CancellationToken cancellationToken) =>
        await dbContext.PersonnelFileVacationRequests
            .Include(request => request.Allocations)
            .Include(request => request.Returns)
            .Where(request => request.PersonnelFile.PublicId == personnelFilePublicId && request.PublicId == vacationRequestPublicId)
            .SingleOrDefaultAsync(cancellationToken);

    public void AddRequest(PersonnelFileVacationRequest entity) => dbContext.PersonnelFileVacationRequests.Add(entity);

    public async Task<IReadOnlyCollection<PersonnelFileVacationRequestResponse>> GetRequestResponsesAsync(
        Guid personnelFilePublicId, CancellationToken cancellationToken)
    {
        var personnelFileId = await dbContext.PersonnelFiles
            .AsNoTracking()
            .Where(file => file.PublicId == personnelFilePublicId)
            .Select(file => (long?)file.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (personnelFileId is null)
        {
            return [];
        }

        var requests = await dbContext.PersonnelFileVacationRequests
            .AsNoTracking()
            .Include(request => request.Allocations)
            .Include(request => request.Returns)
            .Where(request => request.PersonnelFileId == personnelFileId.Value)
            .OrderByDescending(request => request.StartDate)
            .ToListAsync(cancellationToken);

        var periodRefs = await GetPeriodRefLookupAsync(personnelFileId.Value, cancellationToken);
        return requests.Select(request => MapRequest(request, periodRefs)).ToArray();
    }

    public async Task<PersonnelFileVacationRequestResponse?> GetRequestResponseAsync(
        Guid personnelFilePublicId, Guid vacationRequestPublicId, CancellationToken cancellationToken)
    {
        var request = await dbContext.PersonnelFileVacationRequests
            .AsNoTracking()
            .Include(item => item.Allocations)
            .Include(item => item.Returns)
            .Where(item => item.PersonnelFile.PublicId == personnelFilePublicId && item.PublicId == vacationRequestPublicId)
            .SingleOrDefaultAsync(cancellationToken);
        if (request is null)
        {
            return null;
        }

        var periodRefs = await GetPeriodRefLookupAsync(request.PersonnelFileId, cancellationToken);
        return MapRequest(request, periodRefs);
    }

    public async Task<bool> HasOverlappingRequestAsync(
        long personnelFileId, DateOnly startDate, DateOnly endDate, long? excludeRequestId, CancellationToken cancellationToken) =>
        await dbContext.PersonnelFileVacationRequests
            .AsNoTracking()
            .Where(request => request.PersonnelFileId == personnelFileId
                && LiveRequestStatuses.Contains(request.StatusCode)
                && (!excludeRequestId.HasValue || request.Id != excludeRequestId.Value))
            .AnyAsync(request => request.StartDate <= endDate && startDate <= request.EndDate, cancellationToken);

    public async Task<IReadOnlySet<DateOnly>> GetHolidaysInRangeAsync(
        Guid tenantId, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken) =>
        (await dbContext.CompanyHolidays
            .AsNoTracking()
            .Where(holiday => holiday.TenantId == tenantId && holiday.IsActive
                && holiday.Date >= startDate && holiday.Date <= endDate)
            .Select(holiday => holiday.Date)
            .ToListAsync(cancellationToken))
        .ToHashSet();

    public async Task<DayOfWeek?> GetPrimaryPlazaRestDayAsync(long personnelFileId, CancellationToken cancellationToken) =>
        await dbContext.PersonnelFileEmploymentAssignments
            .AsNoTracking()
            .Where(assignment => assignment.PersonnelFileId == personnelFileId && assignment.IsActive)
            .OrderByDescending(assignment => assignment.IsPrimary)
            .ThenBy(assignment => assignment.StartDate)
            .Select(assignment => assignment.RestDayOfWeek)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, long>> ResolveEnjoymentPeriodInternalIdsAsync(
        long personnelFileId, IReadOnlyCollection<Guid> periodPublicIds, CancellationToken cancellationToken)
    {
        if (periodPublicIds.Count == 0)
        {
            return new Dictionary<Guid, long>();
        }

        var ids = periodPublicIds.Distinct().ToArray();
        return await dbContext.PersonnelFileVacationPeriods
            .AsNoTracking()
            .Where(period => period.PersonnelFileId == personnelFileId
                && period.IsActive
                && period.GeneratesEnjoymentDays
                && ids.Contains(period.PublicId))
            .ToDictionaryAsync(period => period.PublicId, period => period.Id, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<long, VacationPeriodRef>> GetPeriodRefLookupAsync(
        long personnelFileId, CancellationToken cancellationToken) =>
        await dbContext.PersonnelFileVacationPeriods
            .AsNoTracking()
            .Where(period => period.PersonnelFileId == personnelFileId)
            .ToDictionaryAsync(
                period => period.Id,
                period => new VacationPeriodRef(period.PublicId, period.PeriodYear),
                cancellationToken);

    // ── Annual plan / calendar / bandeja (PR-9) ─────────────────────────────────────────────────────

    public void AddPlan(VacationPlan entity) => dbContext.VacationPlans.Add(entity);

    public async Task<VacationPlan?> GetPlanEntityAsync(
        Guid tenantId, Guid vacationPlanPublicId, CancellationToken cancellationToken) =>
        await dbContext.VacationPlans
            .Include(plan => plan.Lines)
            .Where(plan => plan.TenantId == tenantId && plan.PublicId == vacationPlanPublicId)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<VacationPlanResponse>> GetPlanResponsesAsync(
        Guid tenantId, int? year, CancellationToken cancellationToken)
    {
        var query = dbContext.VacationPlans
            .AsNoTracking()
            .Include(plan => plan.Lines)
            .Where(plan => plan.TenantId == tenantId);
        if (year is { } filterYear)
        {
            query = query.Where(plan => plan.PlanYear == filterYear);
        }

        var plans = await query
            .OrderByDescending(plan => plan.PlanYear)
            .ThenByDescending(plan => plan.CreatedUtc)
            .ToListAsync(cancellationToken);
        return plans.Select(plan => VacationPlanMapping.Map(plan)).ToArray();
    }

    public async Task<VacationPlanResponse?> GetPlanResponseAsync(
        Guid tenantId, Guid vacationPlanPublicId, CancellationToken cancellationToken)
    {
        var plan = await dbContext.VacationPlans
            .AsNoTracking()
            .Include(item => item.Lines)
            .Where(item => item.TenantId == tenantId && item.PublicId == vacationPlanPublicId)
            .SingleOrDefaultAsync(cancellationToken);
        return plan is null ? null : VacationPlanMapping.Map(plan);
    }

    public async Task<IReadOnlyDictionary<Guid, VacationPlanEmployeeContext>> GetPlanEmployeeContextsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> personnelFilePublicIds, CancellationToken cancellationToken)
    {
        var result = new Dictionary<Guid, VacationPlanEmployeeContext>();
        if (personnelFilePublicIds.Count == 0)
        {
            return result;
        }

        var ids = personnelFilePublicIds.Distinct().ToArray();
        var files = await dbContext.PersonnelFiles
            .AsNoTracking()
            .Where(file => file.TenantId == tenantId
                && file.RecordType == PersonnelFileRecordType.Employee
                && file.IsActive
                && ids.Contains(file.PublicId))
            .Select(file => new { file.Id, file.PublicId })
            .ToListAsync(cancellationToken);
        if (files.Count == 0)
        {
            return result;
        }

        var fileIds = files.Select(file => file.Id).ToArray();

        var restDays = await dbContext.PersonnelFileEmploymentAssignments
            .AsNoTracking()
            .Where(assignment => fileIds.Contains(assignment.PersonnelFileId) && assignment.IsActive)
            .Select(assignment => new { assignment.PersonnelFileId, assignment.IsPrimary, assignment.StartDate, assignment.RestDayOfWeek })
            .ToListAsync(cancellationToken);
        var restByFile = restDays
            .GroupBy(assignment => assignment.PersonnelFileId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(item => item.IsPrimary).ThenBy(item => item.StartDate).First().RestDayOfWeek);

        var periods = await dbContext.PersonnelFileVacationPeriods
            .AsNoTracking()
            .Where(period => fileIds.Contains(period.PersonnelFileId) && period.IsActive && period.GeneratesEnjoymentDays)
            .Select(period => new { period.Id, period.PersonnelFileId, period.LegalDaysGranted, period.BenefitDaysGranted })
            .ToListAsync(cancellationToken);
        var net = await LoadNetConsumptionAsync(fileIds, cancellationToken);
        var availableByFile = periods
            .GroupBy(period => period.PersonnelFileId)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(period =>
                    Math.Max(0, period.LegalDaysGranted + period.BenefitDaysGranted - Math.Max(0, net.GetValueOrDefault(period.Id)))));

        foreach (var file in files)
        {
            result[file.PublicId] = new VacationPlanEmployeeContext(
                file.PublicId,
                restByFile.GetValueOrDefault(file.Id),
                availableByFile.GetValueOrDefault(file.Id));
        }

        return result;
    }

    public async Task<VacationCalendarResponse> GetCalendarAsync(Guid tenantId, int year, CancellationToken cancellationToken)
    {
        var yearStart = new DateOnly(year, 1, 1);
        var yearEnd = new DateOnly(year, 12, 31);
        var consuming = VacationRequestStatuses.ConsumesFund.ToArray();

        var enjoyments = await (
            from request in dbContext.PersonnelFileVacationRequests.AsNoTracking()
            join file in dbContext.PersonnelFiles.AsNoTracking() on request.PersonnelFileId equals file.Id
            join profileEntry in dbContext.PersonnelFileEmployeeProfiles.AsNoTracking()
                on file.Id equals profileEntry.PersonnelFileId into profileGroup
            from profile in profileGroup.DefaultIfEmpty()
            where request.TenantId == tenantId
                && consuming.Contains(request.StatusCode)
                && request.StartDate <= yearEnd && yearStart <= request.EndDate
            orderby request.StartDate, request.EndDate
            select new VacationCalendarEnjoymentEntry(
                file.PublicId,
                file.FullName,
                profile != null ? profile.EmployeeCode : null,
                request.PublicId,
                request.StartDate,
                request.EndDate,
                request.RequestedDays,
                request.StatusCode))
            .ToListAsync(cancellationToken);

        var plannedLines = await (
            from line in dbContext.VacationPlanLines.AsNoTracking()
            join plan in dbContext.VacationPlans.AsNoTracking() on line.VacationPlanId equals plan.Id
            join fileEntry in dbContext.PersonnelFiles.AsNoTracking() on line.PersonnelFilePublicId equals fileEntry.PublicId into fileGroup
            from file in fileGroup.DefaultIfEmpty()
            where plan.TenantId == tenantId
                && plan.StatusCode == VacationPlanStatuses.Vigente
                && plan.PlanYear == year
            orderby line.StartDate, line.EndDate
            select new VacationCalendarPlanEntry(
                line.PersonnelFilePublicId,
                file != null ? file.FullName : null,
                plan.PublicId,
                line.StartDate,
                line.EndDate,
                line.Days))
            .ToListAsync(cancellationToken);

        return new VacationCalendarResponse(year, enjoyments, plannedLines);
    }

    public async Task<VacationRequestBandejaResponse> QueryRequestsAsync(
        QueryVacationRequestsQuery query, CancellationToken cancellationToken)
    {
        var baseQuery = FilteredRequests(query.CompanyId, query.EmployeeId, query.StartFromUtc, query.StartToUtc);

        // StatusCounts over the full (non-status) filter, so every status is represented.
        var statusCounts = await baseQuery
            .GroupBy(row => row.Request.StatusCode)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.Key, group => group.Count, cancellationToken);

        var filtered = baseQuery;
        if (!string.IsNullOrWhiteSpace(query.StatusCode))
        {
            var normalizedStatus = query.StatusCode.Trim().ToUpperInvariant();
            filtered = filtered.Where(row => row.Request.StatusCode == normalizedStatus);
        }

        var totalCount = await filtered.CountAsync(cancellationToken);

        var page = await filtered
            .OrderByDescending(row => row.Request.StartDate)
            .ThenByDescending(row => row.Request.CreatedUtc)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(row => new
            {
                row.Request.PublicId,
                row.EmployeeFilePublicId,
                row.EmployeeFullName,
                row.EmployeeCode,
                row.Request.RequesterFilePublicId,
                row.Request.RequesterNameSnapshot,
                row.Request.StartDate,
                row.Request.EndDate,
                row.Request.RequestedDays,
                Consumed = row.Request.Allocations.Sum(allocation => (int?)allocation.Days) ?? 0,
                Returned = row.Request.Returns.Sum(entry => (int?)entry.Days) ?? 0,
                row.Request.StatusCode,
                row.Request.DecisionDateUtc,
                row.Request.CreatedUtc,
            })
            .ToListAsync(cancellationToken);

        var items = page
            .Select(row => new VacationRequestListItemResponse(
                row.PublicId,
                row.EmployeeFilePublicId,
                row.EmployeeFullName,
                row.EmployeeCode,
                row.RequesterFilePublicId,
                row.RequesterNameSnapshot,
                row.StartDate,
                row.EndDate,
                row.RequestedDays,
                row.Consumed,
                row.Returned,
                row.Consumed - row.Returned,
                row.StatusCode,
                row.DecisionDateUtc,
                row.CreatedUtc))
            .ToArray();

        return new VacationRequestBandejaResponse(items, query.PageNumber, query.PageSize, totalCount, statusCounts);
    }

    public async Task<IReadOnlyCollection<GoceVacacionesExportRow>> GetGoceExportRowsAsync(
        ExportVacationRequestsQuery query, CancellationToken cancellationToken)
    {
        var consuming = VacationRequestStatuses.ConsumesFund.ToArray();
        var filtered = FilteredRequests(query.CompanyId, query.EmployeeId, query.StartFromUtc, query.StartToUtc)
            .Where(row => consuming.Contains(row.Request.StatusCode))
            .OrderByDescending(row => row.Request.StartDate)
            .ThenByDescending(row => row.Request.CreatedUtc);

        var limited = query.MaxRows is { } maxRows ? filtered.Take(maxRows + 1) : filtered;

        var rows = await limited
            .Select(row => new
            {
                row.Request.Id,
                row.EmployeeFullName,
                row.EmployeeCode,
                row.Request.StatusCode,
                row.Request.StartDate,
                row.Request.EndDate,
                row.Request.RequestedDays,
                Consumed = row.Request.Allocations.Sum(allocation => (int?)allocation.Days) ?? 0,
                Returned = row.Request.Returns.Sum(entry => (int?)entry.Days) ?? 0,
                row.Request.DecisionDateUtc,
            })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return [];
        }

        var requestIds = rows.Select(row => row.Id).ToArray();
        var allocationRows = await (
            from allocation in dbContext.VacationRequestAllocations.AsNoTracking()
            join period in dbContext.PersonnelFileVacationPeriods.AsNoTracking() on allocation.VacationPeriodId equals period.Id
            where requestIds.Contains(allocation.VacationRequestId)
            select new { allocation.VacationRequestId, period.PeriodYear, allocation.Days })
            .ToListAsync(cancellationToken);

        var periodsByRequest = allocationRows
            .GroupBy(row => row.VacationRequestId)
            .ToDictionary(
                group => group.Key,
                group => string.Join("; ", group
                    .GroupBy(item => item.PeriodYear)
                    .OrderBy(item => item.Key)
                    .Select(item => $"{item.Key}: {item.Sum(entry => entry.Days)}")));

        return rows
            .Select(row => new GoceVacacionesExportRow(
                row.EmployeeFullName,
                row.EmployeeCode,
                row.StatusCode,
                row.StartDate,
                row.EndDate,
                row.RequestedDays,
                row.Consumed,
                row.Returned,
                row.Consumed - row.Returned,
                periodsByRequest.GetValueOrDefault(row.Id, string.Empty),
                row.DecisionDateUtc))
            .ToArray();
    }

    private IQueryable<VacationRequestQueryRow> FilteredRequests(
        Guid tenantId, Guid? employeeId, DateTime? startFromUtc, DateTime? startToUtc)
    {
        var query =
            from request in dbContext.PersonnelFileVacationRequests.AsNoTracking()
            join file in dbContext.PersonnelFiles.AsNoTracking() on request.PersonnelFileId equals file.Id
            join profileEntry in dbContext.PersonnelFileEmployeeProfiles.AsNoTracking()
                on file.Id equals profileEntry.PersonnelFileId into profileGroup
            from profile in profileGroup.DefaultIfEmpty()
            where request.TenantId == tenantId
            select new VacationRequestQueryRow
            {
                Request = request,
                EmployeeFullName = file.FullName,
                EmployeeFilePublicId = file.PublicId,
                EmployeeCode = profile != null ? profile.EmployeeCode : null,
            };

        if (employeeId is { } employeePublicId)
        {
            query = query.Where(row => row.EmployeeFilePublicId == employeePublicId);
        }

        if (startFromUtc is { } startFrom)
        {
            var from = DateOnly.FromDateTime(startFrom);
            query = query.Where(row => row.Request.StartDate >= from);
        }

        if (startToUtc is { } startTo)
        {
            var to = DateOnly.FromDateTime(startTo);
            query = query.Where(row => row.Request.StartDate <= to);
        }

        return query;
    }

    private sealed class VacationRequestQueryRow
    {
        public PersonnelFileVacationRequest Request { get; init; } = null!;

        public string EmployeeFullName { get; init; } = string.Empty;

        public Guid EmployeeFilePublicId { get; init; }

        public string? EmployeeCode { get; init; }
    }

    private static PersonnelFileVacationRequestResponse MapRequest(
        PersonnelFileVacationRequest request, IReadOnlyDictionary<long, VacationPeriodRef> periodRefs)
    {
        var allocations = request.Allocations
            .Select(allocation => ToAllocationResponse(allocation.VacationPeriodId, allocation.Days, periodRefs))
            .ToArray();

        var returns = request.Returns
            .OrderBy(entry => entry.ReturnDateUtc)
            .Select(entry => new VacationReturnResponse(
                entry.PublicId,
                entry.Days,
                entry.ReturnDateUtc,
                entry.Reason,
                DeserializeDistribution(entry.DistributionJson)
                    .Select(item => ToAllocationResponse(item.VacationPeriodId, item.Days, periodRefs))
                    .ToArray()))
            .ToArray();

        return new PersonnelFileVacationRequestResponse(
            request.PublicId,
            request.RequesterFilePublicId,
            request.RequesterNameSnapshot,
            request.StartDate,
            request.EndDate,
            request.RequestedDays,
            request.StatusCode,
            request.PlanLinePublicId,
            request.DecisionNotes,
            request.DecisionDateUtc,
            request.ConsumedDays,
            request.ReturnedDays,
            request.NetConsumedDays,
            request.Notes,
            request.IsActive,
            request.ConcurrencyToken,
            request.CreatedUtc,
            request.ModifiedUtc,
            allocations,
            returns);
    }

    private static VacationAllocationResponse ToAllocationResponse(
        long periodId, int days, IReadOnlyDictionary<long, VacationPeriodRef> periodRefs) =>
        periodRefs.TryGetValue(periodId, out var reference)
            ? new VacationAllocationResponse(reference.PublicId, reference.PeriodYear, days)
            : new VacationAllocationResponse(Guid.Empty, 0, days);

    private static IReadOnlyList<VacationReturnDistributionInput> DeserializeDistribution(string? distributionJson)
    {
        if (string.IsNullOrWhiteSpace(distributionJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<VacationReturnDistributionInput>>(distributionJson, DistributionOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private async Task<Dictionary<long, int>> LoadNetConsumptionAsync(
        IReadOnlyCollection<long> fileIds, CancellationToken cancellationToken)
    {
        var consuming = VacationRequestStatuses.ConsumesFund.ToArray();
        var fileIdArray = fileIds.ToArray();

        var allocations = await dbContext.VacationRequestAllocations
            .AsNoTracking()
            .Join(
                dbContext.PersonnelFileVacationRequests.AsNoTracking(),
                allocation => allocation.VacationRequestId,
                request => request.Id,
                (allocation, request) => new { allocation.VacationPeriodId, allocation.Days, request.PersonnelFileId, request.StatusCode })
            .Where(row => fileIdArray.Contains(row.PersonnelFileId) && consuming.Contains(row.StatusCode))
            .Select(row => new { row.VacationPeriodId, row.Days })
            .ToListAsync(cancellationToken);

        var returnDistributions = await dbContext.VacationReturns
            .AsNoTracking()
            .Join(
                dbContext.PersonnelFileVacationRequests.AsNoTracking(),
                entry => entry.VacationRequestId,
                request => request.Id,
                (entry, request) => new { entry.DistributionJson, request.PersonnelFileId, request.StatusCode })
            .Where(row => fileIdArray.Contains(row.PersonnelFileId) && consuming.Contains(row.StatusCode))
            .Select(row => row.DistributionJson)
            .ToListAsync(cancellationToken);

        var net = VacationFundMath.NetConsumedByPeriod(
            allocations.Select(row => (row.VacationPeriodId, row.Days)),
            returnDistributions);

        return net.ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    /// <summary>
    /// Batch base-salary resolution (settlement/leave data-provider criterion): the primary plaza per file
    /// (IsPrimary among active assignments, oldest StartDate when none), the country base-salary concept codes
    /// (IsBaseSalary flag, literal SALARIO_BASE fallback), and the fixed income concepts scoped to the primary
    /// plaza or the employee level.
    /// </summary>
    private async Task<Dictionary<long, decimal?>> GetMonthlyBaseSalariesAsync(
        Guid tenantId, IReadOnlyCollection<long> fileIds, CancellationToken cancellationToken)
    {
        var result = new Dictionary<long, decimal?>();
        var fileIdArray = fileIds.ToArray();
        if (fileIdArray.Length == 0)
        {
            return result;
        }

        var countryCatalogItemId = await dbContext.Companies
            .AsNoTracking()
            .Where(company => company.PublicId == tenantId)
            .Select(company => company.CountryCatalogItemId)
            .SingleOrDefaultAsync(cancellationToken);

        var baseSalaryCodes = await dbContext.Set<CompensationConceptTypeCatalogItem>()
            .AsNoTracking()
            .Where(item => item.CountryCatalogItemId == countryCatalogItemId && item.IsBaseSalary)
            .Select(item => item.NormalizedCode)
            .ToListAsync(cancellationToken);
        if (baseSalaryCodes.Count == 0)
        {
            baseSalaryCodes.Add(LegacyBaseSalaryConceptTypeCode);
        }

        var assignments = await dbContext.PersonnelFileEmploymentAssignments
            .AsNoTracking()
            .Where(assignment => fileIdArray.Contains(assignment.PersonnelFileId))
            .Select(assignment => new { assignment.PersonnelFileId, assignment.PublicId, assignment.StartDate, assignment.IsActive, assignment.IsPrimary })
            .ToListAsync(cancellationToken);

        var primaryPlazaByFile = assignments
            .GroupBy(assignment => assignment.PersonnelFileId)
            .ToDictionary(
                group => group.Key,
                group =>
                {
                    var active = group.Where(item => item.IsActive).ToList();
                    var chosen = active.Where(item => item.IsPrimary).OrderBy(item => item.StartDate).FirstOrDefault()
                        ?? active.OrderBy(item => item.StartDate).FirstOrDefault()
                        ?? group.OrderBy(item => item.StartDate).FirstOrDefault();
                    return chosen is null ? ((Guid PublicId, bool IsPrimary)?)null : (chosen.PublicId, chosen.IsPrimary);
                });

        var concepts = await dbContext.Set<PersonnelFileCompensationConcept>()
            .AsNoTracking()
            .Where(concept => fileIdArray.Contains(concept.PersonnelFileId) && concept.IsActive)
            .Where(concept => concept.Nature == CompensationNature.Ingreso && concept.CalculationType == CompensationCalculationType.Fixed)
            .Select(concept => new { concept.PersonnelFileId, concept.AssignedPositionPublicId, concept.ConceptTypeCode, concept.Value })
            .ToListAsync(cancellationToken);

        var conceptsByFile = concepts.ToLookup(concept => concept.PersonnelFileId);

        foreach (var fileId in fileIdArray)
        {
            if (!primaryPlazaByFile.TryGetValue(fileId, out var plaza) || plaza is null)
            {
                result[fileId] = null;
                continue;
            }

            var value = conceptsByFile[fileId]
                .Where(concept => baseSalaryCodes.Contains(concept.ConceptTypeCode.ToUpperInvariant()))
                .Where(concept => concept.AssignedPositionPublicId == plaza.Value.PublicId
                    || (plaza.Value.IsPrimary && concept.AssignedPositionPublicId == null))
                .Select(concept => (decimal?)concept.Value)
                .FirstOrDefault();
            result[fileId] = value;
        }

        return result;
    }

    private static PersonnelFileVacationPeriodResponse MapPeriod(PersonnelFileVacationPeriod period) =>
        new(
            period.PublicId,
            period.PeriodYear,
            period.PeriodStartDate,
            period.PeriodEndDate,
            period.LegalDaysGranted,
            period.BenefitDaysGranted,
            period.LegalDaysGranted + period.BenefitDaysGranted,
            period.GeneratesEnjoymentDays,
            period.UsedAnniversary,
            period.SourceCode,
            period.IsActive,
            period.ConcurrencyToken,
            period.CreatedUtc,
            period.ModifiedUtc);
}

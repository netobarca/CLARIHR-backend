using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.Compensation;
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

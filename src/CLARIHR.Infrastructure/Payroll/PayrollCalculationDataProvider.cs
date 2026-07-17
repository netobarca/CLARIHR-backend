using CLARIHR.Application.Abstractions.Payroll;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Features.Payroll;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.Compensation;
using CLARIHR.Domain.Leave;
using CLARIHR.Domain.Payroll;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Payroll;

/// <summary>
/// Resolves the raw generation data of a Nómina × period (REQ-012 §3.4 — molde
/// <c>LeaveCalculationDataProvider</c>: one class, one <c>ApplicationDbContext</c>, everything AsNoTracking).
/// The base salary is the plaza's own MONTHLYIZED figure (Round2(value × periodsPerYear / 12) over the
/// plaza-scoped <c>IsBaseSalary</c> concepts — NEVER the settlement context's raw amount, §0.8); the legal
/// schemes resolve country-default → instance-override like the certified settlement engine; the Renta
/// table is the EFFECTIVE one of the frequency (IsActive + vigency window — the plain bracket repository
/// does not filter it); the pools ride their public scan/pending queries and the registro inputs include
/// the REQ-014 lagged carryovers (consumption derived from run lines).
/// </summary>
internal sealed class PayrollCalculationDataProvider(
    ApplicationDbContext dbContext,
    IPersonnelFileEmployeeRepository employeeRepository,
    IPayrollRunRepository payrollRunRepository) : IPayrollCalculationDataProvider
{
    public async Task<PayrollRunSourceData> BuildAsync(
        Guid tenantId,
        PayrollDefinition definition,
        PayrollPeriodDefinition period,
        IReadOnlyCollection<Guid>? employeeIds,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var (population, complianceExclusions) = await BuildPopulationAsync(tenantId, definition, period, employeeIds, cancellationToken);
        var populationFileIds = population.Select(row => row.PersonnelFilePublicId).ToHashSet();

        var company = await dbContext.Companies
            .AsNoTracking()
            .Where(item => item.PublicId == tenantId)
            .Select(item => new { item.CountryCatalogItemId })
            .SingleOrDefaultAsync(cancellationToken);
        var countryId = company?.CountryCatalogItemId ?? 0L;

        var isss = await ResolveSchemeAsync(countryId, "ISSS", cancellationToken);
        var afp = await ResolveSchemeAsync(countryId, "AFP", cancellationToken);
        var incafRate = await dbContext.Set<CompensationConceptTypeCatalogItem>()
            .AsNoTracking()
            .Where(item => item.CountryCatalogItemId == countryId && item.NormalizedCode == "INCAF")
            .Select(item => item.DefaultEmployerRate)
            .SingleOrDefaultAsync(cancellationToken)
            // The settlement precedent charges INCAF at 1.00 % over the ISSS base when the country catalog
            // does not override it (P-02).
            ?? 1.00m;

        var rentaBrackets = await GetEffectiveBracketsAsync(tenantId, definition.PayPeriodCode, cancellationToken);

        // Pools — public scans/pending rows of each module, filtered to the population afterwards.
        var recurringIncomes = await BuildRecurringIncomeRowsAsync(tenantId, definition.PayrollTypeCode, populationFileIds, cancellationToken);
        var recurringDeductions = await BuildRecurringDeductionRowsAsync(tenantId, definition.PayrollTypeCode, populationFileIds, cancellationToken);

        var oneTimeIncomes = (await employeeRepository.GetOneTimeIncomePendingRowsAsync(tenantId, definition.PayrollTypeCode, cancellationToken))
            .Where(row => populationFileIds.Contains(row.PersonnelFilePublicId))
            .Where(row => row.PayrollPeriodPublicId is null || row.PayrollPeriodPublicId == period.PublicId)
            .ToArray();
        var oneTimeDeductions = (await employeeRepository.GetOneTimeDeductionPendingAsync(tenantId, definition.PayrollTypeCode, payrollPeriodPublicId: null, employeeId: null, cancellationToken))
            .Where(row => populationFileIds.Contains(row.PersonnelFilePublicId))
            .Where(row => row.PayrollPeriodPublicId is null || row.PayrollPeriodPublicId == period.PublicId)
            .ToArray();
        var overtime = (await employeeRepository.GetOvertimePendingRowsAsync(tenantId, definition.PayrollTypeCode, cancellationToken))
            .Where(row => populationFileIds.Contains(row.PersonnelFilePublicId))
            .Where(row => row.PayrollPeriodPublicId is null || row.PayrollPeriodPublicId == period.PublicId)
            // A future organized shift is not payable (№13) — same guard the Apply mutator enforces.
            .Where(row => row.WorkDate <= today)
            .ToArray();

        var notWorkedTimes = await BuildNotWorkedTimeRowsAsync(tenantId, period, populationFileIds, cancellationToken);
        var disciplinary = await BuildDisciplinaryRowsAsync(tenantId, period, populationFileIds, cancellationToken);
        var incapacities = await BuildIncapacityRowsAsync(tenantId, period, populationFileIds, cancellationToken);

        return new PayrollRunSourceData(
            population,
            isss,
            afp,
            incafRate,
            rentaBrackets,
            recurringIncomes,
            oneTimeIncomes,
            overtime,
            recurringDeductions,
            oneTimeDeductions,
            notWorkedTimes,
            disciplinary,
            incapacities,
            complianceExclusions);
    }

    private async Task<(IReadOnlyList<PayrollPopulationRow> Population, IReadOnlyList<PayrollComplianceExclusion> ComplianceExclusions)> BuildPopulationAsync(
        Guid tenantId,
        PayrollDefinition definition,
        PayrollPeriodDefinition period,
        IReadOnlyCollection<Guid>? employeeIds,
        CancellationToken cancellationToken)
    {
        // Employees with an EMITIDA settlement whose retirement date falls INSIDE the period stay out —
        // the finiquito pays them (golden 8, anti double-pay §0.12).
        var periodStartUtc = period.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var periodEndUtc = period.EndDate.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);
        var settledFileIds = await dbContext.Set<PersonnelFileSettlement>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId &&
                           item.StatusCode == SettlementStatuses.Emitida &&
                           item.RetirementDate >= periodStartUtc &&
                           item.RetirementDate <= periodEndUtc)
            .Select(item => item.PersonnelFileId)
            .ToListAsync(cancellationToken);
        var settledIds = settledFileIds.ToHashSet();

        var rows = await (
            from assignment in dbContext.Set<PersonnelFileEmploymentAssignment>().AsNoTracking()
            join file in dbContext.Set<PersonnelFile>().AsNoTracking() on assignment.PersonnelFileId equals file.Id
            join profile in dbContext.Set<PersonnelFileEmployeeProfile>().AsNoTracking()
                on file.Id equals profile.PersonnelFileId into profiles
            from profile in profiles.DefaultIfEmpty()
            where assignment.TenantId == tenantId &&
                  assignment.IsActive &&
                  assignment.PayrollTypeCode == definition.PayrollTypeCode &&
                  file.RecordType == PersonnelFileRecordType.Employee &&
                  file.LifecycleStatus == PersonnelFileLifecycleStatus.Completed &&
                  (profile == null || profile.EmploymentStatusCode != PersonnelFileEmployeeProfile.RetiredEmploymentStatusCode)
            select new
            {
                file.Id,
                FilePublicId = file.PublicId,
                file.FullName,
                EmployeeCode = profile != null ? profile.EmployeeCode : null,
                PlazaPublicId = assignment.PublicId,
                assignment.IsPrimary,
                assignment.CostCenterPublicId,
                MinimumMonthlyWage = profile != null ? profile.MinimumMonthlyWage : null,
                HasAfpAccountNumber = file.AfpAccountNumber != null,
                HasNupIsss = file.Identifications.Any(identification => identification.IdentificationType == "NUP_ISSS"),
            })
            .ToListAsync(cancellationToken);

        var filtered = rows
            .Where(row => !settledIds.Contains(row.Id))
            .Where(row => employeeIds is null || employeeIds.Contains(row.FilePublicId))
            .ToList();

        if (filtered.Count == 0)
        {
            return ([], []);
        }

        // REQ-016 Gate B (ratified P-11/P-12) — off by default; only enforced once the tenant turns its
        // compliance gates on, after its data-capture campaign (§0.11/§2.3 of the technical plan). An
        // employee missing either datum is excluded from THIS run (their line is skipped, not the run).
        var gatesEnabled = await dbContext.CompanyPreferences
            .AsNoTracking()
            .Where(preference => preference.TenantId == tenantId)
            .Select(preference => preference.PayrollComplianceGatesEnabled)
            .SingleOrDefaultAsync(cancellationToken) == true;

        var complianceExclusions = gatesEnabled
            ? filtered
                .Where(row => !row.HasAfpAccountNumber || !row.HasNupIsss)
                .Select(row => new PayrollComplianceExclusion(row.FilePublicId, row.FullName))
                .ToArray()
            : [];

        if (gatesEnabled)
        {
            var excludedIds = complianceExclusions.Select(exclusion => exclusion.PersonnelFilePublicId).ToHashSet();
            filtered = filtered.Where(row => !excludedIds.Contains(row.FilePublicId)).ToList();
        }

        if (filtered.Count == 0)
        {
            return ([], complianceExclusions);
        }

        // Cost-center names by public id (line snapshot).
        var costCenterIds = filtered.Where(row => row.CostCenterPublicId.HasValue).Select(row => row.CostCenterPublicId!.Value).Distinct().ToList();
        var costCenters = costCenterIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await dbContext.Set<CLARIHR.Domain.CostCenters.CostCenter>()
                .AsNoTracking()
                .Where(item => costCenterIds.Contains(item.PublicId))
                .ToDictionaryAsync(item => item.PublicId, item => item.Name, cancellationToken);

        // The plaza's own MONTHLYIZED base (§0.8): the country's IsBaseSalary concepts (SALARIO_BASE
        // fallback), plaza-scoped or employee-level-on-primary, monthlyized by the concept's own
        // PayPeriodCode — Round2 once (never the settlement's raw figure).
        var company = await dbContext.Companies
            .AsNoTracking()
            .Where(item => item.PublicId == tenantId)
            .Select(item => new { item.CountryCatalogItemId })
            .SingleOrDefaultAsync(cancellationToken);
        var baseSalaryCodes = company is null
            ? []
            : await dbContext.Set<CompensationConceptTypeCatalogItem>()
                .AsNoTracking()
                .Where(item => item.CountryCatalogItemId == company.CountryCatalogItemId && item.IsBaseSalary)
                .Select(item => item.NormalizedCode)
                .ToListAsync(cancellationToken);
        if (baseSalaryCodes.Count == 0)
        {
            baseSalaryCodes.Add("SALARIO_BASE");
        }

        var fileIds = filtered.Select(row => row.Id).Distinct().ToList();
        var concepts = await dbContext.Set<PersonnelFileCompensationConcept>()
            .AsNoTracking()
            .Where(item => fileIds.Contains(item.PersonnelFileId) &&
                           item.IsActive &&
                           item.Nature == CompensationNature.Ingreso &&
                           item.CalculationType == CompensationCalculationType.Fixed)
            .Select(item => new { item.PersonnelFileId, item.AssignedPositionPublicId, item.ConceptTypeCode, item.Value, item.PayPeriodCode })
            .ToListAsync(cancellationToken);
        var baseConcepts = concepts
            .Where(item => baseSalaryCodes.Contains(item.ConceptTypeCode.ToUpperInvariant()))
            .ToList();

        var result = new List<PayrollPopulationRow>(filtered.Count);
        foreach (var row in filtered)
        {
            var monthly = baseConcepts
                .Where(item => item.PersonnelFileId == row.Id)
                .Where(item => item.AssignedPositionPublicId is { } plaza
                    ? plaza == row.PlazaPublicId
                    : row.IsPrimary)
                .Sum(item => Monthlyize(item.Value, item.PayPeriodCode));

            result.Add(new PayrollPopulationRow(
                row.Id,
                row.FilePublicId,
                row.FullName,
                row.EmployeeCode,
                row.PlazaPublicId,
                row.IsPrimary,
                row.CostCenterPublicId is { } costCenterId && costCenters.TryGetValue(costCenterId, out var name) ? name : null,
                row.MinimumMonthlyWage,
                monthly));
        }

        return (result, complianceExclusions);
    }

    /// <summary>Round2(value × periodsPerYear / 12) — the REQ-010 monthlyization the finiquito never does.</summary>
    private static decimal Monthlyize(decimal value, string? payPeriodCode) =>
        Math.Round(
            value * RecurringDeductionFrequencies.PeriodsPerYear(payPeriodCode) / 12m,
            2,
            MidpointRounding.AwayFromZero);

    private async Task<PayrollLegalScheme> ResolveSchemeAsync(
        long countryCatalogItemId,
        string conceptTypeCode,
        CancellationToken cancellationToken)
    {
        var defaults = await dbContext.Set<CompensationConceptTypeCatalogItem>()
            .AsNoTracking()
            .Where(item => item.CountryCatalogItemId == countryCatalogItemId && item.NormalizedCode == conceptTypeCode)
            .Select(item => new { item.DefaultEmployeeRate, item.DefaultEmployerRate, item.ContributionCap })
            .SingleOrDefaultAsync(cancellationToken);

        return new PayrollLegalScheme(
            defaults?.DefaultEmployeeRate ?? 0m,
            defaults?.DefaultEmployerRate ?? 0m,
            defaults?.ContributionCap);
    }

    private async Task<IReadOnlyList<PayrollTaxBracket>> GetEffectiveBracketsAsync(
        Guid tenantId,
        string payPeriodCode,
        CancellationToken cancellationToken)
    {
        // The EFFECTIVE table of the frequency (the settlement filter §0.9 — the plain bracket repository
        // does not filter IsActive/vigency).
        var asOfUtc = DateTime.UtcNow;
        var brackets = await dbContext.IncomeTaxWithholdingBrackets
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId &&
                           item.PayPeriodCode == payPeriodCode &&
                           item.IsActive &&
                           item.EffectiveFromUtc <= asOfUtc &&
                           (item.EffectiveToUtc == null || item.EffectiveToUtc >= asOfUtc))
            .OrderBy(item => item.BracketOrder)
            .Select(item => new PayrollTaxBracket(item.LowerBound, item.UpperBound, item.FixedFee, item.RatePercent, item.ExcessOver))
            .ToListAsync(cancellationToken);

        return brackets;
    }

    private async Task<IReadOnlyList<PayrollRecurringIncomeRow>> BuildRecurringIncomeRowsAsync(
        Guid tenantId,
        string payrollTypeCode,
        IReadOnlySet<Guid> populationFileIds,
        CancellationToken cancellationToken)
    {
        // Identity of the VIGENTE recurring incomes of the payroll type (the public scan carries the plan
        // but not the employee/plaza/concept — this projection adds them without duplicating any rule).
        var identities = await (
            from income in dbContext.Set<PersonnelFileRecurringIncome>().AsNoTracking()
            join file in dbContext.Set<PersonnelFile>().AsNoTracking() on income.PersonnelFileId equals file.Id
            where income.TenantId == tenantId &&
                  income.PayrollTypeCode == payrollTypeCode &&
                  income.StatusCode == RecurringIncomeStatuses.Vigente
            select new
            {
                income.Id,
                income.PublicId,
                FilePublicId = file.PublicId,
                income.AssignedPositionPublicId,
                income.ConceptTypeCode,
                income.ConceptNameSnapshot,
            })
            .ToListAsync(cancellationToken);

        var scan = await employeeRepository.GetRecurringIncomeBatchScanAsync(tenantId, payrollTypeCode, cancellationToken);
        var scanById = scan.ToDictionary(item => item.PublicId);

        return identities
            .Where(identity => populationFileIds.Contains(identity.FilePublicId))
            .Where(identity => scanById.ContainsKey(identity.PublicId))
            .Select(identity => new PayrollRecurringIncomeRow(
                identity.Id,
                identity.PublicId,
                identity.FilePublicId,
                identity.AssignedPositionPublicId,
                identity.ConceptTypeCode,
                identity.ConceptNameSnapshot,
                scanById[identity.PublicId]))
            .OrderBy(row => row.InternalId)
            .ToArray();
    }

    private async Task<IReadOnlyList<PayrollRecurringDeductionRow>> BuildRecurringDeductionRowsAsync(
        Guid tenantId,
        string payrollTypeCode,
        IReadOnlySet<Guid> populationFileIds,
        CancellationToken cancellationToken)
    {
        var identities = await (
            from deduction in dbContext.Set<PersonnelFileRecurringDeduction>().AsNoTracking()
            join file in dbContext.Set<PersonnelFile>().AsNoTracking() on deduction.PersonnelFileId equals file.Id
            where deduction.TenantId == tenantId &&
                  deduction.PayrollTypeCode == payrollTypeCode &&
                  deduction.StatusCode == RecurringDeductionStatuses.Vigente
            select new
            {
                deduction.Id,
                deduction.PublicId,
                FilePublicId = file.PublicId,
                deduction.AssignedPositionPublicId,
                deduction.ConceptTypeCode,
                deduction.ConceptNameSnapshot,
            })
            .ToListAsync(cancellationToken);

        var scan = await employeeRepository.GetRecurringDeductionBatchScanAsync(tenantId, payrollTypeCode, cancellationToken);
        var scanById = scan.ToDictionary(item => item.PublicId);

        return identities
            .Where(identity => populationFileIds.Contains(identity.FilePublicId))
            .Where(identity => scanById.ContainsKey(identity.PublicId))
            .Select(identity => new PayrollRecurringDeductionRow(
                identity.Id,
                identity.PublicId,
                identity.FilePublicId,
                identity.AssignedPositionPublicId,
                identity.ConceptTypeCode,
                identity.ConceptNameSnapshot,
                scanById[identity.PublicId]))
            .OrderBy(row => row.InternalId)
            .ToArray();
    }

    private async Task<IReadOnlyList<PayrollRegistroRow>> BuildNotWorkedTimeRowsAsync(
        Guid tenantId,
        PayrollPeriodDefinition period,
        IReadOnlySet<Guid> populationFileIds,
        CancellationToken cancellationToken)
    {
        // In-range REGISTRADO records with a discount, PLUS the REQ-014 lagged ones: records fully BEFORE
        // the period start not yet consumed by an INCLUDED line of a non-annulled run (§0.11 — derived
        // consumption; no age limit in F1).
        var rows = await (
            from record in dbContext.Set<PersonnelFileNotWorkedTime>().AsNoTracking()
            join file in dbContext.Set<PersonnelFile>().AsNoTracking() on record.PersonnelFileId equals file.Id
            where record.TenantId == tenantId &&
                  record.StatusCode == "REGISTRADO" &&
                  record.DiscountAmount > 0m &&
                  record.StartDate <= period.EndDate
            select new
            {
                record.PublicId,
                FilePublicId = file.PublicId,
                record.TypeNameSnapshot,
                record.DeductionConceptTypeCodeSnapshot,
                record.DiscountAmount,
                record.EndDate,
            })
            .ToListAsync(cancellationToken);

        var consumed = (await payrollRunRepository.GetConsumedSourceReferencesAsync(
            tenantId, PayrollSourceModules.NotWorkedTime, cancellationToken)).ToHashSet();

        return rows
            .Where(row => populationFileIds.Contains(row.FilePublicId))
            .Where(row => !consumed.Contains(row.PublicId))
            .Select(row => new PayrollRegistroRow(
                row.PublicId,
                row.FilePublicId,
                row.DeductionConceptTypeCodeSnapshot ?? "TIEMPO_NO_TRABAJADO",
                row.TypeNameSnapshot,
                row.DiscountAmount,
                EmployerAmount: 0m,
                IsCarryover: row.EndDate < period.StartDate))
            .ToArray();
    }

    private async Task<IReadOnlyList<PayrollRegistroRow>> BuildDisciplinaryRowsAsync(
        Guid tenantId,
        PayrollPeriodDefinition period,
        IReadOnlySet<Guid> populationFileIds,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from action in dbContext.Set<PersonnelFileDisciplinaryAction>().AsNoTracking()
            join file in dbContext.Set<PersonnelFile>().AsNoTracking() on action.PersonnelFileId equals file.Id
            where action.TenantId == tenantId &&
                  action.StatusCode == PersonnelTransactionStatuses.Aplicada &&
                  action.HasPayrollDeduction &&
                  action.DeductionAmount != null &&
                  action.IncidentDate <= period.EndDate
            select new
            {
                action.PublicId,
                FilePublicId = file.PublicId,
                action.DeductionConceptTypeCode,
                action.DeductionConceptNameSnapshot,
                action.TypeNameSnapshot,
                action.DeductionAmount,
                action.IncidentDate,
            })
            .ToListAsync(cancellationToken);

        var consumed = (await payrollRunRepository.GetConsumedSourceReferencesAsync(
            tenantId, PayrollSourceModules.Disciplinary, cancellationToken)).ToHashSet();

        return rows
            .Where(row => populationFileIds.Contains(row.FilePublicId))
            .Where(row => !consumed.Contains(row.PublicId))
            .Select(row => new PayrollRegistroRow(
                row.PublicId,
                row.FilePublicId,
                row.DeductionConceptTypeCode ?? "AMONESTACION",
                row.DeductionConceptNameSnapshot ?? row.TypeNameSnapshot,
                row.DeductionAmount!.Value,
                EmployerAmount: 0m,
                IsCarryover: row.IncidentDate < period.StartDate))
            .ToArray();
    }

    private async Task<IReadOnlyList<PayrollRegistroRow>> BuildIncapacityRowsAsync(
        Guid tenantId,
        PayrollPeriodDefinition period,
        IReadOnlySet<Guid> populationFileIds,
        CancellationToken cancellationToken)
    {
        // In-range only — incapacities are NOT part of the REQ-014 carryover (P-03: TNT + disciplinary).
        var rows = await (
            from incapacity in dbContext.Set<PersonnelFileIncapacity>().AsNoTracking()
            join file in dbContext.Set<PersonnelFile>().AsNoTracking() on incapacity.PersonnelFileId equals file.Id
            where incapacity.TenantId == tenantId &&
                  incapacity.StatusCode == IncapacityStatuses.Registrada &&
                  (incapacity.DiscountAmount > 0m || incapacity.EmployerAmount > 0m) &&
                  incapacity.StartDate <= period.EndDate &&
                  (incapacity.EndDate == null || incapacity.EndDate >= period.StartDate)
            select new
            {
                incapacity.PublicId,
                FilePublicId = file.PublicId,
                incapacity.DiscountAmount,
                incapacity.EmployerAmount,
            })
            .ToListAsync(cancellationToken);

        return rows
            .Where(row => populationFileIds.Contains(row.FilePublicId))
            .Select(row => new PayrollRegistroRow(
                row.PublicId,
                row.FilePublicId,
                "INCAPACIDAD",
                "Incapacidad",
                row.DiscountAmount,
                row.EmployerAmount,
                IsCarryover: false))
            .ToArray();
    }
}

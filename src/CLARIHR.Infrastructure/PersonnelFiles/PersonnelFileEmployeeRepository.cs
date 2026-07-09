using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.CompensatoryTime;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.CostCenters;
using CLARIHR.Domain.CompetencyFramework;
using CLARIHR.Domain.JobProfiles;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Domain.PositionSlots;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.PersonnelFiles;

internal sealed class PersonnelFileEmployeeRepository(ApplicationDbContext dbContext) : IPersonnelFileEmployeeRepository
{
    public async Task<PersonnelFileEmployeeProfileResponse> UpsertEmployeeProfileAsync(
        PersonnelFileEmployeeProfile profile,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.Set<PersonnelFileEmployeeProfile>()
            .SingleOrDefaultAsync(
                item => item.TenantId == profile.TenantId && item.PersonnelFileId == profile.PersonnelFileId,
                cancellationToken);

        if (existing is null)
        {
            dbContext.Set<PersonnelFileEmployeeProfile>().Add(profile);
            return Map(profile);
        }

        // Retirement metadata is deliberately NOT part of the upsert (D-01 of the retirement module):
        // ApplyRetirement/ClearRetirement are its only writers.
        existing.Update(
            profile.EmployeeCode,
            profile.EmploymentStatusCode,
            profile.HireDate);

        return Map(existing);
    }

    public async Task<PersonnelFileEmployeeProfileResponse?> GetEmployeeProfileAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileEmployeeProfile>()
            .AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.PersonnelFile.PublicId == personnelFileId, cancellationToken);

        if (item is null)
        {
            return null;
        }

        // Publish the balances owned by this module (§3.10): the SAME pure rules as the incapacity-balance
        // endpoint / the vacation fund detail, so the figures cuadran by construction. Computed OUTSIDE the
        // Map projection (member-init gotcha).
        var disabilityDaysAvailable = await ComputeDisabilityDaysAvailableAsync(item, cancellationToken);
        var vacationDaysAvailable = await ComputeVacationDaysAvailableAsync(item, cancellationToken);
        var compensatoryTimeHoursAvailable = await ComputeCompensatoryTimeHoursAvailableAsync(item, cancellationToken);
        return Map(item) with
        {
            DisabilityDaysAvailable = disabilityDaysAvailable,
            VacationDaysAvailable = vacationDaysAvailable,
            CompensatoryTimeHoursAvailable = compensatoryTimeHoursAvailable,
        };
    }

    /// <summary>
    /// Available compensatory-time fund balance in HOURS (REQ-002 §3.9): Σ credited − Σ debited over the
    /// REGISTRADA credits/absences, via <see cref="CompensatoryTimeRules.Balance"/> — the SAME aggregation the
    /// estado de cuenta uses, so the profile figure and the statement balance cuadran by construction. Null when
    /// the employee has no compensatory-time movement yet (documented in the FE guide).
    /// </summary>
    private async Task<decimal?> ComputeCompensatoryTimeHoursAvailableAsync(
        PersonnelFileEmployeeProfile profile,
        CancellationToken cancellationToken)
    {
        var hasCredits = await dbContext.PersonnelFileCompensatoryTimeCredits
            .AsNoTracking()
            .AnyAsync(credit => credit.PersonnelFileId == profile.PersonnelFileId, cancellationToken);
        var hasAbsences = await dbContext.PersonnelFileCompensatoryTimeAbsences
            .AsNoTracking()
            .AnyAsync(absence => absence.PersonnelFileId == profile.PersonnelFileId, cancellationToken);
        if (!hasCredits && !hasAbsences)
        {
            return null;
        }

        var credited = await dbContext.PersonnelFileCompensatoryTimeCredits
            .AsNoTracking()
            .Where(credit => credit.PersonnelFileId == profile.PersonnelFileId
                && credit.StatusCode == CompensatoryTimeStatuses.Registrada)
            .SumAsync(credit => (decimal?)credit.HoursCredited, cancellationToken) ?? 0m;
        var debited = await dbContext.PersonnelFileCompensatoryTimeAbsences
            .AsNoTracking()
            .Where(absence => absence.PersonnelFileId == profile.PersonnelFileId
                && absence.StatusCode == CompensatoryTimeStatuses.Registrada)
            .SumAsync(absence => (decimal?)absence.HoursDebited, cancellationToken) ?? 0m;

        return CompensatoryTimeRules.Balance(credited, debited);
    }

    /// <summary>
    /// Available enjoyment days of the employee's active fund (§3.10): Σ over active periods with
    /// <c>GeneratesEnjoymentDays</c> of (granted − net consumed), via <see cref="VacationRules.AvailableDays"/>.
    /// Null when the module has no fund data yet for the employee (documented in the FE guide). Delegates to
    /// <see cref="VacationFundQueries.GetAvailableEnjoymentDaysAsync"/> — the SAME derivation the settlement
    /// engine consumes for the VACACION_PROPORCIONAL suggestion (RF-019), so the two never diverge.
    /// </summary>
    private Task<decimal?> ComputeVacationDaysAvailableAsync(
        PersonnelFileEmployeeProfile profile,
        CancellationToken cancellationToken) =>
        VacationFundQueries.GetAvailableEnjoymentDaysAsync(dbContext, profile.PersonnelFileId, cancellationToken);

    /// <summary>
    /// Remaining employer-cap days of the current year for one employee (D-27/§3.10): (covered + benefit)
    /// minus the EmployerDays already consumed by REGISTRADA incapacities of the year, floored at 0.
    /// </summary>
    private async Task<decimal?> ComputeDisabilityDaysAvailableAsync(
        PersonnelFileEmployeeProfile profile,
        CancellationToken cancellationToken)
    {
        var year = DateTime.UtcNow.Year;
        var cap = await dbContext.CompanyPreferences
            .AsNoTracking()
            .Where(preference => preference.TenantId == profile.TenantId)
            .Select(preference => new
            {
                preference.EmployerCoveredIncapacityDaysPerYear,
                preference.AdditionalIncapacityBenefitDaysPerYear,
            })
            .SingleOrDefaultAsync(cancellationToken);

        var consumed = await dbContext.PersonnelFileIncapacities
            .AsNoTracking()
            .Where(incapacity => incapacity.PersonnelFileId == profile.PersonnelFileId
                && incapacity.StatusCode == IncapacityStatuses.Registrada
                && incapacity.StartDate.Year == year)
            .SumAsync(incapacity => (int?)incapacity.EmployerDays, cancellationToken) ?? 0;

        var balance = IncapacityBalanceRules.Compute(
            cap?.EmployerCoveredIncapacityDaysPerYear,
            cap?.AdditionalIncapacityBenefitDaysPerYear,
            consumed);
        return balance.RemainingDays;
    }

    public async Task<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>> AddEmploymentAssignmentAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileEmploymentAssignment entity,
        CancellationToken cancellationToken)
    {
        dbContext.Set<PersonnelFileEmploymentAssignment>().Add(entity);
        // The just-added row is not persisted yet, so an AsNoTracking re-query would exclude it;
        // map the in-memory entity into the returned set so the new assignment is always present.
        var persisted = await dbContext.Set<PersonnelFileEmploymentAssignment>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId)
            .ToArrayAsync(cancellationToken);
        var ordered = persisted.Append(entity)
            .OrderByDescending(item => item.IsPrimary).ThenBy(item => item.StartDate)
            .ToArray();
        return await MapWithSlotLabelsAsync(ordered, cancellationToken);
    }

    public async Task<PersonnelFileEmploymentAssignmentResponse?> UpdateEmploymentAssignmentAsync(
        Guid employmentAssignmentPublicId,
        Guid tenantId,
        string assignmentTypeCode,
        string? contractTypeCode,
        string? workdayCode,
        string? payrollTypeCode,
        Guid? positionSlotPublicId,
        Guid? orgUnitPublicId,
        Guid? workCenterPublicId,
        Guid? costCenterPublicId,
        DateTime startDate,
        DateTime? endDate,
        bool isPrimary,
        string? notes,
        string? paymentMethodCode,
        Guid? paymentBankAccountPublicId,
        int? restDayOfWeek,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileEmploymentAssignment>()
            .SingleOrDefaultAsync(x => x.PublicId == employmentAssignmentPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Update(assignmentTypeCode, contractTypeCode, workdayCode, payrollTypeCode, positionSlotPublicId, orgUnitPublicId, workCenterPublicId, costCenterPublicId, startDate, endDate, isPrimary, notes, paymentMethodCode, paymentBankAccountPublicId, (DayOfWeek?)restDayOfWeek);
        return await MapWithSlotLabelAsync(item, cancellationToken);
    }

    public async Task<PersonnelFileEmploymentAssignmentResponse?> PatchEmploymentAssignmentAsync(
        Guid employmentAssignmentPublicId,
        Guid tenantId,
        string assignmentTypeCode,
        string? contractTypeCode,
        string? workdayCode,
        string? payrollTypeCode,
        Guid? positionSlotPublicId,
        Guid? orgUnitPublicId,
        Guid? workCenterPublicId,
        Guid? costCenterPublicId,
        DateTime startDate,
        DateTime? endDate,
        bool isPrimary,
        string? notes,
        string? paymentMethodCode,
        Guid? paymentBankAccountPublicId,
        int? restDayOfWeek,
        bool isActive,
        bool isActiveMutated,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileEmploymentAssignment>()
            .SingleOrDefaultAsync(x => x.PublicId == employmentAssignmentPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Update(assignmentTypeCode, contractTypeCode, workdayCode, payrollTypeCode, positionSlotPublicId, orgUnitPublicId, workCenterPublicId, costCenterPublicId, startDate, endDate, isPrimary, notes, paymentMethodCode, paymentBankAccountPublicId, (DayOfWeek?)restDayOfWeek);
        if (isActiveMutated)
        {
            item.SetActive(isActive);
        }

        return await MapWithSlotLabelAsync(item, cancellationToken);
    }

    public async Task<bool> DeleteEmploymentAssignmentAsync(
        Guid employmentAssignmentPublicId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileEmploymentAssignment>()
            .SingleOrDefaultAsync(x => x.PublicId == employmentAssignmentPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return false;
        dbContext.Set<PersonnelFileEmploymentAssignment>().Remove(item);
        return true;
    }

    public async Task<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>> GetEmploymentAssignmentsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken)
    {
        var items = await dbContext.Set<PersonnelFileEmploymentAssignment>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => item.StartDate)
            .ToArrayAsync(cancellationToken);
        return await MapWithSlotLabelsAsync(items, cancellationToken);
    }

    public async Task<PersonnelFileEmploymentAssignmentResponse?> GetEmploymentAssignmentAsync(
        Guid personnelFileId,
        Guid employmentAssignmentPublicId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileEmploymentAssignment>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.PersonnelFile.PublicId == personnelFileId && x.PublicId == employmentAssignmentPublicId, cancellationToken);
        return item is null ? null : await MapWithSlotLabelAsync(item, cancellationToken);
    }

    public async Task<int> CountOverlappingActiveAssignmentsForSlotAsync(
        Guid tenantId,
        Guid positionSlotPublicId,
        DateTime startDate,
        DateTime? endDate,
        Guid? excludeAssignmentPublicId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileEmploymentAssignment>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId
                && item.PositionSlotPublicId == positionSlotPublicId
                && item.IsActive
                && (excludeAssignmentPublicId == null || item.PublicId != excludeAssignmentPublicId)
                && (endDate == null || item.StartDate <= endDate)
                && (item.EndDate == null || item.EndDate >= startDate))
            .CountAsync(cancellationToken);

    public async Task DemoteEmploymentAssignmentsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> assignmentPublicIds,
        CancellationToken cancellationToken)
    {
        if (assignmentPublicIds.Count == 0)
        {
            return;
        }

        var ids = assignmentPublicIds.ToArray();
        var items = await dbContext.Set<PersonnelFileEmploymentAssignment>()
            .Where(item => item.TenantId == tenantId && ids.Contains(item.PublicId))
            .ToListAsync(cancellationToken);
        foreach (var item in items)
        {
            item.SetPrimary(false);
        }
    }

    public async Task CloseActiveEmploymentAssignmentsAsync(
        long personnelFileInternalId,
        Guid tenantId,
        DateTime endDateUtc,
        CancellationToken cancellationToken)
    {
        var items = await dbContext.Set<PersonnelFileEmploymentAssignment>()
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId && item.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var item in items)
        {
            // Preserve an already-set end date; only stamp the rehire boundary on still-open rows.
            if (item.EndDate is null)
            {
                item.Close(endDateUtc);
            }
            else
            {
                item.SetActive(false);
            }
        }
    }

    public async Task CloseActiveContractHistoriesAsync(
        long personnelFileInternalId,
        Guid tenantId,
        DateTime endDateUtc,
        CancellationToken cancellationToken)
    {
        var items = await dbContext.Set<PersonnelFileContractHistory>()
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId && item.IsActive)
            .ToListAsync(cancellationToken);
        foreach (var item in items)
        {
            if (item.ContractEndDate is null)
            {
                item.Close(endDateUtc);
            }
            else
            {
                item.SetActive(false);
            }
        }
    }

    public async Task<IReadOnlyCollection<PersonnelFileContractHistoryResponse>> AddContractHistoryAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileContractHistory entity,
        CancellationToken cancellationToken)
    {
        dbContext.Set<PersonnelFileContractHistory>().Add(entity);
        // The just-added row is not persisted yet (SaveChanges runs in the handler), so an AsNoTracking
        // re-query excludes it; append the in-memory entity so the new row is always present in the result
        // (otherwise the handler's `SingleOrDefault(... == entity.PublicId) ?? throw` 500s on every create).
        var persisted = await dbContext.Set<PersonnelFileContractHistory>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId)
            .ToArrayAsync(cancellationToken);
        return persisted.Append(entity)
            .OrderByDescending(item => item.ContractDate)
            .Select(Map)
            .ToArray();
    }

    public async Task<PersonnelFileContractHistoryResponse?> UpdateContractHistoryAsync(
        Guid contractHistoryPublicId,
        Guid tenantId,
        string contractTypeCode,
        DateTime contractDate,
        DateTime? contractEndDate,
        Guid? positionSlotPublicId,
        string? notes,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileContractHistory>()
            .SingleOrDefaultAsync(x => x.PublicId == contractHistoryPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Update(contractTypeCode, contractDate, contractEndDate, positionSlotPublicId, notes);
        return Map(item);
    }

    public async Task<PersonnelFileContractHistoryResponse?> PatchContractHistoryAsync(
        Guid contractHistoryPublicId,
        Guid tenantId,
        string contractTypeCode,
        DateTime contractDate,
        DateTime? contractEndDate,
        Guid? positionSlotPublicId,
        string? notes,
        bool isActive,
        bool isActiveMutated,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileContractHistory>()
            .SingleOrDefaultAsync(x => x.PublicId == contractHistoryPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Update(contractTypeCode, contractDate, contractEndDate, positionSlotPublicId, notes);
        if (isActiveMutated)
        {
            item.SetActive(isActive);
        }

        return Map(item);
    }

    public async Task<IReadOnlyCollection<PersonnelFileContractHistoryResponse>> GetContractHistoryAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileContractHistory>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderByDescending(item => item.ContractDate)
            .Select(item => Map(item))
            .ToArrayAsync(cancellationToken);

    public async Task<PersonnelFileContractHistoryResponse?> GetContractHistoryAsync(
        Guid personnelFileId,
        Guid contractHistoryPublicId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileContractHistory>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.PersonnelFile.PublicId == personnelFileId && x.PublicId == contractHistoryPublicId, cancellationToken);
        return item is null ? null : Map(item);
    }

    public async Task<PersonnelFilePositionHierarchyResponse> GetPositionHierarchyAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken)
    {
        var file = await dbContext.Set<PersonnelFile>()
            .AsNoTracking()
            .SingleAsync(item => item.PublicId == personnelFileId, cancellationToken);

        // Org unit is no longer stored on the profile; resolve it from the active primary assignment
        // (the plaza relationship), falling back to the file's own org unit.
        var primaryAssignmentOrgUnit = await dbContext.Set<PersonnelFileEmploymentAssignment>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId && item.IsActive && item.IsPrimary)
            .OrderBy(item => item.StartDate)
            .Select(item => item.OrgUnitPublicId)
            .FirstOrDefaultAsync(cancellationToken);

        var resolvedOrgUnit = primaryAssignmentOrgUnit ?? file.OrgUnitPublicId;

        var subordinates = resolvedOrgUnit.HasValue
            ? await dbContext.Set<PersonnelFile>()
                .AsNoTracking()
                .Where(item => item.RecordType == PersonnelFileRecordType.Employee &&
                               item.PublicId != personnelFileId &&
                               item.OrgUnitPublicId == resolvedOrgUnit.Value)
                .OrderBy(item => item.FullName)
                .Take(200)
                .Select(item => new PersonnelFilePositionHierarchySubordinateResponse(
                    item.PublicId,
                    item.FullName,
                    item.OrgUnitPublicId))
                .ToArrayAsync(cancellationToken)
            : Array.Empty<PersonnelFilePositionHierarchySubordinateResponse>();

        return new PersonnelFilePositionHierarchyResponse(
            personnelFileId,
            resolvedOrgUnit,
            ImmediateSupervisorPersonnelFileId: null,
            ImmediateSupervisorName: null,
            subordinates);
    }

    public async Task<IReadOnlyCollection<PersonnelFileCompensationConceptResponse>> AddCompensationConceptAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileCompensationConcept entity,
        CancellationToken cancellationToken)
    {
        dbContext.Set<PersonnelFileCompensationConcept>().Add(entity);
        // Append the in-memory entity: the row is not saved yet, so an AsNoTracking re-query excludes it and
        // the handler's `SingleOrDefault(... == entity.PublicId) ?? throw` would 500 on every create.
        var persisted = await dbContext.Set<PersonnelFileCompensationConcept>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId)
            .ToArrayAsync(cancellationToken);
        return persisted.Append(entity)
            .OrderByDescending(item => item.IsActive).ThenBy(item => item.StartDate)
            .Select(Map)
            .ToArray();
    }

    public async Task<PersonnelFileCompensationConceptResponse?> UpdateCompensationConceptAsync(
        Guid compensationConceptPublicId,
        Guid tenantId,
        Guid? assignedPositionPublicId,
        CompensationNature nature,
        string conceptTypeCode,
        DeductionClass? deductionClass,
        CompensationCalculationType calculationType,
        decimal value,
        string? calculationBaseCode,
        decimal? employerRate,
        decimal? contributionCap,
        string currencyCode,
        string payPeriodCode,
        string? counterpartyName,
        string? externalReference,
        DateTime startDate,
        DateTime? endDate,
        string? notes,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileCompensationConcept>()
            .SingleOrDefaultAsync(x => x.PublicId == compensationConceptPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Update(assignedPositionPublicId, nature, conceptTypeCode, deductionClass, calculationType, value, calculationBaseCode, employerRate, contributionCap, currencyCode, payPeriodCode, counterpartyName, externalReference, startDate, endDate, notes);
        return Map(item);
    }

    public async Task<PersonnelFileCompensationConceptResponse?> PatchCompensationConceptAsync(
        Guid compensationConceptPublicId,
        Guid tenantId,
        Guid? assignedPositionPublicId,
        CompensationNature nature,
        string conceptTypeCode,
        DeductionClass? deductionClass,
        CompensationCalculationType calculationType,
        decimal value,
        string? calculationBaseCode,
        decimal? employerRate,
        decimal? contributionCap,
        string currencyCode,
        string payPeriodCode,
        string? counterpartyName,
        string? externalReference,
        DateTime startDate,
        DateTime? endDate,
        string? notes,
        bool isActive,
        bool isActiveMutated,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileCompensationConcept>()
            .SingleOrDefaultAsync(x => x.PublicId == compensationConceptPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Update(assignedPositionPublicId, nature, conceptTypeCode, deductionClass, calculationType, value, calculationBaseCode, employerRate, contributionCap, currencyCode, payPeriodCode, counterpartyName, externalReference, startDate, endDate, notes);
        if (isActiveMutated)
        {
            item.SetActive(isActive);
        }

        return Map(item);
    }

    public async Task<bool> DeleteCompensationConceptAsync(
        Guid compensationConceptPublicId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileCompensationConcept>()
            .SingleOrDefaultAsync(x => x.PublicId == compensationConceptPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return false;
        dbContext.Set<PersonnelFileCompensationConcept>().Remove(item);
        return true;
    }

    public async Task<IReadOnlyCollection<PersonnelFileCompensationConceptResponse>> GetCompensationConceptsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileCompensationConcept>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderByDescending(item => item.IsActive)
            .ThenBy(item => item.StartDate)
            .Select(item => Map(item))
            .ToArrayAsync(cancellationToken);

    public async Task<PersonnelFileCompensationConceptResponse?> GetCompensationConceptAsync(
        Guid personnelFileId,
        Guid compensationConceptPublicId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileCompensationConcept>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.PersonnelFile.PublicId == personnelFileId && x.PublicId == compensationConceptPublicId, cancellationToken);
        return item is null ? null : Map(item);
    }

    public async Task<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>> AddAdditionalBenefitAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileAdditionalBenefit entity,
        CancellationToken cancellationToken)
    {
        dbContext.Set<PersonnelFileAdditionalBenefit>().Add(entity);
        // Append the in-memory entity: the row is not saved yet, so an AsNoTracking re-query excludes it and
        // the handler's `SingleOrDefault(... == entity.PublicId) ?? throw` would 500 on every create.
        var persisted = await dbContext.Set<PersonnelFileAdditionalBenefit>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId)
            .ToArrayAsync(cancellationToken);
        return persisted.Append(entity)
            .OrderByDescending(item => item.IsActive).ThenBy(item => item.BenefitTypeCode)
            .Select(Map)
            .ToArray();
    }

    public async Task<PersonnelFileAdditionalBenefitResponse?> UpdateAdditionalBenefitAsync(
        Guid additionalBenefitPublicId,
        Guid tenantId,
        string benefitTypeCode,
        DateTime? startDate,
        DateTime? endDate,
        string? notes,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileAdditionalBenefit>()
            .SingleOrDefaultAsync(x => x.PublicId == additionalBenefitPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Update(benefitTypeCode, startDate, endDate, notes);
        return Map(item);
    }

    public async Task<PersonnelFileAdditionalBenefitResponse?> PatchAdditionalBenefitAsync(
        Guid additionalBenefitPublicId,
        Guid tenantId,
        string benefitTypeCode,
        DateTime? startDate,
        DateTime? endDate,
        string? notes,
        bool isActive,
        bool isActiveMutated,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileAdditionalBenefit>()
            .SingleOrDefaultAsync(x => x.PublicId == additionalBenefitPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Update(benefitTypeCode, startDate, endDate, notes);
        if (isActiveMutated)
        {
            item.SetActive(isActive);
        }

        return Map(item);
    }

    public async Task<bool> DeleteAdditionalBenefitAsync(
        Guid additionalBenefitPublicId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileAdditionalBenefit>()
            .SingleOrDefaultAsync(x => x.PublicId == additionalBenefitPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return false;
        dbContext.Set<PersonnelFileAdditionalBenefit>().Remove(item);
        return true;
    }

    public async Task<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>> GetAdditionalBenefitsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileAdditionalBenefit>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderByDescending(item => item.IsActive)
            .ThenBy(item => item.BenefitTypeCode)
            .Select(item => Map(item))
            .ToArrayAsync(cancellationToken);

    public async Task<PersonnelFileAdditionalBenefitResponse?> GetAdditionalBenefitAsync(
        Guid personnelFileId,
        Guid additionalBenefitPublicId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileAdditionalBenefit>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.PersonnelFile.PublicId == personnelFileId && x.PublicId == additionalBenefitPublicId, cancellationToken);
        return item is null ? null : Map(item);
    }

    public async Task<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>> AddAuthorizationSubstitutionAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileAuthorizationSubstitution entity,
        CancellationToken cancellationToken)
    {
        dbContext.Set<PersonnelFileAuthorizationSubstitution>().Add(entity);
        // Append the in-memory entity: the row is not saved yet, so an AsNoTracking re-query excludes it and
        // the handler's `SingleOrDefault(... == entity.PublicId) ?? throw` would 500 on every create.
        var persisted = await dbContext.Set<PersonnelFileAuthorizationSubstitution>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId)
            .ToArrayAsync(cancellationToken);
        return persisted.Append(entity)
            .OrderByDescending(item => item.IsActive).ThenBy(item => item.StartDate)
            .Select(Map)
            .ToArray();
    }

    public async Task<PersonnelFileAuthorizationSubstitutionResponse?> UpdateAuthorizationSubstitutionAsync(
        Guid authorizationSubstitutionPublicId,
        Guid tenantId,
        string substitutionTypeCode,
        Guid substitutePersonnelFilePublicId,
        Guid substitutePositionSlotPublicId,
        string? substitutePositionTitleSnapshot,
        DateTime startDate,
        DateTime endDate,
        string? notes,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileAuthorizationSubstitution>()
            .SingleOrDefaultAsync(x => x.PublicId == authorizationSubstitutionPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Update(substitutionTypeCode, substitutePersonnelFilePublicId, substitutePositionSlotPublicId, substitutePositionTitleSnapshot, startDate, endDate, notes);
        return Map(item);
    }

    public async Task<PersonnelFileAuthorizationSubstitutionResponse?> PatchAuthorizationSubstitutionAsync(
        Guid authorizationSubstitutionPublicId,
        Guid tenantId,
        string substitutionTypeCode,
        Guid substitutePersonnelFilePublicId,
        Guid substitutePositionSlotPublicId,
        string? substitutePositionTitleSnapshot,
        DateTime startDate,
        DateTime endDate,
        string? notes,
        bool isActive,
        bool isActiveMutated,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileAuthorizationSubstitution>()
            .SingleOrDefaultAsync(x => x.PublicId == authorizationSubstitutionPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Update(substitutionTypeCode, substitutePersonnelFilePublicId, substitutePositionSlotPublicId, substitutePositionTitleSnapshot, startDate, endDate, notes);
        if (isActiveMutated)
        {
            item.SetActive(isActive);
        }

        return Map(item);
    }

    public async Task<bool> DeleteAuthorizationSubstitutionAsync(
        Guid authorizationSubstitutionPublicId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileAuthorizationSubstitution>()
            .SingleOrDefaultAsync(x => x.PublicId == authorizationSubstitutionPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return false;
        dbContext.Set<PersonnelFileAuthorizationSubstitution>().Remove(item);
        return true;
    }

    public async Task<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>> GetAuthorizationSubstitutionsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileAuthorizationSubstitution>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderByDescending(item => item.IsActive)
            .ThenBy(item => item.StartDate)
            .Select(item => Map(item))
            .ToArrayAsync(cancellationToken);

    public async Task<PersonnelFileAuthorizationSubstitutionResponse?> GetAuthorizationSubstitutionAsync(
        Guid personnelFileId,
        Guid authorizationSubstitutionPublicId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileAuthorizationSubstitution>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.PersonnelFile.PublicId == personnelFileId && x.PublicId == authorizationSubstitutionPublicId, cancellationToken);
        return item is null ? null : Map(item);
    }

    public Task<PersonnelFilePersonnelActionResponse> AddPersonnelActionAsync(
        PersonnelFilePersonnelAction entity,
        CancellationToken cancellationToken)
    {
        dbContext.Set<PersonnelFilePersonnelAction>().Add(entity);
        return Task.FromResult(Map(entity));
    }

    public async Task<PersonnelFilePersonnelActionResponse?> GetPersonnelActionAsync(
        Guid personnelFileId,
        Guid personnelActionPublicId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFilePersonnelAction>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.PersonnelFile.PublicId == personnelFileId && x.PublicId == personnelActionPublicId, cancellationToken);
        return item is null ? null : Map(item);
    }

    public async Task<PagedResponse<PersonnelFilePersonnelActionResponse>> SearchPersonnelActionsAsync(
        Guid personnelFileId,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? type,
        string? status,
        string? search,
        string? sortBy,
        PersonnelFileSortDirection sortDirection,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = BuildPersonnelActionsBaseQuery(personnelFileId);
        query = ApplyPersonnelActionFilters(query, fromUtc, toUtc, type, status, search);

        var ordered = ApplyPersonnelActionSorting(query, sortBy, sortDirection);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await ordered
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(item => Map(item))
            .ToArrayAsync(cancellationToken);

        return new PagedResponse<PersonnelFilePersonnelActionResponse>(items, pageNumber, pageSize, totalCount);
    }

    public async Task<IReadOnlyCollection<PersonnelFilePersonnelActionExportRow>> ExportPersonnelActionsAsync(
        Guid personnelFileId,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? type,
        string? status,
        string? search,
        string? sortBy,
        PersonnelFileSortDirection sortDirection,
        int? maxRows,
        CancellationToken cancellationToken)
    {
        var query = BuildPersonnelActionsBaseQuery(personnelFileId);
        query = ApplyPersonnelActionFilters(query, fromUtc, toUtc, type, status, search);

        var ordered = ApplyPersonnelActionSorting(query, sortBy, sortDirection);
        IQueryable<PersonnelFilePersonnelAction> limited = ordered;
        if (maxRows.HasValue)
        {
            limited = limited.Take(maxRows.Value);
        }

        var items = await limited
            .Select(item => new PersonnelFilePersonnelActionExportRow(
                item.PublicId,
                item.ActionTypeCode,
                item.ActionStatusCode,
                item.ActionDateUtc,
                item.EffectiveFromUtc,
                item.EffectiveToUtc,
                item.Description,
                item.Reference,
                item.Amount,
                item.CurrencyCode,
                item.IsSystemGenerated,
                item.CreatedUtc,
                item.ModifiedUtc))
            .ToArrayAsync(cancellationToken);

        return items;
    }

    public Task<PersonnelFilePayrollTransactionResponse> AddPayrollTransactionAsync(
        PersonnelFilePayrollTransaction entity,
        CancellationToken cancellationToken)
    {
        dbContext.Set<PersonnelFilePayrollTransaction>().Add(entity);
        return Task.FromResult(Map(entity));
    }

    public async Task<PersonnelFilePayrollTransactionResponse?> GetPayrollTransactionAsync(
        Guid personnelFileId,
        Guid payrollTransactionPublicId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFilePayrollTransaction>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.PersonnelFile.PublicId == personnelFileId && x.PublicId == payrollTransactionPublicId, cancellationToken);
        return item is null ? null : Map(item);
    }

    public async Task<PersonnelFilePayrollTransactionResponse?> PatchPayrollTransactionAsync(
        Guid payrollTransactionPublicId,
        Guid tenantId,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFilePayrollTransaction>()
            .SingleOrDefaultAsync(x => x.PublicId == payrollTransactionPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.SetActive(isActive);
        return Map(item);
    }

    public async Task<PagedResponse<PersonnelFilePayrollTransactionResponse>> SearchPayrollTransactionsAsync(
        Guid personnelFileId,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? type,
        string? status,
        string? search,
        string? sortBy,
        PersonnelFileSortDirection sortDirection,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = BuildPayrollTransactionsBaseQuery(personnelFileId);
        query = ApplyPayrollTransactionFilters(query, fromUtc, toUtc, type, status, search);

        var ordered = ApplyPayrollTransactionSorting(query, sortBy, sortDirection);
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await ordered
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(item => Map(item))
            .ToArrayAsync(cancellationToken);

        return new PagedResponse<PersonnelFilePayrollTransactionResponse>(items, pageNumber, pageSize, totalCount);
    }

    public async Task<IReadOnlyCollection<PersonnelFilePayrollTransactionExportRow>> ExportPayrollTransactionsAsync(
        Guid personnelFileId,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? type,
        string? status,
        string? search,
        string? sortBy,
        PersonnelFileSortDirection sortDirection,
        int? maxRows,
        CancellationToken cancellationToken)
    {
        var query = BuildPayrollTransactionsBaseQuery(personnelFileId);
        query = ApplyPayrollTransactionFilters(query, fromUtc, toUtc, type, status, search);

        var ordered = ApplyPayrollTransactionSorting(query, sortBy, sortDirection);
        IQueryable<PersonnelFilePayrollTransaction> limited = ordered;
        if (maxRows.HasValue)
        {
            limited = limited.Take(maxRows.Value);
        }

        var items = await limited
            .Select(item => new PersonnelFilePayrollTransactionExportRow(
                item.PublicId,
                item.TransactionTypeCode,
                item.TransactionDateUtc,
                item.PayrollPeriodCode,
                item.Description,
                item.Amount,
                item.CurrencyCode,
                item.IsDebit,
                item.SourceSystem,
                item.SourceReference,
                item.SourceSyncedUtc,
                item.CreatedUtc,
                item.ModifiedUtc))
            .ToArrayAsync(cancellationToken);

        return items;
    }

    public async Task<IReadOnlyCollection<PersonnelFileAssetAccessResponse>> AddAssetAccessAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileAssetAccess entity,
        CancellationToken cancellationToken)
    {
        dbContext.Set<PersonnelFileAssetAccess>().Add(entity);
        // Append the in-memory entity: the row is not saved yet, so an AsNoTracking re-query excludes it and
        // the handler's `SingleOrDefault(... == entity.PublicId) ?? throw` would 500 on every create.
        var persisted = await dbContext.Set<PersonnelFileAssetAccess>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId)
            .ToArrayAsync(cancellationToken);
        return persisted.Append(entity)
            .OrderByDescending(item => item.IsActive).ThenBy(item => item.StartDateUtc)
            .Select(Map)
            .ToArray();
    }

    public async Task<PersonnelFileAssetAccessResponse?> UpdateAssetAccessAsync(
        Guid assetAccessPublicId,
        Guid tenantId,
        string assetTypeCode,
        string assetOrAccessName,
        string? accessLevelCode,
        DateTime startDateUtc,
        DateTime? endDateUtc,
        DateTime? deliveryDateUtc,
        string? deliveryStatusCode,
        string? notes,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileAssetAccess>()
            .SingleOrDefaultAsync(x => x.PublicId == assetAccessPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Update(assetTypeCode, assetOrAccessName, accessLevelCode, startDateUtc, endDateUtc, deliveryDateUtc, deliveryStatusCode, notes);
        return Map(item);
    }

    public async Task<PersonnelFileAssetAccessResponse?> PatchAssetAccessAsync(
        Guid assetAccessPublicId,
        Guid tenantId,
        string assetTypeCode,
        string assetOrAccessName,
        string? accessLevelCode,
        DateTime startDateUtc,
        DateTime? endDateUtc,
        DateTime? deliveryDateUtc,
        string? deliveryStatusCode,
        string? notes,
        bool isActive,
        bool isActiveMutated,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileAssetAccess>()
            .SingleOrDefaultAsync(x => x.PublicId == assetAccessPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Update(assetTypeCode, assetOrAccessName, accessLevelCode, startDateUtc, endDateUtc, deliveryDateUtc, deliveryStatusCode, notes);
        if (isActiveMutated)
        {
            item.SetActive(isActive);
        }

        return Map(item);
    }

    public async Task<bool> DeleteAssetAccessAsync(
        Guid assetAccessPublicId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileAssetAccess>()
            .SingleOrDefaultAsync(x => x.PublicId == assetAccessPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return false;
        dbContext.Set<PersonnelFileAssetAccess>().Remove(item);
        return true;
    }

    public async Task<IReadOnlyCollection<PersonnelFileAssetAccessResponse>> GetAssetsAccessesAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileAssetAccess>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderByDescending(item => item.IsActive)
            .ThenBy(item => item.StartDateUtc)
            .Select(item => Map(item))
            .ToArrayAsync(cancellationToken);

    public async Task<PersonnelFileAssetAccessResponse?> GetAssetAccessAsync(
        Guid personnelFileId,
        Guid assetAccessPublicId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileAssetAccess>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.PersonnelFile.PublicId == personnelFileId && x.PublicId == assetAccessPublicId, cancellationToken);
        return item is null ? null : Map(item);
    }

    public async Task<IReadOnlyCollection<PersonnelFileInsuranceResponse>> AddInsuranceAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileInsurance entity,
        CancellationToken cancellationToken)
    {
        dbContext.Set<PersonnelFileInsurance>().Add(entity);
        // Append the in-memory entity: the row is not saved yet, so an AsNoTracking re-query excludes it and
        // the handler's `SingleOrDefault(... == entity.PublicId) ?? throw` would 500 on every create. The new
        // insurance has an empty Beneficiaries collection, which Map renders as an empty array.
        var persisted = await dbContext.Set<PersonnelFileInsurance>()
            .AsNoTracking()
            .Include(item => item.Beneficiaries)
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId)
            .ToListAsync(cancellationToken);
        return persisted.Append(entity)
            .OrderByDescending(item => item.IsActive).ThenBy(item => item.InsuranceCode)
            .Select(Map)
            .ToArray();
    }

    public async Task<PersonnelFileInsuranceResponse?> UpdateInsuranceAsync(
        Guid insurancePublicId,
        Guid tenantId,
        string insuranceCode,
        decimal? employeeContribution,
        decimal? employerContribution,
        string? rangeCode,
        string? policyNumber,
        decimal? insuredAmount,
        string? currencyCode,
        DateTime? startDateUtc,
        DateTime? endDateUtc,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileInsurance>()
            .Include(x => x.Beneficiaries)
            .SingleOrDefaultAsync(x => x.PublicId == insurancePublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Update(insuranceCode, employeeContribution, employerContribution, rangeCode, policyNumber, insuredAmount, currencyCode, startDateUtc, endDateUtc);
        return Map(item);
    }

    public async Task<PersonnelFileInsuranceResponse?> PatchInsuranceAsync(
        Guid insurancePublicId,
        Guid tenantId,
        string insuranceCode,
        decimal? employeeContribution,
        decimal? employerContribution,
        string? rangeCode,
        string? policyNumber,
        decimal? insuredAmount,
        string? currencyCode,
        DateTime? startDateUtc,
        DateTime? endDateUtc,
        bool isActive,
        bool isActiveMutated,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileInsurance>()
            .Include(x => x.Beneficiaries)
            .SingleOrDefaultAsync(x => x.PublicId == insurancePublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Update(insuranceCode, employeeContribution, employerContribution, rangeCode, policyNumber, insuredAmount, currencyCode, startDateUtc, endDateUtc);
        if (isActiveMutated)
        {
            item.SetActive(isActive);
        }

        return Map(item);
    }

    public async Task<bool> DeleteInsuranceAsync(
        Guid insurancePublicId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileInsurance>()
            .SingleOrDefaultAsync(x => x.PublicId == insurancePublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return false;
        dbContext.Set<PersonnelFileInsurance>().Remove(item);
        return true;
    }

    public async Task<IReadOnlyCollection<PersonnelFileInsuranceResponse>> GetInsurancesAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken)
    {
        var items = await dbContext.Set<PersonnelFileInsurance>()
            .AsNoTracking()
            .Include(item => item.Beneficiaries)
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderByDescending(item => item.IsActive)
            .ThenBy(item => item.InsuranceCode)
            .ToListAsync(cancellationToken);

        return items.Select(Map).ToArray();
    }

    public async Task<PersonnelFileInsuranceResponse?> GetInsuranceAsync(
        Guid personnelFileId,
        Guid insurancePublicId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileInsurance>()
            .AsNoTracking()
            .Include(x => x.Beneficiaries)
            .SingleOrDefaultAsync(x => x.PersonnelFile.PublicId == personnelFileId && x.PublicId == insurancePublicId, cancellationToken);
        return item is null ? null : Map(item);
    }

    public async Task<PersonnelFileInsuranceBeneficiaryResponse?> AddInsuranceBeneficiaryAsync(
        Guid personnelFileId,
        Guid insurancePublicId,
        Guid tenantId,
        InsuranceBeneficiaryInput item,
        CancellationToken cancellationToken)
    {
        var insurance = await dbContext.Set<PersonnelFileInsurance>()
            .SingleOrDefaultAsync(
                x => x.PersonnelFile.PublicId == personnelFileId && x.PublicId == insurancePublicId && x.TenantId == tenantId,
                cancellationToken);
        if (insurance is null) return null;

        var entity = PersonnelFileInsuranceBeneficiary.Create(item.FullName, item.DocumentNumber, item.DocumentTypeCode, item.BirthDate, item.KinshipCode, item.AllocationPercentage, item.BeneficiaryType);
        entity.SetTenantId(tenantId);
        entity.BindToInsurance(insurance.Id);
        dbContext.Set<PersonnelFileInsuranceBeneficiary>().Add(entity);
        return Map(entity);
    }

    public async Task<IReadOnlyCollection<PersonnelFileInsuranceBeneficiaryResponse>> GetInsuranceBeneficiariesAsync(
        Guid personnelFileId,
        Guid insurancePublicId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileInsuranceBeneficiary>()
            .AsNoTracking()
            .Where(item => item.Insurance.PersonnelFile.PublicId == personnelFileId && item.Insurance.PublicId == insurancePublicId)
            .OrderByDescending(item => item.IsActive)
            .ThenBy(item => item.FullName)
            .Select(item => Map(item))
            .ToArrayAsync(cancellationToken);

    public async Task<PersonnelFileInsuranceBeneficiaryResponse?> GetInsuranceBeneficiaryAsync(
        Guid personnelFileId,
        Guid insurancePublicId,
        Guid beneficiaryPublicId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileInsuranceBeneficiary>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Insurance.PersonnelFile.PublicId == personnelFileId
                    && x.Insurance.PublicId == insurancePublicId
                    && x.PublicId == beneficiaryPublicId,
                cancellationToken);
        return item is null ? null : Map(item);
    }

    public async Task<PersonnelFileInsuranceBeneficiaryResponse?> UpdateInsuranceBeneficiaryAsync(
        Guid personnelFileId,
        Guid insurancePublicId,
        Guid beneficiaryPublicId,
        Guid tenantId,
        string fullName,
        string? documentNumber,
        DateTime? birthDate,
        string kinshipCode,
        string? documentTypeCode,
        decimal? allocationPercentage,
        string? beneficiaryType,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileInsuranceBeneficiary>()
            .SingleOrDefaultAsync(
                x => x.Insurance.PersonnelFile.PublicId == personnelFileId
                    && x.Insurance.PublicId == insurancePublicId
                    && x.PublicId == beneficiaryPublicId
                    && x.TenantId == tenantId,
                cancellationToken);
        if (item is null) return null;
        item.Update(fullName, documentNumber, documentTypeCode, birthDate, kinshipCode, allocationPercentage, beneficiaryType);
        return Map(item);
    }

    public async Task<PersonnelFileInsuranceBeneficiaryResponse?> PatchInsuranceBeneficiaryAsync(
        Guid personnelFileId,
        Guid insurancePublicId,
        Guid beneficiaryPublicId,
        Guid tenantId,
        string fullName,
        string? documentNumber,
        DateTime? birthDate,
        string kinshipCode,
        string? documentTypeCode,
        decimal? allocationPercentage,
        string? beneficiaryType,
        bool isActive,
        bool isActiveMutated,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileInsuranceBeneficiary>()
            .SingleOrDefaultAsync(
                x => x.Insurance.PersonnelFile.PublicId == personnelFileId
                    && x.Insurance.PublicId == insurancePublicId
                    && x.PublicId == beneficiaryPublicId
                    && x.TenantId == tenantId,
                cancellationToken);
        if (item is null) return null;
        item.Update(fullName, documentNumber, documentTypeCode, birthDate, kinshipCode, allocationPercentage, beneficiaryType);
        if (isActiveMutated)
        {
            item.SetActive(isActive);
        }

        return Map(item);
    }

    public async Task<bool> DeleteInsuranceBeneficiaryAsync(
        Guid personnelFileId,
        Guid insurancePublicId,
        Guid beneficiaryPublicId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileInsuranceBeneficiary>()
            .SingleOrDefaultAsync(
                x => x.Insurance.PersonnelFile.PublicId == personnelFileId
                    && x.Insurance.PublicId == insurancePublicId
                    && x.PublicId == beneficiaryPublicId
                    && x.TenantId == tenantId,
                cancellationToken);
        if (item is null) return false;
        dbContext.Set<PersonnelFileInsuranceBeneficiary>().Remove(item);
        return true;
    }

    public async Task<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>> AddMedicalClaimAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileMedicalClaim entity,
        CancellationToken cancellationToken)
    {
        dbContext.Set<PersonnelFileMedicalClaim>().Add(entity);
        // Append the in-memory entity: the row is not saved yet, so an AsNoTracking re-query excludes it and
        // the handler's `SingleOrDefault(... == entity.PublicId) ?? throw` would 500 on every create.
        var persisted = await dbContext.Set<PersonnelFileMedicalClaim>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId)
            .ToArrayAsync(cancellationToken);
        return persisted.Append(entity)
            .OrderByDescending(item => item.ClaimDateUtc)
            .Select(Map)
            .ToArray();
    }

    public async Task<PersonnelFileMedicalClaimResponse?> UpdateMedicalClaimAsync(
        Guid medicalClaimPublicId,
        Guid tenantId,
        MedicalClaimInput input,
        string? insuranceNameSnapshot,
        string? patientNameSnapshot,
        string? kinshipCodeSnapshot,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileMedicalClaim>()
            .SingleOrDefaultAsync(x => x.PublicId == medicalClaimPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        ApplyMedicalClaimInput(item, input, insuranceNameSnapshot, patientNameSnapshot, kinshipCodeSnapshot);
        return Map(item);
    }

    public async Task<PersonnelFileMedicalClaimResponse?> PatchMedicalClaimAsync(
        Guid medicalClaimPublicId,
        Guid tenantId,
        MedicalClaimInput input,
        string? insuranceNameSnapshot,
        string? patientNameSnapshot,
        string? kinshipCodeSnapshot,
        bool isActive,
        bool isActiveMutated,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileMedicalClaim>()
            .SingleOrDefaultAsync(x => x.PublicId == medicalClaimPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        ApplyMedicalClaimInput(item, input, insuranceNameSnapshot, patientNameSnapshot, kinshipCodeSnapshot);
        if (isActiveMutated)
        {
            item.SetActive(isActive);
        }

        return Map(item);
    }

    private static void ApplyMedicalClaimInput(
        PersonnelFileMedicalClaim item,
        MedicalClaimInput input,
        string? insuranceNameSnapshot,
        string? patientNameSnapshot,
        string? kinshipCodeSnapshot) =>
        item.Update(
            input.InsurancePublicId,
            insuranceNameSnapshot,
            input.AccountNumber,
            input.ClaimantType,
            input.BeneficiaryPublicId,
            patientNameSnapshot,
            kinshipCodeSnapshot,
            input.ClaimTypeCode,
            input.Diagnosis,
            input.ClaimAmount,
            input.CurrencyCode,
            input.PaidAmount,
            input.Notes,
            input.ClaimDateUtc,
            input.ResolutionDateUtc,
            input.ClaimStatusCode,
            input.SourceSystem,
            input.SourceReference,
            input.SourceSyncedUtc);

    public async Task<bool> DeleteMedicalClaimAsync(
        Guid medicalClaimPublicId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileMedicalClaim>()
            .SingleOrDefaultAsync(x => x.PublicId == medicalClaimPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return false;
        dbContext.Set<PersonnelFileMedicalClaim>().Remove(item);
        return true;
    }

    public async Task<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>> GetMedicalClaimsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileMedicalClaim>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderByDescending(item => item.ClaimDateUtc)
            .Select(item => Map(item))
            .ToArrayAsync(cancellationToken);

    public async Task<PersonnelFileMedicalClaimResponse?> GetMedicalClaimAsync(
        Guid personnelFileId,
        Guid medicalClaimPublicId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileMedicalClaim>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.PersonnelFile.PublicId == personnelFileId && x.PublicId == medicalClaimPublicId, cancellationToken);
        return item is null ? null : Map(item);
    }

    public async Task<long?> GetMedicalClaimInternalIdAsync(
        Guid personnelFileId,
        Guid medicalClaimPublicId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileMedicalClaim>()
            .AsNoTracking()
            .Where(claim => claim.PersonnelFile.PublicId == personnelFileId && claim.PublicId == medicalClaimPublicId)
            .Select(claim => (long?)claim.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddMedicalClaimDocumentAsync(
        MedicalClaimDocument entity,
        CancellationToken cancellationToken)
    {
        await dbContext.Set<MedicalClaimDocument>().AddAsync(entity, cancellationToken);
    }

    public async Task<IReadOnlyCollection<MedicalClaimDocumentResponse>> GetMedicalClaimDocumentsAsync(
        Guid medicalClaimPublicId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<MedicalClaimDocument>()
            .AsNoTracking()
            .Where(document => document.MedicalClaim.PublicId == medicalClaimPublicId && document.IsActive)
            .OrderByDescending(document => document.CreatedUtc)
            .Select(document => MapMedicalClaimDocument(document))
            .ToArrayAsync(cancellationToken);

    public async Task<MedicalClaimDocumentResponse?> GetMedicalClaimDocumentAsync(
        Guid medicalClaimPublicId,
        Guid documentPublicId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<MedicalClaimDocument>()
            .AsNoTracking()
            .Where(document => document.MedicalClaim.PublicId == medicalClaimPublicId && document.PublicId == documentPublicId)
            .Select(document => MapMedicalClaimDocument(document))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<MedicalClaimDocument?> GetMedicalClaimDocumentEntityAsync(
        Guid medicalClaimPublicId,
        Guid documentPublicId,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<MedicalClaimDocument>()
            .SingleOrDefaultAsync(
                document => document.MedicalClaim.PublicId == medicalClaimPublicId
                    && document.PublicId == documentPublicId
                    && document.TenantId == tenantId,
                cancellationToken);

    private static MedicalClaimDocumentResponse MapMedicalClaimDocument(MedicalClaimDocument document) =>
        new(
            document.PublicId,
            document.DocumentTypeCatalogItem != null ? document.DocumentTypeCatalogItem.PublicId : (Guid?)null,
            document.DocumentTypeCatalogItem != null ? document.DocumentTypeCatalogItem.Code : null,
            document.DocumentTypeCatalogItem != null ? document.DocumentTypeCatalogItem.Name : null,
            document.Observations,
            document.FilePublicId,
            document.FileName,
            document.ContentType,
            document.SizeBytes,
            document.IsActive,
            document.ConcurrencyToken,
            document.CreatedUtc,
            document.ModifiedUtc);

    public async Task<IReadOnlyCollection<PersonnelFileOffPayrollTransactionResponse>> AddOffPayrollTransactionAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileOffPayrollTransaction entity,
        CancellationToken cancellationToken)
    {
        dbContext.Set<PersonnelFileOffPayrollTransaction>().Add(entity);
        // Append the in-memory entity: the row is not saved yet, so an AsNoTracking re-query excludes it and
        // the handler's `SingleOrDefault(... == entity.PublicId) ?? throw` would 500 on every create.
        var persisted = await dbContext.Set<PersonnelFileOffPayrollTransaction>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId)
            .ToArrayAsync(cancellationToken);
        return persisted.Append(entity)
            .OrderByDescending(item => item.TransactionDateUtc)
            .Select(Map)
            .ToArray();
    }

    public async Task<PersonnelFileOffPayrollTransactionResponse?> UpdateOffPayrollTransactionAsync(
        Guid offPayrollTransactionPublicId,
        Guid tenantId,
        OffPayrollTransactionInput input,
        string currencyCode,
        string? transactionTypeNameSnapshot,
        string? assetNameSnapshot,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileOffPayrollTransaction>()
            .SingleOrDefaultAsync(x => x.PublicId == offPayrollTransactionPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        ApplyOffPayrollTransactionInput(item, input, currencyCode, transactionTypeNameSnapshot, assetNameSnapshot);
        return Map(item);
    }

    public async Task<PersonnelFileOffPayrollTransactionResponse?> PatchOffPayrollTransactionAsync(
        Guid offPayrollTransactionPublicId,
        Guid tenantId,
        OffPayrollTransactionInput input,
        string currencyCode,
        string? transactionTypeNameSnapshot,
        string? assetNameSnapshot,
        bool isActive,
        bool isActiveMutated,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileOffPayrollTransaction>()
            .SingleOrDefaultAsync(x => x.PublicId == offPayrollTransactionPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        ApplyOffPayrollTransactionInput(item, input, currencyCode, transactionTypeNameSnapshot, assetNameSnapshot);
        if (isActiveMutated)
        {
            item.SetActive(isActive);
        }

        return Map(item);
    }

    private static void ApplyOffPayrollTransactionInput(
        PersonnelFileOffPayrollTransaction item,
        OffPayrollTransactionInput input,
        string currencyCode,
        string? transactionTypeNameSnapshot,
        string? assetNameSnapshot) =>
        item.Update(
            input.TransactionTypeCode,
            transactionTypeNameSnapshot,
            input.TransactionDateUtc,
            currencyCode,
            input.Amount,
            input.Year,
            input.Month,
            input.Comment,
            input.AssetAccessPublicId,
            assetNameSnapshot,
            input.CorrectsTransactionPublicId);

    public async Task<bool> SoftDeleteOffPayrollTransactionAsync(
        Guid offPayrollTransactionPublicId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileOffPayrollTransaction>()
            .SingleOrDefaultAsync(x => x.PublicId == offPayrollTransactionPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return false;
        item.SetActive(false);
        return true;
    }

    public async Task<IReadOnlyCollection<PersonnelFileOffPayrollTransactionResponse>> GetOffPayrollTransactionsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileOffPayrollTransaction>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderByDescending(item => item.TransactionDateUtc)
            .Select(item => Map(item))
            .ToArrayAsync(cancellationToken);

    public async Task<PersonnelFileOffPayrollTransactionResponse?> GetOffPayrollTransactionAsync(
        Guid personnelFileId,
        Guid offPayrollTransactionPublicId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileOffPayrollTransaction>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.PersonnelFile.PublicId == personnelFileId && x.PublicId == offPayrollTransactionPublicId, cancellationToken);
        return item is null ? null : Map(item);
    }

    public async Task<IReadOnlyCollection<OffPayrollTransactionCurrencyTotalResponse>> GetOffPayrollTransactionTotalsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileOffPayrollTransaction>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId && item.IsActive)
            .GroupBy(item => item.CurrencyCode)
            .Select(group => new OffPayrollTransactionCurrencyTotalResponse(group.Key, group.Sum(x => x.Amount), group.Count()))
            .OrderBy(result => result.CurrencyCode)
            .ToArrayAsync(cancellationToken);

    public async Task<long?> GetOffPayrollTransactionInternalIdAsync(
        Guid personnelFileId,
        Guid offPayrollTransactionPublicId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileOffPayrollTransaction>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId && item.PublicId == offPayrollTransactionPublicId)
            .Select(item => (long?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddOffPayrollTransactionDocumentAsync(
        OffPayrollTransactionDocument entity,
        CancellationToken cancellationToken)
    {
        await dbContext.Set<OffPayrollTransactionDocument>().AddAsync(entity, cancellationToken);
    }

    public async Task<IReadOnlyCollection<OffPayrollTransactionDocumentResponse>> GetOffPayrollTransactionDocumentsAsync(
        Guid offPayrollTransactionPublicId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<OffPayrollTransactionDocument>()
            .AsNoTracking()
            .Where(document => document.OffPayrollTransaction.PublicId == offPayrollTransactionPublicId && document.IsActive)
            .OrderByDescending(document => document.CreatedUtc)
            .Select(document => MapOffPayrollTransactionDocument(document))
            .ToArrayAsync(cancellationToken);

    public async Task<OffPayrollTransactionDocumentResponse?> GetOffPayrollTransactionDocumentAsync(
        Guid offPayrollTransactionPublicId,
        Guid documentPublicId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<OffPayrollTransactionDocument>()
            .AsNoTracking()
            .Where(document => document.OffPayrollTransaction.PublicId == offPayrollTransactionPublicId && document.PublicId == documentPublicId)
            .Select(document => MapOffPayrollTransactionDocument(document))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<OffPayrollTransactionDocument?> GetOffPayrollTransactionDocumentEntityAsync(
        Guid offPayrollTransactionPublicId,
        Guid documentPublicId,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<OffPayrollTransactionDocument>()
            .SingleOrDefaultAsync(
                document => document.OffPayrollTransaction.PublicId == offPayrollTransactionPublicId
                    && document.PublicId == documentPublicId
                    && document.TenantId == tenantId,
                cancellationToken);

    private static OffPayrollTransactionDocumentResponse MapOffPayrollTransactionDocument(OffPayrollTransactionDocument document) =>
        new(
            document.PublicId,
            document.DocumentTypeCatalogItem != null ? document.DocumentTypeCatalogItem.PublicId : (Guid?)null,
            document.DocumentTypeCatalogItem != null ? document.DocumentTypeCatalogItem.Code : null,
            document.DocumentTypeCatalogItem != null ? document.DocumentTypeCatalogItem.Name : null,
            document.Observations,
            document.FilePublicId,
            document.FileName,
            document.ContentType,
            document.SizeBytes,
            document.IsActive,
            document.ConcurrencyToken,
            document.CreatedUtc,
            document.ModifiedUtc);

    public async Task<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>> AddPerformanceEvaluationAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFilePerformanceEvaluation entity,
        CancellationToken cancellationToken)
    {
        dbContext.Set<PersonnelFilePerformanceEvaluation>().Add(entity);
        // Append the in-memory entity: the row is not saved yet, so an AsNoTracking re-query excludes it and
        // the handler's `SingleOrDefault(... == entity.PublicId) ?? throw` would 500 on every create.
        var persisted = await dbContext.Set<PersonnelFilePerformanceEvaluation>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId)
            .ToArrayAsync(cancellationToken);
        return persisted.Append(entity)
            .OrderByDescending(item => item.EvaluationDateUtc)
            .Select(Map)
            .ToArray();
    }

    public async Task<PersonnelFilePerformanceEvaluationResponse?> UpdatePerformanceEvaluationAsync(
        Guid itemPublicId,
        Guid tenantId,
        string evaluatorName,
        DateTime evaluationDateUtc,
        decimal? score,
        string? qualitativeScoreCode,
        string? comment,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFilePerformanceEvaluation>()
            .SingleOrDefaultAsync(x => x.PublicId == itemPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Update(evaluatorName, evaluationDateUtc, score, qualitativeScoreCode, comment, sourceSystem, sourceReference, sourceSyncedUtc);
        return Map(item);
    }

    public async Task<bool> DeletePerformanceEvaluationAsync(
        Guid itemPublicId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFilePerformanceEvaluation>()
            .SingleOrDefaultAsync(x => x.PublicId == itemPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return false;
        dbContext.Set<PersonnelFilePerformanceEvaluation>().Remove(item);
        return true;
    }

    public async Task<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>> GetPerformanceEvaluationsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFilePerformanceEvaluation>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderByDescending(item => item.EvaluationDateUtc)
            .Select(item => Map(item))
            .ToArrayAsync(cancellationToken);

    public async Task<PersonnelFilePerformanceEvaluationResponse?> GetPerformanceEvaluationAsync(
        Guid personnelFileId,
        Guid evaluationPublicId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFilePerformanceEvaluation>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.PersonnelFile.PublicId == personnelFileId && x.PublicId == evaluationPublicId, cancellationToken);
        return item is null ? null : Map(item);
    }

    public void AddPositionCompetencyResult(PersonnelFilePositionCompetencyResult entity) =>
        dbContext.Set<PersonnelFilePositionCompetencyResult>().Add(entity);

    public async Task<bool> UpdatePositionCompetencyResultAsync(
        Guid itemPublicId,
        Guid tenantId,
        long competencyCatalogItemId,
        long competencyTypeCatalogItemId,
        long? jobProfileCompetencyExpectationId,
        decimal? expectedScore,
        decimal achievedScore,
        DateTime evaluationDateUtc,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFilePositionCompetencyResult>()
            .SingleOrDefaultAsync(x => x.PublicId == itemPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return false;
        item.Update(
            competencyCatalogItemId,
            competencyTypeCatalogItemId,
            jobProfileCompetencyExpectationId,
            expectedScore,
            achievedScore,
            evaluationDateUtc,
            sourceSystem,
            sourceReference,
            sourceSyncedUtc);
        return true;
    }

    public async Task<bool> DeletePositionCompetencyResultAsync(
        Guid itemPublicId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFilePositionCompetencyResult>()
            .SingleOrDefaultAsync(x => x.PublicId == itemPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return false;
        dbContext.Set<PersonnelFilePositionCompetencyResult>().Remove(item);
        return true;
    }

    public async Task<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>> GetPositionCompetencyResultsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await ProjectPositionCompetencyResults(
                dbContext.Set<PersonnelFilePositionCompetencyResult>()
                    .AsNoTracking()
                    .Where(item => item.PersonnelFile.PublicId == personnelFileId))
            .OrderBy(response => response.CompetencyCode)
            .ThenByDescending(response => response.EvaluationDateUtc)
            .ToArrayAsync(cancellationToken);

    public async Task<PersonnelFilePositionCompetencyResultResponse?> GetPositionCompetencyResultAsync(
        Guid personnelFileId,
        Guid positionCompetencyResultPublicId,
        CancellationToken cancellationToken) =>
        await ProjectPositionCompetencyResults(
                dbContext.Set<PersonnelFilePositionCompetencyResult>()
                    .AsNoTracking()
                    .Where(item => item.PersonnelFile.PublicId == personnelFileId && item.PublicId == positionCompetencyResultPublicId))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<long?> GetActiveAssignedJobProfileInternalIdAsync(
        Guid personnelFilePublicId,
        CancellationToken cancellationToken)
    {
        var slotPublicId = await dbContext.Set<PersonnelFileEmploymentAssignment>()
            .AsNoTracking()
            .Where(assignment => assignment.PersonnelFile.PublicId == personnelFilePublicId
                && assignment.IsActive
                && assignment.IsPrimary
                && assignment.PositionSlotPublicId != null)
            .Select(assignment => assignment.PositionSlotPublicId)
            .FirstOrDefaultAsync(cancellationToken);

        if (slotPublicId is null || slotPublicId == Guid.Empty)
        {
            return null;
        }

        return await dbContext.Set<PositionSlot>()
            .AsNoTracking()
            .Where(slot => slot.PublicId == slotPublicId.Value)
            .Select(slot => (long?)slot.JobProfileId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Guid?> GetActivePrimaryPositionSlotPublicIdAsync(
        Guid personnelFilePublicId,
        CancellationToken cancellationToken)
    {
        var slotPublicId = await dbContext.Set<PersonnelFileEmploymentAssignment>()
            .AsNoTracking()
            .Where(assignment => assignment.PersonnelFile.PublicId == personnelFilePublicId
                && assignment.IsActive
                && assignment.IsPrimary
                && assignment.PositionSlotPublicId != null)
            .Select(assignment => assignment.PositionSlotPublicId)
            .FirstOrDefaultAsync(cancellationToken);

        return slotPublicId is null || slotPublicId == Guid.Empty ? null : slotPublicId;
    }

    public async Task<EmployeePositionCompetenciesResponse> GetEmployeePositionCompetenciesAsync(
        Guid personnelFilePublicId,
        CancellationToken cancellationToken)
    {
        var jobProfileInternalId = await GetActiveAssignedJobProfileInternalIdAsync(personnelFilePublicId, cancellationToken);
        if (jobProfileInternalId is null)
        {
            return new EmployeePositionCompetenciesResponse(personnelFilePublicId, null, null, null, false, []);
        }

        var profile = await dbContext.Set<JobProfile>()
            .AsNoTracking()
            .Where(item => item.Id == jobProfileInternalId.Value)
            .Select(item => new { item.PublicId, item.Code, item.Title })
            .FirstOrDefaultAsync(cancellationToken);

        var expectations = await (
            from expectation in dbContext.Set<JobProfileCompetencyExpectation>().AsNoTracking()
            join competency in dbContext.Set<JobCatalogItem>().AsNoTracking() on expectation.CompetencyCatalogItemId equals competency.Id
            join competencyType in dbContext.Set<JobCatalogItem>().AsNoTracking() on expectation.CompetencyTypeCatalogItemId equals competencyType.Id
            join behaviorLevel in dbContext.Set<JobCatalogItem>().AsNoTracking() on expectation.BehaviorLevelCatalogItemId equals behaviorLevel.Id
            join level in dbContext.Set<OccupationalPyramidLevel>().AsNoTracking() on expectation.OccupationalPyramidLevelId equals level.Id
            where expectation.JobProfileId == jobProfileInternalId.Value
            orderby competencyType.Name, expectation.SortOrder
            select new
            {
                ExpectationInternalId = expectation.Id,
                ExpectationPublicId = expectation.PublicId,
                CompetencyPublicId = competency.PublicId,
                CompetencyCode = competency.Code,
                CompetencyName = competency.Name,
                CompetencyTypePublicId = competencyType.PublicId,
                CompetencyTypeCode = competencyType.Code,
                CompetencyTypeName = competencyType.Name,
                LevelPublicId = level.PublicId,
                LevelCode = level.Code,
                LevelName = level.Name,
                level.LevelOrder,
                BehaviorLevelPublicId = behaviorLevel.PublicId,
                BehaviorLevelCode = behaviorLevel.Code,
                BehaviorLevelName = behaviorLevel.Name,
                expectation.ExpectedEvidence,
                expectation.ExpectedValue
            })
            .ToListAsync(cancellationToken);

        var expectationInternalIds = expectations.Select(item => item.ExpectationInternalId).ToArray();

        var conducts = await (
            from link in dbContext.Set<JobProfileCompetencyExpectationConduct>().AsNoTracking()
            join conduct in dbContext.Set<CompetencyConduct>().AsNoTracking() on link.CompetencyConductId equals conduct.Id
            where expectationInternalIds.Contains(link.JobProfileCompetencyExpectationId)
            orderby link.SortOrder
            select new { link.JobProfileCompetencyExpectationId, conduct.Description })
            .ToListAsync(cancellationToken);
        var conductsByExpectation = conducts
            .GroupBy(item => item.JobProfileCompetencyExpectationId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Description).ToList());

        var results = await dbContext.Set<PersonnelFilePositionCompetencyResult>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFilePublicId && item.JobProfileCompetencyExpectationId != null)
            .Select(item => new
            {
                item.PublicId,
                ExpectationInternalId = item.JobProfileCompetencyExpectationId!.Value,
                item.ExpectedScore,
                item.AchievedScore,
                item.GapScore,
                item.EvaluationDateUtc
            })
            .ToListAsync(cancellationToken);
        var resultsByExpectation = results
            .GroupBy(item => item.ExpectationInternalId)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.EvaluationDateUtc).ToList());

        var groups = expectations
            .GroupBy(item => new { item.CompetencyTypePublicId, item.CompetencyTypeCode, item.CompetencyTypeName })
            .Select(group => new EmployeePositionCompetencyTypeGroupResponse(
                group.Key.CompetencyTypePublicId,
                group.Key.CompetencyTypeCode,
                group.Key.CompetencyTypeName,
                group.Select(expectation =>
                {
                    var history = resultsByExpectation.TryGetValue(expectation.ExpectationInternalId, out var rows)
                        ? rows
                        : [];
                    var current = history.FirstOrDefault();
                    return new EmployeePositionCompetencyResponse(
                        expectation.ExpectationPublicId,
                        expectation.CompetencyPublicId,
                        expectation.CompetencyCode,
                        expectation.CompetencyName,
                        expectation.LevelPublicId,
                        expectation.LevelCode,
                        expectation.LevelName,
                        expectation.LevelOrder,
                        expectation.BehaviorLevelPublicId,
                        expectation.BehaviorLevelCode,
                        expectation.BehaviorLevelName,
                        expectation.ExpectedEvidence,
                        expectation.ExpectedValue,
                        current?.AchievedScore,
                        current?.GapScore,
                        current?.EvaluationDateUtc,
                        conductsByExpectation.TryGetValue(expectation.ExpectationInternalId, out var descriptions)
                            ? descriptions
                            : [],
                        history
                            .Select(row => new EmployeePositionCompetencyHistoryEntryResponse(
                                row.PublicId,
                                row.ExpectedScore,
                                row.AchievedScore,
                                row.GapScore,
                                row.EvaluationDateUtc))
                            .ToArray());
                }).ToArray()))
            .ToArray();

        return new EmployeePositionCompetenciesResponse(
            personnelFilePublicId,
            profile?.PublicId,
            profile?.Code,
            profile?.Title,
            true,
            groups);
    }

    private IQueryable<PersonnelFilePositionCompetencyResultResponse> ProjectPositionCompetencyResults(
        IQueryable<PersonnelFilePositionCompetencyResult> source) =>
        from item in source
        join competency in dbContext.Set<JobCatalogItem>().AsNoTracking()
            on item.CompetencyCatalogItemId equals competency.Id
        join competencyType in dbContext.Set<JobCatalogItem>().AsNoTracking()
            on item.CompetencyTypeCatalogItemId equals competencyType.Id
        join expectation in dbContext.Set<JobProfileCompetencyExpectation>().AsNoTracking()
            on item.JobProfileCompetencyExpectationId equals expectation.Id into expectationJoin
        from expectation in expectationJoin.DefaultIfEmpty()
        select new PersonnelFilePositionCompetencyResultResponse(
            item.PublicId,
            expectation != null ? expectation.PublicId : (Guid?)null,
            competency.PublicId,
            competency.Code,
            competency.Name,
            competencyType.PublicId,
            competencyType.Code,
            competencyType.Name,
            item.ExpectedScore,
            item.AchievedScore,
            item.GapScore,
            item.EvaluationDateUtc,
            item.SourceSystem,
            item.SourceReference,
            item.SourceSyncedUtc,
            item.ConcurrencyToken);

    public async Task<IReadOnlyCollection<PersonnelFileSelectionContestResponse>> AddSelectionContestAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileSelectionContest entity,
        CancellationToken cancellationToken)
    {
        dbContext.Set<PersonnelFileSelectionContest>().Add(entity);
        // Append the in-memory entity: the row is not saved yet, so an AsNoTracking re-query excludes it and
        // the handler's `SingleOrDefault(... == entity.PublicId) ?? throw` would 500 on every create.
        var persisted = await dbContext.Set<PersonnelFileSelectionContest>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId)
            .ToArrayAsync(cancellationToken);
        return persisted.Append(entity)
            .OrderByDescending(item => item.ContestDateUtc)
            .Select(Map)
            .ToArray();
    }

    public async Task<PersonnelFileSelectionContestResponse?> UpdateSelectionContestAsync(
        Guid itemPublicId,
        Guid tenantId,
        string contestCode,
        string contestName,
        DateTime contestDateUtc,
        string resultCode,
        string? notes,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileSelectionContest>()
            .SingleOrDefaultAsync(x => x.PublicId == itemPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Update(contestCode, contestName, contestDateUtc, resultCode, notes, sourceSystem, sourceReference, sourceSyncedUtc);
        return Map(item);
    }

    public async Task<bool> DeleteSelectionContestAsync(
        Guid itemPublicId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileSelectionContest>()
            .SingleOrDefaultAsync(x => x.PublicId == itemPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return false;
        dbContext.Set<PersonnelFileSelectionContest>().Remove(item);
        return true;
    }

    public async Task<IReadOnlyCollection<PersonnelFileSelectionContestResponse>> GetSelectionContestsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileSelectionContest>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderByDescending(item => item.ContestDateUtc)
            .Select(item => Map(item))
            .ToArrayAsync(cancellationToken);

    public async Task<PersonnelFileSelectionContestResponse?> GetSelectionContestAsync(
        Guid personnelFileId,
        Guid selectionContestPublicId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileSelectionContest>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.PersonnelFile.PublicId == personnelFileId && x.PublicId == selectionContestPublicId, cancellationToken);
        return item is null ? null : Map(item);
    }

    public async Task<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>> AddCurricularCompetencyAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileCurricularCompetency entity,
        CancellationToken cancellationToken)
    {
        dbContext.Set<PersonnelFileCurricularCompetency>().Add(entity);
        // Append the in-memory entity: the row is not saved yet, so an AsNoTracking re-query excludes it and
        // the handler's `SingleOrDefault(... == entity.PublicId) ?? throw` would 500 on every create.
        var persisted = await dbContext.Set<PersonnelFileCurricularCompetency>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId)
            .ToArrayAsync(cancellationToken);
        return persisted.Append(entity)
            .OrderBy(item => item.RequirementTypeCode).ThenBy(item => item.RequirementName)
            .Select(Map)
            .ToArray();
    }

    public async Task<PersonnelFileCurricularCompetencyResponse?> UpdateCurricularCompetencyAsync(
        Guid itemPublicId,
        Guid tenantId,
        string requirementTypeCode,
        string requirementName,
        string competencyDomain,
        decimal? experienceTimeValue,
        string? metricCode,
        string? notes,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileCurricularCompetency>()
            .SingleOrDefaultAsync(x => x.PublicId == itemPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Update(requirementTypeCode, requirementName, competencyDomain, experienceTimeValue, metricCode, notes, sourceSystem, sourceReference, sourceSyncedUtc);
        return Map(item);
    }

    public async Task<bool> DeleteCurricularCompetencyAsync(
        Guid itemPublicId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileCurricularCompetency>()
            .SingleOrDefaultAsync(x => x.PublicId == itemPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return false;
        dbContext.Set<PersonnelFileCurricularCompetency>().Remove(item);
        return true;
    }

    public async Task<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>> GetCurricularCompetenciesAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileCurricularCompetency>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderBy(item => item.RequirementTypeCode)
            .ThenBy(item => item.RequirementName)
            .Select(item => Map(item))
            .ToArrayAsync(cancellationToken);

    public async Task<PersonnelFileCurricularCompetencyResponse?> GetCurricularCompetencyAsync(
        Guid personnelFileId,
        Guid curricularCompetencyPublicId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileCurricularCompetency>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.PersonnelFile.PublicId == personnelFileId && x.PublicId == curricularCompetencyPublicId, cancellationToken);
        return item is null ? null : Map(item);
    }

    private async Task<IReadOnlyCollection<TResponse>> ReplaceSectionAsync<TEntity, TResponse>(
        long personnelFileInternalId,
        Guid tenantId,
        IReadOnlyCollection<TEntity> entities,
        Func<IReadOnlyCollection<TEntity>, IOrderedEnumerable<TEntity>> orderBy,
        Func<TEntity, TResponse> map,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        var existing = await dbContext.Set<TEntity>()
            .Where(item =>
                EF.Property<Guid>(item, "TenantId") == tenantId &&
                EF.Property<long>(item, "PersonnelFileId") == personnelFileInternalId)
            .ToListAsync(cancellationToken);

        dbContext.Set<TEntity>().RemoveRange(existing);
        dbContext.Set<TEntity>().AddRange(entities);

        return orderBy(entities).Select(map).ToArray();
    }

    private IQueryable<PersonnelFilePersonnelAction> BuildPersonnelActionsBaseQuery(Guid personnelFileId) =>
        dbContext.Set<PersonnelFilePersonnelAction>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId);

    private static IQueryable<PersonnelFilePersonnelAction> ApplyPersonnelActionFilters(
        IQueryable<PersonnelFilePersonnelAction> query,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? type,
        string? status,
        string? search)
    {
        if (fromUtc.HasValue)
        {
            query = query.Where(item => item.ActionDateUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(item => item.ActionDateUtc <= toUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            var normalizedType = type.Trim();
            query = query.Where(item => item.ActionTypeCode == normalizedType);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim();
            query = query.Where(item => item.ActionStatusCode == normalizedStatus);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            query = query.Where(item =>
                (item.Description ?? string.Empty).Contains(normalizedSearch) ||
                (item.Reference ?? string.Empty).Contains(normalizedSearch) ||
                item.ActionTypeCode.Contains(normalizedSearch) ||
                item.ActionStatusCode.Contains(normalizedSearch));
        }

        return query;
    }

    private static IOrderedQueryable<PersonnelFilePersonnelAction> ApplyPersonnelActionSorting(
        IQueryable<PersonnelFilePersonnelAction> query,
        string? sortBy,
        PersonnelFileSortDirection direction)
    {
        var descending = direction == PersonnelFileSortDirection.Desc;
        var field = (sortBy ?? "actionDateUtc").Trim().ToLowerInvariant();

        return field switch
        {
            "createdatutc" => descending ? query.OrderByDescending(item => item.CreatedUtc) : query.OrderBy(item => item.CreatedUtc),
            "type" or "actiontypecode" => descending ? query.OrderByDescending(item => item.ActionTypeCode) : query.OrderBy(item => item.ActionTypeCode),
            "status" or "actionstatuscode" => descending ? query.OrderByDescending(item => item.ActionStatusCode) : query.OrderBy(item => item.ActionStatusCode),
            "amount" => descending ? query.OrderByDescending(item => item.Amount) : query.OrderBy(item => item.Amount),
            _ => descending ? query.OrderByDescending(item => item.ActionDateUtc) : query.OrderBy(item => item.ActionDateUtc)
        };
    }

    private IQueryable<PersonnelFilePayrollTransaction> BuildPayrollTransactionsBaseQuery(Guid personnelFileId) =>
        dbContext.Set<PersonnelFilePayrollTransaction>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId);

    private static IQueryable<PersonnelFilePayrollTransaction> ApplyPayrollTransactionFilters(
        IQueryable<PersonnelFilePayrollTransaction> query,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? type,
        string? status,
        string? search)
    {
        if (fromUtc.HasValue)
        {
            query = query.Where(item => item.TransactionDateUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue)
        {
            query = query.Where(item => item.TransactionDateUtc <= toUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            var normalizedType = type.Trim();
            query = query.Where(item => item.TransactionTypeCode == normalizedType);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim().ToUpperInvariant();
            if (normalizedStatus is "DEBIT" or "DISCOUNT")
            {
                query = query.Where(item => item.IsDebit);
            }
            else if (normalizedStatus is "CREDIT" or "EARNING")
            {
                query = query.Where(item => !item.IsDebit);
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = search.Trim();
            query = query.Where(item =>
                (item.Description ?? string.Empty).Contains(normalizedSearch) ||
                item.PayrollPeriodCode.Contains(normalizedSearch) ||
                item.TransactionTypeCode.Contains(normalizedSearch));
        }

        return query;
    }

    private static IOrderedQueryable<PersonnelFilePayrollTransaction> ApplyPayrollTransactionSorting(
        IQueryable<PersonnelFilePayrollTransaction> query,
        string? sortBy,
        PersonnelFileSortDirection direction)
    {
        var descending = direction == PersonnelFileSortDirection.Desc;
        var field = (sortBy ?? "transactionDateUtc").Trim().ToLowerInvariant();

        return field switch
        {
            "createdatutc" => descending ? query.OrderByDescending(item => item.CreatedUtc) : query.OrderBy(item => item.CreatedUtc),
            "type" or "transactiontypecode" => descending ? query.OrderByDescending(item => item.TransactionTypeCode) : query.OrderBy(item => item.TransactionTypeCode),
            "amount" => descending ? query.OrderByDescending(item => item.Amount) : query.OrderBy(item => item.Amount),
            _ => descending ? query.OrderByDescending(item => item.TransactionDateUtc) : query.OrderBy(item => item.TransactionDateUtc)
        };
    }

    // InstitutionalEmail and Seniority are derived/enriched in the application layer
    // (EmployeeProfileResponseEnricher); the balances are owned by the future vacations/incapacities module.
    private static PersonnelFileEmployeeProfileResponse Map(PersonnelFileEmployeeProfile item) =>
        new(
            item.PublicId,
            item.EmployeeCode,
            item.EmploymentStatusCode,
            InstitutionalEmail: null,
            item.HireDate,
            item.MinimumMonthlyWage,
            EmployeeSeniority.None,
            item.RetirementCategoryCode,
            item.RetirementReasonCode,
            item.RetirementNotes,
            item.RetirementDate,
            VacationDaysAvailable: null,
            DisabilityDaysAvailable: null,
            CompensatoryTimeHoursAvailable: null,
            item.ConcurrencyToken,
            item.CreatedUtc,
            item.ModifiedUtc);

    private static PersonnelFileEmploymentAssignmentResponse Map(
        PersonnelFileEmploymentAssignment item,
        string? positionSlotCode = null,
        string? positionSlotTitle = null) =>
        new(
            item.PublicId,
            item.AssignmentTypeCode,
            item.ContractTypeCode,
            item.WorkdayCode,
            item.PayrollTypeCode,
            item.PositionSlotPublicId,
            item.OrgUnitPublicId,
            item.WorkCenterPublicId,
            item.CostCenterPublicId,
            item.StartDate,
            item.EndDate,
            item.IsPrimary,
            item.IsActive,
            item.Notes,
            item.ConcurrencyToken,
            item.PaymentMethodCode,
            item.PaymentBankAccountPublicId,
            positionSlotCode,
            positionSlotTitle,
            (int?)item.RestDayOfWeek);

    // Resolves each assignment's position-slot code/title in one extra batched query (keyed by the slot's
    // public id) so the list/add paths stay O(1) queries instead of N+1. The slot reference is a loose Guid,
    // not an FK, so a missing slot simply yields a null label.
    private async Task<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>> MapWithSlotLabelsAsync(
        IReadOnlyCollection<PersonnelFileEmploymentAssignment> items,
        CancellationToken cancellationToken)
    {
        var slotIds = items
            .Where(item => item.PositionSlotPublicId.HasValue)
            .Select(item => item.PositionSlotPublicId!.Value)
            .Distinct()
            .ToArray();

        var labels = new Dictionary<Guid, (string Code, string? Title)>();
        if (slotIds.Length > 0)
        {
            var rows = await dbContext.Set<PositionSlot>()
                .AsNoTracking()
                .Where(slot => slotIds.Contains(slot.PublicId))
                .Select(slot => new { slot.PublicId, slot.Code, slot.Title })
                .ToArrayAsync(cancellationToken);
            foreach (var row in rows)
            {
                labels[row.PublicId] = (row.Code, row.Title);
            }
        }

        return items
            .Select(item => item.PositionSlotPublicId is { } slotId && labels.TryGetValue(slotId, out var label)
                ? Map(item, label.Code, label.Title)
                : Map(item))
            .ToArray();
    }

    // Single-item counterpart of <see cref="MapWithSlotLabelsAsync"/> for the by-id/update/patch paths.
    private async Task<PersonnelFileEmploymentAssignmentResponse> MapWithSlotLabelAsync(
        PersonnelFileEmploymentAssignment item,
        CancellationToken cancellationToken)
    {
        if (item.PositionSlotPublicId is not { } slotId)
        {
            return Map(item);
        }

        var label = await dbContext.Set<PositionSlot>()
            .AsNoTracking()
            .Where(slot => slot.PublicId == slotId)
            .Select(slot => new { slot.Code, slot.Title })
            .FirstOrDefaultAsync(cancellationToken);

        return label is null ? Map(item) : Map(item, label.Code, label.Title);
    }

    private static PersonnelFileContractHistoryResponse Map(PersonnelFileContractHistory item) =>
        new(
            item.PublicId,
            item.ContractTypeCode,
            item.ContractDate,
            item.ContractEndDate,
            item.PositionSlotPublicId,
            item.IsActive,
            item.Notes,
            item.ConcurrencyToken);

    private static PersonnelFileCompensationConceptResponse Map(PersonnelFileCompensationConcept item) =>
        new(
            item.PublicId,
            item.AssignedPositionPublicId,
            item.Nature,
            item.ConceptTypeCode,
            item.DeductionClass,
            item.CalculationType,
            item.Value,
            item.CalculationBaseCode,
            item.EmployerRate,
            item.ContributionCap,
            item.CurrencyCode,
            item.PayPeriodCode,
            item.CounterpartyName,
            item.ExternalReference,
            item.StartDate,
            item.EndDate,
            item.IsActive,
            item.IsSystemSuggested,
            item.Notes,
            item.ConcurrencyToken);

    private static PersonnelFileAdditionalBenefitResponse Map(PersonnelFileAdditionalBenefit item) =>
        new(
            item.PublicId,
            item.BenefitTypeCode,
            item.StartDate,
            item.EndDate,
            item.IsActive,
            item.Notes,
            item.ConcurrencyToken);

    private static PersonnelFileAuthorizationSubstitutionResponse Map(PersonnelFileAuthorizationSubstitution item) =>
        new(
            item.PublicId,
            item.SubstitutionTypeCode,
            item.SubstitutePersonnelFilePublicId,
            item.SubstitutePositionSlotPublicId,
            item.SubstitutePositionTitleSnapshot,
            item.StartDate,
            item.EndDate,
            item.IsActive,
            item.Notes,
            item.ConcurrencyToken);

    private static PersonnelFilePersonnelActionResponse Map(PersonnelFilePersonnelAction item) =>
        new(
            item.PublicId,
            item.ActionTypeCode,
            item.ActionStatusCode,
            item.ActionDateUtc,
            item.EffectiveFromUtc,
            item.EffectiveToUtc,
            item.Description,
            item.Reference,
            item.Amount,
            item.CurrencyCode,
            item.IsSystemGenerated,
            item.CreatedUtc,
            item.ModifiedUtc,
            item.ConcurrencyToken);

    private static PersonnelFilePayrollTransactionResponse Map(PersonnelFilePayrollTransaction item) =>
        new(
            item.PublicId,
            item.TransactionTypeCode,
            item.TransactionDateUtc,
            item.PayrollPeriodCode,
            item.Description,
            item.Amount,
            item.CurrencyCode,
            item.IsDebit,
            item.SourceSystem,
            item.SourceReference,
            item.SourceSyncedUtc,
            item.CreatedUtc,
            item.ModifiedUtc,
            item.IsActive,
            item.ConcurrencyToken);

    private static PersonnelFileAssetAccessResponse Map(PersonnelFileAssetAccess item) =>
        new(
            item.PublicId,
            item.AssetTypeCode,
            item.AssetOrAccessName,
            item.AccessLevelCode,
            item.StartDateUtc,
            item.EndDateUtc,
            item.DeliveryDateUtc,
            item.DeliveryStatusCode,
            item.IsActive,
            item.Notes,
            item.ConcurrencyToken);

    private static PersonnelFileInsuranceResponse Map(PersonnelFileInsurance item) =>
        new(
            item.PublicId,
            item.InsuranceCode,
            item.EmployeeContribution,
            item.EmployerContribution,
            item.RangeCode,
            item.PolicyNumber,
            item.InsuredAmount,
            item.CurrencyCode,
            item.IsActive,
            item.StartDateUtc,
            item.EndDateUtc,
            item.Beneficiaries
                .OrderByDescending(beneficiary => beneficiary.IsActive)
                .ThenBy(beneficiary => beneficiary.FullName)
                .Select(beneficiary => new PersonnelFileInsuranceBeneficiaryResponse(
                    beneficiary.PublicId,
                    beneficiary.FullName,
                    beneficiary.DocumentNumber,
                    beneficiary.DocumentTypeCode,
                    beneficiary.BirthDate,
                    beneficiary.KinshipCode,
                    beneficiary.AllocationPercentage,
                    beneficiary.BeneficiaryType,
                    beneficiary.IsActive,
                    beneficiary.ConcurrencyToken))
                .ToArray(),
            item.ConcurrencyToken);

    private static PersonnelFileInsuranceBeneficiaryResponse Map(PersonnelFileInsuranceBeneficiary item) =>
        new(
            item.PublicId,
            item.FullName,
            item.DocumentNumber,
            item.DocumentTypeCode,
            item.BirthDate,
            item.KinshipCode,
            item.AllocationPercentage,
            item.BeneficiaryType,
            item.IsActive,
            item.ConcurrencyToken);

    private static PersonnelFileMedicalClaimResponse Map(PersonnelFileMedicalClaim item) =>
        new(
            item.PublicId,
            item.InsurancePublicId,
            item.InsuranceNameSnapshot,
            item.AccountNumber,
            item.ClaimantType,
            item.BeneficiaryPublicId,
            item.PatientNameSnapshot,
            item.KinshipCodeSnapshot,
            item.ClaimTypeCode,
            item.Diagnosis,
            item.ClaimAmount,
            item.CurrencyCode,
            item.PaidAmount,
            item.ResponseTimeDays,
            item.Notes,
            item.ClaimDateUtc,
            item.ResolutionDateUtc,
            item.ClaimStatusCode,
            item.SourceSystem,
            item.SourceReference,
            item.SourceSyncedUtc,
            item.IsActive,
            item.ConcurrencyToken);

    private static PersonnelFileOffPayrollTransactionResponse Map(PersonnelFileOffPayrollTransaction item) =>
        new(
            item.PublicId,
            item.OffPayrollTransactionTypeCode,
            item.TransactionTypeNameSnapshot,
            item.TransactionDateUtc,
            item.CurrencyCode,
            item.Amount,
            item.Year,
            item.Month,
            item.Comment,
            item.AssetAccessPublicId,
            item.AssetNameSnapshot,
            item.CorrectsTransactionPublicId,
            item.IsActive,
            item.ConcurrencyToken);

    public async Task<IReadOnlyCollection<PersonnelFileEconomicAidRequestResponse>> AddEconomicAidRequestAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileEconomicAidRequest entity,
        CancellationToken cancellationToken)
    {
        dbContext.Set<PersonnelFileEconomicAidRequest>().Add(entity);
        // Append the in-memory entity: the row is not saved yet, so an AsNoTracking re-query excludes it.
        var persisted = await dbContext.Set<PersonnelFileEconomicAidRequest>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId)
            .ToArrayAsync(cancellationToken);
        return persisted.Append(entity)
            .OrderByDescending(item => item.RequestDateUtc)
            .Select(Map)
            .ToArray();
    }

    public async Task<PersonnelFileEconomicAidRequestResponse?> UpdateEconomicAidRequestAsync(
        Guid economicAidRequestPublicId,
        Guid tenantId,
        EconomicAidRequestInput input,
        string currencyCode,
        string? typeNameSnapshot,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileEconomicAidRequest>()
            .SingleOrDefaultAsync(x => x.PublicId == economicAidRequestPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Update(input.TypeCode, typeNameSnapshot, input.Description, input.RequestedAmount, currencyCode);
        return Map(item);
    }

    public async Task<bool> SoftDeleteEconomicAidRequestAsync(
        Guid economicAidRequestPublicId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileEconomicAidRequest>()
            .SingleOrDefaultAsync(x => x.PublicId == economicAidRequestPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return false;
        item.SetActive(false);
        return true;
    }

    public async Task<IReadOnlyCollection<PersonnelFileEconomicAidRequestResponse>> GetEconomicAidRequestsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileEconomicAidRequest>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderByDescending(item => item.RequestDateUtc)
            .Select(item => Map(item))
            .ToArrayAsync(cancellationToken);

    public async Task<PersonnelFileEconomicAidRequestResponse?> GetEconomicAidRequestAsync(
        Guid personnelFileId,
        Guid economicAidRequestPublicId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileEconomicAidRequest>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.PersonnelFile.PublicId == personnelFileId && x.PublicId == economicAidRequestPublicId, cancellationToken);
        return item is null ? null : Map(item);
    }

    public async Task<PersonnelFileEconomicAidRequestResponse?> ResolveEconomicAidRequestAsync(
        Guid economicAidRequestPublicId,
        Guid tenantId,
        string targetStatusCode,
        decimal? approvedAmount,
        Guid decidedByUserId,
        DateTime decidedAtUtc,
        string? notes,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileEconomicAidRequest>()
            .SingleOrDefaultAsync(x => x.PublicId == economicAidRequestPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Resolve(targetStatusCode, approvedAmount, decidedByUserId, decidedAtUtc, notes);
        return Map(item);
    }

    public async Task<PersonnelFileEconomicAidRequestResponse?> DisburseEconomicAidRequestAsync(
        Guid economicAidRequestPublicId,
        Guid tenantId,
        decimal disbursedAmount,
        DateTime disbursementDateUtc,
        string? paymentMethodCode,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileEconomicAidRequest>()
            .SingleOrDefaultAsync(x => x.PublicId == economicAidRequestPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Disburse(disbursedAmount, disbursementDateUtc, paymentMethodCode);
        return Map(item);
    }

    public async Task<PersonnelFileEconomicAidRequestResponse?> CancelEconomicAidRequestAsync(
        Guid economicAidRequestPublicId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileEconomicAidRequest>()
            .SingleOrDefaultAsync(x => x.PublicId == economicAidRequestPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Cancel();
        return Map(item);
    }

    private static PersonnelFileEconomicAidRequestResponse Map(PersonnelFileEconomicAidRequest item) =>
        new(
            item.PublicId,
            item.EconomicAidTypeCode,
            item.TypeNameSnapshot,
            item.RequestStatusCode,
            item.Description,
            item.RequestedAmount,
            item.CurrencyCode,
            item.RequestDateUtc,
            item.RequestedByUserId,
            item.ApprovedAmount,
            item.ResolvedByUserId,
            item.ResolutionDateUtc,
            item.ResolutionNotes,
            item.ResponseTimeDays,
            item.DisbursedAmount,
            item.DisbursementDateUtc,
            item.PaymentMethodCode,
            item.IsActive,
            item.ConcurrencyToken);

    public async Task<long?> GetEconomicAidRequestInternalIdAsync(
        Guid personnelFileId,
        Guid economicAidRequestPublicId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileEconomicAidRequest>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId && item.PublicId == economicAidRequestPublicId)
            .Select(item => (long?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);

    // ── Retirement requests ("retiro definitivo") — D-01…D-19 ───────────────────────────────────────────
    public async Task<IReadOnlyCollection<PersonnelFileRetirementRequestResponse>> AddRetirementRequestAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileRetirementRequest entity,
        CancellationToken cancellationToken)
    {
        dbContext.Set<PersonnelFileRetirementRequest>().Add(entity);
        // Append the in-memory entity: the row is not saved yet, so an AsNoTracking re-query excludes it.
        var persisted = await dbContext.Set<PersonnelFileRetirementRequest>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId)
            .ToArrayAsync(cancellationToken);
        return persisted.Append(entity)
            .OrderByDescending(item => item.RequestDate)
            .Select(RetirementRequestMapping.ToResponse)
            .ToArray();
    }

    public async Task<PersonnelFileRetirementRequestResponse?> UpdateRetirementRequestAsync(
        Guid retirementRequestPublicId,
        Guid tenantId,
        RetirementRequestInput input,
        string requesterNameSnapshot,
        string? categoryNameSnapshot,
        string? reasonNameSnapshot,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileRetirementRequest>()
            .SingleOrDefaultAsync(x => x.PublicId == retirementRequestPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Update(
            input.RequesterFilePublicId,
            requesterNameSnapshot,
            input.RequestDate,
            input.RetirementDate,
            input.RetirementCategoryCode,
            categoryNameSnapshot,
            input.RetirementReasonCode,
            reasonNameSnapshot,
            input.Notes);
        return RetirementRequestMapping.ToResponse(item);
    }

    public async Task<IReadOnlyCollection<PersonnelFileRetirementRequestResponse>> GetRetirementRequestsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken)
    {
        var items = await dbContext.Set<PersonnelFileRetirementRequest>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderByDescending(item => item.RequestDate)
            .ThenByDescending(item => item.Id)
            .ToArrayAsync(cancellationToken);
        return items.Select(RetirementRequestMapping.ToResponse).ToArray();
    }

    public async Task<PersonnelFileRetirementRequestResponse?> GetRetirementRequestAsync(
        Guid personnelFileId,
        Guid retirementRequestPublicId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileRetirementRequest>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.PersonnelFile.PublicId == personnelFileId && x.PublicId == retirementRequestPublicId, cancellationToken);
        return item is null ? null : RetirementRequestMapping.ToResponse(item);
    }

    public async Task<PersonnelFileRetirementRequest?> GetRetirementRequestEntityAsync(
        Guid personnelFileId,
        Guid retirementRequestPublicId,
        Guid tenantId,
        bool includeClosedRecords,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<PersonnelFileRetirementRequest>().AsQueryable();
        if (includeClosedRecords)
        {
            query = query.Include(item => item.ClosedRecords);
        }

        return await query.SingleOrDefaultAsync(
            item => item.TenantId == tenantId
                && item.PublicId == retirementRequestPublicId
                && item.PersonnelFile.PublicId == personnelFileId,
            cancellationToken);
    }

    public Task<bool> HasOpenRetirementRequestAsync(
        long personnelFileInternalId,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        dbContext.Set<PersonnelFileRetirementRequest>()
            .AsNoTracking()
            .AnyAsync(
                item => item.TenantId == tenantId
                    && item.PersonnelFileId == personnelFileInternalId
                    && item.IsActive
                    && RetirementRequestStatuses.Open.Contains(item.RequestStatusCode),
                cancellationToken);

    public async Task<RetirementRequesterLookup?> GetRetirementRequesterLookupAsync(
        Guid requesterFilePublicId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var requester = await dbContext.Set<PersonnelFile>()
            .AsNoTracking()
            .Where(file => file.TenantId == tenantId && file.PublicId == requesterFilePublicId)
            .Select(file => new { file.PublicId, file.FirstName, file.LastName, file.IsActive, file.LinkedUserPublicId })
            .SingleOrDefaultAsync(cancellationToken);

        return requester is null
            ? null
            : new RetirementRequesterLookup(
                requester.PublicId,
                $"{requester.FirstName} {requester.LastName}".Trim(),
                requester.IsActive,
                requester.LinkedUserPublicId);
    }

    public Task<PersonnelFileEmployeeProfile?> GetEmployeeProfileEntityAsync(
        long personnelFileInternalId,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        dbContext.Set<PersonnelFileEmployeeProfile>()
            .SingleOrDefaultAsync(
                profile => profile.TenantId == tenantId && profile.PersonnelFileId == personnelFileInternalId,
                cancellationToken);

    public async Task<IReadOnlyCollection<DateTime>> GetActiveRowStartDatesAsync(
        long personnelFileInternalId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var assignmentStarts = await dbContext.Set<PersonnelFileEmploymentAssignment>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId && item.IsActive)
            .Select(item => item.StartDate)
            .ToArrayAsync(cancellationToken);

        var contractStarts = await dbContext.Set<PersonnelFileContractHistory>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId && item.IsActive)
            .Select(item => item.ContractDate)
            .ToArrayAsync(cancellationToken);

        return assignmentStarts.Concat(contractStarts).ToArray();
    }

    public async Task<IReadOnlyCollection<RetirementClosedRowCapture>> CloseActiveEmploymentAssignmentsCapturingAsync(
        long personnelFileInternalId,
        Guid tenantId,
        DateTime endDateUtc,
        CancellationToken cancellationToken)
    {
        var assignments = await dbContext.Set<PersonnelFileEmploymentAssignment>()
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId && item.IsActive)
            .ToArrayAsync(cancellationToken);

        var captures = new List<RetirementClosedRowCapture>(assignments.Length);
        foreach (var assignment in assignments)
        {
            // Capture the pre-execution end date BEFORE mutating (null ⇒ the execution set it — D-11).
            captures.Add(new RetirementClosedRowCapture(assignment.PublicId, assignment.EndDate));
            if (assignment.EndDate is null)
            {
                assignment.Close(endDateUtc);
            }
            else
            {
                // Preserve an already-fixed end date (same semantics as CloseActiveEmploymentAssignmentsAsync).
                assignment.SetActive(false);
            }
        }

        return captures;
    }

    public async Task<IReadOnlyCollection<RetirementClosedRowCapture>> CloseActiveContractHistoriesCapturingAsync(
        long personnelFileInternalId,
        Guid tenantId,
        DateTime endDateUtc,
        CancellationToken cancellationToken)
    {
        var contracts = await dbContext.Set<PersonnelFileContractHistory>()
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId && item.IsActive)
            .ToArrayAsync(cancellationToken);

        var captures = new List<RetirementClosedRowCapture>(contracts.Length);
        foreach (var contract in contracts)
        {
            captures.Add(new RetirementClosedRowCapture(contract.PublicId, contract.ContractEndDate));
            if (contract.ContractEndDate is null)
            {
                contract.Close(endDateUtc);
            }
            else
            {
                contract.SetActive(false);
            }
        }

        return captures;
    }

    private IQueryable<PersonnelFileRetirementRequest> FilteredRetirementRequests(
        Guid companyId,
        string? statusCode,
        string? categoryCode,
        string? reasonCode,
        Guid? employeeId,
        DateTime? requestFromUtc,
        DateTime? requestToUtc,
        DateTime? retirementFromUtc,
        DateTime? retirementToUtc,
        string? search)
    {
        var query = dbContext.Set<PersonnelFileRetirementRequest>()
            .AsNoTracking()
            .Where(item => item.TenantId == companyId && item.IsActive);

        if (!string.IsNullOrWhiteSpace(statusCode))
        {
            var normalized = statusCode.Trim().ToUpperInvariant();
            query = query.Where(item => item.RequestStatusCode == normalized);
        }

        if (!string.IsNullOrWhiteSpace(categoryCode))
        {
            var normalized = categoryCode.Trim().ToUpperInvariant();
            query = query.Where(item => item.RetirementCategoryCode == normalized);
        }

        if (!string.IsNullOrWhiteSpace(reasonCode))
        {
            var normalized = reasonCode.Trim().ToUpperInvariant();
            query = query.Where(item => item.RetirementReasonCode == normalized);
        }

        if (employeeId is { } employeePublicId)
        {
            query = query.Where(item => item.PersonnelFile.PublicId == employeePublicId);
        }

        if (requestFromUtc is { } requestFrom)
        {
            query = query.Where(item => item.RequestDate >= requestFrom.Date);
        }

        if (requestToUtc is { } requestTo)
        {
            query = query.Where(item => item.RequestDate <= requestTo.Date);
        }

        if (retirementFromUtc is { } retirementFrom)
        {
            query = query.Where(item => item.RetirementDate >= retirementFrom.Date);
        }

        if (retirementToUtc is { } retirementTo)
        {
            query = query.Where(item => item.RetirementDate <= retirementTo.Date);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim().ToUpperInvariant();
            query = query.Where(item =>
                (item.RequesterNameSnapshot != null && item.RequesterNameSnapshot.ToUpper().Contains(normalized)) ||
                (item.PersonnelFile.FirstName + " " + item.PersonnelFile.LastName).ToUpper().Contains(normalized));
        }

        return query;
    }

    public async Task<RetirementRequestBandejaResponse> QueryRetirementRequestsAsync(
        QueryRetirementRequestsQuery query,
        CancellationToken cancellationToken)
    {
        var filtered = FilteredRetirementRequests(
            query.CompanyId, query.StatusCode, query.CategoryCode, query.ReasonCode, query.EmployeeId,
            query.RequestFromUtc, query.RequestToUtc, query.RetirementFromUtc, query.RetirementToUtc, query.Search);

        var totalCount = await filtered.CountAsync(cancellationToken);

        var statusCounts = await filtered
            .GroupBy(item => item.RequestStatusCode)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToArrayAsync(cancellationToken);

        var items = await filtered
            .OrderByDescending(item => item.RequestDate)
            .ThenByDescending(item => item.Id)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(item => new RetirementRequestListItemResponse(
                item.PublicId,
                item.PersonnelFile.PublicId,
                item.PersonnelFile.FirstName + " " + item.PersonnelFile.LastName,
                item.RequesterNameSnapshot,
                item.RequestDate,
                item.RetirementDate,
                item.RetirementCategoryCode,
                item.RetirementCategoryNameSnapshot,
                item.RetirementReasonCode,
                item.RetirementReasonNameSnapshot,
                item.RequestStatusCode,
                item.ResolutionDateUtc,
                item.ExecutionDateUtc,
                item.ReversalDateUtc))
            .ToArrayAsync(cancellationToken);

        return new RetirementRequestBandejaResponse(
            items,
            query.PageNumber,
            query.PageSize,
            totalCount,
            statusCounts.ToDictionary(entry => entry.Status, entry => entry.Count));
    }

    public async Task<IReadOnlyCollection<RetirementRequestExportRow>> GetRetirementRequestExportRowsAsync(
        ExportRetirementRequestsQuery query,
        CancellationToken cancellationToken)
    {
        var take = query.MaxRows ?? 100_000;
        return await FilteredRetirementRequests(
                query.CompanyId, query.StatusCode, query.CategoryCode, query.ReasonCode, query.EmployeeId,
                query.RequestFromUtc, query.RequestToUtc, query.RetirementFromUtc, query.RetirementToUtc, query.Search)
            .OrderByDescending(item => item.RequestDate)
            .ThenByDescending(item => item.Id)
            .Take(take)
            .Select(item => new RetirementRequestExportRow(
                item.PersonnelFile.FirstName + " " + item.PersonnelFile.LastName,
                item.RequesterNameSnapshot,
                item.RequestDate,
                item.RetirementDate,
                item.RetirementCategoryNameSnapshot ?? item.RetirementCategoryCode,
                item.RetirementReasonNameSnapshot ?? item.RetirementReasonCode,
                item.RequestStatusCode,
                item.ResolutionDateUtc,
                item.ExecutionDateUtc,
                item.ReversalDateUtc,
                item.Notes))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<RetirementInterviewTrayItemResponse>> GetRetirementInterviewTrayAsync(
        GetRetirementInterviewTrayQuery query,
        CancellationToken cancellationToken)
    {
        var baseQuery = dbContext.Set<PersonnelFileRetirementRequest>()
            .AsNoTracking()
            .Where(item => item.TenantId == query.CompanyId
                && item.IsActive
                && (item.RequestStatusCode == RetirementRequestStatuses.Autorizada
                    || item.RequestStatusCode == RetirementRequestStatuses.Ejecutada));

        if (!string.IsNullOrWhiteSpace(query.CategoryCode))
        {
            var normalized = query.CategoryCode.Trim().ToUpperInvariant();
            baseQuery = baseQuery.Where(item => item.RetirementCategoryCode == normalized);
        }

        if (!string.IsNullOrWhiteSpace(query.ReasonCode))
        {
            var normalized = query.ReasonCode.Trim().ToUpperInvariant();
            baseQuery = baseQuery.Where(item => item.RetirementReasonCode == normalized);
        }

        if (query.RetirementFromUtc is { } retirementFrom)
        {
            baseQuery = baseQuery.Where(item => item.RetirementDate >= retirementFrom.Date);
        }

        if (query.RetirementToUtc is { } retirementTo)
        {
            baseQuery = baseQuery.Where(item => item.RetirementDate <= retirementTo.Date);
        }

        // One projected query (no N+1): the active-form existence and the latest non-archived submission are
        // correlated subqueries; the derived InterviewStatus is composed in memory.
        var rows = await baseQuery
            .OrderBy(item => item.RetirementDate)
            .ThenBy(item => item.Id)
            .Select(item => new
            {
                item.PublicId,
                FilePublicId = item.PersonnelFile.PublicId,
                EmployeeFullName = item.PersonnelFile.FirstName + " " + item.PersonnelFile.LastName,
                item.RetirementCategoryCode,
                item.RetirementCategoryNameSnapshot,
                item.RetirementReasonCode,
                item.RetirementReasonNameSnapshot,
                item.RetirementDate,
                item.RequestStatusCode,
                HasActiveForm = dbContext.ExitInterviewForms.Any(form =>
                    form.TenantId == query.CompanyId
                    && form.Status == ExitInterviewFormStatus.Published
                    && form.IsActiveForReason
                    && form.RetirementReasonCode == item.RetirementReasonCode),
                Submission = dbContext.ExitInterviewSubmissions
                    .Where(submission => submission.TenantId == query.CompanyId
                        && submission.PersonnelFileId == item.PersonnelFileId
                        && submission.Status != ExitInterviewSubmissionStatus.Archived)
                    .OrderByDescending(submission => submission.Id)
                    .Select(submission => new { submission.PublicId, submission.Status })
                    .FirstOrDefault()
            })
            .ToArrayAsync(cancellationToken);

        var items = rows
            .Select(row => new RetirementInterviewTrayItemResponse(
                row.PublicId,
                row.FilePublicId,
                row.EmployeeFullName,
                row.RetirementCategoryCode,
                row.RetirementCategoryNameSnapshot,
                row.RetirementReasonCode,
                row.RetirementReasonNameSnapshot,
                row.RetirementDate,
                row.RequestStatusCode,
                !row.HasActiveForm
                    ? RetirementInterviewStatuses.SinFormulario
                    : row.Submission is null
                        ? RetirementInterviewStatuses.Pendiente
                        : row.Submission.Status == ExitInterviewSubmissionStatus.Submitted
                            ? RetirementInterviewStatuses.Enviada
                            : RetirementInterviewStatuses.Borrador,
                row.Submission?.PublicId));

        if (!string.IsNullOrWhiteSpace(query.InterviewStatus))
        {
            var normalized = query.InterviewStatus.Trim().ToUpperInvariant();
            items = items.Where(item => item.InterviewStatus == normalized);
        }

        return items.ToArray();
    }

    public Task<bool> HasPersonnelActionSinceAsync(
        long personnelFileInternalId,
        Guid tenantId,
        string actionTypeCode,
        DateTime sinceUtc,
        CancellationToken cancellationToken)
    {
        var normalizedType = actionTypeCode.Trim().ToUpperInvariant();
        return dbContext.Set<PersonnelFilePersonnelAction>()
            .AsNoTracking()
            .AnyAsync(
                item => item.TenantId == tenantId
                    && item.PersonnelFileId == personnelFileInternalId
                    && item.ActionTypeCode == normalizedType
                    && item.CreatedUtc > sinceUtc,
                cancellationToken);
    }

    public Task<bool> HasLaterExecutedRetirementRequestAsync(
        long personnelFileInternalId,
        Guid tenantId,
        Guid excludingRequestPublicId,
        DateTime executionDateUtc,
        CancellationToken cancellationToken) =>
        dbContext.Set<PersonnelFileRetirementRequest>()
            .AsNoTracking()
            .AnyAsync(
                item => item.TenantId == tenantId
                    && item.PersonnelFileId == personnelFileInternalId
                    && item.PublicId != excludingRequestPublicId
                    && item.ExecutionDateUtc != null
                    && item.ExecutionDateUtc > executionDateUtc,
                cancellationToken);

    public async Task<IReadOnlyCollection<PersonnelFileEmploymentAssignment>> GetEmploymentAssignmentsByPublicIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> publicIds,
        CancellationToken cancellationToken) =>
        publicIds.Count == 0
            ? []
            : await dbContext.Set<PersonnelFileEmploymentAssignment>()
                .Where(item => item.TenantId == tenantId && publicIds.Contains(item.PublicId))
                .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<PersonnelFileContractHistory>> GetContractHistoriesByPublicIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> publicIds,
        CancellationToken cancellationToken) =>
        publicIds.Count == 0
            ? []
            : await dbContext.Set<PersonnelFileContractHistory>()
                .Where(item => item.TenantId == tenantId && publicIds.Contains(item.PublicId))
                .ToArrayAsync(cancellationToken);

    // ── Certificate requests ("constancias") — D-02/D-04 ─────────────────────────────────────────────────
    public async Task<IReadOnlyCollection<PersonnelFileCertificateRequestResponse>> AddCertificateRequestAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileCertificateRequest entity,
        CancellationToken cancellationToken)
    {
        dbContext.Set<PersonnelFileCertificateRequest>().Add(entity);
        var persisted = await dbContext.Set<PersonnelFileCertificateRequest>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId)
            .ToArrayAsync(cancellationToken);
        return persisted.Append(entity)
            .OrderByDescending(item => item.RequestDateUtc)
            .Select(MapCertificateRequest)
            .ToArray();
    }

    public async Task<PersonnelFileCertificateRequestResponse?> UpdateCertificateRequestAsync(
        Guid certificateRequestPublicId,
        Guid tenantId,
        CertificateRequestInput input,
        string? typeNameSnapshot,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileCertificateRequest>()
            .SingleOrDefaultAsync(x => x.PublicId == certificateRequestPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Update(input.TypeCode, typeNameSnapshot, input.PurposeCode, input.AddressedTo, input.DeliveryMethodCode, input.LanguageCode ?? "es", input.Copies ?? 1, input.NeededByDateUtc);
        return MapCertificateRequest(item);
    }

    public async Task<bool> SoftDeleteCertificateRequestAsync(
        Guid certificateRequestPublicId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileCertificateRequest>()
            .SingleOrDefaultAsync(x => x.PublicId == certificateRequestPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return false;
        item.SetActive(false);
        return true;
    }

    public async Task<IReadOnlyCollection<PersonnelFileCertificateRequestResponse>> GetCertificateRequestsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileCertificateRequest>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderByDescending(item => item.RequestDateUtc)
            .Select(item => MapCertificateRequest(item))
            .ToArrayAsync(cancellationToken);

    public async Task<PersonnelFileCertificateRequestResponse?> GetCertificateRequestAsync(
        Guid personnelFileId,
        Guid certificateRequestPublicId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileCertificateRequest>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.PersonnelFile.PublicId == personnelFileId && x.PublicId == certificateRequestPublicId, cancellationToken);
        return item is null ? null : MapCertificateRequest(item);
    }

    public async Task<PersonnelFileCertificateRequestResponse?> ProcessCertificateRequestAsync(
        Guid certificateRequestPublicId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileCertificateRequest>()
            .SingleOrDefaultAsync(x => x.PublicId == certificateRequestPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.StartProcessing();
        return MapCertificateRequest(item);
    }

    public async Task<PersonnelFileCertificateRequestResponse?> IssueCertificateRequestAsync(
        Guid certificateRequestPublicId,
        Guid tenantId,
        Guid issuedByUserId,
        DateTime issuedAtUtc,
        string? notes,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileCertificateRequest>()
            .SingleOrDefaultAsync(x => x.PublicId == certificateRequestPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Issue(issuedByUserId, issuedAtUtc, notes);
        return MapCertificateRequest(item);
    }

    public async Task<PersonnelFileCertificateRequestResponse?> DeliverCertificateRequestAsync(
        Guid certificateRequestPublicId,
        Guid tenantId,
        DateTime deliveredAtUtc,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileCertificateRequest>()
            .SingleOrDefaultAsync(x => x.PublicId == certificateRequestPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Deliver(deliveredAtUtc);
        return MapCertificateRequest(item);
    }

    public async Task<PersonnelFileCertificateRequestResponse?> RejectCertificateRequestAsync(
        Guid certificateRequestPublicId,
        Guid tenantId,
        string? notes,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileCertificateRequest>()
            .SingleOrDefaultAsync(x => x.PublicId == certificateRequestPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Reject(notes);
        return MapCertificateRequest(item);
    }

    public async Task<PersonnelFileCertificateRequestResponse?> CancelCertificateRequestAsync(
        Guid certificateRequestPublicId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileCertificateRequest>()
            .SingleOrDefaultAsync(x => x.PublicId == certificateRequestPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Cancel();
        return MapCertificateRequest(item);
    }

    public async Task<long?> GetCertificateRequestInternalIdAsync(
        Guid personnelFileId,
        Guid certificateRequestPublicId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileCertificateRequest>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId && item.PublicId == certificateRequestPublicId)
            .Select(item => (long?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private static PersonnelFileCertificateRequestResponse MapCertificateRequest(PersonnelFileCertificateRequest item) =>
        new(
            item.PublicId,
            item.CertificateTypeCode,
            item.TypeNameSnapshot,
            item.RequestStatusCode,
            item.PurposeCode,
            item.AddressedTo,
            item.DeliveryMethodCode,
            item.LanguageCode,
            item.Copies,
            item.RequestDateUtc,
            item.NeededByDateUtc,
            item.RequestedByUserId,
            item.IssuedByUserId,
            item.IssuedDateUtc,
            item.DeliveredDateUtc,
            item.ResolutionNotes,
            item.ResponseTimeDays,
            item.IsActive,
            item.ConcurrencyToken);

    public async Task AddCertificateRequestDocumentAsync(
        CertificateRequestDocument entity,
        CancellationToken cancellationToken)
    {
        await dbContext.Set<CertificateRequestDocument>().AddAsync(entity, cancellationToken);
    }

    public async Task<IReadOnlyCollection<CertificateRequestDocumentResponse>> GetCertificateRequestDocumentsAsync(
        Guid certificateRequestPublicId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<CertificateRequestDocument>()
            .AsNoTracking()
            .Where(document => document.CertificateRequest.PublicId == certificateRequestPublicId && document.IsActive)
            .OrderByDescending(document => document.CreatedUtc)
            .Select(document => MapCertificateRequestDocument(document))
            .ToArrayAsync(cancellationToken);

    public async Task<CertificateRequestDocumentResponse?> GetCertificateRequestDocumentAsync(
        Guid certificateRequestPublicId,
        Guid documentPublicId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<CertificateRequestDocument>()
            .AsNoTracking()
            .Where(document => document.CertificateRequest.PublicId == certificateRequestPublicId && document.PublicId == documentPublicId)
            .Select(document => MapCertificateRequestDocument(document))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<CertificateRequestDocument?> GetCertificateRequestDocumentEntityAsync(
        Guid certificateRequestPublicId,
        Guid documentPublicId,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<CertificateRequestDocument>()
            .SingleOrDefaultAsync(
                document => document.CertificateRequest.PublicId == certificateRequestPublicId
                    && document.PublicId == documentPublicId
                    && document.TenantId == tenantId,
                cancellationToken);

    private static CertificateRequestDocumentResponse MapCertificateRequestDocument(CertificateRequestDocument document) =>
        new(
            document.PublicId,
            document.IsSystemGenerated,
            document.Observations,
            document.FilePublicId,
            document.FileName,
            document.ContentType,
            document.SizeBytes,
            document.IsActive,
            document.ConcurrencyToken,
            document.CreatedUtc,
            document.ModifiedUtc);

    private IQueryable<PersonnelFileCertificateRequest> FilteredCertificateRequests(
        Guid companyId,
        string? typeCode,
        string? statusCode,
        string? purposeCode,
        Guid? employeeId,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? search)
    {
        var query = dbContext.Set<PersonnelFileCertificateRequest>()
            .AsNoTracking()
            .Where(request => request.TenantId == companyId && request.IsActive);

        if (!string.IsNullOrWhiteSpace(typeCode))
        {
            var code = typeCode.Trim().ToUpperInvariant();
            query = query.Where(request => request.CertificateTypeCode == code);
        }

        if (!string.IsNullOrWhiteSpace(statusCode))
        {
            var code = statusCode.Trim().ToUpperInvariant();
            query = query.Where(request => request.RequestStatusCode == code);
        }

        if (!string.IsNullOrWhiteSpace(purposeCode))
        {
            var code = purposeCode.Trim().ToUpperInvariant();
            query = query.Where(request => request.PurposeCode == code);
        }

        if (employeeId is { } personnelFilePublicId)
        {
            query = query.Where(request => request.PersonnelFile.PublicId == personnelFilePublicId);
        }

        if (fromUtc is { } from)
        {
            query = query.Where(request => request.RequestDateUtc >= from);
        }

        if (toUtc is { } to)
        {
            query = query.Where(request => request.RequestDateUtc <= to);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToUpperInvariant();
            query = query.Where(request =>
                (request.AddressedTo != null && request.AddressedTo.ToUpper().Contains(term))
                || (request.PersonnelFile.FirstName + " " + request.PersonnelFile.LastName).ToUpper().Contains(term));
        }

        return query;
    }

    public async Task<CertificateRequestBandejaResponse> QueryCertificateRequestsAsync(
        Guid companyId,
        string? typeCode,
        string? statusCode,
        string? purposeCode,
        Guid? employeeId,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = FilteredCertificateRequests(companyId, typeCode, statusCode, purposeCode, employeeId, fromUtc, toUtc, search);

        var totalCount = await query.CountAsync(cancellationToken);

        var statusGroups = await query
            .GroupBy(request => request.RequestStatusCode)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var page = await query
            .OrderByDescending(request => request.RequestDateUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(request => new
            {
                request.PublicId,
                PersonnelFilePublicId = request.PersonnelFile.PublicId,
                request.PersonnelFile.FirstName,
                request.PersonnelFile.LastName,
                request.CertificateTypeCode,
                request.TypeNameSnapshot,
                request.PurposeCode,
                request.RequestStatusCode,
                request.AddressedTo,
                request.DeliveryMethodCode,
                request.RequestDateUtc,
                request.IssuedDateUtc,
                request.DeliveredDateUtc,
                request.IssuedByUserId,
                request.ResponseTimeDays
            })
            .ToListAsync(cancellationToken);

        var items = page
            .Select(request => new CertificateRequestListItemResponse(
                request.PublicId,
                request.PersonnelFilePublicId,
                $"{request.FirstName} {request.LastName}".Trim(),
                request.CertificateTypeCode,
                request.TypeNameSnapshot,
                request.PurposeCode,
                request.RequestStatusCode,
                request.AddressedTo,
                request.DeliveryMethodCode,
                request.RequestDateUtc,
                request.IssuedDateUtc,
                request.DeliveredDateUtc,
                request.IssuedByUserId,
                request.ResponseTimeDays))
            .ToArray();

        return new CertificateRequestBandejaResponse(
            items,
            pageNumber,
            pageSize,
            totalCount,
            statusGroups.ToDictionary(group => group.Status, group => group.Count));
    }

    public async Task<IReadOnlyCollection<CertificateRequestExportRow>> GetCertificateRequestExportRowsAsync(
        Guid companyId,
        string? typeCode,
        string? statusCode,
        string? purposeCode,
        Guid? employeeId,
        DateTime? fromUtc,
        DateTime? toUtc,
        string? search,
        int? maxRows,
        CancellationToken cancellationToken)
    {
        var query = FilteredCertificateRequests(companyId, typeCode, statusCode, purposeCode, employeeId, fromUtc, toUtc, search);
        var take = maxRows is > 0 ? maxRows.Value : 100_000;

        var rows = await query
            .OrderByDescending(request => request.RequestDateUtc)
            .Take(take)
            .Select(request => new
            {
                request.PersonnelFile.FirstName,
                request.PersonnelFile.LastName,
                request.CertificateTypeCode,
                request.TypeNameSnapshot,
                request.PurposeCode,
                request.RequestStatusCode,
                request.AddressedTo,
                request.DeliveryMethodCode,
                request.RequestDateUtc,
                request.IssuedDateUtc,
                request.DeliveredDateUtc,
                request.ResponseTimeDays
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(request => new CertificateRequestExportRow(
                $"{request.FirstName} {request.LastName}".Trim(),
                request.TypeNameSnapshot ?? request.CertificateTypeCode,
                request.PurposeCode,
                request.RequestStatusCode,
                request.AddressedTo,
                request.DeliveryMethodCode,
                request.RequestDateUtc,
                request.IssuedDateUtc,
                request.DeliveredDateUtc,
                request.ResponseTimeDays))
            .ToArray();
    }

    public async Task AddEconomicAidRequestDocumentAsync(
        EconomicAidRequestDocument entity,
        CancellationToken cancellationToken)
    {
        await dbContext.Set<EconomicAidRequestDocument>().AddAsync(entity, cancellationToken);
    }

    public async Task<IReadOnlyCollection<EconomicAidRequestDocumentResponse>> GetEconomicAidRequestDocumentsAsync(
        Guid economicAidRequestPublicId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<EconomicAidRequestDocument>()
            .AsNoTracking()
            .Where(document => document.EconomicAidRequest.PublicId == economicAidRequestPublicId && document.IsActive)
            .OrderByDescending(document => document.CreatedUtc)
            .Select(document => MapEconomicAidRequestDocument(document))
            .ToArrayAsync(cancellationToken);

    public async Task<EconomicAidRequestDocumentResponse?> GetEconomicAidRequestDocumentAsync(
        Guid economicAidRequestPublicId,
        Guid documentPublicId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<EconomicAidRequestDocument>()
            .AsNoTracking()
            .Where(document => document.EconomicAidRequest.PublicId == economicAidRequestPublicId && document.PublicId == documentPublicId)
            .Select(document => MapEconomicAidRequestDocument(document))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<EconomicAidRequestDocument?> GetEconomicAidRequestDocumentEntityAsync(
        Guid economicAidRequestPublicId,
        Guid documentPublicId,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<EconomicAidRequestDocument>()
            .SingleOrDefaultAsync(
                document => document.EconomicAidRequest.PublicId == economicAidRequestPublicId
                    && document.PublicId == documentPublicId
                    && document.TenantId == tenantId,
                cancellationToken);

    private static EconomicAidRequestDocumentResponse MapEconomicAidRequestDocument(EconomicAidRequestDocument document) =>
        new(
            document.PublicId,
            document.DocumentTypeCatalogItem != null ? document.DocumentTypeCatalogItem.PublicId : (Guid?)null,
            document.DocumentTypeCatalogItem != null ? document.DocumentTypeCatalogItem.Code : null,
            document.DocumentTypeCatalogItem != null ? document.DocumentTypeCatalogItem.Name : null,
            document.Observations,
            document.FilePublicId,
            document.FileName,
            document.ContentType,
            document.SizeBytes,
            document.IsActive,
            document.ConcurrencyToken,
            document.CreatedUtc,
            document.ModifiedUtc);

    private static PersonnelFilePerformanceEvaluationResponse Map(PersonnelFilePerformanceEvaluation item) =>
        new(
            item.PublicId,
            item.EvaluatorName,
            item.EvaluationDateUtc,
            item.Score,
            item.QualitativeScoreCode,
            item.Comment,
            item.SourceSystem,
            item.SourceReference,
            item.SourceSyncedUtc,
            item.ConcurrencyToken);

    private static PersonnelFileSelectionContestResponse Map(PersonnelFileSelectionContest item) =>
        new(
            item.PublicId,
            item.ContestCode,
            item.ContestName,
            item.ContestDateUtc,
            item.ResultCode,
            item.Notes,
            item.SourceSystem,
            item.SourceReference,
            item.SourceSyncedUtc,
            item.ConcurrencyToken);

    private static PersonnelFileCurricularCompetencyResponse Map(PersonnelFileCurricularCompetency item) =>
        new(
            item.PublicId,
            item.RequirementTypeCode,
            item.RequirementName,
            item.CompetencyDomain,
            item.ExperienceTimeValue,
            item.MetricCode,
            item.Notes,
            item.SourceSystem,
            item.SourceReference,
            item.SourceSyncedUtc,
            item.ConcurrencyToken);

    // ── Recurring incomes (REQ-005) ────────────────────────────────────────────────────────────────

    // A fixed class id namespaces this advisory lock; the object id is derived deterministically from the
    // recurring-income public id so every installment application/annulment of one income contends on the same
    // lock. Executed on the context's current transaction (the handler opens one), pg_advisory_xact_lock holds
    // until that transaction commits/rolls back, serializing the strict installment sequence (RF-006/RF-008).
    private const int RecurringIncomeMutationLockClassId = 0x52_49_4E_43; // "RINC" — recurring income

    public Task AcquireRecurringIncomeMutationLockAsync(Guid recurringIncomePublicId, CancellationToken cancellationToken)
    {
        var objectKey = BitConverter.ToInt32(recurringIncomePublicId.ToByteArray(), 0);
        return dbContext.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock({0}, {1})",
            new object[] { RecurringIncomeMutationLockClassId, objectKey },
            cancellationToken);
    }

    // ── One-time incomes (REQ-006) ─────────────────────────────────────────────────────────────────

    // A fixed class id namespaces this advisory lock; the object id is derived deterministically from the
    // one-time-income public id so every application registration/annulment of one income contends on the same
    // lock. Executed on the context's current transaction (the handler opens one), pg_advisory_xact_lock holds
    // until that transaction commits/rolls back, serializing the at-most-one-active-application rule (RN-06).
    private const int OneTimeIncomeMutationLockClassId = 0x4F_54_49_4E; // "OTIN" — one-time income

    public Task AcquireOneTimeIncomeMutationLockAsync(Guid oneTimeIncomePublicId, CancellationToken cancellationToken)
    {
        var objectKey = BitConverter.ToInt32(oneTimeIncomePublicId.ToByteArray(), 0);
        return dbContext.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock({0}, {1})",
            new object[] { OneTimeIncomeMutationLockClassId, objectKey },
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<RecurringIncomeResponse>> AddRecurringIncomeAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileRecurringIncome entity,
        CancellationToken cancellationToken)
    {
        dbContext.Set<PersonnelFileRecurringIncome>().Add(entity);
        // Append the in-memory entity: the row is not saved yet, so an AsNoTracking re-query excludes it.
        var persisted = await dbContext.Set<PersonnelFileRecurringIncome>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId)
            .ToArrayAsync(cancellationToken);
        return persisted.Append(entity)
            .OrderByDescending(item => item.RegistrationDate)
            .ThenByDescending(item => item.PublicId)
            .Select(RecurringIncomeMapping.ToResponse)
            .ToArray();
    }

    public async Task<IReadOnlyCollection<RecurringIncomeResponse>> GetRecurringIncomesAsync(
        Guid personnelFilePublicId,
        CancellationToken cancellationToken)
    {
        var items = await dbContext.Set<PersonnelFileRecurringIncome>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFilePublicId)
            .OrderByDescending(item => item.RegistrationDate)
            .ThenByDescending(item => item.PublicId)
            .ToArrayAsync(cancellationToken);
        return items.Select(RecurringIncomeMapping.ToResponse).ToArray();
    }

    public async Task<RecurringIncomeResponse?> GetRecurringIncomeAsync(
        Guid personnelFilePublicId,
        Guid recurringIncomePublicId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileRecurringIncome>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.PersonnelFile.PublicId == personnelFilePublicId && x.PublicId == recurringIncomePublicId,
                cancellationToken);
        return item is null ? null : RecurringIncomeMapping.ToResponse(item);
    }

    public Task<PersonnelFileRecurringIncome?> GetRecurringIncomeEntityAsync(
        Guid personnelFilePublicId,
        Guid recurringIncomePublicId,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        dbContext.Set<PersonnelFileRecurringIncome>()
            .SingleOrDefaultAsync(
                x => x.TenantId == tenantId
                    && x.PublicId == recurringIncomePublicId
                    && x.PersonnelFile.PublicId == personnelFilePublicId,
                cancellationToken);

    public async Task<RecurringIncomePlazaResolution> ResolveRecurringIncomePlazaAsync(
        long personnelFileInternalId,
        Guid? assignedPositionPublicId,
        CancellationToken cancellationToken)
    {
        var assignments = await dbContext.Set<PersonnelFileEmploymentAssignment>()
            .AsNoTracking()
            .Where(item => item.PersonnelFileId == personnelFileInternalId)
            .Select(item => new { item.PublicId, item.StartDate, item.IsActive, item.IsPrimary, item.CostCenterPublicId })
            .ToListAsync(cancellationToken);
        if (assignments.Count == 0)
        {
            return RecurringIncomePlazaResolution.NotFound;
        }

        var chosen = assignedPositionPublicId is { } requested && requested != Guid.Empty
            ? assignments.FirstOrDefault(item => item.PublicId == requested)
            : assignments.Where(item => item.IsActive && item.IsPrimary).OrderBy(item => item.StartDate).FirstOrDefault()
                ?? assignments.Where(item => item.IsActive).OrderBy(item => item.StartDate).FirstOrDefault()
                ?? assignments.OrderBy(item => item.StartDate).FirstOrDefault();

        if (chosen is null)
        {
            return RecurringIncomePlazaResolution.NotFound;
        }

        string? costCenterName = null;
        if (chosen.CostCenterPublicId is { } costCenterPublicId)
        {
            costCenterName = await dbContext.CostCenters
                .AsNoTracking()
                .Where(item => item.PublicId == costCenterPublicId)
                .Select(item => item.Name)
                .SingleOrDefaultAsync(cancellationToken);
        }

        return new RecurringIncomePlazaResolution(true, chosen.PublicId, chosen.CostCenterPublicId, costCenterName);
    }

    public Task<bool> IsRecurringIncomeProfileRetiredAsync(
        long personnelFileInternalId,
        CancellationToken cancellationToken) =>
        dbContext.Set<PersonnelFileEmployeeProfile>()
            .AsNoTracking()
            .AnyAsync(
                profile => profile.PersonnelFileId == personnelFileInternalId
                    && profile.EmploymentStatusCode == PersonnelFileEmployeeProfile.RetiredEmploymentStatusCode,
                cancellationToken);

    // ── Recurring-income installments (REQ-005 PR-4) ────────────────────────────────────────────────

    public Task<PersonnelFileRecurringIncome?> GetTrackedRecurringIncomeWithInstallmentsAsync(
        Guid recurringIncomePublicId,
        Guid tenantId,
        CancellationToken cancellationToken) =>
        dbContext.Set<PersonnelFileRecurringIncome>()
            .Include(income => income.Installments)
            .SingleOrDefaultAsync(
                income => income.TenantId == tenantId && income.PublicId == recurringIncomePublicId,
                cancellationToken);

    public async Task<IReadOnlyCollection<PersonnelFileRecurringIncome>> GetVigenteRecurringIncomesForSettlementAsync(
        long personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileRecurringIncome>()
            .Where(income => income.PersonnelFileId == personnelFileId
                && income.StatusCode == RecurringIncomeStatuses.Vigente)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyCollection<PersonnelFileRecurringIncome>> GetRecurringIncomesClosedBySettlementAsync(
        long personnelFileId,
        Guid settlementPublicId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileRecurringIncome>()
            .Where(income => income.PersonnelFileId == personnelFileId
                && income.ClosedBySettlementPublicId == settlementPublicId)
            .ToListAsync(cancellationToken);

    public async Task<RecurringIncomeScheduleData?> GetRecurringIncomeScheduleDataAsync(
        Guid personnelFilePublicId,
        Guid recurringIncomePublicId,
        CancellationToken cancellationToken)
    {
        var income = await dbContext.Set<PersonnelFileRecurringIncome>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFilePublicId && item.PublicId == recurringIncomePublicId)
            .Select(item => new
            {
                item.Id,
                item.PublicId,
                item.StatusCode,
                item.IsIndefinite,
                item.InstallmentFrequencyCode,
                item.InstallmentStartDate,
                item.InstallmentValue,
                item.InstallmentCount,
                item.TotalAmount
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (income is null)
        {
            return null;
        }

        var applied = await dbContext.Set<PersonnelFileRecurringIncomeInstallment>()
            .AsNoTracking()
            .Where(installment => installment.RecurringIncomeId == income.Id
                && installment.StatusCode == RecurringIncomeInstallmentStatuses.Aplicada)
            .Select(installment => installment.InstallmentNumber)
            .ToArrayAsync(cancellationToken);

        return new RecurringIncomeScheduleData(
            income.PublicId,
            income.StatusCode,
            income.IsIndefinite,
            income.InstallmentFrequencyCode,
            income.InstallmentStartDate,
            income.InstallmentValue,
            income.InstallmentCount,
            income.TotalAmount,
            applied);
    }

    public async Task<RecurringIncomeInstallmentHistoryResponse?> GetRecurringIncomeInstallmentHistoryAsync(
        Guid personnelFilePublicId,
        Guid recurringIncomePublicId,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var incomeInternalId = await dbContext.Set<PersonnelFileRecurringIncome>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFilePublicId && item.PublicId == recurringIncomePublicId)
            .Select(item => item.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (incomeInternalId == 0)
        {
            return null;
        }

        var baseQuery = dbContext.Set<PersonnelFileRecurringIncomeInstallment>()
            .AsNoTracking()
            .Where(installment => installment.RecurringIncomeId == incomeInternalId);

        var totalCount = await baseQuery.CountAsync(cancellationToken);

        var rows = await baseQuery
            .OrderByDescending(installment => installment.CreatedUtc)
            .ThenByDescending(installment => installment.InstallmentNumber)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(installment => new
            {
                installment.PublicId,
                installment.InstallmentNumber,
                installment.AppliedDate,
                installment.TheoreticalDueDate,
                installment.Amount,
                installment.CurrencyCode,
                installment.PayrollTypeCode,
                installment.PayrollPeriodId,
                installment.PayrollPeriodLabel,
                installment.OriginCode,
                installment.StatusCode,
                installment.AppliedByUserId,
                installment.AnnulmentReason,
                installment.AnnulledByUserId,
                installment.AnnulledUtc,
                installment.Notes,
                installment.ConcurrencyToken
            })
            .ToArrayAsync(cancellationToken);

        var periodIds = rows.Where(row => row.PayrollPeriodId is not null).Select(row => row.PayrollPeriodId!.Value).Distinct().ToArray();
        var periodPublicIds = periodIds.Length == 0
            ? new Dictionary<long, Guid>()
            : await dbContext.PayrollPeriodDefinitions
                .AsNoTracking()
                .Where(period => periodIds.Contains(period.Id))
                .Select(period => new { period.Id, period.PublicId })
                .ToDictionaryAsync(period => period.Id, period => period.PublicId, cancellationToken);

        var items = rows
            .Select(row => new RecurringIncomeInstallmentResponse(
                row.PublicId,
                row.InstallmentNumber,
                row.AppliedDate,
                row.TheoreticalDueDate,
                row.Amount,
                row.CurrencyCode,
                row.PayrollTypeCode,
                row.PayrollPeriodId is { } internalId && periodPublicIds.TryGetValue(internalId, out var publicId) ? publicId : null,
                row.PayrollPeriodLabel,
                row.OriginCode,
                row.StatusCode,
                row.AppliedByUserId == Guid.Empty ? null : row.AppliedByUserId,
                row.AnnulmentReason,
                row.AnnulledByUserId,
                row.AnnulledUtc,
                row.Notes,
                row.ConcurrencyToken))
            .ToArray();

        return new RecurringIncomeInstallmentHistoryResponse(items, pageNumber, pageSize, totalCount);
    }

    public async Task<RecurringIncomePayrollPeriodResolution?> ResolveRecurringIncomePayrollPeriodAsync(
        Guid tenantId,
        Guid payrollPeriodPublicId,
        CancellationToken cancellationToken)
    {
        var period = await dbContext.PayrollPeriodDefinitions
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PublicId == payrollPeriodPublicId)
            .Select(item => new { item.Id, item.Label, item.EndDate, item.IsActive })
            .SingleOrDefaultAsync(cancellationToken);
        return period is null
            ? null
            : new RecurringIncomePayrollPeriodResolution(period.Id, period.Label, period.EndDate, period.IsActive);
    }

    public async Task<Guid?> ResolvePayrollPeriodPublicIdAsync(
        long payrollPeriodInternalId,
        CancellationToken cancellationToken)
    {
        var publicId = await dbContext.PayrollPeriodDefinitions
            .AsNoTracking()
            .Where(item => item.Id == payrollPeriodInternalId)
            .Select(item => (Guid?)item.PublicId)
            .SingleOrDefaultAsync(cancellationToken);
        return publicId;
    }

    public async Task<IReadOnlyList<RecurringIncomeBatchScanItem>> GetRecurringIncomeBatchScanAsync(
        Guid tenantId,
        string payrollTypeCode,
        CancellationToken cancellationToken)
    {
        var incomes = await dbContext.Set<PersonnelFileRecurringIncome>()
            .AsNoTracking()
            .Where(income => income.TenantId == tenantId
                && income.StatusCode == RecurringIncomeStatuses.Vigente
                && income.PayrollTypeCode == payrollTypeCode)
            .OrderBy(income => income.Id)
            .Select(income => new
            {
                income.Id,
                income.PublicId,
                income.IsIndefinite,
                income.InstallmentFrequencyCode,
                income.InstallmentStartDate,
                income.InstallmentValue,
                income.InstallmentCount,
                income.TotalAmount
            })
            .ToArrayAsync(cancellationToken);
        if (incomes.Length == 0)
        {
            return [];
        }

        var incomeIds = incomes.Select(income => income.Id).ToArray();
        var appliedByIncome = (await dbContext.Set<PersonnelFileRecurringIncomeInstallment>()
                .AsNoTracking()
                .Where(installment => incomeIds.Contains(installment.RecurringIncomeId)
                    && installment.StatusCode == RecurringIncomeInstallmentStatuses.Aplicada)
                .Select(installment => new { installment.RecurringIncomeId, installment.InstallmentNumber })
                .ToArrayAsync(cancellationToken))
            .GroupBy(installment => installment.RecurringIncomeId)
            .ToDictionary(group => group.Key, group => group.Select(installment => installment.InstallmentNumber).ToArray());

        return incomes
            .Select(income => new RecurringIncomeBatchScanItem(
                income.Id,
                income.PublicId,
                income.IsIndefinite,
                income.InstallmentFrequencyCode,
                income.InstallmentStartDate,
                income.InstallmentValue,
                income.InstallmentCount,
                income.TotalAmount,
                appliedByIncome.TryGetValue(income.Id, out var numbers) ? numbers : []))
            .ToArray();
    }

    // ── Recurring-income bandeja + exports (REQ-005 PR-5) ───────────────────────────────────────────

    public async Task<RecurringIncomeBandejaResponse> QueryRecurringIncomesAsync(
        QueryRecurringIncomesQuery query,
        CancellationToken cancellationToken)
    {
        var baseQuery = FilteredRecurringIncomes(
            query.CompanyId, query.EmployeeId, query.RecurringIncomeTypeCode, query.PayrollTypeCode,
            query.RegisteredFromUtc, query.RegisteredToUtc);

        // StatusCounts over the full (non-status) filter, so every status is represented even when the items are
        // narrowed to one status.
        var statusCounts = await baseQuery
            .GroupBy(row => row.Income.StatusCode)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.Key, group => group.Count, cancellationToken);

        var filtered = baseQuery;
        if (!string.IsNullOrWhiteSpace(query.StatusCode))
        {
            var status = query.StatusCode.Trim().ToUpperInvariant();
            filtered = filtered.Where(row => row.Income.StatusCode == status);
        }

        var totalCount = await filtered.CountAsync(cancellationToken);

        var items = await filtered
            .OrderByDescending(row => row.Income.RegistrationDate)
            .ThenByDescending(row => row.Income.CreatedUtc)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(row => new RecurringIncomeListItemResponse(
                row.Income.PublicId,
                row.EmployeeFilePublicId,
                row.EmployeeFullName,
                row.EmployeeCode,
                row.Income.RecurringIncomeTypeCode,
                row.Income.ConceptTypeCode,
                row.Income.ConceptNameSnapshot,
                row.Income.AssignedPositionPublicId,
                row.Income.CostCenterPublicId,
                row.Income.CostCenterNameSnapshot,
                row.Income.RegistrationDate,
                row.Income.InstallmentFrequencyCode,
                row.Income.CurrencyCode,
                row.Income.PayrollTypeCode,
                row.Income.IsIndefinite,
                row.Income.InstallmentValue,
                row.Income.InstallmentCount,
                row.Income.TotalAmount,
                row.Income.SettlementActionCode,
                row.Income.StatusCode,
                row.Income.RegisteredByUserId == Guid.Empty ? (Guid?)null : row.Income.RegisteredByUserId,
                row.Income.DecidedByUserId))
            .ToArrayAsync(cancellationToken);

        return new RecurringIncomeBandejaResponse(items, query.PageNumber, query.PageSize, totalCount, statusCounts);
    }

    public async Task<IReadOnlyCollection<IngresoCiclicoExportRow>> GetRecurringIncomeExportRowsAsync(
        ExportRecurringIncomesQuery query,
        CancellationToken cancellationToken)
    {
        var filtered = FilteredRecurringIncomes(
            query.CompanyId, query.EmployeeId, query.RecurringIncomeTypeCode, query.PayrollTypeCode,
            query.RegisteredFromUtc, query.RegisteredToUtc);

        if (!string.IsNullOrWhiteSpace(query.StatusCode))
        {
            var status = query.StatusCode.Trim().ToUpperInvariant();
            filtered = filtered.Where(row => row.Income.StatusCode == status);
        }

        var ordered = filtered
            .OrderByDescending(row => row.Income.RegistrationDate)
            .ThenByDescending(row => row.Income.CreatedUtc);

        var limited = query.MaxRows is { } maxRows ? ordered.Take(maxRows + 1) : (IQueryable<RecurringIncomeQueryRow>)ordered;

        var rows = await limited.ToArrayAsync(cancellationToken);
        return rows
            .Select(row => new IngresoCiclicoExportRow(
                row.EmployeeFullName,
                row.EmployeeCode,
                row.Income.RecurringIncomeTypeCode,
                row.Income.ConceptNameSnapshot,
                row.Income.AssignedPositionPublicId.ToString(),
                row.Income.CostCenterNameSnapshot,
                row.Income.RegistrationDate,
                row.Income.InstallmentFrequencyCode,
                row.Income.InstallmentValue,
                row.Income.InstallmentCount,
                row.Income.TotalAmount,
                row.Income.IsIndefinite,
                row.Income.SettlementActionCode,
                row.Income.StatusCode,
                row.Income.CurrencyCode,
                row.Income.RegisteredByUserId == Guid.Empty ? null : row.Income.RegisteredByUserId.ToString(),
                row.Income.DecidedByUserId?.ToString()))
            .ToArray();
    }

    public async Task<IReadOnlyList<RecurringIncomePendingScanItem>> GetRecurringIncomePendingScanAsync(
        Guid tenantId,
        string? payrollTypeCode,
        Guid? employeeId,
        CancellationToken cancellationToken)
    {
        var query =
            from income in dbContext.Set<PersonnelFileRecurringIncome>().AsNoTracking()
            join file in dbContext.PersonnelFiles.AsNoTracking() on income.PersonnelFileId equals file.Id
            join profileEntry in dbContext.PersonnelFileEmployeeProfiles.AsNoTracking()
                on file.Id equals profileEntry.PersonnelFileId into profileGroup
            from profile in profileGroup.DefaultIfEmpty()
            where income.TenantId == tenantId && income.StatusCode == RecurringIncomeStatuses.Vigente
            select new
            {
                income.Id,
                income.PublicId,
                FilePublicId = file.PublicId,
                file.FullName,
                EmployeeCode = profile != null ? profile.EmployeeCode : null,
                income.RecurringIncomeTypeCode,
                income.ConceptNameSnapshot,
                income.AssignedPositionPublicId,
                income.CostCenterNameSnapshot,
                income.PayrollTypeCode,
                income.CurrencyCode,
                income.IsIndefinite,
                income.InstallmentFrequencyCode,
                income.InstallmentStartDate,
                income.InstallmentValue,
                income.InstallmentCount,
                income.TotalAmount
            };

        if (!string.IsNullOrWhiteSpace(payrollTypeCode))
        {
            var normalizedPayrollType = payrollTypeCode.Trim().ToUpperInvariant();
            query = query.Where(row => row.PayrollTypeCode == normalizedPayrollType);
        }

        if (employeeId is { } employeePublicId)
        {
            query = query.Where(row => row.FilePublicId == employeePublicId);
        }

        var incomes = await query.OrderBy(row => row.Id).ToArrayAsync(cancellationToken);
        if (incomes.Length == 0)
        {
            return [];
        }

        var incomeIds = incomes.Select(income => income.Id).ToArray();
        var appliedByIncome = (await dbContext.Set<PersonnelFileRecurringIncomeInstallment>()
                .AsNoTracking()
                .Where(installment => incomeIds.Contains(installment.RecurringIncomeId)
                    && installment.StatusCode == RecurringIncomeInstallmentStatuses.Aplicada)
                .Select(installment => new { installment.RecurringIncomeId, installment.InstallmentNumber })
                .ToArrayAsync(cancellationToken))
            .GroupBy(installment => installment.RecurringIncomeId)
            .ToDictionary(group => group.Key, group => group.Select(installment => installment.InstallmentNumber).ToArray());

        return incomes
            .Select(income => new RecurringIncomePendingScanItem(
                income.Id,
                income.PublicId,
                income.FilePublicId,
                income.FullName,
                income.EmployeeCode,
                income.RecurringIncomeTypeCode,
                income.ConceptNameSnapshot,
                income.AssignedPositionPublicId,
                income.CostCenterNameSnapshot,
                income.PayrollTypeCode,
                income.CurrencyCode,
                income.IsIndefinite,
                income.InstallmentFrequencyCode,
                income.InstallmentStartDate,
                income.InstallmentValue,
                income.InstallmentCount,
                income.TotalAmount,
                appliedByIncome.TryGetValue(income.Id, out var numbers) ? numbers : []))
            .ToArray();
    }

    public async Task<IReadOnlyCollection<InsumoPlanillaCiclicoExportRow>> GetRecurringIncomePayrollInputRowsAsync(
        Guid tenantId,
        string? payrollTypeCode,
        DateOnly startDate,
        DateOnly endDate,
        int? maxRows,
        CancellationToken cancellationToken)
    {
        var query =
            from installment in dbContext.Set<PersonnelFileRecurringIncomeInstallment>().AsNoTracking()
            join income in dbContext.Set<PersonnelFileRecurringIncome>().AsNoTracking()
                on installment.RecurringIncomeId equals income.Id
            join file in dbContext.PersonnelFiles.AsNoTracking() on income.PersonnelFileId equals file.Id
            join profileEntry in dbContext.PersonnelFileEmployeeProfiles.AsNoTracking()
                on file.Id equals profileEntry.PersonnelFileId into profileGroup
            from profile in profileGroup.DefaultIfEmpty()
            where installment.TenantId == tenantId
                // Excludes suspendidos/anulados: only APLICADA + active installments feed the payroll input.
                && installment.StatusCode == RecurringIncomeInstallmentStatuses.Aplicada
                && installment.IsActive
                && installment.AppliedDate >= startDate
                && installment.AppliedDate <= endDate
            select new
            {
                file.FullName,
                EmployeeCode = profile != null ? profile.EmployeeCode : null,
                income.ConceptNameSnapshot,
                installment.PayrollTypeCode,
                installment.PayrollPeriodLabel,
                installment.AppliedDate,
                installment.InstallmentNumber,
                installment.Amount,
                installment.CurrencyCode,
                income.CostCenterNameSnapshot
            };

        if (!string.IsNullOrWhiteSpace(payrollTypeCode))
        {
            var normalizedPayrollType = payrollTypeCode.Trim().ToUpperInvariant();
            query = query.Where(row => row.PayrollTypeCode == normalizedPayrollType);
        }

        var ordered = query
            .OrderBy(row => row.AppliedDate)
            .ThenBy(row => row.FullName)
            .ThenBy(row => row.InstallmentNumber);

        var rows = maxRows is { } cap
            ? await ordered.Take(cap + 1).ToArrayAsync(cancellationToken)
            : await ordered.ToArrayAsync(cancellationToken);
        return rows
            .Select(row => new InsumoPlanillaCiclicoExportRow(
                row.FullName,
                row.EmployeeCode,
                row.ConceptNameSnapshot,
                row.PayrollTypeCode,
                row.PayrollPeriodLabel,
                row.AppliedDate,
                row.InstallmentNumber,
                row.Amount,
                row.CurrencyCode,
                row.CostCenterNameSnapshot))
            .ToArray();
    }

    /// <summary>The company-scoped recurring-income filter shared by the bandeja and its export (member-init
    /// intermediate projection so EF composes the Where/GroupBy over it reliably — same shape as the incapacities
    /// / settlements filters).</summary>
    private IQueryable<RecurringIncomeQueryRow> FilteredRecurringIncomes(
        Guid tenantId,
        Guid? employeeId,
        string? recurringIncomeTypeCode,
        string? payrollTypeCode,
        DateTime? registeredFromUtc,
        DateTime? registeredToUtc)
    {
        var query =
            from income in dbContext.Set<PersonnelFileRecurringIncome>().AsNoTracking()
            join file in dbContext.PersonnelFiles.AsNoTracking() on income.PersonnelFileId equals file.Id
            join profileEntry in dbContext.PersonnelFileEmployeeProfiles.AsNoTracking()
                on file.Id equals profileEntry.PersonnelFileId into profileGroup
            from profile in profileGroup.DefaultIfEmpty()
            where income.TenantId == tenantId
            select new RecurringIncomeQueryRow
            {
                Income = income,
                EmployeeFullName = file.FullName,
                EmployeeFilePublicId = file.PublicId,
                EmployeeCode = profile != null ? profile.EmployeeCode : null,
            };

        if (employeeId is { } employeePublicId)
        {
            query = query.Where(row => row.EmployeeFilePublicId == employeePublicId);
        }

        if (!string.IsNullOrWhiteSpace(recurringIncomeTypeCode))
        {
            var normalizedType = recurringIncomeTypeCode.Trim().ToUpperInvariant();
            query = query.Where(row => row.Income.RecurringIncomeTypeCode == normalizedType);
        }

        if (!string.IsNullOrWhiteSpace(payrollTypeCode))
        {
            var normalizedPayrollType = payrollTypeCode.Trim().ToUpperInvariant();
            query = query.Where(row => row.Income.PayrollTypeCode == normalizedPayrollType);
        }

        if (registeredFromUtc is { } from)
        {
            var fromDate = DateOnly.FromDateTime(from);
            query = query.Where(row => row.Income.RegistrationDate >= fromDate);
        }

        if (registeredToUtc is { } to)
        {
            var toDate = DateOnly.FromDateTime(to);
            query = query.Where(row => row.Income.RegistrationDate <= toDate);
        }

        return query;
    }

    private sealed class RecurringIncomeQueryRow
    {
        public PersonnelFileRecurringIncome Income { get; init; } = null!;

        public string EmployeeFullName { get; init; } = string.Empty;

        public Guid EmployeeFilePublicId { get; init; }

        public string? EmployeeCode { get; init; }
    }
}

using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.PersonnelFiles;
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

        existing.Update(
            profile.EmployeeCode,
            profile.EmploymentStatusCode,
            profile.HireDate,
            profile.RetirementCategoryCode,
            profile.RetirementReasonCode,
            profile.RetirementNotes,
            profile.RetirementDate);

        return Map(existing);
    }

    public async Task<PersonnelFileEmployeeProfileResponse?> GetEmployeeProfileAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileEmployeeProfile>()
            .AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.PersonnelFile.PublicId == personnelFileId, cancellationToken);

        return item is null ? null : Map(item);
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
        return persisted.Append(entity)
            .OrderByDescending(item => item.IsPrimary).ThenBy(item => item.StartDate)
            .Select(Map)
            .ToArray();
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
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileEmploymentAssignment>()
            .SingleOrDefaultAsync(x => x.PublicId == employmentAssignmentPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Update(assignmentTypeCode, contractTypeCode, workdayCode, payrollTypeCode, positionSlotPublicId, orgUnitPublicId, workCenterPublicId, costCenterPublicId, startDate, endDate, isPrimary, notes);
        return Map(item);
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
        bool isActive,
        bool isActiveMutated,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileEmploymentAssignment>()
            .SingleOrDefaultAsync(x => x.PublicId == employmentAssignmentPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Update(assignmentTypeCode, contractTypeCode, workdayCode, payrollTypeCode, positionSlotPublicId, orgUnitPublicId, workCenterPublicId, costCenterPublicId, startDate, endDate, isPrimary, notes);
        if (isActiveMutated)
        {
            item.SetActive(isActive);
        }

        return Map(item);
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
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileEmploymentAssignment>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => item.StartDate)
            .Select(item => Map(item))
            .ToArrayAsync(cancellationToken);

    public async Task<PersonnelFileEmploymentAssignmentResponse?> GetEmploymentAssignmentAsync(
        Guid personnelFileId,
        Guid employmentAssignmentPublicId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileEmploymentAssignment>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.PersonnelFile.PublicId == personnelFileId && x.PublicId == employmentAssignmentPublicId, cancellationToken);
        return item is null ? null : Map(item);
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
        var all = await dbContext.Set<PersonnelFileContractHistory>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId)
            .OrderByDescending(item => item.ContractDate)
            .Select(item => Map(item)).ToArrayAsync(cancellationToken);
        return all;
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
        var all = await dbContext.Set<PersonnelFileCompensationConcept>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId)
            .OrderByDescending(item => item.IsActive).ThenBy(item => item.StartDate)
            .Select(item => Map(item)).ToArrayAsync(cancellationToken);
        return all;
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
        var all = await dbContext.Set<PersonnelFileAdditionalBenefit>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId)
            .OrderByDescending(item => item.IsActive).ThenBy(item => item.BenefitTypeCode)
            .Select(item => Map(item)).ToArrayAsync(cancellationToken);
        return all;
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

    public async Task<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>> AddPaymentMethodAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFilePaymentMethod entity,
        CancellationToken cancellationToken)
    {
        dbContext.Set<PersonnelFilePaymentMethod>().Add(entity);
        var all = await dbContext.Set<PersonnelFilePaymentMethod>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId)
            .OrderByDescending(item => item.IsPrimary).ThenBy(item => item.EffectiveFromUtc)
            .Select(item => Map(item)).ToArrayAsync(cancellationToken);
        return all;
    }

    public async Task<PersonnelFilePaymentMethodResponse?> UpdatePaymentMethodAsync(
        Guid paymentMethodPublicId,
        Guid tenantId,
        string paymentMethodCode,
        Guid? bankAccountPublicId,
        bool isPrimary,
        DateTime effectiveFromUtc,
        DateTime? effectiveToUtc,
        string? notes,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFilePaymentMethod>()
            .SingleOrDefaultAsync(x => x.PublicId == paymentMethodPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Update(paymentMethodCode, bankAccountPublicId, isPrimary, effectiveFromUtc, effectiveToUtc, notes);
        return Map(item);
    }

    public async Task<PersonnelFilePaymentMethodResponse?> PatchPaymentMethodAsync(
        Guid paymentMethodPublicId,
        Guid tenantId,
        string paymentMethodCode,
        Guid? bankAccountPublicId,
        bool isPrimary,
        DateTime effectiveFromUtc,
        DateTime? effectiveToUtc,
        string? notes,
        bool isActive,
        bool isActiveMutated,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFilePaymentMethod>()
            .SingleOrDefaultAsync(x => x.PublicId == paymentMethodPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Update(paymentMethodCode, bankAccountPublicId, isPrimary, effectiveFromUtc, effectiveToUtc, notes);
        if (isActiveMutated)
        {
            item.SetActive(isActive);
        }

        return Map(item);
    }

    public async Task<bool> DeletePaymentMethodAsync(
        Guid paymentMethodPublicId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFilePaymentMethod>()
            .SingleOrDefaultAsync(x => x.PublicId == paymentMethodPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return false;
        dbContext.Set<PersonnelFilePaymentMethod>().Remove(item);
        return true;
    }

    public async Task<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>> GetPaymentMethodsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFilePaymentMethod>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => item.EffectiveFromUtc)
            .Select(item => Map(item))
            .ToArrayAsync(cancellationToken);

    public async Task<PersonnelFilePaymentMethodResponse?> GetPaymentMethodAsync(
        Guid personnelFileId,
        Guid paymentMethodPublicId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFilePaymentMethod>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.PersonnelFile.PublicId == personnelFileId && x.PublicId == paymentMethodPublicId, cancellationToken);
        return item is null ? null : Map(item);
    }

    public async Task<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>> AddAuthorizationSubstitutionAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileAuthorizationSubstitution entity,
        CancellationToken cancellationToken)
    {
        dbContext.Set<PersonnelFileAuthorizationSubstitution>().Add(entity);
        var all = await dbContext.Set<PersonnelFileAuthorizationSubstitution>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId)
            .OrderByDescending(item => item.IsActive).ThenBy(item => item.StartDate)
            .Select(item => Map(item)).ToArrayAsync(cancellationToken);
        return all;
    }

    public async Task<PersonnelFileAuthorizationSubstitutionResponse?> UpdateAuthorizationSubstitutionAsync(
        Guid authorizationSubstitutionPublicId,
        Guid tenantId,
        string substitutionTypeCode,
        Guid substitutePersonnelFilePublicId,
        string? substitutePositionTitle,
        DateTime startDate,
        DateTime? endDate,
        string? notes,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileAuthorizationSubstitution>()
            .SingleOrDefaultAsync(x => x.PublicId == authorizationSubstitutionPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Update(substitutionTypeCode, substitutePersonnelFilePublicId, substitutePositionTitle, startDate, endDate, notes);
        return Map(item);
    }

    public async Task<PersonnelFileAuthorizationSubstitutionResponse?> PatchAuthorizationSubstitutionAsync(
        Guid authorizationSubstitutionPublicId,
        Guid tenantId,
        string substitutionTypeCode,
        Guid substitutePersonnelFilePublicId,
        string? substitutePositionTitle,
        DateTime startDate,
        DateTime? endDate,
        string? notes,
        bool isActive,
        bool isActiveMutated,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileAuthorizationSubstitution>()
            .SingleOrDefaultAsync(x => x.PublicId == authorizationSubstitutionPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Update(substitutionTypeCode, substitutePersonnelFilePublicId, substitutePositionTitle, startDate, endDate, notes);
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
        var all = await dbContext.Set<PersonnelFileAssetAccess>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId)
            .OrderByDescending(item => item.IsActive).ThenBy(item => item.StartDateUtc)
            .Select(item => Map(item)).ToArrayAsync(cancellationToken);
        return all;
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
        var all = await dbContext.Set<PersonnelFileInsurance>()
            .AsNoTracking()
            .Include(item => item.Beneficiaries)
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId)
            .OrderByDescending(item => item.IsActive).ThenBy(item => item.InsuranceCode)
            .ToListAsync(cancellationToken);
        return all.Select(Map).ToArray();
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

        var entity = PersonnelFileInsuranceBeneficiary.Create(item.FullName, item.DocumentNumber, item.BirthDate, item.KinshipCode);
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
        item.Update(fullName, documentNumber, birthDate, kinshipCode);
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
        item.Update(fullName, documentNumber, birthDate, kinshipCode);
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
        var all = await dbContext.Set<PersonnelFileMedicalClaim>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId)
            .OrderByDescending(item => item.ClaimDateUtc)
            .Select(item => Map(item)).ToArrayAsync(cancellationToken);
        return all;
    }

    public async Task<PersonnelFileMedicalClaimResponse?> UpdateMedicalClaimAsync(
        Guid medicalClaimPublicId,
        Guid tenantId,
        Guid? insurancePublicId,
        string? accountNumber,
        string claimTypeCode,
        string? diagnosis,
        decimal? claimAmount,
        string? currencyCode,
        decimal? paidAmount,
        int? responseTimeDays,
        string? notes,
        DateTime claimDateUtc,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileMedicalClaim>()
            .SingleOrDefaultAsync(x => x.PublicId == medicalClaimPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Update(insurancePublicId, accountNumber, claimTypeCode, diagnosis, claimAmount, currencyCode, paidAmount, responseTimeDays, notes, claimDateUtc, sourceSystem, sourceReference, sourceSyncedUtc);
        return Map(item);
    }

    public async Task<PersonnelFileMedicalClaimResponse?> PatchMedicalClaimAsync(
        Guid medicalClaimPublicId,
        Guid tenantId,
        Guid? insurancePublicId,
        string? accountNumber,
        string claimTypeCode,
        string? diagnosis,
        decimal? claimAmount,
        string? currencyCode,
        decimal? paidAmount,
        int? responseTimeDays,
        string? notes,
        DateTime claimDateUtc,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc,
        bool isActive,
        bool isActiveMutated,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileMedicalClaim>()
            .SingleOrDefaultAsync(x => x.PublicId == medicalClaimPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Update(insurancePublicId, accountNumber, claimTypeCode, diagnosis, claimAmount, currencyCode, paidAmount, responseTimeDays, notes, claimDateUtc, sourceSystem, sourceReference, sourceSyncedUtc);
        if (isActiveMutated)
        {
            item.SetActive(isActive);
        }

        return Map(item);
    }

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

    public async Task<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>> AddPerformanceEvaluationAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFilePerformanceEvaluation entity,
        CancellationToken cancellationToken)
    {
        dbContext.Set<PersonnelFilePerformanceEvaluation>().Add(entity);
        var all = await dbContext.Set<PersonnelFilePerformanceEvaluation>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId)
            .OrderByDescending(item => item.EvaluationDateUtc)
            .Select(item => Map(item)).ToArrayAsync(cancellationToken);
        return all;
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

    public async Task<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>> AddPositionCompetencyResultAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFilePositionCompetencyResult entity,
        CancellationToken cancellationToken)
    {
        dbContext.Set<PersonnelFilePositionCompetencyResult>().Add(entity);
        var all = await dbContext.Set<PersonnelFilePositionCompetencyResult>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId)
            .OrderBy(item => item.CompetencyCode)
            .Select(item => Map(item)).ToArrayAsync(cancellationToken);
        return all;
    }

    public async Task<PersonnelFilePositionCompetencyResultResponse?> UpdatePositionCompetencyResultAsync(
        Guid itemPublicId,
        Guid tenantId,
        string competencyCode,
        string? desiredBehaviors,
        decimal? expectedScore,
        decimal? achievedScore,
        decimal? gapScore,
        DateTime? evaluationDateUtc,
        string? sourceSystem,
        string? sourceReference,
        DateTime? sourceSyncedUtc,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFilePositionCompetencyResult>()
            .SingleOrDefaultAsync(x => x.PublicId == itemPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Update(competencyCode, desiredBehaviors, expectedScore, achievedScore, gapScore, evaluationDateUtc, sourceSystem, sourceReference, sourceSyncedUtc);
        return Map(item);
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
        await dbContext.Set<PersonnelFilePositionCompetencyResult>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderBy(item => item.CompetencyCode)
            .Select(item => Map(item))
            .ToArrayAsync(cancellationToken);

    public async Task<PersonnelFilePositionCompetencyResultResponse?> GetPositionCompetencyResultAsync(
        Guid personnelFileId,
        Guid positionCompetencyResultPublicId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFilePositionCompetencyResult>()
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.PersonnelFile.PublicId == personnelFileId && x.PublicId == positionCompetencyResultPublicId, cancellationToken);
        return item is null ? null : Map(item);
    }

    public async Task<IReadOnlyCollection<PersonnelFileSelectionContestResponse>> AddSelectionContestAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileSelectionContest entity,
        CancellationToken cancellationToken)
    {
        dbContext.Set<PersonnelFileSelectionContest>().Add(entity);
        var all = await dbContext.Set<PersonnelFileSelectionContest>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId)
            .OrderByDescending(item => item.ContestDateUtc)
            .Select(item => Map(item)).ToArrayAsync(cancellationToken);
        return all;
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
        var all = await dbContext.Set<PersonnelFileCurricularCompetency>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId)
            .OrderBy(item => item.RequirementTypeCode).ThenBy(item => item.RequirementName)
            .Select(item => Map(item)).ToArrayAsync(cancellationToken);
        return all;
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
            EmployeeSeniority.None,
            item.RetirementCategoryCode,
            item.RetirementReasonCode,
            item.RetirementNotes,
            item.RetirementDate,
            VacationDaysAvailable: null,
            DisabilityDaysAvailable: null,
            item.ConcurrencyToken,
            item.CreatedUtc,
            item.ModifiedUtc);

    private static PersonnelFileEmploymentAssignmentResponse Map(PersonnelFileEmploymentAssignment item) =>
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
            item.ConcurrencyToken);

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

    private static PersonnelFilePaymentMethodResponse Map(PersonnelFilePaymentMethod item) =>
        new(
            item.PublicId,
            item.PaymentMethodCode,
            item.BankAccountPublicId,
            item.IsPrimary,
            item.IsActive,
            item.EffectiveFromUtc,
            item.EffectiveToUtc,
            item.Notes,
            item.ConcurrencyToken);

    private static PersonnelFileAuthorizationSubstitutionResponse Map(PersonnelFileAuthorizationSubstitution item) =>
        new(
            item.PublicId,
            item.SubstitutionTypeCode,
            item.SubstitutePersonnelFilePublicId,
            item.SubstitutePositionTitle,
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
                    beneficiary.BirthDate,
                    beneficiary.KinshipCode,
                    beneficiary.IsActive,
                    beneficiary.ConcurrencyToken))
                .ToArray(),
            item.ConcurrencyToken);

    private static PersonnelFileInsuranceBeneficiaryResponse Map(PersonnelFileInsuranceBeneficiary item) =>
        new(
            item.PublicId,
            item.FullName,
            item.DocumentNumber,
            item.BirthDate,
            item.KinshipCode,
            item.IsActive,
            item.ConcurrencyToken);

    private static PersonnelFileMedicalClaimResponse Map(PersonnelFileMedicalClaim item) =>
        new(
            item.PublicId,
            item.InsurancePublicId,
            item.AccountNumber,
            item.ClaimTypeCode,
            item.Diagnosis,
            item.ClaimAmount,
            item.CurrencyCode,
            item.PaidAmount,
            item.ResponseTimeDays,
            item.Notes,
            item.ClaimDateUtc,
            item.SourceSystem,
            item.SourceReference,
            item.SourceSyncedUtc,
            item.IsActive,
            item.ConcurrencyToken);

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

    private static PersonnelFilePositionCompetencyResultResponse Map(PersonnelFilePositionCompetencyResult item) =>
        new(
            item.PublicId,
            item.CompetencyCode,
            item.DesiredBehaviors,
            item.ExpectedScore,
            item.AchievedScore,
            item.GapScore,
            item.EvaluationDateUtc,
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
}

using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PersonnelFiles;
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
            profile.IsEmploymentActive,
            profile.ContractTypeCode,
            profile.HireDate,
            profile.RetirementCategoryCode,
            profile.RetirementReasonCode,
            profile.RetirementNotes,
            profile.RetirementDate,
            profile.WorkdayCode,
            profile.PayrollTypeCode,
            profile.PositionSlotPublicId,
            profile.JobProfilePublicId,
            profile.OrgUnitPublicId,
            profile.WorkCenterPublicId,
            profile.CostCenterPublicId,
            profile.ContractStartDate,
            profile.ContractEndDate,
            profile.VacationConfigurationJson);

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

    public Task<IReadOnlyCollection<PersonnelFileEmploymentAssignmentResponse>> ReplaceEmploymentAssignmentsAsync(
        long personnelFileInternalId,
        Guid tenantId,
        IReadOnlyCollection<PersonnelFileEmploymentAssignment> entities,
        CancellationToken cancellationToken) =>
        ReplaceSectionAsync(
            personnelFileInternalId,
            tenantId,
            entities,
            orderBy: items => items.OrderByDescending(item => item.IsPrimary).ThenBy(item => item.StartDate),
            map: Map,
            cancellationToken);

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

    public Task<IReadOnlyCollection<PersonnelFileContractHistoryResponse>> ReplaceContractHistoryAsync(
        long personnelFileInternalId,
        Guid tenantId,
        IReadOnlyCollection<PersonnelFileContractHistory> entities,
        CancellationToken cancellationToken) =>
        ReplaceSectionAsync(
            personnelFileInternalId,
            tenantId,
            entities,
            orderBy: items => items.OrderByDescending(item => item.ContractDate),
            map: Map,
            cancellationToken);

    public async Task<IReadOnlyCollection<PersonnelFileContractHistoryResponse>> GetContractHistoryAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileContractHistory>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderByDescending(item => item.ContractDate)
            .Select(item => Map(item))
            .ToArrayAsync(cancellationToken);

    public async Task<PersonnelFilePositionHierarchyResponse> GetPositionHierarchyAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken)
    {
        var file = await dbContext.Set<PersonnelFile>()
            .AsNoTracking()
            .SingleAsync(item => item.PublicId == personnelFileId, cancellationToken);

        var profileOrgUnit = await dbContext.Set<PersonnelFileEmployeeProfile>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .Select(item => item.OrgUnitPublicId)
            .SingleOrDefaultAsync(cancellationToken);

        var resolvedOrgUnit = profileOrgUnit ?? file.OrgUnitPublicId;

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

    public async Task<IReadOnlyCollection<PersonnelFileSalaryItemResponse>> AddSalaryItemAsync(
        long personnelFileInternalId,
        Guid tenantId,
        PersonnelFileSalaryItem entity,
        CancellationToken cancellationToken)
    {
        dbContext.Set<PersonnelFileSalaryItem>().Add(entity);
        var all = await dbContext.Set<PersonnelFileSalaryItem>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId)
            .OrderByDescending(item => item.IsActive).ThenBy(item => item.StartDate)
            .Select(item => Map(item)).ToArrayAsync(cancellationToken);
        return all;
    }

    public async Task<PersonnelFileSalaryItemResponse?> UpdateSalaryItemAsync(
        Guid itemPublicId,
        Guid tenantId,
        string incomeTypeCode,
        string salaryRubricCode,
        string currencyCode,
        string payPeriodCode,
        decimal amount,
        DateTime startDate,
        DateTime? endDate,
        bool isActive,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileSalaryItem>()
            .SingleOrDefaultAsync(x => x.PublicId == itemPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return null;
        item.Update(incomeTypeCode, salaryRubricCode, currencyCode, payPeriodCode, amount, startDate, endDate, isActive);
        return Map(item);
    }

    public async Task<bool> DeactivateSalaryItemAsync(
        Guid itemPublicId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileSalaryItem>()
            .SingleOrDefaultAsync(x => x.PublicId == itemPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return false;
        item.Deactivate();
        return true;
    }

    public async Task<IReadOnlyCollection<PersonnelFileSalaryItemResponse>> GetSalaryItemsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileSalaryItem>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderByDescending(item => item.IsActive)
            .ThenBy(item => item.StartDate)
            .Select(item => Map(item))
            .ToArrayAsync(cancellationToken);

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

    public async Task<bool> UpdateAdditionalBenefitAsync(
        Guid itemPublicId,
        Guid tenantId,
        string benefitTypeCode,
        DateTime? startDate,
        DateTime? endDate,
        bool isActive,
        string? notes,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileAdditionalBenefit>()
            .SingleOrDefaultAsync(x => x.PublicId == itemPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return false;
        item.Update(benefitTypeCode, startDate, endDate, isActive, notes);
        return true;
    }

    public async Task<bool> DeactivateAdditionalBenefitAsync(
        Guid itemPublicId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileAdditionalBenefit>()
            .SingleOrDefaultAsync(x => x.PublicId == itemPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return false;
        item.Deactivate();
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

    public async Task<bool> UpdatePaymentMethodAsync(
        Guid itemPublicId,
        Guid tenantId,
        string paymentMethodCode,
        Guid? bankAccountPublicId,
        bool isPrimary,
        bool isActive,
        DateTime effectiveFromUtc,
        DateTime? effectiveToUtc,
        string? notes,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFilePaymentMethod>()
            .SingleOrDefaultAsync(x => x.PublicId == itemPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return false;
        item.Update(paymentMethodCode, bankAccountPublicId, isPrimary, isActive, effectiveFromUtc, effectiveToUtc, notes);
        return true;
    }

    public async Task<bool> DeactivatePaymentMethodAsync(
        Guid itemPublicId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFilePaymentMethod>()
            .SingleOrDefaultAsync(x => x.PublicId == itemPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return false;
        item.Deactivate();
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

    public Task<IReadOnlyCollection<PersonnelFileAuthorizationSubstitutionResponse>> ReplaceAuthorizationSubstitutionsAsync(
        long personnelFileInternalId,
        Guid tenantId,
        IReadOnlyCollection<PersonnelFileAuthorizationSubstitution> entities,
        CancellationToken cancellationToken) =>
        ReplaceSectionAsync(
            personnelFileInternalId,
            tenantId,
            entities,
            orderBy: items => items.OrderByDescending(item => item.IsActive).ThenBy(item => item.StartDate),
            map: Map,
            cancellationToken);

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

    public Task<PersonnelFilePersonnelActionResponse> AddPersonnelActionAsync(
        PersonnelFilePersonnelAction entity,
        CancellationToken cancellationToken)
    {
        dbContext.Set<PersonnelFilePersonnelAction>().Add(entity);
        return Task.FromResult(Map(entity));
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

    public async Task<bool> DeactivatePayrollTransactionAsync(
        Guid itemPublicId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFilePayrollTransaction>()
            .SingleOrDefaultAsync(x => x.PublicId == itemPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return false;
        item.Deactivate();
        return true;
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

    public Task<IReadOnlyCollection<PersonnelFileAssetAccessResponse>> ReplaceAssetsAccessesAsync(
        long personnelFileInternalId,
        Guid tenantId,
        IReadOnlyCollection<PersonnelFileAssetAccess> entities,
        CancellationToken cancellationToken) =>
        ReplaceSectionAsync(
            personnelFileInternalId,
            tenantId,
            entities,
            orderBy: items => items.OrderByDescending(item => item.IsActive).ThenBy(item => item.StartDateUtc),
            map: Map,
            cancellationToken);

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

    public async Task<bool> UpdateInsuranceAsync(
        Guid itemPublicId,
        Guid tenantId,
        string insuranceCode,
        decimal? employeeContribution,
        decimal? employerContribution,
        string? rangeCode,
        string? policyNumber,
        decimal? insuredAmount,
        string? currencyCode,
        bool isActive,
        DateTime? startDateUtc,
        DateTime? endDateUtc,
        IReadOnlyCollection<InsuranceBeneficiaryInput> beneficiaries,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileInsurance>()
            .Include(x => x.Beneficiaries)
            .SingleOrDefaultAsync(x => x.PublicId == itemPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return false;
        item.Update(insuranceCode, employeeContribution, employerContribution, rangeCode, policyNumber, insuredAmount, currencyCode, isActive, startDateUtc, endDateUtc);
        // Replace beneficiaries
        dbContext.Set<PersonnelFileInsuranceBeneficiary>().RemoveRange(item.Beneficiaries);
        var newBeneficiaries = beneficiaries.Select(b =>
        {
            var e = PersonnelFileInsuranceBeneficiary.Create(b.FullName, b.DocumentNumber, b.BirthDate, b.KinshipCode);
            e.SetTenantId(tenantId);
            e.BindToInsurance(item.Id);
            return e;
        }).ToArray();
        dbContext.Set<PersonnelFileInsuranceBeneficiary>().AddRange(newBeneficiaries);
        return true;
    }

    public async Task<bool> DeactivateInsuranceAsync(
        Guid itemPublicId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileInsurance>()
            .SingleOrDefaultAsync(x => x.PublicId == itemPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return false;
        item.Deactivate();
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

    public async Task<bool> UpdateMedicalClaimAsync(
        Guid itemPublicId,
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
            .SingleOrDefaultAsync(x => x.PublicId == itemPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return false;
        item.Update(insurancePublicId, accountNumber, claimTypeCode, diagnosis, claimAmount, currencyCode, paidAmount, responseTimeDays, notes, claimDateUtc, sourceSystem, sourceReference, sourceSyncedUtc);
        return true;
    }

    public async Task<bool> DeactivateMedicalClaimAsync(
        Guid itemPublicId,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Set<PersonnelFileMedicalClaim>()
            .SingleOrDefaultAsync(x => x.PublicId == itemPublicId && x.TenantId == tenantId, cancellationToken);
        if (item is null) return false;
        item.Deactivate();
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

    public async Task<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>> GetPerformanceEvaluationsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFilePerformanceEvaluation>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderByDescending(item => item.EvaluationDateUtc)
            .Select(item => Map(item))
            .ToArrayAsync(cancellationToken);

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

    public async Task<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>> GetPositionCompetencyResultsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFilePositionCompetencyResult>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderBy(item => item.CompetencyCode)
            .Select(item => Map(item))
            .ToArrayAsync(cancellationToken);

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

    public async Task<IReadOnlyCollection<PersonnelFileSelectionContestResponse>> GetSelectionContestsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileSelectionContest>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderByDescending(item => item.ContestDateUtc)
            .Select(item => Map(item))
            .ToArrayAsync(cancellationToken);

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

    private static PersonnelFileEmployeeProfileResponse Map(PersonnelFileEmployeeProfile item) =>
        new(
            item.PublicId,
            item.EmployeeCode,
            item.EmploymentStatusCode,
            item.IsEmploymentActive,
            item.ContractTypeCode,
            item.HireDate,
            item.RetirementCategoryCode,
            item.RetirementReasonCode,
            item.RetirementNotes,
            item.RetirementDate,
            item.WorkdayCode,
            item.PayrollTypeCode,
            item.PositionSlotPublicId,
            item.JobProfilePublicId,
            item.OrgUnitPublicId,
            item.WorkCenterPublicId,
            item.CostCenterPublicId,
            item.ContractStartDate,
            item.ContractEndDate,
            item.VacationConfigurationJson,
            item.ConcurrencyToken,
            item.CreatedUtc,
            item.ModifiedUtc);

    private static PersonnelFileEmploymentAssignmentResponse Map(PersonnelFileEmploymentAssignment item) =>
        new(
            item.PublicId,
            item.AssignmentTypeCode,
            item.PositionSlotPublicId,
            item.OrgUnitPublicId,
            item.WorkCenterPublicId,
            item.CostCenterPublicId,
            item.StartDate,
            item.EndDate,
            item.IsPrimary,
            item.IsActive,
            item.Notes);

    private static PersonnelFileContractHistoryResponse Map(PersonnelFileContractHistory item) =>
        new(
            item.PublicId,
            item.ContractTypeCode,
            item.ContractDate,
            item.ContractEndDate,
            item.PositionSlotPublicId,
            item.Notes);

    private static PersonnelFileSalaryItemResponse Map(PersonnelFileSalaryItem item) =>
        new(
            item.PublicId,
            item.IncomeTypeCode,
            item.SalaryRubricCode,
            item.CurrencyCode,
            item.PayPeriodCode,
            item.Amount,
            item.StartDate,
            item.EndDate,
            item.IsActive);

    private static PersonnelFileAdditionalBenefitResponse Map(PersonnelFileAdditionalBenefit item) =>
        new(
            item.PublicId,
            item.BenefitTypeCode,
            item.StartDate,
            item.EndDate,
            item.IsActive,
            item.Notes);

    private static PersonnelFilePaymentMethodResponse Map(PersonnelFilePaymentMethod item) =>
        new(
            item.PublicId,
            item.PaymentMethodCode,
            item.BankAccountPublicId,
            item.IsPrimary,
            item.IsActive,
            item.EffectiveFromUtc,
            item.EffectiveToUtc,
            item.Notes);

    private static PersonnelFileAuthorizationSubstitutionResponse Map(PersonnelFileAuthorizationSubstitution item) =>
        new(
            item.PublicId,
            item.SubstitutionTypeCode,
            item.SubstitutePersonnelFilePublicId,
            item.SubstitutePositionTitle,
            item.StartDate,
            item.EndDate,
            item.IsActive,
            item.Notes);

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
            item.ModifiedUtc);

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
            item.ModifiedUtc);

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
            item.Notes);

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
                .OrderBy(beneficiary => beneficiary.FullName)
                .Select(beneficiary => new PersonnelFileInsuranceBeneficiaryResponse(
                    beneficiary.PublicId,
                    beneficiary.FullName,
                    beneficiary.DocumentNumber,
                    beneficiary.BirthDate,
                    beneficiary.KinshipCode,
                    beneficiary.IsActive))
                .ToArray());

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
            item.SourceSyncedUtc);

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
            item.SourceSyncedUtc);

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
            item.SourceSyncedUtc);

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
            item.SourceSyncedUtc);

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
            item.SourceSyncedUtc);
}

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

    public Task<IReadOnlyCollection<PersonnelFileSalaryItemResponse>> ReplaceSalaryItemsAsync(
        long personnelFileInternalId,
        Guid tenantId,
        IReadOnlyCollection<PersonnelFileSalaryItem> entities,
        CancellationToken cancellationToken) =>
        ReplaceSectionAsync(
            personnelFileInternalId,
            tenantId,
            entities,
            orderBy: items => items.OrderByDescending(item => item.IsActive).ThenBy(item => item.StartDate),
            map: Map,
            cancellationToken);

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

    public Task<IReadOnlyCollection<PersonnelFileAdditionalBenefitResponse>> ReplaceAdditionalBenefitsAsync(
        long personnelFileInternalId,
        Guid tenantId,
        IReadOnlyCollection<PersonnelFileAdditionalBenefit> entities,
        CancellationToken cancellationToken) =>
        ReplaceSectionAsync(
            personnelFileInternalId,
            tenantId,
            entities,
            orderBy: items => items.OrderByDescending(item => item.IsActive).ThenBy(item => item.BenefitTypeCode),
            map: Map,
            cancellationToken);

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

    public Task<IReadOnlyCollection<PersonnelFilePaymentMethodResponse>> ReplacePaymentMethodsAsync(
        long personnelFileInternalId,
        Guid tenantId,
        IReadOnlyCollection<PersonnelFilePaymentMethod> entities,
        CancellationToken cancellationToken) =>
        ReplaceSectionAsync(
            personnelFileInternalId,
            tenantId,
            entities,
            orderBy: items => items.OrderByDescending(item => item.IsPrimary).ThenBy(item => item.EffectiveFromUtc),
            map: Map,
            cancellationToken);

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
        CancellationToken cancellationToken)
    {
        var query = BuildPersonnelActionsBaseQuery(personnelFileId);
        query = ApplyPersonnelActionFilters(query, fromUtc, toUtc, type, status, search);

        var items = await ApplyPersonnelActionSorting(query, sortBy, sortDirection)
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

    public Task<IReadOnlyCollection<PersonnelFilePayrollTransactionResponse>> ReplacePayrollTransactionsAsync(
        long personnelFileInternalId,
        Guid tenantId,
        IReadOnlyCollection<PersonnelFilePayrollTransaction> entities,
        CancellationToken cancellationToken) =>
        ReplaceSectionAsync(
            personnelFileInternalId,
            tenantId,
            entities,
            orderBy: items => items.OrderByDescending(item => item.TransactionDateUtc),
            map: Map,
            cancellationToken);

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
        CancellationToken cancellationToken)
    {
        var query = BuildPayrollTransactionsBaseQuery(personnelFileId);
        query = ApplyPayrollTransactionFilters(query, fromUtc, toUtc, type, status, search);

        var items = await ApplyPayrollTransactionSorting(query, sortBy, sortDirection)
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

    public async Task<IReadOnlyCollection<PersonnelFileInsuranceResponse>> ReplaceInsurancesAsync(
        long personnelFileInternalId,
        Guid tenantId,
        IReadOnlyCollection<PersonnelFileInsurance> entities,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.Set<PersonnelFileInsurance>()
            .Include(item => item.Beneficiaries)
            .Where(item => item.TenantId == tenantId && item.PersonnelFileId == personnelFileInternalId)
            .ToListAsync(cancellationToken);

        dbContext.Set<PersonnelFileInsurance>().RemoveRange(existing);
        dbContext.Set<PersonnelFileInsurance>().AddRange(entities);

        return entities
            .OrderByDescending(item => item.IsActive)
            .ThenBy(item => item.InsuranceCode)
            .Select(Map)
            .ToArray();
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

    public Task<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>> ReplaceMedicalClaimsAsync(
        long personnelFileInternalId,
        Guid tenantId,
        IReadOnlyCollection<PersonnelFileMedicalClaim> entities,
        CancellationToken cancellationToken) =>
        ReplaceSectionAsync(
            personnelFileInternalId,
            tenantId,
            entities,
            orderBy: items => items.OrderByDescending(item => item.ClaimDateUtc),
            map: Map,
            cancellationToken);

    public async Task<IReadOnlyCollection<PersonnelFileMedicalClaimResponse>> GetMedicalClaimsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileMedicalClaim>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderByDescending(item => item.ClaimDateUtc)
            .Select(item => Map(item))
            .ToArrayAsync(cancellationToken);

    public Task<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>> ReplacePerformanceEvaluationsAsync(
        long personnelFileInternalId,
        Guid tenantId,
        IReadOnlyCollection<PersonnelFilePerformanceEvaluation> entities,
        CancellationToken cancellationToken) =>
        ReplaceSectionAsync(
            personnelFileInternalId,
            tenantId,
            entities,
            orderBy: items => items.OrderByDescending(item => item.EvaluationDateUtc),
            map: Map,
            cancellationToken);

    public async Task<IReadOnlyCollection<PersonnelFilePerformanceEvaluationResponse>> GetPerformanceEvaluationsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFilePerformanceEvaluation>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderByDescending(item => item.EvaluationDateUtc)
            .Select(item => Map(item))
            .ToArrayAsync(cancellationToken);

    public Task<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>> ReplacePositionCompetencyResultsAsync(
        long personnelFileInternalId,
        Guid tenantId,
        IReadOnlyCollection<PersonnelFilePositionCompetencyResult> entities,
        CancellationToken cancellationToken) =>
        ReplaceSectionAsync(
            personnelFileInternalId,
            tenantId,
            entities,
            orderBy: items => items.OrderBy(item => item.CompetencyCode),
            map: Map,
            cancellationToken);

    public async Task<IReadOnlyCollection<PersonnelFilePositionCompetencyResultResponse>> GetPositionCompetencyResultsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFilePositionCompetencyResult>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderBy(item => item.CompetencyCode)
            .Select(item => Map(item))
            .ToArrayAsync(cancellationToken);

    public Task<IReadOnlyCollection<PersonnelFileSelectionContestResponse>> ReplaceSelectionContestsAsync(
        long personnelFileInternalId,
        Guid tenantId,
        IReadOnlyCollection<PersonnelFileSelectionContest> entities,
        CancellationToken cancellationToken) =>
        ReplaceSectionAsync(
            personnelFileInternalId,
            tenantId,
            entities,
            orderBy: items => items.OrderByDescending(item => item.ContestDateUtc),
            map: Map,
            cancellationToken);

    public async Task<IReadOnlyCollection<PersonnelFileSelectionContestResponse>> GetSelectionContestsAsync(
        Guid personnelFileId,
        CancellationToken cancellationToken) =>
        await dbContext.Set<PersonnelFileSelectionContest>()
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFileId)
            .OrderByDescending(item => item.ContestDateUtc)
            .Select(item => Map(item))
            .ToArrayAsync(cancellationToken);

    public Task<IReadOnlyCollection<PersonnelFileCurricularCompetencyResponse>> ReplaceCurricularCompetenciesAsync(
        long personnelFileInternalId,
        Guid tenantId,
        IReadOnlyCollection<PersonnelFileCurricularCompetency> entities,
        CancellationToken cancellationToken) =>
        ReplaceSectionAsync(
            personnelFileInternalId,
            tenantId,
            entities,
            orderBy: items => items.OrderBy(item => item.RequirementTypeCode).ThenBy(item => item.RequirementName),
            map: Map,
            cancellationToken);

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

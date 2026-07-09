using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.PersonnelTransactions;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.EmployeeRelations;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.PersonnelFiles;

/// <summary>
/// EF-backed persistence port of the "otras transacciones de personal" module (REQ-003 PR-2). This PR wires
/// the tracked-entity loaders (PR-3/PR-4), the APLICADA suspension-overlap query (RN-18) and the advisory lock
/// that serializes the apply-with-suspension race per (tenant, employee) (aclaración №3). The company bandeja /
/// exports / payroll-input / time-availability queries land in PR-5/PR-6.
/// </summary>
internal sealed class PersonnelTransactionRepository(ApplicationDbContext dbContext) : IPersonnelTransactionRepository
{
    // RA-1: a fixed class id namespaces this advisory lock against any other advisory-lock use; the object id
    // is derived deterministically from (tenant, personnel file) so every apply-with-suspension of one
    // employee contends on the same lock. Executed on the context's current transaction (the handler opens
    // one), pg_advisory_xact_lock holds until that transaction commits/rolls back.
    private const int EmployeeRelationsLockClassId = 0x45_52_4C_4B; // "ERLK" — employee-relations lock

    public Task<PersonnelFileRecognition?> GetRecognitionEntityAsync(
        Guid personnelFilePublicId, Guid recognitionPublicId, CancellationToken cancellationToken) =>
        dbContext.PersonnelFileRecognitions
            .SingleOrDefaultAsync(
                item => item.PersonnelFile.PublicId == personnelFilePublicId && item.PublicId == recognitionPublicId,
                cancellationToken);

    public Task<long?> GetRecognitionInternalIdAsync(
        Guid personnelFilePublicId, Guid recognitionPublicId, CancellationToken cancellationToken) =>
        dbContext.PersonnelFileRecognitions
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFilePublicId && item.PublicId == recognitionPublicId)
            .Select(item => (long?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public void AddRecognition(PersonnelFileRecognition entity) =>
        dbContext.PersonnelFileRecognitions.Add(entity);

    public void AddRecognitionDocument(PersonnelFileRecognitionDocument entity) =>
        dbContext.PersonnelFileRecognitionDocuments.Add(entity);

    public async Task<IReadOnlyCollection<PersonnelFileRecognitionResponse>> GetRecognitionResponsesAsync(
        Guid personnelFilePublicId, bool onlyApplied, CancellationToken cancellationToken)
    {
        var items = await dbContext.PersonnelFileRecognitions
            .AsNoTracking()
            .Include(item => item.RecognitionType)
            .Where(item => item.PersonnelFile.PublicId == personnelFilePublicId
                && (!onlyApplied || item.StatusCode == PersonnelTransactionStatuses.Aplicada))
            .OrderByDescending(item => item.EventDate)
            .ThenByDescending(item => item.CreatedUtc)
            .ToListAsync(cancellationToken);
        return items.Select(MapRecognition).ToArray();
    }

    public async Task<PersonnelFileRecognitionResponse?> GetRecognitionResponseAsync(
        Guid personnelFilePublicId, Guid recognitionPublicId, CancellationToken cancellationToken)
    {
        var item = await dbContext.PersonnelFileRecognitions
            .AsNoTracking()
            .Include(item => item.RecognitionType)
            .SingleOrDefaultAsync(
                item => item.PersonnelFile.PublicId == personnelFilePublicId && item.PublicId == recognitionPublicId,
                cancellationToken);
        return item is null ? null : MapRecognition(item);
    }

    public Task<RecognitionTypeRef?> ResolveActiveRecognitionTypeAsync(
        Guid tenantId, Guid recognitionTypePublicId, CancellationToken cancellationToken) =>
        dbContext.Set<RecognitionType>()
            .AsNoTracking()
            .Where(type => type.TenantId == tenantId && type.PublicId == recognitionTypePublicId && type.IsActive)
            .Select(type => new RecognitionTypeRef(type.Id, type.Name))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> IsProfileRetiredAsync(long personnelFileId, CancellationToken cancellationToken) =>
        dbContext.PersonnelFileEmployeeProfiles
            .AsNoTracking()
            .AnyAsync(
                profile => profile.PersonnelFileId == personnelFileId
                    && profile.EmploymentStatusCode == PersonnelFileEmployeeProfile.RetiredEmploymentStatusCode,
                cancellationToken);

    public Task<PersonnelFilePersonnelAction?> GetPersonnelActionEntityAsync(
        long personnelFileId, Guid personnelActionPublicId, CancellationToken cancellationToken) =>
        dbContext.PersonnelFilePersonnelActions
            .SingleOrDefaultAsync(
                action => action.PersonnelFileId == personnelFileId && action.PublicId == personnelActionPublicId,
                cancellationToken);

    public async Task<IReadOnlyCollection<RecognitionDocumentResponse>> GetRecognitionDocumentsAsync(
        Guid recognitionPublicId, CancellationToken cancellationToken)
    {
        var items = await dbContext.PersonnelFileRecognitionDocuments
            .AsNoTracking()
            .Include(document => document.DocumentTypeCatalogItem)
            .Where(document => document.Recognition.PublicId == recognitionPublicId && document.IsActive)
            .OrderByDescending(document => document.CreatedUtc)
            .ToListAsync(cancellationToken);
        return items.Select(MapRecognitionDocument).ToArray();
    }

    public async Task<RecognitionDocumentResponse?> GetRecognitionDocumentAsync(
        Guid recognitionPublicId, Guid documentPublicId, CancellationToken cancellationToken)
    {
        var item = await dbContext.PersonnelFileRecognitionDocuments
            .AsNoTracking()
            .Include(document => document.DocumentTypeCatalogItem)
            .SingleOrDefaultAsync(
                document => document.Recognition.PublicId == recognitionPublicId && document.PublicId == documentPublicId,
                cancellationToken);
        return item is null ? null : MapRecognitionDocument(item);
    }

    public Task<PersonnelFileRecognitionDocument?> GetRecognitionDocumentEntityAsync(
        Guid recognitionPublicId, Guid documentPublicId, Guid tenantId, CancellationToken cancellationToken) =>
        dbContext.PersonnelFileRecognitionDocuments
            .SingleOrDefaultAsync(
                document => document.Recognition.PublicId == recognitionPublicId
                    && document.PublicId == documentPublicId
                    && document.TenantId == tenantId,
                cancellationToken);

    private static PersonnelFileRecognitionResponse MapRecognition(PersonnelFileRecognition item) =>
        new(
            item.PublicId,
            item.RecognitionType!.PublicId,
            item.TypeNameSnapshot,
            item.EventDate,
            item.Detail,
            item.Amount,
            item.CurrencyCode,
            item.AssignedPositionPublicId,
            item.RegisteredByUserId,
            item.StatusCode,
            item.DecidedByUserId,
            item.DecidedUtc,
            item.DecisionNote,
            item.AnnulmentReason,
            item.AnnulledByUserId,
            item.AnnulledUtc,
            item.PersonnelActionPublicId,
            item.Notes,
            item.IsActive,
            item.ConcurrencyToken,
            item.CreatedUtc,
            item.ModifiedUtc);

    private static RecognitionDocumentResponse MapRecognitionDocument(PersonnelFileRecognitionDocument document) =>
        new(
            document.PublicId,
            document.DocumentTypeCatalogItem != null ? document.DocumentTypeCatalogItem.PublicId : (Guid?)null,
            document.DocumentTypeCatalogItem != null ? document.DocumentTypeCatalogItem.Name : null,
            document.FilePublicId,
            document.FileName,
            document.ContentType,
            document.SizeBytes,
            document.Observations,
            document.IsActive,
            document.ConcurrencyToken);

    public Task<PersonnelFileDisciplinaryAction?> GetDisciplinaryActionEntityAsync(
        Guid personnelFilePublicId, Guid disciplinaryActionPublicId, CancellationToken cancellationToken) =>
        dbContext.PersonnelFileDisciplinaryActions
            .SingleOrDefaultAsync(
                item => item.PersonnelFile.PublicId == personnelFilePublicId && item.PublicId == disciplinaryActionPublicId,
                cancellationToken);

    public Task<long?> GetDisciplinaryActionInternalIdAsync(
        Guid personnelFilePublicId, Guid disciplinaryActionPublicId, CancellationToken cancellationToken) =>
        dbContext.PersonnelFileDisciplinaryActions
            .AsNoTracking()
            .Where(item => item.PersonnelFile.PublicId == personnelFilePublicId && item.PublicId == disciplinaryActionPublicId)
            .Select(item => (long?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public void AddDisciplinaryAction(PersonnelFileDisciplinaryAction entity) =>
        dbContext.PersonnelFileDisciplinaryActions.Add(entity);

    public void AddDisciplinaryActionDocument(PersonnelFileDisciplinaryActionDocument entity) =>
        dbContext.PersonnelFileDisciplinaryActionDocuments.Add(entity);

    public async Task<IReadOnlyCollection<PersonnelFileDisciplinaryActionResponse>> GetDisciplinaryActionResponsesAsync(
        Guid personnelFilePublicId, bool onlyApplied, CancellationToken cancellationToken)
    {
        var items = await dbContext.PersonnelFileDisciplinaryActions
            .AsNoTracking()
            .Include(item => item.DisciplinaryActionType)
            .Include(item => item.DisciplinaryActionCause)
            .Where(item => item.PersonnelFile.PublicId == personnelFilePublicId
                && (!onlyApplied || item.StatusCode == PersonnelTransactionStatuses.Aplicada))
            .OrderByDescending(item => item.IncidentDate)
            .ThenByDescending(item => item.CreatedUtc)
            .ToListAsync(cancellationToken);
        return items.Select(MapDisciplinaryAction).ToArray();
    }

    public async Task<PersonnelFileDisciplinaryActionResponse?> GetDisciplinaryActionResponseAsync(
        Guid personnelFilePublicId, Guid disciplinaryActionPublicId, CancellationToken cancellationToken)
    {
        var item = await dbContext.PersonnelFileDisciplinaryActions
            .AsNoTracking()
            .Include(item => item.DisciplinaryActionType)
            .Include(item => item.DisciplinaryActionCause)
            .SingleOrDefaultAsync(
                item => item.PersonnelFile.PublicId == personnelFilePublicId && item.PublicId == disciplinaryActionPublicId,
                cancellationToken);
        return item is null ? null : MapDisciplinaryAction(item);
    }

    public Task<DisciplinaryActionTypeRef?> ResolveActiveDisciplinaryActionTypeAsync(
        Guid tenantId, Guid disciplinaryActionTypePublicId, CancellationToken cancellationToken) =>
        dbContext.Set<DisciplinaryActionType>()
            .AsNoTracking()
            .Where(type => type.TenantId == tenantId && type.PublicId == disciplinaryActionTypePublicId && type.IsActive)
            .Select(type => new DisciplinaryActionTypeRef(type.Id, type.Name, type.AppliesSuspension))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<DisciplinaryActionCauseRef?> ResolveActiveDisciplinaryActionCauseAsync(
        Guid tenantId, Guid disciplinaryActionCausePublicId, CancellationToken cancellationToken) =>
        dbContext.Set<DisciplinaryActionCause>()
            .AsNoTracking()
            .Where(cause => cause.TenantId == tenantId && cause.PublicId == disciplinaryActionCausePublicId && cause.IsActive)
            .Select(cause => new DisciplinaryActionCauseRef(cause.Id, cause.Name, cause.DeductionConceptTypeCode))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<EgressConceptRef?> ResolveActiveEgressConceptAsync(
        Guid tenantId, string conceptCode, CancellationToken cancellationToken)
    {
        var normalizedCode = conceptCode.Trim().ToUpperInvariant();

        // Resolve the tenant's country, then require an ACTIVE egreso concept with that normalized code in the
        // country-scoped compensation-concept-types catalog (mirrors DisciplinaryActionCauseRepository —
        // DEDUCTION_CONCEPT_INVALID). Returns the name so the record can snapshot it at Apply.
        var companyCountryId = await dbContext.Companies
            .AsNoTracking()
            .Where(company => company.PublicId == tenantId)
            .Select(company => (long?)company.CountryCatalogItemId)
            .SingleOrDefaultAsync(cancellationToken);
        if (companyCountryId is null)
        {
            return null;
        }

        return await dbContext.CompensationConceptTypeCatalogItems
            .AsNoTracking()
            .Where(item => item.CountryCatalogItemId == companyCountryId.Value
                && item.IsActive
                && item.Nature == CompensationNature.Egreso
                && item.NormalizedCode == normalizedCode)
            .Select(item => new EgressConceptRef(item.Code, item.Name))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<string?> GetDisciplinaryActionCauseConceptCodeAsync(long disciplinaryActionCauseId, CancellationToken cancellationToken) =>
        dbContext.Set<DisciplinaryActionCause>()
            .AsNoTracking()
            .Where(cause => cause.Id == disciplinaryActionCauseId)
            .Select(cause => cause.DeductionConceptTypeCode)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyCollection<DisciplinaryActionDocumentResponse>> GetDisciplinaryActionDocumentsAsync(
        Guid disciplinaryActionPublicId, CancellationToken cancellationToken)
    {
        var items = await dbContext.PersonnelFileDisciplinaryActionDocuments
            .AsNoTracking()
            .Include(document => document.DocumentTypeCatalogItem)
            .Where(document => document.DisciplinaryAction.PublicId == disciplinaryActionPublicId && document.IsActive)
            .OrderByDescending(document => document.CreatedUtc)
            .ToListAsync(cancellationToken);
        return items.Select(MapDisciplinaryActionDocument).ToArray();
    }

    public async Task<DisciplinaryActionDocumentResponse?> GetDisciplinaryActionDocumentAsync(
        Guid disciplinaryActionPublicId, Guid documentPublicId, CancellationToken cancellationToken)
    {
        var item = await dbContext.PersonnelFileDisciplinaryActionDocuments
            .AsNoTracking()
            .Include(document => document.DocumentTypeCatalogItem)
            .SingleOrDefaultAsync(
                document => document.DisciplinaryAction.PublicId == disciplinaryActionPublicId && document.PublicId == documentPublicId,
                cancellationToken);
        return item is null ? null : MapDisciplinaryActionDocument(item);
    }

    public Task<PersonnelFileDisciplinaryActionDocument?> GetDisciplinaryActionDocumentEntityAsync(
        Guid disciplinaryActionPublicId, Guid documentPublicId, Guid tenantId, CancellationToken cancellationToken) =>
        dbContext.PersonnelFileDisciplinaryActionDocuments
            .SingleOrDefaultAsync(
                document => document.DisciplinaryAction.PublicId == disciplinaryActionPublicId
                    && document.PublicId == documentPublicId
                    && document.TenantId == tenantId,
                cancellationToken);

    private static PersonnelFileDisciplinaryActionResponse MapDisciplinaryAction(PersonnelFileDisciplinaryAction item) =>
        new(
            item.PublicId,
            item.DisciplinaryActionType!.PublicId,
            item.TypeNameSnapshot,
            item.TypeAppliedSuspension,
            item.DisciplinaryActionCause!.PublicId,
            item.CauseNameSnapshot,
            item.IncidentDate,
            item.FactsDetail,
            item.HasPayrollDeduction,
            item.DeductionAmount,
            item.CurrencyCode,
            item.DeductionConceptTypeCode,
            item.DeductionConceptNameSnapshot,
            item.SuspensionStartDate,
            item.SuspensionEndDate,
            item.SuspensionDays,
            item.AssignedPositionPublicId,
            item.RegisteredByUserId,
            item.StatusCode,
            item.DecidedByUserId,
            item.DecidedUtc,
            item.DecisionNote,
            item.AnnulmentReason,
            item.AnnulledByUserId,
            item.AnnulledUtc,
            item.PersonnelActionPublicId,
            item.SuspensionActionPublicId,
            item.Notes,
            item.IsActive,
            item.ConcurrencyToken,
            item.CreatedUtc,
            item.ModifiedUtc);

    private static DisciplinaryActionDocumentResponse MapDisciplinaryActionDocument(PersonnelFileDisciplinaryActionDocument document) =>
        new(
            document.PublicId,
            document.DocumentTypeCatalogItem != null ? document.DocumentTypeCatalogItem.PublicId : (Guid?)null,
            document.DocumentTypeCatalogItem != null ? document.DocumentTypeCatalogItem.Name : null,
            document.FilePublicId,
            document.FileName,
            document.ContentType,
            document.SizeBytes,
            document.Observations,
            document.IsActive,
            document.ConcurrencyToken);

    public Task<bool> HasOverlappingSuspensionAsync(
        long personnelFileId,
        DateOnly startDate,
        DateOnly endDate,
        long? excludeDisciplinaryActionId,
        CancellationToken cancellationToken) =>
        dbContext.PersonnelFileDisciplinaryActions
            .AsNoTracking()
            .Where(item => item.PersonnelFileId == personnelFileId
                && item.StatusCode == PersonnelTransactionStatuses.Aplicada
                && item.SuspensionStartDate != null
                && item.SuspensionEndDate != null
                && (excludeDisciplinaryActionId == null || item.Id != excludeDisciplinaryActionId.Value)
                && item.SuspensionStartDate <= endDate
                && item.SuspensionEndDate >= startDate)
            .AnyAsync(cancellationToken);

    public Task AcquireEmployeeRelationsLockAsync(Guid tenantId, long personnelFileId, CancellationToken cancellationToken)
    {
        var tenantKey = BitConverter.ToInt32(tenantId.ToByteArray(), 0);
        var objectKey = unchecked(tenantKey ^ (int)personnelFileId);
        return dbContext.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock({0}, {1})",
            new object[] { EmployeeRelationsLockClassId, objectKey },
            cancellationToken);
    }

    // ── Recognitions bandeja + export (PR-5, §3.9) ────────────────────────────────────────────────

    public async Task<RecognitionBandejaResponse> QueryRecognitionsAsync(
        QueryRecognitionsQuery query, CancellationToken cancellationToken)
    {
        var baseQuery = FilteredRecognitions(query.EmployeeId, query.RecognitionTypeCode, query.FromDate, query.ToDate);

        // StatusCounts over the full (non-status) filter, so every status is represented even though the items
        // default to excluding ANULADA.
        var statusCounts = await baseQuery
            .GroupBy(row => row.Recognition.StatusCode)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.Key, group => group.Count, cancellationToken);

        var filtered = ApplyRecognitionItemStatusFilter(baseQuery, query.StatusCode, query.IncludeAnnulled);
        var totalCount = await filtered.CountAsync(cancellationToken);

        var items = await filtered
            .OrderByDescending(row => row.Recognition.EventDate)
            .ThenByDescending(row => row.Recognition.CreatedUtc)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(row => new RecognitionListItemResponse(
                row.Recognition.PublicId,
                row.EmployeeFilePublicId,
                row.EmployeeFullName,
                row.EmployeeCode,
                row.RecognitionTypePublicId,
                row.RecognitionTypeCode,
                row.Recognition.TypeNameSnapshot,
                row.Recognition.EventDate,
                row.Recognition.Detail,
                row.Recognition.Amount,
                row.Recognition.CurrencyCode,
                row.Recognition.StatusCode,
                row.Recognition.RegisteredByUserId,
                row.Recognition.DecidedByUserId,
                row.Recognition.DecidedUtc,
                row.Recognition.CreatedUtc))
            .ToArrayAsync(cancellationToken);

        return new RecognitionBandejaResponse(items, query.PageNumber, query.PageSize, totalCount, statusCounts);
    }

    public async Task<IReadOnlyCollection<ReconocimientoExportRow>> GetRecognitionExportRowsAsync(
        ExportRecognitionsQuery query, CancellationToken cancellationToken)
    {
        var filtered = ApplyRecognitionItemStatusFilter(
                FilteredRecognitions(query.EmployeeId, query.RecognitionTypeCode, query.FromDate, query.ToDate),
                query.StatusCode, query.IncludeAnnulled)
            .OrderByDescending(row => row.Recognition.EventDate)
            .ThenByDescending(row => row.Recognition.CreatedUtc)
            .Select(row => new ReconocimientoExportRow(
                row.EmployeeFullName,
                row.EmployeeCode,
                row.Recognition.TypeNameSnapshot,
                row.Recognition.EventDate,
                row.Recognition.Detail,
                row.Recognition.Amount,
                row.Recognition.CurrencyCode,
                row.Recognition.StatusCode,
                row.Recognition.RegisteredByUserId,
                row.Recognition.DecidedByUserId,
                row.Recognition.DecidedUtc,
                row.Recognition.CreatedUtc));

        var limited = query.MaxRows is { } maxRows ? filtered.Take(maxRows + 1) : filtered;
        return await limited.ToArrayAsync(cancellationToken);
    }

    private IQueryable<RecognitionQueryRow> FilteredRecognitions(
        Guid? employeeId, string? recognitionTypeCode, DateOnly? fromDate, DateOnly? toDate)
    {
        // Member-init projection (mirrors PersonnelFileIncapacityRepository.FilteredIncapacities) so EF composes
        // further Where/GroupBy over it reliably. Company scoping is handled by the global tenant query filter.
        var query =
            from recognition in dbContext.PersonnelFileRecognitions.AsNoTracking()
            join file in dbContext.PersonnelFiles.AsNoTracking() on recognition.PersonnelFileId equals file.Id
            join type in dbContext.Set<RecognitionType>().AsNoTracking() on recognition.RecognitionTypeId equals type.Id
            join profileEntry in dbContext.PersonnelFileEmployeeProfiles.AsNoTracking()
                on file.Id equals profileEntry.PersonnelFileId into profileGroup
            from profile in profileGroup.DefaultIfEmpty()
            select new RecognitionQueryRow
            {
                Recognition = recognition,
                EmployeeFullName = file.FullName,
                EmployeeFilePublicId = file.PublicId,
                EmployeeCode = profile != null ? profile.EmployeeCode : null,
                RecognitionTypePublicId = type.PublicId,
                RecognitionTypeCode = type.Code,
                RecognitionTypeNormalizedCode = type.NormalizedCode,
            };

        if (employeeId is { } employeePublicId)
        {
            query = query.Where(row => row.EmployeeFilePublicId == employeePublicId);
        }

        if (!string.IsNullOrWhiteSpace(recognitionTypeCode))
        {
            var normalizedType = recognitionTypeCode.Trim().ToUpperInvariant();
            query = query.Where(row => row.RecognitionTypeNormalizedCode == normalizedType);
        }

        if (fromDate is { } from)
        {
            query = query.Where(row => row.Recognition.EventDate >= from);
        }

        if (toDate is { } to)
        {
            query = query.Where(row => row.Recognition.EventDate <= to);
        }

        return query;
    }

    // ── Disciplinary-actions bandeja + export + payroll input (PR-5, §3.9) ────────────────────────

    public async Task<DisciplinaryActionBandejaResponse> QueryDisciplinaryActionsAsync(
        QueryDisciplinaryActionsQuery query, CancellationToken cancellationToken)
    {
        var baseQuery = FilteredDisciplinaryActions(
            query.EmployeeId, query.DisciplinaryActionTypeCode, query.CauseCode, query.FromDate, query.ToDate);

        var statusCounts = await baseQuery
            .GroupBy(row => row.DisciplinaryAction.StatusCode)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.Key, group => group.Count, cancellationToken);

        var filtered = ApplyDisciplinaryActionItemStatusFilter(baseQuery, query.StatusCode, query.IncludeAnnulled);
        var totalCount = await filtered.CountAsync(cancellationToken);

        var items = await filtered
            .OrderByDescending(row => row.DisciplinaryAction.IncidentDate)
            .ThenByDescending(row => row.DisciplinaryAction.CreatedUtc)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(row => new DisciplinaryActionListItemResponse(
                row.DisciplinaryAction.PublicId,
                row.EmployeeFilePublicId,
                row.EmployeeFullName,
                row.EmployeeCode,
                row.DisciplinaryActionTypePublicId,
                row.DisciplinaryActionTypeCode,
                row.DisciplinaryAction.TypeNameSnapshot,
                row.DisciplinaryActionCausePublicId,
                row.DisciplinaryAction.CauseNameSnapshot,
                row.DisciplinaryAction.IncidentDate,
                row.DisciplinaryAction.FactsDetail,
                row.DisciplinaryAction.HasPayrollDeduction,
                row.DisciplinaryAction.DeductionAmount,
                row.DisciplinaryAction.CurrencyCode,
                row.DisciplinaryAction.DeductionConceptTypeCode,
                row.DisciplinaryAction.DeductionConceptNameSnapshot,
                row.DisciplinaryAction.SuspensionStartDate,
                row.DisciplinaryAction.SuspensionEndDate,
                row.DisciplinaryAction.SuspensionDays,
                row.DisciplinaryAction.StatusCode,
                row.DisciplinaryAction.RegisteredByUserId,
                row.DisciplinaryAction.DecidedByUserId,
                row.DisciplinaryAction.DecidedUtc,
                row.DisciplinaryAction.CreatedUtc))
            .ToArrayAsync(cancellationToken);

        return new DisciplinaryActionBandejaResponse(items, query.PageNumber, query.PageSize, totalCount, statusCounts);
    }

    public async Task<IReadOnlyCollection<AmonestacionExportRow>> GetDisciplinaryActionExportRowsAsync(
        ExportDisciplinaryActionsQuery query, CancellationToken cancellationToken)
    {
        var filtered = ApplyDisciplinaryActionItemStatusFilter(
                FilteredDisciplinaryActions(
                    query.EmployeeId, query.DisciplinaryActionTypeCode, query.CauseCode, query.FromDate, query.ToDate),
                query.StatusCode, query.IncludeAnnulled)
            .OrderByDescending(row => row.DisciplinaryAction.IncidentDate)
            .ThenByDescending(row => row.DisciplinaryAction.CreatedUtc)
            .Select(row => new AmonestacionExportRow(
                row.EmployeeFullName,
                row.EmployeeCode,
                row.DisciplinaryAction.TypeNameSnapshot,
                row.DisciplinaryAction.CauseNameSnapshot,
                row.DisciplinaryAction.IncidentDate,
                row.DisciplinaryAction.FactsDetail,
                row.DisciplinaryAction.HasPayrollDeduction,
                row.DisciplinaryAction.DeductionAmount,
                row.DisciplinaryAction.DeductionConceptNameSnapshot,
                row.DisciplinaryAction.CurrencyCode,
                row.DisciplinaryAction.SuspensionStartDate,
                row.DisciplinaryAction.SuspensionEndDate,
                row.DisciplinaryAction.SuspensionDays,
                row.DisciplinaryAction.StatusCode,
                row.DisciplinaryAction.RegisteredByUserId,
                row.DisciplinaryAction.DecidedByUserId,
                row.DisciplinaryAction.DecidedUtc,
                row.DisciplinaryAction.CreatedUtc));

        var limited = query.MaxRows is { } maxRows ? filtered.Take(maxRows + 1) : filtered;
        return await limited.ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<InsumoPlanillaExportRow>> GetPayrollInputRowsAsync(
        ExportPayrollInputQuery query, CancellationToken cancellationToken)
    {
        // Only APLICADA disciplinary actions of the mandatory range with an effect (RN-14/RN-15: revoked =
        // ANULADA, so filtering on APLICADA excludes them by construction). Range = the incident date (the
        // fault), consistent with the disciplinary bandeja's date filter. The rows are fanned out per effect.
        var startDate = query.StartDate!.Value;
        var endDate = query.EndDate!.Value;

        var applied = await FilteredDisciplinaryActions(employeeId: null, typeCode: null, causeCode: null, fromDate: startDate, toDate: endDate)
            .Where(row => row.DisciplinaryAction.StatusCode == PersonnelTransactionStatuses.Aplicada
                && (row.DisciplinaryAction.HasPayrollDeduction
                    || row.DisciplinaryAction.SuspensionStartDate != null))
            .OrderBy(row => row.EmployeeFullName)
            .ThenByDescending(row => row.DisciplinaryAction.IncidentDate)
            .Select(row => new PayrollInputSource
            {
                EmployeeFullName = row.EmployeeFullName,
                EmployeeCode = row.EmployeeCode,
                CauseNameSnapshot = row.DisciplinaryAction.CauseNameSnapshot,
                IncidentDate = row.DisciplinaryAction.IncidentDate,
                HasPayrollDeduction = row.DisciplinaryAction.HasPayrollDeduction,
                DeductionAmount = row.DisciplinaryAction.DeductionAmount,
                CurrencyCode = row.DisciplinaryAction.CurrencyCode,
                DeductionConceptNameSnapshot = row.DisciplinaryAction.DeductionConceptNameSnapshot,
                SuspensionStartDate = row.DisciplinaryAction.SuspensionStartDate,
                SuspensionEndDate = row.DisciplinaryAction.SuspensionEndDate,
                SuspensionDays = row.DisciplinaryAction.SuspensionDays,
            })
            .ToListAsync(cancellationToken);

        var rows = new List<InsumoPlanillaExportRow>(applied.Count);
        foreach (var source in applied)
        {
            if (source.HasPayrollDeduction && source.DeductionAmount is { } deductionAmount)
            {
                rows.Add(new InsumoPlanillaExportRow(
                    source.EmployeeFullName,
                    source.EmployeeCode,
                    PersonnelTransactionPayrollEffects.Deduction,
                    source.CauseNameSnapshot,
                    source.DeductionConceptNameSnapshot,
                    deductionAmount,
                    source.CurrencyCode,
                    source.IncidentDate,
                    source.IncidentDate,
                    Dias: null));
            }

            if (source.SuspensionStartDate is { } suspensionStart && source.SuspensionEndDate is { } suspensionEnd)
            {
                rows.Add(new InsumoPlanillaExportRow(
                    source.EmployeeFullName,
                    source.EmployeeCode,
                    PersonnelTransactionPayrollEffects.UnpaidSuspension,
                    source.CauseNameSnapshot,
                    ConceptoDescuento: null,
                    Monto: null,
                    Moneda: null,
                    suspensionStart,
                    suspensionEnd,
                    source.SuspensionDays));
            }
        }

        return query.MaxRows is { } maxRows ? rows.Take(maxRows + 1).ToArray() : rows;
    }

    private IQueryable<DisciplinaryActionQueryRow> FilteredDisciplinaryActions(
        Guid? employeeId, string? typeCode, string? causeCode, DateOnly? fromDate, DateOnly? toDate)
    {
        var query =
            from action in dbContext.PersonnelFileDisciplinaryActions.AsNoTracking()
            join file in dbContext.PersonnelFiles.AsNoTracking() on action.PersonnelFileId equals file.Id
            join type in dbContext.Set<DisciplinaryActionType>().AsNoTracking() on action.DisciplinaryActionTypeId equals type.Id
            join cause in dbContext.Set<DisciplinaryActionCause>().AsNoTracking() on action.DisciplinaryActionCauseId equals cause.Id
            join profileEntry in dbContext.PersonnelFileEmployeeProfiles.AsNoTracking()
                on file.Id equals profileEntry.PersonnelFileId into profileGroup
            from profile in profileGroup.DefaultIfEmpty()
            select new DisciplinaryActionQueryRow
            {
                DisciplinaryAction = action,
                EmployeeFullName = file.FullName,
                EmployeeFilePublicId = file.PublicId,
                EmployeeCode = profile != null ? profile.EmployeeCode : null,
                DisciplinaryActionTypePublicId = type.PublicId,
                DisciplinaryActionTypeCode = type.Code,
                DisciplinaryActionTypeNormalizedCode = type.NormalizedCode,
                DisciplinaryActionCausePublicId = cause.PublicId,
                DisciplinaryActionCauseNormalizedCode = cause.NormalizedCode,
            };

        if (employeeId is { } employeePublicId)
        {
            query = query.Where(row => row.EmployeeFilePublicId == employeePublicId);
        }

        if (!string.IsNullOrWhiteSpace(typeCode))
        {
            var normalizedType = typeCode.Trim().ToUpperInvariant();
            query = query.Where(row => row.DisciplinaryActionTypeNormalizedCode == normalizedType);
        }

        if (!string.IsNullOrWhiteSpace(causeCode))
        {
            var normalizedCause = causeCode.Trim().ToUpperInvariant();
            query = query.Where(row => row.DisciplinaryActionCauseNormalizedCode == normalizedCause);
        }

        if (fromDate is { } from)
        {
            query = query.Where(row => row.DisciplinaryAction.IncidentDate >= from);
        }

        if (toDate is { } to)
        {
            query = query.Where(row => row.DisciplinaryAction.IncidentDate <= to);
        }

        return query;
    }

    // When a status is supplied the items are filtered to exactly that status; otherwise includeAnnulled decides
    // whether ANULADA is shown — the default excludes it (mirrors the compensatory-time bandeja).
    private static IQueryable<RecognitionQueryRow> ApplyRecognitionItemStatusFilter(
        IQueryable<RecognitionQueryRow> query, string? statusCode, bool includeAnnulled)
    {
        if (!string.IsNullOrWhiteSpace(statusCode))
        {
            var status = statusCode.Trim().ToUpperInvariant();
            return query.Where(row => row.Recognition.StatusCode == status);
        }

        return includeAnnulled
            ? query
            : query.Where(row => row.Recognition.StatusCode != PersonnelTransactionStatuses.Anulada);
    }

    private static IQueryable<DisciplinaryActionQueryRow> ApplyDisciplinaryActionItemStatusFilter(
        IQueryable<DisciplinaryActionQueryRow> query, string? statusCode, bool includeAnnulled)
    {
        if (!string.IsNullOrWhiteSpace(statusCode))
        {
            var status = statusCode.Trim().ToUpperInvariant();
            return query.Where(row => row.DisciplinaryAction.StatusCode == status);
        }

        return includeAnnulled
            ? query
            : query.Where(row => row.DisciplinaryAction.StatusCode != PersonnelTransactionStatuses.Anulada);
    }

    private sealed class RecognitionQueryRow
    {
        public PersonnelFileRecognition Recognition { get; init; } = null!;

        public string EmployeeFullName { get; init; } = string.Empty;

        public Guid EmployeeFilePublicId { get; init; }

        public string? EmployeeCode { get; init; }

        public Guid RecognitionTypePublicId { get; init; }

        public string RecognitionTypeCode { get; init; } = string.Empty;

        public string RecognitionTypeNormalizedCode { get; init; } = string.Empty;
    }

    private sealed class DisciplinaryActionQueryRow
    {
        public PersonnelFileDisciplinaryAction DisciplinaryAction { get; init; } = null!;

        public string EmployeeFullName { get; init; } = string.Empty;

        public Guid EmployeeFilePublicId { get; init; }

        public string? EmployeeCode { get; init; }

        public Guid DisciplinaryActionTypePublicId { get; init; }

        public string DisciplinaryActionTypeCode { get; init; } = string.Empty;

        public string DisciplinaryActionTypeNormalizedCode { get; init; } = string.Empty;

        public Guid DisciplinaryActionCausePublicId { get; init; }

        public string DisciplinaryActionCauseNormalizedCode { get; init; } = string.Empty;
    }

    private sealed class PayrollInputSource
    {
        public string EmployeeFullName { get; init; } = string.Empty;

        public string? EmployeeCode { get; init; }

        public string CauseNameSnapshot { get; init; } = string.Empty;

        public DateOnly IncidentDate { get; init; }

        public bool HasPayrollDeduction { get; init; }

        public decimal? DeductionAmount { get; init; }

        public string? CurrencyCode { get; init; }

        public string? DeductionConceptNameSnapshot { get; init; }

        public DateOnly? SuspensionStartDate { get; init; }

        public DateOnly? SuspensionEndDate { get; init; }

        public int? SuspensionDays { get; init; }
    }
}

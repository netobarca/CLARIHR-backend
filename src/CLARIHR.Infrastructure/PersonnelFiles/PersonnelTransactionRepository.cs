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
}

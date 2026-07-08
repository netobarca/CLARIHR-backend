using CLARIHR.Application.Abstractions.PersonnelFiles;
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

using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Abstractions.PersonnelFiles;

/// <summary>
/// Persistence port of the "otras transacciones de personal" module — recognitions and disciplinary actions
/// (REQ-003 PR-2). This PR wires only the tracked-entity loaders the decision/revocation handlers need
/// (PR-3/PR-4), the suspension-overlap query (RN-18) and the per-(tenant, employee) advisory lock that
/// serializes the apply-with-suspension race (aclaración №3). The company bandeja / export queries, the
/// payroll-input rows and the two time-availability sources are declared in PR-5/PR-6.
/// </summary>
public interface IPersonnelTransactionRepository
{
    // ── Recognition loaders / writes ──────────────────────────────────────────────────────────────
    Task<PersonnelFileRecognition?> GetRecognitionEntityAsync(
        Guid personnelFilePublicId, Guid recognitionPublicId, CancellationToken cancellationToken);

    Task<long?> GetRecognitionInternalIdAsync(
        Guid personnelFilePublicId, Guid recognitionPublicId, CancellationToken cancellationToken);

    void AddRecognition(PersonnelFileRecognition entity);

    void AddRecognitionDocument(PersonnelFileRecognitionDocument entity);

    // ── Disciplinary-action loaders / writes ──────────────────────────────────────────────────────
    Task<PersonnelFileDisciplinaryAction?> GetDisciplinaryActionEntityAsync(
        Guid personnelFilePublicId, Guid disciplinaryActionPublicId, CancellationToken cancellationToken);

    Task<long?> GetDisciplinaryActionInternalIdAsync(
        Guid personnelFilePublicId, Guid disciplinaryActionPublicId, CancellationToken cancellationToken);

    void AddDisciplinaryAction(PersonnelFileDisciplinaryAction entity);

    void AddDisciplinaryActionDocument(PersonnelFileDisciplinaryActionDocument entity);

    // ── Suspension overlap (RN-18) ────────────────────────────────────────────────────────────────

    /// <summary>
    /// True when the employee already has an APLICADA disciplinary action whose suspension range overlaps
    /// [<paramref name="startDate"/>, <paramref name="endDate"/>], excluding
    /// <paramref name="excludeDisciplinaryActionId"/> (the record being applied/edited). Inclusive intersection
    /// (mirrors <c>PersonnelTransactionRules.RangesOverlap</c> — RN-18).
    /// </summary>
    Task<bool> HasOverlappingSuspensionAsync(
        long personnelFileId,
        DateOnly startDate,
        DateOnly endDate,
        long? excludeDisciplinaryActionId,
        CancellationToken cancellationToken);

    // ── Concurrency serialization (aclaración №3) ─────────────────────────────────────────────────

    /// <summary>
    /// Serializes the apply-with-suspension race per (tenant, employee) with a transaction-scoped PostgreSQL
    /// advisory lock, closing the check-then-act TOCTOU on the suspension-overlap invariant (RN-18). Must run
    /// inside an open transaction (the handler opens one); the lock releases on commit/rollback. The default is
    /// a no-op (test fakes have no PostgreSQL); the EF repository takes the real advisory lock.
    /// </summary>
    Task AcquireEmployeeRelationsLockAsync(Guid tenantId, long personnelFileId, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

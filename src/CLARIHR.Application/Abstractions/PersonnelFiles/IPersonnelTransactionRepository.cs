using CLARIHR.Application.Features.PersonnelFiles.PersonnelTransactions;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Abstractions.PersonnelFiles;

/// <summary>Resolved active recognition-type master reference (internal id + name to snapshot).</summary>
public sealed record RecognitionTypeRef(long InternalId, string Name);

/// <summary>
/// Persistence port of the "otras transacciones de personal" module — recognitions and disciplinary actions
/// (REQ-003 PR-2/PR-3). It wires the tracked-entity loaders the decision/revocation handlers need, the
/// recognition read projections (with the self-service APLICADA filter, D-13), the linked personnel-action
/// loader used by the revocation, the recognition-document loaders, the suspension-overlap query (RN-18) and the
/// per-(tenant, employee) advisory lock that serializes the apply-with-suspension race (aclaración №3). The
/// company bandeja / export queries, the payroll-input rows and the two time-availability sources land in
/// PR-5/PR-6.
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

    // ── Recognition read projections (PR-3) ───────────────────────────────────────────────────────

    /// <summary>
    /// Recognitions of the file mapped to the wire. When <paramref name="onlyApplied"/> is true the projection
    /// returns only APLICADA records — the self-service employee's view (D-13). Newest first.
    /// </summary>
    Task<IReadOnlyCollection<PersonnelFileRecognitionResponse>> GetRecognitionResponsesAsync(
        Guid personnelFilePublicId, bool onlyApplied, CancellationToken cancellationToken);

    Task<PersonnelFileRecognitionResponse?> GetRecognitionResponseAsync(
        Guid personnelFilePublicId, Guid recognitionPublicId, CancellationToken cancellationToken);

    /// <summary>Resolves the active recognition-type master of the tenant (422 RECOGNITION_TYPE_INVALID otherwise).</summary>
    Task<RecognitionTypeRef?> ResolveActiveRecognitionTypeAsync(
        Guid tenantId, Guid recognitionTypePublicId, CancellationToken cancellationToken);

    /// <summary>Whether the employee's profile is RETIRADO (blocks creates/edits/applies — aclaración №10).</summary>
    Task<bool> IsProfileRetiredAsync(long personnelFileId, CancellationToken cancellationToken);

    /// <summary>Tracked personnel-action entity by (file, public id) so the revocation can <c>Annul()</c> it.</summary>
    Task<PersonnelFilePersonnelAction?> GetPersonnelActionEntityAsync(
        long personnelFileId, Guid personnelActionPublicId, CancellationToken cancellationToken);

    // ── Recognition documents (PR-3) ──────────────────────────────────────────────────────────────
    Task<IReadOnlyCollection<RecognitionDocumentResponse>> GetRecognitionDocumentsAsync(
        Guid recognitionPublicId, CancellationToken cancellationToken);

    Task<RecognitionDocumentResponse?> GetRecognitionDocumentAsync(
        Guid recognitionPublicId, Guid documentPublicId, CancellationToken cancellationToken);

    Task<PersonnelFileRecognitionDocument?> GetRecognitionDocumentEntityAsync(
        Guid recognitionPublicId, Guid documentPublicId, Guid tenantId, CancellationToken cancellationToken);

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

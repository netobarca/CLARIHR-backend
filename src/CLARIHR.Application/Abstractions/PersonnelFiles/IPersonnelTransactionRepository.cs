using CLARIHR.Application.Features.PersonnelFiles.PersonnelTransactions;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Abstractions.PersonnelFiles;

/// <summary>Resolved active recognition-type master reference (internal id + name to snapshot).</summary>
public sealed record RecognitionTypeRef(long InternalId, string Name);

/// <summary>
/// Resolved active disciplinary-action-type master reference (internal id + name to snapshot + the
/// <c>AppliesSuspension</c> flag snapshotted at creation — RN-05/PR-2).
/// </summary>
public sealed record DisciplinaryActionTypeRef(long InternalId, string Name, bool AppliesSuspension);

/// <summary>
/// Resolved active disciplinary-action-cause master reference (internal id + name to snapshot + the cause's
/// optional default egreso concept code — the "referencia editable, default de la causa" of aclaración №5).
/// </summary>
public sealed record DisciplinaryActionCauseRef(long InternalId, string Name, string? DeductionConceptTypeCode);

/// <summary>Resolved active egreso compensation concept (normalized code + name to snapshot at Apply).</summary>
public sealed record EgressConceptRef(string Code, string Name);

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

    // ── Disciplinary-action read projections (PR-4) ───────────────────────────────────────────────

    /// <summary>
    /// Disciplinary actions of the file mapped to the wire. When <paramref name="onlyApplied"/> is true the
    /// projection returns only APLICADA records — the self-service employee's view (D-13). Newest first.
    /// </summary>
    Task<IReadOnlyCollection<PersonnelFileDisciplinaryActionResponse>> GetDisciplinaryActionResponsesAsync(
        Guid personnelFilePublicId, bool onlyApplied, CancellationToken cancellationToken);

    Task<PersonnelFileDisciplinaryActionResponse?> GetDisciplinaryActionResponseAsync(
        Guid personnelFilePublicId, Guid disciplinaryActionPublicId, CancellationToken cancellationToken);

    /// <summary>Resolves the active disciplinary-action-type master of the tenant (422 DISCIPLINARY_ACTION_TYPE_INVALID otherwise).</summary>
    Task<DisciplinaryActionTypeRef?> ResolveActiveDisciplinaryActionTypeAsync(
        Guid tenantId, Guid disciplinaryActionTypePublicId, CancellationToken cancellationToken);

    /// <summary>Resolves the active disciplinary-action-cause master of the tenant (422 DISCIPLINARY_ACTION_CAUSE_INVALID otherwise).</summary>
    Task<DisciplinaryActionCauseRef?> ResolveActiveDisciplinaryActionCauseAsync(
        Guid tenantId, Guid disciplinaryActionCausePublicId, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves an ACTIVE egreso (<c>Nature = Egreso</c>) compensation concept of the tenant's country by
    /// normalized code, returning its name to snapshot. Null when the code is unknown, inactive or not an
    /// egreso concept (422 DEDUCTION_CONCEPT_INVALID). Shares the channel used by the cause master (PR-1).
    /// </summary>
    Task<EgressConceptRef?> ResolveActiveEgressConceptAsync(
        Guid tenantId, string conceptCode, CancellationToken cancellationToken);

    /// <summary>The cause master's current default egreso concept code (the record freezes it at Apply — aclaración №5).</summary>
    Task<string?> GetDisciplinaryActionCauseConceptCodeAsync(long disciplinaryActionCauseId, CancellationToken cancellationToken);

    // ── Disciplinary-action documents (PR-4) ──────────────────────────────────────────────────────
    Task<IReadOnlyCollection<DisciplinaryActionDocumentResponse>> GetDisciplinaryActionDocumentsAsync(
        Guid disciplinaryActionPublicId, CancellationToken cancellationToken);

    Task<DisciplinaryActionDocumentResponse?> GetDisciplinaryActionDocumentAsync(
        Guid disciplinaryActionPublicId, Guid documentPublicId, CancellationToken cancellationToken);

    Task<PersonnelFileDisciplinaryActionDocument?> GetDisciplinaryActionDocumentEntityAsync(
        Guid disciplinaryActionPublicId, Guid documentPublicId, Guid tenantId, CancellationToken cancellationToken);

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

    // ── Company bandejas + exports + payroll input (PR-5, §3.9) ───────────────────────────────────

    /// <summary>
    /// The company-wide recognitions bandeja page (RF-012): items + paging + StatusCounts. The StatusCounts
    /// cover every status; the items default to excluding ANULADA (opt in with <c>IncludeAnnulled</c> or an
    /// explicit status). Ordered newest event first.
    /// </summary>
    Task<RecognitionBandejaResponse> QueryRecognitionsAsync(
        QueryRecognitionsQuery query, CancellationToken cancellationToken);

    /// <summary>The recognitions export rows (same filters as the bandeja), capped at <c>MaxRows + 1</c>.</summary>
    Task<IReadOnlyCollection<ReconocimientoExportRow>> GetRecognitionExportRowsAsync(
        ExportRecognitionsQuery query, CancellationToken cancellationToken);

    /// <summary>The company-wide disciplinary-actions bandeja page (RF-012): items + paging + StatusCounts.</summary>
    Task<DisciplinaryActionBandejaResponse> QueryDisciplinaryActionsAsync(
        QueryDisciplinaryActionsQuery query, CancellationToken cancellationToken);

    /// <summary>The disciplinary-actions export rows (same filters as the bandeja), capped at <c>MaxRows + 1</c>.</summary>
    Task<IReadOnlyCollection<AmonestacionExportRow>> GetDisciplinaryActionExportRowsAsync(
        ExportDisciplinaryActionsQuery query, CancellationToken cancellationToken);

    /// <summary>
    /// The payroll-input rows (RF-012): only APLICADA disciplinary actions whose incident date falls in
    /// [<c>StartDate</c>, <c>EndDate</c>] with an effect, one row per effect (DESCUENTO / SUSPENSION_SIN_GOCE).
    /// Revoked (ANULADA) records never travel (RN-14/RN-15). The handler has already validated the range.
    /// </summary>
    Task<IReadOnlyCollection<InsumoPlanillaExportRow>> GetPayrollInputRowsAsync(
        ExportPayrollInputQuery query, CancellationToken cancellationToken);

    // ── Time-availability sources (PR-6, §3.11) ───────────────────────────────────────────────────

    /// <summary>
    /// Source 1 of the time-availability query — SUSPENSIONS (aclaración №6): the APLICADA disciplinary actions
    /// whose real suspension block INTERSECTS <paramref name="window"/> (inclusive:
    /// <c>suspensionStart ≤ window.End &amp;&amp; suspensionEnd ≥ window.Start</c>, RN-15). Minimal payload (P-10):
    /// no cause/facts/amounts — <c>categoryCode = SUSPENSION</c>, <c>days = SuspensionDays</c>,
    /// <c>sourceModule = EMPLOYEE_RELATIONS</c>, <c>referencePublicId</c> = the disciplinary action. Applies the
    /// employee and org-unit filters.
    /// </summary>
    Task<IReadOnlyCollection<TimeAvailabilityRowResponse>> GetSuspensionAvailabilityRowsAsync(
        Guid companyId, AvailabilityWindow window, TimeAvailabilityFilters filters, CancellationToken cancellationToken);

    /// <summary>Source 3 — NOT-WORKED TIME (REQ-011): the REGISTERED records overlapping the window. Annulled ones
    /// are out: an annulled absence never happened.</summary>
    Task<IReadOnlyCollection<TimeAvailabilityRowResponse>> GetNotWorkedTimeAvailabilityRowsAsync(
        Guid companyId, AvailabilityWindow window, TimeAvailabilityFilters filters, CancellationToken cancellationToken);

    /// <summary>
    /// Source 2 of the time-availability query — END OF TEMPORARY CONTRACTS (aclaración №7): active assignments
    /// whose <c>ContractTypeCode</c> resolves (by normalized code + tenant country, same criterion as the
    /// contract-type projection) to a catalog item with <c>IsTemporary = true</c> and whose <c>EndDate</c> falls
    /// in <paramref name="window"/>. Derived row (no module record): <c>startDate = endDate = EndDate</c>,
    /// <c>days = 1</c>, <c>statusCode = VIGENTE</c>, <c>categoryCode = FIN_CONTRATO_TEMPORAL</c>,
    /// <c>sourceModule = EMPLOYMENT</c>, <c>referencePublicId</c> = the assignment/plaza. Applies the employee and
    /// org-unit filters.
    /// </summary>
    Task<IReadOnlyCollection<TimeAvailabilityRowResponse>> GetTemporaryContractEndRowsAsync(
        Guid companyId, AvailabilityWindow window, TimeAvailabilityFilters filters, CancellationToken cancellationToken);
}

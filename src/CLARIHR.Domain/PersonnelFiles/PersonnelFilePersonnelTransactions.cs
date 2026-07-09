using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.PersonnelFiles;

/// <summary>
/// A recognition record ("reconocimiento", REQ-003 D-02/D-07): a merit registered on the employee file that,
/// once authorized, is applied to the expediente with an automatic <c>RECONOCIMIENTO</c> personnel-action
/// entry. It holds a hard FK to the company-managed <c>RecognitionType</c> master plus a snapshot of the type
/// name (a later master edit never rewrites history), the event date, the mandatory detail, an optional
/// informational amount/currency (RN-17, no payroll coupling in F1 — D-07/P-08), an optional assigned-position
/// reference, and the one-decision lifecycle (<c>EN_REVISION → APLICADA/RECHAZADA</c>, plus <c>ANULADA</c>
/// from EN_REVISION —trámite withdrawal— or from APLICADA —revocation— with a mandatory reason). The applied
/// entry's public id is captured in <see cref="PersonnelActionPublicId"/> so the revocation knows what to
/// annul.
/// <para>The event-date ≤ today validation, the amount/currency coherence (RN-17) and the anti-self-approval
/// double check (RN-02) are NOT domain guards (they depend on the clock / the current user) — they live in
/// <c>PersonnelTransactionRules</c> and the decision handler.</para>
/// </summary>
public sealed class PersonnelFileRecognition : TenantEntity
{
    public const int MaxTypeNameSnapshotLength = 200;
    public const int MaxDetailLength = 1000;
    public const int MaxCurrencyCodeLength = 10;
    public const int MaxDecisionNoteLength = 1000;
    public const int MaxAnnulmentReasonLength = 1000;
    public const int MaxNotesLength = 1000;

    private PersonnelFileRecognition()
    {
    }

    private PersonnelFileRecognition(
        long recognitionTypeId,
        string typeNameSnapshot,
        DateOnly eventDate,
        string detail,
        decimal? amount,
        string? currencyCode,
        Guid? assignedPositionPublicId,
        Guid registeredByUserId,
        string? notes)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        IsActive = true;
        StatusCode = PersonnelTransactionStatuses.EnRevision;

        ApplyDetails(recognitionTypeId, typeNameSnapshot, eventDate, detail, amount, currencyCode, assignedPositionPublicId, notes);

        RegisteredByUserId = registeredByUserId;
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    // Hard FK to the recognition-type master + snapshot of the name (a later master edit never rewrites a record).
    public long RecognitionTypeId { get; private set; }

    public EmployeeRelations.RecognitionType? RecognitionType { get; private set; }

    public string TypeNameSnapshot { get; private set; } = string.Empty;

    public DateOnly EventDate { get; private set; }

    public string Detail { get; private set; } = string.Empty;

    /// <summary>Optional informational reward amount (RN-17 — &gt; 0 when it travels; no payroll coupling in F1).</summary>
    public decimal? Amount { get; private set; }

    public string? CurrencyCode { get; private set; }

    public Guid? AssignedPositionPublicId { get; private set; }

    public Guid RegisteredByUserId { get; private set; }

    public string StatusCode { get; private set; } = PersonnelTransactionStatuses.EnRevision;

    // ── Decision (who applied/rejected, when, with the reject note — RN-07) ──────────────────────────
    public Guid? DecidedByUserId { get; private set; }

    public DateTime? DecidedUtc { get; private set; }

    public string? DecisionNote { get; private set; }

    // ── Annulment / revocation (mandatory reason — RN-07) ────────────────────────────────────────────
    public string? AnnulmentReason { get; private set; }

    public Guid? AnnulledByUserId { get; private set; }

    public DateTime? AnnulledUtc { get; private set; }

    /// <summary>The applied <c>RECONOCIMIENTO</c> entry's public id (captured on Apply so the revocation can annul it).</summary>
    public Guid? PersonnelActionPublicId { get; private set; }

    public string? Notes { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    /// <summary>
    /// Creates a recognition (initial status EN_REVISION). The type id must be positive, the detail non-empty,
    /// and the amount —when it travels— positive. The event-date ≤ today validation is the handler's job.
    /// </summary>
    public static PersonnelFileRecognition Create(
        long recognitionTypeId,
        string typeNameSnapshot,
        DateOnly eventDate,
        string detail,
        decimal? amount,
        string? currencyCode,
        Guid? assignedPositionPublicId,
        Guid registeredByUserId,
        string? notes) =>
        new(recognitionTypeId, typeNameSnapshot, eventDate, detail, amount, currencyCode, assignedPositionPublicId, registeredByUserId, notes);

    /// <summary>Edits the declarative fields while EN_REVISION (RN-01).</summary>
    public void Update(
        long recognitionTypeId,
        string typeNameSnapshot,
        DateOnly eventDate,
        string detail,
        decimal? amount,
        string? currencyCode,
        Guid? assignedPositionPublicId,
        string? notes)
    {
        EnsureEditable();

        ApplyDetails(recognitionTypeId, typeNameSnapshot, eventDate, detail, amount, currencyCode, assignedPositionPublicId, notes);
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Applies the recognition (EN_REVISION → APLICADA); captures the generated entry's public id (RN-03).</summary>
    public void Apply(Guid byUserId, DateTime atUtc, Guid personnelActionPublicId)
    {
        EnsureEditable();

        if (personnelActionPublicId == Guid.Empty)
        {
            throw new ArgumentException("The personnel-action public id must not be empty.", nameof(personnelActionPublicId));
        }

        StatusCode = PersonnelTransactionStatuses.Aplicada;
        DecidedByUserId = byUserId;
        DecidedUtc = atUtc;
        PersonnelActionPublicId = personnelActionPublicId;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Rejects the recognition (EN_REVISION → RECHAZADA); the note is mandatory (RN-07).</summary>
    public void Reject(Guid byUserId, DateTime atUtc, string note)
    {
        EnsureEditable();

        DecisionNote = Truncate(
            PersonnelFileNormalization.Clean(note, nameof(note)),
            MaxDecisionNoteLength,
            nameof(note));
        StatusCode = PersonnelTransactionStatuses.Rechazada;
        DecidedByUserId = byUserId;
        DecidedUtc = atUtc;
        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Annuls the record (terminal) from EN_REVISION (trámite withdrawal) or APLICADA (revocation); the reason
    /// is mandatory (RN-07). The caller annuls the linked entry via <c>PersonnelFilePersonnelAction.Annul()</c>.
    /// </summary>
    public void Annul(string reason, Guid byUserId, DateTime atUtc)
    {
        if (!PersonnelTransactionStatuses.Vigentes.Contains(StatusCode))
        {
            throw new InvalidOperationException("Only an EN_REVISION or APLICADA recognition can be annulled.");
        }

        AnnulmentReason = Truncate(
            PersonnelFileNormalization.Clean(reason, nameof(reason)),
            MaxAnnulmentReasonLength,
            nameof(reason));
        AnnulledByUserId = byUserId;
        AnnulledUtc = atUtc;
        StatusCode = PersonnelTransactionStatuses.Anulada;
        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }

    private void ApplyDetails(
        long recognitionTypeId,
        string typeNameSnapshot,
        DateOnly eventDate,
        string detail,
        decimal? amount,
        string? currencyCode,
        Guid? assignedPositionPublicId,
        string? notes)
    {
        if (recognitionTypeId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(recognitionTypeId), "Recognition type id must be positive.");
        }

        if (amount is <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "The recognition amount must be greater than zero when provided.");
        }

        if (assignedPositionPublicId == Guid.Empty)
        {
            throw new ArgumentException("The assigned position reference must not be empty when provided.", nameof(assignedPositionPublicId));
        }

        RecognitionTypeId = recognitionTypeId;
        TypeNameSnapshot = Truncate(
            PersonnelFileNormalization.Clean(typeNameSnapshot, nameof(typeNameSnapshot)),
            MaxTypeNameSnapshotLength,
            nameof(typeNameSnapshot));
        EventDate = eventDate;
        Detail = Truncate(
            PersonnelFileNormalization.Clean(detail, nameof(detail)),
            MaxDetailLength,
            nameof(detail));
        Amount = amount;
        CurrencyCode = TruncateOptional(
            PersonnelFileNormalization.CleanOptional(currencyCode)?.ToUpperInvariant(),
            MaxCurrencyCodeLength,
            nameof(currencyCode));
        AssignedPositionPublicId = assignedPositionPublicId;
        Notes = TruncateOptional(PersonnelFileNormalization.CleanOptional(notes), MaxNotesLength, nameof(notes));
    }

    private void EnsureEditable()
    {
        if (!PersonnelTransactionStatuses.IsEditable(StatusCode))
        {
            throw new InvalidOperationException("Only an EN_REVISION recognition can be modified or decided.");
        }
    }

    private static string Truncate(string value, int maxLength, string paramName)
    {
        if (value.Length > maxLength)
        {
            throw new ArgumentException($"{paramName} must be {maxLength} characters or fewer.", paramName);
        }

        return value;
    }

    private static string? TruncateOptional(string? value, int maxLength, string paramName) =>
        value is null ? null : Truncate(value, maxLength, paramName);
}

/// <summary>
/// A disciplinary-action record ("amonestación", REQ-003 D-02/D-08): a fault registered on the employee file
/// that, once authorized, is applied with an automatic <c>AMONESTACION</c> personnel-action entry (plus a
/// <c>SUSPENSION</c> entry when the block applies). It holds hard FKs to the company-managed
/// <c>DisciplinaryActionType</c> / <c>DisciplinaryActionCause</c> masters plus their name snapshots and a
/// snapshot of the type's <c>AppliesSuspension</c> flag at creation (RN — changing the master flag never
/// rewrites history), the incident date, the mandatory facts detail, an OPTIONAL payroll-deduction block
/// (flag + amount + currency + concept snapshot frozen at Apply — RN-06/aclaración №5), an OPTIONAL
/// unpaid-suspension block (date range with derived calendar-inclusive days — P-04/RN-05) and the same
/// one-decision lifecycle as <see cref="PersonnelFileRecognition"/>. The applied entries' public ids are
/// captured so the revocation can annul them.
/// <para>The incident-date ≤ today validation, the suspension↔type-flag / deduction↔amount coherence
/// (RN-05/RN-06), the suspension overlap (RN-18) and the anti-self double check (RN-02) are NOT domain guards
/// — they live in <c>PersonnelTransactionRules</c> and the handler under an advisory lock.</para>
/// </summary>
public sealed class PersonnelFileDisciplinaryAction : TenantEntity
{
    public const int MaxTypeNameSnapshotLength = 200;
    public const int MaxCauseNameSnapshotLength = 200;
    public const int MaxFactsDetailLength = 2000;
    public const int MaxCurrencyCodeLength = 10;
    public const int MaxDeductionConceptTypeCodeLength = 80;
    public const int MaxDeductionConceptNameSnapshotLength = 200;
    public const int MaxDecisionNoteLength = 1000;
    public const int MaxAnnulmentReasonLength = 1000;
    public const int MaxNotesLength = 1000;

    private PersonnelFileDisciplinaryAction()
    {
    }

    private PersonnelFileDisciplinaryAction(
        long disciplinaryActionTypeId,
        string typeNameSnapshot,
        bool typeAppliedSuspension,
        long disciplinaryActionCauseId,
        string causeNameSnapshot,
        DateOnly incidentDate,
        string factsDetail,
        bool hasPayrollDeduction,
        decimal? deductionAmount,
        string? currencyCode,
        DateOnly? suspensionStartDate,
        DateOnly? suspensionEndDate,
        Guid? assignedPositionPublicId,
        Guid registeredByUserId,
        string? notes)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        IsActive = true;
        StatusCode = PersonnelTransactionStatuses.EnRevision;
        TypeAppliedSuspension = typeAppliedSuspension;

        ApplyDetails(
            disciplinaryActionTypeId,
            typeNameSnapshot,
            disciplinaryActionCauseId,
            causeNameSnapshot,
            incidentDate,
            factsDetail,
            hasPayrollDeduction,
            deductionAmount,
            currencyCode,
            suspensionStartDate,
            suspensionEndDate,
            assignedPositionPublicId,
            notes);

        RegisteredByUserId = registeredByUserId;
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    // Hard FK to the type master + snapshot of the name and the AppliesSuspension flag at creation.
    public long DisciplinaryActionTypeId { get; private set; }

    public EmployeeRelations.DisciplinaryActionType? DisciplinaryActionType { get; private set; }

    public string TypeNameSnapshot { get; private set; } = string.Empty;

    /// <summary>Snapshot of the type's AppliesSuspension flag at creation (a later master edit never rewrites it).</summary>
    public bool TypeAppliedSuspension { get; private set; }

    // Hard FK to the cause master + snapshot of the name.
    public long DisciplinaryActionCauseId { get; private set; }

    public EmployeeRelations.DisciplinaryActionCause? DisciplinaryActionCause { get; private set; }

    public string CauseNameSnapshot { get; private set; } = string.Empty;

    public DateOnly IncidentDate { get; private set; }

    public string FactsDetail { get; private set; } = string.Empty;

    // ── Deduction block (OPTIONAL — flag + amount + currency + concept snapshot frozen at Apply) ──────
    public bool HasPayrollDeduction { get; private set; }

    public decimal? DeductionAmount { get; private set; }

    public string? CurrencyCode { get; private set; }

    /// <summary>Deduction concept code — frozen at Apply through <see cref="Apply"/> (RN-06/aclaración №5).</summary>
    public string? DeductionConceptTypeCode { get; private set; }

    public string? DeductionConceptNameSnapshot { get; private set; }

    // ── Suspension block (OPTIONAL — only when the type applies it; future ranges allowed) ────────────
    public DateOnly? SuspensionStartDate { get; private set; }

    public DateOnly? SuspensionEndDate { get; private set; }

    /// <summary>Derived calendar-inclusive suspension days (mirrors <c>PersonnelTransactionRules.SuspensionDays</c>).</summary>
    public int? SuspensionDays { get; private set; }

    public Guid? AssignedPositionPublicId { get; private set; }

    public Guid RegisteredByUserId { get; private set; }

    public string StatusCode { get; private set; } = PersonnelTransactionStatuses.EnRevision;

    // ── Decision / annulment (same as recognition) ───────────────────────────────────────────────────
    public Guid? DecidedByUserId { get; private set; }

    public DateTime? DecidedUtc { get; private set; }

    public string? DecisionNote { get; private set; }

    public string? AnnulmentReason { get; private set; }

    public Guid? AnnulledByUserId { get; private set; }

    public DateTime? AnnulledUtc { get; private set; }

    /// <summary>The applied <c>AMONESTACION</c> entry's public id (captured on Apply).</summary>
    public Guid? PersonnelActionPublicId { get; private set; }

    /// <summary>The applied <c>SUSPENSION</c> entry's public id (captured on Apply when the suspension block travels).</summary>
    public Guid? SuspensionActionPublicId { get; private set; }

    public string? Notes { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    /// <summary>
    /// Creates a disciplinary action (initial status EN_REVISION). The type/cause ids must be positive, the
    /// facts detail non-empty, the suspension range coherent (both dates present together, start ≤ end) with the
    /// derived calendar-inclusive days, and the deduction amount —when it travels— positive. The
    /// suspension↔type-flag and deduction↔amount coherence live in the rules/handler (D-08).
    /// </summary>
    public static PersonnelFileDisciplinaryAction Create(
        long disciplinaryActionTypeId,
        string typeNameSnapshot,
        bool typeAppliedSuspension,
        long disciplinaryActionCauseId,
        string causeNameSnapshot,
        DateOnly incidentDate,
        string factsDetail,
        bool hasPayrollDeduction,
        decimal? deductionAmount,
        string? currencyCode,
        DateOnly? suspensionStartDate,
        DateOnly? suspensionEndDate,
        Guid? assignedPositionPublicId,
        Guid registeredByUserId,
        string? notes) =>
        new(
            disciplinaryActionTypeId,
            typeNameSnapshot,
            typeAppliedSuspension,
            disciplinaryActionCauseId,
            causeNameSnapshot,
            incidentDate,
            factsDetail,
            hasPayrollDeduction,
            deductionAmount,
            currencyCode,
            suspensionStartDate,
            suspensionEndDate,
            assignedPositionPublicId,
            registeredByUserId,
            notes);

    /// <summary>Edits the declarative fields while EN_REVISION (RN-01).</summary>
    public void Update(
        long disciplinaryActionTypeId,
        string typeNameSnapshot,
        bool typeAppliedSuspension,
        long disciplinaryActionCauseId,
        string causeNameSnapshot,
        DateOnly incidentDate,
        string factsDetail,
        bool hasPayrollDeduction,
        decimal? deductionAmount,
        string? currencyCode,
        DateOnly? suspensionStartDate,
        DateOnly? suspensionEndDate,
        Guid? assignedPositionPublicId,
        string? notes)
    {
        EnsureEditable();

        TypeAppliedSuspension = typeAppliedSuspension;
        ApplyDetails(
            disciplinaryActionTypeId,
            typeNameSnapshot,
            disciplinaryActionCauseId,
            causeNameSnapshot,
            incidentDate,
            factsDetail,
            hasPayrollDeduction,
            deductionAmount,
            currencyCode,
            suspensionStartDate,
            suspensionEndDate,
            assignedPositionPublicId,
            notes);
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Applies the disciplinary action (EN_REVISION → APLICADA); captures the generated entries' public ids and
    /// freezes the deduction concept snapshot (RN-06/aclaración №5). <paramref name="suspensionActionPublicId"/>
    /// is supplied only when the suspension block travels; <paramref name="conceptCode"/> /
    /// <paramref name="conceptName"/> only when the deduction travels.
    /// </summary>
    public void Apply(
        Guid byUserId,
        DateTime atUtc,
        Guid personnelActionPublicId,
        Guid? suspensionActionPublicId,
        string? conceptCode,
        string? conceptName)
    {
        EnsureEditable();

        if (personnelActionPublicId == Guid.Empty)
        {
            throw new ArgumentException("The personnel-action public id must not be empty.", nameof(personnelActionPublicId));
        }

        if (suspensionActionPublicId == Guid.Empty)
        {
            throw new ArgumentException("The suspension-action public id must not be empty when provided.", nameof(suspensionActionPublicId));
        }

        StatusCode = PersonnelTransactionStatuses.Aplicada;
        DecidedByUserId = byUserId;
        DecidedUtc = atUtc;
        PersonnelActionPublicId = personnelActionPublicId;
        SuspensionActionPublicId = suspensionActionPublicId;
        DeductionConceptTypeCode = TruncateOptional(
            PersonnelFileNormalization.CleanOptional(conceptCode)?.ToUpperInvariant(),
            MaxDeductionConceptTypeCodeLength,
            nameof(conceptCode));
        DeductionConceptNameSnapshot = TruncateOptional(
            PersonnelFileNormalization.CleanOptional(conceptName),
            MaxDeductionConceptNameSnapshotLength,
            nameof(conceptName));
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Rejects the disciplinary action (EN_REVISION → RECHAZADA); the note is mandatory (RN-07).</summary>
    public void Reject(Guid byUserId, DateTime atUtc, string note)
    {
        EnsureEditable();

        DecisionNote = Truncate(
            PersonnelFileNormalization.Clean(note, nameof(note)),
            MaxDecisionNoteLength,
            nameof(note));
        StatusCode = PersonnelTransactionStatuses.Rechazada;
        DecidedByUserId = byUserId;
        DecidedUtc = atUtc;
        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Annuls the record (terminal) from EN_REVISION (trámite withdrawal) or APLICADA (revocation); the reason
    /// is mandatory (RN-07). The caller annuls the linked entries via <c>PersonnelFilePersonnelAction.Annul()</c>.
    /// </summary>
    public void Annul(string reason, Guid byUserId, DateTime atUtc)
    {
        if (!PersonnelTransactionStatuses.Vigentes.Contains(StatusCode))
        {
            throw new InvalidOperationException("Only an EN_REVISION or APLICADA disciplinary action can be annulled.");
        }

        AnnulmentReason = Truncate(
            PersonnelFileNormalization.Clean(reason, nameof(reason)),
            MaxAnnulmentReasonLength,
            nameof(reason));
        AnnulledByUserId = byUserId;
        AnnulledUtc = atUtc;
        StatusCode = PersonnelTransactionStatuses.Anulada;
        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }

    private void ApplyDetails(
        long disciplinaryActionTypeId,
        string typeNameSnapshot,
        long disciplinaryActionCauseId,
        string causeNameSnapshot,
        DateOnly incidentDate,
        string factsDetail,
        bool hasPayrollDeduction,
        decimal? deductionAmount,
        string? currencyCode,
        DateOnly? suspensionStartDate,
        DateOnly? suspensionEndDate,
        Guid? assignedPositionPublicId,
        string? notes)
    {
        if (disciplinaryActionTypeId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(disciplinaryActionTypeId), "Disciplinary action type id must be positive.");
        }

        if (disciplinaryActionCauseId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(disciplinaryActionCauseId), "Disciplinary action cause id must be positive.");
        }

        if (deductionAmount is <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(deductionAmount), "The deduction amount must be greater than zero when provided.");
        }

        if (suspensionStartDate is null != (suspensionEndDate is null))
        {
            throw new ArgumentException("The suspension start and end dates must both be provided together or both omitted.", nameof(suspensionStartDate));
        }

        if (suspensionStartDate is { } start && suspensionEndDate is { } end && end < start)
        {
            throw new ArgumentException("The suspension end date cannot precede the start date.", nameof(suspensionEndDate));
        }

        if (assignedPositionPublicId == Guid.Empty)
        {
            throw new ArgumentException("The assigned position reference must not be empty when provided.", nameof(assignedPositionPublicId));
        }

        DisciplinaryActionTypeId = disciplinaryActionTypeId;
        TypeNameSnapshot = Truncate(
            PersonnelFileNormalization.Clean(typeNameSnapshot, nameof(typeNameSnapshot)),
            MaxTypeNameSnapshotLength,
            nameof(typeNameSnapshot));
        DisciplinaryActionCauseId = disciplinaryActionCauseId;
        CauseNameSnapshot = Truncate(
            PersonnelFileNormalization.Clean(causeNameSnapshot, nameof(causeNameSnapshot)),
            MaxCauseNameSnapshotLength,
            nameof(causeNameSnapshot));
        IncidentDate = incidentDate;
        FactsDetail = Truncate(
            PersonnelFileNormalization.Clean(factsDetail, nameof(factsDetail)),
            MaxFactsDetailLength,
            nameof(factsDetail));
        HasPayrollDeduction = hasPayrollDeduction;
        DeductionAmount = deductionAmount;
        CurrencyCode = TruncateOptional(
            PersonnelFileNormalization.CleanOptional(currencyCode)?.ToUpperInvariant(),
            MaxCurrencyCodeLength,
            nameof(currencyCode));
        SuspensionStartDate = suspensionStartDate;
        SuspensionEndDate = suspensionEndDate;
        SuspensionDays = suspensionStartDate is { } s && suspensionEndDate is { } e
            ? e.DayNumber - s.DayNumber + 1
            : null;
        AssignedPositionPublicId = assignedPositionPublicId;
        Notes = TruncateOptional(PersonnelFileNormalization.CleanOptional(notes), MaxNotesLength, nameof(notes));
    }

    private void EnsureEditable()
    {
        if (!PersonnelTransactionStatuses.IsEditable(StatusCode))
        {
            throw new InvalidOperationException("Only an EN_REVISION disciplinary action can be modified or decided.");
        }
    }

    private static string Truncate(string value, int maxLength, string paramName)
    {
        if (value.Length > maxLength)
        {
            throw new ArgumentException($"{paramName} must be {maxLength} characters or fewer.", paramName);
        }

        return value;
    }

    private static string? TruncateOptional(string? value, int maxLength, string paramName) =>
        value is null ? null : Truncate(value, maxLength, paramName);
}

/// <summary>
/// Supporting document ("diploma / memo") attached to a recognition (REQ-003 D-12/RF-005). Mirrors
/// <see cref="PersonnelFileIncapacityDocument"/> and reuses the shared file-storage subsystem
/// (<c>StoredFile</c> / <c>IFileStorageProvider</c> / <c>FilePurpose.RecognitionDocument</c>); the
/// document-type classification is OPTIONAL (nullable FK).
/// </summary>
public sealed class PersonnelFileRecognitionDocument : TenantEntity
{
    private PersonnelFileRecognitionDocument()
    {
    }

    private PersonnelFileRecognitionDocument(
        Guid publicId,
        long? documentTypeCatalogItemId,
        Guid filePublicId,
        string fileName,
        string contentType,
        int sizeBytes,
        string? observations)
    {
        if (documentTypeCatalogItemId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(documentTypeCatalogItemId), "Document type catalog item id must be positive.");
        }

        if (filePublicId == Guid.Empty)
        {
            throw new ArgumentException("File public id must not be empty.", nameof(filePublicId));
        }

        PublicId = publicId;
        DocumentTypeCatalogItemId = documentTypeCatalogItemId;
        FilePublicId = filePublicId;
        FileName = PersonnelFileNormalization.Clean(fileName, nameof(fileName));
        ContentType = PersonnelFileNormalization.Clean(contentType, nameof(contentType));
        SizeBytes = sizeBytes;
        Observations = PersonnelFileNormalization.CleanOptional(observations);
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public long RecognitionId { get; private set; }

    public PersonnelFileRecognition Recognition { get; private set; } = null!;

    public long? DocumentTypeCatalogItemId { get; private set; }

    public DocumentTypeCatalogs.DocumentTypeCatalogItem? DocumentTypeCatalogItem { get; private set; }

    public Guid FilePublicId { get; private set; }

    public string? Observations { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public int SizeBytes { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void BindToRecognition(long recognitionId) => RecognitionId = recognitionId;

    public static PersonnelFileRecognitionDocument Create(
        Guid publicId,
        long? documentTypeCatalogItemId,
        Guid filePublicId,
        string fileName,
        string contentType,
        int sizeBytes,
        string? observations) =>
        new(publicId, documentTypeCatalogItemId, filePublicId, fileName, contentType, sizeBytes, observations);

    public void ReplaceFileReference(Guid filePublicId, string fileName, string contentType, int sizeBytes)
    {
        if (filePublicId == Guid.Empty)
        {
            throw new ArgumentException("File public id must not be empty.", nameof(filePublicId));
        }

        FilePublicId = filePublicId;
        FileName = PersonnelFileNormalization.Clean(fileName, nameof(fileName));
        ContentType = PersonnelFileNormalization.Clean(contentType, nameof(contentType));
        SizeBytes = sizeBytes;
        ConcurrencyToken = Guid.NewGuid();
    }

    public void UpdateMetadata(long? documentTypeCatalogItemId, string? observations)
    {
        if (documentTypeCatalogItemId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(documentTypeCatalogItemId), "Document type catalog item id must be positive.");
        }

        DocumentTypeCatalogItemId = documentTypeCatalogItemId;
        Observations = PersonnelFileNormalization.CleanOptional(observations);
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public void Inactivate()
    {
        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }
}

/// <summary>
/// Supporting document ("acta / descargo") attached to a disciplinary action (REQ-003 D-12/RF-008). Mirrors
/// <see cref="PersonnelFileIncapacityDocument"/> and reuses the shared file-storage subsystem
/// (<c>StoredFile</c> / <c>IFileStorageProvider</c> / <c>FilePurpose.DisciplinaryActionDocument</c>); the
/// document-type classification is OPTIONAL (nullable FK).
/// </summary>
public sealed class PersonnelFileDisciplinaryActionDocument : TenantEntity
{
    private PersonnelFileDisciplinaryActionDocument()
    {
    }

    private PersonnelFileDisciplinaryActionDocument(
        Guid publicId,
        long? documentTypeCatalogItemId,
        Guid filePublicId,
        string fileName,
        string contentType,
        int sizeBytes,
        string? observations)
    {
        if (documentTypeCatalogItemId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(documentTypeCatalogItemId), "Document type catalog item id must be positive.");
        }

        if (filePublicId == Guid.Empty)
        {
            throw new ArgumentException("File public id must not be empty.", nameof(filePublicId));
        }

        PublicId = publicId;
        DocumentTypeCatalogItemId = documentTypeCatalogItemId;
        FilePublicId = filePublicId;
        FileName = PersonnelFileNormalization.Clean(fileName, nameof(fileName));
        ContentType = PersonnelFileNormalization.Clean(contentType, nameof(contentType));
        SizeBytes = sizeBytes;
        Observations = PersonnelFileNormalization.CleanOptional(observations);
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public long DisciplinaryActionId { get; private set; }

    public PersonnelFileDisciplinaryAction DisciplinaryAction { get; private set; } = null!;

    public long? DocumentTypeCatalogItemId { get; private set; }

    public DocumentTypeCatalogs.DocumentTypeCatalogItem? DocumentTypeCatalogItem { get; private set; }

    public Guid FilePublicId { get; private set; }

    public string? Observations { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public int SizeBytes { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void BindToDisciplinaryAction(long disciplinaryActionId) => DisciplinaryActionId = disciplinaryActionId;

    public static PersonnelFileDisciplinaryActionDocument Create(
        Guid publicId,
        long? documentTypeCatalogItemId,
        Guid filePublicId,
        string fileName,
        string contentType,
        int sizeBytes,
        string? observations) =>
        new(publicId, documentTypeCatalogItemId, filePublicId, fileName, contentType, sizeBytes, observations);

    public void ReplaceFileReference(Guid filePublicId, string fileName, string contentType, int sizeBytes)
    {
        if (filePublicId == Guid.Empty)
        {
            throw new ArgumentException("File public id must not be empty.", nameof(filePublicId));
        }

        FilePublicId = filePublicId;
        FileName = PersonnelFileNormalization.Clean(fileName, nameof(fileName));
        ContentType = PersonnelFileNormalization.Clean(contentType, nameof(contentType));
        SizeBytes = sizeBytes;
        ConcurrencyToken = Guid.NewGuid();
    }

    public void UpdateMetadata(long? documentTypeCatalogItemId, string? observations)
    {
        if (documentTypeCatalogItemId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(documentTypeCatalogItemId), "Document type catalog item id must be positive.");
        }

        DocumentTypeCatalogItemId = documentTypeCatalogItemId;
        Observations = PersonnelFileNormalization.CleanOptional(observations);
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public void Inactivate()
    {
        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }
}

using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.PersonnelFiles;

/// <summary>
/// Canonical status codes for a compensatory-time movement (credit or absence). The codes are validated
/// against the country-scoped <c>compensatory-time-statuses</c> catalog (visualization / i18n), but the
/// domain transition logic references these constants (REQ-002 D-15). Only a <see cref="Registrada"/>
/// movement joins the fund balance — an <see cref="Anulada"/> movement is excluded (RN-03/RN-06).
/// </summary>
public static class CompensatoryTimeStatuses
{
    public const string Registrada = "REGISTRADA";
    public const string Anulada = "ANULADA";

    /// <summary>Movements that still count toward the fund balance and the estado de cuenta.</summary>
    public static readonly IReadOnlyCollection<string> Vigentes = new[] { Registrada };

    /// <summary>Central predicate: ONLY a REGISTRADA movement counts toward the fund balance.</summary>
    public static bool IsVigente(string status) => status == Registrada;
}

/// <summary>
/// A compensatory-time credit ("acreditación de tiempo compensatorio", REQ-002 D-02/D-20): a declarative
/// record of hours worked outside the regular schedule that credit hours into the employee fund. It holds a
/// hard FK to the company-managed <see cref="Leave.CompensatoryTimeType"/> plus a snapshot of the type name
/// and the applied factor (RN-02, so editing the master factor never rewrites history), the worked date and
/// (optional, informational) time range, the worked hours and the credited hours
/// (<c>Round2(worked × factor)</c>, written exclusively through <see cref="ApplyCreditedHours"/> — the ONLY
/// calculation write path), the mandatory work detail + who authorized it, the optional authorizer file /
/// assigned position references, the <see cref="OvertimeRecordPublicId"/> seam to the future overtime module
/// (D-21, no FK), and the REGISTRADA → ANULADA lifecycle.
/// <para>The work-date ≤ today validation and the fund-balance invariant are NOT domain guards (they depend
/// on the clock / a cross-row aggregation) — they live in <c>CompensatoryTimeRules</c> and the handler under
/// an advisory lock.</para>
/// </summary>
public sealed class PersonnelFileCompensatoryTimeCredit : TenantEntity
{
    public const int MaxTypeNameSnapshotLength = 200;
    public const int MaxWorkDetailLength = 500;
    public const int MaxAuthorizedByTextLength = 200;
    public const int MaxOverrideNoteLength = 500;
    public const int MaxRegisteredByUserIdLength = 100;
    public const int MaxAnnulmentReasonLength = 500;
    public const int MaxNotesLength = 1000;

    private PersonnelFileCompensatoryTimeCredit()
    {
    }

    private PersonnelFileCompensatoryTimeCredit(
        long compensatoryTimeTypeId,
        string typeNameSnapshot,
        DateOnly workDate,
        TimeOnly? startTime,
        TimeOnly? endTime,
        decimal hoursWorked,
        string workDetail,
        string authorizedByText,
        Guid? authorizerFilePublicId,
        Guid? assignedPositionPublicId,
        Guid? overtimeRecordPublicId,
        string registeredByUserId,
        string? notes)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        IsActive = true;
        StatusCode = CompensatoryTimeStatuses.Registrada;

        ApplyDetails(
            compensatoryTimeTypeId,
            typeNameSnapshot,
            workDate,
            startTime,
            endTime,
            hoursWorked,
            workDetail,
            authorizedByText,
            authorizerFilePublicId,
            assignedPositionPublicId,
            overtimeRecordPublicId,
            notes);

        RegisteredByUserId = Truncate(
            PersonnelFileNormalization.Clean(registeredByUserId, nameof(registeredByUserId)),
            MaxRegisteredByUserIdLength,
            nameof(registeredByUserId));
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    // Hard FK to the type master + snapshot of the name (a later master edit never rewrites a credit).
    public long CompensatoryTimeTypeId { get; private set; }

    public Leave.CompensatoryTimeType? CompensatoryTimeType { get; private set; }

    public string TypeNameSnapshot { get; private set; } = string.Empty;

    public DateOnly WorkDate { get; private set; }

    /// <summary>Informational worked-time range (coherent only when both travel).</summary>
    public TimeOnly? StartTime { get; private set; }

    public TimeOnly? EndTime { get; private set; }

    public decimal HoursWorked { get; private set; }

    /// <summary>Snapshot of the type factor at registration (RN-02) — written via <see cref="ApplyCreditedHours"/>.</summary>
    public decimal FactorApplied { get; private set; }

    /// <summary>Credited hours = <c>Round2(worked × factor)</c> — written via <see cref="ApplyCreditedHours"/>.</summary>
    public decimal HoursCredited { get; private set; }

    /// <summary>True when the credited hours carry an audited manual adjustment (RN-02); the note is mandatory.</summary>
    public bool IsOverridden { get; private set; }

    public string? OverrideNote { get; private set; }

    public string WorkDetail { get; private set; } = string.Empty;

    public string AuthorizedByText { get; private set; } = string.Empty;

    public Guid? AuthorizerFilePublicId { get; private set; }

    public Guid? AssignedPositionPublicId { get; private set; }

    /// <summary>Seam for the future overtime module (D-21) — no FK, no validation beyond non-empty.</summary>
    public Guid? OvertimeRecordPublicId { get; private set; }

    public string RegisteredByUserId { get; private set; } = string.Empty;

    public string StatusCode { get; private set; } = CompensatoryTimeStatuses.Registrada;

    public string? AnnulmentReason { get; private set; }

    public string? AnnulledByUserId { get; private set; }

    public DateTime? AnnulledUtc { get; private set; }

    public string? Notes { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    /// <summary>
    /// Creates a compensatory-time credit (initial status REGISTRADA). The worked hours must be positive, the
    /// time range coherent (start &lt; end when both provided), and the work detail / authorized-by text
    /// non-empty. The credited hours are written afterwards through <see cref="ApplyCreditedHours"/>.
    /// </summary>
    public static PersonnelFileCompensatoryTimeCredit Create(
        long compensatoryTimeTypeId,
        string typeNameSnapshot,
        DateOnly workDate,
        TimeOnly? startTime,
        TimeOnly? endTime,
        decimal hoursWorked,
        string workDetail,
        string authorizedByText,
        Guid? authorizerFilePublicId,
        Guid? assignedPositionPublicId,
        Guid? overtimeRecordPublicId,
        string registeredByUserId,
        string? notes) =>
        new(
            compensatoryTimeTypeId,
            typeNameSnapshot,
            workDate,
            startTime,
            endTime,
            hoursWorked,
            workDetail,
            authorizedByText,
            authorizerFilePublicId,
            assignedPositionPublicId,
            overtimeRecordPublicId,
            registeredByUserId,
            notes);

    /// <summary>
    /// Writes the credited hours — the ONLY calculation write path (mirrors the incapacity ApplyCalculation
    /// idiom; the handler re-invokes it after every edit). <paramref name="factorApplied"/> is the type factor
    /// snapshot and must be positive; <paramref name="hoursCredited"/> is the final credited amount and must be
    /// positive. When <paramref name="isOverridden"/> is true the <paramref name="overrideNote"/> is mandatory.
    /// </summary>
    public void ApplyCreditedHours(decimal factorApplied, decimal hoursCredited, bool isOverridden, string? overrideNote)
    {
        EnsureRegistered();

        if (factorApplied <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(factorApplied), "Applied factor must be greater than zero.");
        }

        if (hoursCredited <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(hoursCredited), "Credited hours must be greater than zero.");
        }

        string? normalizedNote = null;
        if (isOverridden)
        {
            normalizedNote = Truncate(
                PersonnelFileNormalization.Clean(overrideNote!, nameof(overrideNote)),
                MaxOverrideNoteLength,
                nameof(overrideNote));
        }

        FactorApplied = factorApplied;
        HoursCredited = hoursCredited;
        IsOverridden = isOverridden;
        OverrideNote = normalizedNote;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Edits the declarative fields while REGISTRADA; the handler re-invokes <see cref="ApplyCreditedHours"/>.</summary>
    public void Update(
        long compensatoryTimeTypeId,
        string typeNameSnapshot,
        DateOnly workDate,
        TimeOnly? startTime,
        TimeOnly? endTime,
        decimal hoursWorked,
        string workDetail,
        string authorizedByText,
        Guid? authorizerFilePublicId,
        Guid? assignedPositionPublicId,
        Guid? overtimeRecordPublicId,
        string? notes)
    {
        EnsureRegistered();

        ApplyDetails(
            compensatoryTimeTypeId,
            typeNameSnapshot,
            workDate,
            startTime,
            endTime,
            hoursWorked,
            workDetail,
            authorizedByText,
            authorizerFilePublicId,
            assignedPositionPublicId,
            overtimeRecordPublicId,
            notes);
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Annuls the credit (terminal) from REGISTRADA; the reason is mandatory (RN-07).</summary>
    public void Annul(string reason, string byUserId, DateTime atUtc)
    {
        EnsureRegistered();

        AnnulmentReason = Truncate(
            PersonnelFileNormalization.Clean(reason, nameof(reason)),
            MaxAnnulmentReasonLength,
            nameof(reason));
        AnnulledByUserId = Truncate(
            PersonnelFileNormalization.Clean(byUserId, nameof(byUserId)),
            MaxRegisteredByUserIdLength,
            nameof(byUserId));
        AnnulledUtc = atUtc;
        StatusCode = CompensatoryTimeStatuses.Anulada;
        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }

    private void ApplyDetails(
        long compensatoryTimeTypeId,
        string typeNameSnapshot,
        DateOnly workDate,
        TimeOnly? startTime,
        TimeOnly? endTime,
        decimal hoursWorked,
        string workDetail,
        string authorizedByText,
        Guid? authorizerFilePublicId,
        Guid? assignedPositionPublicId,
        Guid? overtimeRecordPublicId,
        string? notes)
    {
        if (compensatoryTimeTypeId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(compensatoryTimeTypeId), "Compensatory-time type id must be positive.");
        }

        if (hoursWorked <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(hoursWorked), "Worked hours must be greater than zero.");
        }

        if (startTime is { } start && endTime is { } end && end <= start)
        {
            throw new ArgumentException("The end time must be later than the start time.", nameof(endTime));
        }

        if (authorizerFilePublicId == Guid.Empty)
        {
            throw new ArgumentException("The authorizer file reference must not be empty when provided.", nameof(authorizerFilePublicId));
        }

        if (assignedPositionPublicId == Guid.Empty)
        {
            throw new ArgumentException("The assigned position reference must not be empty when provided.", nameof(assignedPositionPublicId));
        }

        if (overtimeRecordPublicId == Guid.Empty)
        {
            throw new ArgumentException("The overtime record reference must not be empty when provided.", nameof(overtimeRecordPublicId));
        }

        CompensatoryTimeTypeId = compensatoryTimeTypeId;
        TypeNameSnapshot = Truncate(
            PersonnelFileNormalization.Clean(typeNameSnapshot, nameof(typeNameSnapshot)),
            MaxTypeNameSnapshotLength,
            nameof(typeNameSnapshot));
        WorkDate = workDate;
        StartTime = startTime;
        EndTime = endTime;
        HoursWorked = hoursWorked;
        WorkDetail = Truncate(
            PersonnelFileNormalization.Clean(workDetail, nameof(workDetail)),
            MaxWorkDetailLength,
            nameof(workDetail));
        AuthorizedByText = Truncate(
            PersonnelFileNormalization.Clean(authorizedByText, nameof(authorizedByText)),
            MaxAuthorizedByTextLength,
            nameof(authorizedByText));
        AuthorizerFilePublicId = authorizerFilePublicId;
        AssignedPositionPublicId = assignedPositionPublicId;
        OvertimeRecordPublicId = overtimeRecordPublicId;
        Notes = TruncateOptional(PersonnelFileNormalization.CleanOptional(notes), MaxNotesLength, nameof(notes));
    }

    private void EnsureRegistered()
    {
        if (StatusCode != CompensatoryTimeStatuses.Registrada)
        {
            throw new InvalidOperationException("Only a REGISTRADA compensatory-time credit can be modified.");
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
/// Authorization document ("documento de autorización de jefatura") attached to a compensatory-time credit
/// (REQ-002 D-20/RF-012). Exact mirror of <see cref="PersonnelFileIncapacityDocument"/> keyed on the credit
/// (<see cref="CreditId"/>); reuses the shared file-storage subsystem
/// (<c>StoredFile</c> / <c>IFileStorageProvider</c> / <c>FilePurpose.CompensatoryTimeDocument</c>). The
/// document-type classification is OPTIONAL (nullable FK).
/// </summary>
public sealed class PersonnelFileCompensatoryTimeCreditDocument : TenantEntity
{
    private PersonnelFileCompensatoryTimeCreditDocument()
    {
    }

    private PersonnelFileCompensatoryTimeCreditDocument(
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

    public long CreditId { get; private set; }

    public PersonnelFileCompensatoryTimeCredit Credit { get; private set; } = null!;

    public long? DocumentTypeCatalogItemId { get; private set; }

    public DocumentTypeCatalogs.DocumentTypeCatalogItem? DocumentTypeCatalogItem { get; private set; }

    public Guid FilePublicId { get; private set; }

    public string? Observations { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public int SizeBytes { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void BindToCredit(long creditId) => CreditId = creditId;

    public static PersonnelFileCompensatoryTimeCreditDocument Create(
        Guid publicId,
        long? documentTypeCatalogItemId,
        Guid filePublicId,
        string fileName,
        string contentType,
        int sizeBytes,
        string? observations) =>
        new(publicId, documentTypeCatalogItemId, filePublicId, fileName, contentType, sizeBytes, observations);

    public void ReplaceFileReference(
        Guid filePublicId,
        string fileName,
        string contentType,
        int sizeBytes)
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

    public void UpdateMetadata(
        long? documentTypeCatalogItemId,
        string? observations)
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
/// A compensatory-time absence ("ausencia / goce de tiempo compensatorio", REQ-002 D-03): a record that
/// debits hours from the employee fund. It holds a hard FK to the company-managed
/// <see cref="Leave.CompensatoryTimeType"/> plus the type-name snapshot, the date range (start ≤ end,
/// future ranges allowed), the debited hours, the mandatory reason, an optional payroll-period imputation
/// reference and the REGISTRADA → ANULADA lifecycle.
/// <para>The fund-balance invariant (debit ≤ balance) is NOT a domain guard — it is re-verified in the
/// handler under an advisory lock (RN-03).</para>
/// </summary>
public sealed class PersonnelFileCompensatoryTimeAbsence : TenantEntity
{
    public const int MaxTypeNameSnapshotLength = 200;
    public const int MaxReasonLength = 500;
    public const int MaxRegisteredByUserIdLength = 100;
    public const int MaxAnnulmentReasonLength = 500;
    public const int MaxNotesLength = 1000;

    private PersonnelFileCompensatoryTimeAbsence()
    {
    }

    private PersonnelFileCompensatoryTimeAbsence(
        long compensatoryTimeTypeId,
        string typeNameSnapshot,
        DateOnly startDate,
        DateOnly endDate,
        decimal hoursDebited,
        string reason,
        Guid? payrollPeriodPublicId,
        string registeredByUserId,
        string? notes)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        IsActive = true;
        StatusCode = CompensatoryTimeStatuses.Registrada;

        ApplyDetails(
            compensatoryTimeTypeId,
            typeNameSnapshot,
            startDate,
            endDate,
            hoursDebited,
            reason,
            payrollPeriodPublicId,
            notes);

        RegisteredByUserId = Truncate(
            PersonnelFileNormalization.Clean(registeredByUserId, nameof(registeredByUserId)),
            MaxRegisteredByUserIdLength,
            nameof(registeredByUserId));
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    public long CompensatoryTimeTypeId { get; private set; }

    public Leave.CompensatoryTimeType? CompensatoryTimeType { get; private set; }

    public string TypeNameSnapshot { get; private set; } = string.Empty;

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public decimal HoursDebited { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    /// <summary>Optional payroll-period imputation (a reference, not a containment — P-14).</summary>
    public Guid? PayrollPeriodPublicId { get; private set; }

    public string RegisteredByUserId { get; private set; } = string.Empty;

    public string StatusCode { get; private set; } = CompensatoryTimeStatuses.Registrada;

    public string? AnnulmentReason { get; private set; }

    public string? AnnulledByUserId { get; private set; }

    public DateTime? AnnulledUtc { get; private set; }

    public string? Notes { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    /// <summary>
    /// Creates a compensatory-time absence (initial status REGISTRADA). The start date must not be after the
    /// end date (future ranges are allowed), the debited hours must be positive, and the reason non-empty.
    /// </summary>
    public static PersonnelFileCompensatoryTimeAbsence Create(
        long compensatoryTimeTypeId,
        string typeNameSnapshot,
        DateOnly startDate,
        DateOnly endDate,
        decimal hoursDebited,
        string reason,
        Guid? payrollPeriodPublicId,
        string registeredByUserId,
        string? notes) =>
        new(
            compensatoryTimeTypeId,
            typeNameSnapshot,
            startDate,
            endDate,
            hoursDebited,
            reason,
            payrollPeriodPublicId,
            registeredByUserId,
            notes);

    /// <summary>Edits the declarative fields while REGISTRADA.</summary>
    public void Update(
        long compensatoryTimeTypeId,
        string typeNameSnapshot,
        DateOnly startDate,
        DateOnly endDate,
        decimal hoursDebited,
        string reason,
        Guid? payrollPeriodPublicId,
        string? notes)
    {
        EnsureRegistered();

        ApplyDetails(
            compensatoryTimeTypeId,
            typeNameSnapshot,
            startDate,
            endDate,
            hoursDebited,
            reason,
            payrollPeriodPublicId,
            notes);
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Annuls the absence (terminal) from REGISTRADA; the reason is mandatory (RN-07).</summary>
    public void Annul(string reason, string byUserId, DateTime atUtc)
    {
        EnsureRegistered();

        AnnulmentReason = Truncate(
            PersonnelFileNormalization.Clean(reason, nameof(reason)),
            MaxAnnulmentReasonLength,
            nameof(reason));
        AnnulledByUserId = Truncate(
            PersonnelFileNormalization.Clean(byUserId, nameof(byUserId)),
            MaxRegisteredByUserIdLength,
            nameof(byUserId));
        AnnulledUtc = atUtc;
        StatusCode = CompensatoryTimeStatuses.Anulada;
        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }

    private void ApplyDetails(
        long compensatoryTimeTypeId,
        string typeNameSnapshot,
        DateOnly startDate,
        DateOnly endDate,
        decimal hoursDebited,
        string reason,
        Guid? payrollPeriodPublicId,
        string? notes)
    {
        if (compensatoryTimeTypeId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(compensatoryTimeTypeId), "Compensatory-time type id must be positive.");
        }

        if (endDate < startDate)
        {
            throw new ArgumentException("The end date cannot precede the start date.", nameof(endDate));
        }

        if (hoursDebited <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(hoursDebited), "Debited hours must be greater than zero.");
        }

        if (payrollPeriodPublicId == Guid.Empty)
        {
            throw new ArgumentException("The payroll period reference must not be empty when provided.", nameof(payrollPeriodPublicId));
        }

        CompensatoryTimeTypeId = compensatoryTimeTypeId;
        TypeNameSnapshot = Truncate(
            PersonnelFileNormalization.Clean(typeNameSnapshot, nameof(typeNameSnapshot)),
            MaxTypeNameSnapshotLength,
            nameof(typeNameSnapshot));
        StartDate = startDate;
        EndDate = endDate;
        HoursDebited = hoursDebited;
        Reason = Truncate(
            PersonnelFileNormalization.Clean(reason, nameof(reason)),
            MaxReasonLength,
            nameof(reason));
        PayrollPeriodPublicId = payrollPeriodPublicId;
        Notes = TruncateOptional(PersonnelFileNormalization.CleanOptional(notes), MaxNotesLength, nameof(notes));
    }

    private void EnsureRegistered()
    {
        if (StatusCode != CompensatoryTimeStatuses.Registrada)
        {
            throw new InvalidOperationException("Only a REGISTRADA compensatory-time absence can be modified.");
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

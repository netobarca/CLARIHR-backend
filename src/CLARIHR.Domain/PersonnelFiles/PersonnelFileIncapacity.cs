using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.PersonnelFiles;

/// <summary>
/// Canonical status codes for an employee incapacity ("incapacidad"). The codes are validated against the
/// country-scoped <c>incapacity-statuses</c> catalog (visualization / i18n), but the domain transition logic
/// references these constants (vacaciones/incapacidades module D-16). Self-service registrations start in
/// EN_REVISION and only count for caps/balances/exports once HR confirms them (R-T6).
/// </summary>
public static class IncapacityStatuses
{
    public const string EnRevision = "EN_REVISION";
    public const string Registrada = "REGISTRADA";
    public const string Anulada = "ANULADA";

    /// <summary>States from which HR may confirm the registration.</summary>
    public static readonly IReadOnlyCollection<string> Confirmable = new[] { EnRevision };

    /// <summary>States from which the record may still be annulled.</summary>
    public static readonly IReadOnlyCollection<string> Annullable = new[] { EnRevision, Registrada };

    /// <summary>
    /// Central R-T6 predicate: ONLY a REGISTRADA incapacity counts toward the employer cap, the profile
    /// balances and the payroll exports. EN_REVISION never joins those aggregates.
    /// </summary>
    public static bool CountsAsRegistered(string status) => status == Registrada;
}

/// <summary>
/// Constrained origin codes for an incapacity registration: created by HR (immediately REGISTRADA) or
/// self-registered by the employee (EN_REVISION until HR confirms — D-18).
/// </summary>
public static class IncapacityOrigins
{
    public const string Rrhh = "RRHH";
    public const string Autoservicio = "AUTOSERVICIO";

    public static readonly IReadOnlyCollection<string> All = new[] { Rrhh, Autoservicio };

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim().ToUpperInvariant());
}

/// <summary>
/// Engine output applied onto a <see cref="PersonnelFileIncapacity"/> via
/// <see cref="PersonnelFileIncapacity.ApplyCalculation"/> — the ONLY write path of the day/amount breakdown
/// (mirrors the settlement ApplyCalculation idiom). <paramref name="TrancheDetailJson"/> carries the per-tranche
/// audit trail (absolute chain range, percent, payer, days, amount).
/// </summary>
public sealed record IncapacityCalculationSnapshot(
    int CalendarDays,
    int ComputableDays,
    int SubsidizedDays,
    int DiscountDays,
    int EmployerDays,
    decimal MonthlyBaseSalary,
    decimal DailySalary,
    decimal SubsidyAmount,
    decimal DiscountAmount,
    decimal EmployerAmount,
    string? TrancheDetailJson);

/// <summary>
/// An employee incapacity record ("incapacidad"): a hard FK to the company-managed
/// <c>IncapacityRisk</c> master plus a snapshot of the risk code and the five flags that drove the
/// day-counting/consumption at registration time (later master edits never rewrite history), optional
/// references to the clinic / incapacity type / plaza / payroll period, the date range (an open-ended
/// record is allowed only when the risk permits it — D-11), the engine's day/amount breakdown (written
/// exclusively through <see cref="ApplyCalculation"/>), the EN_REVISION → REGISTRADA → ANULADA lifecycle
/// and the extension chain (<see cref="ExtendsIncapacityId"/> — RN-03: an extension continues the tranche
/// numbering of its chain).
/// </summary>
public sealed class PersonnelFileIncapacity : TenantEntity
{
    private PersonnelFileIncapacity()
    {
    }

    private PersonnelFileIncapacity(
        Guid? requesterFilePublicId,
        string? requesterNameSnapshot,
        string requestedByUserId,
        string originCode,
        long incapacityRiskId,
        string riskCodeSnapshot,
        bool riskCountsSeventhDaySnapshot,
        bool riskCountsSaturdaySnapshot,
        bool riskCountsHolidaySnapshot,
        bool riskUsesFundSnapshot,
        bool riskHasSubsidySnapshot,
        long? medicalClinicId,
        long incapacityTypeId,
        Guid? assignedPositionPublicId,
        string? payrollTypeCode,
        long? payrollPeriodDefinitionId,
        DateOnly startDate,
        DateOnly? endDate,
        long? extendsIncapacityId,
        string? notes,
        bool riskAllowsIndefinite)
    {
        PublicId = Guid.NewGuid();
        ConcurrencyToken = Guid.NewGuid();
        IsActive = true;

        RequesterFilePublicId = requesterFilePublicId;
        RequesterNameSnapshot = PersonnelFileNormalization.CleanOptional(requesterNameSnapshot);
        RequestedByUserId = PersonnelFileNormalization.Clean(requestedByUserId, nameof(requestedByUserId));

        var normalizedOrigin = PersonnelFileNormalization.Clean(originCode, nameof(originCode)).ToUpperInvariant();
        if (!IncapacityOrigins.All.Contains(normalizedOrigin))
        {
            throw new ArgumentException(
                $"Origin code '{originCode}' is not supported. Allowed codes: {string.Join(", ", IncapacityOrigins.All)}.",
                nameof(originCode));
        }

        OriginCode = normalizedOrigin;
        StatusCode = normalizedOrigin == IncapacityOrigins.Autoservicio
            ? IncapacityStatuses.EnRevision
            : IncapacityStatuses.Registrada;

        ApplyReferences(
            incapacityRiskId,
            riskCodeSnapshot,
            riskCountsSeventhDaySnapshot,
            riskCountsSaturdaySnapshot,
            riskCountsHolidaySnapshot,
            riskUsesFundSnapshot,
            riskHasSubsidySnapshot,
            medicalClinicId,
            incapacityTypeId,
            assignedPositionPublicId,
            payrollTypeCode,
            payrollPeriodDefinitionId,
            extendsIncapacityId);
        ApplyDates(startDate, endDate, riskAllowsIndefinite);
        Notes = PersonnelFileNormalization.CleanOptional(notes);
    }

    public long PersonnelFileId { get; private set; }

    public PersonnelFile PersonnelFile { get; private set; } = null!;

    // Requester trío (D-18): the employee file that asked (self-service) + audit of who typed it.
    public Guid? RequesterFilePublicId { get; private set; }

    public string? RequesterNameSnapshot { get; private set; }

    public string RequestedByUserId { get; private set; } = string.Empty;

    public string OriginCode { get; private set; } = string.Empty;

    // Hard FK to the risk master + snapshot of the code and the five flags that drive calculation/consumption
    // (a later master edit never rewrites an already-registered incapacity).
    public long IncapacityRiskId { get; private set; }

    public Leave.IncapacityRisk? IncapacityRisk { get; private set; }

    public string RiskCodeSnapshot { get; private set; } = string.Empty;

    public bool RiskCountsSeventhDaySnapshot { get; private set; }

    public bool RiskCountsSaturdaySnapshot { get; private set; }

    public bool RiskCountsHolidaySnapshot { get; private set; }

    public bool RiskUsesFundSnapshot { get; private set; }

    public bool RiskHasSubsidySnapshot { get; private set; }

    public long? MedicalClinicId { get; private set; }

    public Leave.MedicalClinic? MedicalClinic { get; private set; }

    public long IncapacityTypeId { get; private set; }

    public Leave.IncapacityType? IncapacityType { get; private set; }

    public Guid? AssignedPositionPublicId { get; private set; }

    public string? PayrollTypeCode { get; private set; }

    public long? PayrollPeriodDefinitionId { get; private set; }

    public Leave.PayrollPeriodDefinition? PayrollPeriodDefinition { get; private set; }

    public DateOnly StartDate { get; private set; }

    /// <summary>Null = open-ended incapacity (allowed only when the risk permits it — D-11).</summary>
    public DateOnly? EndDate { get; private set; }

    // ── Day counts (engine output; ComputableDays may carry an audited manual override — RN-07).
    public int CalendarDays { get; private set; }

    public int ComputableDays { get; private set; }

    public bool ComputableDaysOverridden { get; private set; }

    public string? OverrideNote { get; private set; }

    // ── Breakdown snapshot (engine output — written exclusively through ApplyCalculation).
    public int SubsidizedDays { get; private set; }

    public int DiscountDays { get; private set; }

    public int EmployerDays { get; private set; }

    public decimal MonthlyBaseSalary { get; private set; }

    public decimal DailySalary { get; private set; }

    public decimal SubsidyAmount { get; private set; }

    public decimal DiscountAmount { get; private set; }

    public decimal EmployerAmount { get; private set; }

    /// <summary>Per-tranche audit trail (absolute chain range, percent, payer, days, amount) — jsonb.</summary>
    public string? TrancheDetailJson { get; private set; }

    public string StatusCode { get; private set; } = IncapacityStatuses.Registrada;

    /// <summary>Extension chain (RN-03): the incapacity this record extends ("prórroga").</summary>
    public long? ExtendsIncapacityId { get; private set; }

    public PersonnelFileIncapacity? ExtendsIncapacity { get; private set; }

    public string? ConfirmedByUserId { get; private set; }

    public DateTime? ConfirmedAtUtc { get; private set; }

    public string? AnnulmentReason { get; private set; }

    public DateTime? AnnulledAtUtc { get; private set; }

    public string? Notes { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void BindToPersonnelFile(long personnelFileId) => PersonnelFileId = personnelFileId;

    /// <summary>
    /// Creates an incapacity registration. The date range must be coherent; an open-ended record
    /// (<paramref name="endDate"/> null) is allowed only when the risk allows an indefinite incapacity
    /// (<paramref name="riskAllowsIndefinite"/> travels as a validation flag, it is not persisted). The
    /// initial status derives from the origin: AUTOSERVICIO → EN_REVISION, RRHH → REGISTRADA (D-18).
    /// </summary>
    public static PersonnelFileIncapacity Create(
        Guid? requesterFilePublicId,
        string? requesterNameSnapshot,
        string requestedByUserId,
        string originCode,
        long incapacityRiskId,
        string riskCodeSnapshot,
        bool riskCountsSeventhDaySnapshot,
        bool riskCountsSaturdaySnapshot,
        bool riskCountsHolidaySnapshot,
        bool riskUsesFundSnapshot,
        bool riskHasSubsidySnapshot,
        long? medicalClinicId,
        long incapacityTypeId,
        Guid? assignedPositionPublicId,
        string? payrollTypeCode,
        long? payrollPeriodDefinitionId,
        DateOnly startDate,
        DateOnly? endDate,
        long? extendsIncapacityId,
        string? notes,
        bool riskAllowsIndefinite) =>
        new(
            requesterFilePublicId,
            requesterNameSnapshot,
            requestedByUserId,
            originCode,
            incapacityRiskId,
            riskCodeSnapshot,
            riskCountsSeventhDaySnapshot,
            riskCountsSaturdaySnapshot,
            riskCountsHolidaySnapshot,
            riskUsesFundSnapshot,
            riskHasSubsidySnapshot,
            medicalClinicId,
            incapacityTypeId,
            assignedPositionPublicId,
            payrollTypeCode,
            payrollPeriodDefinitionId,
            startDate,
            endDate,
            extendsIncapacityId,
            notes,
            riskAllowsIndefinite);

    /// <summary>
    /// Writes the engine's day/amount breakdown — the ONLY write path of the counts and amounts (mirrors
    /// the settlement ApplyCalculation idiom; the handler re-invokes it after every recalculation).
    /// </summary>
    public void ApplyCalculation(IncapacityCalculationSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        EnsureNotAnnulled();

        CalendarDays = snapshot.CalendarDays;
        ComputableDays = snapshot.ComputableDays;
        SubsidizedDays = snapshot.SubsidizedDays;
        DiscountDays = snapshot.DiscountDays;
        EmployerDays = snapshot.EmployerDays;
        MonthlyBaseSalary = snapshot.MonthlyBaseSalary;
        DailySalary = snapshot.DailySalary;
        SubsidyAmount = snapshot.SubsidyAmount;
        DiscountAmount = snapshot.DiscountAmount;
        EmployerAmount = snapshot.EmployerAmount;
        TrancheDetailJson = PersonnelFileNormalization.CleanOptional(snapshot.TrancheDetailJson);
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Edits the master references, dates and notes while the record is not annulled. The recalculation is
    /// the handler's responsibility (it re-invokes <see cref="ApplyCalculation"/> afterwards).
    /// </summary>
    public void UpdateDetails(
        long incapacityRiskId,
        string riskCodeSnapshot,
        bool riskCountsSeventhDaySnapshot,
        bool riskCountsSaturdaySnapshot,
        bool riskCountsHolidaySnapshot,
        bool riskUsesFundSnapshot,
        bool riskHasSubsidySnapshot,
        long? medicalClinicId,
        long incapacityTypeId,
        Guid? assignedPositionPublicId,
        string? payrollTypeCode,
        long? payrollPeriodDefinitionId,
        DateOnly startDate,
        DateOnly? endDate,
        long? extendsIncapacityId,
        string? notes,
        bool riskAllowsIndefinite)
    {
        EnsureNotAnnulled();

        ApplyReferences(
            incapacityRiskId,
            riskCodeSnapshot,
            riskCountsSeventhDaySnapshot,
            riskCountsSaturdaySnapshot,
            riskCountsHolidaySnapshot,
            riskUsesFundSnapshot,
            riskHasSubsidySnapshot,
            medicalClinicId,
            incapacityTypeId,
            assignedPositionPublicId,
            payrollTypeCode,
            payrollPeriodDefinitionId,
            extendsIncapacityId);
        ApplyDates(startDate, endDate, riskAllowsIndefinite);
        Notes = PersonnelFileNormalization.CleanOptional(notes);
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>HR confirmation of a self-service registration: EN_REVISION → REGISTRADA (R-T6).</summary>
    public void Confirm(string byUserId, DateTime atUtc)
    {
        var normalizedByUserId = PersonnelFileNormalization.Clean(byUserId, nameof(byUserId));
        if (!IncapacityStatuses.Confirmable.Contains(StatusCode))
        {
            throw new InvalidOperationException("Only an EN_REVISION incapacity can be confirmed.");
        }

        StatusCode = IncapacityStatuses.Registrada;
        ConfirmedByUserId = normalizedByUserId;
        ConfirmedAtUtc = atUtc;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Closes an open-ended incapacity by fixing its end date (D-11). The recalculation of the breakdown
    /// comes from the handler afterwards.
    /// </summary>
    public void CloseIndefinite(DateOnly endDate)
    {
        EnsureNotAnnulled();

        if (EndDate is not null)
        {
            throw new InvalidOperationException("Only an open-ended incapacity (without an end date) can be closed.");
        }

        if (endDate < StartDate)
        {
            throw new ArgumentException("The end date cannot precede the start date.", nameof(endDate));
        }

        EndDate = endDate;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Annuls the record (terminal) from EN_REVISION or REGISTRADA; the reason is mandatory.</summary>
    public void Annul(string reason, DateTime atUtc)
    {
        var normalizedReason = PersonnelFileNormalization.Clean(reason, nameof(reason));
        if (!IncapacityStatuses.Annullable.Contains(StatusCode))
        {
            throw new InvalidOperationException("Only an EN_REVISION or REGISTRADA incapacity can be annulled.");
        }

        StatusCode = IncapacityStatuses.Anulada;
        AnnulmentReason = normalizedReason;
        AnnulledAtUtc = atUtc;
        IsActive = false;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>Audited manual override of the computable days (RN-07): the note is mandatory.</summary>
    public void OverrideComputableDays(int value, string note)
    {
        EnsureNotAnnulled();

        var normalizedNote = PersonnelFileNormalization.Clean(note, nameof(note));
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Computable days cannot be negative.");
        }

        ComputableDays = value;
        ComputableDaysOverridden = true;
        OverrideNote = normalizedNote;
        ConcurrencyToken = Guid.NewGuid();
    }

    private void EnsureNotAnnulled()
    {
        if (StatusCode == IncapacityStatuses.Anulada)
        {
            throw new InvalidOperationException("An annulled incapacity cannot be modified.");
        }
    }

    private void ApplyReferences(
        long incapacityRiskId,
        string riskCodeSnapshot,
        bool riskCountsSeventhDaySnapshot,
        bool riskCountsSaturdaySnapshot,
        bool riskCountsHolidaySnapshot,
        bool riskUsesFundSnapshot,
        bool riskHasSubsidySnapshot,
        long? medicalClinicId,
        long incapacityTypeId,
        Guid? assignedPositionPublicId,
        string? payrollTypeCode,
        long? payrollPeriodDefinitionId,
        long? extendsIncapacityId)
    {
        if (incapacityRiskId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(incapacityRiskId), "Incapacity risk id must be positive.");
        }

        if (incapacityTypeId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(incapacityTypeId), "Incapacity type id must be positive.");
        }

        if (medicalClinicId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(medicalClinicId), "Medical clinic id must be positive.");
        }

        if (payrollPeriodDefinitionId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(payrollPeriodDefinitionId), "Payroll period definition id must be positive.");
        }

        if (extendsIncapacityId is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(extendsIncapacityId), "Extended incapacity id must be positive.");
        }

        if (assignedPositionPublicId == Guid.Empty)
        {
            throw new ArgumentException("The assigned position reference must not be empty when provided.", nameof(assignedPositionPublicId));
        }

        IncapacityRiskId = incapacityRiskId;
        RiskCodeSnapshot = PersonnelFileNormalization.Clean(riskCodeSnapshot, nameof(riskCodeSnapshot)).ToUpperInvariant();
        RiskCountsSeventhDaySnapshot = riskCountsSeventhDaySnapshot;
        RiskCountsSaturdaySnapshot = riskCountsSaturdaySnapshot;
        RiskCountsHolidaySnapshot = riskCountsHolidaySnapshot;
        RiskUsesFundSnapshot = riskUsesFundSnapshot;
        RiskHasSubsidySnapshot = riskHasSubsidySnapshot;
        MedicalClinicId = medicalClinicId;
        IncapacityTypeId = incapacityTypeId;
        AssignedPositionPublicId = assignedPositionPublicId;
        PayrollTypeCode = PersonnelFileNormalization.CleanOptional(payrollTypeCode)?.ToUpperInvariant();
        PayrollPeriodDefinitionId = payrollPeriodDefinitionId;
        ExtendsIncapacityId = extendsIncapacityId;
    }

    private void ApplyDates(DateOnly startDate, DateOnly? endDate, bool riskAllowsIndefinite)
    {
        if (endDate is null && !riskAllowsIndefinite)
        {
            throw new ArgumentException(
                "An open-ended incapacity requires a risk that allows an indefinite end date.",
                nameof(endDate));
        }

        if (endDate is { } end && end < startDate)
        {
            throw new ArgumentException("The end date cannot precede the start date.", nameof(endDate));
        }

        StartDate = startDate;
        EndDate = endDate;
    }
}

/// <summary>
/// Supporting document ("constancia de incapacidad") attached to an incapacity (D-22/RF-011). Mirrors
/// <see cref="MedicalClaimDocument"/> and reuses the shared file-storage subsystem
/// (<c>StoredFile</c> / <c>IFileStorageProvider</c> / <c>FilePurpose.IncapacityDocument</c>); the
/// document-type classification is OPTIONAL (nullable FK — plan §3.4).
/// </summary>
public sealed class PersonnelFileIncapacityDocument : TenantEntity
{
    private PersonnelFileIncapacityDocument()
    {
    }

    private PersonnelFileIncapacityDocument(
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

    public long PersonnelFileIncapacityId { get; private set; }

    public PersonnelFileIncapacity Incapacity { get; private set; } = null!;

    public long? DocumentTypeCatalogItemId { get; private set; }

    public DocumentTypeCatalogs.DocumentTypeCatalogItem? DocumentTypeCatalogItem { get; private set; }

    public Guid FilePublicId { get; private set; }

    public string? Observations { get; private set; }

    public string FileName { get; private set; } = string.Empty;

    public string ContentType { get; private set; } = string.Empty;

    public int SizeBytes { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public void BindToIncapacity(long personnelFileIncapacityId) => PersonnelFileIncapacityId = personnelFileIncapacityId;

    public static PersonnelFileIncapacityDocument Create(
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

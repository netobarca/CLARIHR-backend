using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.PositionSlots;

public sealed class PositionSlot : TenantEntity
{
    private PositionSlot()
    {
    }

    private PositionSlot(
        Guid publicId,
        string code,
        string? title,
        long jobProfileId,
        long? roleId,
        long? workCenterId,
        long? directDependencyPositionSlotId,
        long? functionalDependencyPositionSlotId,
        PositionSlotStatus status,
        int maxEmployees,
        bool isFixedTerm,
        bool generatesOvertime,
        DateTime effectiveFromUtc,
        DateTime? effectiveToUtc,
        string? notes)
    {
        EnsurePositiveId(jobProfileId, nameof(jobProfileId));
        if (workCenterId.HasValue)
        {
            EnsurePositiveId(workCenterId.Value, nameof(workCenterId));
        }

        if (roleId.HasValue)
        {
            EnsurePositiveId(roleId.Value, nameof(roleId));
        }

        ValidateCapacity(maxEmployees);
        ValidateDateRange(effectiveFromUtc, effectiveToUtc);

        PublicId = publicId;
        SetCode(code);
        Title = PositionSlotNormalization.CleanOptional(title);
        JobProfileId = jobProfileId;
        RoleId = roleId;
        WorkCenterId = workCenterId;
        DirectDependencyPositionSlotId = directDependencyPositionSlotId;
        FunctionalDependencyPositionSlotId = functionalDependencyPositionSlotId;
        MaxEmployees = maxEmployees;
        IsFixedTerm = isFixedTerm;
        GeneratesOvertime = generatesOvertime;
        EffectiveFromUtc = effectiveFromUtc;
        EffectiveToUtc = effectiveToUtc;
        Notes = PositionSlotNormalization.CleanOptional(notes);

        // H-23 — `Vacant`/`Occupied` are no longer persisted: they are FACTS about the assignments, derived on
        // read. The only thing a caller decides at create time is whether the slot is already retired, and that
        // is exactly what `IsActive` records. The old pair (status + occupancy counter) could contradict each
        // other, and the aggregate had to arbitrate between two numbers nobody maintained.
        IsActive = status != PositionSlotStatus.Suspended;
        ConcurrencyToken = Guid.NewGuid();
    }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string? Title { get; private set; }

    public long JobProfileId { get; private set; }

    public long? RoleId { get; private set; }

    public long? WorkCenterId { get; private set; }

    public long? DirectDependencyPositionSlotId { get; private set; }

    public long? FunctionalDependencyPositionSlotId { get; private set; }

    public int MaxEmployees { get; private set; }

    public bool IsFixedTerm { get; private set; }

    /// <summary>
    /// H-19/H-20 — whether work in this plaza can generate overtime. Lives on the POSITION, not on the person:
    /// a directorship generates no overtime whoever holds it, and the rule has to survive a change of incumbent.
    /// It also has to be per-plaza rather than per-employee because one employee can hold several plazas at once
    /// (multi-plaza is in use in the real data), one exempt and one not.
    /// <para>
    /// Defaults to <c>true</c> on purpose: most positions do generate overtime, and a new plaza must not become
    /// exempt by omission. Exemption is the exception and has to be declared.
    /// </para>
    /// <para>
    /// This is also what disambiguates a missing work schedule. Before it, <c>workdayCode = null</c> meant either
    /// "a director deliberately without a shift" or "somebody forgot to configure it", and the system could not
    /// tell them apart — so it did the worst thing for both and stayed silent.
    /// </para>
    /// </summary>
    public bool GeneratesOvertime { get; private set; }

    public DateTime EffectiveFromUtc { get; private set; }

    public DateTime? EffectiveToUtc { get; private set; }

    public string? Notes { get; private set; }

    // §D-02 nivel 2 (R-2): configured/budgeted reference salary of the plaza. Informational only; the
    // negotiated salary is validated against the job profile's tabulator range, not this value.
    public decimal? ConfiguredBaseSalary { get; private set; }

    public string? ConfiguredBaseSalaryCurrencyCode { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static PositionSlot Create(
        string code,
        string? title,
        long jobProfileId,
        long? roleId,
        long? workCenterId,
        long? directDependencyPositionSlotId,
        long? functionalDependencyPositionSlotId,
        PositionSlotStatus status,
        int maxEmployees,
        bool isFixedTerm,
        bool generatesOvertime,
        DateTime effectiveFromUtc,
        DateTime? effectiveToUtc,
        string? notes) =>
        new(
            Guid.NewGuid(),
            code,
            title,
            jobProfileId,
            roleId,
            workCenterId,
            directDependencyPositionSlotId,
            functionalDependencyPositionSlotId,
            status,
            maxEmployees,
            isFixedTerm,
            generatesOvertime,
            effectiveFromUtc,
            effectiveToUtc,
            notes);

    public void UpdateCore(
        string code,
        string? title,
        long jobProfileId,
        long? roleId,
        long? workCenterId,
        int maxEmployees,
        bool isFixedTerm,
        bool generatesOvertime,
        DateTime effectiveFromUtc,
        DateTime? effectiveToUtc,
        string? notes)
    {
        EnsurePositiveId(jobProfileId, nameof(jobProfileId));
        if (workCenterId.HasValue)
        {
            EnsurePositiveId(workCenterId.Value, nameof(workCenterId));
        }

        if (roleId.HasValue)
        {
            EnsurePositiveId(roleId.Value, nameof(roleId));
        }

        ValidateCapacity(maxEmployees);
        ValidateDateRange(effectiveFromUtc, effectiveToUtc);

        SetCode(code);
        Title = PositionSlotNormalization.CleanOptional(title);
        JobProfileId = jobProfileId;
        RoleId = roleId;
        WorkCenterId = workCenterId;
        MaxEmployees = maxEmployees;
        IsFixedTerm = isFixedTerm;
        GeneratesOvertime = generatesOvertime;
        EffectiveFromUtc = effectiveFromUtc;
        EffectiveToUtc = effectiveToUtc;
        Notes = PositionSlotNormalization.CleanOptional(notes);

        RefreshConcurrencyToken();
    }

    public void UpdateDependencies(long? directDependencyPositionSlotId, long? functionalDependencyPositionSlotId)
    {
        if (directDependencyPositionSlotId.HasValue)
        {
            EnsurePositiveId(directDependencyPositionSlotId.Value, nameof(directDependencyPositionSlotId));
        }

        if (functionalDependencyPositionSlotId.HasValue)
        {
            EnsurePositiveId(functionalDependencyPositionSlotId.Value, nameof(functionalDependencyPositionSlotId));
        }

        if (Id > 0)
        {
            if (directDependencyPositionSlotId.HasValue && directDependencyPositionSlotId.Value == Id)
            {
                throw new PositionSlotDomainException(
                    PositionSlotDomainErrorCode.DirectDependencySelfReference,
                    "A position slot cannot depend directly on itself.");
            }

            if (functionalDependencyPositionSlotId.HasValue && functionalDependencyPositionSlotId.Value == Id)
            {
                throw new PositionSlotDomainException(
                    PositionSlotDomainErrorCode.FunctionalDependencySelfReference,
                    "A position slot cannot depend functionally on itself.");
            }
        }

        DirectDependencyPositionSlotId = directDependencyPositionSlotId;
        FunctionalDependencyPositionSlotId = functionalDependencyPositionSlotId;
        RefreshConcurrencyToken();
    }

    /// <summary>
    /// H-23 — the only status decision that is a DECISION: retired or not. `Vacant`/`Occupied` are facts about
    /// the assignments and are derived on read, so asking for either simply means "this slot is in force".
    /// The old version coerced an occupancy counter here (Occupied fabricated a `1`, Vacant zeroed whoever was
    /// inside), which is how the HR dashboard could report occupants that did not exist.
    /// </summary>
    public void ChangeStatus(PositionSlotStatus status)
    {
        IsActive = status != PositionSlotStatus.Suspended;
        RefreshConcurrencyToken();
    }

    /// <summary>
    /// Sets the plaza's configured (reference/budgeted) base salary — D-02 nivel 2 (R-2). Informational
    /// only; it does not gate the employee's negotiated salary (that is validated against the job
    /// profile's tabulator range).
    /// </summary>
    public void SetConfiguredBaseSalary(decimal? configuredBaseSalary, string? configuredBaseSalaryCurrencyCode)
    {
        ConfiguredBaseSalary = configuredBaseSalary;
        ConfiguredBaseSalaryCurrencyCode = PositionSlotNormalization.CleanOptional(configuredBaseSalaryCurrencyCode);
        RefreshConcurrencyToken();
    }

    private void SetCode(string code)
    {
        Code = PositionSlotNormalization.NormalizeCode(code);
        NormalizedCode = Code;
    }

    private static void EnsurePositiveId(long id, string parameterName)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Identifier must be greater than zero.");
        }
    }

    /// <summary>
    /// H-23 — capacity is now only the ceiling. What fills it is checked where it can be checked truthfully: the
    /// assignment handler counts the ACTIVE assignments whose validity overlaps the candidate's window
    /// (`EMPLOYMENT_ASSIGNMENT_CAPACITY_EXCEEDED`), which is finer than any counter on this row.
    /// </summary>
    private static void ValidateCapacity(int maxEmployees)
    {
        if (maxEmployees < 1)
        {
            throw new PositionSlotDomainException(
                PositionSlotDomainErrorCode.MaxEmployeesInvalid,
                "MaxEmployees must be greater than or equal to one.");
        }
    }

    private static void ValidateDateRange(DateTime effectiveFromUtc, DateTime? effectiveToUtc)
    {
        if (effectiveFromUtc == default)
        {
            throw new PositionSlotDomainException(
                PositionSlotDomainErrorCode.EffectiveFromRequired,
                "EffectiveFromUtc is required.");
        }

        if (effectiveToUtc.HasValue && effectiveToUtc.Value < effectiveFromUtc)
        {
            throw new PositionSlotDomainException(
                PositionSlotDomainErrorCode.EffectiveDateRangeInvalid,
                "EffectiveToUtc cannot be less than EffectiveFromUtc.");
        }
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}

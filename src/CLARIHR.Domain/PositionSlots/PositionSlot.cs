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
        int occupiedEmployees,
        bool isFixedTerm,
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

        ValidateCapacity(maxEmployees, occupiedEmployees);
        ValidateDateRange(effectiveFromUtc, effectiveToUtc);

        PublicId = publicId;
        SetCode(code);
        Title = PositionSlotNormalization.CleanOptional(title);
        JobProfileId = jobProfileId;
        RoleId = roleId;
        WorkCenterId = workCenterId;
        DirectDependencyPositionSlotId = directDependencyPositionSlotId;
        FunctionalDependencyPositionSlotId = functionalDependencyPositionSlotId;
        Status = status;
        MaxEmployees = maxEmployees;
        OccupiedEmployees = occupiedEmployees;
        IsFixedTerm = isFixedTerm;
        EffectiveFromUtc = effectiveFromUtc;
        EffectiveToUtc = effectiveToUtc;
        Notes = PositionSlotNormalization.CleanOptional(notes);

        // §PS6: on create the caller supplies BOTH status and occupancy, so a
        // contradiction is rejected (not silently coerced) — the persisted value can no
        // longer diverge from what the client sent.
        ValidateStatusOccupancyConsistency(Status, OccupiedEmployees);

        IsActive = Status != PositionSlotStatus.Suspended;
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

    public PositionSlotStatus Status { get; private set; }

    public int MaxEmployees { get; private set; }

    public int OccupiedEmployees { get; private set; }

    public bool IsFixedTerm { get; private set; }

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
        int occupiedEmployees,
        bool isFixedTerm,
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
            occupiedEmployees,
            isFixedTerm,
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

        ValidateCapacity(maxEmployees, OccupiedEmployees);
        ValidateDateRange(effectiveFromUtc, effectiveToUtc);

        SetCode(code);
        Title = PositionSlotNormalization.CleanOptional(title);
        JobProfileId = jobProfileId;
        RoleId = roleId;
        WorkCenterId = workCenterId;
        MaxEmployees = maxEmployees;
        IsFixedTerm = isFixedTerm;
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

    public void UpdateOccupancy(int occupiedEmployees)
    {
        if (Status == PositionSlotStatus.Suspended)
        {
            throw new PositionSlotDomainException(
                PositionSlotDomainErrorCode.SuspendedOccupancyConflict,
                "Suspended position slots cannot update occupancy.");
        }

        ValidateCapacity(MaxEmployees, occupiedEmployees);

        OccupiedEmployees = occupiedEmployees;
        Status = occupiedEmployees == 0
            ? PositionSlotStatus.Vacant
            : PositionSlotStatus.Occupied;
        IsActive = true;
        RefreshConcurrencyToken();
    }

    public void ChangeStatus(PositionSlotStatus status)
    {
        Status = status;
        EnsureStatusConsistency();
        
        IsActive = Status != PositionSlotStatus.Suspended;
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

    private static void ValidateCapacity(int maxEmployees, int occupiedEmployees)
    {
        if (maxEmployees < 1)
        {
            throw new PositionSlotDomainException(
                PositionSlotDomainErrorCode.MaxEmployeesInvalid,
                "MaxEmployees must be greater than or equal to one.");
        }

        if (occupiedEmployees < 0)
        {
            throw new PositionSlotDomainException(
                PositionSlotDomainErrorCode.OccupiedEmployeesNegative,
                "OccupiedEmployees must be greater than or equal to zero.");
        }

        if (occupiedEmployees > maxEmployees)
        {
            throw new PositionSlotDomainException(
                PositionSlotDomainErrorCode.OccupiedExceedsCapacity,
                "OccupiedEmployees cannot be greater than MaxEmployees.");
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

    // §PS6: validation counterpart used on CREATE, where the caller supplies both the
    // status and the occupancy. A contradictory pair is rejected so the persisted value
    // cannot silently differ from the request.
    private static void ValidateStatusOccupancyConsistency(PositionSlotStatus status, int occupiedEmployees)
    {
        if (status == PositionSlotStatus.Vacant && occupiedEmployees != 0)
        {
            throw new PositionSlotDomainException(
                PositionSlotDomainErrorCode.StatusOccupancyMismatch,
                "A vacant position slot must have zero occupied employees.");
        }

        if (status == PositionSlotStatus.Occupied && occupiedEmployees == 0)
        {
            throw new PositionSlotDomainException(
                PositionSlotDomainErrorCode.StatusOccupancyMismatch,
                "An occupied position slot must have at least one occupied employee.");
        }
    }

    /// <summary>
    /// §PS6: INTENTIONAL coercion for the status-only <see cref="ChangeStatus"/>
    /// transition. The caller changes only the status (the <c>/status</c> endpoint
    /// carries no occupancy), so there is no caller-supplied occupancy value to
    /// contradict — the occupancy is reconciled to match the new status:
    /// <c>Vacant</c> ⇒ 0 occupants, <c>Occupied</c> ⇒ at least 1, <c>Suspended</c>
    /// leaves occupancy untouched. Create instead uses
    /// <see cref="ValidateStatusOccupancyConsistency"/> and rejects contradictions.
    /// </summary>
    private void EnsureStatusConsistency()
    {
        if (Status == PositionSlotStatus.Vacant && OccupiedEmployees != 0)
        {
            OccupiedEmployees = 0;
        }
        else if (Status == PositionSlotStatus.Occupied && OccupiedEmployees == 0)
        {
            OccupiedEmployees = 1;
        }
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}

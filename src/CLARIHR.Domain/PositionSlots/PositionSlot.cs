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
        long orgUnitId,
        long? workCenterId,
        string? costCenterCode,
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
        EnsurePositiveId(orgUnitId, nameof(orgUnitId));
        if (workCenterId.HasValue)
        {
            EnsurePositiveId(workCenterId.Value, nameof(workCenterId));
        }

        ValidateCapacity(maxEmployees, occupiedEmployees);
        ValidateDateRange(effectiveFromUtc, effectiveToUtc);
        ValidateStatusConsistency(status, occupiedEmployees);

        PublicId = publicId;
        SetCode(code);
        Title = PositionSlotNormalization.CleanOptional(title);
        JobProfileId = jobProfileId;
        OrgUnitId = orgUnitId;
        WorkCenterId = workCenterId;
        CostCenterCode = PositionSlotNormalization.CleanOptional(costCenterCode);
        DirectDependencyPositionSlotId = directDependencyPositionSlotId;
        FunctionalDependencyPositionSlotId = functionalDependencyPositionSlotId;
        Status = status;
        MaxEmployees = maxEmployees;
        OccupiedEmployees = occupiedEmployees;
        IsFixedTerm = isFixedTerm;
        EffectiveFromUtc = effectiveFromUtc;
        EffectiveToUtc = effectiveToUtc;
        Notes = PositionSlotNormalization.CleanOptional(notes);
        IsActive = status != PositionSlotStatus.Suspended;
        ConcurrencyToken = Guid.NewGuid();
    }

    public Guid PublicId { get; private set; }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string? Title { get; private set; }

    public long JobProfileId { get; private set; }

    public long OrgUnitId { get; private set; }

    public long? WorkCenterId { get; private set; }

    public string? CostCenterCode { get; private set; }

    public long? DirectDependencyPositionSlotId { get; private set; }

    public long? FunctionalDependencyPositionSlotId { get; private set; }

    public PositionSlotStatus Status { get; private set; }

    public int MaxEmployees { get; private set; }

    public int OccupiedEmployees { get; private set; }

    public bool IsFixedTerm { get; private set; }

    public DateTime EffectiveFromUtc { get; private set; }

    public DateTime? EffectiveToUtc { get; private set; }

    public string? Notes { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static PositionSlot Create(
        string code,
        string? title,
        long jobProfileId,
        long orgUnitId,
        long? workCenterId,
        string? costCenterCode,
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
            orgUnitId,
            workCenterId,
            costCenterCode,
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
        long orgUnitId,
        long? workCenterId,
        string? costCenterCode,
        int maxEmployees,
        bool isFixedTerm,
        DateTime effectiveFromUtc,
        DateTime? effectiveToUtc,
        string? notes)
    {
        EnsurePositiveId(jobProfileId, nameof(jobProfileId));
        EnsurePositiveId(orgUnitId, nameof(orgUnitId));
        if (workCenterId.HasValue)
        {
            EnsurePositiveId(workCenterId.Value, nameof(workCenterId));
        }

        ValidateCapacity(maxEmployees, OccupiedEmployees);
        ValidateDateRange(effectiveFromUtc, effectiveToUtc);

        SetCode(code);
        Title = PositionSlotNormalization.CleanOptional(title);
        JobProfileId = jobProfileId;
        OrgUnitId = orgUnitId;
        WorkCenterId = workCenterId;
        CostCenterCode = PositionSlotNormalization.CleanOptional(costCenterCode);
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
                throw new InvalidOperationException("A position slot cannot depend directly on itself.");
            }

            if (functionalDependencyPositionSlotId.HasValue && functionalDependencyPositionSlotId.Value == Id)
            {
                throw new InvalidOperationException("A position slot cannot depend functionally on itself.");
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
            throw new InvalidOperationException("Suspended position slots cannot update occupancy.");
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
        ValidateStatusConsistency(status, OccupiedEmployees);

        Status = status;
        IsActive = status != PositionSlotStatus.Suspended;
        RefreshConcurrencyToken();
    }

    private void SetCode(string code)
    {
        Code = PositionSlotNormalization.Clean(code, nameof(code));
        NormalizedCode = PositionSlotNormalization.NormalizeCode(code);
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
            throw new InvalidOperationException("MaxEmployees must be greater than or equal to one.");
        }

        if (occupiedEmployees < 0)
        {
            throw new InvalidOperationException("OccupiedEmployees must be greater than or equal to zero.");
        }

        if (occupiedEmployees > maxEmployees)
        {
            throw new InvalidOperationException("OccupiedEmployees cannot be greater than MaxEmployees.");
        }
    }

    private static void ValidateDateRange(DateTime effectiveFromUtc, DateTime? effectiveToUtc)
    {
        if (effectiveFromUtc == default)
        {
            throw new InvalidOperationException("EffectiveFromUtc is required.");
        }

        if (effectiveToUtc.HasValue && effectiveToUtc.Value < effectiveFromUtc)
        {
            throw new InvalidOperationException("EffectiveToUtc cannot be less than EffectiveFromUtc.");
        }
    }

    private static void ValidateStatusConsistency(PositionSlotStatus status, int occupiedEmployees)
    {
        if (status == PositionSlotStatus.Vacant && occupiedEmployees != 0)
        {
            throw new InvalidOperationException("Vacant status requires OccupiedEmployees equal to zero.");
        }

        if (status == PositionSlotStatus.Occupied && occupiedEmployees == 0)
        {
            throw new InvalidOperationException("Occupied status requires OccupiedEmployees greater than zero.");
        }
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}

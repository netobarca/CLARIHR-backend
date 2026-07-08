namespace CLARIHR.Application.Abstractions.Leave;

/// <summary>One subsidy tranche of the risk, ordered (day range → percent + payer — <c>ISSS|EMPRESA|SIN_PAGO</c>).</summary>
public sealed record LeaveRiskTrancheDto(
    int DayFrom,
    int? DayTo,
    decimal SubsidyPercent,
    string PayerCode);

/// <summary>
/// The incapacity-risk master facts the calculation and the registration snapshot consume: the counting
/// flags that drive the engine plus the behavioral flags the handler validates (indefinite/extension/fund).
/// </summary>
public sealed record LeaveRiskSnapshotDto(
    long Id,
    Guid PublicId,
    string Code,
    string Name,
    bool CountsSeventhDay,
    bool CountsSaturday,
    bool CountsHoliday,
    bool UsesWorkSchedule,
    bool AllowsIndefinite,
    bool AllowsExtension,
    bool UsesFund,
    bool HasSubsidy,
    bool IsActive,
    IReadOnlyList<LeaveRiskTrancheDto> Tranches);

/// <summary>
/// Everything the incapacity engine consumes, resolved in ONE trip (mirror of the settlement
/// <c>SettlementCalculationContext</c> idiom — snapshot at calculation time):
/// <list type="bullet">
/// <item><see cref="MonthlyBaseSalary"/> — the negotiated SALARIO_BASE of the referred plaza or the
/// principal one; <c>null</c> when not resolvable (the handler answers 422 INCAPACITY_BASE_SALARY_MISSING).</item>
/// <item><see cref="RestDay"/> — referred plaza → principal plaza <c>RestDayOfWeek</c> → company
/// preference <c>CompanyRestDayOfWeek</c> → Sunday (D-26).</item>
/// <item><see cref="Holidays"/> — active company holidays inside the calculation window.</item>
/// <item><see cref="EmployerCapRemaining"/> — (covered days ?? 9) + (benefit days ?? 0) minus the
/// EmployerDays already consumed by REGISTRADA incapacities of the same start year (D-27), floored at 0.</item>
/// <item><see cref="ChainOffsetDays"/> — Σ ComputableDays of the non-annulled extension chain (RN-03).</item>
/// <item><see cref="Risk"/> — the risk master flags + ordered subsidy tranches.</item>
/// </list>
/// </summary>
public sealed record LeaveCalculationContext(
    decimal? MonthlyBaseSalary,
    DayOfWeek RestDay,
    IReadOnlySet<DateOnly> Holidays,
    decimal EmployerCapRemaining,
    int ChainOffsetDays,
    LeaveRiskSnapshotDto Risk);

/// <summary>
/// One-stop resolver of the incapacity engine's inputs (pattern: <c>ISettlementRepository</c>'s
/// calculation-context method — the engine stays pure, the reads have a single auditable source).
/// </summary>
public interface ILeaveCalculationDataProvider
{
    /// <summary>
    /// Resolves every engine input for one incapacity of one employee: the base salary of the referred
    /// plaza (or the principal — <c>IsPrimary</c> among the active assignments; the oldest
    /// <c>StartDate</c> when none — same criterion as the settlement data provider), the effective rest
    /// day, the active holidays in <c>[startDate, endDate ?? startDate + 366d]</c>, the remaining
    /// employer cap of <paramref name="startDate"/>'s year (excluding <paramref name="excludeIncapacityId"/>
    /// when recalculating an existing record), the chain offset walked back from
    /// <paramref name="extendsIncapacityId"/> and the risk snapshot with its ordered tranches.
    /// Null when the company or the risk does not exist for the tenant.
    /// </summary>
    Task<LeaveCalculationContext?> GetCalculationContextAsync(
        Guid tenantId,
        long personnelFileId,
        Guid? assignedPositionPublicId,
        long riskId,
        DateOnly startDate,
        DateOnly? endDate,
        long? excludeIncapacityId,
        long? extendsIncapacityId,
        CancellationToken cancellationToken);
}

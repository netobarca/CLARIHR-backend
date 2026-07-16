using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Leave;
using CLARIHR.Domain.Leave;

namespace CLARIHR.Application.Abstractions.Leave;

/// <summary>
/// Snapshot of a period's overtime-entry window for the REQ-012 D-05 gate: the gate only acts when the
/// period RESOLVES and hangs from a Nómina (<see cref="PayrollDefinitionId"/> not null) — legacy periods
/// and unresolved references keep the pre-REQ-012 behavior untouched.
/// </summary>
public sealed record PayrollPeriodOvertimeWindow(
    long? PayrollDefinitionId,
    bool AllowsOvertimeEntry,
    DateOnly? OvertimeEntryStart,
    DateOnly? OvertimeEntryEnd);

public interface IPayrollPeriodRepository
{
    void Add(PayrollPeriodDefinition payrollPeriod);

    Task<PayrollPeriodDefinition?> GetByIdAsync(Guid payrollPeriodId, CancellationToken cancellationToken);

    /// <summary>Tracked load by INTERNAL id — the run stores the FK (closure closes the period in the same tx).</summary>
    Task<PayrollPeriodDefinition?> GetByInternalIdAsync(long payrollPeriodId, CancellationToken cancellationToken);

    Task<bool> ExistsOutsideTenantAsync(Guid payrollPeriodId, CancellationToken cancellationToken);

    /// <summary>
    /// Duplicate probe on the LEGACY (TenantId, PayPeriodTypeCode, Year, Number) unique key — since
    /// REQ-012 M2 that index is partial (WHERE payroll_definition_id IS NULL), so this probe only looks
    /// at rows without a Nómina. Pass the public id of the period being edited in
    /// <paramref name="excludingPayrollPeriodId"/> to exclude itself.
    /// </summary>
    Task<bool> PeriodExistsAsync(
        Guid tenantId,
        string payPeriodTypeCode,
        int year,
        int number,
        Guid? excludingPayrollPeriodId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Duplicate probe on the per-Nómina (TenantId, PayrollDefinitionId, Year, Number) unique key
    /// (REQ-012 §1.2 — the mirror of <see cref="PeriodExistsAsync"/> for periods that hang from one).
    /// </summary>
    Task<bool> PeriodExistsForDefinitionAsync(
        Guid tenantId,
        long payrollDefinitionId,
        int year,
        int number,
        Guid? excludingPayrollPeriodId,
        CancellationToken cancellationToken);

    /// <summary>
    /// True when the [<paramref name="startDate"/>, <paramref name="endDate"/>] range overlaps another
    /// ACTIVE period of the same pay-period type and year IN THE SAME BUCKET — the same Nómina when
    /// <paramref name="payrollDefinitionId"/> is provided, the legacy bucket (no Nómina) otherwise
    /// (<c>start &lt;= other.End &amp;&amp; end &gt;= other.Start</c>). Two Nóminas of the same frequency
    /// deliberately do NOT overlap-check each other.
    /// </summary>
    Task<bool> HasOverlapAsync(
        Guid tenantId,
        string payPeriodTypeCode,
        int year,
        DateOnly startDate,
        DateOnly endDate,
        long? payrollDefinitionId,
        Guid? excludingPayrollPeriodId,
        CancellationToken cancellationToken);

    /// <summary>Existing period numbers of a Nómina's year — the idempotency set of the calendar generator.</summary>
    Task<IReadOnlyCollection<int>> GetExistingNumbersForDefinitionAsync(
        Guid tenantId,
        long payrollDefinitionId,
        int year,
        CancellationToken cancellationToken);

    /// <summary>
    /// Overtime-entry window snapshot by public id (tenant-scoped). NULL when the period does not resolve —
    /// the D-05 gate then keeps the pre-REQ-012 behavior untouched (the overtime reference is degraded).
    /// </summary>
    Task<PayrollPeriodOvertimeWindow?> GetOvertimeWindowByPublicIdAsync(
        Guid tenantId,
        Guid payrollPeriodPublicId,
        CancellationToken cancellationToken);

    Task<PagedResponse<PayrollPeriodListItemResponse>> SearchAsync(
        Guid tenantId,
        string? payPeriodTypeCode,
        int? year,
        bool? isActive,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<PayrollPeriodResponse?> GetResponseByIdAsync(Guid payrollPeriodId, CancellationToken cancellationToken);
}

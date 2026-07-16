using CLARIHR.Domain.Payroll;

namespace CLARIHR.Application.Abstractions.Payroll;

public interface IPayrollRunRepository
{
    void Add(PayrollRun run);

    /// <summary>Loads the aggregate WITH its lines (tracked — the review/reversal flows mutate them).</summary>
    Task<PayrollRun?> GetByIdAsync(Guid payrollRunPublicId, CancellationToken cancellationToken);

    Task<bool> ExistsOutsideTenantAsync(Guid payrollRunPublicId, CancellationToken cancellationToken);

    /// <summary>Sequential probe of the one-active-run slot (the filtered unique index closes the race).</summary>
    Task<bool> HasActiveRunAsync(
        Guid tenantId,
        long payrollDefinitionId,
        long payrollPeriodId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Serializes every generation/regeneration/annulment of one Nómina × period on a
    /// pg_advisory_xact_lock (classId ASCII "PRUN"; REQ-012 §0.18 — the handler opens the transaction
    /// BEFORE acquiring the lock so it holds until commit/rollback).
    /// </summary>
    Task AcquirePayrollRunMutationLockAsync(
        long payrollDefinitionId,
        long payrollPeriodId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Source references CONSUMED by an INCLUDED line of a non-annulled run of the tenant for the given
    /// source module — the derived-consumption probe of the TNT/disciplinary carryover (REQ-014 P-03,
    /// §0.11; backed by the M4 index (tenant, source_module, source_reference_public_id)).
    /// </summary>
    Task<IReadOnlyCollection<Guid>> GetConsumedSourceReferencesAsync(
        Guid tenantId,
        string sourceModule,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves the PARENT pool record of an applied installment/application child (the line's re-bound
    /// <c>SourceReferencePublicId</c> after §3.5). The reversal flows (exclude / regenerate / annul — PR-6)
    /// need the parent to call its <c>AnnulInstallment</c>/<c>AnnulApplication</c> mutator. Null when the
    /// child does not resolve in the module.
    /// </summary>
    Task<Guid?> GetPoolParentByChildAsync(
        Guid tenantId,
        string sourceModule,
        Guid childPublicId,
        CancellationToken cancellationToken);

    /// <summary>
    /// The PARENT pool records holding an ACTIVE MOTOR-origin installment/application bound to the given
    /// period — exactly what the run applied (one active run per Nómina × period). The full reversal
    /// (regenerate / annul — §3.5) walks these parents and annuls those children symmetrically; the
    /// selective recalculation passes <paramref name="personnelFilePublicIds"/> to revert ONLY those
    /// employees' records (null ⇒ no employee filter).
    /// </summary>
    Task<IReadOnlyCollection<Guid>> GetMotorAppliedParentsForPeriodAsync(
        Guid tenantId,
        string sourceModule,
        long payrollPeriodId,
        IReadOnlyCollection<Guid>? personnelFilePublicIds,
        CancellationToken cancellationToken);

    /// <summary>The public ids of the run's definition/period FKs (the wire never exposes internal ids).</summary>
    Task<(Guid DefinitionPublicId, Guid PeriodPublicId)?> GetReferencePublicIdsAsync(
        long payrollDefinitionId,
        long payrollPeriodId,
        CancellationToken cancellationToken);

    /// <summary>
    /// The corporate bandeja over the PERSISTED header (REQ-013 P-03 — never recomputed);
    /// <c>StatusCounts</c> spans every status under the same filters minus the status one.
    /// </summary>
    Task<Features.Payroll.PayrollRunBandejaResponse> QueryRunsAsync(
        Features.Payroll.QueryPayrollRunsQuery query,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Features.Payroll.CorridaPlanillaExportRow>> GetRunExportRowsAsync(
        Features.Payroll.ExportPayrollRunsQuery query,
        CancellationToken cancellationToken);

    /// <summary>
    /// The payroll print (REQ-013 RF-003): detail rows + per-concept and per-cost-center totals over the
    /// INCLUDED lines. Null when the run does not exist in the tenant.
    /// </summary>
    Task<IReadOnlyCollection<Features.Payroll.ImpresionPlanillaExportRow>?> GetRunLineExportRowsAsync(
        Guid tenantId,
        Guid payrollRunPublicId,
        int? maxRows,
        CancellationToken cancellationToken);

    /// <summary>
    /// One row per employee of the run with payment method → bank → account (designated or PRIMARY) and
    /// the employee's net; a missing account travels as a warning, never a block. Null when the run does
    /// not exist in the tenant.
    /// </summary>
    Task<IReadOnlyCollection<Features.Payroll.ConciliacionBancariaExportRow>?> GetBankReconciliationRowsAsync(
        Guid tenantId,
        Guid payrollRunPublicId,
        int? maxRows,
        CancellationToken cancellationToken);

    /// <summary>
    /// The employee axis (REQ-015): one row per run holding INCLUDED lines of the employee, with THEIR
    /// sums, newest first (GROUP BY over the M4 employee index).
    /// </summary>
    Task<Features.Payroll.PayrollRunEmployeeHistoryResponse> QueryEmployeeHistoryAsync(
        Guid tenantId,
        Guid personnelFilePublicId,
        int? year,
        Guid? payrollDefinitionPublicId,
        string? payrollTypeCode,
        IReadOnlyCollection<string> statusCodes,
        DateOnly? from,
        DateOnly? to,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);
}

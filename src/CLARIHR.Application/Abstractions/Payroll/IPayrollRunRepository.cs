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
}

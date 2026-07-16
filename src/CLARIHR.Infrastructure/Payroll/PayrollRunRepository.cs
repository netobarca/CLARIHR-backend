using CLARIHR.Application.Abstractions.Payroll;
using CLARIHR.Domain.Payroll;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Payroll;

internal sealed class PayrollRunRepository(ApplicationDbContext dbContext) : IPayrollRunRepository
{
    // "PRUN" — payroll run. A fixed class id namespaces the advisory lock; the object id derives from the
    // (definition, period) pair so every generation/regeneration/annulment of one Nómina × period contends
    // on the same lock. Executed on the context's CURRENT transaction (the handler opens one first — §0.18),
    // so pg_advisory_xact_lock holds until that transaction commits/rolls back.
    private const int PayrollRunLockClassId = 0x50_52_55_4E;

    public void Add(PayrollRun run) => dbContext.Set<PayrollRun>().Add(run);

    public Task<PayrollRun?> GetByIdAsync(Guid payrollRunPublicId, CancellationToken cancellationToken) =>
        dbContext.Set<PayrollRun>()
            .Include(run => run.Lines)
            .SingleOrDefaultAsync(run => run.PublicId == payrollRunPublicId, cancellationToken);

    public Task<bool> ExistsOutsideTenantAsync(Guid payrollRunPublicId, CancellationToken cancellationToken) =>
        dbContext.Set<PayrollRun>()
            // Intentional tenant filter bypass: checks cross-tenant existence only for tenant-mismatch errors.
            .IgnoreQueryFilters()
            .AnyAsync(run => run.PublicId == payrollRunPublicId, cancellationToken);

    public Task<bool> HasActiveRunAsync(
        Guid tenantId,
        long payrollDefinitionId,
        long payrollPeriodId,
        CancellationToken cancellationToken) =>
        dbContext.Set<PayrollRun>().AnyAsync(
            run => run.TenantId == tenantId &&
                   run.PayrollDefinitionId == payrollDefinitionId &&
                   run.PayrollPeriodId == payrollPeriodId &&
                   run.IsActive,
            cancellationToken);

    public Task AcquirePayrollRunMutationLockAsync(
        long payrollDefinitionId,
        long payrollPeriodId,
        CancellationToken cancellationToken)
    {
        // Deterministic 32-bit key from the pair (mirrors AcquireOwnerCapacityLockAsync's derivation).
        var key = unchecked((int)(payrollDefinitionId * 397) ^ (int)payrollPeriodId);
        return dbContext.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock({0}, {1})",
            [PayrollRunLockClassId, key],
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<Guid>> GetConsumedSourceReferencesAsync(
        Guid tenantId,
        string sourceModule,
        CancellationToken cancellationToken) =>
        await (
            from line in dbContext.Set<PayrollRunLine>().AsNoTracking()
            join run in dbContext.Set<PayrollRun>().AsNoTracking() on line.PayrollRunId equals run.Id
            where line.TenantId == tenantId &&
                  line.SourceModule == sourceModule &&
                  line.SourceReferencePublicId != null &&
                  line.IsIncluded &&
                  run.IsActive
            select line.SourceReferencePublicId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);
}

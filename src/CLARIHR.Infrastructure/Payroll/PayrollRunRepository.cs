using CLARIHR.Application.Abstractions.Payroll;
using CLARIHR.Application.Features.Payroll;
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

    public async Task<Guid?> GetPoolParentByChildAsync(
        Guid tenantId,
        string sourceModule,
        Guid childPublicId,
        CancellationToken cancellationToken) => sourceModule switch
    {
        PayrollSourceModules.RecurringIncome => await dbContext.Set<Domain.PersonnelFiles.PersonnelFileRecurringIncome>()
            .AsNoTracking()
            .Where(parent => parent.TenantId == tenantId && parent.Installments.Any(child => child.PublicId == childPublicId))
            .Select(parent => (Guid?)parent.PublicId)
            .SingleOrDefaultAsync(cancellationToken),
        PayrollSourceModules.RecurringDeduction => await dbContext.Set<Domain.PersonnelFiles.PersonnelFileRecurringDeduction>()
            .AsNoTracking()
            .Where(parent => parent.TenantId == tenantId && parent.Installments.Any(child => child.PublicId == childPublicId))
            .Select(parent => (Guid?)parent.PublicId)
            .SingleOrDefaultAsync(cancellationToken),
        PayrollSourceModules.OneTimeIncome => await dbContext.Set<Domain.PersonnelFiles.PersonnelFileOneTimeIncome>()
            .AsNoTracking()
            .Where(parent => parent.TenantId == tenantId && parent.Applications.Any(child => child.PublicId == childPublicId))
            .Select(parent => (Guid?)parent.PublicId)
            .SingleOrDefaultAsync(cancellationToken),
        PayrollSourceModules.OneTimeDeduction => await dbContext.Set<Domain.PersonnelFiles.PersonnelFileOneTimeDeduction>()
            .AsNoTracking()
            .Where(parent => parent.TenantId == tenantId && parent.Applications.Any(child => child.PublicId == childPublicId))
            .Select(parent => (Guid?)parent.PublicId)
            .SingleOrDefaultAsync(cancellationToken),
        _ => null,
    };

    public async Task<IReadOnlyCollection<Guid>> GetMotorAppliedParentsForPeriodAsync(
        Guid tenantId,
        string sourceModule,
        long payrollPeriodId,
        IReadOnlyCollection<Guid>? personnelFilePublicIds,
        CancellationToken cancellationToken)
    {
        // null ⇒ every employee (regenerate/annul); a set ⇒ only those files (selective recalculation).
        var hasEmployeeFilter = personnelFilePublicIds is not null;
        List<long> fileIds = hasEmployeeFilter
            ? await dbContext.Set<Domain.PersonnelFiles.PersonnelFile>()
                .Where(file => personnelFilePublicIds!.Contains(file.PublicId))
                .Select(file => file.Id)
                .ToListAsync(cancellationToken)
            : [];

        return sourceModule switch
        {
            PayrollSourceModules.RecurringIncome => await dbContext.Set<Domain.PersonnelFiles.PersonnelFileRecurringIncome>()
                .AsNoTracking()
                .Where(parent => parent.TenantId == tenantId &&
                    (!hasEmployeeFilter || fileIds.Contains(parent.PersonnelFileId)) &&
                    parent.Installments.Any(child =>
                        child.OriginCode == Domain.PersonnelFiles.RecurringIncomeInstallmentOrigins.Motor &&
                        child.PayrollPeriodId == payrollPeriodId &&
                        child.StatusCode == Domain.PersonnelFiles.RecurringIncomeInstallmentStatuses.Aplicada))
                .Select(parent => parent.PublicId)
                .ToListAsync(cancellationToken),
            PayrollSourceModules.RecurringDeduction => await dbContext.Set<Domain.PersonnelFiles.PersonnelFileRecurringDeduction>()
                .AsNoTracking()
                .Where(parent => parent.TenantId == tenantId &&
                    (!hasEmployeeFilter || fileIds.Contains(parent.PersonnelFileId)) &&
                    parent.Installments.Any(child =>
                        child.OriginCode == Domain.PersonnelFiles.RecurringDeductionInstallmentOrigins.Motor &&
                        child.PayrollPeriodId == payrollPeriodId &&
                        child.StatusCode == Domain.PersonnelFiles.RecurringDeductionInstallmentStatuses.Aplicada))
                .Select(parent => parent.PublicId)
                .ToListAsync(cancellationToken),
            PayrollSourceModules.OneTimeIncome => await dbContext.Set<Domain.PersonnelFiles.PersonnelFileOneTimeIncome>()
                .AsNoTracking()
                .Where(parent => parent.TenantId == tenantId &&
                    (!hasEmployeeFilter || fileIds.Contains(parent.PersonnelFileId)) &&
                    parent.Applications.Any(child =>
                        child.OriginCode == Domain.PersonnelFiles.OneTimeIncomeApplicationOrigins.Motor &&
                        child.PayrollPeriodId == payrollPeriodId &&
                        child.StatusCode == Domain.PersonnelFiles.OneTimeIncomeApplicationStatuses.Aplicada))
                .Select(parent => parent.PublicId)
                .ToListAsync(cancellationToken),
            PayrollSourceModules.OneTimeDeduction => await dbContext.Set<Domain.PersonnelFiles.PersonnelFileOneTimeDeduction>()
                .AsNoTracking()
                .Where(parent => parent.TenantId == tenantId &&
                    (!hasEmployeeFilter || fileIds.Contains(parent.PersonnelFileId)) &&
                    parent.Applications.Any(child =>
                        child.OriginCode == Domain.PersonnelFiles.OneTimeDeductionApplicationOrigins.Motor &&
                        child.PayrollPeriodId == payrollPeriodId &&
                        child.StatusCode == Domain.PersonnelFiles.OneTimeDeductionApplicationStatuses.Aplicada))
                .Select(parent => parent.PublicId)
                .ToListAsync(cancellationToken),
            PayrollSourceModules.Overtime => await dbContext.Set<Domain.PersonnelFiles.PersonnelFileOvertimeRecord>()
                .AsNoTracking()
                .Where(parent => parent.TenantId == tenantId &&
                    (!hasEmployeeFilter || fileIds.Contains(parent.PersonnelFileId)) &&
                    parent.Applications.Any(child =>
                        child.OriginCode == Domain.PersonnelFiles.OvertimeApplicationOrigins.Motor &&
                        child.PayrollPeriodId == payrollPeriodId &&
                        child.StatusCode == Domain.PersonnelFiles.OvertimeApplicationStatuses.Aplicada))
                .Select(parent => parent.PublicId)
                .ToListAsync(cancellationToken),
            _ => [],
        };
    }

    public async Task<(Guid DefinitionPublicId, Guid PeriodPublicId)?> GetReferencePublicIdsAsync(
        long payrollDefinitionId,
        long payrollPeriodId,
        CancellationToken cancellationToken)
    {
        var definition = await dbContext.Set<PayrollDefinition>()
            .AsNoTracking()
            .Where(item => item.Id == payrollDefinitionId)
            .Select(item => (Guid?)item.PublicId)
            .SingleOrDefaultAsync(cancellationToken);
        var period = await dbContext.Set<Domain.Leave.PayrollPeriodDefinition>()
            .AsNoTracking()
            .Where(item => item.Id == payrollPeriodId)
            .Select(item => (Guid?)item.PublicId)
            .SingleOrDefaultAsync(cancellationToken);

        return definition is { } definitionId && period is { } periodId ? (definitionId, periodId) : null;
    }
}

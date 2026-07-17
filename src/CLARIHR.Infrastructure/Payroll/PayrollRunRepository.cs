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

    public async Task<PayrollRunBandejaResponse> QueryRunsAsync(
        QueryPayrollRunsQuery query,
        CancellationToken cancellationToken)
    {
        var runs = await BuildFilteredRunsQuery(
            query.CompanyId, query.PayrollDefinitionPublicId, query.PayrollPeriodPublicId, query.Year, cancellationToken);

        // Tab numbers BEFORE the status filter — they always span every status.
        var statusCounts = (await runs
                .GroupBy(run => run.StatusCode)
                .Select(group => new { group.Key, Count = group.Count() })
                .ToListAsync(cancellationToken))
            .ToDictionary(item => item.Key, item => item.Count);

        if (!string.IsNullOrWhiteSpace(query.StatusCode))
        {
            var normalizedStatus = query.StatusCode.Trim().ToUpperInvariant();
            runs = runs.Where(run => run.StatusCode == normalizedStatus);
        }

        var totalCount = await runs.CountAsync(cancellationToken);
        var items = await runs
            .OrderByDescending(run => run.PeriodStartDate)
            .ThenByDescending(run => run.Id)
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(run => new PayrollRunListItemResponse(
                run.PublicId,
                dbContext.Set<PayrollDefinition>()
                    .Where(definition => definition.Id == run.PayrollDefinitionId)
                    .Select(definition => definition.PublicId)
                    .FirstOrDefault(),
                dbContext.Set<Domain.Leave.PayrollPeriodDefinition>()
                    .Where(period => period.Id == run.PayrollPeriodId)
                    .Select(period => period.PublicId)
                    .FirstOrDefault(),
                run.PayrollDefinitionCode,
                run.PayrollDefinitionName,
                run.PayrollTypeCode,
                run.PeriodLabel,
                run.PeriodStartDate,
                run.PeriodEndDate,
                run.PaymentDate,
                run.CurrencyCode,
                run.StatusCode,
                run.EmployeeCount,
                run.TotalIncome,
                run.TotalDeductions,
                run.TotalEmployerCost,
                run.TotalNet,
                run.GeneratedUtc,
                run.RegeneratedCount))
            .ToListAsync(cancellationToken);

        return new PayrollRunBandejaResponse(items, query.PageNumber, query.PageSize, totalCount, statusCounts);
    }

    public async Task<IReadOnlyCollection<CorridaPlanillaExportRow>> GetRunExportRowsAsync(
        ExportPayrollRunsQuery query,
        CancellationToken cancellationToken)
    {
        var runs = await BuildFilteredRunsQuery(
            query.CompanyId, query.PayrollDefinitionPublicId, query.PayrollPeriodPublicId, query.Year, cancellationToken);

        if (!string.IsNullOrWhiteSpace(query.StatusCode))
        {
            var normalizedStatus = query.StatusCode.Trim().ToUpperInvariant();
            runs = runs.Where(run => run.StatusCode == normalizedStatus);
        }

        var ordered = runs
            .OrderByDescending(run => run.PeriodStartDate)
            .ThenByDescending(run => run.Id)
            .Select(run => new CorridaPlanillaExportRow(
                run.PayrollDefinitionName,
                run.PayrollDefinitionCode,
                run.PayrollTypeCode,
                run.PeriodLabel,
                run.PeriodStartDate,
                run.PeriodEndDate,
                run.PaymentDate,
                run.StatusCode,
                run.EmployeeCount,
                run.TotalIncome,
                run.TotalDeductions,
                run.TotalEmployerCost,
                run.TotalNet,
                run.CurrencyCode,
                run.RegeneratedCount));

        return query.MaxRows is { } maxRows
            ? await ordered.Take(maxRows).ToListAsync(cancellationToken)
            : await ordered.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<ImpresionPlanillaExportRow>?> GetRunLineExportRowsAsync(
        Guid tenantId,
        Guid payrollRunPublicId,
        int? maxRows,
        CancellationToken cancellationToken)
    {
        var run = await dbContext.Set<PayrollRun>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PublicId == payrollRunPublicId)
            .Select(item => new { item.Id, item.CurrencyCode })
            .SingleOrDefaultAsync(cancellationToken);
        if (run is null)
        {
            return null;
        }

        var lines = await dbContext.Set<PayrollRunLine>()
            .AsNoTracking()
            .Where(line => line.PayrollRunId == run.Id)
            .OrderBy(line => line.EmployeeName)
            .ThenBy(line => line.SortOrder)
            .ThenBy(line => line.Id)
            .Select(line => new
            {
                line.EmployeeName,
                line.EmployeeCode,
                line.CostCenterName,
                line.ConceptCode,
                line.ConceptName,
                line.LineClass,
                line.Units,
                line.BaseAmount,
                line.CalculatedAmount,
                line.OverrideAmount,
                Final = line.OverrideAmount ?? line.CalculatedAmount,
                line.IsIncluded,
                line.SourceModule,
            })
            .ToListAsync(cancellationToken);

        var rows = new List<ImpresionPlanillaExportRow>(lines.Count + 32);
        foreach (var line in maxRows is { } cap ? lines.Take(cap) : lines)
        {
            rows.Add(new ImpresionPlanillaExportRow(
                PayrollRunReportingConstants.DetailRow,
                line.EmployeeName,
                line.EmployeeCode,
                line.CostCenterName,
                line.ConceptName,
                line.ConceptCode,
                line.LineClass,
                line.Units,
                line.BaseAmount,
                line.CalculatedAmount,
                line.OverrideAmount,
                line.Final,
                line.IsIncluded ? "SI" : "NO",
                line.SourceModule,
                run.CurrencyCode));
        }

        // The print's summary blocks (REQ-013 RF-003) — computed over the INCLUDED lines only.
        var included = lines.Where(line => line.IsIncluded).ToArray();
        foreach (var group in included
                     .GroupBy(line => (line.ConceptCode, line.ConceptName, line.LineClass))
                     .OrderBy(group => group.Key.LineClass, StringComparer.Ordinal)
                     .ThenBy(group => group.Key.ConceptCode, StringComparer.Ordinal))
        {
            rows.Add(new ImpresionPlanillaExportRow(
                PayrollRunReportingConstants.ConceptTotalRow,
                null, null, null,
                group.Key.ConceptName,
                group.Key.ConceptCode,
                group.Key.LineClass,
                null, null, null, null,
                Math.Round(group.Sum(line => line.Final), 2, MidpointRounding.AwayFromZero),
                null, null,
                run.CurrencyCode));
        }

        foreach (var group in included
                     .Where(line => line.LineClass != PayrollLineClasses.PagoPatronal)
                     .GroupBy(line => line.CostCenterName ?? "(sin centro de costo)")
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var net = group.Where(line => line.LineClass == PayrollLineClasses.Ingreso).Sum(line => line.Final)
                      - group.Where(line => line.LineClass == PayrollLineClasses.Descuento).Sum(line => line.Final);
            rows.Add(new ImpresionPlanillaExportRow(
                PayrollRunReportingConstants.CostCenterTotalRow,
                null, null,
                group.Key,
                null, null,
                "NETO",
                null, null, null, null,
                Math.Round(net, 2, MidpointRounding.AwayFromZero),
                null, null,
                run.CurrencyCode));
        }

        return rows;
    }

    public async Task<IReadOnlyCollection<PlanillaPatronalExportRow>?> GetEmployerCostReportRowsAsync(
        Guid tenantId,
        Guid payrollRunPublicId,
        int? maxRows,
        CancellationToken cancellationToken)
    {
        var run = await dbContext.Set<PayrollRun>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PublicId == payrollRunPublicId)
            .Select(item => new { item.Id, item.CurrencyCode })
            .SingleOrDefaultAsync(cancellationToken);
        if (run is null)
        {
            return null;
        }

        var employerLines = await dbContext.Set<PayrollRunLine>()
            .AsNoTracking()
            .Where(line => line.PayrollRunId == run.Id && line.IsIncluded)
            .Where(line => line.ConceptCode == PayrollEngineConceptCodes.Salario || line.LineClass == PayrollLineClasses.PagoPatronal)
            .Select(line => new
            {
                line.EmployeeName,
                line.EmployeeCode,
                line.CostCenterName,
                line.ConceptCode,
                Final = line.OverrideAmount ?? line.CalculatedAmount,
            })
            .ToListAsync(cancellationToken);

        var grouped = employerLines
            .GroupBy(line => new { line.EmployeeName, line.EmployeeCode, line.CostCenterName })
            .OrderBy(group => group.Key.EmployeeName)
            .Select(group =>
            {
                var salarioBase = group.Where(line => line.ConceptCode == PayrollEngineConceptCodes.Salario).Sum(line => line.Final);
                var isssPatronal = group.Where(line => line.ConceptCode == PayrollEngineConceptCodes.IsssPatronal).Sum(line => line.Final);
                var afpPatronal = group.Where(line => line.ConceptCode == PayrollEngineConceptCodes.AfpPatronal).Sum(line => line.Final);
                var otrasCargas = group
                    .Where(line => line.ConceptCode != PayrollEngineConceptCodes.Salario &&
                                   line.ConceptCode != PayrollEngineConceptCodes.IsssPatronal &&
                                   line.ConceptCode != PayrollEngineConceptCodes.AfpPatronal)
                    .Sum(line => line.Final);
                return new PlanillaPatronalExportRow(
                    group.Key.EmployeeName,
                    group.Key.EmployeeCode,
                    group.Key.CostCenterName,
                    salarioBase,
                    isssPatronal,
                    afpPatronal,
                    otrasCargas,
                    isssPatronal + afpPatronal + otrasCargas,
                    run.CurrencyCode);
            })
            .ToList();

        return maxRows is { } cap ? grouped.Take(cap).ToList() : grouped;
    }

    public async Task<IReadOnlyCollection<ConciliacionBancariaExportRow>?> GetBankReconciliationRowsAsync(
        Guid tenantId,
        Guid payrollRunPublicId,
        int? maxRows,
        CancellationToken cancellationToken)
    {
        var run = await dbContext.Set<PayrollRun>()
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId && item.PublicId == payrollRunPublicId)
            .Select(item => new { item.Id, item.CurrencyCode })
            .SingleOrDefaultAsync(cancellationToken);
        if (run is null)
        {
            return null;
        }

        var employees = await dbContext.Set<PayrollRunLine>()
            .AsNoTracking()
            .Where(line => line.PayrollRunId == run.Id && line.IsIncluded &&
                           line.LineClass != PayrollLineClasses.PagoPatronal)
            .GroupBy(line => new { line.PersonnelFileId, line.EmployeeName, line.EmployeeCode })
            .Select(group => new
            {
                group.Key.PersonnelFileId,
                group.Key.EmployeeName,
                group.Key.EmployeeCode,
                Net = group.Sum(line => line.LineClass == PayrollLineClasses.Ingreso
                          ? (line.OverrideAmount ?? line.CalculatedAmount)
                          : -(line.OverrideAmount ?? line.CalculatedAmount)),
            })
            .OrderBy(item => item.EmployeeName)
            .ToListAsync(cancellationToken);

        if (maxRows is { } cap)
        {
            employees = employees.Take(cap).ToList();
        }

        var fileIds = employees.Select(item => item.PersonnelFileId).ToArray();

        // Payment data lives on the PLAZA: the primary ACTIVE assignment speaks for the employee.
        var assignments = await dbContext.Set<Domain.PersonnelFiles.PersonnelFileEmploymentAssignment>()
            .AsNoTracking()
            .Where(assignment => fileIds.Contains(assignment.PersonnelFileId) && assignment.IsActive)
            .Select(assignment => new
            {
                assignment.PersonnelFileId,
                assignment.IsPrimary,
                assignment.PaymentMethodCode,
                assignment.PaymentBankAccountPublicId,
            })
            .ToListAsync(cancellationToken);
        var accounts = await dbContext.Set<Domain.PersonnelFiles.PersonnelFileBankAccount>()
            .AsNoTracking()
            .Where(account => fileIds.Contains(account.PersonnelFileId))
            .Select(account => new
            {
                account.PersonnelFileId,
                account.PublicId,
                account.BankCode,
                account.AccountTypeCode,
                account.AccountNumber,
                account.IsPrimary,
            })
            .ToListAsync(cancellationToken);

        var assignmentsByFile = assignments.ToLookup(assignment => assignment.PersonnelFileId);
        var accountsByFile = accounts.ToLookup(account => account.PersonnelFileId);

        var rows = new List<ConciliacionBancariaExportRow>(employees.Count);
        foreach (var employee in employees)
        {
            var paying = assignmentsByFile[employee.PersonnelFileId]
                .OrderByDescending(assignment => assignment.IsPrimary)
                .FirstOrDefault(assignment => assignment.PaymentMethodCode != null || assignment.PaymentBankAccountPublicId != null)
                ?? assignmentsByFile[employee.PersonnelFileId].OrderByDescending(assignment => assignment.IsPrimary).FirstOrDefault();
            var fileAccounts = accountsByFile[employee.PersonnelFileId].ToArray();

            // The plaza's designated payment account wins; the PRIMARY account is the fallback.
            var account = paying?.PaymentBankAccountPublicId is { } designated
                ? fileAccounts.FirstOrDefault(item => item.PublicId == designated)
                : null;
            account ??= fileAccounts.FirstOrDefault(item => item.IsPrimary) ?? fileAccounts.FirstOrDefault();

            rows.Add(new ConciliacionBancariaExportRow(
                employee.EmployeeName,
                employee.EmployeeCode,
                paying?.PaymentMethodCode,
                account?.BankCode,
                account?.AccountTypeCode,
                account?.AccountNumber,
                Math.Round(employee.Net, 2, MidpointRounding.AwayFromZero),
                run.CurrencyCode,
                account is null ? PayrollRunReportingConstants.NoBankAccountWarning : null));
        }

        return rows;
    }

    public async Task<PayrollRunEmployeeHistoryResponse> QueryEmployeeHistoryAsync(
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
        CancellationToken cancellationToken)
    {
        var query =
            from line in dbContext.Set<PayrollRunLine>().AsNoTracking()
            join run in dbContext.Set<PayrollRun>().AsNoTracking() on line.PayrollRunId equals run.Id
            where line.TenantId == tenantId &&
                  line.EmployeePublicId == personnelFilePublicId &&
                  line.IsIncluded &&
                  statusCodes.Contains(run.StatusCode)
            select new { line, run };

        if (year is { } filterYear)
        {
            query = query.Where(item => item.run.PeriodStartDate.Year == filterYear);
        }

        if (payrollDefinitionPublicId is { } definitionPublicId)
        {
            query = query.Where(item => dbContext.Set<PayrollDefinition>()
                .Any(definition => definition.PublicId == definitionPublicId && definition.Id == item.run.PayrollDefinitionId));
        }

        if (!string.IsNullOrWhiteSpace(payrollTypeCode))
        {
            var normalizedType = payrollTypeCode.Trim().ToUpperInvariant();
            query = query.Where(item => item.run.PayrollTypeCode == normalizedType);
        }

        if (from is { } fromDate)
        {
            query = query.Where(item => item.run.PeriodEndDate >= fromDate);
        }

        if (to is { } toDate)
        {
            query = query.Where(item => item.run.PeriodStartDate <= toDate);
        }

        var grouped = query.GroupBy(item => new
        {
            item.run.Id,
            item.run.PublicId,
            item.run.PayrollDefinitionCode,
            item.run.PayrollDefinitionName,
            item.run.PayrollTypeCode,
            item.run.PeriodLabel,
            item.run.PeriodStartDate,
            item.run.PeriodEndDate,
            item.run.PaymentDate,
            item.run.StatusCode,
            item.run.CurrencyCode,
        });

        var totalCount = await grouped.CountAsync(cancellationToken);
        var items = (await grouped
                .OrderByDescending(group => group.Key.PeriodStartDate)
                .ThenByDescending(group => group.Key.Id)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(group => new
                {
                    group.Key,
                    Income = group.Sum(item => item.line.LineClass == PayrollLineClasses.Ingreso
                        ? (item.line.OverrideAmount ?? item.line.CalculatedAmount)
                        : 0m),
                    Deductions = group.Sum(item => item.line.LineClass == PayrollLineClasses.Descuento
                        ? (item.line.OverrideAmount ?? item.line.CalculatedAmount)
                        : 0m),
                })
                .ToListAsync(cancellationToken))
            .Select(item => new PayrollRunEmployeeHistoryItemResponse(
                item.Key.PublicId,
                item.Key.PayrollDefinitionCode,
                item.Key.PayrollDefinitionName,
                item.Key.PayrollTypeCode,
                item.Key.PeriodLabel,
                item.Key.PeriodStartDate,
                item.Key.PeriodEndDate,
                item.Key.PaymentDate,
                item.Key.StatusCode,
                item.Key.CurrencyCode,
                Math.Round(item.Income, 2, MidpointRounding.AwayFromZero),
                Math.Round(item.Deductions, 2, MidpointRounding.AwayFromZero),
                Math.Round(item.Income - item.Deductions, 2, MidpointRounding.AwayFromZero)))
            .ToList();

        return new PayrollRunEmployeeHistoryResponse(personnelFilePublicId, items, pageNumber, pageSize, totalCount);
    }

    /// <summary>Base bandeja filter (tenant + Nómina + period + year) shared by the query and its export.</summary>
    private async Task<IQueryable<PayrollRun>> BuildFilteredRunsQuery(
        Guid tenantId,
        Guid? payrollDefinitionPublicId,
        Guid? payrollPeriodPublicId,
        int? year,
        CancellationToken cancellationToken)
    {
        var runs = dbContext.Set<PayrollRun>().AsNoTracking().Where(run => run.TenantId == tenantId);

        if (payrollDefinitionPublicId is { } definitionPublicId)
        {
            var definitionId = await dbContext.Set<PayrollDefinition>()
                .Where(definition => definition.PublicId == definitionPublicId)
                .Select(definition => (long?)definition.Id)
                .SingleOrDefaultAsync(cancellationToken);
            runs = runs.Where(run => run.PayrollDefinitionId == (definitionId ?? -1));
        }

        if (payrollPeriodPublicId is { } periodPublicId)
        {
            var periodId = await dbContext.Set<Domain.Leave.PayrollPeriodDefinition>()
                .Where(period => period.PublicId == periodPublicId)
                .Select(period => (long?)period.Id)
                .SingleOrDefaultAsync(cancellationToken);
            runs = runs.Where(run => run.PayrollPeriodId == (periodId ?? -1));
        }

        if (year is { } filterYear)
        {
            runs = runs.Where(run => run.PeriodStartDate.Year == filterYear);
        }

        return runs;
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

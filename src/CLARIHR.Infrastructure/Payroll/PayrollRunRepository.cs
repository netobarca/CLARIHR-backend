using CLARIHR.Application.Abstractions.Payroll;
using CLARIHR.Application.Features.Payroll;
using CLARIHR.Domain.Leave;
using CLARIHR.Domain.Payroll;
using CLARIHR.Domain.PersonnelFiles;
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

    public async Task<(IReadOnlyList<PlanillaEmpleadoRow> Rows, PlanillaEmpleadoRow Totals)?>
        GetEmployeeMatrixRowsAsync(
            Guid tenantId,
            Guid payrollRunPublicId,
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

        // Solo las líneas INCLUIDAS: la que se excluyó en revisión no se paga y no debe sumar. El importe final es
        // el override auditado cuando existe, igual que en el resto del módulo.
        var lines = await dbContext.Set<PayrollRunLine>()
            .AsNoTracking()
            .Where(line => line.PayrollRunId == run.Id && line.IsIncluded)
            .Select(line => new
            {
                line.EmployeePublicId,
                line.EmployeeName,
                line.EmployeeCode,
                line.ConceptCode,
                line.LineClass,
                line.IncomeClass,
                line.DeductionClass,
                line.Units,
                line.BaseAmount,
                line.UnpaidDays,
                line.EmployerPaidDays,
                line.SubsidizedDays,
                Final = line.OverrideAmount ?? line.CalculatedAmount,
            })
            .ToListAsync(cancellationToken);

        var rows = lines
            // Se agrupa por el EMPLEADO, no por nombre + centro de costo: multi-plaza consolida en una fila y dos
            // homónimos no se mezclan.
            .GroupBy(line => line.EmployeePublicId)
            .Select(group =>
            {
                decimal Income(Domain.Common.IncomeClass income) =>
                    group.Where(line => line.LineClass == PayrollLineClasses.Ingreso && line.IncomeClass == income)
                        .Sum(line => line.Final);

                var incomeTotal = group.Where(line => line.LineClass == PayrollLineClasses.Ingreso).Sum(line => line.Final);
                var salary = Income(Domain.Common.IncomeClass.Salario);
                var bonus = Income(Domain.Common.IncomeClass.Bono);
                var commission = Income(Domain.Common.IncomeClass.Comision);
                var overtime = Income(Domain.Common.IncomeClass.HorasExtra);
                var nonDeductible = Income(Domain.Common.IncomeClass.NoDeducible);
                var christmas = Income(Domain.Common.IncomeClass.Aguinaldo);
                // Todo ingreso sin clasificar (incluido `Otro`) cae acá, para que el cuadre nunca se rompa.
                var otherIncome = incomeTotal - salary - bonus - commission - overtime - nonDeductible - christmas;

                decimal Deduction(string conceptCode) =>
                    group.Where(line => line.LineClass == PayrollLineClasses.Descuento && line.ConceptCode == conceptCode)
                        .Sum(line => line.Final);
                decimal DeductionOfClass(Domain.Common.DeductionClass deduction) =>
                    group.Where(line => line.LineClass == PayrollLineClasses.Descuento && line.DeductionClass == deduction)
                        .Sum(line => line.Final);

                var deductionTotal = group.Where(line => line.LineClass == PayrollLineClasses.Descuento).Sum(line => line.Final);
                var isss = Deduction(PayrollEngineConceptCodes.Isss);
                var afp = Deduction(PayrollEngineConceptCodes.Afp);
                var renta = Deduction(PayrollEngineConceptCodes.Renta);
                var external = DeductionOfClass(Domain.Common.DeductionClass.Externo);
                var internalDeductions = DeductionOfClass(Domain.Common.DeductionClass.Interno);
                var otherDeductions = deductionTotal - isss - afp - renta - external - internalDeductions;

                // Los días del periodo se toman UNA vez: en multi-plaza cada plaza aporta su propia línea de
                // salario con los mismos 15 días, y sumarlos daría 30.
                var periodDays = group
                    .Where(line => line.LineClass == PayrollLineClasses.Ingreso
                                   && line.IncomeClass == Domain.Common.IncomeClass.Salario)
                    .Max(line => line.Units) ?? 0m;
                var unpaidDays = group.Sum(line => line.UnpaidDays ?? 0m);
                var employerDays = group.Sum(line => line.EmployerPaidDays ?? 0m);
                var subsidizedDays = group.Sum(line => line.SubsidizedDays ?? 0m);
                // La incapacidad reparte sus días en dos líneas: el descuento del empleado (Descuento) y el aporte
                // patronal (PagoPatronal). `unpaidDays` de la incapacidad son los SIN_PAGO.
                var incapacityUnpaid = group
                    .Where(line => line.ConceptCode == IncapacityConceptCode || line.ConceptCode == IncapacityEmployerConceptCode)
                    .Sum(line => line.UnpaidDays ?? 0m);
                var leaveUnpaid = unpaidDays - incapacityUnpaid;

                // El equivalente pagado por la empresa sobre los días de incapacidad se deriva del MONTO que el
                // motor pagó, no de un porcentaje: los tramos por riesgo pueden diferir dentro de la misma
                // incapacidad. `baseAmount` de la línea de salario es el mensual, así que la diaria es /30.
                var monthlyBase = group
                    .Where(line => line.IncomeClass == Domain.Common.IncomeClass.Salario)
                    .Sum(line => line.BaseAmount ?? 0m);
                var employerIncapacityAmount = group
                    .Where(line => line.LineClass == PayrollLineClasses.PagoPatronal
                                   && line.ConceptCode == IncapacityEmployerConceptCode)
                    .Sum(line => line.Final);
                var dailyRate = monthlyBase / 30m;
                var employerPaidEquivalent = dailyRate > 0m ? employerIncapacityAmount / dailyRate : 0m;

                var paidEquivalent = periodDays - leaveUnpaid - subsidizedDays - incapacityUnpaid
                                     - employerDays + employerPaidEquivalent;

                var first = group.First();
                return new PlanillaEmpleadoRow(
                    first.EmployeeCode,
                    first.EmployeeName,
                    periodDays,
                    Round2(leaveUnpaid),
                    employerDays,
                    subsidizedDays,
                    incapacityUnpaid,
                    Round2(paidEquivalent),
                    monthlyBase,
                    salary,
                    bonus,
                    commission,
                    overtime,
                    nonDeductible,
                    christmas,
                    otherIncome,
                    incomeTotal,
                    isss,
                    afp,
                    renta,
                    external,
                    internalDeductions,
                    otherDeductions,
                    deductionTotal,
                    incomeTotal - deductionTotal,
                    run.CurrencyCode);
            })
            .OrderBy(row => row.Empleado, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var totals = new PlanillaEmpleadoRow(
            null,
            "TOTAL",
            rows.Sum(row => row.DiasPeriodo),
            rows.Sum(row => row.DiasSinGoce),
            rows.Sum(row => row.DiasIncapacidadEmpresa),
            rows.Sum(row => row.DiasIncapacidadIsss),
            rows.Sum(row => row.DiasIncapacidadSinPago),
            rows.Sum(row => row.DiasPagadosEquivalentes),
            rows.Sum(row => row.SalarioBase),
            rows.Sum(row => row.SalarioDelPeriodo),
            rows.Sum(row => row.Bonos),
            rows.Sum(row => row.Comisiones),
            rows.Sum(row => row.HorasExtras),
            rows.Sum(row => row.IngresosAdicionales),
            rows.Sum(row => row.Aguinaldo),
            rows.Sum(row => row.OtrosIngresos),
            rows.Sum(row => row.IngresoTotal),
            rows.Sum(row => row.Isss),
            rows.Sum(row => row.Afp),
            rows.Sum(row => row.Renta),
            rows.Sum(row => row.DescuentosExternos),
            rows.Sum(row => row.DescuentosInternos),
            rows.Sum(row => row.OtrosDescuentos),
            rows.Sum(row => row.TotalDescuentos),
            rows.Sum(row => row.LiquidoAPagar),
            run.CurrencyCode);

        return (rows, totals);
    }

    private const string IncapacityConceptCode = "INCAPACIDAD";
    private const string IncapacityEmployerConceptCode = "INCAPACIDAD_PATRONAL";

    private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

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

    public async Task<IReadOnlyCollection<F14ExportRow>> GetMonthlyIncomeTaxWithholdingRowsAsync(
        Guid tenantId,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        // RN-03/P-01/P-09 — consolidate every CERRADA run whose period falls in this calendar month,
        // regardless of the Nómina's own frequency (mensual/quincenal/semanal all count).
        var runIds = await (
            from run in dbContext.Set<PayrollRun>().AsNoTracking()
            join period in dbContext.Set<PayrollPeriodDefinition>().AsNoTracking() on run.PayrollPeriodId equals period.Id
            where run.TenantId == tenantId &&
                  run.StatusCode == PayrollRunStatuses.Cerrada &&
                  period.Year == year &&
                  period.Month == month
            select run.Id)
            .ToListAsync(cancellationToken);

        if (runIds.Count == 0)
        {
            return [];
        }

        var lines = await dbContext.Set<PayrollRunLine>()
            .AsNoTracking()
            .Where(line => runIds.Contains(line.PayrollRunId) && line.IsIncluded && line.ConceptCode == PayrollEngineConceptCodes.Renta)
            .Select(line => new
            {
                line.PersonnelFileId,
                line.EmployeeName,
                line.EmployeeCode,
                BaseAmount = line.BaseAmount ?? 0m,
                Final = line.OverrideAmount ?? line.CalculatedAmount,
            })
            .ToListAsync(cancellationToken);

        if (lines.Count == 0)
        {
            return [];
        }

        var fileIds = lines.Select(line => line.PersonnelFileId).Distinct().ToList();
        var nitByFileId = await dbContext.Set<PersonnelFileIdentification>()
            .AsNoTracking()
            .Where(identification => fileIds.Contains(identification.PersonnelFileId) && identification.IdentificationType == "NIT")
            .ToDictionaryAsync(identification => identification.PersonnelFileId, identification => identification.IdentificationNumber, cancellationToken);

        return lines
            .GroupBy(line => new { line.PersonnelFileId, line.EmployeeName, line.EmployeeCode })
            .OrderBy(group => group.Key.EmployeeName)
            .Select(group =>
            {
                var nit = nitByFileId.GetValueOrDefault(group.Key.PersonnelFileId);
                return new F14ExportRow(
                    group.Key.EmployeeName,
                    group.Key.EmployeeCode,
                    nit,
                    group.Sum(line => line.BaseAmount),
                    group.Sum(line => line.Final),
                    nit is null ? "Sin NIT registrado." : null);
            })
            .ToList();
    }

    public async Task<IReadOnlyCollection<PlanillaUnicaExportRow>> GetMonthlySocialSecurityContributionRowsAsync(
        Guid tenantId,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        var runIds = await (
            from run in dbContext.Set<PayrollRun>().AsNoTracking()
            join period in dbContext.Set<PayrollPeriodDefinition>().AsNoTracking() on run.PayrollPeriodId equals period.Id
            where run.TenantId == tenantId &&
                  run.StatusCode == PayrollRunStatuses.Cerrada &&
                  period.Year == year &&
                  period.Month == month
            select run.Id)
            .ToListAsync(cancellationToken);

        if (runIds.Count == 0)
        {
            return [];
        }

        var socialSecurityConceptCodes = new[]
        {
            PayrollEngineConceptCodes.Isss,
            PayrollEngineConceptCodes.IsssPatronal,
            PayrollEngineConceptCodes.Afp,
            PayrollEngineConceptCodes.AfpPatronal,
        };

        var lines = await dbContext.Set<PayrollRunLine>()
            .AsNoTracking()
            .Where(line => runIds.Contains(line.PayrollRunId) && line.IsIncluded && socialSecurityConceptCodes.Contains(line.ConceptCode))
            .Select(line => new
            {
                line.PersonnelFileId,
                line.EmployeeName,
                line.EmployeeCode,
                line.ConceptCode,
                BaseAmount = line.BaseAmount ?? 0m,
                Final = line.OverrideAmount ?? line.CalculatedAmount,
            })
            .ToListAsync(cancellationToken);

        if (lines.Count == 0)
        {
            return [];
        }

        var fileIds = lines.Select(line => line.PersonnelFileId).Distinct().ToList();
        var nupIsssByFileId = await dbContext.Set<PersonnelFileIdentification>()
            .AsNoTracking()
            .Where(identification => fileIds.Contains(identification.PersonnelFileId) && identification.IdentificationType == "NUP_ISSS")
            .ToDictionaryAsync(identification => identification.PersonnelFileId, identification => identification.IdentificationNumber, cancellationToken);
        var afpByFileId = await dbContext.Set<PersonnelFile>()
            .AsNoTracking()
            .Where(file => fileIds.Contains(file.Id))
            .ToDictionaryAsync(file => file.Id, file => new { file.AfpCode, file.AfpAccountNumber }, cancellationToken);

        return lines
            .GroupBy(line => new { line.PersonnelFileId, line.EmployeeName, line.EmployeeCode })
            .OrderBy(group => group.Key.EmployeeName)
            .Select(group =>
            {
                var nupIsss = nupIsssByFileId.GetValueOrDefault(group.Key.PersonnelFileId);
                var afp = afpByFileId.GetValueOrDefault(group.Key.PersonnelFileId);
                var isssLine = group.Where(line => line.ConceptCode == PayrollEngineConceptCodes.Isss).ToList();
                var warnings = new List<string>();
                if (nupIsss is null)
                {
                    warnings.Add("sin NUP ISSS registrado");
                }

                if (afp?.AfpAccountNumber is null)
                {
                    warnings.Add("sin cuenta AFP registrada");
                }

                return new PlanillaUnicaExportRow(
                    group.Key.EmployeeName,
                    group.Key.EmployeeCode,
                    nupIsss,
                    isssLine.Sum(line => line.BaseAmount),
                    isssLine.Sum(line => line.Final),
                    group.Where(line => line.ConceptCode == PayrollEngineConceptCodes.IsssPatronal).Sum(line => line.Final),
                    afp?.AfpCode,
                    afp?.AfpAccountNumber,
                    group.Where(line => line.ConceptCode == PayrollEngineConceptCodes.Afp).Sum(line => line.Final),
                    group.Where(line => line.ConceptCode == PayrollEngineConceptCodes.AfpPatronal).Sum(line => line.Final),
                    warnings.Count == 0 ? null : string.Join("; ", warnings) + ".");
            })
            .ToList();
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
                account.Id,
            })
            // H-27 — orden explícito: la elección de la cuenta era `FirstOrDefault(item => item.IsPrimary)` sobre
            // una consulta SIN ordenar, o sea el orden FÍSICO de las filas, que cambia con los updates y el vacuum.
            // Con el índice único parcial ya no puede haber dos primarias, pero el orden explícito hace la elección
            // estable también sobre datos escritos antes del arreglo — y deja de depender de un detalle del motor.
            .OrderByDescending(account => account.IsPrimary)
            .ThenBy(account => account.Id)
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

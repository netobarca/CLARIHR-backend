using System.Text.Json;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Compliance;
using CLARIHR.Application.Abstractions.Leave;
using CLARIHR.Application.Abstractions.Payroll;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Preferences;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles.Compensation;
using CLARIHR.Domain.Leave;
using CLARIHR.Domain.Payroll;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.Payroll;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// REQ-012 §3.4/§3.5 — generation. The provider (Infrastructure) brings RAW data; this assembler turns it
// into the pure engine's input + the pool application plan USING THE MODULES' OWN pure rules (installment
// derivation is never re-implemented — the same Support/Rules the apply-period batches run). The generate
// handler then persists the run and applies the INCLUDED pool lines with origin MOTOR under the modules'
// own locks; any record that changed mid-flight rolls the WHOLE transaction back (409 POOL_CONFLICT).
// ─────────────────────────────────────────────────────────────────────────────────────────────

internal static class PayrollRunAssembler
{
    internal sealed record PoolApplyTarget(
        string SourceModule,
        Guid RecordPublicId,
        IReadOnlyList<int> InstallmentNumbers);

    internal sealed record AssembledRun(
        PayrollCalculationInput EngineInput,
        IReadOnlyList<PoolApplyTarget> PoolTargets,
        IReadOnlyDictionary<Guid, PayrollPopulationRow> RowByPlaza,
        IReadOnlyDictionary<Guid, PayrollPopulationRow> PrimaryRowByFile,
        int CarryoverCount);

    public static AssembledRun Assemble(
        PayrollDefinition definition,
        PayrollPeriodDefinition period,
        PayrollRunSourceData source)
    {
        var rowByPlaza = source.Population.ToDictionary(row => row.AssignedPositionPublicId);
        var primaryRowByFile = source.Population
            .GroupBy(row => row.PersonnelFilePublicId)
            .ToDictionary(
                group => group.Key,
                group => group.FirstOrDefault(row => row.IsPrimary) ?? group.First());

        var incomesByPlaza = new Dictionary<Guid, List<PayrollIncomeItem>>();
        var overtimeByPlaza = new Dictionary<Guid, List<PayrollOvertimeItem>>();
        var deductionsByPlaza = new Dictionary<Guid, List<PayrollDeductionItem>>();
        var employerByPlaza = new Dictionary<Guid, List<PayrollEmployerItem>>();
        var poolTargets = new List<PoolApplyTarget>();
        var carryovers = 0;

        static List<TItem> Bucket<TItem>(Dictionary<Guid, List<TItem>> buckets, Guid plaza)
        {
            if (!buckets.TryGetValue(plaza, out var list))
            {
                list = [];
                buckets[plaza] = list;
            }

            return list;
        }

        Guid? PlazaFor(Guid filePublicId, Guid? declaredPlaza)
        {
            if (declaredPlaza is { } plaza && rowByPlaza.ContainsKey(plaza))
            {
                return plaza;
            }

            return primaryRowByFile.TryGetValue(filePublicId, out var primary)
                ? primary.AssignedPositionPublicId
                : null;
        }

        // Recurring incomes — one engine item per pending installment up to the period end, derived with
        // the module's OWN rules (the amounts the apply step re-derives and must match).
        foreach (var row in source.RecurringIncomes)
        {
            var plaza = PlazaFor(row.PersonnelFilePublicId, row.AssignedPositionPublicId);
            if (plaza is null)
            {
                continue;
            }

            var pending = RecurringIncomeInstallmentSupport.PendingInstallmentsUpTo(row.Plan, period.EndDate);
            if (pending.Count == 0)
            {
                continue;
            }

            var plan = new RecurringIncomePlan(row.Plan.InstallmentValue, row.Plan.InstallmentCount, row.Plan.TotalAmount, row.Plan.IsIndefinite);
            foreach (var number in pending)
            {
                Bucket(incomesByPlaza, plaza.Value).Add(new PayrollIncomeItem(
                    row.ConceptCode,
                    row.ConceptName,
                    RecurringIncomeRules.InstallmentAmountFor(number, plan),
                    PayrollSourceModules.RecurringIncome,
                    row.PublicId,
                    AffectsIsss: true,
                    AffectsAfp: true,
                    AffectsRenta: true,
                    Units: number));
            }

            poolTargets.Add(new PoolApplyTarget(PayrollSourceModules.RecurringIncome, row.PublicId, pending));
        }

        // One-time incomes — the single application's amount is the income's own.
        foreach (var row in source.OneTimeIncomes.OrderBy(item => item.OneTimeIncomePublicId))
        {
            var plaza = PlazaFor(row.PersonnelFilePublicId, null);
            if (plaza is null)
            {
                continue;
            }

            Bucket(incomesByPlaza, plaza.Value).Add(new PayrollIncomeItem(
                row.ConceptTypeCode,
                row.ConceptNameSnapshot,
                row.Amount,
                PayrollSourceModules.OneTimeIncome,
                row.OneTimeIncomePublicId,
                AffectsIsss: true,
                AffectsAfp: true,
                AffectsRenta: true));
            poolTargets.Add(new PoolApplyTarget(PayrollSourceModules.OneTimeIncome, row.OneTimeIncomePublicId, []));
        }

        // Overtime — the engine values Σ(hours×factor) per plaza in one line (golden 2).
        foreach (var row in source.OvertimeRecords.OrderBy(item => item.OvertimeRecordPublicId))
        {
            var plaza = PlazaFor(row.PersonnelFilePublicId, null);
            if (plaza is null)
            {
                continue;
            }

            Bucket(overtimeByPlaza, plaza.Value).Add(new PayrollOvertimeItem(
                row.DurationDecimalHours,
                row.FactorApplied,
                row.OvertimeRecordPublicId));
            poolTargets.Add(new PoolApplyTarget(PayrollSourceModules.Overtime, row.OvertimeRecordPublicId, []));
        }

        // Recurring deductions — deferrable (P-08 LIFO: higher order = more recent = deferred first).
        var deferralOrder = 0;
        foreach (var row in source.RecurringDeductions)
        {
            var plaza = PlazaFor(row.PersonnelFilePublicId, row.AssignedPositionPublicId);
            if (plaza is null)
            {
                continue;
            }

            var pending = RecurringDeductionInstallmentSupport.PendingChargesUpTo(row.Plan, period.EndDate);
            if (pending.Count == 0)
            {
                continue;
            }

            var plan = RecurringDeductionInstallmentSupport.ToPlan(row.Plan);
            deferralOrder++;
            foreach (var number in pending)
            {
                var (amount, _, _) = RecurringDeductionRules.ChargeSplitFor(number, plan, row.Plan.ApplicationFrequencyCode);
                Bucket(deductionsByPlaza, plaza.Value).Add(new PayrollDeductionItem(
                    row.ConceptCode,
                    row.ConceptName,
                    amount,
                    PayrollSourceModules.RecurringDeduction,
                    row.PublicId,
                    IsDeferrable: true,
                    DeferralOrder: deferralOrder,
                    Units: number));
            }

            poolTargets.Add(new PoolApplyTarget(PayrollSourceModules.RecurringDeduction, row.PublicId, pending));
        }

        // One-time deductions — deferrable, most recent charge deferred first.
        foreach (var row in source.OneTimeDeductions.OrderBy(item => item.DeductionDate).ThenBy(item => item.OneTimeDeductionPublicId))
        {
            var plaza = PlazaFor(row.PersonnelFilePublicId, null);
            if (plaza is null)
            {
                continue;
            }

            deferralOrder++;
            Bucket(deductionsByPlaza, plaza.Value).Add(new PayrollDeductionItem(
                row.ConceptTypeCode,
                row.ConceptNameSnapshot,
                row.Amount,
                PayrollSourceModules.OneTimeDeduction,
                row.OneTimeDeductionPublicId,
                IsDeferrable: true,
                DeferralOrder: deferralOrder));
            poolTargets.Add(new PoolApplyTarget(PayrollSourceModules.OneTimeDeduction, row.OneTimeDeductionPublicId, []));
        }

        // Registro inputs — never deferrable (P-08: only VOLUNTARY pool charges defer).
        foreach (var row in source.NotWorkedTimes)
        {
            var plaza = PlazaFor(row.PersonnelFilePublicId, null);
            if (plaza is null)
            {
                continue;
            }

            if (row.IsCarryover)
            {
                carryovers++;
            }

            Bucket(deductionsByPlaza, plaza.Value).Add(new PayrollDeductionItem(
                row.ConceptCode,
                row.ConceptName,
                row.Amount,
                PayrollSourceModules.NotWorkedTime,
                row.RecordPublicId,
                IsCarryover: row.IsCarryover));
        }

        foreach (var row in source.DisciplinaryActions)
        {
            var plaza = PlazaFor(row.PersonnelFilePublicId, null);
            if (plaza is null)
            {
                continue;
            }

            if (row.IsCarryover)
            {
                carryovers++;
            }

            Bucket(deductionsByPlaza, plaza.Value).Add(new PayrollDeductionItem(
                row.ConceptCode,
                row.ConceptName,
                row.Amount,
                PayrollSourceModules.Disciplinary,
                row.RecordPublicId,
                IsCarryover: row.IsCarryover));
        }

        foreach (var row in source.Incapacities)
        {
            var plaza = PlazaFor(row.PersonnelFilePublicId, null);
            if (plaza is null)
            {
                continue;
            }

            if (row.Amount > 0m)
            {
                Bucket(deductionsByPlaza, plaza.Value).Add(new PayrollDeductionItem(
                    row.ConceptCode,
                    row.ConceptName,
                    row.Amount,
                    PayrollSourceModules.Incapacity,
                    row.RecordPublicId));
            }

            if (row.EmployerAmount > 0m)
            {
                Bucket(employerByPlaza, plaza.Value).Add(new PayrollEmployerItem(
                    "INCAPACIDAD_PATRONAL",
                    "Aporte patronal de incapacidad",
                    row.EmployerAmount,
                    PayrollSourceModules.Incapacity,
                    row.RecordPublicId));
            }
        }

        var employees = source.Population
            .GroupBy(row => row.PersonnelFilePublicId)
            .OrderBy(group => group.Key)
            .Select(group => new PayrollEmployeeInput(
                group.Key,
                group
                    .OrderByDescending(row => row.IsPrimary)
                    .ThenBy(row => row.AssignedPositionPublicId)
                    .Select(row => new PayrollPlazaInput(
                        row.AssignedPositionPublicId,
                        row.MonthlyBaseSalary,
                        incomesByPlaza.TryGetValue(row.AssignedPositionPublicId, out var incomes) ? incomes : [],
                        overtimeByPlaza.TryGetValue(row.AssignedPositionPublicId, out var overtime) ? overtime : [],
                        deductionsByPlaza.TryGetValue(row.AssignedPositionPublicId, out var deductions) ? deductions : [],
                        employerByPlaza.TryGetValue(row.AssignedPositionPublicId, out var employer) ? employer : []))
                    .ToArray(),
                group.Select(row => row.MinimumMonthlyWage).FirstOrDefault(value => value.HasValue)))
            .ToArray();

        var engineInput = new PayrollCalculationInput(
            definition.PayrollTypeCode,
            definition.PayPeriodCode,
            definition.GuaranteesMinimumIncome,
            new PayrollContributionScheme(source.Isss.EmployeeRatePercent, source.Isss.EmployerRatePercent, source.Isss.MonthlyContributionCap),
            new PayrollContributionScheme(source.Afp.EmployeeRatePercent, source.Afp.EmployerRatePercent, source.Afp.MonthlyContributionCap),
            source.IncafRatePercent,
            source.RentaBrackets,
            employees);

        // Deterministic global apply order (anti-deadlock §0.18): by module then record public id — every
        // concurrent generation walks the same sequence.
        var orderedTargets = poolTargets
            .OrderBy(target => target.SourceModule, StringComparer.Ordinal)
            .ThenBy(target => target.RecordPublicId)
            .ToArray();

        return new AssembledRun(engineInput, orderedTargets, rowByPlaza, primaryRowByFile, carryovers);
    }
}

internal sealed class GeneratePayrollRunCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPayrollDefinitionRepository definitionRepository,
    IPayrollPeriodRepository periodRepository,
    IPayrollRunRepository runRepository,
    IPayrollCalculationDataProvider dataProvider,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICompanyPreferenceRepository companyPreferenceRepository,
    ICompanyLegalProfileRepository companyLegalProfileRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<GeneratePayrollRunCommand, PayrollRunResponse>
{
    public async Task<Result<PayrollRunResponse>> Handle(
        GeneratePayrollRunCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManagePayrollRunsAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PayrollRunResponse>.Failure(authorizationResult.Error);
        }

        // REQ-016 Gate A (ratified P-03) — off by default (§0.11/§2.3 of the technical plan): only
        // enforced once the tenant's compliance gates are turned on, after its data-capture campaign.
        var preference = await companyPreferenceRepository.GetByTenantIdAsync(command.CompanyId, cancellationToken);
        if (preference?.PayrollComplianceGatesEnabled == true)
        {
            var legalProfile = await companyLegalProfileRepository.GetByTenantIdAsync(command.CompanyId, cancellationToken);
            if (legalProfile is null)
            {
                return Result<PayrollRunResponse>.Failure(PayrollRunErrors.MissingLegalProfile);
            }
        }

        var context = await PayrollRunGenerationSupport.ResolveAsync(
            definitionRepository, periodRepository, command.CompanyId,
            command.PayrollDefinitionPublicId, command.PayrollPeriodPublicId, cancellationToken);
        if (context.IsFailure)
        {
            return Result<PayrollRunResponse>.Failure(context.Error);
        }

        var (definition, period) = context.Value;
        var now = dateTimeProvider.UtcNow;
        var today = DateOnly.FromDateTime(now);
        _ = Guid.TryParse(currentUserService.UserId, out var generatedByUserId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // The advisory lock rides the transaction (§0.18) and serializes every generation of this
            // Nómina × period; the sequential probe closes the friendly case, the partial unique index the race.
            await runRepository.AcquirePayrollRunMutationLockAsync(definition.Id, period.Id, cancellationToken);
            if (await runRepository.HasActiveRunAsync(command.CompanyId, definition.Id, period.Id, cancellationToken))
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<PayrollRunResponse>.Failure(PayrollRunErrors.AlreadyActive);
            }

            var source = await dataProvider.BuildAsync(command.CompanyId, definition, period, command.EmployeeIds, today, cancellationToken);
            var assembled = PayrollRunAssembler.Assemble(definition, period, source);
            var calculation = PayrollCalculationRules.Calculate(assembled.EngineInput);

            var run = PayrollRun.Create(
                definition.Id,
                period.Id,
                definition.Code,
                definition.Name,
                definition.PayrollTypeCode,
                period.Label,
                period.StartDate,
                period.EndDate,
                period.PaymentDate,
                definition.CurrencyCode,
                generatedByUserId,
                now);
            run.SetTenantId(command.CompanyId);

            var lines = new List<PayrollRunLine>(calculation.Lines.Count);
            foreach (var line in calculation.Lines)
            {
                var snapshot = assembled.PrimaryRowByFile[line.PersonnelFilePublicId];
                var plazaRow = line.AssignedPositionPublicId is { } plaza && assembled.RowByPlaza.TryGetValue(plaza, out var byPlaza)
                    ? byPlaza
                    : snapshot;
                var entity = PayrollRunLine.Create(
                    snapshot.PersonnelFileId,
                    line.PersonnelFilePublicId,
                    snapshot.EmployeeFullName,
                    snapshot.EmployeeCode,
                    line.AssignedPositionPublicId,
                    line.AssignedPositionPublicId is null ? null : plazaRow.CostCenterName,
                    line.ConceptCode,
                    line.ConceptName,
                    line.LineClass,
                    line.Units,
                    line.BaseAmount,
                    line.CalculatedAmount,
                    line.IsIncluded,
                    line.SourceModule,
                    line.SourceReferencePublicId,
                    definition.CurrencyCode,
                    line.WarningCodes.Count == 0 ? null : JsonSerializer.Serialize(line.WarningCodes),
                    line.SortOrder);
                entity.SetTenantId(command.CompanyId);
                lines.Add(entity);
            }

            var warningsJson = calculation.Warnings.Count == 0
                ? null
                : JsonSerializer.Serialize(calculation.Warnings.Select(warning => new PayrollRunWarningResponse(
                    warning.Code, warning.PersonnelFilePublicId, warning.Context)));

            run.ReplaceLines(
                lines,
                calculation.Totals.EmployeeCount,
                calculation.Totals.TotalIncome,
                calculation.Totals.TotalDeductions,
                calculation.Totals.TotalEmployerCost,
                calculation.Totals.TotalNet,
                warningsJson);
            runRepository.Add(run);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            // §3.5 — apply the INCLUDED pool lines with origin MOTOR under the modules' own locks. A record
            // that changed between the scan and here (state, plan, amount) rolls the WHOLE generation back.
            var applyError = await PayrollRunGenerationSupport.ApplyPoolsAsync(
                employeeRepository, run, assembled.PoolTargets, period, today, generatedByUserId, now, cancellationToken);
            if (applyError is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<PayrollRunResponse>.Failure(applyError);
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = PayrollRunGenerationSupport.ToResponse(run, definition.PublicId, period.PublicId, calculation.Warnings);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PayrollRunGenerated,
                    AuditEntityTypes.PayrollRun,
                    run.PublicId,
                    $"{definition.Code} {period.Label}",
                    AuditActions.Create,
                    $"Generated payroll run for {definition.Code} — {period.Label}: {run.EmployeeCount} employee(s), net {run.TotalNet:0.00}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PayrollRunResponse>.Success(response);
        }
        catch (UniqueConstraintViolationException)
        {
            // The one-active-run partial unique index caught a concurrent generation.
            await transaction.RollbackAsync(cancellationToken);
            return Result<PayrollRunResponse>.Failure(PayrollRunErrors.AlreadyActive);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class PreflightPayrollRunCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPayrollDefinitionRepository definitionRepository,
    IPayrollPeriodRepository periodRepository,
    IPayrollCalculationDataProvider dataProvider,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<PreflightPayrollRunCommand, PayrollRunPreflightResponse>
{
    public async Task<Result<PayrollRunPreflightResponse>> Handle(
        PreflightPayrollRunCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManagePayrollRunsAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PayrollRunPreflightResponse>.Failure(authorizationResult.Error);
        }

        var context = await PayrollRunGenerationSupport.ResolveAsync(
            definitionRepository, periodRepository, command.CompanyId,
            command.PayrollDefinitionPublicId, command.PayrollPeriodPublicId, cancellationToken);
        if (context.IsFailure)
        {
            return Result<PayrollRunPreflightResponse>.Failure(context.Error);
        }

        var (definition, period) = context.Value;
        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow);
        var source = await dataProvider.BuildAsync(command.CompanyId, definition, period, command.EmployeeIds, today, cancellationToken);
        var assembled = PayrollRunAssembler.Assemble(definition, period, source);
        var calculation = PayrollCalculationRules.Calculate(assembled.EngineInput);

        var response = new PayrollRunPreflightResponse(
            calculation.Totals.EmployeeCount,
            calculation.Lines.Count(line => line.SourceModule == PayrollSourceModules.Salario),
            calculation.Lines.Count(line => line.SourceModule is PayrollSourceModules.RecurringIncome or PayrollSourceModules.OneTimeIncome),
            calculation.Lines.Count(line => line.SourceModule == PayrollSourceModules.Overtime),
            calculation.Lines.Count(line => line.SourceModule is PayrollSourceModules.RecurringDeduction or PayrollSourceModules.OneTimeDeduction),
            calculation.Lines.Count(line => line.SourceModule == PayrollSourceModules.NotWorkedTime),
            calculation.Lines.Count(line => line.SourceModule == PayrollSourceModules.Disciplinary),
            calculation.Lines.Count(line => line.SourceModule == PayrollSourceModules.Incapacity && line.LineClass == PayrollLineClasses.Descuento),
            assembled.CarryoverCount,
            calculation.Totals.TotalIncome,
            calculation.Totals.TotalDeductions,
            calculation.Totals.TotalNet,
            calculation.Warnings
                .Select(warning => new PayrollRunWarningResponse(warning.Code, warning.PersonnelFilePublicId, warning.Context))
                .ToArray());

        return Result<PayrollRunPreflightResponse>.Success(response);
    }
}

internal static class PayrollRunGenerationSupport
{
    /// <summary>Definition ACTIVE + period ACTIVE/GENERADO and hanging from that definition (422 otherwise).</summary>
    public static async Task<Result<(PayrollDefinition Definition, PayrollPeriodDefinition Period)>> ResolveAsync(
        IPayrollDefinitionRepository definitionRepository,
        IPayrollPeriodRepository periodRepository,
        Guid companyId,
        Guid payrollDefinitionPublicId,
        Guid payrollPeriodPublicId,
        CancellationToken cancellationToken)
    {
        var definition = await definitionRepository.GetByIdAsync(payrollDefinitionPublicId, cancellationToken);
        if (definition is null || definition.TenantId != companyId || !definition.IsActive)
        {
            return Result<(PayrollDefinition, PayrollPeriodDefinition)>.Failure(PayrollRunErrors.InputInvalid);
        }

        var period = await periodRepository.GetByIdAsync(payrollPeriodPublicId, cancellationToken);
        if (period is null ||
            period.TenantId != companyId ||
            !period.IsActive ||
            period.StatusCode != PayrollPeriodStatuses.Generado ||
            period.PayrollDefinitionId != definition.Id)
        {
            return Result<(PayrollDefinition, PayrollPeriodDefinition)>.Failure(PayrollRunErrors.InputInvalid);
        }

        return Result<(PayrollDefinition, PayrollPeriodDefinition)>.Success((definition, period));
    }

    /// <summary>
    /// Applies the INCLUDED pool lines of the run with origin MOTOR (§3.5): per target — the module's own
    /// advisory lock, the FIRST tracked load after it, the module's own pure-rule revalidation and a strict
    /// amount match against the persisted line; the created installment/application public id is bound back
    /// to the line (the reversal of PR-6 annuls exactly those). Deferred lines (IsIncluded=false) skip their
    /// application — the installment stays pending in its module. Any mismatch → error (caller rolls back).
    /// </summary>
    public static async Task<Error?> ApplyPoolsAsync(
        IPersonnelFileEmployeeRepository employeeRepository,
        PayrollRun run,
        IReadOnlyList<PayrollRunAssembler.PoolApplyTarget> targets,
        PayrollPeriodDefinition period,
        DateOnly today,
        Guid appliedByUserId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        foreach (var target in targets)
        {
            switch (target.SourceModule)
            {
                case PayrollSourceModules.RecurringIncome:
                {
                    var lines = IncludedLines(run, target);
                    if (lines.Count == 0)
                    {
                        break;
                    }

                    await employeeRepository.AcquireRecurringIncomeMutationLockAsync(target.RecordPublicId, cancellationToken);
                    var income = await employeeRepository.GetTrackedRecurringIncomeWithInstallmentsAsync(target.RecordPublicId, run.TenantId, cancellationToken);
                    if (income is null || income.StatusCode != RecurringIncomeStatuses.Vigente)
                    {
                        return PayrollRunErrors.PoolConflict;
                    }

                    var plan = RecurringIncomeInstallmentSupport.ToPlan(income);
                    foreach (var line in lines)
                    {
                        var number = (int)(line.Units ?? 0m);
                        var applied = RecurringIncomeInstallmentSupport.ActiveAppliedNumbers(income);
                        if (!RecurringIncomeRules.CanApplyInstallment(income.StatusCode, number, plan, applied).IsValid ||
                            RecurringIncomeRules.InstallmentAmountFor(number, plan) != line.CalculatedAmount)
                        {
                            return PayrollRunErrors.PoolConflict;
                        }

                        var installment = income.ApplyInstallment(
                            number,
                            today,
                            RecurringIncomeRules.TheoreticalDueDateFor(income.InstallmentFrequencyCode, income.InstallmentStartDate, number),
                            line.CalculatedAmount,
                            income.CurrencyCode,
                            income.PayrollTypeCode,
                            period.Id,
                            period.Label,
                            RecurringIncomeInstallmentOrigins.Motor,
                            appliedByUserId,
                            notes: null);
                        line.BindApplicationReference(installment.PublicId);
                    }

                    if (!income.IsIndefinite &&
                        RecurringIncomeRules.IsPlanComplete(plan, RecurringIncomeInstallmentSupport.ActiveAppliedNumbers(income)))
                    {
                        income.FinalizeByPlanCompletion(now);
                    }

                    break;
                }

                case PayrollSourceModules.RecurringDeduction:
                {
                    var lines = IncludedLines(run, target);
                    if (lines.Count == 0)
                    {
                        break;
                    }

                    await employeeRepository.AcquireRecurringDeductionMutationLockAsync(target.RecordPublicId, cancellationToken);
                    var deduction = await employeeRepository.GetTrackedRecurringDeductionWithInstallmentsAsync(target.RecordPublicId, run.TenantId, cancellationToken);
                    if (deduction is null || deduction.StatusCode != RecurringDeductionStatuses.Vigente)
                    {
                        return PayrollRunErrors.PoolConflict;
                    }

                    var plan = RecurringDeductionInstallmentSupport.ToPlan(deduction);
                    var exceptionMonths = deduction.ExceptionMonths.ToHashSet();
                    foreach (var line in lines)
                    {
                        var number = (int)(line.Units ?? 0m);
                        var applied = RecurringDeductionInstallmentSupport.ActiveAppliedNumbers(deduction);
                        var (amount, capital, interest) = RecurringDeductionRules.ChargeSplitFor(number, plan, deduction.ApplicationFrequencyCode);
                        if (!RecurringDeductionRules.CanApplyCharge(
                                deduction.StatusCode, deduction.EffectiveDate, today, number, plan,
                                deduction.ApplicationFrequencyCode, applied).IsValid ||
                            amount != line.CalculatedAmount)
                        {
                            return PayrollRunErrors.PoolConflict;
                        }

                        var installment = deduction.ApplyInstallment(
                            number,
                            today,
                            RecurringDeductionRules.ChargeDueDateFor(deduction.ApplicationFrequencyCode, deduction.InstallmentStartDate, exceptionMonths, number),
                            amount,
                            capital,
                            interest,
                            deduction.CurrencyCode,
                            deduction.PayrollTypeCode,
                            period.Id,
                            period.Label,
                            RecurringDeductionInstallmentOrigins.Motor,
                            appliedByUserId,
                            notes: null);
                        line.BindApplicationReference(installment.PublicId);
                    }

                    if (deduction.IsPlanComplete())
                    {
                        deduction.FinalizeByPlanCompletion(now);
                    }

                    break;
                }

                case PayrollSourceModules.OneTimeIncome:
                {
                    var line = IncludedLines(run, target).SingleOrDefault();
                    if (line is null)
                    {
                        break;
                    }

                    await employeeRepository.AcquireOneTimeIncomeMutationLockAsync(target.RecordPublicId, cancellationToken);
                    var income = await employeeRepository.GetTrackedOneTimeIncomeWithApplicationsAsync(target.RecordPublicId, run.TenantId, cancellationToken);
                    if (income is null || income.StatusCode != OneTimeIncomeStatuses.Autorizado || income.HasActiveApplication ||
                        income.Amount != line.CalculatedAmount)
                    {
                        return PayrollRunErrors.PoolConflict;
                    }

                    var application = income.Apply(
                        today,
                        income.PayrollTypeCode,
                        period.Id,
                        period.PublicId,
                        period.Label,
                        OneTimeIncomeApplicationOrigins.Motor,
                        appliedByUserId,
                        settlementPublicId: null,
                        notes: null);
                    line.BindApplicationReference(application.PublicId);
                    break;
                }

                case PayrollSourceModules.OneTimeDeduction:
                {
                    var line = IncludedLines(run, target).SingleOrDefault();
                    if (line is null)
                    {
                        break;
                    }

                    await employeeRepository.AcquireOneTimeDeductionMutationLockAsync(target.RecordPublicId, cancellationToken);
                    var deduction = await employeeRepository.GetTrackedOneTimeDeductionWithApplicationsAsync(target.RecordPublicId, run.TenantId, cancellationToken);
                    if (deduction is null || deduction.StatusCode != OneTimeDeductionStatuses.Autorizado || deduction.HasActiveApplication ||
                        deduction.Amount != line.CalculatedAmount)
                    {
                        return PayrollRunErrors.PoolConflict;
                    }

                    var application = deduction.Apply(
                        today,
                        deduction.PayrollTypeCode,
                        period.Id,
                        period.PublicId,
                        period.Label,
                        OneTimeDeductionApplicationOrigins.Motor,
                        appliedByUserId,
                        settlementPublicId: null,
                        notes: null);
                    line.BindApplicationReference(application.PublicId);
                    break;
                }

                case PayrollSourceModules.Overtime:
                {
                    // The engine aggregates overtime per plaza in ONE line; the application is per RECORD, so
                    // the record's own reference stays on the target (the line keeps the aggregate).
                    var aggregateIncluded = run.Lines.Any(line =>
                        line is { SourceModule: PayrollSourceModules.Overtime, IsIncluded: true });
                    if (!aggregateIncluded)
                    {
                        break;
                    }

                    await employeeRepository.AcquireOvertimeRecordMutationLockAsync(target.RecordPublicId, cancellationToken);
                    var record = await employeeRepository.GetTrackedOvertimeRecordWithApplicationsAsync(target.RecordPublicId, run.TenantId, cancellationToken);
                    if (record is null || record.StatusCode != OvertimeRecordStatuses.Autorizada || record.HasActiveApplication)
                    {
                        return PayrollRunErrors.PoolConflict;
                    }

                    _ = record.Apply(
                        today,
                        today,
                        record.PayrollTypeCode,
                        period.Id,
                        period.PublicId,
                        period.Label,
                        OvertimeApplicationOrigins.Motor,
                        appliedByUserId,
                        settlementPublicId: null,
                        notes: null);
                    break;
                }
            }
        }

        return null;
    }

    private static List<PayrollRunLine> IncludedLines(PayrollRun run, PayrollRunAssembler.PoolApplyTarget target) =>
        run.Lines
            .Where(line => line.SourceModule == target.SourceModule &&
                           line.SourceReferencePublicId == target.RecordPublicId &&
                           line.IsIncluded)
            .ToList();

    public static PayrollRunResponse ToResponse(
        PayrollRun run,
        Guid definitionPublicId,
        Guid periodPublicId,
        IReadOnlyList<PayrollRunWarningResult> warnings) =>
        new(
            run.PublicId,
            definitionPublicId,
            periodPublicId,
            run.PayrollDefinitionCode,
            run.PayrollDefinitionName,
            run.PayrollTypeCode,
            run.PeriodLabel,
            run.PeriodStartDate,
            run.PeriodEndDate,
            run.PaymentDate,
            run.CurrencyCode,
            run.StatusCode,
            run.GeneratedByUserId,
            run.GeneratedUtc,
            run.RegeneratedCount,
            run.EmployeeCount,
            run.TotalIncome,
            run.TotalDeductions,
            run.TotalEmployerCost,
            run.TotalNet,
            warnings.Select(warning => new PayrollRunWarningResponse(warning.Code, warning.PersonnelFilePublicId, warning.Context)).ToArray(),
            run.ConcurrencyToken,
            run.CreatedUtc,
            run.ModifiedUtc);
}

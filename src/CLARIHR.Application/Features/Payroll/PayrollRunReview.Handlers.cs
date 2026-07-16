using System.Text.Json;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Leave;
using CLARIHR.Application.Abstractions.Payroll;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.Payroll;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.Payroll;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// REQ-012 §3.6 — review + decision + closure (PR-6, closes Ola B). GENERADA is the only editable
// state; AUTHORIZE freezes (double anti-self: dedicated grant WITHOUT Admin + the generator never
// authorizes their own run); RETURN with a reason is the ONLY pre-closure reopening (REQ-013 P-02);
// CLOSE is terminal and closes the PERIOD in the same transaction; ANNUL (pre-closure) reverts every
// MOTOR pool application symmetrically (§3.5 — the pools end as if the run never existed) and releases
// the one-active-run slot. Excluding a pool line reverts ITS application and frees the source record
// (REQ-014 RF-007); excluding a registro line frees it by the derived-consumption probe (IsIncluded).
// ─────────────────────────────────────────────────────────────────────────────────────────────

internal sealed class GetPayrollRunByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPayrollRunRepository runRepository)
    : IQueryHandler<GetPayrollRunByIdQuery, PayrollRunResponse>
{
    public async Task<Result<PayrollRunResponse>> Handle(
        GetPayrollRunByIdQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewPayrollRunsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PayrollRunResponse>.Failure(authorizationResult.Error);
        }

        var loaded = await PayrollRunReviewSupport.LoadAsync(
            runRepository, query.CompanyId, query.PayrollRunId, RbacPermissionAction.Read, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<PayrollRunResponse>.Failure(loaded.Error);
        }

        return Result<PayrollRunResponse>.Success(
            await PayrollRunReviewSupport.ToResponseAsync(runRepository, loaded.Value, cancellationToken));
    }
}

internal sealed class GetPayrollRunEmployeeLinesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPayrollRunRepository runRepository)
    : IQueryHandler<GetPayrollRunEmployeeLinesQuery, PayrollRunEmployeeLinesResponse>
{
    public async Task<Result<PayrollRunEmployeeLinesResponse>> Handle(
        GetPayrollRunEmployeeLinesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewPayrollRunsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PayrollRunEmployeeLinesResponse>.Failure(authorizationResult.Error);
        }

        var loaded = await PayrollRunReviewSupport.LoadAsync(
            runRepository, query.CompanyId, query.PayrollRunId, RbacPermissionAction.Read, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<PayrollRunEmployeeLinesResponse>.Failure(loaded.Error);
        }

        var run = loaded.Value;
        var lines = run.Lines
            .Where(line => line.EmployeePublicId == query.PersonnelFilePublicId)
            .OrderBy(line => line.SortOrder)
            .ThenBy(line => line.PublicId)
            .ToArray();
        if (lines.Length == 0)
        {
            return Result<PayrollRunEmployeeLinesResponse>.Failure(PayrollRunReviewErrors.LineNotFound);
        }

        var income = lines.Where(line => line is { LineClass: PayrollLineClasses.Ingreso, IsIncluded: true }).Sum(line => line.FinalAmount);
        var deductions = lines.Where(line => line is { LineClass: PayrollLineClasses.Descuento, IsIncluded: true }).Sum(line => line.FinalAmount);

        return Result<PayrollRunEmployeeLinesResponse>.Success(new PayrollRunEmployeeLinesResponse(
            run.PublicId,
            query.PersonnelFilePublicId,
            lines[0].EmployeeName,
            income,
            deductions,
            income - deductions,
            lines.Select(PayrollRunReviewSupport.ToLineResponse).ToArray()));
    }
}

internal sealed class AdjustPayrollRunLineCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPayrollRunRepository runRepository,
    IPayrollPeriodRepository periodRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AdjustPayrollRunLineCommand, PayrollRunResponse>
{
    public async Task<Result<PayrollRunResponse>> Handle(
        AdjustPayrollRunLineCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManagePayrollRunsAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PayrollRunResponse>.Failure(authorizationResult.Error);
        }

        var loaded = await PayrollRunReviewSupport.LoadEditableAsync(
            runRepository, command.CompanyId, command.PayrollRunId, command.ConcurrencyToken, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<PayrollRunResponse>.Failure(loaded.Error);
        }

        var run = loaded.Value;
        var line = run.Lines.SingleOrDefault(item => item.PublicId == command.LineId);
        if (line is null)
        {
            return Result<PayrollRunResponse>.Failure(PayrollRunReviewErrors.LineNotFound);
        }

        // The law is never edited (§3.6): ISSS/AFP/Renta and the employer charges admit no adjustment.
        if (line.SourceModule is PayrollSourceModules.LeyIsss or PayrollSourceModules.LeyAfp or PayrollSourceModules.LeyRenta
            or PayrollSourceModules.PatronalIsss or PayrollSourceModules.PatronalAfp or PayrollSourceModules.PatronalIncaf)
        {
            return Result<PayrollRunResponse>.Failure(PayrollRunReviewErrors.LineNotAdjustable);
        }

        var togglesInclusion = command.IsIncluded is { } requested && requested != line.IsIncluded;

        // The overtime line is the per-plaza AGGREGATE of several records — one toggle cannot map to one
        // application. Overtime leaves the run by annulling the record in its module + recalculating.
        if (togglesInclusion && line.SourceModule == PayrollSourceModules.Overtime)
        {
            return Result<PayrollRunResponse>.Failure(PayrollRunReviewErrors.LineNotAdjustable);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var actingUserId);
        var now = dateTimeProvider.UtcNow;
        var today = DateOnly.FromDateTime(now);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await runRepository.AcquirePayrollRunMutationLockAsync(run.PayrollDefinitionId, run.PayrollPeriodId, cancellationToken);

            if (command.OverrideSupplied)
            {
                line.SetOverride(command.OverrideAmount, command.OverrideNote, actingUserId);
            }

            if (togglesInclusion)
            {
                var include = command.IsIncluded!.Value;
                var isPool = PayrollRunReviewSupport.IsPoolModule(line.SourceModule);

                if (!include && isPool && line.SourceReferencePublicId is { } childId)
                {
                    // Excluding an APPLIED pool line reverts its MOTOR child: the source record is released
                    // and becomes a candidate again (REQ-014 RF-007). The line re-binds to the PARENT.
                    var revertError = await PayrollRunReviewSupport.RevertLineApplicationAsync(
                        runRepository, employeeRepository, run, line, childId,
                        $"EXCLUSION PLANILLA {run.PublicId}", actingUserId, now, cancellationToken);
                    if (revertError is not null)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<PayrollRunResponse>.Failure(revertError);
                    }
                }

                line.SetIncluded(include, actingUserId);

                if (include && isPool && line.SourceReferencePublicId is { } parentId)
                {
                    // Re-including re-applies the record with origin MOTOR through the SAME §3.5 flow.
                    var period = await periodRepository.GetByInternalIdAsync(run.PayrollPeriodId, cancellationToken);
                    if (period is null)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<PayrollRunResponse>.Failure(PayrollRunErrors.InputInvalid);
                    }

                    IReadOnlyList<int> numbers =
                        line.SourceModule is PayrollSourceModules.RecurringIncome or PayrollSourceModules.RecurringDeduction
                            ? [(int)(line.Units ?? 0m)]
                            : [];
                    var applyError = await PayrollRunGenerationSupport.ApplyPoolsAsync(
                        employeeRepository,
                        run,
                        [new PayrollRunAssembler.PoolApplyTarget(line.SourceModule!, parentId, numbers)],
                        period,
                        today,
                        actingUserId,
                        now,
                        cancellationToken);
                    if (applyError is not null)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return Result<PayrollRunResponse>.Failure(applyError);
                    }
                }
            }

            PayrollRunReviewSupport.RecomputeTotals(run);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await PayrollRunReviewSupport.ToResponseAsync(runRepository, run, cancellationToken);
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PayrollRunLineAdjusted,
                    AuditEntityTypes.PayrollRun,
                    run.PublicId,
                    $"{run.PayrollDefinitionCode} {run.PeriodLabel}",
                    AuditActions.Update,
                    $"Adjusted line {line.ConceptCode} of {line.EmployeeName} on payroll run {run.PayrollDefinitionCode} — {run.PeriodLabel}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PayrollRunResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class RecalculatePayrollRunCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPayrollDefinitionRepository definitionRepository,
    IPayrollPeriodRepository periodRepository,
    IPayrollRunRepository runRepository,
    IPayrollCalculationDataProvider dataProvider,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RecalculatePayrollRunCommand, PayrollRunResponse>
{
    public async Task<Result<PayrollRunResponse>> Handle(
        RecalculatePayrollRunCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManagePayrollRunsAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PayrollRunResponse>.Failure(authorizationResult.Error);
        }

        var loaded = await PayrollRunReviewSupport.LoadEditableAsync(
            runRepository, command.CompanyId, command.PayrollRunId, command.ConcurrencyToken, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<PayrollRunResponse>.Failure(loaded.Error);
        }

        var run = loaded.Value;
        var resolved = await PayrollRunReviewSupport.ResolveReferencesAsync(
            runRepository, definitionRepository, periodRepository, command.CompanyId, run, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result<PayrollRunResponse>.Failure(resolved.Error);
        }

        var (definition, period) = resolved.Value;
        var targets = command.EmployeeIds.ToHashSet();
        _ = Guid.TryParse(currentUserService.UserId, out var actingUserId);
        var now = dateTimeProvider.UtcNow;
        var today = DateOnly.FromDateTime(now);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await runRepository.AcquirePayrollRunMutationLockAsync(run.PayrollDefinitionId, run.PayrollPeriodId, cancellationToken);

            // Revert ONLY the target employees' MOTOR applications, then flush so the provider's scans see
            // the released records again (the re-derivation below re-applies them).
            var childToParent = await PayrollRunReviewSupport.RevertMotorApplicationsAsync(
                runRepository, employeeRepository, run, targets,
                $"RECALCULO PLANILLA {run.PublicId}", actingUserId, now, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            // Preserve the audited overrides of the recalculated employees, matched by concept + source
            // (pool refs normalized child → parent: the fresh lines reference the PARENT record).
            var preservedOverrides = new Dictionary<(Guid Employee, string Concept, Guid? Reference, Guid? Plaza, decimal? Units), (decimal Amount, string? Note)>();
            foreach (var line in run.Lines)
            {
                if (!targets.Contains(line.EmployeePublicId) || !line.OverrideAmount.HasValue)
                {
                    continue;
                }

                var reference = line.SourceReferencePublicId is { } child && childToParent.TryGetValue(child, out var parent)
                    ? parent
                    : line.SourceReferencePublicId;
                _ = preservedOverrides.TryAdd(
                    (line.EmployeePublicId, line.ConceptCode, reference, line.AssignedPositionPublicId, line.Units),
                    (line.OverrideAmount.Value, line.OverrideNote));
            }

            var source = await dataProvider.BuildAsync(command.CompanyId, definition, period, targets, today, cancellationToken);
            var assembled = PayrollRunAssembler.Assemble(definition, period, source);
            var calculation = PayrollCalculationRules.Calculate(assembled.EngineInput);

            var refreshed = PayrollRunReviewSupport.BuildLines(command.CompanyId, definition, calculation, assembled);
            foreach (var line in refreshed)
            {
                if (preservedOverrides.TryGetValue(
                        (line.EmployeePublicId, line.ConceptCode, line.SourceReferencePublicId, line.AssignedPositionPublicId, line.Units),
                        out var preserved))
                {
                    line.SetOverride(preserved.Amount, preserved.Note, actingUserId);
                }
            }

            // Warnings: keep the untouched employees' entries; the targets' (and the global, employee-less
            // ones) come from the fresh calculation.
            var warnings = PayrollRunReviewSupport.DeserializeWarnings(run.WarningsJson)
                .Where(warning => warning.PersonnelFilePublicId is { } file && !targets.Contains(file))
                .Concat(calculation.Warnings.Select(warning =>
                    new PayrollRunWarningResponse(warning.Code, warning.PersonnelFilePublicId, warning.Context)))
                .ToArray();

            var merged = run.Lines
                .Where(line => !targets.Contains(line.EmployeePublicId))
                .Concat(refreshed)
                .ToArray();
            run.ReplaceLines(
                merged,
                merged.Select(line => line.EmployeePublicId).Distinct().Count(),
                totalIncome: 0m,
                totalDeductions: 0m,
                totalEmployerCost: 0m,
                totalNet: 0m,
                warnings.Length == 0 ? null : JsonSerializer.Serialize(warnings));
            PayrollRunReviewSupport.RecomputeTotals(run);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var applyError = await PayrollRunGenerationSupport.ApplyPoolsAsync(
                employeeRepository, run, assembled.PoolTargets, period, today, actingUserId, now, cancellationToken);
            if (applyError is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<PayrollRunResponse>.Failure(applyError);
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await PayrollRunReviewSupport.ToResponseAsync(runRepository, run, cancellationToken);
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PayrollRunRecalculated,
                    AuditEntityTypes.PayrollRun,
                    run.PublicId,
                    $"{run.PayrollDefinitionCode} {run.PeriodLabel}",
                    AuditActions.Update,
                    $"Recalculated {targets.Count} employee(s) of payroll run {run.PayrollDefinitionCode} — {run.PeriodLabel}.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PayrollRunResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class RegeneratePayrollRunCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPayrollDefinitionRepository definitionRepository,
    IPayrollPeriodRepository periodRepository,
    IPayrollRunRepository runRepository,
    IPayrollCalculationDataProvider dataProvider,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<RegeneratePayrollRunCommand, PayrollRunResponse>
{
    public async Task<Result<PayrollRunResponse>> Handle(
        RegeneratePayrollRunCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManagePayrollRunsAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PayrollRunResponse>.Failure(authorizationResult.Error);
        }

        var loaded = await PayrollRunReviewSupport.LoadEditableAsync(
            runRepository, command.CompanyId, command.PayrollRunId, command.ConcurrencyToken, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<PayrollRunResponse>.Failure(loaded.Error);
        }

        var run = loaded.Value;
        var resolved = await PayrollRunReviewSupport.ResolveReferencesAsync(
            runRepository, definitionRepository, periodRepository, command.CompanyId, run, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result<PayrollRunResponse>.Failure(resolved.Error);
        }

        var (definition, period) = resolved.Value;
        _ = Guid.TryParse(currentUserService.UserId, out var actingUserId);
        var now = dateTimeProvider.UtcNow;
        var today = DateOnly.FromDateTime(now);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await runRepository.AcquirePayrollRunMutationLockAsync(run.PayrollDefinitionId, run.PayrollPeriodId, cancellationToken);

            // Symmetric full reversal (§3.5): every MOTOR child of the period is annulled, then the run is
            // rebuilt from scratch — adjustments (overrides/exclusions) are deliberately discarded.
            _ = await PayrollRunReviewSupport.RevertMotorApplicationsAsync(
                runRepository, employeeRepository, run, personnelFilePublicIds: null,
                $"REGENERACION PLANILLA {run.PublicId}", actingUserId, now, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var source = await dataProvider.BuildAsync(command.CompanyId, definition, period, employeeIds: null, today, cancellationToken);
            var assembled = PayrollRunAssembler.Assemble(definition, period, source);
            var calculation = PayrollCalculationRules.Calculate(assembled.EngineInput);

            var lines = PayrollRunReviewSupport.BuildLines(command.CompanyId, definition, calculation, assembled);
            run.ReplaceLines(
                lines,
                calculation.Totals.EmployeeCount,
                calculation.Totals.TotalIncome,
                calculation.Totals.TotalDeductions,
                calculation.Totals.TotalEmployerCost,
                calculation.Totals.TotalNet,
                PayrollRunReviewSupport.SerializeWarnings(calculation.Warnings));
            run.MarkRegenerated();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var applyError = await PayrollRunGenerationSupport.ApplyPoolsAsync(
                employeeRepository, run, assembled.PoolTargets, period, today, actingUserId, now, cancellationToken);
            if (applyError is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<PayrollRunResponse>.Failure(applyError);
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await PayrollRunReviewSupport.ToResponseAsync(runRepository, run, cancellationToken);
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PayrollRunRegenerated,
                    AuditEntityTypes.PayrollRun,
                    run.PublicId,
                    $"{run.PayrollDefinitionCode} {run.PeriodLabel}",
                    AuditActions.Update,
                    $"Regenerated payroll run {run.PayrollDefinitionCode} — {run.PeriodLabel} (regeneration #{run.RegeneratedCount}).",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PayrollRunResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class AuthorizePayrollRunCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPayrollRunRepository runRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AuthorizePayrollRunCommand, PayrollRunResponse>
{
    public async Task<Result<PayrollRunResponse>> Handle(
        AuthorizePayrollRunCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanAuthorizePayrollRunsAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PayrollRunResponse>.Failure(authorizationResult.Error);
        }

        var loaded = await PayrollRunReviewSupport.LoadWithTokenAsync(
            runRepository, command.CompanyId, command.PayrollRunId, command.ConcurrencyToken, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<PayrollRunResponse>.Failure(loaded.Error);
        }

        var run = loaded.Value;
        if (!PayrollRunStatuses.Authorizable.Contains(run.StatusCode))
        {
            return Result<PayrollRunResponse>.Failure(PayrollRunErrors.StateRuleViolation);
        }

        // Double anti-self (§3.6): the generator — the run's last formulator — never authorizes it.
        _ = Guid.TryParse(currentUserService.UserId, out var actingUserId);
        if (actingUserId == run.GeneratedByUserId)
        {
            return Result<PayrollRunResponse>.Failure(PayrollRunReviewErrors.SelfAuthorizationForbidden);
        }

        run.Authorize(actingUserId, dateTimeProvider.UtcNow);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = await PayrollRunReviewSupport.ToResponseAsync(runRepository, run, cancellationToken);
        await auditService.LogAsync(
            new AuditLogEntry(
                AuditEventTypes.PayrollRunAuthorized,
                AuditEntityTypes.PayrollRun,
                run.PublicId,
                $"{run.PayrollDefinitionCode} {run.PeriodLabel}",
                AuditActions.Update,
                $"Authorized payroll run {run.PayrollDefinitionCode} — {run.PeriodLabel}.",
                After: response),
            cancellationToken);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<PayrollRunResponse>.Success(response);
    }
}

internal sealed class ReturnPayrollRunCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPayrollRunRepository runRepository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ReturnPayrollRunCommand, PayrollRunResponse>
{
    public async Task<Result<PayrollRunResponse>> Handle(
        ReturnPayrollRunCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanAuthorizePayrollRunsAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PayrollRunResponse>.Failure(authorizationResult.Error);
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return Result<PayrollRunResponse>.Failure(PayrollRunReviewErrors.ReturnReasonRequired);
        }

        var loaded = await PayrollRunReviewSupport.LoadWithTokenAsync(
            runRepository, command.CompanyId, command.PayrollRunId, command.ConcurrencyToken, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<PayrollRunResponse>.Failure(loaded.Error);
        }

        var run = loaded.Value;
        if (!PayrollRunStatuses.Returnable.Contains(run.StatusCode))
        {
            return Result<PayrollRunResponse>.Failure(PayrollRunErrors.StateRuleViolation);
        }

        run.Return(command.Reason);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = await PayrollRunReviewSupport.ToResponseAsync(runRepository, run, cancellationToken);
        await auditService.LogAsync(
            new AuditLogEntry(
                AuditEventTypes.PayrollRunReturned,
                AuditEntityTypes.PayrollRun,
                run.PublicId,
                $"{run.PayrollDefinitionCode} {run.PeriodLabel}",
                AuditActions.Update,
                $"Returned payroll run {run.PayrollDefinitionCode} — {run.PeriodLabel} to GENERADA: {command.Reason.Trim()}",
                After: response),
            cancellationToken);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<PayrollRunResponse>.Success(response);
    }
}

internal sealed class ClosePayrollRunCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPayrollRunRepository runRepository,
    IPayrollPeriodRepository periodRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ClosePayrollRunCommand, PayrollRunResponse>
{
    public async Task<Result<PayrollRunResponse>> Handle(
        ClosePayrollRunCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManagePayrollRunsAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PayrollRunResponse>.Failure(authorizationResult.Error);
        }

        var loaded = await PayrollRunReviewSupport.LoadWithTokenAsync(
            runRepository, command.CompanyId, command.PayrollRunId, command.ConcurrencyToken, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<PayrollRunResponse>.Failure(loaded.Error);
        }

        var run = loaded.Value;
        if (!PayrollRunStatuses.Closable.Contains(run.StatusCode))
        {
            return Result<PayrollRunResponse>.Failure(PayrollRunErrors.StateRuleViolation);
        }

        var period = await periodRepository.GetByInternalIdAsync(run.PayrollPeriodId, cancellationToken);
        if (period is null || period.StatusCode != PayrollPeriodStatuses.Generado)
        {
            return Result<PayrollRunResponse>.Failure(PayrollRunErrors.InputInvalid);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var actingUserId);

        // The run closes AND its period closes in the SAME SaveChanges transaction (§3.6 — REQ-013 P-01).
        run.Close(actingUserId, dateTimeProvider.UtcNow);
        period.Close();
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = await PayrollRunReviewSupport.ToResponseAsync(runRepository, run, cancellationToken);
        await auditService.LogAsync(
            new AuditLogEntry(
                AuditEventTypes.PayrollRunClosed,
                AuditEntityTypes.PayrollRun,
                run.PublicId,
                $"{run.PayrollDefinitionCode} {run.PeriodLabel}",
                AuditActions.Update,
                $"Closed payroll run {run.PayrollDefinitionCode} — {run.PeriodLabel}; the period closed with it.",
                After: response),
            cancellationToken);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<PayrollRunResponse>.Success(response);
    }
}

internal sealed class AnnulPayrollRunCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPayrollRunRepository runRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AnnulPayrollRunCommand, PayrollRunResponse>
{
    public async Task<Result<PayrollRunResponse>> Handle(
        AnnulPayrollRunCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManagePayrollRunsAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PayrollRunResponse>.Failure(authorizationResult.Error);
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return Result<PayrollRunResponse>.Failure(PayrollRunReviewErrors.AnnulmentReasonRequired);
        }

        var loaded = await PayrollRunReviewSupport.LoadWithTokenAsync(
            runRepository, command.CompanyId, command.PayrollRunId, command.ConcurrencyToken, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<PayrollRunResponse>.Failure(loaded.Error);
        }

        var run = loaded.Value;
        if (PayrollRunStatuses.Terminal.Contains(run.StatusCode))
        {
            return Result<PayrollRunResponse>.Failure(PayrollRunErrors.StateRuleViolation);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var actingUserId);
        var now = dateTimeProvider.UtcNow;

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await runRepository.AcquirePayrollRunMutationLockAsync(run.PayrollDefinitionId, run.PayrollPeriodId, cancellationToken);

            // Symmetric reversal (§3.5, decision №1): every MOTOR child the run applied is annulled — the
            // pools end as if the run never existed and the one-active-run slot is released.
            _ = await PayrollRunReviewSupport.RevertMotorApplicationsAsync(
                runRepository, employeeRepository, run, personnelFilePublicIds: null,
                $"ANULACION PLANILLA {run.PublicId}", actingUserId, now, cancellationToken);

            run.Annul(actingUserId, command.Reason, now);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await PayrollRunReviewSupport.ToResponseAsync(runRepository, run, cancellationToken);
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.PayrollRunAnnulled,
                    AuditEntityTypes.PayrollRun,
                    run.PublicId,
                    $"{run.PayrollDefinitionCode} {run.PeriodLabel}",
                    AuditActions.Update,
                    $"Annulled payroll run {run.PayrollDefinitionCode} — {run.PeriodLabel}: {command.Reason.Trim()}",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<PayrollRunResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal static class PayrollRunReviewSupport
{
    public static bool IsPoolModule(string? sourceModule) =>
        sourceModule is PayrollSourceModules.RecurringIncome or PayrollSourceModules.RecurringDeduction
            or PayrollSourceModules.OneTimeIncome or PayrollSourceModules.OneTimeDeduction
            or PayrollSourceModules.Overtime;

    public static async Task<Result<PayrollRun>> LoadAsync(
        IPayrollRunRepository runRepository,
        Guid companyId,
        Guid payrollRunId,
        RbacPermissionAction action,
        CancellationToken cancellationToken)
    {
        var run = await runRepository.GetByIdAsync(payrollRunId, cancellationToken);
        if (run is null || run.TenantId != companyId)
        {
            return Result<PayrollRun>.Failure(
                await runRepository.ExistsOutsideTenantAsync(payrollRunId, cancellationToken)
                    ? PayrollRunErrors.TenantMismatch(action)
                    : PayrollRunErrors.PayrollRunNotFound);
        }

        return Result<PayrollRun>.Success(run);
    }

    public static async Task<Result<PayrollRun>> LoadWithTokenAsync(
        IPayrollRunRepository runRepository,
        Guid companyId,
        Guid payrollRunId,
        Guid concurrencyToken,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadAsync(runRepository, companyId, payrollRunId, RbacPermissionAction.Update, cancellationToken);
        if (loaded.IsFailure)
        {
            return loaded;
        }

        return loaded.Value.ConcurrencyToken == concurrencyToken
            ? loaded
            : Result<PayrollRun>.Failure(PayrollRunErrors.ConcurrencyConflict);
    }

    public static async Task<Result<PayrollRun>> LoadEditableAsync(
        IPayrollRunRepository runRepository,
        Guid companyId,
        Guid payrollRunId,
        Guid concurrencyToken,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadWithTokenAsync(runRepository, companyId, payrollRunId, concurrencyToken, cancellationToken);
        if (loaded.IsFailure)
        {
            return loaded;
        }

        return loaded.Value.IsEditable
            ? loaded
            : Result<PayrollRun>.Failure(PayrollRunErrors.StateRuleViolation);
    }

    /// <summary>The run's definition/period re-validated for a re-generation (active + GENERADO + coherent).</summary>
    public static async Task<Result<(PayrollDefinition Definition, Domain.Leave.PayrollPeriodDefinition Period)>> ResolveReferencesAsync(
        IPayrollRunRepository runRepository,
        IPayrollDefinitionRepository definitionRepository,
        IPayrollPeriodRepository periodRepository,
        Guid companyId,
        PayrollRun run,
        CancellationToken cancellationToken)
    {
        var references = await runRepository.GetReferencePublicIdsAsync(run.PayrollDefinitionId, run.PayrollPeriodId, cancellationToken);
        if (references is null)
        {
            return Result<(PayrollDefinition, Domain.Leave.PayrollPeriodDefinition)>.Failure(PayrollRunErrors.InputInvalid);
        }

        return await PayrollRunGenerationSupport.ResolveAsync(
            definitionRepository, periodRepository, companyId,
            references.Value.DefinitionPublicId, references.Value.PeriodPublicId, cancellationToken);
    }

    /// <summary>
    /// Reverts ONE applied pool line (exclusion): annuls the MOTOR child under the module's own lock and
    /// re-binds the line to its PARENT record — re-applicable, re-selectable, re-carryable (REQ-014).
    /// </summary>
    public static async Task<Error?> RevertLineApplicationAsync(
        IPayrollRunRepository runRepository,
        IPersonnelFileEmployeeRepository employeeRepository,
        PayrollRun run,
        PayrollRunLine line,
        Guid childPublicId,
        string reason,
        Guid byUserId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var module = line.SourceModule!;
        var parentId = await runRepository.GetPoolParentByChildAsync(run.TenantId, module, childPublicId, cancellationToken);
        if (parentId is null)
        {
            // The reference still points at the source record: a deferred/never-applied line — nothing to revert.
            return null;
        }

        switch (module)
        {
            case PayrollSourceModules.RecurringIncome:
            {
                await employeeRepository.AcquireRecurringIncomeMutationLockAsync(parentId.Value, cancellationToken);
                var income = await employeeRepository.GetTrackedRecurringIncomeWithInstallmentsAsync(parentId.Value, run.TenantId, cancellationToken);
                if (income is null)
                {
                    return PayrollRunErrors.PoolConflict;
                }

                if (income.Installments.Any(child => child.PublicId == childPublicId &&
                                                     child.StatusCode == RecurringIncomeInstallmentStatuses.Aplicada))
                {
                    income.AnnulInstallment(childPublicId, reason, byUserId, now);
                }

                break;
            }

            case PayrollSourceModules.RecurringDeduction:
            {
                await employeeRepository.AcquireRecurringDeductionMutationLockAsync(parentId.Value, cancellationToken);
                var deduction = await employeeRepository.GetTrackedRecurringDeductionWithInstallmentsAsync(parentId.Value, run.TenantId, cancellationToken);
                if (deduction is null)
                {
                    return PayrollRunErrors.PoolConflict;
                }

                if (deduction.Installments.Any(child => child.PublicId == childPublicId &&
                                                        child.StatusCode == RecurringDeductionInstallmentStatuses.Aplicada))
                {
                    deduction.AnnulInstallment(childPublicId, reason, byUserId, now);
                }

                break;
            }

            case PayrollSourceModules.OneTimeIncome:
            {
                await employeeRepository.AcquireOneTimeIncomeMutationLockAsync(parentId.Value, cancellationToken);
                var income = await employeeRepository.GetTrackedOneTimeIncomeWithApplicationsAsync(parentId.Value, run.TenantId, cancellationToken);
                if (income is null)
                {
                    return PayrollRunErrors.PoolConflict;
                }

                if (income.Applications.Any(child => child.PublicId == childPublicId &&
                                                     child.StatusCode == OneTimeIncomeApplicationStatuses.Aplicada))
                {
                    income.AnnulApplication(childPublicId, reason, byUserId, now);
                }

                break;
            }

            case PayrollSourceModules.OneTimeDeduction:
            {
                await employeeRepository.AcquireOneTimeDeductionMutationLockAsync(parentId.Value, cancellationToken);
                var deduction = await employeeRepository.GetTrackedOneTimeDeductionWithApplicationsAsync(parentId.Value, run.TenantId, cancellationToken);
                if (deduction is null)
                {
                    return PayrollRunErrors.PoolConflict;
                }

                if (deduction.Applications.Any(child => child.PublicId == childPublicId &&
                                                        child.StatusCode == OneTimeDeductionApplicationStatuses.Aplicada))
                {
                    deduction.AnnulApplication(childPublicId, reason, byUserId, now);
                }

                break;
            }

            default:
                return PayrollRunReviewErrors.LineNotAdjustable;
        }

        line.BindApplicationReference(parentId.Value);
        return null;
    }

    /// <summary>
    /// Reverts the MOTOR applications of the run's period (regenerate/annul: every employee; selective
    /// recalculation: only <paramref name="personnelFilePublicIds"/>). Deterministic walk — modules in
    /// ordinal order, parents ascending — mirroring the generation's anti-deadlock ordering (§0.18).
    /// Returns the annulled child → parent map (the recalculation normalizes override keys with it).
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, Guid>> RevertMotorApplicationsAsync(
        IPayrollRunRepository runRepository,
        IPersonnelFileEmployeeRepository employeeRepository,
        PayrollRun run,
        IReadOnlyCollection<Guid>? personnelFilePublicIds,
        string reason,
        Guid byUserId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var childToParent = new Dictionary<Guid, Guid>();

        foreach (var module in new[]
                 {
                     PayrollSourceModules.OneTimeDeduction,
                     PayrollSourceModules.OneTimeIncome,
                     PayrollSourceModules.Overtime,
                     PayrollSourceModules.RecurringDeduction,
                     PayrollSourceModules.RecurringIncome,
                 })
        {
            var parents = (await runRepository.GetMotorAppliedParentsForPeriodAsync(
                    run.TenantId, module, run.PayrollPeriodId, personnelFilePublicIds, cancellationToken))
                .OrderBy(id => id)
                .ToArray();

            foreach (var parentId in parents)
            {
                switch (module)
                {
                    case PayrollSourceModules.RecurringIncome:
                    {
                        await employeeRepository.AcquireRecurringIncomeMutationLockAsync(parentId, cancellationToken);
                        var income = await employeeRepository.GetTrackedRecurringIncomeWithInstallmentsAsync(parentId, run.TenantId, cancellationToken);
                        if (income is null)
                        {
                            break;
                        }

                        foreach (var childId in income.Installments
                                     .Where(child => child.OriginCode == RecurringIncomeInstallmentOrigins.Motor &&
                                                     child.PayrollPeriodId == run.PayrollPeriodId &&
                                                     child.StatusCode == RecurringIncomeInstallmentStatuses.Aplicada)
                                     .Select(child => child.PublicId)
                                     .ToArray())
                        {
                            income.AnnulInstallment(childId, reason, byUserId, now);
                            childToParent[childId] = parentId;
                        }

                        break;
                    }

                    case PayrollSourceModules.RecurringDeduction:
                    {
                        await employeeRepository.AcquireRecurringDeductionMutationLockAsync(parentId, cancellationToken);
                        var deduction = await employeeRepository.GetTrackedRecurringDeductionWithInstallmentsAsync(parentId, run.TenantId, cancellationToken);
                        if (deduction is null)
                        {
                            break;
                        }

                        foreach (var childId in deduction.Installments
                                     .Where(child => child.OriginCode == RecurringDeductionInstallmentOrigins.Motor &&
                                                     child.PayrollPeriodId == run.PayrollPeriodId &&
                                                     child.StatusCode == RecurringDeductionInstallmentStatuses.Aplicada)
                                     .Select(child => child.PublicId)
                                     .ToArray())
                        {
                            deduction.AnnulInstallment(childId, reason, byUserId, now);
                            childToParent[childId] = parentId;
                        }

                        break;
                    }

                    case PayrollSourceModules.OneTimeIncome:
                    {
                        await employeeRepository.AcquireOneTimeIncomeMutationLockAsync(parentId, cancellationToken);
                        var income = await employeeRepository.GetTrackedOneTimeIncomeWithApplicationsAsync(parentId, run.TenantId, cancellationToken);
                        if (income is null)
                        {
                            break;
                        }

                        foreach (var childId in income.Applications
                                     .Where(child => child.OriginCode == OneTimeIncomeApplicationOrigins.Motor &&
                                                     child.PayrollPeriodId == run.PayrollPeriodId &&
                                                     child.StatusCode == OneTimeIncomeApplicationStatuses.Aplicada)
                                     .Select(child => child.PublicId)
                                     .ToArray())
                        {
                            income.AnnulApplication(childId, reason, byUserId, now);
                            childToParent[childId] = parentId;
                        }

                        break;
                    }

                    case PayrollSourceModules.OneTimeDeduction:
                    {
                        await employeeRepository.AcquireOneTimeDeductionMutationLockAsync(parentId, cancellationToken);
                        var deduction = await employeeRepository.GetTrackedOneTimeDeductionWithApplicationsAsync(parentId, run.TenantId, cancellationToken);
                        if (deduction is null)
                        {
                            break;
                        }

                        foreach (var childId in deduction.Applications
                                     .Where(child => child.OriginCode == OneTimeDeductionApplicationOrigins.Motor &&
                                                     child.PayrollPeriodId == run.PayrollPeriodId &&
                                                     child.StatusCode == OneTimeDeductionApplicationStatuses.Aplicada)
                                     .Select(child => child.PublicId)
                                     .ToArray())
                        {
                            deduction.AnnulApplication(childId, reason, byUserId, now);
                            childToParent[childId] = parentId;
                        }

                        break;
                    }

                    case PayrollSourceModules.Overtime:
                    {
                        await employeeRepository.AcquireOvertimeRecordMutationLockAsync(parentId, cancellationToken);
                        var record = await employeeRepository.GetTrackedOvertimeRecordWithApplicationsAsync(parentId, run.TenantId, cancellationToken);
                        if (record is null)
                        {
                            break;
                        }

                        foreach (var childId in record.Applications
                                     .Where(child => child.OriginCode == OvertimeApplicationOrigins.Motor &&
                                                     child.PayrollPeriodId == run.PayrollPeriodId &&
                                                     child.StatusCode == OvertimeApplicationStatuses.Aplicada)
                                     .Select(child => child.PublicId)
                                     .ToArray())
                        {
                            record.AnnulApplication(childId, reason, byUserId, now);
                            childToParent[childId] = parentId;
                        }

                        break;
                    }
                }
            }
        }

        return childToParent;
    }

    /// <summary>Materializes engine lines as persisted entities (regeneration/recalculation — mirrors generate).</summary>
    public static List<PayrollRunLine> BuildLines(
        Guid tenantId,
        PayrollDefinition definition,
        PayrollCalculationResult calculation,
        PayrollRunAssembler.AssembledRun assembled)
    {
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
            entity.SetTenantId(tenantId);
            lines.Add(entity);
        }

        return lines;
    }

    public static string? SerializeWarnings(IReadOnlyList<PayrollRunWarningResult> warnings) =>
        warnings.Count == 0
            ? null
            : JsonSerializer.Serialize(warnings.Select(warning => new PayrollRunWarningResponse(
                warning.Code, warning.PersonnelFilePublicId, warning.Context)));

    public static IReadOnlyList<PayrollRunWarningResponse> DeserializeWarnings(string? warningsJson) =>
        warningsJson is null
            ? []
            : JsonSerializer.Deserialize<List<PayrollRunWarningResponse>>(warningsJson) ?? [];

    /// <summary>Persisted totals = Σ FinalAmount (override ?? calculated) of the INCLUDED lines, per class.</summary>
    public static void RecomputeTotals(PayrollRun run)
    {
        var income = Round2(run.Lines.Where(line => line is { LineClass: PayrollLineClasses.Ingreso, IsIncluded: true }).Sum(line => line.FinalAmount));
        var deductions = Round2(run.Lines.Where(line => line is { LineClass: PayrollLineClasses.Descuento, IsIncluded: true }).Sum(line => line.FinalAmount));
        var employer = Round2(run.Lines.Where(line => line is { LineClass: PayrollLineClasses.PagoPatronal, IsIncluded: true }).Sum(line => line.FinalAmount));
        run.RefreshTotals(income, deductions, employer, Round2(income - deductions));
    }

    public static async Task<PayrollRunResponse> ToResponseAsync(
        IPayrollRunRepository runRepository,
        PayrollRun run,
        CancellationToken cancellationToken)
    {
        var references = await runRepository.GetReferencePublicIdsAsync(run.PayrollDefinitionId, run.PayrollPeriodId, cancellationToken)
            ?? throw new InvalidOperationException("The payroll run's definition/period references could not be resolved.");

        return new PayrollRunResponse(
            run.PublicId,
            references.DefinitionPublicId,
            references.PeriodPublicId,
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
            DeserializeWarnings(run.WarningsJson),
            run.ConcurrencyToken,
            run.CreatedUtc,
            run.ModifiedUtc);
    }

    public static PayrollRunLineResponse ToLineResponse(PayrollRunLine line) => new(
        line.PublicId,
        line.EmployeePublicId,
        line.EmployeeName,
        line.EmployeeCode,
        line.AssignedPositionPublicId,
        line.CostCenterName,
        line.ConceptCode,
        line.ConceptName,
        line.LineClass,
        line.Units,
        line.BaseAmount,
        line.CalculatedAmount,
        line.OverrideAmount,
        line.OverrideNote,
        line.FinalAmount,
        line.IsIncluded,
        line.SourceModule,
        line.SourceReferencePublicId,
        line.CurrencyCode,
        line.WarningCodesJson is null ? [] : JsonSerializer.Deserialize<List<string>>(line.WarningCodesJson) ?? [],
        line.SortOrder);

    private static decimal Round2(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

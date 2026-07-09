using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Application.Features.PersonnelFiles.Overtime;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>Maps an overtime-record application entity to its API response (user ids null-safe — a non-Guid principal → null).</summary>
internal static class OvertimeRecordApplicationMapping
{
    public static OvertimeRecordApplicationResponse ToResponse(PersonnelFileOvertimeRecordApplication entity) =>
        new(
            entity.PublicId,
            entity.AppliedDate,
            entity.PayrollTypeCode,
            entity.PayrollPeriodPublicId,
            entity.PayrollPeriodLabel,
            entity.OriginCode,
            entity.StatusCode,
            NullIfEmpty(entity.AppliedByUserId),
            entity.SettlementPublicId,
            entity.AnnulmentReason,
            entity.AnnulledByUserId,
            entity.AnnulledUtc,
            entity.Notes,
            entity.ConcurrencyToken);

    private static Guid? NullIfEmpty(Guid value) => value == Guid.Empty ? null : value;
}

/// <summary>
/// Shared application snapshot logic so the unitary application and the apply-period batch cuadran by
/// construction. An application's payroll destination defaults to the record's OWN declared destination (degraded:
/// public id + label, no FK — mirroring the record header, PR-3) unless a resolved payroll-period override is
/// supplied (FK real, §0.14). The payroll type is always the record's declared type (snapshot; not editable). The
/// domain <see cref="PersonnelFileOvertimeRecord.Apply"/> re-checks the elapsed-work-date guard (№13).
/// </summary>
internal static class OvertimeRecordApplicationSupport
{
    public readonly record struct PeriodSnapshot(long? InternalId, Guid? PublicId, string? Label);

    /// <summary>The snapshot for one record given an optional resolved period override (null → record's own destination).</summary>
    public static PeriodSnapshot SnapshotFor(PersonnelFileOvertimeRecord record, PeriodSnapshot? overridePeriod) =>
        overridePeriod ?? new PeriodSnapshot(null, record.PayrollPeriodPublicId, record.PayrollPeriodLabel);

    /// <summary>Applies the record (RN-06) with the resolved snapshot; the caller commits through the unit of work.</summary>
    public static PersonnelFileOvertimeRecordApplication Apply(
        PersonnelFileOvertimeRecord record,
        DateOnly appliedDate,
        DateOnly today,
        PeriodSnapshot snapshot,
        Guid appliedByUserId,
        string? notes) =>
        record.Apply(
            appliedDate,
            today,
            record.PayrollTypeCode,
            snapshot.InternalId,
            snapshot.PublicId,
            snapshot.Label,
            OvertimeApplicationOrigins.Manual,
            appliedByUserId,
            settlementPublicId: null,
            notes);
}

// ── Reads (View OR self — application history) ─────────────────────────────────────────────────────────

internal sealed class GetOvertimeRecordApplicationsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetOvertimeRecordApplicationsQuery, IReadOnlyCollection<OvertimeRecordApplicationResponse>>
{
    public async Task<Result<IReadOnlyCollection<OvertimeRecordApplicationResponse>>> Handle(
        GetOvertimeRecordApplicationsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForOvertimeReadAsync<IReadOnlyCollection<OvertimeRecordApplicationResponse>>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetOvertimeRecordApplicationsAsync(
            personnelFile!.PublicId, query.OvertimeRecordPublicId, cancellationToken);
        return response is null
            ? Result<IReadOnlyCollection<OvertimeRecordApplicationResponse>>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<IReadOnlyCollection<OvertimeRecordApplicationResponse>>.Success(response);
    }
}

// ── Unitary application (Manage — lock) ─────────────────────────────────────────────────────────────────

internal sealed class ApplyOvertimeRecordApplicationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<ApplyOvertimeRecordApplicationCommand, OvertimeRecordApplicationResult>
{
    public async Task<Result<OvertimeRecordApplicationResult>> Handle(
        ApplyOvertimeRecordApplicationCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageOvertimeRecordsAsync<OvertimeRecordApplicationResult>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<OvertimeRecordApplicationResult>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        if (await employeeRepository.IsOvertimeProfileRetiredAsync(personnelFile.Id, cancellationToken))
        {
            return Result<OvertimeRecordApplicationResult>.Failure(PersonnelFileErrors.ProfileRetiredLocked);
        }

        // Pre-check existence + If-Match (AsNoTracking) so we never take the lock for a bad request.
        var snapshot = await employeeRepository.GetOvertimeRecordAsync(personnelFile.PublicId, command.OvertimeRecordPublicId, cancellationToken);
        if (snapshot is null)
        {
            return Result<OvertimeRecordApplicationResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (snapshot.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OvertimeRecordApplicationResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // Resolve the (optional) payroll-period override to a FK real before opening the transaction (§0.14). When
        // omitted, the application defaults to the record's own declared destination (degraded, mirrors the header).
        OvertimeRecordApplicationSupport.PeriodSnapshot? overridePeriod = null;
        if (command.PayrollPeriodPublicId is { } payrollPeriodPublicId && payrollPeriodPublicId != Guid.Empty)
        {
            var period = await employeeRepository.ResolveOvertimePayrollPeriodAsync(
                personnelFile.TenantId, payrollPeriodPublicId, cancellationToken);
            if (period is null || !period.IsActive)
            {
                return Result<OvertimeRecordApplicationResult>.Failure(OvertimeRecordApplicationErrors.PayrollPeriodInvalid);
            }

            overridePeriod = new OvertimeRecordApplicationSupport.PeriodSnapshot(period.InternalId, payrollPeriodPublicId, period.Label);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var appliedByUserId);
        var now = dateTimeProvider.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var appliedDate = command.AppliedDate ?? today;

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // Serialize the at-most-one-active-application rule: acquire the lock, then load fresh (the first
            // tracking load of this record on the context, so it reflects any committed concurrent application).
            await employeeRepository.AcquireOvertimeRecordMutationLockAsync(command.OvertimeRecordPublicId, cancellationToken);
            var record = await employeeRepository.GetTrackedOvertimeRecordWithApplicationsAsync(
                command.OvertimeRecordPublicId, personnelFile.TenantId, cancellationToken);
            if (record is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<OvertimeRecordApplicationResult>.Failure(PersonnelFileErrors.ItemNotFound);
            }

            if (record.ConcurrencyToken != command.ConcurrencyToken)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<OvertimeRecordApplicationResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
            }

            // AUTORIZADA + elapsed work date (№13: a future organized shift → OVERTIME_WORK_DATE_NOT_ELAPSED) +
            // no active application — the pure rule surfaces the granular 422 code before the domain mutator.
            var canApply = OvertimeRecordRules.CanApply(record.StatusCode, record.HasActiveApplication, record.WorkDate, today);
            if (!canApply.IsValid)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<OvertimeRecordApplicationResult>.Failure(
                    new Error(canApply.ErrorCode!, "The overtime record cannot be applied.", ErrorType.UnprocessableEntity));
            }

            var application = OvertimeRecordApplicationSupport.Apply(
                record,
                appliedDate,
                today,
                OvertimeRecordApplicationSupport.SnapshotFor(record, overridePeriod),
                appliedByUserId,
                command.Notes);

            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var result = new OvertimeRecordApplicationResult(
                OvertimeRecordApplicationMapping.ToResponse(application),
                record.StatusCode,
                record.ConcurrencyToken);

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile,
                $"Applied overtime record for {personnelFile.FullName}.", result, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<OvertimeRecordApplicationResult>.Success(result);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

// ── Unitary annulment / reversal (Manage — lock) ────────────────────────────────────────────────────────

internal sealed class AnnulOvertimeRecordApplicationCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AnnulOvertimeRecordApplicationCommand, OvertimeRecordApplicationResult>
{
    public async Task<Result<OvertimeRecordApplicationResult>> Handle(
        AnnulOvertimeRecordApplicationCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageOvertimeRecordsAsync<OvertimeRecordApplicationResult>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<OvertimeRecordApplicationResult>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return Result<OvertimeRecordApplicationResult>.Failure(OvertimeRecordErrors.AnnulmentReasonRequired);
        }

        var snapshot = await employeeRepository.GetOvertimeRecordAsync(personnelFile.PublicId, command.OvertimeRecordPublicId, cancellationToken);
        if (snapshot is null)
        {
            return Result<OvertimeRecordApplicationResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (snapshot.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OvertimeRecordApplicationResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var byUserId);
        var now = dateTimeProvider.UtcNow;

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await employeeRepository.AcquireOvertimeRecordMutationLockAsync(command.OvertimeRecordPublicId, cancellationToken);
            var record = await employeeRepository.GetTrackedOvertimeRecordWithApplicationsAsync(
                command.OvertimeRecordPublicId, personnelFile.TenantId, cancellationToken);
            if (record is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<OvertimeRecordApplicationResult>.Failure(PersonnelFileErrors.ItemNotFound);
            }

            if (record.ConcurrencyToken != command.ConcurrencyToken)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<OvertimeRecordApplicationResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
            }

            var application = record.Applications.FirstOrDefault(item =>
                item.PublicId == command.ApplicationPublicId
                && item.StatusCode == OvertimeApplicationStatuses.Aplicada);
            if (application is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<OvertimeRecordApplicationResult>.Failure(OvertimeRecordApplicationErrors.ApplicationNotFound);
            }

            // Annuls the active application (is_active=false + ANULADA) and reopens the record APLICADA → AUTORIZADA
            // in the SAME transaction, so it is immediately re-applicable.
            record.AnnulApplication(command.ApplicationPublicId, command.Reason, byUserId, now);

            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var result = new OvertimeRecordApplicationResult(
                OvertimeRecordApplicationMapping.ToResponse(application),
                record.StatusCode,
                record.ConcurrencyToken);

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile,
                $"Annulled overtime record application for {personnelFile.FullName}.", result, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<OvertimeRecordApplicationResult>.Success(result);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

// ── Apply-period batch (company-scoped, Manage per-handler, ordered locks, atomic) ─────────────────────

internal sealed class ApplyOvertimePeriodCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ApplyOvertimePeriodCommand, OvertimeApplyPeriodResult>
{
    public async Task<Result<OvertimeApplyPeriodResult>> Handle(
        ApplyOvertimePeriodCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageOvertimeRecordsAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OvertimeApplyPeriodResult>.Failure(authorizationResult.Error);
        }

        var tenantId = command.CompanyId;
        var payrollTypeCode = command.PayrollTypeCode.Trim().ToUpperInvariant();

        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
                tenantId, PersonnelCurriculumCatalogCategories.PayrollType, payrollTypeCode, cancellationToken))
        {
            return Result<OvertimeApplyPeriodResult>.Failure(OvertimeRecordErrors.PayrollTypeInvalid);
        }

        var now = dateTimeProvider.UtcNow;
        var today = DateOnly.FromDateTime(now);

        // Resolve the (optional) destination override ONCE for the whole batch (FK real when a public id resolves;
        // a bare label is a degraded override; none → each application defaults to its record's own destination).
        OvertimeRecordApplicationSupport.PeriodSnapshot? overridePeriod = null;
        if (command.PayrollPeriodPublicId is { } payrollPeriodPublicId && payrollPeriodPublicId != Guid.Empty)
        {
            var period = await employeeRepository.ResolveOvertimePayrollPeriodAsync(tenantId, payrollPeriodPublicId, cancellationToken);
            if (period is null || !period.IsActive)
            {
                return Result<OvertimeApplyPeriodResult>.Failure(OvertimeRecordApplicationErrors.PayrollPeriodInvalid);
            }

            overridePeriod = new OvertimeRecordApplicationSupport.PeriodSnapshot(period.InternalId, payrollPeriodPublicId, period.Label);
        }
        else if (!string.IsNullOrWhiteSpace(command.PayrollPeriodLabel))
        {
            overridePeriod = new OvertimeRecordApplicationSupport.PeriodSnapshot(null, null, command.PayrollPeriodLabel.Trim());
        }

        var excluded = command.ExcludedRecordPublicIds?.ToHashSet() ?? [];
        // Candidates = AUTORIZADA of the payroll type whose work date has elapsed (future shifts excluded), id-ordered.
        var candidates = await employeeRepository.GetOvertimeApplyPeriodCandidatesAsync(tenantId, payrollTypeCode, today, cancellationToken);

        var targets = new List<OvertimeApplyPeriodCandidate>();
        var pospuestos = 0;
        foreach (var candidate in candidates)
        {
            if (excluded.Contains(candidate.PublicId))
            {
                pospuestos++;
                continue;
            }

            targets.Add(candidate);
        }

        if (targets.Count == 0)
        {
            return Result<OvertimeApplyPeriodResult>.Success(new OvertimeApplyPeriodResult(0, pospuestos));
        }

        _ = Guid.TryParse(currentUserService.UserId, out var appliedByUserId);
        var appliedDate = today;

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var aplicados = 0;

            // Ordered by internal id (targets already come from an id-ordered scan) → anti-deadlock. The batch is
            // atomic: any conflict rolls the whole transaction back with the offending record in the detail.
            foreach (var target in targets)
            {
                await employeeRepository.AcquireOvertimeRecordMutationLockAsync(target.PublicId, cancellationToken);
                var record = await employeeRepository.GetTrackedOvertimeRecordWithApplicationsAsync(target.PublicId, tenantId, cancellationToken);
                if (record is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<OvertimeApplyPeriodResult>.Failure(
                        OvertimeRecordApplicationErrors.ApplyPeriodConflict($"overtime record {target.PublicId} is no longer available"));
                }

                var canApply = OvertimeRecordRules.CanApply(record.StatusCode, record.HasActiveApplication, record.WorkDate, today);
                if (!canApply.IsValid)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return Result<OvertimeApplyPeriodResult>.Failure(
                        OvertimeRecordApplicationErrors.ApplyPeriodConflict($"overtime record {target.PublicId} is no longer an applicable AUTORIZADA"));
                }

                _ = OvertimeRecordApplicationSupport.Apply(
                    record,
                    appliedDate,
                    today,
                    OvertimeRecordApplicationSupport.SnapshotFor(record, overridePeriod),
                    appliedByUserId,
                    notes: null);
                aplicados++;
            }

            var summary = new OvertimeApplyPeriodResult(aplicados, pospuestos);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.OvertimeApplicationsApplied,
                    AuditEntityTypes.PersonnelFile,
                    command.CompanyId,
                    $"OVERTIME_APPLY_PERIOD_{payrollTypeCode}",
                    AuditActions.Update,
                    $"Applied {aplicados} overtime record(s) for payroll type {payrollTypeCode} ({pospuestos} postponed).",
                    After: summary),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<OvertimeApplyPeriodResult>.Success(summary);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

// ── Pending / overdue tray (company-scoped, View per-handler) ──────────────────────────────────────────

internal sealed class QueryOvertimeRecordPendingQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileEmployeeRepository employeeRepository,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<QueryOvertimeRecordPendingQuery, OvertimeRecordPendingResponse>
{
    public async Task<Result<OvertimeRecordPendingResponse>> Handle(
        QueryOvertimeRecordPendingQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewOvertimeRecordsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<OvertimeRecordPendingResponse>.Failure(authorizationResult.Error);
        }

        var payrollTypeCode = string.IsNullOrWhiteSpace(query.PayrollTypeCode)
            ? null
            : query.PayrollTypeCode.Trim().ToUpperInvariant();

        var rows = await employeeRepository.GetOvertimePendingRowsAsync(query.CompanyId, payrollTypeCode, cancellationToken);
        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow);

        var items = rows
            .Select(row => new OvertimeRecordPendingRow(
                row.OvertimeRecordPublicId,
                row.PersonnelFilePublicId,
                row.EmployeeName,
                row.WorkDate,
                row.OvertimeTypeCodeSnapshot,
                row.OvertimeTypeNameSnapshot,
                row.DurationDecimalHours,
                row.FactorApplied,
                row.PayrollTypeCode,
                row.PayrollPeriodPublicId,
                row.PayrollPeriodLabel,
                row.PayrollPeriodEndDate,
                OvertimeRecordRules.IsOverdue(row.PayrollPeriodEndDate, today),
                row.ConcurrencyToken))
            .Where(row => !query.OnlyOverdue || row.IsOverdue)
            .ToArray();

        var overdueCount = items.Count(row => row.IsOverdue);
        return Result<OvertimeRecordPendingResponse>.Success(
            new OvertimeRecordPendingResponse(items, items.Length, overdueCount));
    }
}

using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Application.Features.PersonnelFiles.Overtime;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>Maps an overtime-record aggregate to its API response (user ids null-safe — a non-Guid principal → null).</summary>
public static class OvertimeRecordMapping
{
    public static OvertimeRecordResponse ToResponse(PersonnelFileOvertimeRecord entity) =>
        new(
            entity.PublicId,
            entity.WorkDate,
            entity.OvertimeTypePublicId,
            entity.OvertimeTypeCodeSnapshot,
            entity.OvertimeTypeNameSnapshot,
            entity.TypeFactorSnapshot,
            entity.FactorApplied,
            entity.FactorOverrideNote,
            entity.DurationHours,
            entity.DurationMinutes,
            entity.DurationDecimalHours,
            entity.StartTime,
            entity.EndTime,
            entity.JustificationTypePublicId,
            entity.JustificationCodeSnapshot,
            entity.JustificationNameSnapshot,
            entity.Observations,
            entity.OriginChannel,
            entity.AssignedPositionPublicId,
            entity.RequesterFilePublicId,
            entity.RequesterNameSnapshot,
            entity.PayrollTypeCode,
            entity.PayrollPeriodPublicId,
            entity.PayrollPeriodLabel,
            entity.PayrollPeriodEndDate,
            entity.StatusCode,
            NullIfEmpty(entity.RequestedByUserId),
            entity.DecidedByUserId,
            entity.DecidedUtc,
            entity.DecisionNote,
            entity.AnnulledByUserId,
            entity.AnnulledUtc,
            entity.AnnulmentReason,
            entity.AnnulledBySettlementPublicId,
            entity.AppliedBySettlementPublicId,
            entity.CompensatedByCreditPublicId,
            entity.IsActive,
            entity.ConcurrencyToken);

    private static Guid? NullIfEmpty(Guid value) => value == Guid.Empty ? null : value;
}

/// <summary>
/// The snapshots + validated value of an overtime record, resolved against the masters/catalogs/plaza (this needs
/// a database, so it lives outside the pure <c>OvertimeRecordRules</c>): the overtime type (code + name +
/// reference factor snapshot) and the applied factor with its override note; the justification (code + name); the
/// plaza (default principal); the requester trío name (RRHH channel) or the subject employee (PORTAL channel); the
/// derived origin channel.
/// </summary>
internal sealed record OvertimeRecordResolved(
    Guid OvertimeTypePublicId,
    string OvertimeTypeCode,
    string OvertimeTypeName,
    decimal TypeFactor,
    decimal FactorApplied,
    string? FactorOverrideNote,
    Guid JustificationTypePublicId,
    string JustificationCode,
    string JustificationName,
    Guid AssignedPositionPublicId,
    Guid RequesterFilePublicId,
    string RequesterName,
    string OriginChannel);

internal static class OvertimeRecordWriteSupport
{
    /// <summary>The date sanity cap of a future work date (№2/№13): a shift more than 366 days ahead is rejected.</summary>
    public const int SanityCapDays = 366;

    private static Error Rule(string code) =>
        new(code, "The overtime record value is not coherent.", ErrorType.UnprocessableEntity);

    public static async Task<Result<OvertimeRecordResolved>> ResolveAndValidateAsync(
        OvertimeRecordInput input,
        PersonnelFile personnelFile,
        bool isManager,
        IPersonnelFileRepository personnelFileRepository,
        IPersonnelFileEmployeeRepository employeeRepository,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        // 1) Overtime type master — active type of the company; snapshot its code/name + reference factor (RN-19).
        var type = await employeeRepository.GetOvertimeTypeLookupAsync(input.OvertimeTypePublicId, personnelFile.TenantId, cancellationToken);
        if (type is null || !type.IsActive)
        {
            return Result<OvertimeRecordResolved>.Failure(OvertimeRecordErrors.OvertimeTypeInvalid);
        }

        // 2) Applied factor + override note (P-06): default to the type's reference factor when omitted.
        var factorApplied = input.FactorApplied ?? type.DefaultFactor;
        var factorRule = OvertimeRecordRules.ValidateFactor(factorApplied, type.DefaultFactor, input.FactorOverrideNote);
        if (!factorRule.IsValid)
        {
            return Result<OvertimeRecordResolved>.Failure(Rule(factorRule.ErrorCode!));
        }

        // 3) Duration (h:m → decimal, minutes 0–59, positive total).
        var duration = OvertimeRecordRules.DeriveDecimalHours(input.DurationHours, input.DurationMinutes);
        if (!duration.IsValid)
        {
            return Result<OvertimeRecordResolved>.Failure(Rule(duration.ErrorCode!));
        }

        // 4) Work-date sanity cap (№13): past OR future permitted, but not more than SanityCapDays ahead.
        var workDateRule = OvertimeRecordRules.ValidateWorkDate(input.WorkDate, today, SanityCapDays);
        if (!workDateRule.IsValid)
        {
            return Result<OvertimeRecordResolved>.Failure(Rule(workDateRule.ErrorCode!));
        }

        // 5) Justification type master — active type of the company; snapshot its code/name (RF-003).
        var justification = await employeeRepository.GetOvertimeJustificationLookupAsync(
            input.JustificationTypePublicId, personnelFile.TenantId, cancellationToken);
        if (justification is null || !justification.IsActive)
        {
            return Result<OvertimeRecordResolved>.Failure(OvertimeRecordErrors.JustificationInvalid);
        }

        // 6) Payroll type (REQ-004 catalog).
        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
                personnelFile.TenantId, PersonnelCurriculumCatalogCategories.PayrollType, input.PayrollTypeCode, cancellationToken))
        {
            return Result<OvertimeRecordResolved>.Failure(OvertimeRecordErrors.PayrollTypeInvalid);
        }

        // 7) Plaza (D-12): default the principal plaza (the settlement + input anchor). No cost center persisted (§0.1).
        var plaza = await employeeRepository.ResolveOvertimePlazaAsync(
            personnelFile.Id, input.AssignedPositionPublicId, cancellationToken);
        if (!plaza.Found)
        {
            return Result<OvertimeRecordResolved>.Failure(OvertimeRecordErrors.AssignedPositionInvalid);
        }

        // 8) Requester trío (№6/№10) + origin channel (P-01): on the PORTAL self-service channel the requester is
        // the subject employee; on the RRHH channel it is the supplied trío file (resolved for the name snapshot).
        Guid requesterFilePublicId;
        string requesterName;
        if (isManager)
        {
            if (input.RequesterFilePublicId is not { } requestedFileId || requestedFileId == Guid.Empty)
            {
                return Result<OvertimeRecordResolved>.Failure(OvertimeRecordErrors.RequesterInvalid);
            }

            var requester = await employeeRepository.GetOvertimeRequesterLookupAsync(
                requestedFileId, personnelFile.TenantId, cancellationToken);
            if (requester is null)
            {
                return Result<OvertimeRecordResolved>.Failure(OvertimeRecordErrors.RequesterInvalid);
            }

            requesterFilePublicId = requester.FilePublicId;
            requesterName = requester.FullName;
        }
        else
        {
            requesterFilePublicId = personnelFile.PublicId;
            requesterName = personnelFile.FullName;
        }

        var originChannel = isManager ? OvertimeRecordChannels.Rrhh : OvertimeRecordChannels.Portal;

        return Result<OvertimeRecordResolved>.Success(new OvertimeRecordResolved(
            type.PublicId,
            type.Code,
            type.Name,
            type.DefaultFactor,
            factorApplied,
            input.FactorOverrideNote,
            justification.PublicId,
            justification.Code,
            justification.Name,
            plaza.AssignedPositionPublicId,
            requesterFilePublicId,
            requesterName,
            originChannel));
    }

    /// <summary>
    /// Daily-cap guard (P-05, №12): the sum of the day's active minutes (EN_REVISION + AUTORIZADA + APLICADA,
    /// excluding the record being edited) plus the new minutes must not exceed the company cap. Best-effort (no
    /// lock — two simultaneous inserts may exceed, accepted since there is no monetary invariant). A null cap → OK.
    /// </summary>
    public static async Task<Error?> CheckDailyCapAsync(
        OvertimeRecordInput input,
        PersonnelFile personnelFile,
        Guid? excludeRecordPublicId,
        IPersonnelFileRepository personnelFileRepository,
        IPersonnelFileEmployeeRepository employeeRepository,
        CancellationToken cancellationToken)
    {
        var preferences = await personnelFileRepository.GetOvertimePreferencesAsync(personnelFile.TenantId, cancellationToken);
        var existingMinutes = await employeeRepository.GetActiveOvertimeMinutesForDayAsync(
            personnelFile.Id, input.WorkDate, personnelFile.TenantId, excludeRecordPublicId, cancellationToken);
        var newMinutes = (input.DurationHours * 60) + input.DurationMinutes;
        var cap = OvertimeRecordRules.ValidateDailyCap(existingMinutes, newMinutes, preferences.MaxDailyMinutes);
        return cap.IsExceeded
            ? new Error(OvertimeRecordRules.DailyCapExceededCode, "The overtime record exceeds the company's daily cap.", ErrorType.UnprocessableEntity)
            : null;
    }
}

/// <summary>
/// Separation-of-duties checks for the authorizer actions (aclaración №6, TRIPLE anti-self): neither the SUBJECT
/// employee (the file's linked login), the REGISTRAR (who created the record) nor the REQUESTER (the trío file's
/// linked login) may decide or revoke it. The requester pata (c) needs a database read, so it is async; a
/// requester file without a linked login cannot trip it (documented behavior). On the portal channel (a)=(b)=(c) —
/// a third empowered authorizer always decides.
/// </summary>
internal static class OvertimeRecordAuthorizerGuards
{
    public static async Task<Error?> CheckAsync(
        PersonnelFile personnelFile,
        PersonnelFileOvertimeRecord record,
        Guid actingUserId,
        IPersonnelFileEmployeeRepository employeeRepository,
        CancellationToken cancellationToken)
    {
        if (actingUserId == Guid.Empty)
        {
            return null;
        }

        // (a) The subject employee never decides/revokes their own record.
        if (personnelFile.LinkedUserPublicId is { } subjectUserId && subjectUserId == actingUserId)
        {
            return OvertimeRecordErrors.SelfApprovalForbidden;
        }

        // (b) The registrar never decides/revokes the record they registered.
        if (record.RequestedByUserId == actingUserId)
        {
            return OvertimeRecordErrors.SelfApprovalForbidden;
        }

        // (c) The requester (the trío file's linked login) never decides/revokes the record they asked for.
        var requester = await employeeRepository.GetOvertimeRequesterLookupAsync(
            record.RequesterFilePublicId, personnelFile.TenantId, cancellationToken);
        if (requester?.LinkedUserPublicId is { } requesterUserId && requesterUserId == actingUserId)
        {
            return OvertimeRecordErrors.SelfApprovalForbidden;
        }

        return null;
    }
}

// ── CRUD (dual channel: HR Manage or employee portal) ──────────────────────────────────────────────────

internal sealed class AddPersonnelFileOvertimeRecordCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFileOvertimeRecordCommand, OvertimeRecordResponse>
{
    public async Task<Result<OvertimeRecordResponse>> Handle(
        AddPersonnelFileOvertimeRecordCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile, isManager) = await LoadForCreateOwnOrManageOvertimeAsync<OvertimeRecordResponse>(
            command.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<OvertimeRecordResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        if (await employeeRepository.IsOvertimeProfileRetiredAsync(personnelFile.Id, cancellationToken))
        {
            return Result<OvertimeRecordResponse>.Failure(PersonnelFileErrors.ProfileRetiredLocked);
        }

        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow);
        var resolution = await OvertimeRecordWriteSupport.ResolveAndValidateAsync(
            command.Item, personnelFile, isManager, personnelFileRepository, employeeRepository, today, cancellationToken);
        if (resolution.IsFailure)
        {
            return Result<OvertimeRecordResponse>.Failure(resolution.Error);
        }

        if (await OvertimeRecordWriteSupport.CheckDailyCapAsync(
                command.Item, personnelFile, excludeRecordPublicId: null, personnelFileRepository, employeeRepository, cancellationToken) is { } capError)
        {
            return Result<OvertimeRecordResponse>.Failure(capError);
        }

        var resolved = resolution.Value;
        _ = Guid.TryParse(currentUserService.UserId, out var requestedByUserId);

        var entity = PersonnelFileOvertimeRecord.Create(
            command.Item.WorkDate,
            resolved.OvertimeTypePublicId,
            resolved.OvertimeTypeCode,
            resolved.OvertimeTypeName,
            resolved.TypeFactor,
            resolved.FactorApplied,
            resolved.FactorOverrideNote,
            command.Item.DurationHours,
            command.Item.DurationMinutes,
            command.Item.StartTime,
            command.Item.EndTime,
            resolved.JustificationTypePublicId,
            resolved.JustificationCode,
            resolved.JustificationName,
            command.Item.Observations,
            resolved.OriginChannel,
            resolved.AssignedPositionPublicId,
            resolved.RequesterFilePublicId,
            resolved.RequesterName,
            command.Item.PayrollTypeCode,
            payrollPeriodId: null,
            command.Item.PayrollPeriodPublicId,
            command.Item.PayrollPeriodLabel,
            command.Item.PayrollPeriodEndDate,
            requestedByUserId);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        var all = await employeeRepository.AddOvertimeRecordAsync(personnelFile.Id, personnelFile.TenantId, entity, cancellationToken);
        var response = all.SingleOrDefault(item => item.Id == entity.PublicId)
            ?? throw new InvalidOperationException("Overtime-record response could not be resolved after creation.");
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Registered overtime record for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<OvertimeRecordResponse>.Success(response);
    }
}

internal sealed class UpdatePersonnelFileOvertimeRecordCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileOvertimeRecordCommand, OvertimeRecordResponse>
{
    public async Task<Result<OvertimeRecordResponse>> Handle(
        UpdatePersonnelFileOvertimeRecordCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile, isManager) = await LoadForManageOrOwnOvertimeAsync<OvertimeRecordResponse>(
            command.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<OvertimeRecordResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        if (await employeeRepository.IsOvertimeProfileRetiredAsync(personnelFile.Id, cancellationToken))
        {
            return Result<OvertimeRecordResponse>.Failure(PersonnelFileErrors.ProfileRetiredLocked);
        }

        var entity = await employeeRepository.GetOvertimeRecordEntityAsync(
            personnelFile.PublicId, command.OvertimeRecordPublicId, personnelFile.TenantId, cancellationToken);
        if (entity is null)
        {
            return Result<OvertimeRecordResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OvertimeRecordResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // The employee portal (self) may only edit their OWN EN_REVISION record registered through the portal.
        if (!isManager && entity.OriginChannel != OvertimeRecordChannels.Portal)
        {
            return Result<OvertimeRecordResponse>.Failure(PersonnelFileErrors.Forbidden);
        }

        // Only an EN_REVISION record can be edited (RN-02); pre-check to avoid a domain exception → 500.
        if (entity.StatusCode != OvertimeRecordStatuses.EnRevision)
        {
            return Result<OvertimeRecordResponse>.Failure(OvertimeRecordErrors.StateRuleViolation);
        }

        var today = DateOnly.FromDateTime(dateTimeProvider.UtcNow);
        var resolution = await OvertimeRecordWriteSupport.ResolveAndValidateAsync(
            command.Item, personnelFile, isManager, personnelFileRepository, employeeRepository, today, cancellationToken);
        if (resolution.IsFailure)
        {
            return Result<OvertimeRecordResponse>.Failure(resolution.Error);
        }

        if (await OvertimeRecordWriteSupport.CheckDailyCapAsync(
                command.Item, personnelFile, entity.PublicId, personnelFileRepository, employeeRepository, cancellationToken) is { } capError)
        {
            return Result<OvertimeRecordResponse>.Failure(capError);
        }

        var resolved = resolution.Value;
        entity.Update(
            command.Item.WorkDate,
            resolved.OvertimeTypePublicId,
            resolved.OvertimeTypeCode,
            resolved.OvertimeTypeName,
            resolved.TypeFactor,
            resolved.FactorApplied,
            resolved.FactorOverrideNote,
            command.Item.DurationHours,
            command.Item.DurationMinutes,
            command.Item.StartTime,
            command.Item.EndTime,
            resolved.JustificationTypePublicId,
            resolved.JustificationCode,
            resolved.JustificationName,
            command.Item.Observations,
            resolved.AssignedPositionPublicId,
            resolved.RequesterFilePublicId,
            resolved.RequesterName,
            command.Item.PayrollTypeCode,
            payrollPeriodId: null,
            command.Item.PayrollPeriodPublicId,
            command.Item.PayrollPeriodLabel,
            command.Item.PayrollPeriodEndDate);

        var response = OvertimeRecordMapping.ToResponse(entity);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Updated overtime record for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<OvertimeRecordResponse>.Success(response);
    }
}

internal sealed class DeletePersonnelFileOvertimeRecordCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DeletePersonnelFileOvertimeRecordCommand, PersonnelFileParentConcurrencyResult>
{
    public async Task<Result<PersonnelFileParentConcurrencyResult>> Handle(
        DeletePersonnelFileOvertimeRecordCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile, isManager) = await LoadForManageOrOwnOvertimeAsync<PersonnelFileParentConcurrencyResult>(
            command.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var entity = await employeeRepository.GetOvertimeRecordEntityAsync(
            personnelFile.PublicId, command.OvertimeRecordPublicId, personnelFile.TenantId, cancellationToken);
        if (entity is null)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (!isManager && entity.OriginChannel != OvertimeRecordChannels.Portal)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(PersonnelFileErrors.Forbidden);
        }

        // Only an EN_REVISION draft can be discarded (soft delete); an authorized record is revoked/annulled.
        if (entity.StatusCode != OvertimeRecordStatuses.EnRevision)
        {
            return Result<PersonnelFileParentConcurrencyResult>.Failure(OvertimeRecordErrors.StateRuleViolation);
        }

        entity.Deactivate();
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"Deactivated overtime record draft for {personnelFile.FullName}.", OvertimeRecordMapping.ToResponse(entity), cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<PersonnelFileParentConcurrencyResult>.Success(new PersonnelFileParentConcurrencyResult(personnelFile.ConcurrencyToken));
    }
}

// ── Lifecycle: annulment (dual channel) / re-imputation (Manage) ───────────────────────────────────────

internal sealed class AnnulPersonnelFileOvertimeRecordCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AnnulPersonnelFileOvertimeRecordCommand, OvertimeRecordResponse>
{
    public async Task<Result<OvertimeRecordResponse>> Handle(
        AnnulPersonnelFileOvertimeRecordCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile, isManager) = await LoadForManageOrOwnOvertimeAsync<OvertimeRecordResponse>(
            command.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entity = await employeeRepository.GetOvertimeRecordEntityAsync(
            personnelFile!.PublicId, command.OvertimeRecordPublicId, personnelFile.TenantId, cancellationToken);
        if (entity is null)
        {
            return Result<OvertimeRecordResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OvertimeRecordResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (!isManager && entity.OriginChannel != OvertimeRecordChannels.Portal)
        {
            return Result<OvertimeRecordResponse>.Failure(PersonnelFileErrors.Forbidden);
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return Result<OvertimeRecordResponse>.Failure(OvertimeRecordErrors.AnnulmentReasonRequired);
        }

        // Manage/self annulment (retiro) operates on an EN_REVISION record; an AUTORIZADA one is revoked by the authorizer.
        if (entity.StatusCode != OvertimeRecordStatuses.EnRevision)
        {
            return Result<OvertimeRecordResponse>.Failure(OvertimeRecordErrors.StateRuleViolation);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var byUserId);
        entity.Annul(command.Reason, byUserId, dateTimeProvider.UtcNow);
        TouchPersonnelFile(personnelFile);

        return await OvertimeRecordManageLoad.FinishAsync(entity, personnelFile, auditService, unitOfWork, "Annulled", cancellationToken);
    }
}

internal sealed class RetargetPersonnelFileOvertimeRecordPeriodCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<RetargetPersonnelFileOvertimeRecordPeriodCommand, OvertimeRecordResponse>
{
    public async Task<Result<OvertimeRecordResponse>> Handle(
        RetargetPersonnelFileOvertimeRecordPeriodCommand command,
        CancellationToken cancellationToken)
    {
        // Re-imputation is Manage-only (no self-service).
        var (failure, personnelFile) = await LoadForManageOvertimeRecordsAsync<OvertimeRecordResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entity = await employeeRepository.GetOvertimeRecordEntityAsync(
            personnelFile!.PublicId, command.OvertimeRecordPublicId, personnelFile.TenantId, cancellationToken);
        if (entity is null)
        {
            return Result<OvertimeRecordResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OvertimeRecordResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // Re-imputation ("enviar a otro periodo", RF-005) only from AUTORIZADA; pre-check to avoid a 500.
        if (entity.StatusCode != OvertimeRecordStatuses.Autorizada)
        {
            return Result<OvertimeRecordResponse>.Failure(OvertimeRecordErrors.NotRetargetable);
        }

        if (!await personnelFileRepository.CatalogCodeIsActiveAsync(
                personnelFile.TenantId, PersonnelCurriculumCatalogCategories.PayrollType, command.Period.PayrollTypeCode, cancellationToken))
        {
            return Result<OvertimeRecordResponse>.Failure(OvertimeRecordErrors.PayrollTypeInvalid);
        }

        // The payroll-period FK resolution is deferred to PR-4; PR-3 re-targets in the degraded mode (label +
        // optional public id + optional end date, no hard FK).
        entity.RetargetPeriod(
            command.Period.PayrollTypeCode,
            payrollPeriodId: null,
            command.Period.PayrollPeriodPublicId,
            command.Period.PayrollPeriodLabel,
            command.Period.PayrollPeriodEndDate,
            dateTimeProvider.UtcNow);
        TouchPersonnelFile(personnelFile);

        return await OvertimeRecordManageLoad.FinishAsync(entity, personnelFile, auditService, unitOfWork, "Re-targeted", cancellationToken);
    }
}

/// <summary>Shared finish glue for the lifecycle handlers (annulment / re-imputation / resolution / revocation).</summary>
internal static class OvertimeRecordManageLoad
{
    // The caller (a command-handler subclass) touches the personnel file before calling this so its concurrency
    // token rotates with the sub-record write; this shared glue only persists + audits inside the transaction.
    public static async Task<Result<OvertimeRecordResponse>> FinishAsync(
        PersonnelFileOvertimeRecord entity,
        PersonnelFile personnelFile,
        IAuditService auditService,
        IUnitOfWork unitOfWork,
        string verb,
        CancellationToken cancellationToken)
    {
        var response = OvertimeRecordMapping.ToResponse(entity);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, $"{verb} overtime record for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<OvertimeRecordResponse>.Success(response);
    }
}

// ── Resolution / revocation (Authorize — TRIPLE anti-self) ─────────────────────────────────────────────

internal sealed class ResolvePersonnelFileOvertimeRecordCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<ResolvePersonnelFileOvertimeRecordCommand, OvertimeRecordResponse>
{
    public async Task<Result<OvertimeRecordResponse>> Handle(
        ResolvePersonnelFileOvertimeRecordCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForAuthorizeOvertimeRecordsAsync<OvertimeRecordResponse>(
            command.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entity = await employeeRepository.GetOvertimeRecordEntityAsync(
            personnelFile!.PublicId, command.OvertimeRecordPublicId, personnelFile.TenantId, cancellationToken);
        if (entity is null)
        {
            return Result<OvertimeRecordResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OvertimeRecordResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // Re-verify the state inside the request (a second concurrent decision holds a stale token → 409 above, or
        // a non-EN_REVISION state here → 422).
        if (entity.StatusCode != OvertimeRecordStatuses.EnRevision)
        {
            return Result<OvertimeRecordResponse>.Failure(OvertimeRecordErrors.StateRuleViolation);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var decidedByUserId);
        if (await OvertimeRecordAuthorizerGuards.CheckAsync(personnelFile, entity, decidedByUserId, employeeRepository, cancellationToken) is { } selfError)
        {
            return Result<OvertimeRecordResponse>.Failure(selfError);
        }

        var target = command.TargetStatusCode.Trim().ToUpperInvariant();
        var isAuthorize = target == OvertimeRecordStatuses.Autorizada;
        var isReject = target == OvertimeRecordStatuses.Rechazada;
        if ((!isAuthorize && !isReject)
            || !await personnelFileRepository.CatalogCodeIsActiveAsync(
                personnelFile.TenantId, PersonnelCurriculumCatalogCategories.OvertimeRecordStatus, target, cancellationToken))
        {
            return Result<OvertimeRecordResponse>.Failure(OvertimeRecordErrors.StatusInvalid);
        }

        var now = dateTimeProvider.UtcNow;
        string verb;
        if (isAuthorize)
        {
            entity.Approve(decidedByUserId, now);
            verb = "Authorized";
        }
        else
        {
            if (string.IsNullOrWhiteSpace(command.Note))
            {
                return Result<OvertimeRecordResponse>.Failure(OvertimeRecordErrors.DecisionNoteRequired);
            }

            entity.Reject(decidedByUserId, now, command.Note);
            verb = "Rejected";
        }

        TouchPersonnelFile(personnelFile);
        return await OvertimeRecordManageLoad.FinishAsync(entity, personnelFile, auditService, unitOfWork, verb, cancellationToken);
    }
}

internal sealed class RevokePersonnelFileOvertimeRecordCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<RevokePersonnelFileOvertimeRecordCommand, OvertimeRecordResponse>
{
    public async Task<Result<OvertimeRecordResponse>> Handle(
        RevokePersonnelFileOvertimeRecordCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForAuthorizeOvertimeRecordsAsync<OvertimeRecordResponse>(
            command.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entity = await employeeRepository.GetOvertimeRecordEntityAsync(
            personnelFile!.PublicId, command.OvertimeRecordPublicId, personnelFile.TenantId, cancellationToken);
        if (entity is null)
        {
            return Result<OvertimeRecordResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<OvertimeRecordResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        // Revocation applies to an AUTORIZADA record only.
        if (entity.StatusCode != OvertimeRecordStatuses.Autorizada)
        {
            return Result<OvertimeRecordResponse>.Failure(OvertimeRecordErrors.StateRuleViolation);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var byUserId);
        if (await OvertimeRecordAuthorizerGuards.CheckAsync(personnelFile, entity, byUserId, employeeRepository, cancellationToken) is { } selfError)
        {
            return Result<OvertimeRecordResponse>.Failure(selfError);
        }

        if (string.IsNullOrWhiteSpace(command.Reason))
        {
            return Result<OvertimeRecordResponse>.Failure(OvertimeRecordErrors.AnnulmentReasonRequired);
        }

        entity.Annul(command.Reason, byUserId, dateTimeProvider.UtcNow);
        TouchPersonnelFile(personnelFile);

        return await OvertimeRecordManageLoad.FinishAsync(entity, personnelFile, auditService, unitOfWork, "Revoked", cancellationToken);
    }
}

// ── Reads (View OR self — P-12 self-read) ──────────────────────────────────────────────────────────────

internal sealed class GetPersonnelFileOvertimeRecordsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileOvertimeRecordsQuery, IReadOnlyCollection<OvertimeRecordResponse>>
{
    public async Task<Result<IReadOnlyCollection<OvertimeRecordResponse>>> Handle(
        GetPersonnelFileOvertimeRecordsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForOvertimeReadAsync<IReadOnlyCollection<OvertimeRecordResponse>>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetOvertimeRecordsAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<OvertimeRecordResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileOvertimeRecordByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileOvertimeRecordByIdQuery, OvertimeRecordResponse>
{
    public async Task<Result<OvertimeRecordResponse>> Handle(
        GetPersonnelFileOvertimeRecordByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForOvertimeReadAsync<OvertimeRecordResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await employeeRepository.GetOvertimeRecordAsync(personnelFile!.PublicId, query.OvertimeRecordPublicId, cancellationToken);
        return response is null
            ? Result<OvertimeRecordResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<OvertimeRecordResponse>.Success(response);
    }
}

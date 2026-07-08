using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Preferences;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Application.Features.PersonnelFiles.CompensatoryTime;
using CLARIHR.Domain.Leave;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Shared write glue of the compensatory-time absence handlers: the type resolution + operation check (RN-04),
/// the payroll-period imputation validation (P-14), the own + cross-module overlap checks (RN-05) and the
/// GOCE_TIEMPO_COMPENSATORIO journal (fila #13). The fund-balance re-verification runs in the handler under the
/// advisory lock (RN-03) — it is NOT here.
/// </summary>
internal static class CompensatoryTimeAbsenceWriteSupport
{
    /// <summary>
    /// Resolves the type (422 when inactive/foreign) and enforces the debiting operation (RN-04: an absence
    /// requires a DEBITA or AMBAS type). Returns the resolved type on success.
    /// </summary>
    public static async Task<Result<CompensatoryTimeTypeRef>> ResolveDebitingTypeAsync(
        ICompensatoryTimeRepository repository,
        Guid tenantId,
        Guid typePublicId,
        CancellationToken cancellationToken)
    {
        var type = await repository.ResolveTypeAsync(tenantId, typePublicId, cancellationToken);
        if (type is null)
        {
            return Result<CompensatoryTimeTypeRef>.Failure(CompensatoryTimeCreditErrors.TypeInvalid);
        }

        if (type.OperationCode == CompensatoryTimeOperations.Credits)
        {
            return Result<CompensatoryTimeTypeRef>.Failure(CompensatoryTimeCreditErrors.TypeOperationMismatch);
        }

        return Result<CompensatoryTimeTypeRef>.Success(type);
    }

    /// <summary>The payroll-period imputation (P-14) + overlap pre-checks (RN-05). Clean 422 before any mutation.</summary>
    public static async Task<Error?> ValidateImputationAndOverlapsAsync(
        ICompensatoryTimeRepository repository,
        Guid tenantId,
        long personnelFileId,
        CompensatoryTimeAbsenceInput item,
        long? excludeAbsenceId,
        CancellationToken cancellationToken)
    {
        if (item.PayrollPeriodPublicId is { } payrollPeriodPublicId
            && !await repository.PayrollPeriodExistsAsync(tenantId, payrollPeriodPublicId, cancellationToken))
        {
            return CompensatoryTimeAbsenceErrors.PayrollPeriodInvalid;
        }

        if (await repository.HasOverlappingAbsenceAsync(
            personnelFileId, item.StartDate, item.EndDate, excludeAbsenceId, cancellationToken))
        {
            return CompensatoryTimeAbsenceErrors.AbsenceOverlap;
        }

        var crossOverlap = await repository.CheckCrossModuleOverlapAsync(
            personnelFileId, item.StartDate, item.EndDate, cancellationToken);
        if (crossOverlap.IncapacityOverlap)
        {
            return CompensatoryTimeAbsenceErrors.IncapacityOverlap;
        }

        if (crossOverlap.VacationOverlap)
        {
            return CompensatoryTimeAbsenceErrors.VacationOverlap;
        }

        return null;
    }

    /// <summary>Journals the GOCE_TIEMPO_COMPENSATORIO personnel action in the same transaction (fila #13).</summary>
    public static async Task AddAbsenceJournalAsync(
        IPersonnelFileEmployeeRepository employeeRepository,
        long personnelFileId,
        Guid tenantId,
        PersonnelFileCompensatoryTimeAbsence entity,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var action = PersonnelFilePersonnelAction.Create(
            "GOCE_TIEMPO_COMPENSATORIO",
            "APLICADA",
            actionDateUtc: nowUtc,
            effectiveFromUtc: entity.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            effectiveToUtc: entity.EndDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            description: $"GOCE_TIEMPO_COMPENSATORIO ({entity.HoursDebited:0.##} h) {entity.StartDate:yyyy-MM-dd}..{entity.EndDate:yyyy-MM-dd}.",
            reference: null,
            amount: null,
            currencyCode: null,
            isSystemGenerated: true);
        action.BindToPersonnelFile(personnelFileId);
        action.SetTenantId(tenantId);
        _ = await employeeRepository.AddPersonnelActionAsync(action, cancellationToken);
    }
}

internal sealed class AddCompensatoryTimeAbsenceCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICompensatoryTimeRepository compensatoryTimeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddCompensatoryTimeAbsenceCommand, PersonnelFileCompensatoryTimeAbsenceResponse>
{
    public async Task<Result<PersonnelFileCompensatoryTimeAbsenceResponse>> Handle(
        AddCompensatoryTimeAbsenceCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageCompensatoryTimeAsync<PersonnelFileCompensatoryTimeAbsenceResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileCompensatoryTimeAbsenceResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var tenantId = personnelFile.TenantId;
        if (await compensatoryTimeRepository.IsProfileRetiredAsync(personnelFile.Id, cancellationToken))
        {
            return Result<PersonnelFileCompensatoryTimeAbsenceResponse>.Failure(PersonnelFileErrors.ProfileRetiredLocked);
        }

        var item = command.Item;
        var typeResult = await CompensatoryTimeAbsenceWriteSupport.ResolveDebitingTypeAsync(
            compensatoryTimeRepository, tenantId, item.CompensatoryTimeTypePublicId, cancellationToken);
        if (typeResult.IsFailure)
        {
            return Result<PersonnelFileCompensatoryTimeAbsenceResponse>.Failure(typeResult.Error);
        }

        var type = typeResult.Value;
        if (await CompensatoryTimeAbsenceWriteSupport.ValidateImputationAndOverlapsAsync(
            compensatoryTimeRepository, tenantId, personnelFile.Id, item, excludeAbsenceId: null, cancellationToken) is { } overlapError)
        {
            return Result<PersonnelFileCompensatoryTimeAbsenceResponse>.Failure(overlapError);
        }

        var nowUtc = dateTimeProvider.UtcNow;

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // R-T1 (the gate): an absence debits the fund, so serialize under the advisory lock and re-verify the
            // never-negative invariant against the freshly read balance INSIDE the transaction (RN-03, №3).
            await compensatoryTimeRepository.AcquireFundLockAsync(tenantId, personnelFile.Id, cancellationToken);
            var balance = await compensatoryTimeRepository.GetBalanceAsync(personnelFile.Id, cancellationToken);
            var debitCheck = CompensatoryTimeRules.ValidateDebit(balance, item.HoursDebited);
            if (!debitCheck.IsAllowed)
            {
                return Result<PersonnelFileCompensatoryTimeAbsenceResponse>.Failure(CompensatoryTimeAbsenceErrors.BalanceInsufficient);
            }

            var entity = PersonnelFileCompensatoryTimeAbsence.Create(
                type.InternalId,
                type.Name,
                item.StartDate,
                item.EndDate,
                item.HoursDebited,
                item.Reason,
                item.PayrollPeriodPublicId,
                currentUserService.UserId ?? string.Empty,
                item.Notes);
            entity.BindToPersonnelFile(personnelFile.Id);
            entity.SetTenantId(tenantId);
            compensatoryTimeRepository.AddAbsence(entity);

            await CompensatoryTimeAbsenceWriteSupport.AddAbsenceJournalAsync(
                employeeRepository, personnelFile.Id, tenantId, entity, nowUtc, cancellationToken);

            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await compensatoryTimeRepository.GetAbsenceResponseAsync(personnelFile.PublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Compensatory-time absence response could not be resolved after creation.");

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Registered a compensatory-time absence for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileCompensatoryTimeAbsenceResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateCompensatoryTimeAbsenceCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    ICompensatoryTimeRepository compensatoryTimeRepository,
    ITenantContext tenantContext,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdateCompensatoryTimeAbsenceCommand, PersonnelFileCompensatoryTimeAbsenceResponse>
{
    public async Task<Result<PersonnelFileCompensatoryTimeAbsenceResponse>> Handle(
        UpdateCompensatoryTimeAbsenceCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageCompensatoryTimeAsync<PersonnelFileCompensatoryTimeAbsenceResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var tenantId = personnelFile!.TenantId;
        if (await compensatoryTimeRepository.IsProfileRetiredAsync(personnelFile.Id, cancellationToken))
        {
            return Result<PersonnelFileCompensatoryTimeAbsenceResponse>.Failure(PersonnelFileErrors.ProfileRetiredLocked);
        }

        var entity = await compensatoryTimeRepository.GetAbsenceEntityAsync(personnelFile.PublicId, command.CompensatoryTimeAbsencePublicId, cancellationToken);
        if (entity is null)
        {
            return Result<PersonnelFileCompensatoryTimeAbsenceResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileCompensatoryTimeAbsenceResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (entity.StatusCode != CompensatoryTimeStatuses.Registrada)
        {
            return Result<PersonnelFileCompensatoryTimeAbsenceResponse>.Failure(CompensatoryTimeCreditErrors.StateRuleViolation);
        }

        var item = command.Item;
        var typeResult = await CompensatoryTimeAbsenceWriteSupport.ResolveDebitingTypeAsync(
            compensatoryTimeRepository, tenantId, item.CompensatoryTimeTypePublicId, cancellationToken);
        if (typeResult.IsFailure)
        {
            return Result<PersonnelFileCompensatoryTimeAbsenceResponse>.Failure(typeResult.Error);
        }

        var type = typeResult.Value;
        if (await CompensatoryTimeAbsenceWriteSupport.ValidateImputationAndOverlapsAsync(
            compensatoryTimeRepository, tenantId, personnelFile.Id, item, excludeAbsenceId: entity.Id, cancellationToken) is { } overlapError)
        {
            return Result<PersonnelFileCompensatoryTimeAbsenceResponse>.Failure(overlapError);
        }

        var previousHoursDebited = entity.HoursDebited;

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // R-T1: raising the debited hours reduces the balance, so serialize under the advisory lock and
            // re-verify against the balance that EXCLUDES this absence's old debit (RN-03).
            await compensatoryTimeRepository.AcquireFundLockAsync(tenantId, personnelFile.Id, cancellationToken);
            var balanceExcludingThis = await compensatoryTimeRepository.GetBalanceAsync(personnelFile.Id, cancellationToken) + previousHoursDebited;
            var debitCheck = CompensatoryTimeRules.ValidateDebit(balanceExcludingThis, item.HoursDebited);
            if (!debitCheck.IsAllowed)
            {
                return Result<PersonnelFileCompensatoryTimeAbsenceResponse>.Failure(CompensatoryTimeAbsenceErrors.BalanceInsufficient);
            }

            entity.Update(
                type.InternalId,
                type.Name,
                item.StartDate,
                item.EndDate,
                item.HoursDebited,
                item.Reason,
                item.PayrollPeriodPublicId,
                item.Notes);

            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await compensatoryTimeRepository.GetAbsenceResponseAsync(personnelFile.PublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Compensatory-time absence response could not be resolved after update.");

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Updated a compensatory-time absence for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileCompensatoryTimeAbsenceResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class AnnulCompensatoryTimeAbsenceCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    ICompensatoryTimeRepository compensatoryTimeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AnnulCompensatoryTimeAbsenceCommand, PersonnelFileCompensatoryTimeAbsenceResponse>
{
    public async Task<Result<PersonnelFileCompensatoryTimeAbsenceResponse>> Handle(
        AnnulCompensatoryTimeAbsenceCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageCompensatoryTimeAsync<PersonnelFileCompensatoryTimeAbsenceResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var tenantId = personnelFile!.TenantId;
        if (await compensatoryTimeRepository.IsProfileRetiredAsync(personnelFile.Id, cancellationToken))
        {
            return Result<PersonnelFileCompensatoryTimeAbsenceResponse>.Failure(PersonnelFileErrors.ProfileRetiredLocked);
        }

        var entity = await compensatoryTimeRepository.GetAbsenceEntityAsync(personnelFile.PublicId, command.CompensatoryTimeAbsencePublicId, cancellationToken);
        if (entity is null)
        {
            return Result<PersonnelFileCompensatoryTimeAbsenceResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileCompensatoryTimeAbsenceResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (entity.StatusCode != CompensatoryTimeStatuses.Registrada)
        {
            return Result<PersonnelFileCompensatoryTimeAbsenceResponse>.Failure(CompensatoryTimeCreditErrors.StateRuleViolation);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // Annulling an absence RESTORES hours to the fund (raises the balance), so it cannot uncover a
            // negative balance; the advisory lock is still taken to serialize against concurrent debits (№3).
            await compensatoryTimeRepository.AcquireFundLockAsync(tenantId, personnelFile.Id, cancellationToken);

            entity.Annul(command.Reason, currentUserService.UserId ?? string.Empty, dateTimeProvider.UtcNow);
            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await compensatoryTimeRepository.GetAbsenceResponseAsync(personnelFile.PublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Compensatory-time absence response could not be resolved after annulment.");

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Annulled a compensatory-time absence for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileCompensatoryTimeAbsenceResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class GetCompensatoryTimeAbsencesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    ICompensatoryTimeRepository compensatoryTimeRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetCompensatoryTimeAbsencesQuery, IReadOnlyCollection<PersonnelFileCompensatoryTimeAbsenceResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileCompensatoryTimeAbsenceResponse>>> Handle(
        GetCompensatoryTimeAbsencesQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForCompensatoryTimeReadAsync<IReadOnlyCollection<PersonnelFileCompensatoryTimeAbsenceResponse>>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await compensatoryTimeRepository.GetAbsenceResponsesAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileCompensatoryTimeAbsenceResponse>>.Success(response);
    }
}

internal sealed class GetCompensatoryTimeAbsenceByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    ICompensatoryTimeRepository compensatoryTimeRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetCompensatoryTimeAbsenceByIdQuery, PersonnelFileCompensatoryTimeAbsenceResponse>
{
    public async Task<Result<PersonnelFileCompensatoryTimeAbsenceResponse>> Handle(
        GetCompensatoryTimeAbsenceByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForCompensatoryTimeReadAsync<PersonnelFileCompensatoryTimeAbsenceResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await compensatoryTimeRepository.GetAbsenceResponseAsync(personnelFile!.PublicId, query.CompensatoryTimeAbsencePublicId, cancellationToken);
        return response is null
            ? Result<PersonnelFileCompensatoryTimeAbsenceResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<PersonnelFileCompensatoryTimeAbsenceResponse>.Success(response);
    }
}

internal sealed class GetCompensatoryTimeAbsenceHoursSuggestionQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    ICompensatoryTimeRepository compensatoryTimeRepository,
    ICompanyPreferenceRepository companyPreferenceRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetCompensatoryTimeAbsenceHoursSuggestionQuery, CompensatoryTimeAbsenceHoursSuggestionResponse>
{
    public async Task<Result<CompensatoryTimeAbsenceHoursSuggestionResponse>> Handle(
        GetCompensatoryTimeAbsenceHoursSuggestionQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForCompensatoryTimeReadAsync<CompensatoryTimeAbsenceHoursSuggestionResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var tenantId = personnelFile!.TenantId;
        var preference = await companyPreferenceRepository.GetByTenantIdAsync(tenantId, cancellationToken);
        var standardDailyHours = preference?.CompensatoryTimeStandardDailyHours ?? 8m;

        // Rest day: the plaza's weekly rest day, then the company preference; degraded (null) when neither is set.
        var plazaRestDay = await compensatoryTimeRepository.GetPrimaryPlazaRestDayAsync(personnelFile.Id, cancellationToken);
        var restDay = plazaRestDay
            ?? (preference?.CompanyRestDayOfWeek is { } day && day is >= 0 and <= 6 ? (DayOfWeek?)day : null);

        var holidays = await compensatoryTimeRepository.GetHolidaysInRangeAsync(tenantId, query.StartDate, query.EndDate, cancellationToken);
        var suggestedHours = CompensatoryTimeRules.SuggestAbsenceHours(
            query.StartDate, query.EndDate, restDay, holidays, standardDailyHours);

        var workingDays = standardDailyHours == 0m ? 0 : (int)(suggestedHours / standardDailyHours);
        return Result<CompensatoryTimeAbsenceHoursSuggestionResponse>.Success(
            new CompensatoryTimeAbsenceHoursSuggestionResponse(
                query.StartDate,
                query.EndDate,
                suggestedHours,
                standardDailyHours,
                workingDays,
                restDay is { } resolvedRestDay ? (int)resolvedRestDay : null,
                holidays.Count));
    }
}

using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Shared write glue of the lactation handlers: pre-validates the schedule set (containment + no overlap) so
/// the caller gets a clean 422 (<c>LACTATION_SCHEDULE_OUT_OF_RANGE</c> / <c>LACTATION_SCHEDULE_OVERLAP</c>)
/// before the domain <c>ReplaceSchedules</c> guard — which re-validates the same invariants — ever runs, and
/// maps the wire DTOs to the domain <see cref="LactationScheduleInput"/>.
/// </summary>
internal static class LactationWriteSupport
{
    public static Result ValidateSchedules(
        DateOnly periodStart,
        DateOnly periodEnd,
        IReadOnlyList<LactationScheduleInputDto> schedules)
    {
        var ordered = schedules
            .OrderBy(schedule => schedule.StartDate)
            .ThenBy(schedule => schedule.EndDate)
            .ToList();

        for (var index = 0; index < ordered.Count; index++)
        {
            var schedule = ordered[index];
            if (schedule.StartDate < periodStart || schedule.EndDate > periodEnd)
            {
                return Result.Failure(LactationErrors.ScheduleOutOfRange);
            }

            if (index > 0 && schedule.StartDate <= ordered[index - 1].EndDate)
            {
                return Result.Failure(LactationErrors.ScheduleOverlap);
            }
        }

        return Result.Success();
    }

    public static IReadOnlyCollection<LactationScheduleInput> ToDomainInputs(
        IReadOnlyList<LactationScheduleInputDto> schedules) =>
        schedules
            .Select(schedule => new LactationScheduleInput(
                schedule.StartDate, schedule.EndDate, schedule.DailyPermitsCount, schedule.MinutesPerPermit))
            .ToArray();
}

internal sealed class AddPersonnelFileLactationPeriodCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IPersonnelFileLactationRepository lactationRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFileLactationPeriodCommand, PersonnelFileLactationPeriodResponse>
{
    public async Task<Result<PersonnelFileLactationPeriodResponse>> Handle(
        AddPersonnelFileLactationPeriodCommand command,
        CancellationToken cancellationToken)
    {
        // Lactation is HR-only (D-18): no self-service branch.
        var (failure, personnelFile) = await LoadForManageIncapacitiesAsync<PersonnelFileLactationPeriodResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileLactationPeriodResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var item = command.Item;
        var tenantId = personnelFile.TenantId;

        var typeInternalId = await lactationRepository.ResolveLactationTypeInternalIdAsync(
            tenantId, item.IncapacityTypePublicId, cancellationToken);
        if (typeInternalId is null)
        {
            return Result<PersonnelFileLactationPeriodResponse>.Failure(LactationErrors.TypeInvalid);
        }

        var scheduleValidation = LactationWriteSupport.ValidateSchedules(item.StartDate, item.EndDate, item.Schedules);
        if (scheduleValidation.IsFailure)
        {
            return Result<PersonnelFileLactationPeriodResponse>.Failure(scheduleValidation.Error);
        }

        var entity = PersonnelFileLactationPeriod.Create(
            requesterFilePublicId: null,
            requesterNameSnapshot: null,
            requestedByUserId: currentUserService.UserId ?? string.Empty,
            incapacityTypeId: typeInternalId.Value,
            startDate: item.StartDate,
            endDate: item.EndDate,
            notes: item.Notes);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(tenantId);
        entity.ReplaceSchedules(LactationWriteSupport.ToDomainInputs(item.Schedules));

        var nowUtc = dateTimeProvider.UtcNow;

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            lactationRepository.Add(entity);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await AddLactationJournalAsync(employeeRepository, personnelFile.Id, tenantId, entity, nowUtc, cancellationToken);

            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await lactationRepository.GetResponseAsync(personnelFile.PublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Lactation period response could not be resolved after creation.");

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Registered a lactation period for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileLactationPeriodResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    internal static async Task AddLactationJournalAsync(
        IPersonnelFileEmployeeRepository employeeRepository,
        long personnelFileId,
        Guid tenantId,
        PersonnelFileLactationPeriod entity,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var action = PersonnelFilePersonnelAction.Create(
            "LACTANCIA",
            "APLICADA",
            actionDateUtc: nowUtc,
            effectiveFromUtc: entity.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            effectiveToUtc: entity.EndDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            description: $"LACTANCIA {entity.StartDate:yyyy-MM-dd} — {entity.EndDate:yyyy-MM-dd}.",
            reference: null,
            amount: null,
            currencyCode: null,
            isSystemGenerated: true);
        action.BindToPersonnelFile(personnelFileId);
        action.SetTenantId(tenantId);
        _ = await employeeRepository.AddPersonnelActionAsync(action, cancellationToken);
    }
}

internal sealed class UpdatePersonnelFileLactationPeriodCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileLactationRepository lactationRepository,
    ITenantContext tenantContext,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<UpdatePersonnelFileLactationPeriodCommand, PersonnelFileLactationPeriodResponse>
{
    public async Task<Result<PersonnelFileLactationPeriodResponse>> Handle(
        UpdatePersonnelFileLactationPeriodCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageIncapacitiesAsync<PersonnelFileLactationPeriodResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entity = await lactationRepository.GetEntityAsync(personnelFile!.PublicId, command.LactationPeriodPublicId, cancellationToken);
        if (entity is null)
        {
            return Result<PersonnelFileLactationPeriodResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileLactationPeriodResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (entity.StatusCode == IncapacityStatuses.Anulada)
        {
            return Result<PersonnelFileLactationPeriodResponse>.Failure(LactationErrors.StateRuleViolation);
        }

        var item = command.Item;
        var tenantId = personnelFile.TenantId;

        // The type must remain the active LACTANCIA template.
        var typeInternalId = await lactationRepository.ResolveLactationTypeInternalIdAsync(
            tenantId, item.IncapacityTypePublicId, cancellationToken);
        if (typeInternalId is null)
        {
            return Result<PersonnelFileLactationPeriodResponse>.Failure(LactationErrors.TypeInvalid);
        }

        var scheduleValidation = LactationWriteSupport.ValidateSchedules(item.StartDate, item.EndDate, item.Schedules);
        if (scheduleValidation.IsFailure)
        {
            return Result<PersonnelFileLactationPeriodResponse>.Failure(scheduleValidation.Error);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // Replace the full set atomically: clear first so the period can move freely, then re-apply the
            // dates/notes and the validated schedules against the new range (the PUT reemplaza datos + horarios).
            entity.ReplaceSchedules([]);
            entity.UpdatePeriod(item.StartDate, item.EndDate, item.Notes);
            entity.ReplaceSchedules(LactationWriteSupport.ToDomainInputs(item.Schedules));

            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await lactationRepository.GetResponseAsync(personnelFile.PublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Lactation period response could not be resolved after update.");

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Updated a lactation period for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileLactationPeriodResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class AnnulPersonnelFileLactationPeriodCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileLactationRepository lactationRepository,
    IDateTimeProvider dateTimeProvider,
    ITenantContext tenantContext,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AnnulPersonnelFileLactationPeriodCommand, PersonnelFileLactationPeriodResponse>
{
    public async Task<Result<PersonnelFileLactationPeriodResponse>> Handle(
        AnnulPersonnelFileLactationPeriodCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageIncapacitiesAsync<PersonnelFileLactationPeriodResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entity = await lactationRepository.GetEntityAsync(personnelFile!.PublicId, command.LactationPeriodPublicId, cancellationToken);
        if (entity is null)
        {
            return Result<PersonnelFileLactationPeriodResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileLactationPeriodResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (!IncapacityStatuses.Annullable.Contains(entity.StatusCode))
        {
            return Result<PersonnelFileLactationPeriodResponse>.Failure(LactationErrors.StateRuleViolation);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            entity.Annul(command.Reason, dateTimeProvider.UtcNow);
            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await lactationRepository.GetResponseAsync(personnelFile.PublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Lactation period response could not be resolved after annulment.");

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Annulled a lactation period for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileLactationPeriodResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class GetPersonnelFileLactationPeriodsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileLactationRepository lactationRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileLactationPeriodsQuery, IReadOnlyCollection<PersonnelFileLactationPeriodResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileLactationPeriodResponse>>> Handle(
        GetPersonnelFileLactationPeriodsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForIncapacityReadAsync<IReadOnlyCollection<PersonnelFileLactationPeriodResponse>>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await lactationRepository.GetResponsesAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileLactationPeriodResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileLactationPeriodByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileLactationRepository lactationRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileLactationPeriodByIdQuery, PersonnelFileLactationPeriodResponse>
{
    public async Task<Result<PersonnelFileLactationPeriodResponse>> Handle(
        GetPersonnelFileLactationPeriodByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForIncapacityReadAsync<PersonnelFileLactationPeriodResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await lactationRepository.GetResponseAsync(personnelFile!.PublicId, query.LactationPeriodPublicId, cancellationToken);
        return response is null
            ? Result<PersonnelFileLactationPeriodResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<PersonnelFileLactationPeriodResponse>.Success(response);
    }
}

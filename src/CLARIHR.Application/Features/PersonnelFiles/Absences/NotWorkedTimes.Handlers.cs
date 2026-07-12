using System.Text.Json;
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

namespace CLARIHR.Application.Features.PersonnelFiles.Absences;

internal static class NotWorkedTimeMapping
{
    public static NotWorkedTimeResponse ToResponse(PersonnelFileNotWorkedTime entity) =>
        new(
            entity.PublicId,
            entity.AssignedPositionPublicId,
            entity.TypeCodeSnapshot,
            entity.TypeNameSnapshot,
            entity.UsesWorkSchedule,
            entity.DiscountPercentSnapshot,
            entity.DeductionConceptTypeCodeSnapshot,
            entity.IncomeConceptTypeCodeSnapshot,
            entity.StartDate,
            entity.EndDate,
            entity.Hours,
            entity.Reason,
            entity.OriginCode,
            entity.CalendarDays,
            entity.ComputableDays,
            entity.SeventhDayPenaltyDays,
            entity.DiscountedDays,
            entity.DailySalarySnapshot,
            entity.DiscountAmount,
            entity.CurrencyCode,
            entity.StatusCode,
            entity.RegisteredByUserId == Guid.Empty ? null : entity.RegisteredByUserId,
            entity.RegisteredUtc,
            entity.AnnulledByUserId,
            entity.AnnulledUtc,
            entity.AnnulmentReason,
            entity.ConcurrencyToken);
}

internal sealed class AddNotWorkedTimeCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    INotWorkedTimeTypeRepository typeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddNotWorkedTimeCommand, NotWorkedTimeResponse>
{
    public async Task<Result<NotWorkedTimeResponse>> Handle(
        AddNotWorkedTimeCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageNotWorkedTimesAsync<NotWorkedTimeResponse>(
            command.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (command.EndDate < command.StartDate)
        {
            return Result<NotWorkedTimeResponse>.Failure(NotWorkedTimeErrors.RangeInvalid);
        }

        // The TYPE is the master; the record only snapshots it.
        var types = await typeRepository.GetAsync(personnelFile!.TenantId, isActive: true, cancellationToken);
        var normalizedCode = command.TypeCode.Trim().ToUpperInvariant();
        var type = types.SingleOrDefault(item => item.NormalizedCode == normalizedCode);
        if (type is null)
        {
            return Result<NotWorkedTimeResponse>.Failure(NotWorkedTimeErrors.TypeInvalid);
        }

        // Hours belong to the types captured in hours, and ONLY to them: a two-hour "absence" of a day-based type
        // would silently be discounted as a whole day.
        if (type.UsesWorkSchedule && command.Hours is null)
        {
            return Result<NotWorkedTimeResponse>.Failure(NotWorkedTimeErrors.HoursRequired);
        }

        if (!type.UsesWorkSchedule && command.Hours is not null)
        {
            return Result<NotWorkedTimeResponse>.Failure(NotWorkedTimeErrors.HoursNotApplicable);
        }

        var context = await employeeRepository.GetNotWorkedTimeContextAsync(
            personnelFile.TenantId,
            personnelFile.Id,
            command.AssignedPositionPublicId,
            command.StartDate,
            command.EndDate,
            cancellationToken);
        if (!context.PlazaFound)
        {
            return Result<NotWorkedTimeResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        // The amount is NEVER typed by the user: the server computes it from the type's flags and the calendar.
        var calculation = NotWorkedTimeRules.Calculate(new NotWorkedTimeCalculationInput(
            command.StartDate,
            command.EndDate,
            type.CountsHoliday,
            type.CountsSaturday,
            type.CountsRestDay,
            type.CountsSeventhDayPenalty,
            type.UsesWorkSchedule,
            command.Hours,
            type.DiscountPercent,
            context.Holidays,
            context.RestDay,
            context.MonthlyBaseSalary,
            context.StandardDailyHours));

        _ = Guid.TryParse(currentUserService.UserId, out var registeredByUserId);
        var now = dateTimeProvider.UtcNow;

        var entity = PersonnelFileNotWorkedTime.Create(
            context.AssignedPositionPublicId,
            type.Code,
            type.Name,
            type.UsesWorkSchedule,
            type.CountsHoliday,
            type.CountsSaturday,
            type.CountsRestDay,
            type.CountsSeventhDayPenalty,
            type.DiscountPercent,
            type.DeductionConceptTypeCode,
            type.IncomeConceptTypeCode,
            command.StartDate,
            command.EndDate,
            command.Hours,
            command.Reason,
            NotWorkedTimeOrigins.Manual,
            calculation.CalendarDays,
            calculation.ComputableDays,
            calculation.SeventhDayPenaltyDays,
            calculation.DiscountedDays,
            calculation.DailySalary,
            calculation.DiscountAmount,
            context.CurrencyCode,
            JsonSerializer.Serialize(calculation.Details),
            registeredByUserId,
            now);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(personnelFile.TenantId);

        _ = await employeeRepository.AddNotWorkedTimeAsync(entity, cancellationToken);

        // The journal entry (P-20): a DEDICATED action type. PERMISO is reserved for the future permission-REQUEST
        // module — conflating a recorded absence with a requested permission would poison the dashboard for both.
        var action = PersonnelFilePersonnelAction.Create(
            "TIEMPO_NO_TRABAJADO",
            "APLICADA",
            actionDateUtc: now,
            effectiveFromUtc: command.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            effectiveToUtc: command.EndDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            description: $"Tiempo no trabajado ({type.Code}) {command.StartDate:yyyy-MM-dd} – {command.EndDate:yyyy-MM-dd}.",
            reference: null,
            amount: calculation.DiscountAmount == 0m ? null : calculation.DiscountAmount,
            currencyCode: calculation.DiscountAmount == 0m ? null : context.CurrencyCode,
            isSystemGenerated: true);
        action.BindToPersonnelFile(personnelFile.Id);
        action.SetTenantId(personnelFile.TenantId);
        _ = await employeeRepository.AddPersonnelActionAsync(action, cancellationToken);

        var response = NotWorkedTimeMapping.ToResponse(entity);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Registered not-worked time for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<NotWorkedTimeResponse>.Success(response);
    }
}

internal sealed class AnnulNotWorkedTimeCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AnnulNotWorkedTimeCommand, NotWorkedTimeResponse>
{
    public async Task<Result<NotWorkedTimeResponse>> Handle(
        AnnulNotWorkedTimeCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageNotWorkedTimesAsync<NotWorkedTimeResponse>(
            command.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entity = await employeeRepository.GetNotWorkedTimeEntityAsync(
            personnelFile!.TenantId, personnelFile.PublicId, command.NotWorkedTimePublicId, cancellationToken);
        if (entity is null)
        {
            return Result<NotWorkedTimeResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<NotWorkedTimeResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (entity.IsAnnulled)
        {
            return Result<NotWorkedTimeResponse>.Failure(NotWorkedTimeErrors.AlreadyAnnulled);
        }

        _ = Guid.TryParse(currentUserService.UserId, out var byUserId);
        entity.Annul(command.Reason, byUserId, dateTimeProvider.UtcNow);

        var response = NotWorkedTimeMapping.ToResponse(entity);
        TouchPersonnelFile(personnelFile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Annulled not-worked time for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        return Result<NotWorkedTimeResponse>.Success(response);
    }
}

internal sealed class GetNotWorkedTimesQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeCommandHandlerBase,
      IQueryHandler<GetNotWorkedTimesQuery, IReadOnlyCollection<NotWorkedTimeResponse>>
{
    public async Task<Result<IReadOnlyCollection<NotWorkedTimeResponse>>> Handle(
        GetNotWorkedTimesQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForViewNotWorkedTimesAsync<IReadOnlyCollection<NotWorkedTimeResponse>>(
            query.PersonnelFileId, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var items = await employeeRepository.GetNotWorkedTimesAsync(
            personnelFile!.TenantId, personnelFile.PublicId, cancellationToken);

        return Result<IReadOnlyCollection<NotWorkedTimeResponse>>.Success(
            items.Select(NotWorkedTimeMapping.ToResponse).ToArray());
    }
}

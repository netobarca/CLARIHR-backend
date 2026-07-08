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
using CLARIHR.Domain.Preferences;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Shared helpers for the vacation-requests vertical (leave module §3.4/§3.7): the Art. 178 date preferences,
/// the primary-plaza rest day resolution, the per-period availability projection and the personnel-action
/// journal entries (GOCE_VACACIONES / DEVOLUCION_VACACIONES) written in the same transaction as the event.
/// </summary>
internal static class VacationRequestSupport
{
    public const string GoceActionType = "GOCE_VACACIONES";
    public const string DevolucionActionType = "DEVOLUCION_VACACIONES";

    public static VacationDateRulePreferences BuildDatePreferences(CompanyPreference? preference) =>
        new(
            AllowStartOnHoliday: preference?.AllowVacationStartOnHoliday ?? false,
            AllowEndOnHoliday: preference?.AllowVacationEndOnHoliday ?? true,
            AllowStartOnRestDay: preference?.AllowVacationStartOnRestDay ?? false);

    /// <summary>Rest day of the employee: the primary plaza's rest day, else the company preference, else Sunday (Art. 178).</summary>
    public static DayOfWeek ResolveRestDay(DayOfWeek? plazaRestDay, int? companyRestDayOfWeek)
    {
        if (plazaRestDay is { } plaza)
        {
            return plaza;
        }

        return companyRestDayOfWeek is { } day && day is >= 0 and <= 6 ? (DayOfWeek)day : DayOfWeek.Sunday;
    }

    /// <summary>Per fund period availability (granted − net consumed) for the enjoyment periods only (FIFO / sufficiency).</summary>
    public static IReadOnlyList<VacationPeriodAvailability> ToAvailabilities(
        IEnumerable<VacationPeriodConsumptionRow> consumptions) =>
        consumptions
            .Where(row => row.GeneratesEnjoymentDays)
            .Select(row => new VacationPeriodAvailability(
                row.PeriodId,
                row.PeriodYear,
                Math.Max(0, row.LegalDaysGranted + row.BenefitDaysGranted - row.NetConsumedDays)))
            .ToArray();

    public static async Task AddJournalAsync(
        IPersonnelFileEmployeeRepository employeeRepository,
        long personnelFileId,
        Guid tenantId,
        string actionTypeCode,
        PersonnelFileVacationRequest request,
        int days,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var action = PersonnelFilePersonnelAction.Create(
            actionTypeCode,
            "APLICADA",
            actionDateUtc: nowUtc,
            effectiveFromUtc: request.StartDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            effectiveToUtc: request.EndDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
            description: $"{actionTypeCode} {days} día(s) {request.StartDate:yyyy-MM-dd}…{request.EndDate:yyyy-MM-dd}.",
            reference: null,
            amount: null,
            currencyCode: null,
            isSystemGenerated: true);
        action.BindToPersonnelFile(personnelFileId);
        action.SetTenantId(tenantId);
        _ = await employeeRepository.AddPersonnelActionAsync(action, cancellationToken);
    }
}

internal sealed class AddPersonnelFileVacationRequestCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileVacationRepository vacationRepository,
    IPersonnelFileIncapacityRepository incapacityRepository,
    ICompanyPreferenceRepository companyPreferenceRepository,
    ICurrentUserService currentUserService,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFileVacationRequestCommand, PersonnelFileVacationRequestResponse>
{
    public async Task<Result<PersonnelFileVacationRequestResponse>> Handle(
        AddPersonnelFileVacationRequestCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile, _) = await LoadForCreateOwnOrManageVacationRequestAsync<PersonnelFileVacationRequestResponse>(
            command.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (!personnelFile!.IsCompletedEmployee)
        {
            return Result<PersonnelFileVacationRequestResponse>.Failure(PersonnelFileErrors.StateRuleViolation);
        }

        var item = command.Item;
        var tenantId = personnelFile.TenantId;

        // Art. 178 date rules (RN-27): 422 on the request (warnings on the annual plan, PR-9).
        var preference = await companyPreferenceRepository.GetByTenantIdAsync(tenantId, cancellationToken);
        var holidays = await vacationRepository.GetHolidaysInRangeAsync(tenantId, item.StartDate, item.EndDate, cancellationToken);
        var plazaRestDay = await vacationRepository.GetPrimaryPlazaRestDayAsync(personnelFile.Id, cancellationToken);
        var restDay = VacationRequestSupport.ResolveRestDay(plazaRestDay, preference?.CompanyRestDayOfWeek);
        var violations = VacationRules.ValidateRequestDates(
            item.StartDate, item.EndDate, holidays, restDay, VacationRequestSupport.BuildDatePreferences(preference));
        if (violations.Count > 0)
        {
            return Result<PersonnelFileVacationRequestResponse>.Failure(VacationErrors.ForDateViolation(violations[0]));
        }

        // Overlap with a live vacation request (RN-15) / an active incapacity (RN-16).
        if (await vacationRepository.HasOverlappingRequestAsync(personnelFile.Id, item.StartDate, item.EndDate, excludeRequestId: null, cancellationToken))
        {
            return Result<PersonnelFileVacationRequestResponse>.Failure(VacationErrors.RequestOverlap);
        }

        if (await incapacityRepository.HasOverlappingIncapacityAsync(personnelFile.Id, item.StartDate, item.EndDate, excludeIncapacityId: null, cancellationToken))
        {
            return Result<PersonnelFileVacationRequestResponse>.Failure(VacationErrors.IncapacityOverlap);
        }

        // Fund availability at creation time (RN-10): the requested days must fit the enjoyment fund.
        var consumptions = await vacationRepository.GetActivePeriodConsumptionsAsync(personnelFile.Id, cancellationToken);
        var available = VacationRequestSupport.ToAvailabilities(consumptions).Sum(period => period.AvailableDays);
        if (available < item.RequestedDays)
        {
            return Result<PersonnelFileVacationRequestResponse>.Failure(VacationErrors.FundInsufficient);
        }

        var entity = PersonnelFileVacationRequest.Create(
            requesterFilePublicId: personnelFile.PublicId,
            requesterNameSnapshot: personnelFile.FullName,
            requestedByUserId: currentUserService.UserId ?? string.Empty,
            startDate: item.StartDate,
            endDate: item.EndDate,
            requestedDays: item.RequestedDays,
            planLinePublicId: item.PlanLinePublicId,
            notes: item.Notes);
        entity.BindToPersonnelFile(personnelFile.Id);
        entity.SetTenantId(tenantId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            vacationRepository.AddRequest(entity);
            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await vacationRepository.GetRequestResponseAsync(personnelFile.PublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Vacation request response could not be resolved after creation.");

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Registered a vacation request for {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileVacationRequestResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class DecidePersonnelFileVacationRequestCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IPersonnelFileVacationRepository vacationRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<DecidePersonnelFileVacationRequestCommand, PersonnelFileVacationRequestResponse>
{
    public async Task<Result<PersonnelFileVacationRequestResponse>> Handle(
        DecidePersonnelFileVacationRequestCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageVacationsAsync<PersonnelFileVacationRequestResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        // Anti-self (RN-17): the employee cannot decide a request on their own file.
        _ = Guid.TryParse(currentUserService.UserId, out var actingUserId);
        if (personnelFile!.LinkedUserPublicId is { } subjectUserId && subjectUserId == actingUserId)
        {
            return Result<PersonnelFileVacationRequestResponse>.Failure(VacationErrors.DecisionSelfForbidden);
        }

        var entity = await vacationRepository.GetRequestEntityAsync(personnelFile.PublicId, command.VacationRequestPublicId, cancellationToken);
        if (entity is null)
        {
            return Result<PersonnelFileVacationRequestResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileVacationRequestResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (!VacationRequestStatuses.DecisionTargets.Contains(entity.StatusCode))
        {
            return Result<PersonnelFileVacationRequestResponse>.Failure(VacationErrors.StateRuleViolation);
        }

        var tenantId = personnelFile.TenantId;
        var actingBy = currentUserService.UserId ?? string.Empty;
        var nowUtc = dateTimeProvider.UtcNow;

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            if (!command.Item.Approve)
            {
                entity.Reject(actingBy, nowUtc, command.Item.Notes);
            }
            else
            {
                // Re-verify the fund INSIDE the transaction (race → 422 VACATION_FUND_INSUFFICIENT).
                var consumptions = await vacationRepository.GetActivePeriodConsumptionsAsync(personnelFile.Id, cancellationToken);
                var availabilities = VacationRequestSupport.ToAvailabilities(consumptions);

                var (allocations, allocationError) = await ResolveAllocationsAsync(
                    personnelFile.Id, entity.RequestedDays, command.Item.Allocations, availabilities, cancellationToken);
                if (allocationError is not null)
                {
                    return Result<PersonnelFileVacationRequestResponse>.Failure(allocationError);
                }

                entity.Approve(allocations!, actingBy, nowUtc);
                await VacationRequestSupport.AddJournalAsync(
                    employeeRepository, personnelFile.Id, tenantId, VacationRequestSupport.GoceActionType,
                    entity, entity.ConsumedDays, nowUtc, cancellationToken);
            }

            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await vacationRepository.GetRequestResponseAsync(personnelFile.PublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Vacation request response could not be resolved after the decision.");

            var summary = command.Item.Approve
                ? $"Approved a vacation request of {personnelFile.FullName}."
                : $"Rejected a vacation request of {personnelFile.FullName}.";
            await PersonnelFileEmployeeAudits.LogUpdateAsync(auditService, personnelFile, summary, response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileVacationRequestResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<(IReadOnlyCollection<VacationAllocationInput>? Allocations, Error? Error)> ResolveAllocationsAsync(
        long personnelFileId,
        int requestedDays,
        IReadOnlyCollection<VacationAllocationItem>? provided,
        IReadOnlyList<VacationPeriodAvailability> availabilities,
        CancellationToken cancellationToken)
    {
        var availableByPeriod = availabilities.ToDictionary(period => period.PeriodId, period => period.AvailableDays);

        if (provided is { Count: > 0 })
        {
            var publicIds = provided.Select(item => item.VacationPeriodPublicId).ToArray();
            var internalIds = await vacationRepository.ResolveEnjoymentPeriodInternalIdsAsync(personnelFileId, publicIds, cancellationToken);

            var requestedByPeriod = new Dictionary<long, int>();
            foreach (var allocation in provided)
            {
                if (!internalIds.TryGetValue(allocation.VacationPeriodPublicId, out var periodId))
                {
                    return (null, VacationErrors.AllocationMismatch);
                }

                requestedByPeriod[periodId] = requestedByPeriod.GetValueOrDefault(periodId) + allocation.Days;
            }

            if (requestedByPeriod.Values.Sum() != requestedDays)
            {
                return (null, VacationErrors.AllocationMismatch);
            }

            foreach (var (periodId, days) in requestedByPeriod)
            {
                if (days > availableByPeriod.GetValueOrDefault(periodId))
                {
                    return (null, VacationErrors.FundInsufficient);
                }
            }

            return (requestedByPeriod.Select(pair => new VacationAllocationInput(pair.Key, pair.Value)).ToArray(), null);
        }

        // FIFO suggestion (default): consume the oldest period with a balance first.
        if (availabilities.Sum(period => period.AvailableDays) < requestedDays)
        {
            return (null, VacationErrors.FundInsufficient);
        }

        var suggested = VacationRules.SuggestFifoAllocations(availabilities, requestedDays);
        if (suggested.Sum(allocation => allocation.Days) != requestedDays)
        {
            return (null, VacationErrors.FundInsufficient);
        }

        return (suggested, null);
    }
}

internal sealed class CancelPersonnelFileVacationRequestCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileVacationRepository vacationRepository,
    ICurrentUserService currentUserService,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<CancelPersonnelFileVacationRequestCommand, PersonnelFileVacationRequestResponse>
{
    public async Task<Result<PersonnelFileVacationRequestResponse>> Handle(
        CancelPersonnelFileVacationRequestCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile, _) = await LoadForCreateOwnOrManageVacationRequestAsync<PersonnelFileVacationRequestResponse>(
            command.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var entity = await vacationRepository.GetRequestEntityAsync(personnelFile!.PublicId, command.VacationRequestPublicId, cancellationToken);
        if (entity is null)
        {
            return Result<PersonnelFileVacationRequestResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileVacationRequestResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (!VacationRequestStatuses.Cancellable.Contains(entity.StatusCode))
        {
            return Result<PersonnelFileVacationRequestResponse>.Failure(VacationErrors.StateRuleViolation);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            entity.Cancel();
            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await vacationRepository.GetRequestResponseAsync(personnelFile.PublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Vacation request response could not be resolved after cancellation.");

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Cancelled a vacation request of {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileVacationRequestResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class AddPersonnelFileVacationReturnCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileEmployeeRepository employeeRepository,
    IPersonnelFileVacationRepository vacationRepository,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork)
    : PersonnelFileEmployeeCommandHandlerBase,
      ICommandHandler<AddPersonnelFileVacationReturnCommand, PersonnelFileVacationRequestResponse>
{
    public async Task<Result<PersonnelFileVacationRequestResponse>> Handle(
        AddPersonnelFileVacationReturnCommand command,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadForManageVacationsAsync<PersonnelFileVacationRequestResponse>(
            command.PersonnelFileId, Guid.Empty, tenantContext, authorizationService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        // Anti-self (RN-17): the employee cannot return a request on their own file.
        _ = Guid.TryParse(currentUserService.UserId, out var actingUserId);
        if (personnelFile!.LinkedUserPublicId is { } subjectUserId && subjectUserId == actingUserId)
        {
            return Result<PersonnelFileVacationRequestResponse>.Failure(VacationErrors.DecisionSelfForbidden);
        }

        var entity = await vacationRepository.GetRequestEntityAsync(personnelFile.PublicId, command.VacationRequestPublicId, cancellationToken);
        if (entity is null)
        {
            return Result<PersonnelFileVacationRequestResponse>.Failure(PersonnelFileErrors.ItemNotFound);
        }

        if (entity.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PersonnelFileVacationRequestResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (!VacationRequestStatuses.Returnable.Contains(entity.StatusCode))
        {
            return Result<PersonnelFileVacationRequestResponse>.Failure(VacationErrors.StateRuleViolation);
        }

        var days = command.Item.Days;
        if (days > entity.NetConsumedDays)
        {
            return Result<PersonnelFileVacationRequestResponse>.Failure(VacationErrors.ReturnExceedsConsumed);
        }

        // Net outstanding per period (allocations − prior returns), in allocation order (for LIFO).
        var netByPeriod = VacationFundMath.NetConsumedByPeriod(
            entity.Allocations.Select(allocation => (allocation.VacationPeriodId, allocation.Days)),
            entity.Returns.Select(entry => entry.DistributionJson));

        var (distribution, distributionError) = await ResolveDistributionAsync(
            personnelFile.Id, entity, days, netByPeriod, command.Item.Distribution, cancellationToken);
        if (distributionError is not null)
        {
            return Result<PersonnelFileVacationRequestResponse>.Failure(distributionError);
        }

        var tenantId = personnelFile.TenantId;
        var actingBy = currentUserService.UserId ?? string.Empty;
        var nowUtc = dateTimeProvider.UtcNow;

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            entity.Return(days, command.Item.Reason, actingBy, nowUtc, distribution!);
            await VacationRequestSupport.AddJournalAsync(
                employeeRepository, personnelFile.Id, tenantId, VacationRequestSupport.DevolucionActionType,
                entity, days, nowUtc, cancellationToken);

            TouchPersonnelFile(personnelFile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await vacationRepository.GetRequestResponseAsync(personnelFile.PublicId, entity.PublicId, cancellationToken)
                ?? throw new InvalidOperationException("Vacation request response could not be resolved after the return.");

            await PersonnelFileEmployeeAudits.LogUpdateAsync(
                auditService, personnelFile, $"Returned {days} vacation day(s) of {personnelFile.FullName}.", response, cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<PersonnelFileVacationRequestResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<(IReadOnlyCollection<VacationReturnDistributionInput>? Distribution, Error? Error)> ResolveDistributionAsync(
        long personnelFileId,
        PersonnelFileVacationRequest entity,
        int days,
        IReadOnlyDictionary<long, int> netByPeriod,
        IReadOnlyCollection<VacationReturnDistributionItem>? provided,
        CancellationToken cancellationToken)
    {
        if (provided is { Count: > 0 })
        {
            var publicIds = provided.Select(item => item.VacationPeriodPublicId).ToArray();
            var internalIds = await vacationRepository.ResolveEnjoymentPeriodInternalIdsAsync(personnelFileId, publicIds, cancellationToken);

            var byPeriod = new Dictionary<long, int>();
            foreach (var entry in provided)
            {
                if (!internalIds.TryGetValue(entry.VacationPeriodPublicId, out var periodId)
                    || !netByPeriod.ContainsKey(periodId))
                {
                    return (null, VacationErrors.AllocationMismatch);
                }

                byPeriod[periodId] = byPeriod.GetValueOrDefault(periodId) + entry.Days;
            }

            if (byPeriod.Values.Sum() != days)
            {
                return (null, VacationErrors.AllocationMismatch);
            }

            foreach (var (periodId, entryDays) in byPeriod)
            {
                if (entryDays > Math.Max(0, netByPeriod.GetValueOrDefault(periodId)))
                {
                    return (null, VacationErrors.ReturnExceedsConsumed);
                }
            }

            return (byPeriod.Select(pair => new VacationReturnDistributionInput(pair.Key, pair.Value)).ToArray(), null);
        }

        // LIFO suggestion (default): undo the most recent allocation first.
        var outstanding = entity.Allocations
            .Select(allocation => allocation.VacationPeriodId)
            .Distinct()
            .Select(periodId => new VacationPeriodOutstanding(periodId, Math.Max(0, netByPeriod.GetValueOrDefault(periodId))))
            .ToArray();

        var suggested = VacationRules.SuggestLifoReturn(outstanding, days);
        if (suggested.Sum(entry => entry.Days) != days)
        {
            return (null, VacationErrors.ReturnExceedsConsumed);
        }

        return (suggested, null);
    }
}

internal sealed class GetPersonnelFileVacationRequestsQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileVacationRepository vacationRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileVacationRequestsQuery, IReadOnlyCollection<PersonnelFileVacationRequestResponse>>
{
    public async Task<Result<IReadOnlyCollection<PersonnelFileVacationRequestResponse>>> Handle(
        GetPersonnelFileVacationRequestsQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForVacationReadAsync<IReadOnlyCollection<PersonnelFileVacationRequestResponse>>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await vacationRepository.GetRequestResponsesAsync(personnelFile!.PublicId, cancellationToken);
        return Result<IReadOnlyCollection<PersonnelFileVacationRequestResponse>>.Success(response);
    }
}

internal sealed class GetPersonnelFileVacationRequestByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileRepository personnelFileRepository,
    IPersonnelFileVacationRepository vacationRepository,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext)
    : PersonnelFileEmployeeReadQueryHandlerBase,
      IQueryHandler<GetPersonnelFileVacationRequestByIdQuery, PersonnelFileVacationRequestResponse>
{
    public async Task<Result<PersonnelFileVacationRequestResponse>> Handle(
        GetPersonnelFileVacationRequestByIdQuery query,
        CancellationToken cancellationToken)
    {
        var (failure, personnelFile) = await LoadCompletedEmployeeForVacationReadAsync<PersonnelFileVacationRequestResponse>(
            query.PersonnelFileId, tenantContext, authorizationService, currentUserService, personnelFileRepository, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var response = await vacationRepository.GetRequestResponseAsync(personnelFile!.PublicId, query.VacationRequestPublicId, cancellationToken);
        return response is null
            ? Result<PersonnelFileVacationRequestResponse>.Failure(PersonnelFileErrors.ItemNotFound)
            : Result<PersonnelFileVacationRequestResponse>.Success(response);
    }
}

using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Localization;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Preferences;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Domain.Leave;
using CLARIHR.Domain.Preferences;

namespace CLARIHR.Application.Features.PersonnelFiles;

/// <summary>
/// Shared helpers for the annual vacation-plan vertical (leave module §3.7): translation of the raw line items
/// to the domain input and the per-line warning computation (availability of the employee's fund, holiday /
/// rest-day findings — non-blocking, aclaración №9).
/// </summary>
internal static class VacationPlanSupport
{
    public static IReadOnlyCollection<VacationPlanLineInput> ToDomainInputs(IEnumerable<VacationPlanLineItem> lines) =>
        lines.Select(line => new VacationPlanLineInput(line.PersonnelFilePublicId, line.StartDate, line.EndDate, line.Days)).ToArray();

    /// <summary>
    /// Computes the per-line warnings of a plan (keyed by line public id): a DATE_RULE warning when the window
    /// violates the Art. 178 defaults, and an INSUFFICIENT_FUND warning when the cumulative planned days of the
    /// employee exceed the days available in their fund. Both are indicative — they never block (D-24).
    /// </summary>
    public static Dictionary<Guid, IReadOnlyList<VacationPlanLineWarning>> ComputeWarnings(
        VacationPlan plan,
        CompanyPreference? preference,
        IReadOnlySet<DateOnly> holidays,
        IReadOnlyDictionary<Guid, VacationPlanEmployeeContext> contexts,
        IBackendMessageLocalizer localizer)
    {
        var result = new Dictionary<Guid, IReadOnlyList<VacationPlanLineWarning>>();
        var datePreferences = VacationRequestSupport.BuildDatePreferences(preference);
        var cumulativeByEmployee = new Dictionary<Guid, int>();

        foreach (var line in plan.Lines.OrderBy(item => item.PersonnelFilePublicId).ThenBy(item => item.StartDate))
        {
            var warnings = new List<VacationPlanLineWarning>();
            contexts.TryGetValue(line.PersonnelFilePublicId, out var context);

            var restDay = VacationRequestSupport.ResolveRestDay(context?.PlazaRestDay, preference?.CompanyRestDayOfWeek);
            var violations = VacationRules.ValidateRequestDates(line.StartDate, line.EndDate, holidays, restDay, datePreferences);
            if (violations.Count > 0)
            {
                warnings.Add(new VacationPlanLineWarning(
                    VacationPlanWarnings.DateRuleCode,
                    localizer.Localize(VacationPlanWarnings.DateRuleCode, VacationPlanWarnings.DateRuleDefaultMessage)));
            }

            var cumulative = cumulativeByEmployee.GetValueOrDefault(line.PersonnelFilePublicId) + line.Days;
            cumulativeByEmployee[line.PersonnelFilePublicId] = cumulative;
            if (cumulative > (context?.AvailableFundDays ?? 0))
            {
                warnings.Add(new VacationPlanLineWarning(
                    VacationPlanWarnings.InsufficientFundCode,
                    localizer.Localize(VacationPlanWarnings.InsufficientFundCode, VacationPlanWarnings.InsufficientFundDefaultMessage)));
            }

            result[line.PublicId] = warnings;
        }

        return result;
    }
}

internal sealed class AddVacationPlanCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileVacationRepository vacationRepository,
    ICompanyPreferenceRepository companyPreferenceRepository,
    IBackendMessageLocalizer localizer,
    ICurrentUserService currentUserService,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AddVacationPlanCommand, VacationPlanResponse>
{
    public async Task<Result<VacationPlanResponse>> Handle(AddVacationPlanCommand command, CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageVacationsAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<VacationPlanResponse>.Failure(authorizationResult.Error);
        }

        var tenantId = command.CompanyId;

        if (VacationPlanRules.HasOverlappingLines(command.Lines))
        {
            return Result<VacationPlanResponse>.Failure(VacationErrors.PlanLineOverlap);
        }

        var distinctEmployees = command.Lines.Select(line => line.PersonnelFilePublicId).Distinct().ToArray();
        var contexts = await vacationRepository.GetPlanEmployeeContextsAsync(tenantId, distinctEmployees, cancellationToken);
        if (distinctEmployees.Any(id => !contexts.ContainsKey(id)))
        {
            return Result<VacationPlanResponse>.Failure(VacationErrors.PlanEmployeeInvalid);
        }

        var plan = VacationPlan.Create(
            command.PlanYear,
            DateOnly.FromDateTime(dateTimeProvider.UtcNow),
            currentUserService.UserId ?? string.Empty,
            requesterNameSnapshot: null);
        plan.ReplaceLines(VacationPlanSupport.ToDomainInputs(command.Lines));
        plan.SetTenantId(tenantId);

        var preference = await companyPreferenceRepository.GetByTenantIdAsync(tenantId, cancellationToken);
        var holidays = await ResolveHolidaysAsync(tenantId, plan, cancellationToken);
        var warnings = VacationPlanSupport.ComputeWarnings(plan, preference, holidays, contexts, localizer);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            vacationRepository.AddPlan(plan);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = VacationPlanMapping.Map(plan, warnings);
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.VacationPlanSaved,
                    AuditEntityTypes.PersonnelFile,
                    command.CompanyId,
                    $"VACATION_PLAN_{plan.PlanYear}_{plan.PublicId}",
                    AuditActions.Create,
                    $"Created the {plan.PlanYear} vacation plan with {plan.Lines.Count} line(s).",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<VacationPlanResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<IReadOnlySet<DateOnly>> ResolveHolidaysAsync(Guid tenantId, VacationPlan plan, CancellationToken cancellationToken)
    {
        if (plan.Lines.Count == 0)
        {
            return new HashSet<DateOnly>();
        }

        var minStart = plan.Lines.Min(line => line.StartDate);
        var maxEnd = plan.Lines.Max(line => line.EndDate);
        return await vacationRepository.GetHolidaysInRangeAsync(tenantId, minStart, maxEnd, cancellationToken);
    }
}

internal sealed class UpdateVacationPlanCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileVacationRepository vacationRepository,
    ICompanyPreferenceRepository companyPreferenceRepository,
    IBackendMessageLocalizer localizer,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateVacationPlanCommand, VacationPlanResponse>
{
    public async Task<Result<VacationPlanResponse>> Handle(UpdateVacationPlanCommand command, CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageVacationsAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<VacationPlanResponse>.Failure(authorizationResult.Error);
        }

        var tenantId = command.CompanyId;

        if (VacationPlanRules.HasOverlappingLines(command.Lines))
        {
            return Result<VacationPlanResponse>.Failure(VacationErrors.PlanLineOverlap);
        }

        var plan = await vacationRepository.GetPlanEntityAsync(tenantId, command.VacationPlanPublicId, cancellationToken);
        if (plan is null)
        {
            return Result<VacationPlanResponse>.Failure(VacationErrors.PlanNotFound);
        }

        if (plan.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<VacationPlanResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (plan.StatusCode != VacationPlanStatuses.Vigente)
        {
            return Result<VacationPlanResponse>.Failure(VacationErrors.PlanStateRuleViolation);
        }

        var distinctEmployees = command.Lines.Select(line => line.PersonnelFilePublicId).Distinct().ToArray();
        var contexts = await vacationRepository.GetPlanEmployeeContextsAsync(tenantId, distinctEmployees, cancellationToken);
        if (distinctEmployees.Any(id => !contexts.ContainsKey(id)))
        {
            return Result<VacationPlanResponse>.Failure(VacationErrors.PlanEmployeeInvalid);
        }

        plan.ReplaceLines(VacationPlanSupport.ToDomainInputs(command.Lines));

        var preference = await companyPreferenceRepository.GetByTenantIdAsync(tenantId, cancellationToken);
        var holidays = plan.Lines.Count == 0
            ? new HashSet<DateOnly>()
            : await vacationRepository.GetHolidaysInRangeAsync(
                tenantId, plan.Lines.Min(line => line.StartDate), plan.Lines.Max(line => line.EndDate), cancellationToken);
        var warnings = VacationPlanSupport.ComputeWarnings(plan, preference, holidays, contexts, localizer);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = VacationPlanMapping.Map(plan, warnings);
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.VacationPlanSaved,
                    AuditEntityTypes.PersonnelFile,
                    command.CompanyId,
                    $"VACATION_PLAN_{plan.PlanYear}_{plan.PublicId}",
                    AuditActions.Update,
                    $"Updated the {plan.PlanYear} vacation plan ({plan.Lines.Count} line(s)).",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<VacationPlanResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class AnnulVacationPlanCommandHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileVacationRepository vacationRepository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<AnnulVacationPlanCommand, VacationPlanResponse>
{
    public async Task<Result<VacationPlanResponse>> Handle(AnnulVacationPlanCommand command, CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageVacationsAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<VacationPlanResponse>.Failure(authorizationResult.Error);
        }

        var plan = await vacationRepository.GetPlanEntityAsync(command.CompanyId, command.VacationPlanPublicId, cancellationToken);
        if (plan is null)
        {
            return Result<VacationPlanResponse>.Failure(VacationErrors.PlanNotFound);
        }

        if (plan.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<VacationPlanResponse>.Failure(PersonnelFileErrors.ConcurrencyConflict);
        }

        if (plan.StatusCode != VacationPlanStatuses.Vigente)
        {
            return Result<VacationPlanResponse>.Failure(VacationErrors.PlanStateRuleViolation);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            plan.Annul();
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = VacationPlanMapping.Map(plan);
            await auditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.VacationPlanSaved,
                    AuditEntityTypes.PersonnelFile,
                    command.CompanyId,
                    $"VACATION_PLAN_{plan.PlanYear}_{plan.PublicId}",
                    AuditActions.Deactivate,
                    $"Annulled the {plan.PlanYear} vacation plan.",
                    After: response),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<VacationPlanResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class GetVacationPlansQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileVacationRepository vacationRepository)
    : IQueryHandler<GetVacationPlansQuery, IReadOnlyCollection<VacationPlanResponse>>
{
    public async Task<Result<IReadOnlyCollection<VacationPlanResponse>>> Handle(
        GetVacationPlansQuery query, CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewVacationsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IReadOnlyCollection<VacationPlanResponse>>.Failure(authorizationResult.Error);
        }

        var plans = await vacationRepository.GetPlanResponsesAsync(query.CompanyId, query.Year, cancellationToken);
        return Result<IReadOnlyCollection<VacationPlanResponse>>.Success(plans);
    }
}

internal sealed class GetVacationPlanByIdQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPersonnelFileVacationRepository vacationRepository)
    : IQueryHandler<GetVacationPlanByIdQuery, VacationPlanResponse>
{
    public async Task<Result<VacationPlanResponse>> Handle(
        GetVacationPlanByIdQuery query, CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewVacationsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<VacationPlanResponse>.Failure(authorizationResult.Error);
        }

        var plan = await vacationRepository.GetPlanResponseAsync(query.CompanyId, query.VacationPlanPublicId, cancellationToken);
        return plan is null
            ? Result<VacationPlanResponse>.Failure(VacationErrors.PlanNotFound)
            : Result<VacationPlanResponse>.Success(plan);
    }
}

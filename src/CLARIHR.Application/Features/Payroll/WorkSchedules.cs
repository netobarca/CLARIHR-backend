using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Payroll.Common;
using CLARIHR.Domain.Payroll;
using FluentValidation;

namespace CLARIHR.Application.Features.Payroll;

/// <summary>One weekday of a work schedule as it travels on the wire (times as HH:mm).</summary>
public sealed record WorkScheduleDayResponse(
    int DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    TimeOnly? MealStart,
    TimeOnly? MealEnd,
    decimal NetHours);

public sealed record WorkScheduleDayInputModel(
    int DayOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    TimeOnly? MealStart = null,
    TimeOnly? MealEnd = null);

public sealed record WorkScheduleListItemResponse(
    Guid Id,
    string Code,
    string Name,
    string? ScheduleLabel,
    string AttendanceDateAnchor,
    string ScheduleClass,
    decimal TotalWeeklyHours,
    int DaysCount,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

public sealed record WorkScheduleResponse(
    Guid Id,
    string Code,
    string Name,
    string? ScheduleLabel,
    string AttendanceDateAnchor,
    string ScheduleClass,
    decimal TotalWeeklyHours,
    IReadOnlyCollection<WorkScheduleDayResponse> Days,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

public sealed record SearchWorkSchedulesQuery(
    Guid CompanyId,
    bool? IsActive,
    string? Search,
    int PageNumber = 1,
    int PageSize = PayrollConfigurationValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false)
    : IQuery<PagedResponse<WorkScheduleListItemResponse>>;

public sealed record GetWorkScheduleByIdQuery(Guid WorkScheduleId)
    : IQuery<WorkScheduleResponse>;

public sealed record CreateWorkScheduleCommand(
    Guid CompanyId,
    string Code,
    string Name,
    string? ScheduleLabel,
    string AttendanceDateAnchor,
    string ScheduleClass,
    decimal? TotalWeeklyHours,
    IReadOnlyCollection<WorkScheduleDayInputModel> Days)
    : ICommand<WorkScheduleResponse>;

public sealed record UpdateWorkScheduleCommand(
    Guid WorkScheduleId,
    string Code,
    string Name,
    string? ScheduleLabel,
    string AttendanceDateAnchor,
    string ScheduleClass,
    decimal? TotalWeeklyHours,
    IReadOnlyCollection<WorkScheduleDayInputModel> Days,
    Guid ConcurrencyToken)
    : ICommand<WorkScheduleResponse>;

public sealed record ActivateWorkScheduleCommand(Guid WorkScheduleId, Guid ConcurrencyToken)
    : ICommand<WorkScheduleResponse>;

public sealed record InactivateWorkScheduleCommand(Guid WorkScheduleId, Guid ConcurrencyToken)
    : ICommand<WorkScheduleResponse>;

public static class WorkScheduleErrors
{
    public static readonly Error WorkScheduleNotFound = new(
        "WORK_SCHEDULE_NOT_FOUND",
        "The work schedule could not be found.",
        ErrorType.NotFound);

    // The upfront duplicate probe returns this; a concurrent writer that trips the filtered unique index is
    // mapped to the same code (see WorkScheduleConstraintViolations) — REQ-012 §5.
    public static readonly Error CodeTaken = new(
        "WORK_SCHEDULE_CODE_TAKEN",
        "Another active work schedule already uses the requested code.",
        ErrorType.Conflict);

    /// <summary>The day set violates a schedule rule (duplicated weekday, bad meal break, zero shift…).</summary>
    public static readonly Error DayInvalid = new(
        "WORK_SCHEDULE_DAY_INVALID",
        "The work schedule days are not valid (weekday, shift times or meal break).",
        ErrorType.UnprocessableEntity);

    public static readonly Error InUse = new(
        "WORK_SCHEDULE_IN_USE",
        "The work schedule is referenced by an active employment assignment and cannot be inactivated.",
        ErrorType.UnprocessableEntity);

    public static readonly Error ConcurrencyConflict = new(
        "CONCURRENCY_CONFLICT",
        "The resource was modified by another request. Refresh and try again.",
        ErrorType.Conflict);

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch(PayrollConfigurationPermissionCodes.WorkSchedulesResourceKey, action);
}

internal sealed class SearchWorkSchedulesQueryValidator : AbstractValidator<SearchWorkSchedulesQuery>
{
    public SearchWorkSchedulesQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.Search)
            .MaximumLength(150)
            .Must(PayrollConfigurationValidationRules.IsValidSearchLength)
            .WithMessage($"Search must be at least {PayrollConfigurationValidationRules.MinSearchLength} characters when provided.");
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PayrollConfigurationValidationRules.MaxPageSize);
    }
}

internal sealed class GetWorkScheduleByIdQueryValidator : AbstractValidator<GetWorkScheduleByIdQuery>
{
    public GetWorkScheduleByIdQueryValidator()
    {
        RuleFor(query => query.WorkScheduleId).NotEmpty();
    }
}

internal sealed class WorkScheduleDayInputModelValidator : AbstractValidator<WorkScheduleDayInputModel>
{
    public WorkScheduleDayInputModelValidator()
    {
        RuleFor(day => day.DayOfWeek).InclusiveBetween(0, 6);
    }
}

internal sealed class CreateWorkScheduleCommandValidator : AbstractValidator<CreateWorkScheduleCommand>
{
    public CreateWorkScheduleCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Code).NotEmpty().MaximumLength(WorkSchedule.MaxCodeLength);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(WorkSchedule.MaxNameLength);
        RuleFor(command => command.ScheduleLabel).MaximumLength(WorkSchedule.MaxScheduleLabelLength);
        RuleFor(command => command.AttendanceDateAnchor).NotEmpty();
        RuleFor(command => command.ScheduleClass).NotEmpty();
        RuleFor(command => command.TotalWeeklyHours)
            .GreaterThan(0m)
            .LessThanOrEqualTo(WorkSchedule.MaxWeeklyHours)
            .When(command => command.TotalWeeklyHours.HasValue);
        RuleFor(command => command.Days).NotEmpty();
        RuleForEach(command => command.Days).SetValidator(new WorkScheduleDayInputModelValidator());
    }
}

internal sealed class UpdateWorkScheduleCommandValidator : AbstractValidator<UpdateWorkScheduleCommand>
{
    public UpdateWorkScheduleCommandValidator()
    {
        RuleFor(command => command.WorkScheduleId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Code).NotEmpty().MaximumLength(WorkSchedule.MaxCodeLength);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(WorkSchedule.MaxNameLength);
        RuleFor(command => command.ScheduleLabel).MaximumLength(WorkSchedule.MaxScheduleLabelLength);
        RuleFor(command => command.AttendanceDateAnchor).NotEmpty();
        RuleFor(command => command.ScheduleClass).NotEmpty();
        RuleFor(command => command.TotalWeeklyHours)
            .GreaterThan(0m)
            .LessThanOrEqualTo(WorkSchedule.MaxWeeklyHours)
            .When(command => command.TotalWeeklyHours.HasValue);
        RuleFor(command => command.Days).NotEmpty();
        RuleForEach(command => command.Days).SetValidator(new WorkScheduleDayInputModelValidator());
    }
}

internal sealed class ActivateWorkScheduleCommandValidator : AbstractValidator<ActivateWorkScheduleCommand>
{
    public ActivateWorkScheduleCommandValidator()
    {
        RuleFor(command => command.WorkScheduleId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivateWorkScheduleCommandValidator : AbstractValidator<InactivateWorkScheduleCommand>
{
    public InactivateWorkScheduleCommandValidator()
    {
        RuleFor(command => command.WorkScheduleId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

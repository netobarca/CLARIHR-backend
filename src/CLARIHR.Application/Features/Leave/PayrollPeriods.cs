using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Leave.Common;
using CLARIHR.Domain.Leave;
using FluentValidation;

namespace CLARIHR.Application.Features.Leave;

public sealed record PayrollPeriodListItemResponse(
    Guid Id,
    string PayPeriodTypeCode,
    int Year,
    int Number,
    string Label,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Code,
    Guid? PayrollDefinitionPublicId,
    DateOnly? CutoffDate,
    DateOnly? PaymentDate,
    int? Month,
    string StatusCode,
    bool AllowsOvertimeEntry,
    DateOnly? OvertimeEntryStart,
    DateOnly? OvertimeEntryEnd,
    bool AllowsAttendance,
    DateOnly? AttendanceEntryStart,
    DateOnly? AttendanceEntryEnd,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

public sealed record PayrollPeriodResponse(
    Guid Id,
    string PayPeriodTypeCode,
    int Year,
    int Number,
    string Label,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Code,
    Guid? PayrollDefinitionPublicId,
    DateOnly? CutoffDate,
    DateOnly? PaymentDate,
    int? Month,
    string StatusCode,
    bool AllowsOvertimeEntry,
    DateOnly? OvertimeEntryStart,
    DateOnly? OvertimeEntryEnd,
    bool AllowsAttendance,
    DateOnly? AttendanceEntryStart,
    DateOnly? AttendanceEntryEnd,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

public sealed record SearchPayrollPeriodsQuery(
    Guid CompanyId,
    string? PayPeriodTypeCode,
    int? Year,
    bool? IsActive,
    int PageNumber = 1,
    int PageSize = LeaveConfigurationValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false)
    : IQuery<PagedResponse<PayrollPeriodListItemResponse>>;

public sealed record GetPayrollPeriodByIdQuery(Guid PayrollPeriodId) : IQuery<PayrollPeriodResponse>;

public sealed record CreatePayrollPeriodCommand(
    Guid CompanyId,
    string PayPeriodTypeCode,
    int Year,
    int Number,
    string Label,
    DateOnly StartDate,
    DateOnly EndDate,
    Guid? PayrollDefinitionPublicId = null,
    string? Code = null,
    DateOnly? CutoffDate = null,
    DateOnly? PaymentDate = null,
    int? Month = null,
    bool AllowsOvertimeEntry = false,
    DateOnly? OvertimeEntryStart = null,
    DateOnly? OvertimeEntryEnd = null,
    bool AllowsAttendance = false,
    DateOnly? AttendanceEntryStart = null,
    DateOnly? AttendanceEntryEnd = null)
    : ICommand<PayrollPeriodResponse>;

public sealed record UpdatePayrollPeriodCommand(
    Guid PayrollPeriodId,
    string PayPeriodTypeCode,
    int Year,
    int Number,
    string Label,
    DateOnly StartDate,
    DateOnly EndDate,
    Guid ConcurrencyToken,
    Guid? PayrollDefinitionPublicId = null,
    string? Code = null,
    DateOnly? CutoffDate = null,
    DateOnly? PaymentDate = null,
    int? Month = null,
    bool AllowsOvertimeEntry = false,
    DateOnly? OvertimeEntryStart = null,
    DateOnly? OvertimeEntryEnd = null,
    bool AllowsAttendance = false,
    DateOnly? AttendanceEntryStart = null,
    DateOnly? AttendanceEntryEnd = null)
    : ICommand<PayrollPeriodResponse>;

public sealed record ActivatePayrollPeriodCommand(Guid PayrollPeriodId, Guid ConcurrencyToken)
    : ICommand<PayrollPeriodResponse>;

public sealed record InactivatePayrollPeriodCommand(Guid PayrollPeriodId, Guid ConcurrencyToken)
    : ICommand<PayrollPeriodResponse>;

public static class PayrollPeriodErrors
{
    public static readonly Error PayrollPeriodNotFound = new(
        "PAYROLL_PERIOD_NOT_FOUND",
        "The payroll period could not be found.",
        ErrorType.NotFound);

    public static readonly Error TypeInvalid = new(
        "PAYROLL_PERIOD_TYPE_INVALID",
        "The pay period type code is not valid for the active catalog.",
        ErrorType.UnprocessableEntity);

    public static readonly Error PeriodConflict = new(
        "PAYROLL_PERIOD_DUPLICATE",
        "Another payroll period already uses the requested type, year and number.",
        ErrorType.Conflict);

    public static readonly Error PeriodOverlap = new(
        "PAYROLL_PERIOD_OVERLAP",
        "The payroll period date range overlaps another active period of the same type and year.",
        ErrorType.UnprocessableEntity);

    public static readonly Error RuleViolation = new(
        "PAYROLL_PERIOD_RULE_VIOLATION",
        "The payroll period request violates a domain rule.",
        ErrorType.UnprocessableEntity);

    /// <summary>The referenced Nómina does not exist, is inactive or belongs to another tenant (REQ-012 §5).</summary>
    public static readonly Error DefinitionRequired = new(
        "PAYROLL_PERIOD_DEFINITION_REQUIRED",
        "An active payroll definition is required for this payroll period.",
        ErrorType.UnprocessableEntity);

    /// <summary>Cutoff/month/window values are incoherent with the period or its Nómina (REQ-012 §5).</summary>
    public static readonly Error ScheduleInvalid = new(
        "PAYROLL_PERIOD_SCHEDULE_INVALID",
        "The payroll period schedule (cutoff, payment date, month or entry windows) is not valid.",
        ErrorType.UnprocessableEntity);

    /// <summary>The period is CERRADO/ANULADO and no longer accepts changes (REQ-012 §5).</summary>
    public static readonly Error StateRuleViolation = new(
        "PAYROLL_PERIOD_STATE_RULE_VIOLATION",
        "The payroll period status does not allow the requested change.",
        ErrorType.UnprocessableEntity);

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch(LeaveConfigurationPermissionCodes.PayrollPeriodsResourceKey, action);
}

/// <summary>
/// Annual calendar mass-generation for a Nómina (REQ-012 §3.2 — molde <c>vacation-periods/generate</c>):
/// derives the definition's <c>TotalPeriods</c> ranges for <see cref="Year"/> by its pay frequency
/// (quincenas 1-15/16-fin · ISO weeks · calendar months), cutoff/payment = period end (editable),
/// month = end's month, status GENERADO, code «{NOMINA}-{YYYY}-{NN}», windows materialized from the
/// Nómina rule (P-18). Idempotent by (definition, year, number) — a re-run creates nothing.
/// </summary>
public sealed record GeneratePayrollPeriodCalendarCommand(
    Guid CompanyId,
    Guid PayrollDefinitionPublicId,
    int Year)
    : ICommand<PayrollPeriodCalendarGenerationSummary>;

/// <summary>
/// Summary of a calendar generation run: how many periods were derived for the year, created, skipped
/// (already existed — idempotency) and not derivable (a <c>TotalPeriods</c> beyond the natural calendar
/// capacity, e.g. a 13th monthly run, is created by hand).
/// </summary>
public sealed record PayrollPeriodCalendarGenerationSummary(
    int Year,
    int TotalPeriods,
    int Created,
    int Skipped,
    int NotDerivable);

internal sealed class GeneratePayrollPeriodCalendarCommandValidator : AbstractValidator<GeneratePayrollPeriodCalendarCommand>
{
    public GeneratePayrollPeriodCalendarCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.PayrollDefinitionPublicId).NotEmpty();
        RuleFor(command => command.Year)
            .InclusiveBetween(PayrollPeriodDefinition.MinYear, PayrollPeriodDefinition.MaxYear);
    }
}

internal sealed class SearchPayrollPeriodsQueryValidator : AbstractValidator<SearchPayrollPeriodsQuery>
{
    public SearchPayrollPeriodsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.PayPeriodTypeCode).MaximumLength(PayrollPeriodDefinition.MaxPayPeriodTypeCodeLength);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, LeaveConfigurationValidationRules.MaxPageSize);
    }
}

internal sealed class GetPayrollPeriodByIdQueryValidator : AbstractValidator<GetPayrollPeriodByIdQuery>
{
    public GetPayrollPeriodByIdQueryValidator()
    {
        RuleFor(query => query.PayrollPeriodId).NotEmpty();
    }
}

internal sealed class CreatePayrollPeriodCommandValidator : AbstractValidator<CreatePayrollPeriodCommand>
{
    public CreatePayrollPeriodCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.PayPeriodTypeCode)
            .NotEmpty()
            .MaximumLength(PayrollPeriodDefinition.MaxPayPeriodTypeCodeLength);
        RuleFor(command => command.Year)
            .InclusiveBetween(PayrollPeriodDefinition.MinYear, PayrollPeriodDefinition.MaxYear);
        RuleFor(command => command.Number).GreaterThanOrEqualTo(1);
        RuleFor(command => command.Label).NotEmpty().MaximumLength(PayrollPeriodDefinition.MaxLabelLength);
        RuleFor(command => command.EndDate)
            .GreaterThanOrEqualTo(command => command.StartDate)
            .WithMessage("End date must be greater than or equal to start date.");
        // REQ-012 extension: the new fields are OPTIONAL on the wire (legacy periods keep working
        // untouched) but become MANDATORY as a group when the Nómina travels (§3.2 / decision №5); the
        // window rules mirror the domain guards so a bad request dies with a clean 400 instead of a 500.
        RuleFor(command => command.Code).MaximumLength(PayrollPeriodDefinition.MaxCodeLength);
        RuleFor(command => command.Code)
            .NotEmpty()
            .When(command => command.PayrollDefinitionPublicId.HasValue)
            .WithMessage("Code is required when the payroll definition travels.");
        RuleFor(command => command.CutoffDate)
            .NotNull()
            .When(command => command.PayrollDefinitionPublicId.HasValue)
            .WithMessage("Cutoff date is required when the payroll definition travels.");
        RuleFor(command => command.PaymentDate)
            .NotNull()
            .When(command => command.PayrollDefinitionPublicId.HasValue)
            .WithMessage("Payment date is required when the payroll definition travels.");
        RuleFor(command => command.Month)
            .NotNull()
            .When(command => command.PayrollDefinitionPublicId.HasValue)
            .WithMessage("Month is required when the payroll definition travels.");
        RuleFor(command => command.Month).InclusiveBetween(1, 12).When(command => command.Month.HasValue);
        RuleFor(command => command.OvertimeEntryStart)
            .Null()
            .When(command => !command.AllowsOvertimeEntry)
            .WithMessage("Overtime entry window dates require the window to be enabled.");
        RuleFor(command => command.OvertimeEntryEnd)
            .Null()
            .When(command => !command.AllowsOvertimeEntry)
            .WithMessage("Overtime entry window dates require the window to be enabled.");
        RuleFor(command => command.AttendanceEntryStart)
            .Null()
            .When(command => !command.AllowsAttendance)
            .WithMessage("Attendance entry window dates require the window to be enabled.");
        RuleFor(command => command.AttendanceEntryEnd)
            .Null()
            .When(command => !command.AllowsAttendance)
            .WithMessage("Attendance entry window dates require the window to be enabled.");
    }
}

internal sealed class UpdatePayrollPeriodCommandValidator : AbstractValidator<UpdatePayrollPeriodCommand>
{
    public UpdatePayrollPeriodCommandValidator()
    {
        RuleFor(command => command.PayrollPeriodId).NotEmpty();
        RuleFor(command => command.PayPeriodTypeCode)
            .NotEmpty()
            .MaximumLength(PayrollPeriodDefinition.MaxPayPeriodTypeCodeLength);
        RuleFor(command => command.Year)
            .InclusiveBetween(PayrollPeriodDefinition.MinYear, PayrollPeriodDefinition.MaxYear);
        RuleFor(command => command.Number).GreaterThanOrEqualTo(1);
        RuleFor(command => command.Label).NotEmpty().MaximumLength(PayrollPeriodDefinition.MaxLabelLength);
        RuleFor(command => command.EndDate)
            .GreaterThanOrEqualTo(command => command.StartDate)
            .WithMessage("End date must be greater than or equal to start date.");
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        // REQ-012 extension — same group/window rules as the create validator.
        RuleFor(command => command.Code).MaximumLength(PayrollPeriodDefinition.MaxCodeLength);
        RuleFor(command => command.Code)
            .NotEmpty()
            .When(command => command.PayrollDefinitionPublicId.HasValue)
            .WithMessage("Code is required when the payroll definition travels.");
        RuleFor(command => command.CutoffDate)
            .NotNull()
            .When(command => command.PayrollDefinitionPublicId.HasValue)
            .WithMessage("Cutoff date is required when the payroll definition travels.");
        RuleFor(command => command.PaymentDate)
            .NotNull()
            .When(command => command.PayrollDefinitionPublicId.HasValue)
            .WithMessage("Payment date is required when the payroll definition travels.");
        RuleFor(command => command.Month)
            .NotNull()
            .When(command => command.PayrollDefinitionPublicId.HasValue)
            .WithMessage("Month is required when the payroll definition travels.");
        RuleFor(command => command.Month).InclusiveBetween(1, 12).When(command => command.Month.HasValue);
        RuleFor(command => command.OvertimeEntryStart)
            .Null()
            .When(command => !command.AllowsOvertimeEntry)
            .WithMessage("Overtime entry window dates require the window to be enabled.");
        RuleFor(command => command.OvertimeEntryEnd)
            .Null()
            .When(command => !command.AllowsOvertimeEntry)
            .WithMessage("Overtime entry window dates require the window to be enabled.");
        RuleFor(command => command.AttendanceEntryStart)
            .Null()
            .When(command => !command.AllowsAttendance)
            .WithMessage("Attendance entry window dates require the window to be enabled.");
        RuleFor(command => command.AttendanceEntryEnd)
            .Null()
            .When(command => !command.AllowsAttendance)
            .WithMessage("Attendance entry window dates require the window to be enabled.");
    }
}

internal sealed class ActivatePayrollPeriodCommandValidator : AbstractValidator<ActivatePayrollPeriodCommand>
{
    public ActivatePayrollPeriodCommandValidator()
    {
        RuleFor(command => command.PayrollPeriodId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivatePayrollPeriodCommandValidator : AbstractValidator<InactivatePayrollPeriodCommand>
{
    public InactivatePayrollPeriodCommandValidator()
    {
        RuleFor(command => command.PayrollPeriodId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

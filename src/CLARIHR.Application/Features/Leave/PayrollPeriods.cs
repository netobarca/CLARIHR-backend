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
    DateOnly EndDate)
    : ICommand<PayrollPeriodResponse>;

public sealed record UpdatePayrollPeriodCommand(
    Guid PayrollPeriodId,
    string PayPeriodTypeCode,
    int Year,
    int Number,
    string Label,
    DateOnly StartDate,
    DateOnly EndDate,
    Guid ConcurrencyToken)
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

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch(LeaveConfigurationPermissionCodes.PayrollPeriodsResourceKey, action);
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

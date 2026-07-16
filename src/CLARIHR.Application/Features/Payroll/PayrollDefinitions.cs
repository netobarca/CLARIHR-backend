using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Payroll.Common;
using CLARIHR.Domain.Payroll;
using FluentValidation;

namespace CLARIHR.Application.Features.Payroll;

public sealed record PayrollDefinitionListItemResponse(
    Guid Id,
    string Code,
    string Name,
    string PayrollTypeCode,
    string PayPeriodCode,
    int TotalPeriods,
    bool GuaranteesMinimumIncome,
    string CurrencyCode,
    bool OvertimeWindowEnabled,
    int? OvertimeWindowOffsetDays,
    bool AttendanceWindowEnabled,
    int? AttendanceWindowOffsetDays,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

public sealed record PayrollDefinitionResponse(
    Guid Id,
    string Code,
    string Name,
    string PayrollTypeCode,
    string PayPeriodCode,
    int TotalPeriods,
    bool GuaranteesMinimumIncome,
    string CurrencyCode,
    bool OvertimeWindowEnabled,
    int? OvertimeWindowOffsetDays,
    bool AttendanceWindowEnabled,
    int? AttendanceWindowOffsetDays,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

public sealed record SearchPayrollDefinitionsQuery(
    Guid CompanyId,
    bool? IsActive,
    string? Search,
    int PageNumber = 1,
    int PageSize = PayrollConfigurationValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false)
    : IQuery<PagedResponse<PayrollDefinitionListItemResponse>>;

public sealed record GetPayrollDefinitionByIdQuery(Guid PayrollDefinitionId)
    : IQuery<PayrollDefinitionResponse>;

public sealed record CreatePayrollDefinitionCommand(
    Guid CompanyId,
    string Code,
    string Name,
    string PayrollTypeCode,
    string PayPeriodCode,
    int TotalPeriods,
    bool GuaranteesMinimumIncome,
    string CurrencyCode,
    bool OvertimeWindowEnabled,
    int? OvertimeWindowOffsetDays,
    bool AttendanceWindowEnabled,
    int? AttendanceWindowOffsetDays)
    : ICommand<PayrollDefinitionResponse>;

public sealed record UpdatePayrollDefinitionCommand(
    Guid PayrollDefinitionId,
    string Code,
    string Name,
    string PayrollTypeCode,
    string PayPeriodCode,
    int TotalPeriods,
    bool GuaranteesMinimumIncome,
    string CurrencyCode,
    bool OvertimeWindowEnabled,
    int? OvertimeWindowOffsetDays,
    bool AttendanceWindowEnabled,
    int? AttendanceWindowOffsetDays,
    Guid ConcurrencyToken)
    : ICommand<PayrollDefinitionResponse>;

public sealed record ActivatePayrollDefinitionCommand(Guid PayrollDefinitionId, Guid ConcurrencyToken)
    : ICommand<PayrollDefinitionResponse>;

public sealed record InactivatePayrollDefinitionCommand(Guid PayrollDefinitionId, Guid ConcurrencyToken)
    : ICommand<PayrollDefinitionResponse>;

public static class PayrollDefinitionErrors
{
    public static readonly Error PayrollDefinitionNotFound = new(
        "PAYROLL_DEFINITION_NOT_FOUND",
        "The payroll definition could not be found.",
        ErrorType.NotFound);

    // The upfront duplicate probe returns this; a concurrent writer that trips the filtered unique index is
    // mapped to the same code (see PayrollDefinitionConstraintViolations) — REQ-012 §5.
    public static readonly Error CodeTaken = new(
        "PAYROLL_DEFINITION_CODE_TAKEN",
        "Another active payroll definition already uses the requested code.",
        ErrorType.Conflict);

    public static readonly Error InUse = new(
        "PAYROLL_DEFINITION_IN_USE",
        "The payroll definition is referenced by an active period or run and cannot be inactivated.",
        ErrorType.UnprocessableEntity);

    /// <summary>One of payrollTypeCode / payPeriodCode / currencyCode is not an active catalog code (§5).</summary>
    public static readonly Error CatalogInvalid = new(
        "PAYROLL_DEFINITION_CATALOG_INVALID",
        "The payroll type, pay period or currency code is not an active catalog code.",
        ErrorType.UnprocessableEntity);

    public static readonly Error ConcurrencyConflict = new(
        "CONCURRENCY_CONFLICT",
        "The resource was modified by another request. Refresh and try again.",
        ErrorType.Conflict);

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch(PayrollConfigurationPermissionCodes.PayrollDefinitionsResourceKey, action);
}

internal sealed class SearchPayrollDefinitionsQueryValidator : AbstractValidator<SearchPayrollDefinitionsQuery>
{
    public SearchPayrollDefinitionsQueryValidator()
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

internal sealed class GetPayrollDefinitionByIdQueryValidator : AbstractValidator<GetPayrollDefinitionByIdQuery>
{
    public GetPayrollDefinitionByIdQueryValidator()
    {
        RuleFor(query => query.PayrollDefinitionId).NotEmpty();
    }
}

internal sealed class CreatePayrollDefinitionCommandValidator : AbstractValidator<CreatePayrollDefinitionCommand>
{
    public CreatePayrollDefinitionCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Code).NotEmpty().MaximumLength(PayrollDefinition.MaxCodeLength);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(PayrollDefinition.MaxNameLength);
        RuleFor(command => command.PayrollTypeCode).NotEmpty().MaximumLength(PayrollDefinition.MaxPayrollTypeCodeLength);
        RuleFor(command => command.PayPeriodCode).NotEmpty().MaximumLength(PayrollDefinition.MaxPayPeriodCodeLength);
        // The coherence with the canonical periods-per-year of the fixed frequencies is SOFT
        // (PayrollFrequencies — a 13th aguinaldo run is deliberate), so only nonsense is rejected here.
        RuleFor(command => command.TotalPeriods)
            .InclusiveBetween(1, PayrollConfigurationValidationRules.MaxTotalPeriods);
        RuleFor(command => command.CurrencyCode).NotEmpty().Length(PayrollDefinition.CurrencyCodeLength);
        // Window offsets only while their window is enabled — mirrors the domain guard so the request dies
        // with a clean 400 instead of a 500 (the offset itself may be negative, P-18).
        RuleFor(command => command.OvertimeWindowOffsetDays)
            .Null()
            .When(command => !command.OvertimeWindowEnabled)
            .WithMessage("Overtime window offset requires the overtime window to be enabled.");
        RuleFor(command => command.AttendanceWindowOffsetDays)
            .Null()
            .When(command => !command.AttendanceWindowEnabled)
            .WithMessage("Attendance window offset requires the attendance window to be enabled.");
    }
}

internal sealed class UpdatePayrollDefinitionCommandValidator : AbstractValidator<UpdatePayrollDefinitionCommand>
{
    public UpdatePayrollDefinitionCommandValidator()
    {
        RuleFor(command => command.PayrollDefinitionId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command.Code).NotEmpty().MaximumLength(PayrollDefinition.MaxCodeLength);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(PayrollDefinition.MaxNameLength);
        RuleFor(command => command.PayrollTypeCode).NotEmpty().MaximumLength(PayrollDefinition.MaxPayrollTypeCodeLength);
        RuleFor(command => command.PayPeriodCode).NotEmpty().MaximumLength(PayrollDefinition.MaxPayPeriodCodeLength);
        RuleFor(command => command.TotalPeriods)
            .InclusiveBetween(1, PayrollConfigurationValidationRules.MaxTotalPeriods);
        RuleFor(command => command.CurrencyCode).NotEmpty().Length(PayrollDefinition.CurrencyCodeLength);
        RuleFor(command => command.OvertimeWindowOffsetDays)
            .Null()
            .When(command => !command.OvertimeWindowEnabled)
            .WithMessage("Overtime window offset requires the overtime window to be enabled.");
        RuleFor(command => command.AttendanceWindowOffsetDays)
            .Null()
            .When(command => !command.AttendanceWindowEnabled)
            .WithMessage("Attendance window offset requires the attendance window to be enabled.");
    }
}

internal sealed class ActivatePayrollDefinitionCommandValidator : AbstractValidator<ActivatePayrollDefinitionCommand>
{
    public ActivatePayrollDefinitionCommandValidator()
    {
        RuleFor(command => command.PayrollDefinitionId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivatePayrollDefinitionCommandValidator : AbstractValidator<InactivatePayrollDefinitionCommand>
{
    public InactivatePayrollDefinitionCommandValidator()
    {
        RuleFor(command => command.PayrollDefinitionId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

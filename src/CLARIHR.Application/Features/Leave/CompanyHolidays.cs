using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Leave.Common;
using CLARIHR.Domain.Leave;
using FluentValidation;

namespace CLARIHR.Application.Features.Leave;

public sealed record CompanyHolidayListItemResponse(
    Guid Id,
    DateOnly Date,
    string Description,
    string ScopeCode,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

public sealed record CompanyHolidayResponse(
    Guid Id,
    DateOnly Date,
    string Description,
    string ScopeCode,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

public sealed record SearchCompanyHolidaysQuery(
    Guid CompanyId,
    int? Year,
    string? ScopeCode,
    bool? IsActive,
    int PageNumber = 1,
    int PageSize = LeaveConfigurationValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false)
    : IQuery<PagedResponse<CompanyHolidayListItemResponse>>;

public sealed record GetCompanyHolidayByIdQuery(Guid CompanyHolidayId) : IQuery<CompanyHolidayResponse>;

public sealed record CreateCompanyHolidayCommand(
    Guid CompanyId,
    DateOnly Date,
    string Description,
    string ScopeCode)
    : ICommand<CompanyHolidayResponse>;

public sealed record UpdateCompanyHolidayCommand(
    Guid CompanyHolidayId,
    DateOnly Date,
    string Description,
    string ScopeCode,
    Guid ConcurrencyToken)
    : ICommand<CompanyHolidayResponse>;

public sealed record ActivateCompanyHolidayCommand(Guid CompanyHolidayId, Guid ConcurrencyToken)
    : ICommand<CompanyHolidayResponse>;

public sealed record InactivateCompanyHolidayCommand(Guid CompanyHolidayId, Guid ConcurrencyToken)
    : ICommand<CompanyHolidayResponse>;

public static class CompanyHolidayErrors
{
    public static readonly Error CompanyHolidayNotFound = new(
        "COMPANY_HOLIDAY_NOT_FOUND",
        "The company holiday could not be found.",
        ErrorType.NotFound);

    public static readonly Error DateConflict = new(
        "HOLIDAY_DUPLICATE",
        "Another company holiday already exists for the requested date.",
        ErrorType.Conflict);

    public static readonly Error RuleViolation = new(
        "COMPANY_HOLIDAY_RULE_VIOLATION",
        "The company holiday request violates a domain rule.",
        ErrorType.UnprocessableEntity);

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch(LeaveConfigurationPermissionCodes.CompanyHolidaysResourceKey, action);
}

/// <summary>
/// Shared validator helpers for the company-holiday contracts: the scope code is a closed domain
/// enumeration (<see cref="CompanyHolidayScopes"/>), matched case-insensitively after trim so the
/// validator mirrors the domain's <c>NormalizeCode</c> and rejects unknown scopes with a 400
/// before they reach the entity guard.
/// </summary>
internal static class CompanyHolidayValidationRules
{
    public static readonly string ScopeCodeMessage =
        $"Scope code must be one of: {string.Join(", ", CompanyHolidayScopes.All)}.";

    public static bool IsSupportedScopeCode(string? scopeCode) =>
        !string.IsNullOrWhiteSpace(scopeCode) &&
        CompanyHolidayScopes.All.Contains(scopeCode.Trim().ToUpperInvariant());
}

internal sealed class SearchCompanyHolidaysQueryValidator : AbstractValidator<SearchCompanyHolidaysQuery>
{
    public SearchCompanyHolidaysQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.ScopeCode)
            .Must(scopeCode => string.IsNullOrWhiteSpace(scopeCode) || CompanyHolidayValidationRules.IsSupportedScopeCode(scopeCode))
            .WithMessage(CompanyHolidayValidationRules.ScopeCodeMessage);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, LeaveConfigurationValidationRules.MaxPageSize);
    }
}

internal sealed class GetCompanyHolidayByIdQueryValidator : AbstractValidator<GetCompanyHolidayByIdQuery>
{
    public GetCompanyHolidayByIdQueryValidator()
    {
        RuleFor(query => query.CompanyHolidayId).NotEmpty();
    }
}

internal sealed class CreateCompanyHolidayCommandValidator : AbstractValidator<CreateCompanyHolidayCommand>
{
    public CreateCompanyHolidayCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Date)
            .NotEqual(default(DateOnly))
            .WithMessage("Date is required.");
        RuleFor(command => command.Description).NotEmpty().MaximumLength(CompanyHoliday.MaxDescriptionLength);
        RuleFor(command => command.ScopeCode)
            .NotEmpty()
            .Must(CompanyHolidayValidationRules.IsSupportedScopeCode)
            .WithMessage(CompanyHolidayValidationRules.ScopeCodeMessage);
    }
}

internal sealed class UpdateCompanyHolidayCommandValidator : AbstractValidator<UpdateCompanyHolidayCommand>
{
    public UpdateCompanyHolidayCommandValidator()
    {
        RuleFor(command => command.CompanyHolidayId).NotEmpty();
        RuleFor(command => command.Date)
            .NotEqual(default(DateOnly))
            .WithMessage("Date is required.");
        RuleFor(command => command.Description).NotEmpty().MaximumLength(CompanyHoliday.MaxDescriptionLength);
        RuleFor(command => command.ScopeCode)
            .NotEmpty()
            .Must(CompanyHolidayValidationRules.IsSupportedScopeCode)
            .WithMessage(CompanyHolidayValidationRules.ScopeCodeMessage);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ActivateCompanyHolidayCommandValidator : AbstractValidator<ActivateCompanyHolidayCommand>
{
    public ActivateCompanyHolidayCommandValidator()
    {
        RuleFor(command => command.CompanyHolidayId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivateCompanyHolidayCommandValidator : AbstractValidator<InactivateCompanyHolidayCommand>
{
    public InactivateCompanyHolidayCommandValidator()
    {
        RuleFor(command => command.CompanyHolidayId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

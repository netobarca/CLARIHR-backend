using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Leave.Common;
using CLARIHR.Domain.Leave;
using FluentValidation;

namespace CLARIHR.Application.Features.Leave;

public sealed record IncapacityTypeListItemResponse(
    Guid Id,
    string Code,
    string Name,
    string? DeductionTypeText,
    string? IncomeTypeText,
    bool AppliesToWorkAccident,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

public sealed record IncapacityTypeResponse(
    Guid Id,
    string Code,
    string Name,
    string? DeductionTypeText,
    string? IncomeTypeText,
    bool AppliesToWorkAccident,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

public sealed record SearchIncapacityTypesQuery(
    Guid CompanyId,
    bool? IsActive,
    string? Search,
    int PageNumber = 1,
    int PageSize = LeaveConfigurationValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false)
    : IQuery<PagedResponse<IncapacityTypeListItemResponse>>;

public sealed record GetIncapacityTypeByIdQuery(Guid IncapacityTypeId) : IQuery<IncapacityTypeResponse>;

public sealed record CreateIncapacityTypeCommand(
    Guid CompanyId,
    string Code,
    string Name,
    string? DeductionTypeText,
    string? IncomeTypeText,
    bool AppliesToWorkAccident)
    : ICommand<IncapacityTypeResponse>;

public sealed record UpdateIncapacityTypeCommand(
    Guid IncapacityTypeId,
    string Code,
    string Name,
    string? DeductionTypeText,
    string? IncomeTypeText,
    bool AppliesToWorkAccident,
    Guid ConcurrencyToken)
    : ICommand<IncapacityTypeResponse>;

public sealed record ActivateIncapacityTypeCommand(Guid IncapacityTypeId, Guid ConcurrencyToken)
    : ICommand<IncapacityTypeResponse>;

public sealed record InactivateIncapacityTypeCommand(Guid IncapacityTypeId, Guid ConcurrencyToken)
    : ICommand<IncapacityTypeResponse>;

public static class IncapacityTypeErrors
{
    public static readonly Error IncapacityTypeNotFound = new(
        "INCAPACITY_TYPE_NOT_FOUND",
        "The incapacity type could not be found.",
        ErrorType.NotFound);

    public static readonly Error CodeConflict = new(
        "INCAPACITY_TYPE_CODE_CONFLICT",
        "Another incapacity type already uses the requested code.",
        ErrorType.Conflict);

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch(LeaveConfigurationPermissionCodes.IncapacityTypesResourceKey, action);
}

internal sealed class SearchIncapacityTypesQueryValidator : AbstractValidator<SearchIncapacityTypesQuery>
{
    public SearchIncapacityTypesQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.Search)
            .MaximumLength(150)
            .Must(LeaveConfigurationValidationRules.IsValidSearchLength)
            .WithMessage($"Search must be at least {LeaveConfigurationValidationRules.MinSearchLength} characters when provided.");
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, LeaveConfigurationValidationRules.MaxPageSize);
    }
}

internal sealed class GetIncapacityTypeByIdQueryValidator : AbstractValidator<GetIncapacityTypeByIdQuery>
{
    public GetIncapacityTypeByIdQueryValidator()
    {
        RuleFor(query => query.IncapacityTypeId).NotEmpty();
    }
}

internal sealed class CreateIncapacityTypeCommandValidator : AbstractValidator<CreateIncapacityTypeCommand>
{
    public CreateIncapacityTypeCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Code).NotEmpty().MaximumLength(IncapacityType.MaxCodeLength);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(IncapacityType.MaxNameLength);
        RuleFor(command => command.DeductionTypeText).MaximumLength(IncapacityType.MaxDeductionTypeTextLength);
        RuleFor(command => command.IncomeTypeText).MaximumLength(IncapacityType.MaxIncomeTypeTextLength);
    }
}

internal sealed class UpdateIncapacityTypeCommandValidator : AbstractValidator<UpdateIncapacityTypeCommand>
{
    public UpdateIncapacityTypeCommandValidator()
    {
        RuleFor(command => command.IncapacityTypeId).NotEmpty();
        RuleFor(command => command.Code).NotEmpty().MaximumLength(IncapacityType.MaxCodeLength);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(IncapacityType.MaxNameLength);
        RuleFor(command => command.DeductionTypeText).MaximumLength(IncapacityType.MaxDeductionTypeTextLength);
        RuleFor(command => command.IncomeTypeText).MaximumLength(IncapacityType.MaxIncomeTypeTextLength);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ActivateIncapacityTypeCommandValidator : AbstractValidator<ActivateIncapacityTypeCommand>
{
    public ActivateIncapacityTypeCommandValidator()
    {
        RuleFor(command => command.IncapacityTypeId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivateIncapacityTypeCommandValidator : AbstractValidator<InactivateIncapacityTypeCommand>
{
    public InactivateIncapacityTypeCommandValidator()
    {
        RuleFor(command => command.IncapacityTypeId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

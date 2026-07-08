using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Leave.Common;
using CLARIHR.Domain.Leave;
using FluentValidation;

namespace CLARIHR.Application.Features.Leave;

public sealed record CompensatoryTimeTypeListItemResponse(
    Guid Id,
    string Code,
    string Name,
    string OperationCode,
    decimal CreditFactor,
    int SortOrder,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

public sealed record CompensatoryTimeTypeResponse(
    Guid Id,
    string Code,
    string Name,
    string OperationCode,
    decimal CreditFactor,
    int SortOrder,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

public sealed record SearchCompensatoryTimeTypesQuery(
    Guid CompanyId,
    bool? IsActive,
    string? OperationCode,
    string? Search,
    int PageNumber = 1,
    int PageSize = LeaveConfigurationValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false)
    : IQuery<PagedResponse<CompensatoryTimeTypeListItemResponse>>;

public sealed record GetCompensatoryTimeTypeByIdQuery(Guid CompensatoryTimeTypeId)
    : IQuery<CompensatoryTimeTypeResponse>;

public sealed record CreateCompensatoryTimeTypeCommand(
    Guid CompanyId,
    string Code,
    string Name,
    string OperationCode,
    decimal CreditFactor,
    int SortOrder)
    : ICommand<CompensatoryTimeTypeResponse>;

public sealed record UpdateCompensatoryTimeTypeCommand(
    Guid CompensatoryTimeTypeId,
    string Code,
    string Name,
    string OperationCode,
    decimal CreditFactor,
    int SortOrder,
    Guid ConcurrencyToken)
    : ICommand<CompensatoryTimeTypeResponse>;

public sealed record ActivateCompensatoryTimeTypeCommand(Guid CompensatoryTimeTypeId, Guid ConcurrencyToken)
    : ICommand<CompensatoryTimeTypeResponse>;

public sealed record InactivateCompensatoryTimeTypeCommand(Guid CompensatoryTimeTypeId, Guid ConcurrencyToken)
    : ICommand<CompensatoryTimeTypeResponse>;

public static class CompensatoryTimeTypeErrors
{
    public static readonly Error CompensatoryTimeTypeNotFound = new(
        "COMPENSATORY_TIME_TYPE_NOT_FOUND",
        "The compensatory-time type could not be found.",
        ErrorType.NotFound);

    public static readonly Error CodeConflict = new(
        "COMPENSATORY_TIME_TYPE_CODE_CONFLICT",
        "Another active compensatory-time type already uses the requested code.",
        ErrorType.Conflict);

    public static readonly Error InUse = new(
        "COMPENSATORY_TIME_TYPE_IN_USE",
        "The compensatory-time type is referenced by an active record and cannot be inactivated.",
        ErrorType.UnprocessableEntity);

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch(LeaveConfigurationPermissionCodes.CompensatoryTimeTypesResourceKey, action);
}

internal sealed class SearchCompensatoryTimeTypesQueryValidator : AbstractValidator<SearchCompensatoryTimeTypesQuery>
{
    public SearchCompensatoryTimeTypesQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.OperationCode).MaximumLength(CompensatoryTimeType.MaxOperationCodeLength);
        RuleFor(query => query.Search)
            .MaximumLength(150)
            .Must(LeaveConfigurationValidationRules.IsValidSearchLength)
            .WithMessage($"Search must be at least {LeaveConfigurationValidationRules.MinSearchLength} characters when provided.");
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, LeaveConfigurationValidationRules.MaxPageSize);
    }
}

internal sealed class GetCompensatoryTimeTypeByIdQueryValidator : AbstractValidator<GetCompensatoryTimeTypeByIdQuery>
{
    public GetCompensatoryTimeTypeByIdQueryValidator()
    {
        RuleFor(query => query.CompensatoryTimeTypeId).NotEmpty();
    }
}

internal sealed class CreateCompensatoryTimeTypeCommandValidator : AbstractValidator<CreateCompensatoryTimeTypeCommand>
{
    public CreateCompensatoryTimeTypeCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Code).NotEmpty().MaximumLength(CompensatoryTimeType.MaxCodeLength);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(CompensatoryTimeType.MaxNameLength);
        RuleFor(command => command.OperationCode)
            .NotEmpty()
            .Must(static operationCode => CompensatoryTimeOperations.IsValid(operationCode))
            .WithMessage("Operation code must be ACREDITA, DEBITA or AMBAS.");
        RuleFor(command => command.CreditFactor).GreaterThan(0m);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class UpdateCompensatoryTimeTypeCommandValidator : AbstractValidator<UpdateCompensatoryTimeTypeCommand>
{
    public UpdateCompensatoryTimeTypeCommandValidator()
    {
        RuleFor(command => command.CompensatoryTimeTypeId).NotEmpty();
        RuleFor(command => command.Code).NotEmpty().MaximumLength(CompensatoryTimeType.MaxCodeLength);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(CompensatoryTimeType.MaxNameLength);
        RuleFor(command => command.OperationCode)
            .NotEmpty()
            .Must(static operationCode => CompensatoryTimeOperations.IsValid(operationCode))
            .WithMessage("Operation code must be ACREDITA, DEBITA or AMBAS.");
        RuleFor(command => command.CreditFactor).GreaterThan(0m);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ActivateCompensatoryTimeTypeCommandValidator : AbstractValidator<ActivateCompensatoryTimeTypeCommand>
{
    public ActivateCompensatoryTimeTypeCommandValidator()
    {
        RuleFor(command => command.CompensatoryTimeTypeId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivateCompensatoryTimeTypeCommandValidator : AbstractValidator<InactivateCompensatoryTimeTypeCommand>
{
    public InactivateCompensatoryTimeTypeCommandValidator()
    {
        RuleFor(command => command.CompensatoryTimeTypeId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}


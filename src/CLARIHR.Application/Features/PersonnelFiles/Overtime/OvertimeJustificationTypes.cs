using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PersonnelFiles.Overtime.Common;
using CLARIHR.Domain.Overtime;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles.Overtime;

public sealed record OvertimeJustificationTypeListItemResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    int SortOrder,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

public sealed record OvertimeJustificationTypeResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    int SortOrder,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

public sealed record SearchOvertimeJustificationTypesQuery(
    Guid CompanyId,
    bool? IsActive,
    string? Search,
    int PageNumber = 1,
    int PageSize = OvertimeConfigurationValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false)
    : IQuery<PagedResponse<OvertimeJustificationTypeListItemResponse>>;

public sealed record GetOvertimeJustificationTypeByIdQuery(Guid JustificationTypeId)
    : IQuery<OvertimeJustificationTypeResponse>;

public sealed record CreateOvertimeJustificationTypeCommand(
    Guid CompanyId,
    string Code,
    string Name,
    string? Description,
    int SortOrder)
    : ICommand<OvertimeJustificationTypeResponse>;

public sealed record UpdateOvertimeJustificationTypeCommand(
    Guid JustificationTypeId,
    string Code,
    string Name,
    string? Description,
    int SortOrder,
    Guid ConcurrencyToken)
    : ICommand<OvertimeJustificationTypeResponse>;

public sealed record ActivateOvertimeJustificationTypeCommand(Guid JustificationTypeId, Guid ConcurrencyToken)
    : ICommand<OvertimeJustificationTypeResponse>;

public sealed record InactivateOvertimeJustificationTypeCommand(Guid JustificationTypeId, Guid ConcurrencyToken)
    : ICommand<OvertimeJustificationTypeResponse>;

public static class OvertimeJustificationTypeErrors
{
    public static readonly Error JustificationTypeNotFound = new(
        "OVERTIME_JUSTIFICATION_TYPE_NOT_FOUND",
        "The overtime justification type could not be found.",
        ErrorType.NotFound);

    public static readonly Error CodeTaken = new(
        "OVERTIME_JUSTIFICATION_TYPE_CODE_TAKEN",
        "Another active overtime justification type already uses the requested code.",
        ErrorType.Conflict);

    public static readonly Error InUse = new(
        "OVERTIME_JUSTIFICATION_TYPE_IN_USE",
        "The overtime justification type is referenced by an active record and cannot be inactivated.",
        ErrorType.UnprocessableEntity);

    public static readonly Error ConcurrencyConflict = new(
        "CONCURRENCY_CONFLICT",
        "The resource was modified by another request. Refresh and try again.",
        ErrorType.Conflict);

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch(OvertimeConfigurationResourceKeys.OvertimeJustificationTypes, action);
}

internal sealed class SearchOvertimeJustificationTypesQueryValidator : AbstractValidator<SearchOvertimeJustificationTypesQuery>
{
    public SearchOvertimeJustificationTypesQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.Search)
            .MaximumLength(150)
            .Must(OvertimeConfigurationValidationRules.IsValidSearchLength)
            .WithMessage($"Search must be at least {OvertimeConfigurationValidationRules.MinSearchLength} characters when provided.");
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, OvertimeConfigurationValidationRules.MaxPageSize);
    }
}

internal sealed class GetOvertimeJustificationTypeByIdQueryValidator : AbstractValidator<GetOvertimeJustificationTypeByIdQuery>
{
    public GetOvertimeJustificationTypeByIdQueryValidator()
    {
        RuleFor(query => query.JustificationTypeId).NotEmpty();
    }
}

internal sealed class CreateOvertimeJustificationTypeCommandValidator : AbstractValidator<CreateOvertimeJustificationTypeCommand>
{
    public CreateOvertimeJustificationTypeCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Code).NotEmpty().MaximumLength(OvertimeJustificationType.MaxCodeLength);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(OvertimeJustificationType.MaxNameLength);
        RuleFor(command => command.Description).MaximumLength(OvertimeJustificationType.MaxDescriptionLength);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class UpdateOvertimeJustificationTypeCommandValidator : AbstractValidator<UpdateOvertimeJustificationTypeCommand>
{
    public UpdateOvertimeJustificationTypeCommandValidator()
    {
        RuleFor(command => command.JustificationTypeId).NotEmpty();
        RuleFor(command => command.Code).NotEmpty().MaximumLength(OvertimeJustificationType.MaxCodeLength);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(OvertimeJustificationType.MaxNameLength);
        RuleFor(command => command.Description).MaximumLength(OvertimeJustificationType.MaxDescriptionLength);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ActivateOvertimeJustificationTypeCommandValidator : AbstractValidator<ActivateOvertimeJustificationTypeCommand>
{
    public ActivateOvertimeJustificationTypeCommandValidator()
    {
        RuleFor(command => command.JustificationTypeId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivateOvertimeJustificationTypeCommandValidator : AbstractValidator<InactivateOvertimeJustificationTypeCommand>
{
    public InactivateOvertimeJustificationTypeCommandValidator()
    {
        RuleFor(command => command.JustificationTypeId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

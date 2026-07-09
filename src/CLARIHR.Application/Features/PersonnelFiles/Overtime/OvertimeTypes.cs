using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PersonnelFiles.Overtime.Common;
using CLARIHR.Domain.Overtime;
using FluentValidation;

namespace CLARIHR.Application.Features.PersonnelFiles.Overtime;

public sealed record OvertimeTypeListItemResponse(
    Guid Id,
    string Code,
    string Name,
    decimal DefaultFactor,
    string? PayrollEffectDescription,
    int SortOrder,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

public sealed record OvertimeTypeResponse(
    Guid Id,
    string Code,
    string Name,
    decimal DefaultFactor,
    string? PayrollEffectDescription,
    int SortOrder,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

public sealed record SearchOvertimeTypesQuery(
    Guid CompanyId,
    bool? IsActive,
    string? Search,
    int PageNumber = 1,
    int PageSize = OvertimeConfigurationValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false)
    : IQuery<PagedResponse<OvertimeTypeListItemResponse>>;

public sealed record GetOvertimeTypeByIdQuery(Guid OvertimeTypeId)
    : IQuery<OvertimeTypeResponse>;

public sealed record CreateOvertimeTypeCommand(
    Guid CompanyId,
    string Code,
    string Name,
    decimal DefaultFactor,
    string? PayrollEffectDescription,
    int SortOrder)
    : ICommand<OvertimeTypeResponse>;

public sealed record UpdateOvertimeTypeCommand(
    Guid OvertimeTypeId,
    string Code,
    string Name,
    decimal DefaultFactor,
    string? PayrollEffectDescription,
    int SortOrder,
    Guid ConcurrencyToken)
    : ICommand<OvertimeTypeResponse>;

public sealed record ActivateOvertimeTypeCommand(Guid OvertimeTypeId, Guid ConcurrencyToken)
    : ICommand<OvertimeTypeResponse>;

public sealed record InactivateOvertimeTypeCommand(Guid OvertimeTypeId, Guid ConcurrencyToken)
    : ICommand<OvertimeTypeResponse>;

public static class OvertimeTypeErrors
{
    public static readonly Error OvertimeTypeNotFound = new(
        "OVERTIME_TYPE_NOT_FOUND",
        "The overtime type could not be found.",
        ErrorType.NotFound);

    // The upfront duplicate probe returns this; a concurrent writer that trips the filtered unique index is
    // mapped to the same code (see OvertimeTypeConstraintViolations) — REQ-007 §3.2 "OVERTIME_TYPE_CODE_TAKEN".
    public static readonly Error CodeTaken = new(
        "OVERTIME_TYPE_CODE_TAKEN",
        "Another active overtime type already uses the requested code.",
        ErrorType.Conflict);

    public static readonly Error InUse = new(
        "OVERTIME_TYPE_IN_USE",
        "The overtime type is referenced by an active record and cannot be inactivated.",
        ErrorType.UnprocessableEntity);

    public static readonly Error ConcurrencyConflict = new(
        "CONCURRENCY_CONFLICT",
        "The resource was modified by another request. Refresh and try again.",
        ErrorType.Conflict);

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch(OvertimeConfigurationResourceKeys.OvertimeTypes, action);
}

internal sealed class SearchOvertimeTypesQueryValidator : AbstractValidator<SearchOvertimeTypesQuery>
{
    public SearchOvertimeTypesQueryValidator()
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

internal sealed class GetOvertimeTypeByIdQueryValidator : AbstractValidator<GetOvertimeTypeByIdQuery>
{
    public GetOvertimeTypeByIdQueryValidator()
    {
        RuleFor(query => query.OvertimeTypeId).NotEmpty();
    }
}

internal sealed class CreateOvertimeTypeCommandValidator : AbstractValidator<CreateOvertimeTypeCommand>
{
    public CreateOvertimeTypeCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Code).NotEmpty().MaximumLength(OvertimeType.MaxCodeLength);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(OvertimeType.MaxNameLength);
        // Explicit factor guard: record ctor defaults do not apply on deserialization, so a missing factor
        // arrives as 0 and must be rejected here (mirrors the domain SetDefaultFactor guard). numeric(5,2).
        RuleFor(command => command.DefaultFactor).GreaterThan(0m).LessThan(1000m);
        RuleFor(command => command.PayrollEffectDescription)
            .MaximumLength(OvertimeType.MaxPayrollEffectDescriptionLength);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class UpdateOvertimeTypeCommandValidator : AbstractValidator<UpdateOvertimeTypeCommand>
{
    public UpdateOvertimeTypeCommandValidator()
    {
        RuleFor(command => command.OvertimeTypeId).NotEmpty();
        RuleFor(command => command.Code).NotEmpty().MaximumLength(OvertimeType.MaxCodeLength);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(OvertimeType.MaxNameLength);
        RuleFor(command => command.DefaultFactor).GreaterThan(0m).LessThan(1000m);
        RuleFor(command => command.PayrollEffectDescription)
            .MaximumLength(OvertimeType.MaxPayrollEffectDescriptionLength);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ActivateOvertimeTypeCommandValidator : AbstractValidator<ActivateOvertimeTypeCommand>
{
    public ActivateOvertimeTypeCommandValidator()
    {
        RuleFor(command => command.OvertimeTypeId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivateOvertimeTypeCommandValidator : AbstractValidator<InactivateOvertimeTypeCommand>
{
    public InactivateOvertimeTypeCommandValidator()
    {
        RuleFor(command => command.OvertimeTypeId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

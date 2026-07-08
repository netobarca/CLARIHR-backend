using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.EmployeeRelations.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.EmployeeRelations;
using FluentValidation;

namespace CLARIHR.Application.Features.EmployeeRelations;

public sealed record DisciplinaryActionTypeListItemResponse(
    Guid Id,
    string Code,
    string Name,
    bool AppliesSuspension,
    int SortOrder,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

public sealed record DisciplinaryActionTypeResponse(
    Guid Id,
    string Code,
    string Name,
    bool AppliesSuspension,
    int SortOrder,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

public sealed record SearchDisciplinaryActionTypesQuery(
    Guid CompanyId,
    bool? IsActive,
    bool? AppliesSuspension,
    string? Search,
    int PageNumber = 1,
    int PageSize = EmployeeRelationsConfigurationValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false)
    : IQuery<PagedResponse<DisciplinaryActionTypeListItemResponse>>;

public sealed record GetDisciplinaryActionTypeByIdQuery(Guid DisciplinaryActionTypeId)
    : IQuery<DisciplinaryActionTypeResponse>;

public sealed record CreateDisciplinaryActionTypeCommand(
    Guid CompanyId,
    string Code,
    string Name,
    bool AppliesSuspension,
    int SortOrder)
    : ICommand<DisciplinaryActionTypeResponse>;

public sealed record UpdateDisciplinaryActionTypeCommand(
    Guid DisciplinaryActionTypeId,
    string Code,
    string Name,
    bool AppliesSuspension,
    int SortOrder,
    Guid ConcurrencyToken)
    : ICommand<DisciplinaryActionTypeResponse>;

public sealed record ActivateDisciplinaryActionTypeCommand(Guid DisciplinaryActionTypeId, Guid ConcurrencyToken)
    : ICommand<DisciplinaryActionTypeResponse>;

public sealed record InactivateDisciplinaryActionTypeCommand(Guid DisciplinaryActionTypeId, Guid ConcurrencyToken)
    : ICommand<DisciplinaryActionTypeResponse>;

public static class DisciplinaryActionTypeErrors
{
    public static readonly Error DisciplinaryActionTypeNotFound = new(
        "DISCIPLINARY_ACTION_TYPE_NOT_FOUND",
        "The disciplinary-action type could not be found.",
        ErrorType.NotFound);

    public static readonly Error CodeConflict = new(
        "DISCIPLINARY_ACTION_TYPE_CODE_CONFLICT",
        "Another active disciplinary-action type already uses the requested code.",
        ErrorType.Conflict);

    public static readonly Error InUse = new(
        "DISCIPLINARY_ACTION_TYPE_IN_USE",
        "The disciplinary-action type is referenced by an active record and cannot be inactivated.",
        ErrorType.UnprocessableEntity);

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch(EmployeeRelationsConfigurationPermissionCodes.DisciplinaryActionTypesResourceKey, action);
}

internal sealed class SearchDisciplinaryActionTypesQueryValidator : AbstractValidator<SearchDisciplinaryActionTypesQuery>
{
    public SearchDisciplinaryActionTypesQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.Search)
            .MaximumLength(150)
            .Must(EmployeeRelationsConfigurationValidationRules.IsValidSearchLength)
            .WithMessage($"Search must be at least {EmployeeRelationsConfigurationValidationRules.MinSearchLength} characters when provided.");
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, EmployeeRelationsConfigurationValidationRules.MaxPageSize);
    }
}

internal sealed class GetDisciplinaryActionTypeByIdQueryValidator : AbstractValidator<GetDisciplinaryActionTypeByIdQuery>
{
    public GetDisciplinaryActionTypeByIdQueryValidator()
    {
        RuleFor(query => query.DisciplinaryActionTypeId).NotEmpty();
    }
}

internal sealed class CreateDisciplinaryActionTypeCommandValidator : AbstractValidator<CreateDisciplinaryActionTypeCommand>
{
    public CreateDisciplinaryActionTypeCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Code).NotEmpty().MaximumLength(DisciplinaryActionType.MaxCodeLength);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(DisciplinaryActionType.MaxNameLength);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class UpdateDisciplinaryActionTypeCommandValidator : AbstractValidator<UpdateDisciplinaryActionTypeCommand>
{
    public UpdateDisciplinaryActionTypeCommandValidator()
    {
        RuleFor(command => command.DisciplinaryActionTypeId).NotEmpty();
        RuleFor(command => command.Code).NotEmpty().MaximumLength(DisciplinaryActionType.MaxCodeLength);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(DisciplinaryActionType.MaxNameLength);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ActivateDisciplinaryActionTypeCommandValidator : AbstractValidator<ActivateDisciplinaryActionTypeCommand>
{
    public ActivateDisciplinaryActionTypeCommandValidator()
    {
        RuleFor(command => command.DisciplinaryActionTypeId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivateDisciplinaryActionTypeCommandValidator : AbstractValidator<InactivateDisciplinaryActionTypeCommand>
{
    public InactivateDisciplinaryActionTypeCommandValidator()
    {
        RuleFor(command => command.DisciplinaryActionTypeId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

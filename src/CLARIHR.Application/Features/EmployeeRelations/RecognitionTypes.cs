using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.EmployeeRelations.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.EmployeeRelations;
using FluentValidation;

namespace CLARIHR.Application.Features.EmployeeRelations;

public sealed record RecognitionTypeListItemResponse(
    Guid Id,
    string Code,
    string Name,
    int SortOrder,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

public sealed record RecognitionTypeResponse(
    Guid Id,
    string Code,
    string Name,
    int SortOrder,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

public sealed record SearchRecognitionTypesQuery(
    Guid CompanyId,
    bool? IsActive,
    string? Search,
    int PageNumber = 1,
    int PageSize = EmployeeRelationsConfigurationValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false)
    : IQuery<PagedResponse<RecognitionTypeListItemResponse>>;

public sealed record GetRecognitionTypeByIdQuery(Guid RecognitionTypeId)
    : IQuery<RecognitionTypeResponse>;

public sealed record CreateRecognitionTypeCommand(
    Guid CompanyId,
    string Code,
    string Name,
    int SortOrder)
    : ICommand<RecognitionTypeResponse>;

public sealed record UpdateRecognitionTypeCommand(
    Guid RecognitionTypeId,
    string Code,
    string Name,
    int SortOrder,
    Guid ConcurrencyToken)
    : ICommand<RecognitionTypeResponse>;

public sealed record ActivateRecognitionTypeCommand(Guid RecognitionTypeId, Guid ConcurrencyToken)
    : ICommand<RecognitionTypeResponse>;

public sealed record InactivateRecognitionTypeCommand(Guid RecognitionTypeId, Guid ConcurrencyToken)
    : ICommand<RecognitionTypeResponse>;

public static class RecognitionTypeErrors
{
    public static readonly Error RecognitionTypeNotFound = new(
        "RECOGNITION_TYPE_NOT_FOUND",
        "The recognition type could not be found.",
        ErrorType.NotFound);

    public static readonly Error CodeConflict = new(
        "RECOGNITION_TYPE_CODE_CONFLICT",
        "Another active recognition type already uses the requested code.",
        ErrorType.Conflict);

    public static readonly Error InUse = new(
        "RECOGNITION_TYPE_IN_USE",
        "The recognition type is referenced by an active record and cannot be inactivated.",
        ErrorType.UnprocessableEntity);

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch(EmployeeRelationsConfigurationPermissionCodes.RecognitionTypesResourceKey, action);
}

internal sealed class SearchRecognitionTypesQueryValidator : AbstractValidator<SearchRecognitionTypesQuery>
{
    public SearchRecognitionTypesQueryValidator()
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

internal sealed class GetRecognitionTypeByIdQueryValidator : AbstractValidator<GetRecognitionTypeByIdQuery>
{
    public GetRecognitionTypeByIdQueryValidator()
    {
        RuleFor(query => query.RecognitionTypeId).NotEmpty();
    }
}

internal sealed class CreateRecognitionTypeCommandValidator : AbstractValidator<CreateRecognitionTypeCommand>
{
    public CreateRecognitionTypeCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Code).NotEmpty().MaximumLength(RecognitionType.MaxCodeLength);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(RecognitionType.MaxNameLength);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class UpdateRecognitionTypeCommandValidator : AbstractValidator<UpdateRecognitionTypeCommand>
{
    public UpdateRecognitionTypeCommandValidator()
    {
        RuleFor(command => command.RecognitionTypeId).NotEmpty();
        RuleFor(command => command.Code).NotEmpty().MaximumLength(RecognitionType.MaxCodeLength);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(RecognitionType.MaxNameLength);
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ActivateRecognitionTypeCommandValidator : AbstractValidator<ActivateRecognitionTypeCommand>
{
    public ActivateRecognitionTypeCommandValidator()
    {
        RuleFor(command => command.RecognitionTypeId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivateRecognitionTypeCommandValidator : AbstractValidator<InactivateRecognitionTypeCommand>
{
    public InactivateRecognitionTypeCommandValidator()
    {
        RuleFor(command => command.RecognitionTypeId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.EmployeeRelations.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.EmployeeRelations;
using FluentValidation;

namespace CLARIHR.Application.Features.EmployeeRelations;

public sealed record DisciplinaryActionCauseListItemResponse(
    Guid Id,
    string Code,
    string Name,
    string? DeductionConceptTypeCode,
    int SortOrder,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

public sealed record DisciplinaryActionCauseResponse(
    Guid Id,
    string Code,
    string Name,
    string? DeductionConceptTypeCode,
    int SortOrder,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

public sealed record SearchDisciplinaryActionCausesQuery(
    Guid CompanyId,
    bool? IsActive,
    string? Search,
    int PageNumber = 1,
    int PageSize = EmployeeRelationsConfigurationValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false)
    : IQuery<PagedResponse<DisciplinaryActionCauseListItemResponse>>;

public sealed record GetDisciplinaryActionCauseByIdQuery(Guid DisciplinaryActionCauseId)
    : IQuery<DisciplinaryActionCauseResponse>;

public sealed record CreateDisciplinaryActionCauseCommand(
    Guid CompanyId,
    string Code,
    string Name,
    string? DeductionConceptTypeCode,
    int SortOrder)
    : ICommand<DisciplinaryActionCauseResponse>;

public sealed record UpdateDisciplinaryActionCauseCommand(
    Guid DisciplinaryActionCauseId,
    string Code,
    string Name,
    string? DeductionConceptTypeCode,
    int SortOrder,
    Guid ConcurrencyToken)
    : ICommand<DisciplinaryActionCauseResponse>;

public sealed record ActivateDisciplinaryActionCauseCommand(Guid DisciplinaryActionCauseId, Guid ConcurrencyToken)
    : ICommand<DisciplinaryActionCauseResponse>;

public sealed record InactivateDisciplinaryActionCauseCommand(Guid DisciplinaryActionCauseId, Guid ConcurrencyToken)
    : ICommand<DisciplinaryActionCauseResponse>;

public static class DisciplinaryActionCauseErrors
{
    public static readonly Error DisciplinaryActionCauseNotFound = new(
        "DISCIPLINARY_ACTION_CAUSE_NOT_FOUND",
        "The disciplinary-action cause could not be found.",
        ErrorType.NotFound);

    public static readonly Error CodeConflict = new(
        "DISCIPLINARY_ACTION_CAUSE_CODE_CONFLICT",
        "Another active disciplinary-action cause already uses the requested code.",
        ErrorType.Conflict);

    public static readonly Error InUse = new(
        "DISCIPLINARY_ACTION_CAUSE_IN_USE",
        "The disciplinary-action cause is referenced by an active record and cannot be inactivated.",
        ErrorType.UnprocessableEntity);

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch(EmployeeRelationsConfigurationPermissionCodes.DisciplinaryActionCausesResourceKey, action);
}

internal sealed class SearchDisciplinaryActionCausesQueryValidator : AbstractValidator<SearchDisciplinaryActionCausesQuery>
{
    public SearchDisciplinaryActionCausesQueryValidator()
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

internal sealed class GetDisciplinaryActionCauseByIdQueryValidator : AbstractValidator<GetDisciplinaryActionCauseByIdQuery>
{
    public GetDisciplinaryActionCauseByIdQueryValidator()
    {
        RuleFor(query => query.DisciplinaryActionCauseId).NotEmpty();
    }
}

internal sealed class CreateDisciplinaryActionCauseCommandValidator : AbstractValidator<CreateDisciplinaryActionCauseCommand>
{
    public CreateDisciplinaryActionCauseCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Code).NotEmpty().MaximumLength(DisciplinaryActionCause.MaxCodeLength);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(DisciplinaryActionCause.MaxNameLength);
        RuleFor(command => command.DeductionConceptTypeCode)
            .MaximumLength(DisciplinaryActionCause.MaxDeductionConceptTypeCodeLength)
            .When(command => !string.IsNullOrWhiteSpace(command.DeductionConceptTypeCode));
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
    }
}

internal sealed class UpdateDisciplinaryActionCauseCommandValidator : AbstractValidator<UpdateDisciplinaryActionCauseCommand>
{
    public UpdateDisciplinaryActionCauseCommandValidator()
    {
        RuleFor(command => command.DisciplinaryActionCauseId).NotEmpty();
        RuleFor(command => command.Code).NotEmpty().MaximumLength(DisciplinaryActionCause.MaxCodeLength);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(DisciplinaryActionCause.MaxNameLength);
        RuleFor(command => command.DeductionConceptTypeCode)
            .MaximumLength(DisciplinaryActionCause.MaxDeductionConceptTypeCodeLength)
            .When(command => !string.IsNullOrWhiteSpace(command.DeductionConceptTypeCode));
        RuleFor(command => command.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ActivateDisciplinaryActionCauseCommandValidator : AbstractValidator<ActivateDisciplinaryActionCauseCommand>
{
    public ActivateDisciplinaryActionCauseCommandValidator()
    {
        RuleFor(command => command.DisciplinaryActionCauseId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivateDisciplinaryActionCauseCommandValidator : AbstractValidator<InactivateDisciplinaryActionCauseCommand>
{
    public InactivateDisciplinaryActionCauseCommandValidator()
    {
        RuleFor(command => command.DisciplinaryActionCauseId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

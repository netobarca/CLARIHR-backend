using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.Leave.Common;
using CLARIHR.Domain.Leave;
using FluentValidation;

namespace CLARIHR.Application.Features.Leave;

/// <summary>
/// One subsidy tranche of an incapacity risk (day range → subsidy percent + payer). Child of the
/// risk aggregate: it carries no allowed-actions and no concurrency token of its own — the parent's
/// token covers the full set.
/// </summary>
public sealed record IncapacityRiskParameterResponse(
    int DayFrom,
    int? DayTo,
    decimal SubsidyPercent,
    string PayerCode,
    int SortOrder);

public sealed record IncapacityRiskListItemResponse(
    Guid Id,
    string Code,
    string Name,
    bool CountsSeventhDay,
    bool CountsSaturday,
    bool CountsHoliday,
    bool UsesWorkSchedule,
    bool AllowsIndefinite,
    bool AllowsExtension,
    bool UsesFund,
    bool HasSubsidy,
    int ParameterCount,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

public sealed record IncapacityRiskResponse(
    Guid Id,
    string Code,
    string Name,
    bool CountsSeventhDay,
    bool CountsSaturday,
    bool CountsHoliday,
    bool UsesWorkSchedule,
    bool AllowsIndefinite,
    bool AllowsExtension,
    bool UsesFund,
    bool HasSubsidy,
    IReadOnlyCollection<IncapacityRiskParameterResponse> Parameters,
    bool IsActive,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

/// <summary>
/// Input shape for one subsidy tranche in create/replace requests. Structural rules (first tranche
/// starts at day 1, contiguity, single open-ended tail, payer catalog) live in the domain guard
/// <see cref="IncapacityRisk.ReplaceParameters"/>; the validator only covers per-item basics.
/// </summary>
public sealed record IncapacityRiskParameterInputModel(
    int DayFrom,
    int? DayTo,
    decimal SubsidyPercent,
    string PayerCode);

public sealed record SearchIncapacityRisksQuery(
    Guid CompanyId,
    bool? IsActive,
    string? Search,
    int PageNumber = 1,
    int PageSize = LeaveConfigurationValidationRules.DefaultPageSize,
    bool IncludeAllowedActions = false)
    : IQuery<PagedResponse<IncapacityRiskListItemResponse>>;

public sealed record GetIncapacityRiskByIdQuery(Guid IncapacityRiskId) : IQuery<IncapacityRiskResponse>;

public sealed record CreateIncapacityRiskCommand(
    Guid CompanyId,
    string Code,
    string Name,
    bool CountsSeventhDay,
    bool CountsSaturday,
    bool CountsHoliday,
    bool UsesWorkSchedule,
    bool AllowsIndefinite,
    bool AllowsExtension,
    bool UsesFund,
    bool HasSubsidy,
    IReadOnlyCollection<IncapacityRiskParameterInputModel> Parameters)
    : ICommand<IncapacityRiskResponse>;

public sealed record UpdateIncapacityRiskCommand(
    Guid IncapacityRiskId,
    string Code,
    string Name,
    bool CountsSeventhDay,
    bool CountsSaturday,
    bool CountsHoliday,
    bool UsesWorkSchedule,
    bool AllowsIndefinite,
    bool AllowsExtension,
    bool UsesFund,
    bool HasSubsidy,
    Guid ConcurrencyToken)
    : ICommand<IncapacityRiskResponse>;

public sealed record ReplaceIncapacityRiskParametersCommand(
    Guid IncapacityRiskId,
    IReadOnlyCollection<IncapacityRiskParameterInputModel> Parameters,
    Guid ConcurrencyToken)
    : ICommand<IncapacityRiskResponse>;

public sealed record ActivateIncapacityRiskCommand(Guid IncapacityRiskId, Guid ConcurrencyToken)
    : ICommand<IncapacityRiskResponse>;

public sealed record InactivateIncapacityRiskCommand(Guid IncapacityRiskId, Guid ConcurrencyToken)
    : ICommand<IncapacityRiskResponse>;

public static class IncapacityRiskErrors
{
    public static readonly Error IncapacityRiskNotFound = new(
        "INCAPACITY_RISK_NOT_FOUND",
        "The incapacity risk could not be found.",
        ErrorType.NotFound);

    public static readonly Error CodeConflict = new(
        "INCAPACITY_RISK_CODE_CONFLICT",
        "Another incapacity risk already uses the requested code.",
        ErrorType.Conflict);

    /// <summary>
    /// The subsidy tranche set violates a structural domain rule (must start at day 1, be
    /// contiguous, only the last tranche open-ended, valid payer codes, subsidy flag coherence).
    /// The domain guard's message travels as the error description.
    /// </summary>
    public static Error ParametersInvalid(string reason) => new(
        "RISK_PARAMETERS_INVALID",
        reason,
        ErrorType.UnprocessableEntity);

    /// <summary>
    /// A non-parametric domain guard rejected the mutation (e.g. turning off <c>hasSubsidy</c>
    /// while subsidy parameters still exist).
    /// </summary>
    public static Error RuleViolation(string reason) => new(
        "INCAPACITY_RISK_RULE_VIOLATION",
        reason,
        ErrorType.UnprocessableEntity);

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch(LeaveConfigurationPermissionCodes.IncapacityRisksResourceKey, action);
}

internal sealed class IncapacityRiskParameterInputModelValidator : AbstractValidator<IncapacityRiskParameterInputModel>
{
    public IncapacityRiskParameterInputModelValidator()
    {
        RuleFor(parameter => parameter.DayFrom).GreaterThanOrEqualTo(1);
        RuleFor(parameter => parameter.SubsidyPercent).InclusiveBetween(0, 100);
        RuleFor(parameter => parameter.PayerCode)
            .NotEmpty()
            .MaximumLength(IncapacityRiskParameter.MaxPayerCodeLength);
    }
}

internal sealed class SearchIncapacityRisksQueryValidator : AbstractValidator<SearchIncapacityRisksQuery>
{
    public SearchIncapacityRisksQueryValidator()
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

internal sealed class GetIncapacityRiskByIdQueryValidator : AbstractValidator<GetIncapacityRiskByIdQuery>
{
    public GetIncapacityRiskByIdQueryValidator()
    {
        RuleFor(query => query.IncapacityRiskId).NotEmpty();
    }
}

internal sealed class CreateIncapacityRiskCommandValidator : AbstractValidator<CreateIncapacityRiskCommand>
{
    public CreateIncapacityRiskCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.Code).NotEmpty().MaximumLength(IncapacityRisk.MaxCodeLength);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(IncapacityRisk.MaxNameLength);
        RuleFor(command => command.Parameters).NotNull();
        RuleForEach(command => command.Parameters).SetValidator(new IncapacityRiskParameterInputModelValidator());
    }
}

internal sealed class UpdateIncapacityRiskCommandValidator : AbstractValidator<UpdateIncapacityRiskCommand>
{
    public UpdateIncapacityRiskCommandValidator()
    {
        RuleFor(command => command.IncapacityRiskId).NotEmpty();
        RuleFor(command => command.Code).NotEmpty().MaximumLength(IncapacityRisk.MaxCodeLength);
        RuleFor(command => command.Name).NotEmpty().MaximumLength(IncapacityRisk.MaxNameLength);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ReplaceIncapacityRiskParametersCommandValidator : AbstractValidator<ReplaceIncapacityRiskParametersCommand>
{
    public ReplaceIncapacityRiskParametersCommandValidator()
    {
        RuleFor(command => command.IncapacityRiskId).NotEmpty();
        RuleFor(command => command.Parameters).NotNull();
        RuleForEach(command => command.Parameters).SetValidator(new IncapacityRiskParameterInputModelValidator());
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ActivateIncapacityRiskCommandValidator : AbstractValidator<ActivateIncapacityRiskCommand>
{
    public ActivateIncapacityRiskCommandValidator()
    {
        RuleFor(command => command.IncapacityRiskId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivateIncapacityRiskCommandValidator : AbstractValidator<InactivateIncapacityRiskCommand>
{
    public InactivateIncapacityRiskCommandValidator()
    {
        RuleFor(command => command.IncapacityRiskId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.CommercialPlans.Common;
using CLARIHR.Domain.Companies;
using FluentValidation;

namespace CLARIHR.Application.Features.CommercialPlans;

public sealed record CommercialPlanLimitInput(
    string Code,
    decimal Value);

public sealed record CommercialPlanLimitResponse(
    string Code,
    decimal Value);

public sealed record CommercialPlanSummaryResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    decimal BaseMonthlyFee,
    decimal PricePerActiveEmployee,
    CommercialPlanStatus Status,
    bool IsSystemPlan,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record CommercialPlanResponse(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    decimal BaseMonthlyFee,
    decimal PricePerActiveEmployee,
    CommercialPlanStatus Status,
    bool IsSystemPlan,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    IReadOnlyCollection<CommercialPlanLimitResponse> Limits);

public sealed record SearchCommercialPlansQuery(
    CommercialPlanStatus? Status,
    string? Search,
    int PageNumber = 1,
    int PageSize = CommercialPlanValidationRules.DefaultPageSize)
    : IQuery<PagedResponse<CommercialPlanSummaryResponse>>;

public sealed record GetCommercialPlanByIdQuery(Guid CommercialPlanId)
    : IQuery<CommercialPlanResponse>;

public sealed record CreateCommercialPlanCommand(
    string Code,
    string Name,
    string? Description,
    decimal BaseMonthlyFee,
    decimal PricePerActiveEmployee,
    CommercialPlanStatus Status,
    IReadOnlyCollection<CommercialPlanLimitInput> Limits)
    : ICommand<CommercialPlanResponse>;

public sealed record UpdateCommercialPlanCommand(
    Guid CommercialPlanId,
    string Code,
    string Name,
    string? Description,
    decimal BaseMonthlyFee,
    decimal PricePerActiveEmployee,
    IReadOnlyCollection<CommercialPlanLimitInput> Limits,
    Guid ConcurrencyToken)
    : ICommand<CommercialPlanResponse>;

public sealed record ActivateCommercialPlanCommand(Guid CommercialPlanId, Guid ConcurrencyToken)
    : ICommand<CommercialPlanResponse>;

public sealed record InactivateCommercialPlanCommand(Guid CommercialPlanId, Guid ConcurrencyToken)
    : ICommand<CommercialPlanResponse>;

internal sealed class SearchCommercialPlansQueryValidator : AbstractValidator<SearchCommercialPlansQuery>
{
    public SearchCommercialPlansQueryValidator()
    {
        RuleFor(query => query.Search).MaximumLength(150);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, CommercialPlanValidationRules.MaxPageSize);
        RuleFor(query => query.Status)
            .IsInEnum()
            .When(static query => query.Status.HasValue);
    }
}

internal sealed class GetCommercialPlanByIdQueryValidator : AbstractValidator<GetCommercialPlanByIdQuery>
{
    public GetCommercialPlanByIdQueryValidator()
    {
        RuleFor(query => query.CommercialPlanId).NotEmpty();
    }
}

internal sealed class CommercialPlanLimitInputValidator : AbstractValidator<CommercialPlanLimitInput>
{
    public CommercialPlanLimitInputValidator()
    {
        RuleFor(limit => limit.Code)
            .NotEmpty()
            .MaximumLength(80)
            .Must(CommercialPlanValidationRules.IsValidLimitCode)
            .WithMessage("Limit code format is invalid.");
        RuleFor(limit => limit.Value).GreaterThanOrEqualTo(0m);
        RuleFor(limit => limit.Value)
            .Must(CommercialPlanValidationRules.HasSupportedScale)
            .WithMessage("Limit value cannot exceed 2 decimal places.");
    }
}

internal sealed class CreateCommercialPlanCommandValidator : AbstractValidator<CreateCommercialPlanCommand>
{
    public CreateCommercialPlanCommandValidator()
    {
        ApplyCommonRules();
        RuleFor(command => command.Status).IsInEnum();
    }

    private void ApplyCommonRules()
    {
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(40)
            .Must(CommercialPlanValidationRules.IsValidPlanCode)
            .WithMessage("Commercial plan code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.Description).MaximumLength(500);
        RuleFor(command => command.BaseMonthlyFee).GreaterThanOrEqualTo(0m);
        RuleFor(command => command.PricePerActiveEmployee).GreaterThanOrEqualTo(0m);
        RuleFor(command => command.BaseMonthlyFee)
            .Must(CommercialPlanValidationRules.HasSupportedScale)
            .WithMessage("Base monthly fee cannot exceed 2 decimal places.");
        RuleFor(command => command.PricePerActiveEmployee)
            .Must(CommercialPlanValidationRules.HasSupportedScale)
            .WithMessage("Price per active employee cannot exceed 2 decimal places.");
        RuleFor(command => command.Limits).NotNull();
        RuleForEach(command => command.Limits).SetValidator(new CommercialPlanLimitInputValidator());
        RuleFor(command => command.Limits)
            .Must(HaveDistinctLimitCodes)
            .WithMessage("Commercial plan limits cannot contain duplicate codes.");
    }

    private static bool HaveDistinctLimitCodes(IReadOnlyCollection<CommercialPlanLimitInput> limits) =>
        limits
            .Select(limit => limit.Code.Trim().ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .Count() == limits.Count;
}

internal sealed class UpdateCommercialPlanCommandValidator : AbstractValidator<UpdateCommercialPlanCommand>
{
    public UpdateCommercialPlanCommandValidator()
    {
        RuleFor(command => command.CommercialPlanId).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(40)
            .Must(CommercialPlanValidationRules.IsValidPlanCode)
            .WithMessage("Commercial plan code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.Description).MaximumLength(500);
        RuleFor(command => command.BaseMonthlyFee).GreaterThanOrEqualTo(0m);
        RuleFor(command => command.PricePerActiveEmployee).GreaterThanOrEqualTo(0m);
        RuleFor(command => command.BaseMonthlyFee)
            .Must(CommercialPlanValidationRules.HasSupportedScale)
            .WithMessage("Base monthly fee cannot exceed 2 decimal places.");
        RuleFor(command => command.PricePerActiveEmployee)
            .Must(CommercialPlanValidationRules.HasSupportedScale)
            .WithMessage("Price per active employee cannot exceed 2 decimal places.");
        RuleFor(command => command.Limits).NotNull();
        RuleForEach(command => command.Limits).SetValidator(new CommercialPlanLimitInputValidator());
        RuleFor(command => command.Limits)
            .Must(static limits =>
                limits
                    .Select(limit => limit.Code.Trim().ToUpperInvariant())
                    .Distinct(StringComparer.Ordinal)
                    .Count() == limits.Count)
            .WithMessage("Commercial plan limits cannot contain duplicate codes.");
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ActivateCommercialPlanCommandValidator : AbstractValidator<ActivateCommercialPlanCommand>
{
    public ActivateCommercialPlanCommandValidator()
    {
        RuleFor(command => command.CommercialPlanId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivateCommercialPlanCommandValidator : AbstractValidator<InactivateCommercialPlanCommand>
{
    public InactivateCommercialPlanCommandValidator()
    {
        RuleFor(command => command.CommercialPlanId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

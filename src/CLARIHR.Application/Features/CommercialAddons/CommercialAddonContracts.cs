using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.CommercialAddons.Common;
using CLARIHR.Domain.Companies;
using FluentValidation;

namespace CLARIHR.Application.Features.CommercialAddons;

public sealed record CommercialAddonSummaryResponse(
    Guid PublicId,
    string Code,
    string Name,
    string? Description,
    CommercialAddonType Type,
    decimal PricePerActiveEmployee,
    decimal? MinimumMonthlyFee,
    CommercialAddonPeriodicity Periodicity,
    CommercialAddonStatus Status,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record CommercialAddonResponse(
    Guid PublicId,
    string Code,
    string Name,
    string? Description,
    CommercialAddonType Type,
    decimal PricePerActiveEmployee,
    decimal? MinimumMonthlyFee,
    CommercialAddonPeriodicity Periodicity,
    CommercialAddonStatus Status,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record SearchCommercialAddonsQuery(
    CommercialAddonStatus? Status,
    string? Search,
    int PageNumber = 1,
    int PageSize = CommercialAddonValidationRules.DefaultPageSize)
    : IQuery<PagedResponse<CommercialAddonSummaryResponse>>;

public sealed record GetCommercialAddonByIdQuery(Guid CommercialAddonId)
    : IQuery<CommercialAddonResponse>;

public sealed record CreateCommercialAddonCommand(
    string Code,
    string Name,
    string? Description,
    CommercialAddonType Type,
    decimal PricePerActiveEmployee,
    decimal? MinimumMonthlyFee,
    CommercialAddonPeriodicity Periodicity,
    CommercialAddonStatus Status)
    : ICommand<CommercialAddonResponse>;

public sealed record UpdateCommercialAddonCommand(
    Guid CommercialAddonId,
    string Code,
    string Name,
    string? Description,
    CommercialAddonType Type,
    decimal PricePerActiveEmployee,
    decimal? MinimumMonthlyFee,
    CommercialAddonPeriodicity Periodicity,
    Guid ConcurrencyToken)
    : ICommand<CommercialAddonResponse>;

public sealed record ActivateCommercialAddonCommand(Guid CommercialAddonId, Guid ConcurrencyToken)
    : ICommand<CommercialAddonResponse>;

public sealed record InactivateCommercialAddonCommand(Guid CommercialAddonId, Guid ConcurrencyToken)
    : ICommand<CommercialAddonResponse>;

internal sealed class SearchCommercialAddonsQueryValidator : AbstractValidator<SearchCommercialAddonsQuery>
{
    public SearchCommercialAddonsQueryValidator()
    {
        RuleFor(query => query.Search).MaximumLength(150);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, CommercialAddonValidationRules.MaxPageSize);
        RuleFor(query => query.Status)
            .IsInEnum()
            .When(static query => query.Status.HasValue);
    }
}

internal sealed class GetCommercialAddonByIdQueryValidator : AbstractValidator<GetCommercialAddonByIdQuery>
{
    public GetCommercialAddonByIdQueryValidator()
    {
        RuleFor(query => query.CommercialAddonId).NotEmpty();
    }
}

internal sealed class CreateCommercialAddonCommandValidator : AbstractValidator<CreateCommercialAddonCommand>
{
    public CreateCommercialAddonCommandValidator()
    {
        ApplyCommonRules();
        RuleFor(command => command.Status).IsInEnum();
    }

    private void ApplyCommonRules()
    {
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(40)
            .Must(CommercialAddonValidationRules.IsValidAddonCode)
            .WithMessage("Commercial addon code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.Description).MaximumLength(500);
        RuleFor(command => command.Type)
            .IsInEnum()
            .Equal(CommercialAddonType.Massive)
            .WithMessage("Only massive commercial add-ons are supported.");
        RuleFor(command => command.PricePerActiveEmployee).GreaterThanOrEqualTo(0m);
        RuleFor(command => command.MinimumMonthlyFee)
            .GreaterThanOrEqualTo(0m)
            .When(static command => command.MinimumMonthlyFee.HasValue);
        RuleFor(command => command.PricePerActiveEmployee)
            .Must(CommercialAddonValidationRules.HasSupportedScale)
            .WithMessage("Price per active employee cannot exceed 2 decimal places.");
        RuleFor(command => command.MinimumMonthlyFee)
            .Must(static fee => !fee.HasValue || CommercialAddonValidationRules.HasSupportedScale(fee.Value))
            .WithMessage("Minimum monthly fee cannot exceed 2 decimal places.");
        RuleFor(command => command.Periodicity).IsInEnum();
    }
}

internal sealed class UpdateCommercialAddonCommandValidator : AbstractValidator<UpdateCommercialAddonCommand>
{
    public UpdateCommercialAddonCommandValidator()
    {
        RuleFor(command => command.CommercialAddonId).NotEmpty();
        RuleFor(command => command.Code)
            .NotEmpty()
            .MaximumLength(40)
            .Must(CommercialAddonValidationRules.IsValidAddonCode)
            .WithMessage("Commercial addon code format is invalid.");
        RuleFor(command => command.Name).NotEmpty().MaximumLength(150);
        RuleFor(command => command.Description).MaximumLength(500);
        RuleFor(command => command.Type)
            .IsInEnum()
            .Equal(CommercialAddonType.Massive)
            .WithMessage("Only massive commercial add-ons are supported.");
        RuleFor(command => command.PricePerActiveEmployee).GreaterThanOrEqualTo(0m);
        RuleFor(command => command.MinimumMonthlyFee)
            .GreaterThanOrEqualTo(0m)
            .When(static command => command.MinimumMonthlyFee.HasValue);
        RuleFor(command => command.PricePerActiveEmployee)
            .Must(CommercialAddonValidationRules.HasSupportedScale)
            .WithMessage("Price per active employee cannot exceed 2 decimal places.");
        RuleFor(command => command.MinimumMonthlyFee)
            .Must(static fee => !fee.HasValue || CommercialAddonValidationRules.HasSupportedScale(fee.Value))
            .WithMessage("Minimum monthly fee cannot exceed 2 decimal places.");
        RuleFor(command => command.Periodicity).IsInEnum();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class ActivateCommercialAddonCommandValidator : AbstractValidator<ActivateCommercialAddonCommand>
{
    public ActivateCommercialAddonCommandValidator()
    {
        RuleFor(command => command.CommercialAddonId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class InactivateCommercialAddonCommandValidator : AbstractValidator<InactivateCommercialAddonCommand>
{
    public InactivateCommercialAddonCommandValidator()
    {
        RuleFor(command => command.CommercialAddonId).NotEmpty();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

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
    CommercialAddonBillingModel BillingModel,
    string MeasurementUnit,
    decimal UnitPrice,
    int? MinimumQuantity,
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
    CommercialAddonBillingModel BillingModel,
    string MeasurementUnit,
    decimal UnitPrice,
    int? MinimumQuantity,
    decimal? MinimumMonthlyFee,
    CommercialAddonPeriodicity Periodicity,
    CommercialAddonStatus Status,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record SearchCommercialAddonsQuery(
    CommercialAddonType? Type,
    CommercialAddonBillingModel? BillingModel,
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
    CommercialAddonBillingModel BillingModel,
    string MeasurementUnit,
    decimal UnitPrice,
    int? MinimumQuantity,
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
    CommercialAddonBillingModel BillingModel,
    string MeasurementUnit,
    decimal UnitPrice,
    int? MinimumQuantity,
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
        RuleFor(query => query.Type)
            .IsInEnum()
            .When(static query => query.Type.HasValue);
        RuleFor(query => query.BillingModel)
            .IsInEnum()
            .When(static query => query.BillingModel.HasValue);
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
        RuleFor(command => command.Type).IsInEnum();
        RuleFor(command => command.BillingModel).IsInEnum();
        RuleFor(command => command.MeasurementUnit)
            .NotEmpty()
            .MaximumLength(CommercialAddonValidationRules.MeasurementUnitMaxLength)
            .Must(static unit => !string.IsNullOrWhiteSpace(unit))
            .WithMessage("Measurement unit is required.");
        RuleFor(command => command.UnitPrice).GreaterThanOrEqualTo(0m);
        RuleFor(command => command.MinimumQuantity)
            .GreaterThanOrEqualTo(0)
            .When(static command => command.MinimumQuantity.HasValue);
        RuleFor(command => command.MinimumMonthlyFee)
            .GreaterThanOrEqualTo(0m)
            .When(static command => command.MinimumMonthlyFee.HasValue);
        RuleFor(command => command.UnitPrice)
            .Must(CommercialAddonValidationRules.HasSupportedScale)
            .WithMessage("Unit price cannot exceed 2 decimal places.");
        RuleFor(command => command.MinimumMonthlyFee)
            .Must(static fee => !fee.HasValue || CommercialAddonValidationRules.HasSupportedScale(fee.Value))
            .WithMessage("Minimum monthly fee cannot exceed 2 decimal places.");
        RuleFor(command => command.Periodicity).IsInEnum();
        RuleFor(command => command)
            .Must(HaveCoherentPricingConfiguration)
            .WithMessage("Commercial addon pricing configuration is inconsistent with the selected type and billing model.");
    }

    private static bool HaveCoherentPricingConfiguration(CreateCommercialAddonCommand command)
    {
        var measurementUnit = command.MeasurementUnit.Trim();
        var usesReservedMassiveUnit = CommercialAddonValidationRules.UsesReservedMassiveUnit(measurementUnit);
        var containsSeat = CommercialAddonValidationRules.ContainsSeat(measurementUnit);

        return command.Type switch
        {
            CommercialAddonType.Massive =>
                command.BillingModel == CommercialAddonBillingModel.PerActiveEmployee &&
                usesReservedMassiveUnit &&
                !command.MinimumQuantity.HasValue,
            CommercialAddonType.Specialized =>
                command.BillingModel != CommercialAddonBillingModel.PerActiveEmployee &&
                !usesReservedMassiveUnit &&
                !command.MinimumMonthlyFee.HasValue &&
                ((command.BillingModel == CommercialAddonBillingModel.PerSeat && containsSeat) ||
                 (command.BillingModel == CommercialAddonBillingModel.PerVolume && !containsSeat)),
            _ => false
        };
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
        RuleFor(command => command.Type).IsInEnum();
        RuleFor(command => command.BillingModel).IsInEnum();
        RuleFor(command => command.MeasurementUnit)
            .NotEmpty()
            .MaximumLength(CommercialAddonValidationRules.MeasurementUnitMaxLength)
            .Must(static unit => !string.IsNullOrWhiteSpace(unit))
            .WithMessage("Measurement unit is required.");
        RuleFor(command => command.UnitPrice).GreaterThanOrEqualTo(0m);
        RuleFor(command => command.MinimumQuantity)
            .GreaterThanOrEqualTo(0)
            .When(static command => command.MinimumQuantity.HasValue);
        RuleFor(command => command.MinimumMonthlyFee)
            .GreaterThanOrEqualTo(0m)
            .When(static command => command.MinimumMonthlyFee.HasValue);
        RuleFor(command => command.UnitPrice)
            .Must(CommercialAddonValidationRules.HasSupportedScale)
            .WithMessage("Unit price cannot exceed 2 decimal places.");
        RuleFor(command => command.MinimumMonthlyFee)
            .Must(static fee => !fee.HasValue || CommercialAddonValidationRules.HasSupportedScale(fee.Value))
            .WithMessage("Minimum monthly fee cannot exceed 2 decimal places.");
        RuleFor(command => command.Periodicity).IsInEnum();
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
        RuleFor(command => command)
            .Must(HaveCoherentPricingConfiguration)
            .WithMessage("Commercial addon pricing configuration is inconsistent with the selected type and billing model.");
    }

    private static bool HaveCoherentPricingConfiguration(UpdateCommercialAddonCommand command)
    {
        var measurementUnit = command.MeasurementUnit.Trim();
        var usesReservedMassiveUnit = CommercialAddonValidationRules.UsesReservedMassiveUnit(measurementUnit);
        var containsSeat = CommercialAddonValidationRules.ContainsSeat(measurementUnit);

        return command.Type switch
        {
            CommercialAddonType.Massive =>
                command.BillingModel == CommercialAddonBillingModel.PerActiveEmployee &&
                usesReservedMassiveUnit &&
                !command.MinimumQuantity.HasValue,
            CommercialAddonType.Specialized =>
                command.BillingModel != CommercialAddonBillingModel.PerActiveEmployee &&
                !usesReservedMassiveUnit &&
                !command.MinimumMonthlyFee.HasValue &&
                ((command.BillingModel == CommercialAddonBillingModel.PerSeat && containsSeat) ||
                 (command.BillingModel == CommercialAddonBillingModel.PerVolume && !containsSeat)),
            _ => false
        };
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

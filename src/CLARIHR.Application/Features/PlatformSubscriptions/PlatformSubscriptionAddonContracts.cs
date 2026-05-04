using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PlatformSubscriptions.Common;
using CLARIHR.Domain.Companies;
using FluentValidation;

namespace CLARIHR.Application.Features.PlatformSubscriptions;

public sealed record PlatformCompanyAddonResponse(
    Guid CompanyAddonId,
    Guid CompanyId,
    Guid CompanySubscriptionId,
    Guid CommercialAddonId,
    string AddonCode,
    string AddonName,
    CommercialAddonType AddonType,
    CommercialAddonBillingModel BillingModel,
    string MeasurementUnit,
    decimal UnitPrice,
    int? MinimumQuantity,
    decimal? MinimumMonthlyFee,
    CommercialAddonPeriodicity Periodicity,
    string CurrencyCode,
    CompanyAddonStatus Status,
    DateTime StatusEffectiveDateUtc,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record PlatformCompanyEligibleAddonResponse(
    Guid CommercialAddonId,
    string AddonCode,
    string AddonName,
    string? Description,
    CommercialAddonType AddonType,
    CommercialAddonBillingModel BillingModel,
    string MeasurementUnit,
    decimal UnitPrice,
    int? MinimumQuantity,
    decimal? MinimumMonthlyFee,
    CommercialAddonPeriodicity Periodicity,
    CommercialAddonStatus CatalogStatus,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record PlatformCompanyAddonChangePreviewResponse(
    Guid CompanyId,
    string CompanyName,
    string CompanySlug,
    Guid CurrentSubscriptionId,
    Guid CommercialAddonId,
    string AddonCode,
    string AddonName,
    CommercialAddonType AddonType,
    CommercialAddonBillingModel BillingModel,
    string MeasurementUnit,
    decimal UnitPrice,
    int? MinimumQuantity,
    decimal? MinimumMonthlyFee,
    CommercialAddonPeriodicity Periodicity,
    string CurrencyCode,
    SubscriptionAddonChangeAction Action,
    SubscriptionAddonChangeMode Mode,
    CompanyAddonStatus CurrentStatus,
    CompanyAddonStatus ResultingStatus,
    DateTime EffectiveDateUtc,
    int QuantityBasis,
    decimal EstimatedNextChargeImpact,
    bool IsEligible,
    bool IsInformationalEstimate,
    IReadOnlyCollection<string> IneligibilityReasons,
    IReadOnlyCollection<string> Warnings);

public sealed record PlatformCompanyAddonChangeResponse(
    Guid AddonChangeId,
    Guid CompanyId,
    Guid CompanySubscriptionId,
    Guid CommercialAddonId,
    string AddonCode,
    string AddonName,
    CommercialAddonType AddonType,
    CommercialAddonBillingModel BillingModel,
    string MeasurementUnit,
    decimal UnitPrice,
    int? MinimumQuantity,
    decimal? MinimumMonthlyFee,
    CommercialAddonPeriodicity Periodicity,
    string CurrencyCode,
    SubscriptionAddonChangeAction Action,
    SubscriptionAddonChangeMode Mode,
    SubscriptionAddonChangeStatus Status,
    SubscriptionAddonChangeReasonCode ReasonCode,
    CompanyAddonStatus PreviousStatus,
    CompanyAddonStatus ResultingStatus,
    DateTime RequestedAtUtc,
    DateTime EffectiveDateUtc,
    Guid? RequestedByUserId,
    string? Observations,
    int QuantityBasis,
    decimal EstimatedNextChargeImpact,
    DateTime? AppliedAtUtc,
    Guid? AppliedSubscriptionId,
    DateTime? CancelledAtUtc,
    Guid? CancelledByUserId,
    string? CancellationObservations,
    DateTime? RejectedAtUtc,
    string? RejectionReason,
    Guid ConcurrencyToken);

public sealed record SearchPlatformCompanyAddonsQuery(
    Guid CompanyId,
    CompanyAddonStatus? Status = null,
    string? Search = null,
    int PageNumber = 1,
    int PageSize = PlatformSubscriptionValidationRules.DefaultPageSize)
    : IQuery<PagedResponse<PlatformCompanyAddonResponse>>;

public sealed record SearchPlatformCompanyEligibleAddonsQuery(
    Guid CompanyId,
    CommercialAddonType? Type = null,
    string? Search = null,
    int PageNumber = 1,
    int PageSize = PlatformSubscriptionValidationRules.DefaultPageSize)
    : IQuery<PagedResponse<PlatformCompanyEligibleAddonResponse>>;

public sealed record PreviewPlatformCompanyAddonChangeQuery(
    Guid CompanyId,
    Guid CommercialAddonId,
    SubscriptionAddonChangeAction Action,
    SubscriptionAddonChangeMode Mode,
    DateTime? RequestedEffectiveDateUtc)
    : IQuery<PlatformCompanyAddonChangePreviewResponse>;

public sealed record CreatePlatformCompanyAddonChangeCommand(
    Guid CompanyId,
    Guid CommercialAddonId,
    SubscriptionAddonChangeAction Action,
    SubscriptionAddonChangeMode Mode,
    DateTime? RequestedEffectiveDateUtc,
    SubscriptionAddonChangeReasonCode ReasonCode,
    string? Observations)
    : ICommand<PlatformCompanyAddonChangeResponse>;

public sealed record SearchPlatformCompanyAddonChangesQuery(
    Guid CompanyId,
    int PageNumber = 1,
    int PageSize = PlatformSubscriptionValidationRules.DefaultPageSize)
    : IQuery<PagedResponse<PlatformCompanyAddonChangeResponse>>;

public sealed record CancelPlatformCompanyAddonChangeCommand(
    Guid CompanyId,
    Guid AddonChangeId,
    string Observations,
    Guid ConcurrencyToken)
    : ICommand<PlatformCompanyAddonChangeResponse>;

internal sealed class SearchPlatformCompanyAddonsQueryValidator : AbstractValidator<SearchPlatformCompanyAddonsQuery>
{
    public SearchPlatformCompanyAddonsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.Search).MaximumLength(150);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PlatformSubscriptionValidationRules.MaxPageSize);
        RuleFor(query => query.Status)
            .IsInEnum()
            .When(static query => query.Status.HasValue);
    }
}

internal sealed class SearchPlatformCompanyEligibleAddonsQueryValidator : AbstractValidator<SearchPlatformCompanyEligibleAddonsQuery>
{
    public SearchPlatformCompanyEligibleAddonsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.Search).MaximumLength(150);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PlatformSubscriptionValidationRules.MaxPageSize);
        RuleFor(query => query.Type)
            .IsInEnum()
            .When(static query => query.Type.HasValue);
    }
}

internal sealed class PreviewPlatformCompanyAddonChangeQueryValidator : AbstractValidator<PreviewPlatformCompanyAddonChangeQuery>
{
    public PreviewPlatformCompanyAddonChangeQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.CommercialAddonId).NotEmpty();
        RuleFor(query => query.Action).IsInEnum();
        RuleFor(query => query.Mode).IsInEnum();
        RuleFor(query => query.RequestedEffectiveDateUtc)
            .NotEmpty()
            .When(query => query.Mode == SubscriptionAddonChangeMode.SpecificDate);
    }
}

internal sealed class CreatePlatformCompanyAddonChangeCommandValidator : AbstractValidator<CreatePlatformCompanyAddonChangeCommand>
{
    public CreatePlatformCompanyAddonChangeCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.CommercialAddonId).NotEmpty();
        RuleFor(command => command.Action).IsInEnum();
        RuleFor(command => command.Mode).IsInEnum();
        RuleFor(command => command.ReasonCode).IsInEnum();
        RuleFor(command => command.Observations).MaximumLength(2000);
        RuleFor(command => command.RequestedEffectiveDateUtc)
            .NotEmpty()
            .When(command => command.Mode == SubscriptionAddonChangeMode.SpecificDate);
    }
}

internal sealed class SearchPlatformCompanyAddonChangesQueryValidator : AbstractValidator<SearchPlatformCompanyAddonChangesQuery>
{
    public SearchPlatformCompanyAddonChangesQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PlatformSubscriptionValidationRules.MaxPageSize);
    }
}

internal sealed class CancelPlatformCompanyAddonChangeCommandValidator : AbstractValidator<CancelPlatformCompanyAddonChangeCommand>
{
    public CancelPlatformCompanyAddonChangeCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.AddonChangeId).NotEmpty();
        RuleFor(command => command.Observations)
            .NotEmpty()
            .MaximumLength(2000);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

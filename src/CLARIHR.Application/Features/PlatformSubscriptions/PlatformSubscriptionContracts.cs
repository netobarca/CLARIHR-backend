using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.PlatformSubscriptions.Common;
using CLARIHR.Domain.Companies;
using FluentValidation;

namespace CLARIHR.Application.Features.PlatformSubscriptions;

public sealed record PlatformCompanySubscriptionResponse(
    Guid SubscriptionId,
    Guid CompanyId,
    Guid CommercialPlanId,
    Guid CommercialPlanVersionId,
    string PlanCode,
    string PlanName,
    int PlanVersionNumber,
    decimal BaseMonthlyFee,
    decimal PricePerActiveEmployee,
    CompanySubscriptionPeriodicity Periodicity,
    string CurrencyCode,
    SubscriptionStatus Status,
    DateTime StartDateUtc,
    DateTime? ExpiresAtUtc,
    DateTime? EndDateUtc,
    DateTime StatusChangedAtUtc,
    SubscriptionStatusChangeReasonCode CurrentStatusReasonCode,
    string? CurrentStatusObservations,
    SubscriptionStatusChangeOrigin CurrentStatusOrigin,
    bool CanOperate,
    bool CanGenerateCharges,
    Guid ActivatedByUserId,
    DateTime ActivatedAtUtc,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record PlatformCompanySubscriptionOverviewResponse(
    Guid CompanyId,
    string CompanyName,
    string CompanySlug,
    CompanyStatus CompanyStatus,
    bool IsBillable,
    DateTime? BillableSinceUtc,
    PlatformCompanySubscriptionResponse? CurrentSubscription,
    PlatformCompanySubscriptionResponse? ScheduledReplacement);

public sealed record PlatformCompanySubscriptionPreviewResponse(
    Guid CompanyId,
    string CompanyName,
    string CompanySlug,
    CompanyStatus CompanyStatus,
    bool IsBillable,
    Guid CommercialPlanId,
    Guid CommercialPlanVersionId,
    string PlanCode,
    string PlanName,
    int PlanVersionNumber,
    decimal BaseMonthlyFee,
    decimal PricePerActiveEmployee,
    CompanySubscriptionPeriodicity Periodicity,
    string CurrencyCode,
    SubscriptionStatus ResolvedStatus,
    DateTime StartDateUtc,
    DateTime? ExpiresAtUtc,
    bool CanOperate,
    bool CanGenerateCharges,
    bool IsEligible,
    IReadOnlyCollection<string> IneligibilityReasons);

public sealed record PlatformCompanySubscriptionListItemResponse(
    Guid CompanyId,
    string CompanyName,
    string CompanySlug,
    bool IsBillable,
    Guid SubscriptionId,
    Guid CommercialPlanId,
    Guid CommercialPlanVersionId,
    string PlanCode,
    string PlanName,
    int PlanVersionNumber,
    DateTime StartDateUtc,
    DateTime? ExpiresAtUtc,
    CompanySubscriptionPeriodicity Periodicity,
    string CurrencyCode,
    SubscriptionStatus Status,
    DateTime StatusChangedAtUtc,
    SubscriptionStatusChangeReasonCode CurrentStatusReasonCode,
    SubscriptionStatusChangeOrigin CurrentStatusOrigin,
    bool CanOperate,
    bool CanGenerateCharges);

public sealed record PlatformCompanySubscriptionStatusTransitionResponse(
    SubscriptionStatus? PreviousStatus,
    SubscriptionStatus NewStatus,
    DateTime ChangedAtUtc,
    SubscriptionStatusChangeOrigin Origin,
    Guid? ActorUserId,
    SubscriptionStatusChangeReasonCode ReasonCode,
    string? Observations);

public sealed record GetPlatformCompanySubscriptionQuery(Guid CompanyId)
    : IQuery<PlatformCompanySubscriptionOverviewResponse>;

public sealed record GetPlatformCompanySubscriptionsQuery(
    Guid CompanyId,
    int PageNumber = 1,
    int PageSize = PlatformSubscriptionValidationRules.DefaultPageSize)
    : IQuery<PagedResponse<PlatformCompanySubscriptionResponse>>;

public sealed record SearchPlatformCompanySubscriptionsQuery(
    SubscriptionStatus? Status = null,
    string? Search = null,
    int PageNumber = 1,
    int PageSize = PlatformSubscriptionValidationRules.DefaultPageSize)
    : IQuery<PagedResponse<PlatformCompanySubscriptionListItemResponse>>;

public sealed record PreviewPlatformCompanySubscriptionQuery(
    Guid CompanyId,
    Guid CommercialPlanId,
    DateTime StartDateUtc,
    DateTime? ExpiresAtUtc,
    CompanySubscriptionPeriodicity Periodicity)
    : IQuery<PlatformCompanySubscriptionPreviewResponse>;

public sealed record ActivatePlatformCompanySubscriptionCommand(
    Guid CompanyId,
    Guid CommercialPlanId,
    DateTime StartDateUtc,
    DateTime? ExpiresAtUtc,
    CompanySubscriptionPeriodicity Periodicity)
    : ICommand<PlatformCompanySubscriptionResponse>;

public sealed record ChangePlatformCompanySubscriptionStatusCommand(
    Guid CompanyId,
    Guid SubscriptionId,
    SubscriptionStatus TargetStatus,
    SubscriptionStatusChangeReasonCode ReasonCode,
    string? Observations)
    : ICommand<PlatformCompanySubscriptionResponse>;

public sealed record SearchPlatformCompanySubscriptionStatusHistoryQuery(
    Guid CompanyId,
    Guid SubscriptionId,
    int PageNumber = 1,
    int PageSize = PlatformSubscriptionValidationRules.DefaultPageSize)
    : IQuery<PagedResponse<PlatformCompanySubscriptionStatusTransitionResponse>>;

internal sealed class GetPlatformCompanySubscriptionQueryValidator : AbstractValidator<GetPlatformCompanySubscriptionQuery>
{
    public GetPlatformCompanySubscriptionQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
    }
}

internal sealed class GetPlatformCompanySubscriptionsQueryValidator : AbstractValidator<GetPlatformCompanySubscriptionsQuery>
{
    public GetPlatformCompanySubscriptionsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PlatformSubscriptionValidationRules.MaxPageSize);
    }
}

internal sealed class SearchPlatformCompanySubscriptionsQueryValidator : AbstractValidator<SearchPlatformCompanySubscriptionsQuery>
{
    public SearchPlatformCompanySubscriptionsQueryValidator()
    {
        RuleFor(query => query.Search).MaximumLength(150);
        RuleFor(query => query.Status)
            .IsInEnum()
            .When(static query => query.Status.HasValue);
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PlatformSubscriptionValidationRules.MaxPageSize);
    }
}

internal sealed class PreviewPlatformCompanySubscriptionQueryValidator : AbstractValidator<PreviewPlatformCompanySubscriptionQuery>
{
    public PreviewPlatformCompanySubscriptionQueryValidator()
    {
        ApplyCommonRules();
    }

    private void ApplyCommonRules()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.CommercialPlanId).NotEmpty();
        RuleFor(query => query.StartDateUtc)
            .Must(static value => value != default);
        RuleFor(query => query.ExpiresAtUtc)
            .Must(static value => !value.HasValue || value.Value != default);
        RuleFor(query => query.Periodicity).IsInEnum();
    }
}

internal sealed class ActivatePlatformCompanySubscriptionCommandValidator : AbstractValidator<ActivatePlatformCompanySubscriptionCommand>
{
    public ActivatePlatformCompanySubscriptionCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.CommercialPlanId).NotEmpty();
        RuleFor(command => command.StartDateUtc)
            .Must(static value => value != default);
        RuleFor(command => command.ExpiresAtUtc)
            .Must(static value => !value.HasValue || value.Value != default);
        RuleFor(command => command.Periodicity).IsInEnum();
    }
}

internal sealed class ChangePlatformCompanySubscriptionStatusCommandValidator : AbstractValidator<ChangePlatformCompanySubscriptionStatusCommand>
{
    public ChangePlatformCompanySubscriptionStatusCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.SubscriptionId).NotEmpty();
        RuleFor(command => command.TargetStatus).IsInEnum();
        RuleFor(command => command.ReasonCode).IsInEnum();
        RuleFor(command => command.Observations).MaximumLength(2000);
    }
}

internal sealed class SearchPlatformCompanySubscriptionStatusHistoryQueryValidator : AbstractValidator<SearchPlatformCompanySubscriptionStatusHistoryQuery>
{
    public SearchPlatformCompanySubscriptionStatusHistoryQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.SubscriptionId).NotEmpty();
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PlatformSubscriptionValidationRules.MaxPageSize);
    }
}

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
    PlatformCompanySubscriptionPendingStatusChangeResponse? PendingStatusChange,
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

public sealed record PlatformCompanySubscriptionPendingStatusChangeResponse(
    SubscriptionStatus TargetStatus,
    DateTime EffectiveDateUtc,
    SubscriptionStatusChangeReasonCode ReasonCode,
    string? Observations,
    DateTime RequestedAtUtc,
    Guid? RequestedByUserId);

public sealed record PlatformCompanySubscriptionStatusChangePreviewResponse(
    Guid CompanyId,
    string CompanyName,
    string CompanySlug,
    CompanyStatus CompanyStatus,
    Guid SubscriptionId,
    SubscriptionStatus CurrentStatus,
    SubscriptionStatus TargetStatus,
    DateTime EffectiveDateUtc,
    string PlanCode,
    string PlanName,
    int PlanVersionNumber,
    DateTime? ExpiresAtUtc,
    bool CanOperate,
    bool CanGenerateCharges,
    bool IsEligible,
    IReadOnlyCollection<string> IneligibilityReasons);

public sealed record PlatformCompanySubscriptionPlanChangePreviewResponse(
    Guid CompanyId,
    string CompanyName,
    string CompanySlug,
    Guid CurrentSubscriptionId,
    Guid CurrentCommercialPlanId,
    Guid CurrentCommercialPlanVersionId,
    string CurrentPlanCode,
    string CurrentPlanName,
    int CurrentPlanVersionNumber,
    decimal CurrentBaseMonthlyFee,
    decimal CurrentPricePerActiveEmployee,
    CompanySubscriptionPeriodicity CurrentPeriodicity,
    string CurrentCurrencyCode,
    Guid TargetCommercialPlanId,
    Guid TargetCommercialPlanVersionId,
    string TargetPlanCode,
    string TargetPlanName,
    int TargetPlanVersionNumber,
    decimal TargetBaseMonthlyFee,
    decimal TargetPricePerActiveEmployee,
    CompanySubscriptionPeriodicity TargetPeriodicity,
    string TargetCurrencyCode,
    SubscriptionPlanChangeMode Mode,
    DateTime EffectiveDateUtc,
    int ActiveEmployeeCount,
    decimal EstimatedNextCharge,
    bool IsEligible,
    IReadOnlyCollection<string> IneligibilityReasons,
    IReadOnlyCollection<string> AddonCompatibilityWarnings);

public sealed record PlatformCompanySubscriptionPlanChangeResponse(
    Guid PlanChangeId,
    Guid CompanyId,
    Guid CurrentSubscriptionId,
    Guid CurrentCommercialPlanId,
    Guid CurrentCommercialPlanVersionId,
    string CurrentPlanCode,
    string CurrentPlanName,
    int CurrentPlanVersionNumber,
    decimal CurrentBaseMonthlyFee,
    decimal CurrentPricePerActiveEmployee,
    CompanySubscriptionPeriodicity CurrentPeriodicity,
    string CurrentCurrencyCode,
    Guid TargetCommercialPlanId,
    Guid TargetCommercialPlanVersionId,
    string TargetPlanCode,
    string TargetPlanName,
    int TargetPlanVersionNumber,
    decimal TargetBaseMonthlyFee,
    decimal TargetPricePerActiveEmployee,
    CompanySubscriptionPeriodicity TargetPeriodicity,
    string TargetCurrencyCode,
    SubscriptionPlanChangeMode Mode,
    SubscriptionPlanChangeStatus Status,
    SubscriptionPlanChangeReasonCode ReasonCode,
    DateTime RequestedAtUtc,
    DateTime EffectiveDateUtc,
    Guid? RequestedByUserId,
    string? Observations,
    int ActiveEmployeeCount,
    decimal EstimatedNextCharge,
    DateTime? AppliedAtUtc,
    Guid? AppliedSubscriptionId,
    DateTime? CancelledAtUtc,
    Guid? CancelledByUserId,
    string? CancellationObservations,
    DateTime? RejectedAtUtc,
    string? RejectionReason,
    Guid ConcurrencyToken);

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
    string? Observations,
    DateTime? EffectiveDateUtc)
    : ICommand<PlatformCompanySubscriptionResponse>;

public sealed record PreviewPlatformCompanySubscriptionStatusChangeQuery(
    Guid CompanyId,
    Guid SubscriptionId,
    SubscriptionStatus TargetStatus,
    SubscriptionStatusChangeReasonCode ReasonCode,
    string? Observations,
    DateTime? EffectiveDateUtc)
    : IQuery<PlatformCompanySubscriptionStatusChangePreviewResponse>;

public sealed record SearchPlatformCompanySubscriptionStatusHistoryQuery(
    Guid CompanyId,
    Guid SubscriptionId,
    int PageNumber = 1,
    int PageSize = PlatformSubscriptionValidationRules.DefaultPageSize)
    : IQuery<PagedResponse<PlatformCompanySubscriptionStatusTransitionResponse>>;

public sealed record PreviewPlatformCompanySubscriptionPlanChangeQuery(
    Guid CompanyId,
    Guid CommercialPlanId,
    SubscriptionPlanChangeMode Mode,
    DateTime? RequestedEffectiveDateUtc)
    : IQuery<PlatformCompanySubscriptionPlanChangePreviewResponse>;

public sealed record CreatePlatformCompanySubscriptionPlanChangeCommand(
    Guid CompanyId,
    Guid CommercialPlanId,
    SubscriptionPlanChangeMode Mode,
    DateTime? RequestedEffectiveDateUtc,
    SubscriptionPlanChangeReasonCode ReasonCode,
    string? Observations)
    : ICommand<PlatformCompanySubscriptionPlanChangeResponse>;

public sealed record SearchPlatformCompanySubscriptionPlanChangesQuery(
    Guid CompanyId,
    int PageNumber = 1,
    int PageSize = PlatformSubscriptionValidationRules.DefaultPageSize)
    : IQuery<PagedResponse<PlatformCompanySubscriptionPlanChangeResponse>>;

public sealed record CancelPlatformCompanySubscriptionPlanChangeCommand(
    Guid CompanyId,
    Guid PlanChangeId,
    string Observations,
    Guid ConcurrencyToken)
    : ICommand<PlatformCompanySubscriptionPlanChangeResponse>;

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
        RuleFor(command => command.EffectiveDateUtc)
            .Must(static value => !value.HasValue || value.Value != default);
    }
}

internal sealed class PreviewPlatformCompanySubscriptionStatusChangeQueryValidator : AbstractValidator<PreviewPlatformCompanySubscriptionStatusChangeQuery>
{
    public PreviewPlatformCompanySubscriptionStatusChangeQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.SubscriptionId).NotEmpty();
        RuleFor(query => query.TargetStatus).IsInEnum();
        RuleFor(query => query.ReasonCode).IsInEnum();
        RuleFor(query => query.Observations).MaximumLength(2000);
        RuleFor(query => query.EffectiveDateUtc)
            .Must(static value => !value.HasValue || value.Value != default);
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

internal sealed class PreviewPlatformCompanySubscriptionPlanChangeQueryValidator : AbstractValidator<PreviewPlatformCompanySubscriptionPlanChangeQuery>
{
    public PreviewPlatformCompanySubscriptionPlanChangeQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.CommercialPlanId).NotEmpty();
        RuleFor(query => query.Mode).IsInEnum();
        RuleFor(query => query.RequestedEffectiveDateUtc)
            .NotNull()
            .When(query => query.Mode == SubscriptionPlanChangeMode.SpecificDate);
    }
}

internal sealed class CreatePlatformCompanySubscriptionPlanChangeCommandValidator : AbstractValidator<CreatePlatformCompanySubscriptionPlanChangeCommand>
{
    public CreatePlatformCompanySubscriptionPlanChangeCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.CommercialPlanId).NotEmpty();
        RuleFor(command => command.Mode).IsInEnum();
        RuleFor(command => command.RequestedEffectiveDateUtc)
            .NotNull()
            .When(command => command.Mode == SubscriptionPlanChangeMode.SpecificDate);
        RuleFor(command => command.ReasonCode).IsInEnum();
        RuleFor(command => command.Observations).MaximumLength(2000);
    }
}

internal sealed class SearchPlatformCompanySubscriptionPlanChangesQueryValidator : AbstractValidator<SearchPlatformCompanySubscriptionPlanChangesQuery>
{
    public SearchPlatformCompanySubscriptionPlanChangesQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.PageNumber).GreaterThan(0);
        RuleFor(query => query.PageSize).InclusiveBetween(1, PlatformSubscriptionValidationRules.MaxPageSize);
    }
}

internal sealed class CancelPlatformCompanySubscriptionPlanChangeCommandValidator : AbstractValidator<CancelPlatformCompanySubscriptionPlanChangeCommand>
{
    public CancelPlatformCompanySubscriptionPlanChangeCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.PlanChangeId).NotEmpty();
        RuleFor(command => command.Observations)
            .NotEmpty()
            .MaximumLength(2000);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

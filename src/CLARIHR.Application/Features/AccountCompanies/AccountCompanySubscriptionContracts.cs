using CLARIHR.Application.Common.CQRS;
using CLARIHR.Domain.Companies;
using FluentValidation;

namespace CLARIHR.Application.Features.AccountCompanies;

public sealed record AccountCompanyEffectiveModuleResponse(
    string ModuleKey,
    string DisplayName,
    string Description,
    string Source,
    bool GrantedByPlan,
    bool GrantedByAddon);

public sealed record AccountCompanySubscriptionPlanResponse(
    Guid CommercialPlanId,
    string Code,
    string Name,
    string? Description,
    decimal BaseMonthlyFee,
    decimal PricePerActiveEmployee,
    int CurrentVersionNumber,
    string CurrencyCode,
    int ModuleCount,
    IReadOnlyCollection<string> ModuleKeys,
    bool IsCurrent);

public sealed record AccountCompanySubscriptionAddonResponse(
    Guid CompanyAddonId,
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
    CompanyAddonStatus Status,
    int ModuleCount,
    IReadOnlyCollection<string> ModuleKeys);

public sealed record AccountCompanySubscriptionOverviewResponse(
    Guid CompanyId,
    string CompanyName,
    string CompanySlug,
    string PlanCode,
    AccountCompanySubscriptionPlanResponse CurrentPlan,
    IReadOnlyCollection<AccountCompanySubscriptionAddonResponse> ActiveAddons,
    IReadOnlyCollection<AccountCompanyEffectiveModuleResponse> EffectiveModules,
    Guid ConcurrencyToken);

public sealed record AccountCompanySubscriptionPlanPreviewResponse(
    Guid CompanyId,
    AccountCompanySubscriptionPlanResponse CurrentPlan,
    AccountCompanySubscriptionPlanResponse TargetPlan,
    IReadOnlyCollection<string> AddedModuleKeys,
    IReadOnlyCollection<string> RemovedModuleKeys,
    IReadOnlyCollection<string> AddonDeactivationWarnings,
    bool IsEligible,
    IReadOnlyCollection<string> IneligibilityReasons);

public sealed record AccountCompanyMarketplaceAddonResponse(
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
    int ModuleCount,
    IReadOnlyCollection<string> ModuleKeys,
    bool IsOwned,
    bool CanAcquire,
    string? BlockedReason);

public sealed record AccountCompanyAddonChangePreviewResponse(
    Guid CompanyId,
    Guid CommercialAddonId,
    string AddonCode,
    string AddonName,
    SubscriptionAddonChangeAction Action,
    IReadOnlyCollection<string> AddedModuleKeys,
    IReadOnlyCollection<string> RemovedModuleKeys,
    bool IsEligible,
    IReadOnlyCollection<string> IneligibilityReasons,
    IReadOnlyCollection<string> Warnings);

public sealed record GetOwnedCompanySubscriptionQuery(Guid CompanyId)
    : IQuery<AccountCompanySubscriptionOverviewResponse>;

public sealed record GetOwnedCompanySubscriptionPlansQuery(Guid CompanyId)
    : IQuery<IReadOnlyCollection<AccountCompanySubscriptionPlanResponse>>;

public sealed record PreviewOwnedCompanySubscriptionPlanChangeQuery(Guid CompanyId, Guid CommercialPlanId)
    : IQuery<AccountCompanySubscriptionPlanPreviewResponse>;

public sealed record ChangeOwnedCompanySubscriptionCommand(Guid CompanyId, Guid CommercialPlanId, string? Observations, Guid ConcurrencyToken)
    : ICommand<AccountCompanySubscriptionOverviewResponse>;

public sealed record GetOwnedCompanySubscriptionAddonsQuery(Guid CompanyId)
    : IQuery<IReadOnlyCollection<AccountCompanySubscriptionAddonResponse>>;

public sealed record GetOwnedCompanySubscriptionMarketplaceQuery(Guid CompanyId)
    : IQuery<IReadOnlyCollection<AccountCompanyMarketplaceAddonResponse>>;

public sealed record PreviewOwnedCompanyAddonChangeQuery(
    Guid CompanyId,
    Guid CommercialAddonId,
    SubscriptionAddonChangeAction Action)
    : IQuery<AccountCompanyAddonChangePreviewResponse>;

public sealed record CreateOwnedCompanyAddonChangeCommand(
    Guid CompanyId,
    Guid CommercialAddonId,
    SubscriptionAddonChangeAction Action,
    string? Observations,
    Guid ConcurrencyToken)
    : ICommand<AccountCompanySubscriptionOverviewResponse>;

internal sealed class GetOwnedCompanySubscriptionQueryValidator : AbstractValidator<GetOwnedCompanySubscriptionQuery>
{
    public GetOwnedCompanySubscriptionQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
    }
}

internal sealed class GetOwnedCompanySubscriptionPlansQueryValidator : AbstractValidator<GetOwnedCompanySubscriptionPlansQuery>
{
    public GetOwnedCompanySubscriptionPlansQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
    }
}

internal sealed class PreviewOwnedCompanySubscriptionPlanChangeQueryValidator : AbstractValidator<PreviewOwnedCompanySubscriptionPlanChangeQuery>
{
    public PreviewOwnedCompanySubscriptionPlanChangeQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.CommercialPlanId).NotEmpty();
    }
}

internal sealed class ChangeOwnedCompanySubscriptionCommandValidator : AbstractValidator<ChangeOwnedCompanySubscriptionCommand>
{
    public ChangeOwnedCompanySubscriptionCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.CommercialPlanId).NotEmpty();
        RuleFor(command => command.Observations).MaximumLength(2000);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class GetOwnedCompanySubscriptionAddonsQueryValidator : AbstractValidator<GetOwnedCompanySubscriptionAddonsQuery>
{
    public GetOwnedCompanySubscriptionAddonsQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
    }
}

internal sealed class GetOwnedCompanySubscriptionMarketplaceQueryValidator : AbstractValidator<GetOwnedCompanySubscriptionMarketplaceQuery>
{
    public GetOwnedCompanySubscriptionMarketplaceQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
    }
}

internal sealed class PreviewOwnedCompanyAddonChangeQueryValidator : AbstractValidator<PreviewOwnedCompanyAddonChangeQuery>
{
    public PreviewOwnedCompanyAddonChangeQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.CommercialAddonId).NotEmpty();
        RuleFor(query => query.Action).IsInEnum();
    }
}

internal sealed class CreateOwnedCompanyAddonChangeCommandValidator : AbstractValidator<CreateOwnedCompanyAddonChangeCommand>
{
    public CreateOwnedCompanyAddonChangeCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.CommercialAddonId).NotEmpty();
        RuleFor(command => command.Action).IsInEnum();
        RuleFor(command => command.Observations).MaximumLength(2000);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

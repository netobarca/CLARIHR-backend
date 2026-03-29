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
    string PlanCode,
    string PlanName,
    decimal BaseMonthlyFee,
    decimal PricePerActiveEmployee,
    SubscriptionStatus Status,
    DateTime StartDateUtc,
    DateTime? EndDateUtc,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc);

public sealed record GetPlatformCompanySubscriptionQuery(Guid CompanyId)
    : IQuery<PlatformCompanySubscriptionResponse>;

public sealed record GetPlatformCompanySubscriptionsQuery(
    Guid CompanyId,
    int PageNumber = 1,
    int PageSize = PlatformSubscriptionValidationRules.DefaultPageSize)
    : IQuery<PagedResponse<PlatformCompanySubscriptionResponse>>;

public sealed record ReplacePlatformCompanySubscriptionCommand(
    Guid CompanyId,
    Guid CommercialPlanId) : ICommand<PlatformCompanySubscriptionResponse>;

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

internal sealed class ReplacePlatformCompanySubscriptionCommandValidator : AbstractValidator<ReplacePlatformCompanySubscriptionCommand>
{
    public ReplacePlatformCompanySubscriptionCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.CommercialPlanId).NotEmpty();
    }
}

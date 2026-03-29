using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Platform;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.PlatformSubscriptions.Common;
using CLARIHR.Domain.Companies;

namespace CLARIHR.Application.Features.PlatformSubscriptions;

internal sealed class GetPlatformCompanySubscriptionQueryHandler(
    IPlatformAuthorizationService authorizationService,
    ICompanyRepository companyRepository,
    ICompanySubscriptionRepository subscriptionRepository)
    : IQueryHandler<GetPlatformCompanySubscriptionQuery, PlatformCompanySubscriptionResponse>
{
    public async Task<Result<PlatformCompanySubscriptionResponse>> Handle(
        GetPlatformCompanySubscriptionQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PlatformCompanySubscriptionResponse>.Failure(authorizationResult.Error);
        }

        if (await companyRepository.FindByPublicIdAsync(query.CompanyId, cancellationToken) is null)
        {
            return Result<PlatformCompanySubscriptionResponse>.Failure(PlatformSubscriptionErrors.CompanyNotFound);
        }

        var subscription = await subscriptionRepository.GetCurrentByCompanyPublicIdAsync(query.CompanyId, cancellationToken);
        return subscription is null
            ? Result<PlatformCompanySubscriptionResponse>.Failure(PlatformSubscriptionErrors.SubscriptionNotFound)
            : Result<PlatformCompanySubscriptionResponse>.Success(subscription);
    }

    private static PlatformCompanySubscriptionResponse Map(
        CompanySubscription subscription,
        Guid companyPublicId,
        Guid commercialPlanPublicId) =>
        new(
            subscription.PublicId,
            companyPublicId,
            commercialPlanPublicId,
            subscription.PlanCode,
            subscription.PlanName,
            subscription.BaseMonthlyFee,
            subscription.PricePerActiveEmployee,
            subscription.Status,
            subscription.StartDateUtc,
            subscription.EndDateUtc,
            subscription.CreatedUtc,
            subscription.ModifiedUtc);
}

internal sealed class GetPlatformCompanySubscriptionsQueryHandler(
    IPlatformAuthorizationService authorizationService,
    ICompanyRepository companyRepository,
    ICompanySubscriptionRepository subscriptionRepository)
    : IQueryHandler<GetPlatformCompanySubscriptionsQuery, PagedResponse<PlatformCompanySubscriptionResponse>>
{
    public async Task<Result<PagedResponse<PlatformCompanySubscriptionResponse>>> Handle(
        GetPlatformCompanySubscriptionsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<PlatformCompanySubscriptionResponse>>.Failure(authorizationResult.Error);
        }

        if (await companyRepository.FindByPublicIdAsync(query.CompanyId, cancellationToken) is null)
        {
            return Result<PagedResponse<PlatformCompanySubscriptionResponse>>.Failure(PlatformSubscriptionErrors.CompanyNotFound);
        }

        var response = await subscriptionRepository.SearchByCompanyPublicIdAsync(
            query.CompanyId,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        return Result<PagedResponse<PlatformCompanySubscriptionResponse>>.Success(response);
    }
}

internal sealed class ReplacePlatformCompanySubscriptionCommandHandler(
    IPlatformAuthorizationService authorizationService,
    ICompanyRepository companyRepository,
    ICommercialPlanRepository commercialPlanRepository,
    ICompanySubscriptionRepository subscriptionRepository,
    IPlatformAuditService platformAuditService,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork) : ICommandHandler<ReplacePlatformCompanySubscriptionCommand, PlatformCompanySubscriptionResponse>
{
    public async Task<Result<PlatformCompanySubscriptionResponse>> Handle(
        ReplacePlatformCompanySubscriptionCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PlatformCompanySubscriptionResponse>.Failure(authorizationResult.Error);
        }

        var company = await companyRepository.FindByPublicIdAsync(command.CompanyId, cancellationToken);
        if (company is null)
        {
            return Result<PlatformCompanySubscriptionResponse>.Failure(PlatformSubscriptionErrors.CompanyNotFound);
        }

        var plan = await commercialPlanRepository.GetByIdAsync(command.CommercialPlanId, cancellationToken);
        if (plan is null)
        {
            return Result<PlatformCompanySubscriptionResponse>.Failure(PlatformSubscriptionErrors.PlanNotFound);
        }

        if (plan.Status != CommercialPlanStatus.Active)
        {
            return Result<PlatformCompanySubscriptionResponse>.Failure(PlatformSubscriptionErrors.PlanInactive);
        }

        var before = await subscriptionRepository.GetCurrentByCompanyPublicIdAsync(command.CompanyId, cancellationToken);
        var currentSubscription = await subscriptionRepository.GetActiveByCompanyIdAsync(company.Id, cancellationToken);
        if (currentSubscription is not null && currentSubscription.CommercialPlanId == plan.Id)
        {
            return Result<PlatformCompanySubscriptionResponse>.Failure(PlatformSubscriptionErrors.AlreadyAssigned);
        }

        var utcNow = dateTimeProvider.UtcNow;

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            if (currentSubscription is not null)
            {
                currentSubscription.Cancel(utcNow);
            }

            var nextSubscription = CompanySubscription.Activate(company.Id, plan, utcNow);
            subscriptionRepository.Add(nextSubscription);

            var after = Map(nextSubscription, command.CompanyId, plan.PublicId);
            await platformAuditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CompanySubscriptionReplaced,
                    AuditEntityTypes.CompanySubscription,
                    nextSubscription.PublicId,
                    company.Slug,
                    AuditActions.Update,
                    $"Replaced active subscription for {company.Name} with plan {plan.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return Result<PlatformCompanySubscriptionResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static PlatformCompanySubscriptionResponse Map(
        CompanySubscription subscription,
        Guid companyPublicId,
        Guid commercialPlanPublicId) =>
        new(
            subscription.PublicId,
            companyPublicId,
            commercialPlanPublicId,
            subscription.PlanCode,
            subscription.PlanName,
            subscription.BaseMonthlyFee,
            subscription.PricePerActiveEmployee,
            subscription.Status,
            subscription.StartDateUtc,
            subscription.EndDateUtc,
            subscription.CreatedUtc,
            subscription.ModifiedUtc);
}

using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Domain.Companies;
using CLARIHR.Infrastructure.Configuration;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CLARIHR.Infrastructure.Companies;

internal sealed class CompanySubscriptionLifecycleProcessor(
    ApplicationDbContext dbContext,
    ICompanySubscriptionRepository subscriptionRepository,
    IPlatformAuditService platformAuditService,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork,
    IOptions<CompanySubscriptionLifecycleOptions> options,
    ILogger<CompanySubscriptionLifecycleProcessor> logger)
{
    public async Task<int> PromoteDueScheduledSubscriptionsAsync(CancellationToken cancellationToken)
    {
        var utcNow = dateTimeProvider.UtcNow;
        var dueSubscriptionIds = await subscriptionRepository.GetDueScheduledSubscriptionIdsAsync(
            utcNow,
            Math.Max(1, options.Value.ScheduledPromotionBatchSize),
            cancellationToken);

        var promotedCount = 0;
        foreach (var subscriptionPublicId in dueSubscriptionIds)
        {
            if (await PromoteSingleAsync(subscriptionPublicId, utcNow, cancellationToken))
            {
                promotedCount++;
            }
        }

        return promotedCount;
    }

    private async Task<bool> PromoteSingleAsync(Guid subscriptionPublicId, DateTime utcNow, CancellationToken cancellationToken)
    {
        var scheduledSubscription = await subscriptionRepository.GetByPublicIdAsync(subscriptionPublicId, cancellationToken);
        if (scheduledSubscription is null ||
            scheduledSubscription.Status != SubscriptionStatus.Scheduled ||
            scheduledSubscription.StartDateUtc > utcNow)
        {
            return false;
        }

        var company = await dbContext.Companies
            .SingleOrDefaultAsync(item => item.Id == scheduledSubscription.CompanyId, cancellationToken);
        if (company is null)
        {
            logger.LogWarning(
                "Skipping scheduled subscription promotion for {SubscriptionPublicId} because the company no longer exists.",
                subscriptionPublicId);
            return false;
        }

        var before = await subscriptionRepository.GetOverviewByCompanyPublicIdAsync(company.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var activeSubscription = await subscriptionRepository.GetActiveByCompanyIdAsync(company.Id, cancellationToken);
            if (activeSubscription is not null)
            {
                activeSubscription.Cancel(utcNow);
            }

            scheduledSubscription.PromoteScheduled(utcNow);

            var planIsSystem = await dbContext.CommercialPlans
                .AsNoTracking()
                .Where(plan => plan.Id == scheduledSubscription.CommercialPlanId)
                .Select(plan => plan.IsSystemPlan)
                .SingleAsync(cancellationToken);

            if (planIsSystem)
            {
                company.ClearBillable();
            }
            else
            {
                company.MarkBillable(utcNow);
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await subscriptionRepository.GetOverviewByCompanyPublicIdAsync(company.PublicId, cancellationToken);
            await platformAuditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CompanySubscriptionPromotionProcessed,
                    AuditEntityTypes.CompanySubscription,
                    scheduledSubscription.PublicId,
                    company.Slug,
                    AuditActions.Update,
                    $"Promoted scheduled commercial subscription for {company.Name}.",
                    Before: before,
                    After: after),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Promoted scheduled subscription {SubscriptionPublicId} for company {CompanyPublicId}.",
                scheduledSubscription.PublicId,
                company.PublicId);

            return true;
        }
        catch (DbUpdateException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogWarning(
                exception,
                "Scheduled subscription promotion for {SubscriptionPublicId} was skipped because another process likely completed it first.",
                subscriptionPublicId);
            return false;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

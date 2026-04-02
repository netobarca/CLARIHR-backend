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
    ICommercialPlanRepository commercialPlanRepository,
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

    public async Task<int> ExpireDueSubscriptionsAsync(CancellationToken cancellationToken)
    {
        var utcNow = dateTimeProvider.UtcNow;
        var dueSubscriptionIds = await subscriptionRepository.GetDueExpiringSubscriptionIdsAsync(
            utcNow,
            Math.Max(1, options.Value.ExpirationBatchSize),
            cancellationToken);

        var expiredCount = 0;
        foreach (var subscriptionPublicId in dueSubscriptionIds)
        {
            if (await ExpireSingleAsync(subscriptionPublicId, utcNow, cancellationToken))
            {
                expiredCount++;
            }
        }

        return expiredCount;
    }

    private async Task<bool> PromoteSingleAsync(Guid subscriptionPublicId, DateTime utcNow, CancellationToken cancellationToken)
    {
        var scheduledSubscription = await subscriptionRepository.GetByPublicIdAsync(subscriptionPublicId, cancellationToken);
        if (scheduledSubscription is null ||
            scheduledSubscription.Status != SubscriptionStatus.Scheduled ||
            scheduledSubscription.StartDateUtc > utcNow.Date)
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
            var currentSubscription = await subscriptionRepository.GetCurrentByCompanyIdAsync(company.Id, cancellationToken);
            if (currentSubscription is not null &&
                currentSubscription.PublicId != scheduledSubscription.PublicId)
            {
                currentSubscription.Cancel(
                    utcNow,
                    SubscriptionStatusChangeReasonCode.SubscriptionReplacement,
                    observations: null,
                    SubscriptionStatusChangeOrigin.SystemProcess,
                    actorUserPublicId: null);
            }

            scheduledSubscription.PromoteScheduled(utcNow);

            var planIsSystem = await commercialPlanRepository.IsSystemPlanAsync(
                scheduledSubscription.CommercialPlanId,
                cancellationToken);
            if (planIsSystem || !SubscriptionStatusPolicy.CanGenerateCharges(scheduledSubscription.Status))
            {
                company.ClearBillable();
            }
            else
            {
                company.MarkBillable(utcNow.Date);
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

    private async Task<bool> ExpireSingleAsync(Guid subscriptionPublicId, DateTime utcNow, CancellationToken cancellationToken)
    {
        var activeSubscription = await subscriptionRepository.GetByPublicIdAsync(subscriptionPublicId, cancellationToken);
        if (activeSubscription is null ||
            activeSubscription.Status != SubscriptionStatus.Active ||
            !activeSubscription.ExpiresAtUtc.HasValue ||
            activeSubscription.ExpiresAtUtc.Value.Date > utcNow.Date)
        {
            return false;
        }

        var company = await dbContext.Companies
            .SingleOrDefaultAsync(item => item.Id == activeSubscription.CompanyId, cancellationToken);
        if (company is null)
        {
            logger.LogWarning(
                "Skipping company subscription expiration for {SubscriptionPublicId} because the company no longer exists.",
                subscriptionPublicId);
            return false;
        }

        var before = await subscriptionRepository.GetOverviewByCompanyPublicIdAsync(company.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            activeSubscription.Expire(utcNow);
            company.ClearBillable();

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await subscriptionRepository.GetOverviewByCompanyPublicIdAsync(company.PublicId, cancellationToken);
            await platformAuditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CompanySubscriptionExpirationProcessed,
                    AuditEntityTypes.CompanySubscription,
                    activeSubscription.PublicId,
                    company.Slug,
                    AuditActions.Update,
                    $"Expired company subscription for {company.Name}.",
                    Before: before,
                    After: after),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Expired company subscription {SubscriptionPublicId} for company {CompanyPublicId}.",
                activeSubscription.PublicId,
                company.PublicId);

            return true;
        }
        catch (DbUpdateException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogWarning(
                exception,
                "Company subscription expiration for {SubscriptionPublicId} was skipped because another process likely completed it first.",
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

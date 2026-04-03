using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.PlatformSubscriptions;
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

    public async Task<int> ApplyDueScheduledStatusChangesAsync(CancellationToken cancellationToken)
    {
        var utcNow = dateTimeProvider.UtcNow;
        var dueStatusChangeRequestIds = await subscriptionRepository.GetDueScheduledStatusChangeRequestIdsAsync(
            utcNow,
            Math.Max(1, options.Value.ScheduledPromotionBatchSize),
            cancellationToken);

        var appliedCount = 0;
        foreach (var statusChangeRequestPublicId in dueStatusChangeRequestIds)
        {
            if (await ApplyScheduledStatusChangeAsync(statusChangeRequestPublicId, utcNow, cancellationToken))
            {
                appliedCount++;
            }
        }

        return appliedCount;
    }

    public async Task<int> ApplyDueScheduledPlanChangesAsync(CancellationToken cancellationToken)
    {
        var utcNow = dateTimeProvider.UtcNow;
        var duePlanChangeIds = await subscriptionRepository.GetDueScheduledPlanChangeIdsAsync(
            utcNow,
            Math.Max(1, options.Value.ScheduledPromotionBatchSize),
            cancellationToken);

        var appliedCount = 0;
        foreach (var planChangePublicId in duePlanChangeIds)
        {
            if (await ApplyScheduledPlanChangeAsync(planChangePublicId, utcNow, cancellationToken))
            {
                appliedCount++;
            }
        }

        return appliedCount;
    }

    public async Task<int> ApplyDueScheduledAddonChangesAsync(CancellationToken cancellationToken)
    {
        var utcNow = dateTimeProvider.UtcNow;
        var dueAddonChangeIds = await subscriptionRepository.GetDueScheduledAddonChangeIdsAsync(
            utcNow,
            Math.Max(1, options.Value.ScheduledPromotionBatchSize),
            cancellationToken);

        var appliedCount = 0;
        foreach (var addonChangePublicId in dueAddonChangeIds)
        {
            if (await ApplyScheduledAddonChangeAsync(addonChangePublicId, utcNow, cancellationToken))
            {
                appliedCount++;
            }
        }

        return appliedCount;
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

    private async Task<bool> ApplyScheduledStatusChangeAsync(
        Guid statusChangeRequestPublicId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var statusChangeRequest = await subscriptionRepository.GetStatusChangeRequestByPublicIdAsync(
            statusChangeRequestPublicId,
            cancellationToken);

        if (statusChangeRequest is null ||
            statusChangeRequest.Status != SubscriptionStatusChangeRequestStatus.Scheduled ||
            statusChangeRequest.EffectiveDateUtc > utcNow.Date)
        {
            return false;
        }

        var company = await dbContext.Companies
            .SingleOrDefaultAsync(item => item.Id == statusChangeRequest.CompanyId, cancellationToken);
        if (company is null)
        {
            logger.LogWarning(
                "Skipping scheduled subscription status change {StatusChangeRequestPublicId} because the company no longer exists.",
                statusChangeRequestPublicId);
            return false;
        }

        var before = await subscriptionRepository.GetOverviewByCompanyPublicIdAsync(company.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var subscription = await dbContext.CompanySubscriptions
                .Include(item => item.StatusTransitions)
                .SingleOrDefaultAsync(item => item.Id == statusChangeRequest.CompanySubscriptionId, cancellationToken);

            if (subscription is null)
            {
                await RejectStatusChangeAsync(
                    statusChangeRequest,
                    company,
                    before,
                    "The subscription no longer exists.",
                    utcNow,
                    cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return false;
            }

            if (company.Status != CompanyStatus.Active)
            {
                await RejectStatusChangeAsync(
                    statusChangeRequest,
                    company,
                    before,
                    "The company is no longer active.",
                    utcNow,
                    cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return false;
            }

            if (subscription.Status != statusChangeRequest.CurrentStatus)
            {
                await RejectStatusChangeAsync(
                    statusChangeRequest,
                    company,
                    before,
                    "The subscription status changed before the scheduled reactivation could be applied.",
                    utcNow,
                    cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return false;
            }

            if (!SubscriptionStatusPolicy.CanTransition(
                    subscription.Status,
                    statusChangeRequest.TargetStatus,
                    SubscriptionStatusChangeOrigin.SystemProcess) ||
                !SubscriptionStatusPolicy.IsReasonAllowed(
                    subscription.Status,
                    statusChangeRequest.TargetStatus,
                    SubscriptionStatusChangeOrigin.SystemProcess,
                    statusChangeRequest.ReasonCode))
            {
                await RejectStatusChangeAsync(
                    statusChangeRequest,
                    company,
                    before,
                    "The subscription no longer supports the scheduled reactivation transition.",
                    utcNow,
                    cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return false;
            }

            if (subscription.ExpiresAtUtc.HasValue &&
                statusChangeRequest.EffectiveDateUtc > subscription.ExpiresAtUtc.Value.Date)
            {
                await RejectStatusChangeAsync(
                    statusChangeRequest,
                    company,
                    before,
                    "The subscription expiration date is earlier than the scheduled reactivation date.",
                    utcNow,
                    cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return false;
            }

            subscription.Reactivate(
                utcNow,
                statusChangeRequest.ReasonCode,
                statusChangeRequest.Observations,
                SubscriptionStatusChangeOrigin.SystemProcess,
                actorUserPublicId: null);

            var planIsSystem = await commercialPlanRepository.IsSystemPlanAsync(
                subscription.CommercialPlanId,
                cancellationToken);
            if (planIsSystem || !SubscriptionStatusPolicy.CanGenerateCharges(subscription.Status))
            {
                company.ClearBillable();
            }
            else
            {
                company.MarkBillable(utcNow.Date);
            }

            statusChangeRequest.MarkApplied(utcNow);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await subscriptionRepository.GetOverviewByCompanyPublicIdAsync(company.PublicId, cancellationToken);
            await platformAuditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CompanySubscriptionStatusChangeApplied,
                    AuditEntityTypes.CompanySubscriptionStatusChangeRequest,
                    statusChangeRequest.PublicId,
                    company.Slug,
                    AuditActions.Update,
                    $"Applied scheduled subscription reactivation for {company.Name}.",
                    Before: before,
                    After: after),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Applied scheduled subscription status change {StatusChangeRequestPublicId} for company {CompanyPublicId}.",
                statusChangeRequest.PublicId,
                company.PublicId);

            return true;
        }
        catch (DbUpdateException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogWarning(
                exception,
                "Scheduled subscription status change {StatusChangeRequestPublicId} was skipped because another process likely completed it first.",
                statusChangeRequestPublicId);
            return false;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<bool> ApplyScheduledPlanChangeAsync(Guid planChangePublicId, DateTime utcNow, CancellationToken cancellationToken)
    {
        var planChange = await subscriptionRepository.GetPlanChangeByPublicIdAsync(planChangePublicId, cancellationToken);
        if (planChange is null ||
            planChange.Status != SubscriptionPlanChangeStatus.Scheduled ||
            planChange.EffectiveDateUtc > utcNow.Date)
        {
            return false;
        }

        var company = await dbContext.Companies
            .SingleOrDefaultAsync(item => item.Id == planChange.CompanyId, cancellationToken);
        if (company is null)
        {
            logger.LogWarning(
                "Skipping scheduled plan change {PlanChangePublicId} because the company no longer exists.",
                planChangePublicId);
            return false;
        }

        var before = await subscriptionRepository.GetOverviewByCompanyPublicIdAsync(company.PublicId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var currentSubscription = await subscriptionRepository.GetCurrentByCompanyIdAsync(company.Id, cancellationToken);
            if (currentSubscription is null ||
                currentSubscription.Status != SubscriptionStatus.Active ||
                currentSubscription.Id != planChange.CompanySubscriptionId)
            {
                await RejectPlanChangeAsync(
                    planChange,
                    company,
                    before,
                    "The current subscription changed before the scheduled plan change could be applied.",
                    utcNow,
                    cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return false;
            }

            var targetPlan = await commercialPlanRepository.GetByInternalIdAsync(planChange.TargetCommercialPlanId, cancellationToken);
            if (targetPlan is null || targetPlan.Status != CommercialPlanStatus.Active)
            {
                await RejectPlanChangeAsync(
                    planChange,
                    company,
                    before,
                    "The target plan is no longer active or available.",
                    utcNow,
                    cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return false;
            }

            CommercialPlanVersion? targetVersion;
            try
            {
                targetVersion = targetPlan.GetVersionEffectiveOn(planChange.EffectiveDateUtc);
            }
            catch (InvalidOperationException)
            {
                targetVersion = null;
            }

            if (targetVersion is null || targetVersion.Id != planChange.TargetCommercialPlanVersionId)
            {
                await RejectPlanChangeAsync(
                    planChange,
                    company,
                    before,
                    "The target plan version is no longer valid for the scheduled effective date.",
                    utcNow,
                    cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return false;
            }

            currentSubscription.Cancel(
                utcNow,
                SubscriptionStatusChangeReasonCode.SubscriptionReplacement,
                observations: null,
                SubscriptionStatusChangeOrigin.SystemProcess,
                actorUserPublicId: null);

            var nextSubscription = CompanySubscription.Activate(
                company.Id,
                targetPlan,
                currentSubscription.Periodicity,
                planChange.EffectiveDateUtc,
                currentSubscription.ExpiresAtUtc,
                Guid.Empty,
                utcNow,
                SubscriptionStatusChangeReasonCode.PlanChangeApplied,
                SubscriptionStatusChangeOrigin.SystemProcess,
                planChange.Observations);

            subscriptionRepository.Add(nextSubscription);
            planChange.MarkApplied(utcNow, nextSubscription.PublicId);

            var planIsSystem = await commercialPlanRepository.IsSystemPlanAsync(nextSubscription.CommercialPlanId, cancellationToken);
            if (planIsSystem || !SubscriptionStatusPolicy.CanGenerateCharges(nextSubscription.Status))
            {
                company.ClearBillable();
            }
            else
            {
                company.MarkBillable(utcNow.Date);
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await subscriptionRepository.GetPlanChangeResponseByPublicIdAsync(
                company.PublicId,
                planChange.PublicId,
                cancellationToken);

            await platformAuditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CompanySubscriptionPlanChangeApplied,
                    AuditEntityTypes.CompanySubscriptionPlanChange,
                    planChange.PublicId,
                    company.Slug,
                    AuditActions.Update,
                    $"Applied scheduled plan change for {company.Name}.",
                    Before: before,
                    After: response),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Applied scheduled plan change {PlanChangePublicId} for company {CompanyPublicId}.",
                planChange.PublicId,
                company.PublicId);

            return true;
        }
        catch (DbUpdateException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogWarning(
                exception,
                "Scheduled plan change {PlanChangePublicId} was skipped because another process likely completed it first.",
                planChangePublicId);
            return false;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task<bool> ApplyScheduledAddonChangeAsync(Guid addonChangePublicId, DateTime utcNow, CancellationToken cancellationToken)
    {
        var addonChange = await subscriptionRepository.GetAddonChangeByPublicIdAsync(addonChangePublicId, cancellationToken);
        if (addonChange is null ||
            addonChange.Status != SubscriptionAddonChangeStatus.Scheduled ||
            addonChange.EffectiveDateUtc > utcNow.Date)
        {
            return false;
        }

        var company = await dbContext.Companies
            .SingleOrDefaultAsync(item => item.Id == addonChange.CompanyId, cancellationToken);
        if (company is null)
        {
            logger.LogWarning(
                "Skipping scheduled add-on change {AddonChangePublicId} because the company no longer exists.",
                addonChangePublicId);
            return false;
        }

        var before = await subscriptionRepository.SearchCompanyAddonsByCompanyPublicIdAsync(
            company.PublicId,
            null,
            null,
            1,
            100,
            cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var currentSubscription = await subscriptionRepository.GetActiveByCompanyIdAsync(company.Id, cancellationToken);
            if (currentSubscription is null ||
                currentSubscription.Id != addonChange.CompanySubscriptionId)
            {
                await RejectAddonChangeAsync(
                    addonChange,
                    company,
                    "The current subscription changed before the scheduled add-on change could be applied.",
                    utcNow,
                    cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return false;
            }

            var companyAddon = await subscriptionRepository.GetCompanyAddonByCompanyIdAndAddonIdAsync(
                company.Id,
                addonChange.CommercialAddonId,
                cancellationToken);

            if (companyAddon is null)
            {
                await RejectAddonChangeAsync(
                    addonChange,
                    company,
                    "The company add-on state could not be found when applying the scheduled change.",
                    utcNow,
                    cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return false;
            }

            var commercialAddon = await dbContext.CommercialAddons
                .SingleOrDefaultAsync(addon => addon.Id == addonChange.CommercialAddonId, cancellationToken);

            if (commercialAddon is null)
            {
                await RejectAddonChangeAsync(
                    addonChange,
                    company,
                    "The target add-on no longer exists.",
                    utcNow,
                    cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return false;
            }

            if (addonChange.Action == SubscriptionAddonChangeAction.Activate)
            {
                if (commercialAddon.Status != CommercialAddonStatus.Active)
                {
                    await RejectAddonChangeAsync(
                        addonChange,
                        company,
                        "The target add-on is no longer active in the commercial catalog.",
                        utcNow,
                        cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return false;
                }

                if (companyAddon.Status != CompanyAddonStatus.PendingActivation)
                {
                    await RejectAddonChangeAsync(
                        addonChange,
                        company,
                        "The company add-on state is no longer pending activation.",
                        utcNow,
                        cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return false;
                }

                companyAddon.ApplyActivation(currentSubscription, commercialAddon, addonChange.EffectiveDateUtc);
            }
            else
            {
                if (companyAddon.Status != CompanyAddonStatus.PendingDeactivation)
                {
                    await RejectAddonChangeAsync(
                        addonChange,
                        company,
                        "The company add-on state is no longer pending deactivation.",
                        utcNow,
                        cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return false;
                }

                companyAddon.ApplyDeactivation(addonChange.EffectiveDateUtc);
            }

            addonChange.MarkApplied(utcNow, currentSubscription.PublicId);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await subscriptionRepository.GetAddonChangeResponseByPublicIdAsync(
                company.PublicId,
                addonChange.PublicId,
                cancellationToken);

            await platformAuditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CompanySubscriptionAddonChangeApplied,
                    AuditEntityTypes.CompanyCommercialAddonChange,
                    addonChange.PublicId,
                    company.Slug,
                    AuditActions.Update,
                    $"Applied scheduled add-on change for {company.Name} and add-on {addonChange.AddonCode}.",
                    Before: before,
                    After: response),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "Applied scheduled add-on change {AddonChangePublicId} for company {CompanyPublicId}.",
                addonChange.PublicId,
                company.PublicId);

            return true;
        }
        catch (DbUpdateException exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogWarning(
                exception,
                "Scheduled add-on change {AddonChangePublicId} was skipped because another process likely completed it first.",
                addonChangePublicId);
            return false;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task RejectPlanChangeAsync(
        CompanySubscriptionPlanChange planChange,
        Company company,
        PlatformCompanySubscriptionOverviewResponse? before,
        string rejectionReason,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        planChange.Reject(utcNow, rejectionReason);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = await subscriptionRepository.GetPlanChangeResponseByPublicIdAsync(
            company.PublicId,
            planChange.PublicId,
            cancellationToken);

        await platformAuditService.LogAsync(
            new AuditLogEntry(
                AuditEventTypes.CompanySubscriptionPlanChangeRejected,
                AuditEntityTypes.CompanySubscriptionPlanChange,
                planChange.PublicId,
                company.Slug,
                AuditActions.Update,
                $"Rejected scheduled plan change for {company.Name}.",
                Before: before,
                After: response),
            cancellationToken);

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "Rejected scheduled plan change {PlanChangePublicId} for company {CompanyPublicId}: {Reason}",
            planChange.PublicId,
            company.PublicId,
            rejectionReason);
    }

    private async Task RejectAddonChangeAsync(
        CompanyCommercialAddonChange addonChange,
        Company company,
        string rejectionReason,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var companyAddon = await subscriptionRepository.GetCompanyAddonByCompanyIdAndAddonIdAsync(
            company.Id,
            addonChange.CommercialAddonId,
            cancellationToken);

        addonChange.Reject(utcNow, rejectionReason);
        companyAddon?.RestoreStatus(addonChange.PreviousStatus, utcNow);

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        var response = await subscriptionRepository.GetAddonChangeResponseByPublicIdAsync(
            company.PublicId,
            addonChange.PublicId,
            cancellationToken);

        await platformAuditService.LogAsync(
            new AuditLogEntry(
                AuditEventTypes.CompanySubscriptionAddonChangeRejected,
                AuditEntityTypes.CompanyCommercialAddonChange,
                addonChange.PublicId,
                company.Slug,
                AuditActions.Update,
                $"Rejected scheduled add-on change for {company.Name} and add-on {addonChange.AddonCode}.",
                Before: null,
                After: response),
            cancellationToken);

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "Rejected scheduled add-on change {AddonChangePublicId} for company {CompanyPublicId}: {Reason}",
            addonChange.PublicId,
            company.PublicId,
            rejectionReason);
    }

    private async Task RejectStatusChangeAsync(
        CompanySubscriptionStatusChangeRequest statusChangeRequest,
        Company company,
        PlatformCompanySubscriptionOverviewResponse? before,
        string rejectionReason,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        statusChangeRequest.Reject(utcNow, rejectionReason);
        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        await platformAuditService.LogAsync(
            new AuditLogEntry(
                AuditEventTypes.CompanySubscriptionStatusChangeRejected,
                AuditEntityTypes.CompanySubscriptionStatusChangeRequest,
                statusChangeRequest.PublicId,
                company.Slug,
                AuditActions.Update,
                $"Rejected scheduled subscription reactivation for {company.Name}.",
                Before: before,
                After: new
                {
                    statusChangeRequest.PublicId,
                    statusChangeRequest.Status,
                    statusChangeRequest.TargetStatus,
                    statusChangeRequest.EffectiveDateUtc,
                    statusChangeRequest.RejectionReason
                }),
            cancellationToken);

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "Rejected scheduled subscription status change {StatusChangeRequestPublicId} for company {CompanyPublicId}: {Reason}",
            statusChangeRequest.PublicId,
            company.PublicId,
            rejectionReason);
    }
}

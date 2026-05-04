using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.LegalRepresentatives;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Platform;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.Provisioning.Common;
using CLARIHR.Application.Features.PlatformSubscriptions.Common;
using CLARIHR.Domain.Companies;

namespace CLARIHR.Application.Features.PlatformSubscriptions;

internal sealed class GetPlatformCompanySubscriptionQueryHandler(
    IPlatformAuthorizationService authorizationService,
    ICompanyRepository companyRepository,
    ICompanySubscriptionRepository subscriptionRepository)
    : IQueryHandler<GetPlatformCompanySubscriptionQuery, PlatformCompanySubscriptionOverviewResponse>
{
    public async Task<Result<PlatformCompanySubscriptionOverviewResponse>> Handle(
        GetPlatformCompanySubscriptionQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PlatformCompanySubscriptionOverviewResponse>.Failure(authorizationResult.Error);
        }

        if (await companyRepository.FindByPublicIdAsync(query.CompanyId, cancellationToken) is null)
        {
            return Result<PlatformCompanySubscriptionOverviewResponse>.Failure(PlatformSubscriptionErrors.CompanyNotFound);
        }

        var overview = await subscriptionRepository.GetOverviewByCompanyPublicIdAsync(query.CompanyId, cancellationToken);
        if (overview is null ||
            (overview.CurrentSubscription is null && overview.ScheduledReplacement is null))
        {
            return Result<PlatformCompanySubscriptionOverviewResponse>.Failure(PlatformSubscriptionErrors.SubscriptionNotFound);
        }

        return Result<PlatformCompanySubscriptionOverviewResponse>.Success(overview);
    }
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

internal sealed class SearchPlatformCompanySubscriptionsQueryHandler(
    IPlatformAuthorizationService authorizationService,
    ICompanySubscriptionRepository subscriptionRepository)
    : IQueryHandler<SearchPlatformCompanySubscriptionsQuery, PagedResponse<PlatformCompanySubscriptionListItemResponse>>
{
    public async Task<Result<PagedResponse<PlatformCompanySubscriptionListItemResponse>>> Handle(
        SearchPlatformCompanySubscriptionsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<PlatformCompanySubscriptionListItemResponse>>.Failure(authorizationResult.Error);
        }

        var response = await subscriptionRepository.SearchAsync(
            query.Status,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        return Result<PagedResponse<PlatformCompanySubscriptionListItemResponse>>.Success(response);
    }
}

internal sealed class SearchPlatformCompanySubscriptionStatusHistoryQueryHandler(
    IPlatformAuthorizationService authorizationService,
    ICompanyRepository companyRepository,
    ICompanySubscriptionRepository subscriptionRepository)
    : IQueryHandler<SearchPlatformCompanySubscriptionStatusHistoryQuery, PagedResponse<PlatformCompanySubscriptionStatusTransitionResponse>>
{
    public async Task<Result<PagedResponse<PlatformCompanySubscriptionStatusTransitionResponse>>> Handle(
        SearchPlatformCompanySubscriptionStatusHistoryQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<PlatformCompanySubscriptionStatusTransitionResponse>>.Failure(authorizationResult.Error);
        }

        if (await companyRepository.FindByPublicIdAsync(query.CompanyId, cancellationToken) is null)
        {
            return Result<PagedResponse<PlatformCompanySubscriptionStatusTransitionResponse>>.Failure(PlatformSubscriptionErrors.CompanyNotFound);
        }

        var subscription = await subscriptionRepository.GetByCompanyAndSubscriptionPublicIdAsync(
            query.CompanyId,
            query.SubscriptionId,
            cancellationToken);

        if (subscription is null)
        {
            return Result<PagedResponse<PlatformCompanySubscriptionStatusTransitionResponse>>.Failure(PlatformSubscriptionErrors.SubscriptionNotFound);
        }

        var response = await subscriptionRepository.SearchStatusHistoryAsync(
            query.CompanyId,
            query.SubscriptionId,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        return Result<PagedResponse<PlatformCompanySubscriptionStatusTransitionResponse>>.Success(response);
    }
}

internal sealed class PreviewPlatformCompanySubscriptionQueryHandler(
    IPlatformAuthorizationService authorizationService,
    ICompanyRepository companyRepository,
    ICommercialPlanRepository commercialPlanRepository,
    ICompanySubscriptionRepository subscriptionRepository,
    IUserCompanyRepository userCompanyRepository,
    ILegalRepresentativeRepository legalRepresentativeRepository,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<PreviewPlatformCompanySubscriptionQuery, PlatformCompanySubscriptionPreviewResponse>
{
    public async Task<Result<PlatformCompanySubscriptionPreviewResponse>> Handle(
        PreviewPlatformCompanySubscriptionQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PlatformCompanySubscriptionPreviewResponse>.Failure(authorizationResult.Error);
        }

        var resolution = await PlatformSubscriptionResolver.ResolveAsync(
            query.CompanyId,
            query.CommercialPlanId,
            query.StartDateUtc,
            query.ExpiresAtUtc,
            query.Periodicity,
            companyRepository,
            commercialPlanRepository,
            subscriptionRepository,
            userCompanyRepository,
            legalRepresentativeRepository,
            dateTimeProvider.UtcNow,
            cancellationToken);

        if (resolution.IsFailure)
        {
            return Result<PlatformCompanySubscriptionPreviewResponse>.Failure(resolution.Error);
        }

        return Result<PlatformCompanySubscriptionPreviewResponse>.Success(resolution.Value.ToPreviewResponse());
    }
}

internal sealed class ActivatePlatformCompanySubscriptionCommandHandler(
    IPlatformAuthorizationService authorizationService,
    ICompanyRepository companyRepository,
    ICommercialPlanRepository commercialPlanRepository,
    ICompanySubscriptionRepository subscriptionRepository,
    IUserCompanyRepository userCompanyRepository,
    ILegalRepresentativeRepository legalRepresentativeRepository,
    ICurrentUserService currentUserService,
    IPlatformAuditService platformAuditService,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ActivatePlatformCompanySubscriptionCommand, PlatformCompanySubscriptionResponse>
{
    public async Task<Result<PlatformCompanySubscriptionResponse>> Handle(
        ActivatePlatformCompanySubscriptionCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PlatformCompanySubscriptionResponse>.Failure(authorizationResult.Error);
        }

        var utcNow = dateTimeProvider.UtcNow;
        var resolution = await PlatformSubscriptionResolver.ResolveAsync(
            command.CompanyId,
            command.CommercialPlanId,
            command.StartDateUtc,
            command.ExpiresAtUtc,
            command.Periodicity,
            companyRepository,
            commercialPlanRepository,
            subscriptionRepository,
            userCompanyRepository,
            legalRepresentativeRepository,
            utcNow,
            cancellationToken);

        if (resolution.IsFailure)
        {
            return Result<PlatformCompanySubscriptionResponse>.Failure(resolution.Error);
        }

        var context = resolution.Value;
        if (!context.IsEligible)
        {
            return Result<PlatformCompanySubscriptionResponse>.Failure(context.PrimaryError);
        }

        var actorUserPublicId = PlatformSubscriptionAdministrationHelpers.TryParseCurrentUserId(currentUserService.UserId);
        var before = await subscriptionRepository.GetOverviewByCompanyPublicIdAsync(command.CompanyId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            if (context.ResolvedStatus == SubscriptionStatus.Active && context.CurrentSubscription is not null)
            {
                context.CurrentSubscription.Cancel(
                    utcNow,
                    SubscriptionStatusChangeReasonCode.SubscriptionReplacement,
                    observations: null,
                    SubscriptionStatusChangeOrigin.PlatformOperator,
                    actorUserPublicId);
            }

            var nextSubscription = context.ResolvedStatus == SubscriptionStatus.Active
                ? CompanySubscription.Activate(
                    context.Company.Id,
                    context.Plan,
                    context.Periodicity,
                    context.StartDateUtc,
                    context.ExpiresAtUtc,
                    actorUserPublicId ?? Guid.Empty,
                    utcNow,
                    SubscriptionStatusChangeReasonCode.ManualActivation,
                    SubscriptionStatusChangeOrigin.PlatformOperator,
                    observations: null)
                : CompanySubscription.Schedule(
                    context.Company.Id,
                    context.Plan,
                    context.Periodicity,
                    context.StartDateUtc,
                    context.ExpiresAtUtc,
                    actorUserPublicId ?? Guid.Empty,
                    utcNow,
                    SubscriptionStatusChangeReasonCode.ActivationScheduled,
                    SubscriptionStatusChangeOrigin.PlatformOperator,
                    observations: null);

            var nextSubscriptionPublicId = nextSubscription.PublicId;
            subscriptionRepository.Add(nextSubscription);

            if (context.ResolvedStatus == SubscriptionStatus.Active)
            {
                await PlatformSubscriptionBillablePolicy.ApplyBillableStateAsync(
                    context.Company,
                    nextSubscription,
                    commercialPlanRepository,
                    utcNow,
                    cancellationToken);
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var overviewAfter = await subscriptionRepository.GetOverviewByCompanyPublicIdAsync(command.CompanyId, cancellationToken);
            var response = await subscriptionRepository.GetResponseByPublicIdAsync(
                command.CompanyId,
                nextSubscriptionPublicId,
                cancellationToken);

            if (response is null)
            {
                throw new InvalidOperationException("The created company subscription could not be loaded after persistence.");
            }

            await platformAuditService.LogAsync(
                new AuditLogEntry(
                    context.ResolvedStatus == SubscriptionStatus.Active
                        ? AuditEventTypes.CompanySubscriptionActivated
                        : AuditEventTypes.CompanySubscriptionScheduled,
                    AuditEntityTypes.CompanySubscription,
                    nextSubscriptionPublicId,
                    context.Company.Slug,
                    context.ResolvedStatus == SubscriptionStatus.Active ? AuditActions.Create : AuditActions.Update,
                    context.ResolvedStatus == SubscriptionStatus.Active
                        ? $"Activated company subscription for {context.Company.Name} using plan {context.Plan.Code}."
                        : $"Scheduled company subscription for {context.Company.Name} using plan {context.Plan.Code}.",
                    Before: before,
                    After: overviewAfter),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PlatformCompanySubscriptionResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class ChangePlatformCompanySubscriptionStatusCommandHandler(
    IPlatformAuthorizationService authorizationService,
    ICompanyRepository companyRepository,
    ICommercialPlanRepository commercialPlanRepository,
    ICompanySubscriptionRepository subscriptionRepository,
    ICurrentUserService currentUserService,
    IPlatformAuditService platformAuditService,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork)
    : ICommandHandler<ChangePlatformCompanySubscriptionStatusCommand, PlatformCompanySubscriptionResponse>
{
    public async Task<Result<PlatformCompanySubscriptionResponse>> Handle(
        ChangePlatformCompanySubscriptionStatusCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PlatformCompanySubscriptionResponse>.Failure(authorizationResult.Error);
        }

        var utcNow = dateTimeProvider.UtcNow;
        if (command.TargetStatus != SubscriptionStatus.Active &&
            command.EffectiveDateUtc.HasValue)
        {
            return Result<PlatformCompanySubscriptionResponse>.Failure(PlatformSubscriptionErrors.StatusChangeSchedulingNotAllowed);
        }

        if (command.TargetStatus == SubscriptionStatus.Active)
        {
            var resolution = await PlatformSubscriptionStatusChangeResolver.ResolveAsync(
                command.CompanyId,
                command.SubscriptionId,
                command.TargetStatus,
                command.ReasonCode,
                command.EffectiveDateUtc,
                companyRepository,
                subscriptionRepository,
                utcNow,
                cancellationToken);

            if (resolution.IsFailure)
            {
                return Result<PlatformCompanySubscriptionResponse>.Failure(resolution.Error);
            }

            if (!resolution.Value.IsEligible)
            {
                return Result<PlatformCompanySubscriptionResponse>.Failure(resolution.Value.PrimaryError);
            }

            var reactivationBefore = await subscriptionRepository.GetOverviewByCompanyPublicIdAsync(command.CompanyId, cancellationToken);
            var reactivationActorUserPublicId = PlatformSubscriptionAdministrationHelpers.TryParseCurrentUserId(currentUserService.UserId);

            await using var reactivationTransaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                if (resolution.Value.EffectiveDateUtc > utcNow.Date)
                {
                    var statusChangeRequest = CompanySubscriptionStatusChangeRequest.Create(
                        resolution.Value.Subscription,
                        command.TargetStatus,
                        command.ReasonCode,
                        utcNow,
                        resolution.Value.EffectiveDateUtc,
                        reactivationActorUserPublicId,
                        command.Observations);

                    subscriptionRepository.AddStatusChangeRequest(statusChangeRequest);
                    _ = await unitOfWork.SaveChangesAsync(cancellationToken);

                    var overviewAfterSchedule = await subscriptionRepository.GetOverviewByCompanyPublicIdAsync(command.CompanyId, cancellationToken);
                    var scheduledResponse = await subscriptionRepository.GetResponseByPublicIdAsync(
                        command.CompanyId,
                        command.SubscriptionId,
                        cancellationToken);

                    if (scheduledResponse is null)
                    {
                        throw new InvalidOperationException("The scheduled subscription status change could not be loaded after persistence.");
                    }

                    await platformAuditService.LogAsync(
                        new AuditLogEntry(
                            AuditEventTypes.CompanySubscriptionStatusChangeRequested,
                            AuditEntityTypes.CompanySubscriptionStatusChangeRequest,
                            statusChangeRequest.PublicId,
                            resolution.Value.Company.Slug,
                            AuditActions.Update,
                            $"Scheduled subscription reactivation for {resolution.Value.Company.Name}.",
                            Before: reactivationBefore,
                            After: overviewAfterSchedule),
                        cancellationToken);

                    _ = await unitOfWork.SaveChangesAsync(cancellationToken);
                    await reactivationTransaction.CommitAsync(cancellationToken);
                    return Result<PlatformCompanySubscriptionResponse>.Success(scheduledResponse);
                }

                resolution.Value.Subscription.Reactivate(
                    utcNow,
                    command.ReasonCode,
                    command.Observations,
                    SubscriptionStatusChangeOrigin.PlatformOperator,
                    reactivationActorUserPublicId);

                await PlatformSubscriptionBillablePolicy.ApplyBillableStateAsync(
                    resolution.Value.Company,
                    resolution.Value.Subscription,
                    commercialPlanRepository,
                    utcNow,
                    cancellationToken);

                _ = await unitOfWork.SaveChangesAsync(cancellationToken);

                var overviewAfterReactivation = await subscriptionRepository.GetOverviewByCompanyPublicIdAsync(command.CompanyId, cancellationToken);
                var reactivatedResponse = await subscriptionRepository.GetResponseByPublicIdAsync(
                    command.CompanyId,
                    command.SubscriptionId,
                    cancellationToken);

                if (reactivatedResponse is null)
                {
                    throw new InvalidOperationException("The updated company subscription could not be loaded after persistence.");
                }

                await platformAuditService.LogAsync(
                        new AuditLogEntry(
                            AuditEventTypes.CompanySubscriptionStatusChanged,
                            AuditEntityTypes.CompanySubscription,
                            resolution.Value.Subscription.PublicId,
                            resolution.Value.Company.Slug,
                            AuditActions.Update,
                            $"Changed company subscription status from {resolution.Value.CurrentStatus} to {command.TargetStatus} for {resolution.Value.Company.Name}.",
                            Before: reactivationBefore,
                            After: overviewAfterReactivation),
                        cancellationToken);

                _ = await unitOfWork.SaveChangesAsync(cancellationToken);
                await reactivationTransaction.CommitAsync(cancellationToken);
                return Result<PlatformCompanySubscriptionResponse>.Success(reactivatedResponse);
            }
            catch (InvalidOperationException exception) when (
                exception.Message.Contains("past their expiration date", StringComparison.OrdinalIgnoreCase))
            {
                await reactivationTransaction.RollbackAsync(cancellationToken);
                return Result<PlatformCompanySubscriptionResponse>.Failure(PlatformSubscriptionErrors.ReactivationPastExpiration);
            }
            catch
            {
                await reactivationTransaction.RollbackAsync(cancellationToken);
                throw;
            }
        }

        var company = await companyRepository.FindByPublicIdAsync(command.CompanyId, cancellationToken);
        if (company is null)
        {
            return Result<PlatformCompanySubscriptionResponse>.Failure(PlatformSubscriptionErrors.CompanyNotFound);
        }

        var subscription = await subscriptionRepository.GetByCompanyAndSubscriptionPublicIdAsync(
            command.CompanyId,
            command.SubscriptionId,
            cancellationToken);

        if (subscription is null)
        {
            return Result<PlatformCompanySubscriptionResponse>.Failure(PlatformSubscriptionErrors.SubscriptionNotFound);
        }

        if (!SubscriptionStatusPolicy.CanTransition(
                subscription.Status,
                command.TargetStatus,
                SubscriptionStatusChangeOrigin.PlatformOperator))
        {
            return Result<PlatformCompanySubscriptionResponse>.Failure(PlatformSubscriptionErrors.InvalidStatusTransition);
        }

        if (!SubscriptionStatusPolicy.IsReasonAllowed(
                subscription.Status,
                command.TargetStatus,
                SubscriptionStatusChangeOrigin.PlatformOperator,
                command.ReasonCode))
        {
            return Result<PlatformCompanySubscriptionResponse>.Failure(PlatformSubscriptionErrors.InvalidStatusReason);
        }

        var before = await subscriptionRepository.GetOverviewByCompanyPublicIdAsync(command.CompanyId, cancellationToken);
        var actorUserPublicId = PlatformSubscriptionAdministrationHelpers.TryParseCurrentUserId(currentUserService.UserId);
        var previousStatus = subscription.Status;

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            switch (command.TargetStatus)
            {
                case SubscriptionStatus.Suspended:
                    subscription.Suspend(
                        utcNow,
                        command.ReasonCode,
                        command.Observations,
                        SubscriptionStatusChangeOrigin.PlatformOperator,
                        actorUserPublicId);
                    company.ClearBillable();
                    break;

                case SubscriptionStatus.Active:
                    subscription.Reactivate(
                        utcNow,
                        command.ReasonCode,
                        command.Observations,
                        SubscriptionStatusChangeOrigin.PlatformOperator,
                        actorUserPublicId);
                    await PlatformSubscriptionBillablePolicy.ApplyBillableStateAsync(
                        company,
                        subscription,
                        commercialPlanRepository,
                        utcNow,
                        cancellationToken);
                    break;

                case SubscriptionStatus.Cancelled:
                    subscription.Cancel(
                        utcNow,
                        command.ReasonCode,
                        command.Observations,
                        SubscriptionStatusChangeOrigin.PlatformOperator,
                        actorUserPublicId);
                    if (previousStatus != SubscriptionStatus.Scheduled)
                    {
                        company.ClearBillable();
                    }

                    break;

                default:
                    return Result<PlatformCompanySubscriptionResponse>.Failure(PlatformSubscriptionErrors.InvalidStatusTransition);
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var overviewAfter = await subscriptionRepository.GetOverviewByCompanyPublicIdAsync(command.CompanyId, cancellationToken);
            var response = await subscriptionRepository.GetResponseByPublicIdAsync(
                command.CompanyId,
                command.SubscriptionId,
                cancellationToken);

            if (response is null)
            {
                throw new InvalidOperationException("The updated company subscription could not be loaded after persistence.");
            }

            await platformAuditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CompanySubscriptionStatusChanged,
                    AuditEntityTypes.CompanySubscription,
                    subscription.PublicId,
                    company.Slug,
                    AuditActions.Update,
                    $"Changed company subscription status from {previousStatus} to {command.TargetStatus} for {company.Name}.",
                    Before: before,
                    After: overviewAfter),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PlatformCompanySubscriptionResponse>.Success(response);
        }
        catch (InvalidOperationException exception) when (
            command.TargetStatus == SubscriptionStatus.Active &&
            exception.Message.Contains("past their expiration date", StringComparison.OrdinalIgnoreCase))
        {
            await transaction.RollbackAsync(cancellationToken);
            return Result<PlatformCompanySubscriptionResponse>.Failure(PlatformSubscriptionErrors.ReactivationPastExpiration);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class PreviewPlatformCompanySubscriptionStatusChangeQueryHandler(
    IPlatformAuthorizationService authorizationService,
    ICompanyRepository companyRepository,
    ICompanySubscriptionRepository subscriptionRepository,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<PreviewPlatformCompanySubscriptionStatusChangeQuery, PlatformCompanySubscriptionStatusChangePreviewResponse>
{
    public async Task<Result<PlatformCompanySubscriptionStatusChangePreviewResponse>> Handle(
        PreviewPlatformCompanySubscriptionStatusChangeQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PlatformCompanySubscriptionStatusChangePreviewResponse>.Failure(authorizationResult.Error);
        }

        var resolution = await PlatformSubscriptionStatusChangeResolver.ResolveAsync(
            query.CompanyId,
            query.SubscriptionId,
            query.TargetStatus,
            query.ReasonCode,
            query.EffectiveDateUtc,
            companyRepository,
            subscriptionRepository,
            dateTimeProvider.UtcNow,
            cancellationToken);

        if (resolution.IsFailure)
        {
            return Result<PlatformCompanySubscriptionStatusChangePreviewResponse>.Failure(resolution.Error);
        }

        return Result<PlatformCompanySubscriptionStatusChangePreviewResponse>.Success(resolution.Value.ToPreviewResponse());
    }
}

internal sealed class PreviewPlatformCompanySubscriptionPlanChangeQueryHandler(
    IPlatformAuthorizationService authorizationService,
    ICompanyRepository companyRepository,
    ICommercialPlanRepository commercialPlanRepository,
    ICompanySubscriptionRepository subscriptionRepository,
    IUserCompanyRepository userCompanyRepository,
    ILegalRepresentativeRepository legalRepresentativeRepository,
    IPersonnelFileRepository personnelFileRepository,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<PreviewPlatformCompanySubscriptionPlanChangeQuery, PlatformCompanySubscriptionPlanChangePreviewResponse>
{
    public async Task<Result<PlatformCompanySubscriptionPlanChangePreviewResponse>> Handle(
        PreviewPlatformCompanySubscriptionPlanChangeQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PlatformCompanySubscriptionPlanChangePreviewResponse>.Failure(authorizationResult.Error);
        }

        var resolution = await PlatformSubscriptionPlanChangeResolver.ResolveAsync(
            query.CompanyId,
            query.CommercialPlanId,
            query.Mode,
            query.RequestedEffectiveDateUtc,
            companyRepository,
            commercialPlanRepository,
            subscriptionRepository,
            userCompanyRepository,
            legalRepresentativeRepository,
            personnelFileRepository,
            dateTimeProvider.UtcNow,
            cancellationToken);

        if (resolution.IsFailure)
        {
            return Result<PlatformCompanySubscriptionPlanChangePreviewResponse>.Failure(resolution.Error);
        }

        return Result<PlatformCompanySubscriptionPlanChangePreviewResponse>.Success(resolution.Value.ToPreviewResponse());
    }
}

internal sealed class SearchPlatformCompanySubscriptionPlanChangesQueryHandler(
    IPlatformAuthorizationService authorizationService,
    ICompanyRepository companyRepository,
    ICompanySubscriptionRepository subscriptionRepository)
    : IQueryHandler<SearchPlatformCompanySubscriptionPlanChangesQuery, PagedResponse<PlatformCompanySubscriptionPlanChangeResponse>>
{
    public async Task<Result<PagedResponse<PlatformCompanySubscriptionPlanChangeResponse>>> Handle(
        SearchPlatformCompanySubscriptionPlanChangesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<PlatformCompanySubscriptionPlanChangeResponse>>.Failure(authorizationResult.Error);
        }

        if (await companyRepository.FindByPublicIdAsync(query.CompanyId, cancellationToken) is null)
        {
            return Result<PagedResponse<PlatformCompanySubscriptionPlanChangeResponse>>.Failure(PlatformSubscriptionErrors.CompanyNotFound);
        }

        var response = await subscriptionRepository.SearchPlanChangesByCompanyPublicIdAsync(
            query.CompanyId,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        return Result<PagedResponse<PlatformCompanySubscriptionPlanChangeResponse>>.Success(response);
    }
}

internal sealed class CreatePlatformCompanySubscriptionPlanChangeCommandHandler(
    IPlatformAuthorizationService authorizationService,
    ICompanyRepository companyRepository,
    ICommercialPlanRepository commercialPlanRepository,
    ICompanySubscriptionRepository subscriptionRepository,
    IUserCompanyRepository userCompanyRepository,
    ILegalRepresentativeRepository legalRepresentativeRepository,
    IPersonnelFileRepository personnelFileRepository,
    ICurrentUserService currentUserService,
    IPlatformAuditService platformAuditService,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreatePlatformCompanySubscriptionPlanChangeCommand, PlatformCompanySubscriptionPlanChangeResponse>
{
    public async Task<Result<PlatformCompanySubscriptionPlanChangeResponse>> Handle(
        CreatePlatformCompanySubscriptionPlanChangeCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PlatformCompanySubscriptionPlanChangeResponse>.Failure(authorizationResult.Error);
        }

        var utcNow = dateTimeProvider.UtcNow;
        var resolution = await PlatformSubscriptionPlanChangeResolver.ResolveAsync(
            command.CompanyId,
            command.CommercialPlanId,
            command.Mode,
            command.RequestedEffectiveDateUtc,
            companyRepository,
            commercialPlanRepository,
            subscriptionRepository,
            userCompanyRepository,
            legalRepresentativeRepository,
            personnelFileRepository,
            utcNow,
            cancellationToken);

        if (resolution.IsFailure)
        {
            return Result<PlatformCompanySubscriptionPlanChangeResponse>.Failure(resolution.Error);
        }

        var context = resolution.Value;
        if (!context.IsEligible)
        {
            return Result<PlatformCompanySubscriptionPlanChangeResponse>.Failure(context.PrimaryError);
        }

        var actorUserPublicId = PlatformSubscriptionAdministrationHelpers.TryParseCurrentUserId(currentUserService.UserId);
        var before = await subscriptionRepository.GetOverviewByCompanyPublicIdAsync(command.CompanyId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var planChange = CompanySubscriptionPlanChange.Create(
                context.CurrentSubscription,
                context.CurrentPlan,
                context.CurrentPlanVersion,
                context.TargetPlan,
                context.TargetPlanVersion,
                context.CurrentSubscription.Periodicity,
                context.Mode,
                command.ReasonCode,
                utcNow,
                context.EffectiveDateUtc,
                actorUserPublicId,
                command.Observations,
                context.EstimatedNextCharge,
                context.ActiveEmployeeCount);

            subscriptionRepository.AddPlanChange(planChange);

            if (context.Mode == SubscriptionPlanChangeMode.Immediate)
            {
                context.CurrentSubscription.Cancel(
                    utcNow,
                    SubscriptionStatusChangeReasonCode.SubscriptionReplacement,
                    observations: null,
                    SubscriptionStatusChangeOrigin.PlatformOperator,
                    actorUserPublicId);

                var nextSubscription = CompanySubscription.Activate(
                    context.Company.Id,
                    context.TargetPlan,
                    context.CurrentSubscription.Periodicity,
                    context.EffectiveDateUtc,
                    context.CurrentSubscription.ExpiresAtUtc,
                    actorUserPublicId ?? Guid.Empty,
                    utcNow,
                    SubscriptionStatusChangeReasonCode.PlanChangeApplied,
                    SubscriptionStatusChangeOrigin.PlatformOperator,
                    command.Observations);

                subscriptionRepository.Add(nextSubscription);
                planChange.MarkApplied(utcNow, nextSubscription.PublicId);

                await PlatformSubscriptionBillablePolicy.ApplyBillableStateAsync(
                    context.Company,
                    nextSubscription,
                    commercialPlanRepository,
                    utcNow,
                    cancellationToken);
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await subscriptionRepository.GetPlanChangeResponseByPublicIdAsync(
                command.CompanyId,
                planChange.PublicId,
                cancellationToken);

            if (response is null)
            {
                throw new InvalidOperationException("The created plan change could not be loaded after persistence.");
            }

            var after = await subscriptionRepository.GetOverviewByCompanyPublicIdAsync(command.CompanyId, cancellationToken);

            await platformAuditService.LogAsync(
                new AuditLogEntry(
                    context.Mode == SubscriptionPlanChangeMode.Immediate
                        ? AuditEventTypes.CompanySubscriptionPlanChangeApplied
                        : AuditEventTypes.CompanySubscriptionPlanChangeRequested,
                    AuditEntityTypes.CompanySubscriptionPlanChange,
                    planChange.PublicId,
                    context.Company.Slug,
                    AuditActions.Update,
                    context.Mode == SubscriptionPlanChangeMode.Immediate
                        ? $"Applied plan change for {context.Company.Name} from {context.CurrentSubscription.PlanCode} to {context.TargetPlan.Code}."
                        : $"Scheduled plan change for {context.Company.Name} from {context.CurrentSubscription.PlanCode} to {context.TargetPlan.Code}.",
                    Before: before,
                    After: response),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PlatformCompanySubscriptionPlanChangeResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class CancelPlatformCompanySubscriptionPlanChangeCommandHandler(
    IPlatformAuthorizationService authorizationService,
    ICompanyRepository companyRepository,
    ICompanySubscriptionRepository subscriptionRepository,
    ICurrentUserService currentUserService,
    IPlatformAuditService platformAuditService,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CancelPlatformCompanySubscriptionPlanChangeCommand, PlatformCompanySubscriptionPlanChangeResponse>
{
    public async Task<Result<PlatformCompanySubscriptionPlanChangeResponse>> Handle(
        CancelPlatformCompanySubscriptionPlanChangeCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PlatformCompanySubscriptionPlanChangeResponse>.Failure(authorizationResult.Error);
        }

        var company = await companyRepository.FindByPublicIdAsync(command.CompanyId, cancellationToken);
        if (company is null)
        {
            return Result<PlatformCompanySubscriptionPlanChangeResponse>.Failure(PlatformSubscriptionErrors.CompanyNotFound);
        }

        var planChange = await subscriptionRepository.GetPlanChangeByCompanyAndPublicIdAsync(
            command.CompanyId,
            command.PlanChangeId,
            cancellationToken);

        if (planChange is null)
        {
            return Result<PlatformCompanySubscriptionPlanChangeResponse>.Failure(PlatformSubscriptionErrors.PlanChangeNotFound);
        }

        if (planChange.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PlatformCompanySubscriptionPlanChangeResponse>.Failure(PlatformSubscriptionErrors.ConcurrencyConflict);
        }

        if (planChange.Status != SubscriptionPlanChangeStatus.Scheduled ||
            planChange.EffectiveDateUtc <= dateTimeProvider.UtcNow.Date)
        {
            return Result<PlatformCompanySubscriptionPlanChangeResponse>.Failure(PlatformSubscriptionErrors.PlanChangeCancellationNotAllowed);
        }

        var actorUserPublicId = PlatformSubscriptionAdministrationHelpers.TryParseCurrentUserId(currentUserService.UserId);
        var before = await subscriptionRepository.GetPlanChangeResponseByPublicIdAsync(
            command.CompanyId,
            command.PlanChangeId,
            cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            planChange.Cancel(dateTimeProvider.UtcNow, actorUserPublicId, command.Observations);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await subscriptionRepository.GetPlanChangeResponseByPublicIdAsync(
                command.CompanyId,
                command.PlanChangeId,
                cancellationToken);

            if (response is null)
            {
                throw new InvalidOperationException("The cancelled plan change could not be loaded after persistence.");
            }

            await platformAuditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CompanySubscriptionPlanChangeCancelled,
                    AuditEntityTypes.CompanySubscriptionPlanChange,
                    planChange.PublicId,
                    company.Slug,
                    AuditActions.Update,
                    $"Cancelled scheduled plan change for {company.Name}.",
                    Before: before,
                    After: response),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PlatformCompanySubscriptionPlanChangeResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed record PlatformSubscriptionResolution(
    Company Company,
    CommercialPlan Plan,
    CommercialPlanVersion PlanVersion,
    CompanySubscriptionPeriodicity Periodicity,
    DateTime StartDateUtc,
    DateTime? ExpiresAtUtc,
    SubscriptionStatus ResolvedStatus,
    CompanySubscription? CurrentSubscription,
    CompanySubscription? ScheduledSubscription,
    IReadOnlyCollection<string> IneligibilityReasons,
    Error PrimaryError)
{
    public bool IsEligible => IneligibilityReasons.Count == 0;

    public PlatformCompanySubscriptionPreviewResponse ToPreviewResponse() =>
        new(
            Company.PublicId,
            Company.Name,
            Company.Slug,
            Company.Status,
            Company.IsBillable,
            Plan.PublicId,
            PlanVersion.PublicId,
            Plan.Code,
            Plan.Name,
            PlanVersion.VersionNumber,
            PlanVersion.BaseMonthlyFee,
            PlanVersion.PricePerActiveEmployee,
            Periodicity,
            PlanVersion.CurrencyCode,
            ResolvedStatus,
            StartDateUtc,
            ExpiresAtUtc,
            SubscriptionStatusPolicy.CanOperate(ResolvedStatus),
            SubscriptionStatusPolicy.CanGenerateCharges(ResolvedStatus),
            IsEligible,
            IneligibilityReasons);
}

internal sealed record PlatformSubscriptionStatusChangeResolution(
    Company Company,
    CompanySubscription Subscription,
    SubscriptionStatus CurrentStatus,
    SubscriptionStatus TargetStatus,
    DateTime EffectiveDateUtc,
    IReadOnlyCollection<string> IneligibilityReasons,
    Error PrimaryError)
{
    public bool IsEligible => IneligibilityReasons.Count == 0;

    public PlatformCompanySubscriptionStatusChangePreviewResponse ToPreviewResponse() =>
        new(
            Company.PublicId,
            Company.Name,
            Company.Slug,
            Company.Status,
            Subscription.PublicId,
            CurrentStatus,
            TargetStatus,
            EffectiveDateUtc,
            Subscription.PlanCode,
            Subscription.PlanName,
            Subscription.PlanVersionNumber,
            Subscription.ExpiresAtUtc,
            SubscriptionStatusPolicy.CanOperate(CurrentStatus),
            SubscriptionStatusPolicy.CanGenerateCharges(CurrentStatus),
            IsEligible,
            IneligibilityReasons);
}

internal static class PlatformSubscriptionStatusChangeResolver
{
    public static async Task<Result<PlatformSubscriptionStatusChangeResolution>> ResolveAsync(
        Guid companyPublicId,
        Guid subscriptionPublicId,
        SubscriptionStatus targetStatus,
        SubscriptionStatusChangeReasonCode reasonCode,
        DateTime? effectiveDateUtc,
        ICompanyRepository companyRepository,
        ICompanySubscriptionRepository subscriptionRepository,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var company = await companyRepository.FindByPublicIdAsync(companyPublicId, cancellationToken);
        if (company is null)
        {
            return Result<PlatformSubscriptionStatusChangeResolution>.Failure(PlatformSubscriptionErrors.CompanyNotFound);
        }

        var subscription = await subscriptionRepository.GetByCompanyAndSubscriptionPublicIdAsync(
            companyPublicId,
            subscriptionPublicId,
            cancellationToken);

        if (subscription is null)
        {
            return Result<PlatformSubscriptionStatusChangeResolution>.Failure(PlatformSubscriptionErrors.SubscriptionNotFound);
        }

        if (targetStatus != SubscriptionStatus.Active)
        {
            return Result<PlatformSubscriptionStatusChangeResolution>.Failure(PlatformSubscriptionErrors.StatusChangeSchedulingNotAllowed);
        }

        if (!SubscriptionStatusPolicy.IsReasonAllowed(
                SubscriptionStatus.Suspended,
                targetStatus,
                SubscriptionStatusChangeOrigin.PlatformOperator,
                reasonCode))
        {
            return Result<PlatformSubscriptionStatusChangeResolution>.Failure(PlatformSubscriptionErrors.InvalidStatusReason);
        }

        if (!effectiveDateUtc.HasValue || effectiveDateUtc.Value == default)
        {
            return Result<PlatformSubscriptionStatusChangeResolution>.Failure(PlatformSubscriptionErrors.StatusChangeEffectiveDateRequired);
        }

        if (effectiveDateUtc.Value.Date < utcNow.Date)
        {
            return Result<PlatformSubscriptionStatusChangeResolution>.Failure(PlatformSubscriptionErrors.StatusChangeEffectiveDateInPast);
        }

        var scheduledStatusChangeRequest = await subscriptionRepository.GetScheduledStatusChangeRequestBySubscriptionIdAsync(
            subscription.Id,
            cancellationToken);

        var reasons = new List<string>();
        var errors = new List<Error>();

        if (company.Status != CompanyStatus.Active)
        {
            reasons.Add("La empresa debe estar activa para reactivar una suscripcion.");
            errors.Add(PlatformSubscriptionErrors.CompanyNotEligible);
        }

        if (subscription.Status != SubscriptionStatus.Suspended)
        {
            reasons.Add("Solo las suscripciones suspendidas pueden reactivarse.");
            errors.Add(PlatformSubscriptionErrors.ReactivationRequiresSuspendedStatus);
        }

        if (scheduledStatusChangeRequest is not null)
        {
            reasons.Add("La suscripcion ya tiene una reactivacion programada pendiente.");
            errors.Add(PlatformSubscriptionErrors.StatusChangePendingConflict);
        }

        if (subscription.ExpiresAtUtc.HasValue &&
            effectiveDateUtc.Value.Date > subscription.ExpiresAtUtc.Value.Date)
        {
            reasons.Add("La fecha efectiva de reactivacion no puede ser posterior al vencimiento de la suscripcion.");
            errors.Add(PlatformSubscriptionErrors.ReactivationPastExpiration);
        }

        return Result<PlatformSubscriptionStatusChangeResolution>.Success(new PlatformSubscriptionStatusChangeResolution(
            company,
            subscription,
            subscription.Status,
            targetStatus,
            effectiveDateUtc.Value.Date,
            reasons,
            errors.FirstOrDefault() ?? PlatformSubscriptionErrors.ReactivationRequiresSuspendedStatus));
    }
}

internal sealed record PlatformSubscriptionPlanChangeResolution(
    Company Company,
    CompanySubscription CurrentSubscription,
    CommercialPlan? CurrentPlan,
    CommercialPlanVersion? CurrentPlanVersion,
    CommercialPlan TargetPlan,
    CommercialPlanVersion TargetPlanVersion,
    SubscriptionPlanChangeMode Mode,
    DateTime EffectiveDateUtc,
    int ActiveEmployeeCount,
    decimal EstimatedNextCharge,
    IReadOnlyCollection<string> IneligibilityReasons,
    IReadOnlyCollection<string> AddonCompatibilityWarnings,
    Error PrimaryError)
{
    public bool IsEligible => IneligibilityReasons.Count == 0;

    public PlatformCompanySubscriptionPlanChangePreviewResponse ToPreviewResponse() =>
        new(
            Company.PublicId,
            Company.Name,
            Company.Slug,
            CurrentSubscription.PublicId,
            CurrentPlan?.PublicId ?? Guid.Empty,
            CurrentPlanVersion?.PublicId ?? Guid.Empty,
            CurrentSubscription.PlanCode,
            CurrentSubscription.PlanName,
            CurrentSubscription.PlanVersionNumber,
            CurrentSubscription.BaseMonthlyFee,
            CurrentSubscription.PricePerActiveEmployee,
            CurrentSubscription.Periodicity,
            CurrentSubscription.CurrencyCode,
            TargetPlan.PublicId,
            TargetPlanVersion.PublicId,
            TargetPlan.Code,
            TargetPlan.Name,
            TargetPlanVersion.VersionNumber,
            TargetPlanVersion.BaseMonthlyFee,
            TargetPlanVersion.PricePerActiveEmployee,
            CurrentSubscription.Periodicity,
            TargetPlanVersion.CurrencyCode,
            Mode,
            EffectiveDateUtc,
            ActiveEmployeeCount,
            EstimatedNextCharge,
            IsEligible,
            IneligibilityReasons,
            AddonCompatibilityWarnings);
}

internal static class PlatformSubscriptionPlanChangeResolver
{
    public static async Task<Result<PlatformSubscriptionPlanChangeResolution>> ResolveAsync(
        Guid companyPublicId,
        Guid commercialPlanPublicId,
        SubscriptionPlanChangeMode mode,
        DateTime? requestedEffectiveDateUtc,
        ICompanyRepository companyRepository,
        ICommercialPlanRepository commercialPlanRepository,
        ICompanySubscriptionRepository subscriptionRepository,
        IUserCompanyRepository userCompanyRepository,
        ILegalRepresentativeRepository legalRepresentativeRepository,
        IPersonnelFileRepository personnelFileRepository,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var company = await companyRepository.FindByPublicIdAsync(companyPublicId, cancellationToken);
        if (company is null)
        {
            return Result<PlatformSubscriptionPlanChangeResolution>.Failure(PlatformSubscriptionErrors.CompanyNotFound);
        }

        var targetPlan = await commercialPlanRepository.GetByIdAsync(commercialPlanPublicId, cancellationToken);
        if (targetPlan is null)
        {
            return Result<PlatformSubscriptionPlanChangeResolution>.Failure(PlatformSubscriptionErrors.PlanNotFound);
        }

        if (!Enum.IsDefined(mode))
        {
            return Result<PlatformSubscriptionPlanChangeResolution>.Failure(PlatformSubscriptionErrors.PlanChangeInvalidMode);
        }

        var currentSubscription = await subscriptionRepository.GetCurrentByCompanyIdAsync(company.Id, cancellationToken);
        if (currentSubscription is null)
        {
            return Result<PlatformSubscriptionPlanChangeResolution>.Failure(PlatformSubscriptionErrors.SubscriptionNotFound);
        }

        CommercialPlan? currentPlan = null;
        CommercialPlanVersion? currentPlanVersion = null;
        if (currentSubscription.CommercialPlanId != 0)
        {
            currentPlan = await commercialPlanRepository.GetByInternalIdAsync(currentSubscription.CommercialPlanId, cancellationToken);
            if (currentPlan is not null && currentSubscription.CommercialPlanVersionId != 0)
            {
                currentPlanVersion = currentPlan.Versions.SingleOrDefault(version => version.Id == currentSubscription.CommercialPlanVersionId);
            }
        }

        if (targetPlan.Status != CommercialPlanStatus.Active)
        {
            return Result<PlatformSubscriptionPlanChangeResolution>.Failure(PlatformSubscriptionErrors.PlanInactive);
        }

        var effectiveDateResolution = ResolveEffectiveDate(
            currentSubscription,
            mode,
            requestedEffectiveDateUtc,
            utcNow);

        if (effectiveDateResolution.IsFailure)
        {
            return Result<PlatformSubscriptionPlanChangeResolution>.Failure(effectiveDateResolution.Error);
        }

        CommercialPlanVersion targetPlanVersion;
        try
        {
            targetPlanVersion = targetPlan.GetVersionEffectiveOn(effectiveDateResolution.Value);
        }
        catch (InvalidOperationException)
        {
            return Result<PlatformSubscriptionPlanChangeResolution>.Failure(PlatformSubscriptionErrors.PlanVersionNotAvailable);
        }

        var scheduledPlanChange = await subscriptionRepository.GetScheduledPlanChangeByCompanyIdAsync(company.Id, cancellationToken);
        var reasons = new List<string>();
        var errors = new List<Error>();

        if (company.Status != CompanyStatus.Active)
        {
            reasons.Add("La empresa debe estar activa para cambiar de plan.");
            errors.Add(PlatformSubscriptionErrors.CompanyNotEligible);
        }

        if (currentSubscription.Status != SubscriptionStatus.Active)
        {
            reasons.Add("Solo las suscripciones activas pueden cambiar de plan en este MVP.");
            errors.Add(PlatformSubscriptionErrors.PlanChangeUnsupportedCurrentStatus);
        }

        if (await legalRepresentativeRepository.GetActiveCountAsync(company.PublicId, cancellationToken) <= 0)
        {
            reasons.Add("La empresa debe tener al menos un representante legal activo.");
            errors.Add(PlatformSubscriptionErrors.MissingLegalRepresentative);
        }

        if (!await userCompanyRepository.HasAnyActiveAdministratorAsync(company.PublicId, cancellationToken))
        {
            reasons.Add("La empresa debe tener al menos un owner o administrador activo.");
            errors.Add(PlatformSubscriptionErrors.MissingAdministrator);
        }

        if (scheduledPlanChange is not null)
        {
            reasons.Add("La empresa ya tiene un cambio de plan programado pendiente.");
            errors.Add(PlatformSubscriptionErrors.PlanChangePendingConflict);
        }

        if (currentSubscription.CommercialPlanId == targetPlan.Id &&
            currentSubscription.CommercialPlanVersionId == targetPlanVersion.Id)
        {
            reasons.Add("La empresa ya usa el mismo plan y version efectiva solicitados.");
            errors.Add(PlatformSubscriptionErrors.PlanChangeSamePlanVersion);
        }

        if (currentSubscription.ExpiresAtUtc.HasValue &&
            effectiveDateResolution.Value.Date > currentSubscription.ExpiresAtUtc.Value.Date)
        {
            reasons.Add("La fecha efectiva del cambio no puede ser posterior al vencimiento de la suscripcion actual.");
            errors.Add(PlatformSubscriptionErrors.PlanChangeDatePastExpiration);
        }

        var activeEmployeeCount = await personnelFileRepository.CountActiveEmployeesAsync(company.PublicId, cancellationToken);
        var estimatedNextCharge = targetPlanVersion.BaseMonthlyFee + (targetPlanVersion.PricePerActiveEmployee * activeEmployeeCount);
        var addonCompatibilityWarnings = Array.Empty<string>();

        if (string.Equals(targetPlan.Code, ProvisioningConstants.FreePlanCode, StringComparison.Ordinal))
        {
            var activeAddons = await subscriptionRepository.SearchCompanyAddonsByCompanyPublicIdAsync(
                company.PublicId,
                CompanyAddonStatus.Active,
                search: null,
                pageNumber: 1,
                pageSize: 100,
                cancellationToken);

            addonCompatibilityWarnings = activeAddons.Items
                .Select(static addon => $"The add-on {addon.AddonCode} will be deactivated when the company moves to FREE.")
                .ToArray();
        }

        return Result<PlatformSubscriptionPlanChangeResolution>.Success(new PlatformSubscriptionPlanChangeResolution(
            company,
            currentSubscription,
            currentPlan,
            currentPlanVersion,
            targetPlan,
            targetPlanVersion,
            mode,
            effectiveDateResolution.Value,
            activeEmployeeCount,
            estimatedNextCharge,
            reasons,
            addonCompatibilityWarnings,
            errors.FirstOrDefault() ?? PlatformSubscriptionErrors.PlanChangeUnsupportedCurrentStatus));
    }

    private static Result<DateTime> ResolveEffectiveDate(
        CompanySubscription currentSubscription,
        SubscriptionPlanChangeMode mode,
        DateTime? requestedEffectiveDateUtc,
        DateTime utcNow)
    {
        return mode switch
        {
            SubscriptionPlanChangeMode.Immediate => Result<DateTime>.Success(utcNow.Date),
            SubscriptionPlanChangeMode.SpecificDate => ResolveSpecificDate(requestedEffectiveDateUtc, utcNow),
            SubscriptionPlanChangeMode.NextBillingCycle => Result<DateTime>.Success(
                PlatformSubscriptionAdministrationHelpers.GetNextBillingCycleDate(currentSubscription, utcNow)),
            _ => Result<DateTime>.Failure(PlatformSubscriptionErrors.PlanChangeInvalidMode)
        };
    }

    private static Result<DateTime> ResolveSpecificDate(DateTime? requestedEffectiveDateUtc, DateTime utcNow)
    {
        if (!requestedEffectiveDateUtc.HasValue || requestedEffectiveDateUtc.Value == default)
        {
            return Result<DateTime>.Failure(PlatformSubscriptionErrors.PlanChangeEffectiveDateRequired);
        }

        if (requestedEffectiveDateUtc.Value.Date < utcNow.Date)
        {
            return Result<DateTime>.Failure(PlatformSubscriptionErrors.PlanChangeEffectiveDateInPast);
        }

        return Result<DateTime>.Success(requestedEffectiveDateUtc.Value.Date);
    }
}

internal static class PlatformSubscriptionResolver
{
    public static async Task<Result<PlatformSubscriptionResolution>> ResolveAsync(
        Guid companyPublicId,
        Guid commercialPlanPublicId,
        DateTime startDateUtc,
        DateTime? expiresAtUtc,
        CompanySubscriptionPeriodicity periodicity,
        ICompanyRepository companyRepository,
        ICommercialPlanRepository commercialPlanRepository,
        ICompanySubscriptionRepository subscriptionRepository,
        IUserCompanyRepository userCompanyRepository,
        ILegalRepresentativeRepository legalRepresentativeRepository,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var company = await companyRepository.FindByPublicIdAsync(companyPublicId, cancellationToken);
        if (company is null)
        {
            return Result<PlatformSubscriptionResolution>.Failure(PlatformSubscriptionErrors.CompanyNotFound);
        }

        var plan = await commercialPlanRepository.GetByIdAsync(commercialPlanPublicId, cancellationToken);
        if (plan is null)
        {
            return Result<PlatformSubscriptionResolution>.Failure(PlatformSubscriptionErrors.PlanNotFound);
        }

        if (!Enum.IsDefined(periodicity))
        {
            return Result<PlatformSubscriptionResolution>.Failure(PlatformSubscriptionErrors.InvalidPeriodicity);
        }

        if (startDateUtc == default || startDateUtc.Date < utcNow.Date)
        {
            return Result<PlatformSubscriptionResolution>.Failure(PlatformSubscriptionErrors.StartDateInPast);
        }

        if (expiresAtUtc.HasValue && expiresAtUtc.Value.Date < startDateUtc.Date)
        {
            return Result<PlatformSubscriptionResolution>.Failure(PlatformSubscriptionErrors.ExpirationBeforeStartDate);
        }

        if (plan.Status != CommercialPlanStatus.Active)
        {
            return Result<PlatformSubscriptionResolution>.Failure(PlatformSubscriptionErrors.PlanInactive);
        }

        CommercialPlanVersion? planVersion;
        try
        {
            planVersion = plan.GetVersionEffectiveOn(startDateUtc);
        }
        catch (InvalidOperationException)
        {
            return Result<PlatformSubscriptionResolution>.Failure(PlatformSubscriptionErrors.PlanVersionNotAvailable);
        }

        var currentSubscription = await subscriptionRepository.GetCurrentByCompanyIdAsync(company.Id, cancellationToken);
        var scheduledSubscription = await subscriptionRepository.GetScheduledByCompanyIdAsync(company.Id, cancellationToken);
        var resolvedStatus = startDateUtc.Date > utcNow.Date
            ? SubscriptionStatus.Scheduled
            : SubscriptionStatus.Active;

        var reasons = new List<string>();
        var errors = new List<Error>();

        if (company.Status != CompanyStatus.Active)
        {
            reasons.Add("La empresa debe estar activa para contratar una suscripcion.");
            errors.Add(PlatformSubscriptionErrors.CompanyNotEligible);
        }

        if (await legalRepresentativeRepository.GetActiveCountAsync(company.PublicId, cancellationToken) <= 0)
        {
            reasons.Add("La empresa debe tener al menos un representante legal activo.");
            errors.Add(PlatformSubscriptionErrors.MissingLegalRepresentative);
        }

        if (!await userCompanyRepository.HasAnyActiveAdministratorAsync(company.PublicId, cancellationToken))
        {
            reasons.Add("La empresa debe tener al menos un owner o administrador activo.");
            errors.Add(PlatformSubscriptionErrors.MissingAdministrator);
        }

        if (resolvedStatus == SubscriptionStatus.Active &&
            currentSubscription is not null &&
            currentSubscription.CommercialPlanId == plan.Id &&
            currentSubscription.CommercialPlanVersionId == planVersion.Id &&
            currentSubscription.Periodicity == periodicity)
        {
            reasons.Add("La empresa ya usa una suscripcion vigente con el mismo plan, version y periodicidad.");
            errors.Add(PlatformSubscriptionErrors.AlreadyAssigned);
        }

        if (scheduledSubscription is not null)
        {
            reasons.Add("La empresa ya tiene una suscripcion programada pendiente de entrada en vigor.");
            errors.Add(PlatformSubscriptionErrors.ScheduledConflict);
        }

        return Result<PlatformSubscriptionResolution>.Success(new PlatformSubscriptionResolution(
            company,
            plan,
            planVersion,
            periodicity,
            startDateUtc.Date,
            expiresAtUtc?.Date,
            resolvedStatus,
            currentSubscription,
            scheduledSubscription,
            reasons,
            errors.FirstOrDefault() ?? PlatformSubscriptionErrors.CompanyNotEligible));
    }
}

internal static class PlatformSubscriptionBillablePolicy
{
    public static async Task ApplyBillableStateAsync(
        Company company,
        CompanySubscription subscription,
        ICommercialPlanRepository commercialPlanRepository,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var isSystemPlan = await commercialPlanRepository.IsSystemPlanAsync(subscription.CommercialPlanId, cancellationToken);
        if (isSystemPlan || !SubscriptionStatusPolicy.CanGenerateCharges(subscription.Status))
        {
            company.ClearBillable();
            return;
        }

        company.MarkBillable(utcNow.Date);
    }
}

internal static class PlatformSubscriptionAdministrationHelpers
{
    public static Guid? TryParseCurrentUserId(string? currentUserId) =>
        Guid.TryParse(currentUserId, out var parsedUserId) ? parsedUserId : null;

    public static DateTime GetNextBillingCycleDate(CompanySubscription subscription, DateTime utcNow) =>
        subscription.Periodicity switch
        {
            CompanySubscriptionPeriodicity.Monthly => GetNextMonthlyCycleDate(subscription.StartDateUtc, utcNow),
            CompanySubscriptionPeriodicity.Annual => GetNextAnnualCycleDate(subscription.StartDateUtc, utcNow),
            _ => throw new InvalidOperationException($"Unsupported subscription periodicity '{subscription.Periodicity}'.")
        };

    private static DateTime GetNextMonthlyCycleDate(DateTime anchorDateUtc, DateTime utcNow)
    {
        var monthsDifference = Math.Max(
            0,
            ((utcNow.Year - anchorDateUtc.Year) * 12) + utcNow.Month - anchorDateUtc.Month);

        var candidate = AddMonthsKeepingAnchor(anchorDateUtc.Date, monthsDifference);
        while (candidate <= utcNow.Date)
        {
            monthsDifference++;
            candidate = AddMonthsKeepingAnchor(anchorDateUtc.Date, monthsDifference);
        }

        return candidate;
    }

    private static DateTime GetNextAnnualCycleDate(DateTime anchorDateUtc, DateTime utcNow)
    {
        var yearsDifference = Math.Max(0, utcNow.Year - anchorDateUtc.Year);
        var candidate = AddYearsKeepingAnchor(anchorDateUtc.Date, yearsDifference);
        while (candidate <= utcNow.Date)
        {
            yearsDifference++;
            candidate = AddYearsKeepingAnchor(anchorDateUtc.Date, yearsDifference);
        }

        return candidate;
    }

    private static DateTime AddMonthsKeepingAnchor(DateTime anchorDateUtc, int monthsToAdd)
    {
        var firstDay = new DateTime(anchorDateUtc.Year, anchorDateUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(monthsToAdd);
        var day = Math.Min(anchorDateUtc.Day, DateTime.DaysInMonth(firstDay.Year, firstDay.Month));
        return new DateTime(firstDay.Year, firstDay.Month, day, 0, 0, 0, DateTimeKind.Utc);
    }

    private static DateTime AddYearsKeepingAnchor(DateTime anchorDateUtc, int yearsToAdd)
    {
        var year = anchorDateUtc.Year + yearsToAdd;
        var month = anchorDateUtc.Month;
        var day = Math.Min(anchorDateUtc.Day, DateTime.DaysInMonth(year, month));
        return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);
    }
}

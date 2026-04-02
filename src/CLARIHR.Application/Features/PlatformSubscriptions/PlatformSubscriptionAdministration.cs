using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.LegalRepresentatives;
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

        if (command.TargetStatus == SubscriptionStatus.Active &&
            subscription.ExpiresAtUtc.HasValue &&
            subscription.ExpiresAtUtc.Value.Date < dateTimeProvider.UtcNow.Date)
        {
            return Result<PlatformCompanySubscriptionResponse>.Failure(PlatformSubscriptionErrors.ReactivationPastExpiration);
        }

        var before = await subscriptionRepository.GetOverviewByCompanyPublicIdAsync(command.CompanyId, cancellationToken);
        var actorUserPublicId = PlatformSubscriptionAdministrationHelpers.TryParseCurrentUserId(currentUserService.UserId);
        var utcNow = dateTimeProvider.UtcNow;
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
}

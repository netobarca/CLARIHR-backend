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

        var actorUserPublicId = Guid.TryParse(currentUserService.UserId, out var parsedUserId)
            ? parsedUserId
            : Guid.Empty;

        var before = await subscriptionRepository.GetOverviewByCompanyPublicIdAsync(command.CompanyId, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            if (context.ResolvedStatus == SubscriptionStatus.Active && context.ActiveSubscription is not null)
            {
                context.ActiveSubscription.Cancel(utcNow);
            }

            var nextSubscription = context.ResolvedStatus == SubscriptionStatus.Active
                ? CompanySubscription.Activate(
                    context.Company.Id,
                    context.Plan,
                    context.Periodicity,
                    context.StartDateUtc,
                    actorUserPublicId,
                    utcNow)
                : CompanySubscription.Schedule(
                    context.Company.Id,
                    context.Plan,
                    context.Periodicity,
                    context.StartDateUtc,
                    actorUserPublicId,
                    utcNow);

            subscriptionRepository.Add(nextSubscription);

            if (context.ResolvedStatus == SubscriptionStatus.Active)
            {
                if (context.Plan.IsSystemPlan)
                {
                    context.Company.ClearBillable();
                }
                else
                {
                    context.Company.MarkBillable(utcNow);
                }
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var overviewAfter = await subscriptionRepository.GetOverviewByCompanyPublicIdAsync(command.CompanyId, cancellationToken);
            var response = overviewAfter?.CurrentSubscription?.SubscriptionId == nextSubscription.PublicId
                ? overviewAfter.CurrentSubscription
                : overviewAfter?.ScheduledReplacement?.SubscriptionId == nextSubscription.PublicId
                    ? overviewAfter.ScheduledReplacement
                    : null;

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
                    nextSubscription.PublicId,
                    context.Company.Slug,
                    context.ResolvedStatus == SubscriptionStatus.Active ? AuditActions.Create : AuditActions.Update,
                    context.ResolvedStatus == SubscriptionStatus.Active
                        ? $"Activated commercial subscription for {context.Company.Name} using plan {context.Plan.Code}."
                        : $"Scheduled commercial subscription for {context.Company.Name} using plan {context.Plan.Code}.",
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

internal sealed record PlatformSubscriptionResolution(
    Company Company,
    CommercialPlan Plan,
    CommercialPlanVersion PlanVersion,
    CompanySubscriptionPeriodicity Periodicity,
    DateTime StartDateUtc,
    SubscriptionStatus ResolvedStatus,
    CompanySubscription? ActiveSubscription,
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
            IsEligible,
            IneligibilityReasons);
}

internal static class PlatformSubscriptionResolver
{
    public static async Task<Result<PlatformSubscriptionResolution>> ResolveAsync(
        Guid companyPublicId,
        Guid commercialPlanPublicId,
        DateTime startDateUtc,
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

        var activeSubscription = await subscriptionRepository.GetActiveByCompanyIdAsync(company.Id, cancellationToken);
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
            activeSubscription is not null &&
            activeSubscription.CommercialPlanId == plan.Id &&
            activeSubscription.CommercialPlanVersionId == planVersion.Id &&
            activeSubscription.Periodicity == periodicity)
        {
            reasons.Add("La empresa ya tiene activa una suscripcion con el mismo plan, version y periodicidad.");
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
            startDateUtc,
            resolvedStatus,
            activeSubscription,
            scheduledSubscription,
            reasons,
            errors.FirstOrDefault() ?? PlatformSubscriptionErrors.CompanyNotEligible));
    }
}

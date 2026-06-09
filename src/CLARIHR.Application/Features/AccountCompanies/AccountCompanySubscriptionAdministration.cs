using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.LegalRepresentatives;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Platform;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.AccountCompanies.Common;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.PlatformSubscriptions;
using CLARIHR.Application.Features.PlatformSubscriptions.Common;
using CLARIHR.Application.Features.Provisioning.Common;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Companies;

namespace CLARIHR.Application.Features.AccountCompanies;

internal sealed class GetOwnedCompanySubscriptionQueryHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    ICompanyRepository companyRepository,
    ICompanySubscriptionRepository subscriptionRepository,
    ICommercialPlanRepository commercialPlanRepository,
    ICommercialAddonRepository commercialAddonRepository,
    IPlanEntitlementService planEntitlementService)
    : IQueryHandler<GetOwnedCompanySubscriptionQuery, AccountCompanySubscriptionOverviewResponse>
{
    public async Task<Result<AccountCompanySubscriptionOverviewResponse>> Handle(
        GetOwnedCompanySubscriptionQuery query,
        CancellationToken cancellationToken)
    {
        var ownershipResult = await AccountCompanySubscriptionHelper.ResolveOwnershipAsync(
            currentUserService,
            userRepository,
            companyRepository,
            query.CompanyId,
            cancellationToken);

        if (ownershipResult.IsFailure)
        {
            return Result<AccountCompanySubscriptionOverviewResponse>.Failure(ownershipResult.Error);
        }

        var overview = await AccountCompanySubscriptionHelper.BuildOverviewAsync(
            ownershipResult.Value.Company,
            subscriptionRepository,
            commercialPlanRepository,
            commercialAddonRepository,
            planEntitlementService,
            cancellationToken);

        return overview is null
            ? Result<AccountCompanySubscriptionOverviewResponse>.Failure(PlatformSubscriptionErrors.SubscriptionNotFound)
            : Result<AccountCompanySubscriptionOverviewResponse>.Success(overview);
    }
}

internal sealed class GetOwnedCompanySubscriptionPlansQueryHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    ICompanyRepository companyRepository,
    ICompanySubscriptionRepository subscriptionRepository,
    ICommercialPlanRepository commercialPlanRepository,
    IPlatformOperatorRepository platformOperatorRepository)
    : IQueryHandler<GetOwnedCompanySubscriptionPlansQuery, IReadOnlyCollection<AccountCompanySubscriptionPlanResponse>>
{
    public async Task<Result<IReadOnlyCollection<AccountCompanySubscriptionPlanResponse>>> Handle(
        GetOwnedCompanySubscriptionPlansQuery query,
        CancellationToken cancellationToken)
    {
        var ownershipResult = await AccountCompanySubscriptionHelper.ResolveOwnershipAsync(
            currentUserService,
            userRepository,
            companyRepository,
            query.CompanyId,
            cancellationToken);

        if (ownershipResult.IsFailure)
        {
            return Result<IReadOnlyCollection<AccountCompanySubscriptionPlanResponse>>.Failure(ownershipResult.Error);
        }

        var currentSubscription = await subscriptionRepository.GetActiveByCompanyIdAsync(
            ownershipResult.Value.Company.Id,
            cancellationToken);

        if (currentSubscription is null)
        {
            return Result<IReadOnlyCollection<AccountCompanySubscriptionPlanResponse>>.Failure(PlatformSubscriptionErrors.SubscriptionNotFound);
        }

        var canAccessMasterPlan = await AccountCompanySubscriptionHelper.CanAccessMasterPlanAsync(
            ownershipResult.Value.Owner.PublicId,
            platformOperatorRepository,
            cancellationToken);

        var plans = await commercialPlanRepository.ListActiveAsync(cancellationToken);
        var response = plans
            .Where(plan => canAccessMasterPlan || !AccountCompanySubscriptionHelper.IsMasterPlan(plan))
            .Select(plan => AccountCompanySubscriptionHelper.MapPlan(
                plan,
                isCurrent: plan.Id == currentSubscription.CommercialPlanId))
            .ToArray();

        return Result<IReadOnlyCollection<AccountCompanySubscriptionPlanResponse>>.Success(response);
    }
}

internal sealed class PreviewOwnedCompanySubscriptionPlanChangeQueryHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    ICompanyRepository companyRepository,
    ICompanySubscriptionRepository subscriptionRepository,
    ICommercialPlanRepository commercialPlanRepository,
    IUserCompanyRepository userCompanyRepository,
    ILegalRepresentativeRepository legalRepresentativeRepository,
    IPersonnelFileRepository personnelFileRepository,
    ICommercialAddonRepository commercialAddonRepository,
    IPlanEntitlementService planEntitlementService,
    IDateTimeProvider dateTimeProvider,
    IPlatformOperatorRepository platformOperatorRepository)
    : IQueryHandler<PreviewOwnedCompanySubscriptionPlanChangeQuery, AccountCompanySubscriptionPlanPreviewResponse>
{
    public async Task<Result<AccountCompanySubscriptionPlanPreviewResponse>> Handle(
        PreviewOwnedCompanySubscriptionPlanChangeQuery query,
        CancellationToken cancellationToken)
    {
        var ownershipResult = await AccountCompanySubscriptionHelper.ResolveOwnershipAsync(
            currentUserService,
            userRepository,
            companyRepository,
            query.CompanyId,
            cancellationToken);

        if (ownershipResult.IsFailure)
        {
            return Result<AccountCompanySubscriptionPlanPreviewResponse>.Failure(ownershipResult.Error);
        }

        var resolution = await PlatformSubscriptionPlanChangeResolver.ResolveAsync(
            query.CompanyId,
            query.CommercialPlanId,
            SubscriptionPlanChangeMode.Immediate,
            requestedEffectiveDateUtc: null,
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
            return Result<AccountCompanySubscriptionPlanPreviewResponse>.Failure(resolution.Error);
        }

        var accessResult = await AccountCompanySubscriptionHelper.EnsureMasterPlanAccessAsync(
            ownershipResult.Value.Owner.PublicId,
            resolution.Value.TargetPlan,
            platformOperatorRepository,
            cancellationToken);

        if (accessResult.IsFailure)
        {
            return Result<AccountCompanySubscriptionPlanPreviewResponse>.Failure(accessResult.Error);
        }

        var preview = await AccountCompanySubscriptionHelper.BuildPlanPreviewAsync(
            resolution.Value,
            subscriptionRepository,
            commercialAddonRepository,
            planEntitlementService,
            cancellationToken);

        return Result<AccountCompanySubscriptionPlanPreviewResponse>.Success(preview);
    }
}

internal sealed class ChangeOwnedCompanySubscriptionCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    ICompanyRepository companyRepository,
    ICompanySubscriptionRepository subscriptionRepository,
    ICommercialPlanRepository commercialPlanRepository,
    ICommercialAddonRepository commercialAddonRepository,
    IUserCompanyRepository userCompanyRepository,
    ILegalRepresentativeRepository legalRepresentativeRepository,
    IPersonnelFileRepository personnelFileRepository,
    IPlanEntitlementService planEntitlementService,
    IAuditService auditService,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork,
    IPlatformOperatorRepository platformOperatorRepository)
    : ICommandHandler<ChangeOwnedCompanySubscriptionCommand, AccountCompanySubscriptionOverviewResponse>
{
    public async Task<Result<AccountCompanySubscriptionOverviewResponse>> Handle(
        ChangeOwnedCompanySubscriptionCommand command,
        CancellationToken cancellationToken)
    {
        var ownershipResult = await AccountCompanySubscriptionHelper.ResolveOwnershipAsync(
            currentUserService,
            userRepository,
            companyRepository,
            command.CompanyId,
            cancellationToken);

        if (ownershipResult.IsFailure)
        {
            return Result<AccountCompanySubscriptionOverviewResponse>.Failure(ownershipResult.Error);
        }

        var owner = ownershipResult.Value.Owner;
        var company = ownershipResult.Value.Company;
        var utcNow = dateTimeProvider.UtcNow;

        var resolution = await PlatformSubscriptionPlanChangeResolver.ResolveAsync(
            command.CompanyId,
            command.CommercialPlanId,
            SubscriptionPlanChangeMode.Immediate,
            requestedEffectiveDateUtc: null,
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
            return Result<AccountCompanySubscriptionOverviewResponse>.Failure(resolution.Error);
        }

        var context = resolution.Value;
        var accessResult = await AccountCompanySubscriptionHelper.EnsureMasterPlanAccessAsync(
            owner.PublicId,
            context.TargetPlan,
            platformOperatorRepository,
            cancellationToken);

        if (accessResult.IsFailure)
        {
            return Result<AccountCompanySubscriptionOverviewResponse>.Failure(accessResult.Error);
        }

        if (context.CurrentSubscription.ConcurrencyToken != command.ConcurrencyToken)
        {
            // PR-B: optimistic concurrency — reject a plan change applied on a stale overview (the
            // active subscription was replaced/mutated since the owner loaded it).
            return Result<AccountCompanySubscriptionOverviewResponse>.Failure(AccountCompanyErrors.ConcurrencyConflict);
        }

        if (!context.IsEligible)
        {
            return Result<AccountCompanySubscriptionOverviewResponse>.Failure(context.PrimaryError);
        }

        var before = await AccountCompanySubscriptionHelper.BuildOverviewAsync(
            company,
            subscriptionRepository,
            commercialPlanRepository,
            commercialAddonRepository,
            planEntitlementService,
            cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var reasonCode = string.Equals(context.TargetPlan.Code, ProvisioningConstants.FreePlanCode, StringComparison.Ordinal)
                ? SubscriptionPlanChangeReasonCode.DowngradeRequestedByCustomer
                : SubscriptionPlanChangeReasonCode.UpgradeCommercial;

            var planChange = CompanySubscriptionPlanChange.Create(
                context.CurrentSubscription,
                context.CurrentPlan,
                context.CurrentPlanVersion,
                context.TargetPlan,
                context.TargetPlanVersion,
                context.CurrentSubscription.Periodicity,
                SubscriptionPlanChangeMode.Immediate,
                reasonCode,
                utcNow,
                context.EffectiveDateUtc,
                owner.PublicId,
                command.Observations,
                context.EstimatedNextCharge,
                context.ActiveEmployeeCount);

            subscriptionRepository.AddPlanChange(planChange);

            context.CurrentSubscription.Cancel(
                utcNow,
                SubscriptionStatusChangeReasonCode.SubscriptionReplacement,
                observations: null,
                SubscriptionStatusChangeOrigin.CompanyOwner,
                owner.PublicId);

            var nextSubscription = CompanySubscription.Activate(
                context.Company.Id,
                context.TargetPlan,
                context.CurrentSubscription.Periodicity,
                context.EffectiveDateUtc,
                context.CurrentSubscription.ExpiresAtUtc,
                owner.PublicId,
                utcNow,
                SubscriptionStatusChangeReasonCode.PlanChangeApplied,
                SubscriptionStatusChangeOrigin.CompanyOwner,
                command.Observations);

            subscriptionRepository.Add(nextSubscription);
            planChange.MarkApplied(utcNow, nextSubscription.PublicId);

            if (string.Equals(context.TargetPlan.Code, ProvisioningConstants.FreePlanCode, StringComparison.Ordinal))
            {
                await AccountCompanySubscriptionHelper.DeactivateAddonsForFreePlanAsync(
                    company,
                    owner.PublicId,
                    utcNow,
                    subscriptionRepository,
                    cancellationToken);
            }

            await PlatformSubscriptionBillablePolicy.ApplyBillableStateAsync(
                context.Company,
                nextSubscription,
                commercialPlanRepository,
                utcNow,
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await AccountCompanySubscriptionHelper.BuildOverviewAsync(
                company,
                subscriptionRepository,
                commercialPlanRepository,
                commercialAddonRepository,
                planEntitlementService,
                cancellationToken);

            await auditService.LogForTenantAsync(
                company.PublicId,
                new AuditLogEntry(
                    AuditEventTypes.CompanySubscriptionPlanChangeApplied,
                    AuditEntityTypes.CompanySubscriptionPlanChange,
                    planChange.PublicId,
                    company.Slug,
                    AuditActions.Update,
                    $"Owner changed subscription plan for {company.Name} from {context.CurrentSubscription.PlanCode} to {context.TargetPlan.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return after is null
                ? Result<AccountCompanySubscriptionOverviewResponse>.Failure(PlatformSubscriptionErrors.SubscriptionNotFound)
                : Result<AccountCompanySubscriptionOverviewResponse>.Success(after);
        }
        catch (UniqueConstraintViolationException ex) when (CompanySubscriptionConstraintViolations.IsConcurrencyConflict(ex.ConstraintName))
        {
            // ACS-A: a concurrent double-submit collides on a filtered unique index (one live / one
            // scheduled subscription, one scheduled plan-change, one company-add-on, one scheduled
            // add-on-change) → surface the race as 409 CONCURRENCY_CONFLICT instead of an opaque 500.
            await transaction.RollbackAsync(cancellationToken);
            return Result<AccountCompanySubscriptionOverviewResponse>.Failure(AccountCompanyErrors.ConcurrencyConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class GetOwnedCompanySubscriptionAddonsQueryHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    ICompanyRepository companyRepository,
    ICompanySubscriptionRepository subscriptionRepository,
    ICommercialAddonRepository commercialAddonRepository)
    : IQueryHandler<GetOwnedCompanySubscriptionAddonsQuery, IReadOnlyCollection<AccountCompanySubscriptionAddonResponse>>
{
    public async Task<Result<IReadOnlyCollection<AccountCompanySubscriptionAddonResponse>>> Handle(
        GetOwnedCompanySubscriptionAddonsQuery query,
        CancellationToken cancellationToken)
    {
        var ownershipResult = await AccountCompanySubscriptionHelper.ResolveOwnershipAsync(
            currentUserService,
            userRepository,
            companyRepository,
            query.CompanyId,
            cancellationToken);

        if (ownershipResult.IsFailure)
        {
            return Result<IReadOnlyCollection<AccountCompanySubscriptionAddonResponse>>.Failure(ownershipResult.Error);
        }

        var response = await AccountCompanySubscriptionHelper.GetActiveAddonsAsync(
            ownershipResult.Value.Company,
            subscriptionRepository,
            commercialAddonRepository,
            cancellationToken);

        return Result<IReadOnlyCollection<AccountCompanySubscriptionAddonResponse>>.Success(response);
    }
}

internal sealed class GetOwnedCompanySubscriptionMarketplaceQueryHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    ICompanyRepository companyRepository,
    ICompanySubscriptionRepository subscriptionRepository,
    ICommercialAddonRepository commercialAddonRepository)
    : IQueryHandler<GetOwnedCompanySubscriptionMarketplaceQuery, IReadOnlyCollection<AccountCompanyMarketplaceAddonResponse>>
{
    public async Task<Result<IReadOnlyCollection<AccountCompanyMarketplaceAddonResponse>>> Handle(
        GetOwnedCompanySubscriptionMarketplaceQuery query,
        CancellationToken cancellationToken)
    {
        var ownershipResult = await AccountCompanySubscriptionHelper.ResolveOwnershipAsync(
            currentUserService,
            userRepository,
            companyRepository,
            query.CompanyId,
            cancellationToken);

        if (ownershipResult.IsFailure)
        {
            return Result<IReadOnlyCollection<AccountCompanyMarketplaceAddonResponse>>.Failure(ownershipResult.Error);
        }

        var currentSubscription = await subscriptionRepository.GetActiveByCompanyIdAsync(
            ownershipResult.Value.Company.Id,
            cancellationToken);

        if (currentSubscription is null)
        {
            return Result<IReadOnlyCollection<AccountCompanyMarketplaceAddonResponse>>.Failure(PlatformSubscriptionErrors.SubscriptionNotFound);
        }

        var activeAddons = await AccountCompanySubscriptionHelper.GetActiveAddonsAsync(
            ownershipResult.Value.Company,
            subscriptionRepository,
            commercialAddonRepository,
            cancellationToken);
        var ownedAddonIds = activeAddons
            .Select(addon => addon.CommercialAddonId)
            .ToHashSet();

        var canUseMarketplace = !string.Equals(currentSubscription.PlanCode, ProvisioningConstants.FreePlanCode, StringComparison.Ordinal);
        var addons = await commercialAddonRepository.ListActiveAsync(cancellationToken);
        var response = addons
            .Select(addon =>
            {
                var isOwned = ownedAddonIds.Contains(addon.PublicId);
                var canAcquire = canUseMarketplace && !isOwned;
                var blockedReason = canAcquire
                    ? null
                    : isOwned
                        ? "The add-on is already active for the company."
                        : "The FREE subscription must upgrade before acquiring add-ons.";

                return new AccountCompanyMarketplaceAddonResponse(
                    addon.PublicId,
                    addon.Code,
                    addon.Name,
                    addon.Description,
                    addon.Type,
                    addon.BillingModel,
                    addon.MeasurementUnit,
                    addon.UnitPrice,
                    addon.MinimumQuantity,
                    addon.MinimumMonthlyFee,
                    addon.Periodicity,
                    addon.Entitlements.Count(entitlement => entitlement.IsEnabled),
                    addon.Entitlements
                        .Where(entitlement => entitlement.IsEnabled)
                        .OrderBy(entitlement => entitlement.ModuleKey, StringComparer.Ordinal)
                        .Select(entitlement => entitlement.ModuleKey)
                        .ToArray(),
                    isOwned,
                    canAcquire,
                    blockedReason);
            })
            .ToArray();

        return Result<IReadOnlyCollection<AccountCompanyMarketplaceAddonResponse>>.Success(response);
    }
}

internal sealed class PreviewOwnedCompanyAddonChangeQueryHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    ICompanyRepository companyRepository,
    ICompanySubscriptionRepository subscriptionRepository,
    ICommercialPlanRepository commercialPlanRepository,
    ICommercialAddonRepository commercialAddonRepository,
    IPersonnelFileRepository personnelFileRepository,
    IPlanEntitlementService planEntitlementService,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<PreviewOwnedCompanyAddonChangeQuery, AccountCompanyAddonChangePreviewResponse>
{
    public async Task<Result<AccountCompanyAddonChangePreviewResponse>> Handle(
        PreviewOwnedCompanyAddonChangeQuery query,
        CancellationToken cancellationToken)
    {
        var ownershipResult = await AccountCompanySubscriptionHelper.ResolveOwnershipAsync(
            currentUserService,
            userRepository,
            companyRepository,
            query.CompanyId,
            cancellationToken);

        if (ownershipResult.IsFailure)
        {
            return Result<AccountCompanyAddonChangePreviewResponse>.Failure(ownershipResult.Error);
        }

        var resolution = await PlatformCompanyAddonChangeResolver.ResolveAsync(
            query.CompanyId,
            query.CommercialAddonId,
            query.Action,
            SubscriptionAddonChangeMode.Immediate,
            requestedEffectiveDateUtc: null,
            companyRepository,
            commercialAddonRepository,
            subscriptionRepository,
            personnelFileRepository,
            dateTimeProvider.UtcNow,
            cancellationToken);

        if (resolution.IsFailure)
        {
            return Result<AccountCompanyAddonChangePreviewResponse>.Failure(resolution.Error);
        }

        var preview = await AccountCompanySubscriptionHelper.BuildAddonPreviewAsync(
            resolution.Value,
            commercialPlanRepository,
            subscriptionRepository,
            commercialAddonRepository,
            planEntitlementService,
            cancellationToken);

        return Result<AccountCompanyAddonChangePreviewResponse>.Success(preview);
    }
}

internal sealed class CreateOwnedCompanyAddonChangeCommandHandler(
    ICurrentUserService currentUserService,
    IUserRepository userRepository,
    ICompanyRepository companyRepository,
    ICompanySubscriptionRepository subscriptionRepository,
    ICommercialPlanRepository commercialPlanRepository,
    ICommercialAddonRepository commercialAddonRepository,
    IPersonnelFileRepository personnelFileRepository,
    IPlanEntitlementService planEntitlementService,
    IAuditService auditService,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateOwnedCompanyAddonChangeCommand, AccountCompanySubscriptionOverviewResponse>
{
    public async Task<Result<AccountCompanySubscriptionOverviewResponse>> Handle(
        CreateOwnedCompanyAddonChangeCommand command,
        CancellationToken cancellationToken)
    {
        var ownershipResult = await AccountCompanySubscriptionHelper.ResolveOwnershipAsync(
            currentUserService,
            userRepository,
            companyRepository,
            command.CompanyId,
            cancellationToken);

        if (ownershipResult.IsFailure)
        {
            return Result<AccountCompanySubscriptionOverviewResponse>.Failure(ownershipResult.Error);
        }

        var owner = ownershipResult.Value.Owner;
        var company = ownershipResult.Value.Company;
        var utcNow = dateTimeProvider.UtcNow;

        var resolution = await PlatformCompanyAddonChangeResolver.ResolveAsync(
            command.CompanyId,
            command.CommercialAddonId,
            command.Action,
            SubscriptionAddonChangeMode.Immediate,
            requestedEffectiveDateUtc: null,
            companyRepository,
            commercialAddonRepository,
            subscriptionRepository,
            personnelFileRepository,
            utcNow,
            cancellationToken);

        if (resolution.IsFailure)
        {
            return Result<AccountCompanySubscriptionOverviewResponse>.Failure(resolution.Error);
        }

        var context = resolution.Value;
        if (context.CurrentSubscription.ConcurrencyToken != command.ConcurrencyToken)
        {
            // PR-B: optimistic concurrency — reject an add-on change applied on a stale overview.
            return Result<AccountCompanySubscriptionOverviewResponse>.Failure(AccountCompanyErrors.ConcurrencyConflict);
        }

        if (!context.IsEligible)
        {
            return Result<AccountCompanySubscriptionOverviewResponse>.Failure(context.PrimaryError);
        }

        var before = await AccountCompanySubscriptionHelper.BuildOverviewAsync(
            company,
            subscriptionRepository,
            commercialPlanRepository,
            commercialAddonRepository,
            planEntitlementService,
            cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            if (context.ScheduledChange is not null)
            {
                context.ScheduledChange.Cancel(
                    utcNow,
                    owner.PublicId,
                    PlatformCompanyAddonChangeResolver.BuildAutomaticCancellationObservations(command.Observations));

                context.CurrentState?.RestoreStatus(context.ScheduledChange.PreviousStatus, utcNow);
            }

            var change = CompanyCommercialAddonChange.Create(
                context.CurrentSubscription,
                context.CommercialAddon,
                command.Action,
                SubscriptionAddonChangeMode.Immediate,
                SubscriptionAddonChangeReasonCode.CustomerRequest,
                context.CurrentStatus,
                context.ResultingStatus,
                utcNow,
                context.EffectiveDateUtc,
                owner.PublicId,
                command.Observations,
                context.QuantityBasis,
                context.EstimatedNextChargeImpact);

            subscriptionRepository.AddCompanyAddonChange(change);

            var companyAddon = context.CurrentState;
            if (companyAddon is null)
            {
                companyAddon = CompanyCommercialAddon.Create(
                    context.CurrentSubscription,
                    context.CommercialAddon,
                    CompanyAddonStatus.Active,
                    context.EffectiveDateUtc);
                subscriptionRepository.AddCompanyAddon(companyAddon);
            }
            else if (command.Action == SubscriptionAddonChangeAction.Activate)
            {
                companyAddon.ApplyActivation(context.CurrentSubscription, context.CommercialAddon, context.EffectiveDateUtc);
            }
            else if (companyAddon.Status is CompanyAddonStatus.Active or CompanyAddonStatus.PendingDeactivation)
            {
                companyAddon.ApplyDeactivation(context.EffectiveDateUtc);
            }
            else
            {
                companyAddon.RestoreStatus(CompanyAddonStatus.Inactive, context.EffectiveDateUtc);
            }

            change.MarkApplied(utcNow, context.CurrentSubscription.PublicId);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = await AccountCompanySubscriptionHelper.BuildOverviewAsync(
                company,
                subscriptionRepository,
                commercialPlanRepository,
                commercialAddonRepository,
                planEntitlementService,
                cancellationToken);

            await auditService.LogForTenantAsync(
                company.PublicId,
                new AuditLogEntry(
                    AuditEventTypes.CompanySubscriptionAddonChangeApplied,
                    AuditEntityTypes.CompanyCommercialAddonChange,
                    change.PublicId,
                    company.Slug,
                    AuditActions.Update,
                    $"Owner applied add-on change for {company.Name} and add-on {context.CommercialAddon.Code}.",
                    Before: before,
                    After: after),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return after is null
                ? Result<AccountCompanySubscriptionOverviewResponse>.Failure(PlatformSubscriptionErrors.SubscriptionNotFound)
                : Result<AccountCompanySubscriptionOverviewResponse>.Success(after);
        }
        catch (UniqueConstraintViolationException ex) when (CompanySubscriptionConstraintViolations.IsConcurrencyConflict(ex.ConstraintName))
        {
            // ACS-A: a concurrent double-submit collides on a filtered unique index (one live / one
            // scheduled subscription, one scheduled plan-change, one company-add-on, one scheduled
            // add-on-change) → surface the race as 409 CONCURRENCY_CONFLICT instead of an opaque 500.
            await transaction.RollbackAsync(cancellationToken);
            return Result<AccountCompanySubscriptionOverviewResponse>.Failure(AccountCompanyErrors.ConcurrencyConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal static class AccountCompanySubscriptionHelper
{
    private const int AddonPageSize = 100;

    public static async Task<Result<(User Owner, Company Company)>> ResolveOwnershipAsync(
        ICurrentUserService currentUserService,
        IUserRepository userRepository,
        ICompanyRepository companyRepository,
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var currentUserResult = await AccountCompanyActorResolver.ResolveCurrentUserAsync(
            currentUserService,
            userRepository,
            cancellationToken);

        if (currentUserResult.IsFailure)
        {
            return Result<(User Owner, Company Company)>.Failure(currentUserResult.Error);
        }

        var companyResult = await AccountCompanyActorResolver.ResolveOwnedCompanyAsync(
            companyRepository,
            companyId,
            currentUserResult.Value.PublicId,
            cancellationToken);

        return companyResult.IsFailure
            ? Result<(User Owner, Company Company)>.Failure(companyResult.Error)
            : Result<(User Owner, Company Company)>.Success((currentUserResult.Value, companyResult.Value));
    }

    public static async Task<AccountCompanySubscriptionOverviewResponse?> BuildOverviewAsync(
        Company company,
        ICompanySubscriptionRepository subscriptionRepository,
        ICommercialPlanRepository commercialPlanRepository,
        ICommercialAddonRepository commercialAddonRepository,
        IPlanEntitlementService planEntitlementService,
        CancellationToken cancellationToken)
    {
        var currentSubscription = await subscriptionRepository.GetActiveByCompanyIdAsync(company.Id, cancellationToken);
        if (currentSubscription is null)
        {
            return null;
        }

        var currentPlan = await commercialPlanRepository.GetByInternalIdAsync(
            currentSubscription.CommercialPlanId,
            cancellationToken);
        if (currentPlan is null)
        {
            return null;
        }

        var activeAddons = await GetActiveAddonsAsync(
            company,
            subscriptionRepository,
            commercialAddonRepository,
            cancellationToken);

        var effectiveModules = await planEntitlementService.GetEffectiveModulesAsync(company.PublicId, cancellationToken);

        return new AccountCompanySubscriptionOverviewResponse(
            company.PublicId,
            company.Name,
            company.Slug,
            currentSubscription.PlanCode,
            MapPlan(currentPlan, isCurrent: true),
            activeAddons,
            effectiveModules
                .Select(MapEffectiveModule)
                .ToArray(),
            currentSubscription.ConcurrencyToken);
    }

    public static async Task<IReadOnlyCollection<AccountCompanySubscriptionAddonResponse>> GetActiveAddonsAsync(
        Company company,
        ICompanySubscriptionRepository subscriptionRepository,
        ICommercialAddonRepository commercialAddonRepository,
        CancellationToken cancellationToken)
    {
        var activeAddonsPage = await subscriptionRepository.SearchCompanyAddonsByCompanyPublicIdAsync(
            company.PublicId,
            CompanyAddonStatus.Active,
            search: null,
            pageNumber: 1,
            pageSize: AddonPageSize,
            cancellationToken);

        var addons = new List<AccountCompanySubscriptionAddonResponse>(activeAddonsPage.Items.Count);
        foreach (var activeAddon in activeAddonsPage.Items)
        {
            var catalogAddon = await commercialAddonRepository.GetByIdAsync(activeAddon.CommercialAddonId, cancellationToken);
            addons.Add(MapAddon(activeAddon, catalogAddon));
        }

        return addons;
    }

    public static async Task<AccountCompanySubscriptionPlanPreviewResponse> BuildPlanPreviewAsync(
        PlatformSubscriptionPlanChangeResolution resolution,
        ICompanySubscriptionRepository subscriptionRepository,
        ICommercialAddonRepository commercialAddonRepository,
        IPlanEntitlementService planEntitlementService,
        CancellationToken cancellationToken)
    {
        var currentModuleKeys = (await planEntitlementService.GetEffectiveModulesAsync(
                resolution.Company.PublicId,
                cancellationToken))
            .Select(module => module.ModuleKey)
            .ToHashSet(StringComparer.Ordinal);

        var addonModuleKeys = new HashSet<string>(StringComparer.Ordinal);
        if (!string.Equals(resolution.TargetPlan.Code, ProvisioningConstants.FreePlanCode, StringComparison.Ordinal))
        {
            var activeAddons = await GetActiveAddonsAsync(
                resolution.Company,
                subscriptionRepository,
                commercialAddonRepository,
                cancellationToken);

            foreach (var moduleKey in activeAddons.SelectMany(addon => addon.ModuleKeys))
            {
                addonModuleKeys.Add(moduleKey);
            }
        }

        var futureModuleKeys = resolution.TargetPlan.Entitlements
            .Where(entitlement => entitlement.IsEnabled)
            .Select(entitlement => entitlement.ModuleKey)
            .Concat(addonModuleKeys)
            .ToHashSet(StringComparer.Ordinal);

        return new AccountCompanySubscriptionPlanPreviewResponse(
            resolution.Company.PublicId,
            MapPlan(resolution.CurrentPlan ?? resolution.TargetPlan, isCurrent: true),
            MapPlan(resolution.TargetPlan, isCurrent: false),
            futureModuleKeys.Except(currentModuleKeys, StringComparer.Ordinal).OrderBy(key => key, StringComparer.Ordinal).ToArray(),
            currentModuleKeys.Except(futureModuleKeys, StringComparer.Ordinal).OrderBy(key => key, StringComparer.Ordinal).ToArray(),
            resolution.AddonCompatibilityWarnings,
            resolution.IsEligible,
            resolution.IneligibilityReasons);
    }

    public static async Task<AccountCompanyAddonChangePreviewResponse> BuildAddonPreviewAsync(
        PlatformCompanyAddonChangeResolution resolution,
        ICommercialPlanRepository commercialPlanRepository,
        ICompanySubscriptionRepository subscriptionRepository,
        ICommercialAddonRepository commercialAddonRepository,
        IPlanEntitlementService planEntitlementService,
        CancellationToken cancellationToken)
    {
        var currentModuleKeys = (await planEntitlementService.GetEffectiveModulesAsync(
                resolution.Company.PublicId,
                cancellationToken))
            .Select(module => module.ModuleKey)
            .ToHashSet(StringComparer.Ordinal);

        var currentPlan = await commercialPlanRepository.GetByInternalIdAsync(
            resolution.CurrentSubscription.CommercialPlanId,
            cancellationToken);

        var planModuleKeys = currentPlan?.Entitlements
            .Where(entitlement => entitlement.IsEnabled)
            .Select(entitlement => entitlement.ModuleKey)
            .ToHashSet(StringComparer.Ordinal) ?? new HashSet<string>(StringComparer.Ordinal);

        var activeAddons = await GetActiveAddonsAsync(
            resolution.Company,
            subscriptionRepository,
            commercialAddonRepository,
            cancellationToken);

        var activeAddonModules = new Dictionary<Guid, IReadOnlyCollection<string>>();
        foreach (var addon in activeAddons)
        {
            activeAddonModules[addon.CommercialAddonId] = addon.ModuleKeys;
        }

        if (resolution.Action == SubscriptionAddonChangeAction.Activate)
        {
            activeAddonModules[resolution.CommercialAddon.PublicId] = resolution.CommercialAddon.Entitlements
                .Where(entitlement => entitlement.IsEnabled)
                .Select(entitlement => entitlement.ModuleKey)
                .ToArray();
        }
        else
        {
            _ = activeAddonModules.Remove(resolution.CommercialAddon.PublicId);
        }

        var futureModuleKeys = planModuleKeys
            .Concat(activeAddonModules.Values.SelectMany(keys => keys))
            .ToHashSet(StringComparer.Ordinal);

        return new AccountCompanyAddonChangePreviewResponse(
            resolution.Company.PublicId,
            resolution.CommercialAddon.PublicId,
            resolution.CommercialAddon.Code,
            resolution.CommercialAddon.Name,
            resolution.Action,
            futureModuleKeys.Except(currentModuleKeys, StringComparer.Ordinal).OrderBy(key => key, StringComparer.Ordinal).ToArray(),
            currentModuleKeys.Except(futureModuleKeys, StringComparer.Ordinal).OrderBy(key => key, StringComparer.Ordinal).ToArray(),
            resolution.IsEligible,
            resolution.IneligibilityReasons,
            resolution.Warnings);
    }

    public static async Task DeactivateAddonsForFreePlanAsync(
        Company company,
        Guid actorUserPublicId,
        DateTime utcNow,
        ICompanySubscriptionRepository subscriptionRepository,
        CancellationToken cancellationToken)
    {
        var scheduledChanges = await subscriptionRepository.GetScheduledAddonChangesByCompanyIdAsync(company.Id, cancellationToken);
        foreach (var scheduledChange in scheduledChanges)
        {
            scheduledChange.Cancel(
                utcNow,
                actorUserPublicId,
                "Cancelled automatically because the company moved to the FREE plan.");
        }

        var companyAddons = await subscriptionRepository.GetNonInactiveCompanyAddonsByCompanyIdAsync(company.Id, cancellationToken);
        foreach (var companyAddon in companyAddons)
        {
            if (companyAddon.Status is CompanyAddonStatus.Active or CompanyAddonStatus.PendingDeactivation)
            {
                companyAddon.ApplyDeactivation(utcNow);
                continue;
            }

            companyAddon.RestoreStatus(CompanyAddonStatus.Inactive, utcNow);
        }
    }

    public static bool IsMasterPlan(CommercialPlan plan) =>
        string.Equals(plan.Code, ProvisioningConstants.MasterPlanCode, StringComparison.Ordinal);

    public static async Task<bool> CanAccessMasterPlanAsync(
        Guid userPublicId,
        IPlatformOperatorRepository platformOperatorRepository,
        CancellationToken cancellationToken) =>
        await platformOperatorRepository.GetActiveByUserPublicIdAsync(userPublicId, cancellationToken) is not null;

    public static async Task<Result> EnsureMasterPlanAccessAsync(
        Guid userPublicId,
        CommercialPlan targetPlan,
        IPlatformOperatorRepository platformOperatorRepository,
        CancellationToken cancellationToken)
    {
        if (!IsMasterPlan(targetPlan))
        {
            return Result.Success();
        }

        var canAccessMasterPlan = await CanAccessMasterPlanAsync(
            userPublicId,
            platformOperatorRepository,
            cancellationToken);

        return canAccessMasterPlan
            ? Result.Success()
            : Result.Failure(AccountCompanyErrors.MasterPlanForbidden);
    }

    public static AccountCompanySubscriptionPlanResponse MapPlan(CommercialPlan plan, bool isCurrent)
    {
        var currentVersion = plan.GetCurrentVersion();
        return new AccountCompanySubscriptionPlanResponse(
            plan.PublicId,
            plan.Code,
            plan.Name,
            plan.Description,
            currentVersion.BaseMonthlyFee,
            currentVersion.PricePerActiveEmployee,
            currentVersion.VersionNumber,
            currentVersion.CurrencyCode,
            plan.Entitlements.Count(entitlement => entitlement.IsEnabled),
            plan.Entitlements
                .Where(entitlement => entitlement.IsEnabled)
                .OrderBy(entitlement => entitlement.ModuleKey, StringComparer.Ordinal)
                .Select(entitlement => entitlement.ModuleKey)
                .ToArray(),
            isCurrent);
    }

    private static AccountCompanySubscriptionAddonResponse MapAddon(
        PlatformCompanyAddonResponse activeAddon,
        CommercialAddon? catalogAddon) =>
        new(
            activeAddon.CompanyAddonId,
            activeAddon.CommercialAddonId,
            activeAddon.AddonCode,
            activeAddon.AddonName,
            catalogAddon?.Description,
            activeAddon.AddonType,
            activeAddon.BillingModel,
            activeAddon.MeasurementUnit,
            activeAddon.UnitPrice,
            activeAddon.MinimumQuantity,
            activeAddon.MinimumMonthlyFee,
            activeAddon.Periodicity,
            activeAddon.Status,
            catalogAddon?.Entitlements.Count(entitlement => entitlement.IsEnabled) ?? 0,
            catalogAddon?.Entitlements
                .Where(entitlement => entitlement.IsEnabled)
                .OrderBy(entitlement => entitlement.ModuleKey, StringComparer.Ordinal)
                .Select(entitlement => entitlement.ModuleKey)
                .ToArray() ?? Array.Empty<string>());

    private static AccountCompanyEffectiveModuleResponse MapEffectiveModule(EffectiveCommercialModuleGrant grant)
    {
        var definition = CommercialModuleCatalog.Get(grant.ModuleKey);
        var source = grant.GrantedByPlan && grant.GrantedByAddon
            ? "plan+addon"
            : grant.GrantedByPlan
                ? "plan"
                : "addon";

        return new AccountCompanyEffectiveModuleResponse(
            grant.ModuleKey,
            definition.DisplayName,
            definition.Description,
            source,
            grant.GrantedByPlan,
            grant.GrantedByAddon);
    }
}

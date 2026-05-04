using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.PersonnelFiles;
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

internal sealed class SearchPlatformCompanyAddonsQueryHandler(
    IPlatformAuthorizationService authorizationService,
    ICompanyRepository companyRepository,
    ICompanySubscriptionRepository subscriptionRepository)
    : IQueryHandler<SearchPlatformCompanyAddonsQuery, PagedResponse<PlatformCompanyAddonResponse>>
{
    public async Task<Result<PagedResponse<PlatformCompanyAddonResponse>>> Handle(
        SearchPlatformCompanyAddonsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<PlatformCompanyAddonResponse>>.Failure(authorizationResult.Error);
        }

        if (await companyRepository.FindByPublicIdAsync(query.CompanyId, cancellationToken) is null)
        {
            return Result<PagedResponse<PlatformCompanyAddonResponse>>.Failure(PlatformSubscriptionErrors.CompanyNotFound);
        }

        var response = await subscriptionRepository.SearchCompanyAddonsByCompanyPublicIdAsync(
            query.CompanyId,
            query.Status,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        return Result<PagedResponse<PlatformCompanyAddonResponse>>.Success(response);
    }
}

internal sealed class SearchPlatformCompanyEligibleAddonsQueryHandler(
    IPlatformAuthorizationService authorizationService,
    ICompanyRepository companyRepository,
    ICompanySubscriptionRepository subscriptionRepository)
    : IQueryHandler<SearchPlatformCompanyEligibleAddonsQuery, PagedResponse<PlatformCompanyEligibleAddonResponse>>
{
    public async Task<Result<PagedResponse<PlatformCompanyEligibleAddonResponse>>> Handle(
        SearchPlatformCompanyEligibleAddonsQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<PlatformCompanyEligibleAddonResponse>>.Failure(authorizationResult.Error);
        }

        if (await companyRepository.FindByPublicIdAsync(query.CompanyId, cancellationToken) is null)
        {
            return Result<PagedResponse<PlatformCompanyEligibleAddonResponse>>.Failure(PlatformSubscriptionErrors.CompanyNotFound);
        }

        var response = await subscriptionRepository.SearchEligibleAddonsByCompanyPublicIdAsync(
            query.CompanyId,
            query.Type,
            query.Search,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        return Result<PagedResponse<PlatformCompanyEligibleAddonResponse>>.Success(response);
    }
}

internal sealed class PreviewPlatformCompanyAddonChangeQueryHandler(
    IPlatformAuthorizationService authorizationService,
    ICompanyRepository companyRepository,
    ICommercialAddonRepository commercialAddonRepository,
    ICompanySubscriptionRepository subscriptionRepository,
    IPersonnelFileRepository personnelFileRepository,
    IDateTimeProvider dateTimeProvider)
    : IQueryHandler<PreviewPlatformCompanyAddonChangeQuery, PlatformCompanyAddonChangePreviewResponse>
{
    public async Task<Result<PlatformCompanyAddonChangePreviewResponse>> Handle(
        PreviewPlatformCompanyAddonChangeQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PlatformCompanyAddonChangePreviewResponse>.Failure(authorizationResult.Error);
        }

        var resolution = await PlatformCompanyAddonChangeResolver.ResolveAsync(
            query.CompanyId,
            query.CommercialAddonId,
            query.Action,
            query.Mode,
            query.RequestedEffectiveDateUtc,
            companyRepository,
            commercialAddonRepository,
            subscriptionRepository,
            personnelFileRepository,
            dateTimeProvider.UtcNow,
            cancellationToken);

        if (resolution.IsFailure)
        {
            return Result<PlatformCompanyAddonChangePreviewResponse>.Failure(resolution.Error);
        }

        return Result<PlatformCompanyAddonChangePreviewResponse>.Success(resolution.Value.ToPreviewResponse());
    }
}

internal sealed class SearchPlatformCompanyAddonChangesQueryHandler(
    IPlatformAuthorizationService authorizationService,
    ICompanyRepository companyRepository,
    ICompanySubscriptionRepository subscriptionRepository)
    : IQueryHandler<SearchPlatformCompanyAddonChangesQuery, PagedResponse<PlatformCompanyAddonChangeResponse>>
{
    public async Task<Result<PagedResponse<PlatformCompanyAddonChangeResponse>>> Handle(
        SearchPlatformCompanyAddonChangesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<PlatformCompanyAddonChangeResponse>>.Failure(authorizationResult.Error);
        }

        if (await companyRepository.FindByPublicIdAsync(query.CompanyId, cancellationToken) is null)
        {
            return Result<PagedResponse<PlatformCompanyAddonChangeResponse>>.Failure(PlatformSubscriptionErrors.CompanyNotFound);
        }

        var response = await subscriptionRepository.SearchAddonChangesByCompanyPublicIdAsync(
            query.CompanyId,
            query.PageNumber,
            query.PageSize,
            cancellationToken);

        return Result<PagedResponse<PlatformCompanyAddonChangeResponse>>.Success(response);
    }
}

internal sealed class CreatePlatformCompanyAddonChangeCommandHandler(
    IPlatformAuthorizationService authorizationService,
    ICompanyRepository companyRepository,
    ICommercialAddonRepository commercialAddonRepository,
    ICompanySubscriptionRepository subscriptionRepository,
    IPersonnelFileRepository personnelFileRepository,
    ICurrentUserService currentUserService,
    IPlatformAuditService platformAuditService,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreatePlatformCompanyAddonChangeCommand, PlatformCompanyAddonChangeResponse>
{
    public async Task<Result<PlatformCompanyAddonChangeResponse>> Handle(
        CreatePlatformCompanyAddonChangeCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PlatformCompanyAddonChangeResponse>.Failure(authorizationResult.Error);
        }

        var resolution = await PlatformCompanyAddonChangeResolver.ResolveAsync(
            command.CompanyId,
            command.CommercialAddonId,
            command.Action,
            command.Mode,
            command.RequestedEffectiveDateUtc,
            companyRepository,
            commercialAddonRepository,
            subscriptionRepository,
            personnelFileRepository,
            dateTimeProvider.UtcNow,
            cancellationToken);

        if (resolution.IsFailure)
        {
            return Result<PlatformCompanyAddonChangeResponse>.Failure(resolution.Error);
        }

        var context = resolution.Value;
        if (!context.IsEligible)
        {
            return Result<PlatformCompanyAddonChangeResponse>.Failure(context.PrimaryError);
        }

        var actorUserPublicId = PlatformSubscriptionAdministrationHelpers.TryParseCurrentUserId(currentUserService.UserId);
        var utcNow = dateTimeProvider.UtcNow;

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            if (context.ScheduledChange is not null)
            {
                var beforeScheduled = await subscriptionRepository.GetAddonChangeResponseByPublicIdAsync(
                    command.CompanyId,
                    context.ScheduledChange.PublicId,
                    cancellationToken);

                context.ScheduledChange.Cancel(
                    utcNow,
                    actorUserPublicId,
                    PlatformCompanyAddonChangeResolver.BuildAutomaticCancellationObservations(command.Observations));

                context.CurrentState?.RestoreStatus(context.ScheduledChange.PreviousStatus, utcNow);

                _ = await unitOfWork.SaveChangesAsync(cancellationToken);

                var cancelledResponse = await subscriptionRepository.GetAddonChangeResponseByPublicIdAsync(
                    command.CompanyId,
                    context.ScheduledChange.PublicId,
                    cancellationToken);

                if (cancelledResponse is null)
                {
                    throw new InvalidOperationException("The cancelled add-on change could not be loaded after persistence.");
                }

                await platformAuditService.LogAsync(
                    new AuditLogEntry(
                        AuditEventTypes.CompanySubscriptionAddonChangeCancelled,
                        AuditEntityTypes.CompanyCommercialAddonChange,
                        context.ScheduledChange.PublicId,
                        context.Company.Slug,
                        AuditActions.Update,
                        $"Cancelled scheduled add-on change for {context.Company.Name} and add-on {context.CommercialAddon.Code}.",
                        Before: beforeScheduled,
                        After: cancelledResponse),
                    cancellationToken);

                _ = await unitOfWork.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return Result<PlatformCompanyAddonChangeResponse>.Success(cancelledResponse);
            }

            var beforeState = context.CurrentState is null
                ? null
                : new
                {
                    context.CurrentState.Status,
                    context.CurrentState.StatusEffectiveDateUtc
                };

            var change = CompanyCommercialAddonChange.Create(
                context.CurrentSubscription,
                context.CommercialAddon,
                command.Action,
                command.Mode,
                command.ReasonCode,
                context.CurrentStatus,
                context.ResultingStatus,
                utcNow,
                context.EffectiveDateUtc,
                actorUserPublicId,
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
                    command.Mode == SubscriptionAddonChangeMode.Immediate
                        ? CompanyAddonStatus.Active
                        : CompanyAddonStatus.PendingActivation,
                    context.EffectiveDateUtc);
                subscriptionRepository.AddCompanyAddon(companyAddon);
            }
            else if (command.Action == SubscriptionAddonChangeAction.Activate)
            {
                if (command.Mode == SubscriptionAddonChangeMode.Immediate)
                {
                    companyAddon.ApplyActivation(context.CurrentSubscription, context.CommercialAddon, context.EffectiveDateUtc);
                }
                else
                {
                    companyAddon.ScheduleActivation(context.CurrentSubscription, context.CommercialAddon, context.EffectiveDateUtc);
                }
            }
            else if (command.Mode == SubscriptionAddonChangeMode.Immediate)
            {
                companyAddon.ApplyDeactivation(context.EffectiveDateUtc);
            }
            else
            {
                companyAddon.ScheduleDeactivation(context.EffectiveDateUtc);
            }

            if (command.Mode == SubscriptionAddonChangeMode.Immediate)
            {
                change.MarkApplied(utcNow, context.CurrentSubscription.PublicId);
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await subscriptionRepository.GetAddonChangeResponseByPublicIdAsync(
                command.CompanyId,
                change.PublicId,
                cancellationToken);

            if (response is null)
            {
                throw new InvalidOperationException("The created add-on change could not be loaded after persistence.");
            }

            await platformAuditService.LogAsync(
                new AuditLogEntry(
                    command.Mode == SubscriptionAddonChangeMode.Immediate
                        ? AuditEventTypes.CompanySubscriptionAddonChangeApplied
                        : AuditEventTypes.CompanySubscriptionAddonChangeRequested,
                    AuditEntityTypes.CompanyCommercialAddonChange,
                    change.PublicId,
                    context.Company.Slug,
                    AuditActions.Update,
                    command.Mode == SubscriptionAddonChangeMode.Immediate
                        ? $"Applied add-on change for {context.Company.Name} and add-on {context.CommercialAddon.Code}."
                        : $"Scheduled add-on change for {context.Company.Name} and add-on {context.CommercialAddon.Code}.",
                    Before: beforeState,
                    After: response),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PlatformCompanyAddonChangeResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class CancelPlatformCompanyAddonChangeCommandHandler(
    IPlatformAuthorizationService authorizationService,
    ICompanyRepository companyRepository,
    ICompanySubscriptionRepository subscriptionRepository,
    ICurrentUserService currentUserService,
    IPlatformAuditService platformAuditService,
    IDateTimeProvider dateTimeProvider,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CancelPlatformCompanyAddonChangeCommand, PlatformCompanyAddonChangeResponse>
{
    public async Task<Result<PlatformCompanyAddonChangeResponse>> Handle(
        CancelPlatformCompanyAddonChangeCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PlatformCompanyAddonChangeResponse>.Failure(authorizationResult.Error);
        }

        var company = await companyRepository.FindByPublicIdAsync(command.CompanyId, cancellationToken);
        if (company is null)
        {
            return Result<PlatformCompanyAddonChangeResponse>.Failure(PlatformSubscriptionErrors.CompanyNotFound);
        }

        var addonChange = await subscriptionRepository.GetAddonChangeByCompanyAndPublicIdAsync(
            command.CompanyId,
            command.AddonChangeId,
            cancellationToken);

        if (addonChange is null)
        {
            return Result<PlatformCompanyAddonChangeResponse>.Failure(PlatformSubscriptionErrors.AddonChangeNotFound);
        }

        if (addonChange.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<PlatformCompanyAddonChangeResponse>.Failure(PlatformSubscriptionErrors.ConcurrencyConflict);
        }

        if (addonChange.Status != SubscriptionAddonChangeStatus.Scheduled ||
            addonChange.EffectiveDateUtc <= dateTimeProvider.UtcNow.Date)
        {
            return Result<PlatformCompanyAddonChangeResponse>.Failure(PlatformSubscriptionErrors.AddonChangeCancellationNotAllowed);
        }

        var companyAddon = await subscriptionRepository.GetCompanyAddonByCompanyIdAndAddonIdAsync(
            company.Id,
            addonChange.CommercialAddonId,
            cancellationToken);

        var actorUserPublicId = PlatformSubscriptionAdministrationHelpers.TryParseCurrentUserId(currentUserService.UserId);
        var before = await subscriptionRepository.GetAddonChangeResponseByPublicIdAsync(
            command.CompanyId,
            command.AddonChangeId,
            cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            addonChange.Cancel(dateTimeProvider.UtcNow, actorUserPublicId, command.Observations);
            companyAddon?.RestoreStatus(addonChange.PreviousStatus, dateTimeProvider.UtcNow);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var response = await subscriptionRepository.GetAddonChangeResponseByPublicIdAsync(
                command.CompanyId,
                command.AddonChangeId,
                cancellationToken);

            if (response is null)
            {
                throw new InvalidOperationException("The cancelled add-on change could not be loaded after persistence.");
            }

            await platformAuditService.LogAsync(
                new AuditLogEntry(
                    AuditEventTypes.CompanySubscriptionAddonChangeCancelled,
                    AuditEntityTypes.CompanyCommercialAddonChange,
                    addonChange.PublicId,
                    company.Slug,
                    AuditActions.Update,
                    $"Cancelled scheduled add-on change for {company.Name} and add-on {addonChange.AddonCode}.",
                    Before: before,
                    After: response),
                cancellationToken);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PlatformCompanyAddonChangeResponse>.Success(response);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed record PlatformCompanyAddonChangeResolution(
    Company Company,
    CompanySubscription CurrentSubscription,
    CommercialAddon CommercialAddon,
    CompanyCommercialAddon? CurrentState,
    CompanyCommercialAddonChange? ScheduledChange,
    SubscriptionAddonChangeAction Action,
    SubscriptionAddonChangeMode Mode,
    DateTime EffectiveDateUtc,
    CompanyAddonStatus CurrentStatus,
    CompanyAddonStatus ResultingStatus,
    int QuantityBasis,
    decimal EstimatedNextChargeImpact,
    IReadOnlyCollection<string> IneligibilityReasons,
    IReadOnlyCollection<string> Warnings,
    Error PrimaryError)
{
    public bool IsEligible => IneligibilityReasons.Count == 0;

    public PlatformCompanyAddonChangePreviewResponse ToPreviewResponse() =>
        new(
            Company.PublicId,
            Company.Name,
            Company.Slug,
            CurrentSubscription.PublicId,
            CommercialAddon.PublicId,
            CommercialAddon.Code,
            CommercialAddon.Name,
            CommercialAddon.Type,
            CommercialAddon.BillingModel,
            CommercialAddon.MeasurementUnit,
            CommercialAddon.UnitPrice,
            CommercialAddon.MinimumQuantity,
            CommercialAddon.MinimumMonthlyFee,
            CommercialAddon.Periodicity,
            "USD",
            Action,
            Mode,
            CurrentStatus,
            ResultingStatus,
            EffectiveDateUtc,
            QuantityBasis,
            EstimatedNextChargeImpact,
            IsEligible,
            true,
            IneligibilityReasons,
            Warnings);
}

internal static class PlatformCompanyAddonChangeResolver
{
    public static async Task<Result<PlatformCompanyAddonChangeResolution>> ResolveAsync(
        Guid companyPublicId,
        Guid commercialAddonPublicId,
        SubscriptionAddonChangeAction action,
        SubscriptionAddonChangeMode mode,
        DateTime? requestedEffectiveDateUtc,
        ICompanyRepository companyRepository,
        ICommercialAddonRepository commercialAddonRepository,
        ICompanySubscriptionRepository subscriptionRepository,
        IPersonnelFileRepository personnelFileRepository,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var company = await companyRepository.FindByPublicIdAsync(companyPublicId, cancellationToken);
        if (company is null)
        {
            return Result<PlatformCompanyAddonChangeResolution>.Failure(PlatformSubscriptionErrors.CompanyNotFound);
        }

        var commercialAddon = await commercialAddonRepository.GetByIdAsync(commercialAddonPublicId, cancellationToken);
        if (commercialAddon is null)
        {
            return Result<PlatformCompanyAddonChangeResolution>.Failure(PlatformSubscriptionErrors.AddonNotFound);
        }

        if (!Enum.IsDefined(action))
        {
            return Result<PlatformCompanyAddonChangeResolution>.Failure(PlatformSubscriptionErrors.AddonChangeInvalidAction);
        }

        if (!Enum.IsDefined(mode))
        {
            return Result<PlatformCompanyAddonChangeResolution>.Failure(PlatformSubscriptionErrors.AddonChangeInvalidMode);
        }

        var currentSubscription = await subscriptionRepository.GetActiveByCompanyIdAsync(company.Id, cancellationToken);
        if (currentSubscription is null)
        {
            return Result<PlatformCompanyAddonChangeResolution>.Failure(PlatformSubscriptionErrors.SubscriptionNotFound);
        }

        var effectiveDateResolution = ResolveEffectiveDate(currentSubscription, mode, requestedEffectiveDateUtc, utcNow);
        if (effectiveDateResolution.IsFailure)
        {
            return Result<PlatformCompanyAddonChangeResolution>.Failure(effectiveDateResolution.Error);
        }

        var currentState = await subscriptionRepository.GetCompanyAddonByCompanyIdAndAddonIdAsync(
            company.Id,
            commercialAddon.Id,
            cancellationToken);
        var scheduledChange = await subscriptionRepository.GetScheduledAddonChangeByCompanyAndAddonIdAsync(
            company.Id,
            commercialAddon.Id,
            cancellationToken);

        var reasons = new List<string>();
        var warnings = new List<string>();
        var errors = new List<Error>();

        if (company.Status != CompanyStatus.Active)
        {
            reasons.Add("La empresa debe estar activa para administrar add-ons.");
            errors.Add(PlatformSubscriptionErrors.CompanyNotEligible);
        }

        if (action == SubscriptionAddonChangeAction.Activate &&
            string.Equals(currentSubscription.PlanCode, ProvisioningConstants.FreePlanCode, StringComparison.Ordinal))
        {
            reasons.Add("La suscripcion FREE no puede adquirir add-ons comerciales.");
            errors.Add(PlatformSubscriptionErrors.AddonForbiddenForFreePlan);
        }

        if (commercialAddon.Status != CommercialAddonStatus.Active &&
            action == SubscriptionAddonChangeAction.Activate)
        {
            reasons.Add("Solo se pueden activar add-ons que esten activos en el catalogo comercial.");
            errors.Add(PlatformSubscriptionErrors.AddonInactive);
        }

        var currentStatus = currentState?.Status ?? CompanyAddonStatus.Inactive;
        if (action == SubscriptionAddonChangeAction.Activate)
        {
            if (currentStatus == CompanyAddonStatus.Active)
            {
                reasons.Add("El add-on ya se encuentra activo para la empresa.");
                errors.Add(PlatformSubscriptionErrors.AddonAlreadyActive);
            }
        }
        else if (currentStatus == CompanyAddonStatus.Inactive)
        {
            reasons.Add("Solo se pueden desactivar add-ons vigentes en la empresa.");
            errors.Add(PlatformSubscriptionErrors.AddonNotActive);
        }

        if (scheduledChange is not null)
        {
            if (scheduledChange.Action == action)
            {
                reasons.Add("Ya existe un cambio programado pendiente para este add-on.");
                errors.Add(PlatformSubscriptionErrors.AddonPendingConflict);
            }
            else
            {
                warnings.Add("Existe un cambio pendiente opuesto; si se confirma esta solicitud se cancelara ese pendiente.");
            }
        }

        if (commercialAddon.Type == CommercialAddonType.Specialized)
        {
            warnings.Add("Los add-ons especializados pueden requerir configuracion adicional en historias posteriores.");
        }

        var resultingStatus = ResolveResultingStatus(action, effectiveDateResolution.Value, utcNow);
        var quantityBasis = await ResolveQuantityBasisAsync(
            company.PublicId,
            commercialAddon,
            currentState,
            action,
            personnelFileRepository,
            cancellationToken);
        var estimateSnapshot = BuildEstimateSnapshot(commercialAddon, currentState, action);
        var estimatedNextChargeImpact = CalculateEstimatedImpact(estimateSnapshot, action, quantityBasis);

        return Result<PlatformCompanyAddonChangeResolution>.Success(new PlatformCompanyAddonChangeResolution(
            company,
            currentSubscription,
            commercialAddon,
            currentState,
            scheduledChange,
            action,
            mode,
            effectiveDateResolution.Value,
            currentStatus,
            resultingStatus,
            quantityBasis,
            estimatedNextChargeImpact,
            reasons,
            warnings,
            errors.FirstOrDefault() ?? PlatformSubscriptionErrors.AddonPendingConflict));
    }

    public static string BuildAutomaticCancellationObservations(string? observations)
    {
        const string prefix = "Cancelled automatically because an opposite add-on change was requested.";
        return string.IsNullOrWhiteSpace(observations)
            ? prefix
            : $"{prefix} {observations.Trim()}";
    }

    private static Result<DateTime> ResolveEffectiveDate(
        CompanySubscription currentSubscription,
        SubscriptionAddonChangeMode mode,
        DateTime? requestedEffectiveDateUtc,
        DateTime utcNow)
    {
        return mode switch
        {
            SubscriptionAddonChangeMode.Immediate => Result<DateTime>.Success(utcNow.Date),
            SubscriptionAddonChangeMode.SpecificDate => ResolveSpecificDate(requestedEffectiveDateUtc, utcNow),
            SubscriptionAddonChangeMode.NextBillingCycle => Result<DateTime>.Success(
                PlatformSubscriptionAdministrationHelpers.GetNextBillingCycleDate(currentSubscription, utcNow)),
            _ => Result<DateTime>.Failure(PlatformSubscriptionErrors.AddonChangeInvalidMode)
        };
    }

    private static Result<DateTime> ResolveSpecificDate(DateTime? requestedEffectiveDateUtc, DateTime utcNow)
    {
        if (!requestedEffectiveDateUtc.HasValue || requestedEffectiveDateUtc.Value == default)
        {
            return Result<DateTime>.Failure(PlatformSubscriptionErrors.AddonChangeEffectiveDateRequired);
        }

        if (requestedEffectiveDateUtc.Value.Date < utcNow.Date)
        {
            return Result<DateTime>.Failure(PlatformSubscriptionErrors.AddonChangeEffectiveDateInPast);
        }

        return Result<DateTime>.Success(requestedEffectiveDateUtc.Value.Date);
    }

    private static CompanyAddonStatus ResolveResultingStatus(
        SubscriptionAddonChangeAction action,
        DateTime effectiveDateUtc,
        DateTime utcNow) =>
        action switch
        {
            SubscriptionAddonChangeAction.Activate when effectiveDateUtc.Date <= utcNow.Date => CompanyAddonStatus.Active,
            SubscriptionAddonChangeAction.Activate => CompanyAddonStatus.PendingActivation,
            SubscriptionAddonChangeAction.Deactivate when effectiveDateUtc.Date <= utcNow.Date => CompanyAddonStatus.Inactive,
            SubscriptionAddonChangeAction.Deactivate => CompanyAddonStatus.PendingDeactivation,
            _ => throw new InvalidOperationException($"Unsupported add-on action '{action}'.")
        };

    private static async Task<int> ResolveQuantityBasisAsync(
        Guid companyPublicId,
        CommercialAddon commercialAddon,
        CompanyCommercialAddon? currentState,
        SubscriptionAddonChangeAction action,
        IPersonnelFileRepository personnelFileRepository,
        CancellationToken cancellationToken)
    {
        var snapshot = BuildEstimateSnapshot(commercialAddon, currentState, action);
        return snapshot.AddonType switch
        {
            CommercialAddonType.Massive => await personnelFileRepository.CountActiveEmployeesAsync(companyPublicId, cancellationToken),
            CommercialAddonType.Specialized => snapshot.MinimumQuantity ?? 1,
            _ => 0
        };
    }

    private static AddonEstimateSnapshot BuildEstimateSnapshot(
        CommercialAddon commercialAddon,
        CompanyCommercialAddon? currentState,
        SubscriptionAddonChangeAction action)
    {
        if (action == SubscriptionAddonChangeAction.Deactivate && currentState is not null)
        {
            return new AddonEstimateSnapshot(
                currentState.AddonType,
                currentState.UnitPrice,
                currentState.MinimumQuantity,
                currentState.MinimumMonthlyFee);
        }

        return new AddonEstimateSnapshot(
            commercialAddon.Type,
            commercialAddon.UnitPrice,
            commercialAddon.MinimumQuantity,
            commercialAddon.MinimumMonthlyFee);
    }

    private static decimal CalculateEstimatedImpact(
        AddonEstimateSnapshot snapshot,
        SubscriptionAddonChangeAction action,
        int quantityBasis)
    {
        decimal estimate = snapshot.AddonType switch
        {
            CommercialAddonType.Massive => snapshot.MinimumMonthlyFee.HasValue
                ? Math.Max(snapshot.MinimumMonthlyFee.Value, snapshot.UnitPrice * quantityBasis)
                : snapshot.UnitPrice * quantityBasis,
            CommercialAddonType.Specialized => snapshot.UnitPrice * (snapshot.MinimumQuantity ?? Math.Max(1, quantityBasis)),
            _ => 0m
        };

        estimate = decimal.Round(estimate, 2, MidpointRounding.AwayFromZero);
        return action == SubscriptionAddonChangeAction.Deactivate ? -estimate : estimate;
    }

    private sealed record AddonEstimateSnapshot(
        CommercialAddonType AddonType,
        decimal UnitPrice,
        int? MinimumQuantity,
        decimal? MinimumMonthlyFee);
}

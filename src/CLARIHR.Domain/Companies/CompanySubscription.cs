using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Companies;

public sealed class CompanySubscription : AuditableEntity
{
    private readonly List<CompanySubscriptionStatusTransition> _statusTransitions = [];

    private CompanySubscription()
    {
    }

    private CompanySubscription(
        long companyId,
        long commercialPlanId,
        long commercialPlanVersionId,
        string planCode,
        string planName,
        int planVersionNumber,
        decimal baseMonthlyFee,
        decimal pricePerActiveEmployee,
        CompanySubscriptionPeriodicity periodicity,
        string currencyCode,
        SubscriptionStatus status,
        DateTime startDateUtc,
        DateTime? expiresAtUtc,
        DateTime? endDateUtc,
        Guid activatedByUserPublicId,
        DateTime activatedAtUtc,
        SubscriptionStatusChangeReasonCode statusReasonCode,
        SubscriptionStatusChangeOrigin statusOrigin,
        string? statusObservations)
    {
        if (companyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(companyId), "Company id must be greater than zero.");
        }

        if (commercialPlanId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(commercialPlanId), "Commercial plan id must be a persisted non-zero identifier.");
        }

        if (commercialPlanVersionId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(commercialPlanVersionId), "Commercial plan version id must be a persisted non-zero identifier.");
        }

        if (planVersionNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(planVersionNumber), "Plan version number must be greater than zero.");
        }

        if (!Enum.IsDefined(periodicity))
        {
            throw new ArgumentOutOfRangeException(nameof(periodicity), "Subscription periodicity is invalid.");
        }

        if (startDateUtc == default)
        {
            throw new ArgumentException("Start date is required.", nameof(startDateUtc));
        }

        if (expiresAtUtc.HasValue && expiresAtUtc.Value.Date < startDateUtc.Date)
        {
            throw new ArgumentOutOfRangeException(nameof(expiresAtUtc), "Expiration date cannot be earlier than start date.");
        }

        if (activatedAtUtc == default)
        {
            throw new ArgumentException("ActivatedAtUtc is required.", nameof(activatedAtUtc));
        }

        CompanyId = companyId;
        CommercialPlanId = commercialPlanId;
        CommercialPlanVersionId = commercialPlanVersionId;
        PlanCode = CompanyNormalization.NormalizePlanCode(planCode);
        PlanName = CompanyNormalization.Clean(planName, nameof(planName));
        PlanVersionNumber = planVersionNumber;
        BaseMonthlyFee = NormalizeAmount(baseMonthlyFee, nameof(baseMonthlyFee));
        PricePerActiveEmployee = NormalizeAmount(pricePerActiveEmployee, nameof(pricePerActiveEmployee));
        Periodicity = periodicity;
        CurrencyCode = CompanyNormalization.NormalizeCurrencyCode(currencyCode);
        Status = status;
        StartDateUtc = startDateUtc.Date;
        ExpiresAtUtc = expiresAtUtc?.Date;
        EndDateUtc = NormalizeEndDate(endDateUtc);
        ActivatedByUserPublicId = activatedByUserPublicId;
        ActivatedAtUtc = activatedAtUtc;
        StatusChangedAtUtc = activatedAtUtc;
        CurrentStatusReasonCode = statusReasonCode;
        CurrentStatusOrigin = statusOrigin;
        CurrentStatusObservations = CompanyNormalization.CleanOptional(statusObservations);

        AddTransition(
            previousStatus: null,
            newStatus: status,
            reasonCode: statusReasonCode,
            changedAtUtc: activatedAtUtc,
            origin: statusOrigin,
            actorUserPublicId: NormalizeActorId(activatedByUserPublicId),
            observations: statusObservations);
    }

    public long CompanyId { get; private set; }

    public long CommercialPlanId { get; private set; }

    public long CommercialPlanVersionId { get; private set; }

    public string PlanCode { get; private set; } = string.Empty;

    public string PlanName { get; private set; } = string.Empty;

    public int PlanVersionNumber { get; private set; }

    public decimal BaseMonthlyFee { get; private set; }

    public decimal PricePerActiveEmployee { get; private set; }

    public CompanySubscriptionPeriodicity Periodicity { get; private set; }

    public string CurrencyCode { get; private set; } = string.Empty;

    public SubscriptionStatus Status { get; private set; }

    public DateTime StartDateUtc { get; private set; }

    public DateTime? ExpiresAtUtc { get; private set; }

    public DateTime? EndDateUtc { get; private set; }

    public Guid ActivatedByUserPublicId { get; private set; }

    public DateTime ActivatedAtUtc { get; private set; }

    public DateTime StatusChangedAtUtc { get; private set; }

    public SubscriptionStatusChangeReasonCode CurrentStatusReasonCode { get; private set; }

    public string? CurrentStatusObservations { get; private set; }

    public SubscriptionStatusChangeOrigin CurrentStatusOrigin { get; private set; }

    public Guid ConcurrencyToken { get; private set; } = Guid.NewGuid();

    public IReadOnlyCollection<CompanySubscriptionStatusTransition> StatusTransitions => _statusTransitions;

    public static CompanySubscription Activate(long companyId, CommercialPlan commercialPlan, DateTime startDateUtc) =>
        Activate(
            companyId,
            commercialPlan,
            CompanySubscriptionPeriodicity.Monthly,
            startDateUtc,
            expiresAtUtc: null,
            Guid.Empty,
            startDateUtc,
            SubscriptionStatusChangeReasonCode.InitialAssignment,
            SubscriptionStatusChangeOrigin.SystemProcess,
            observations: null);

    public static CompanySubscription Activate(
        long companyId,
        CommercialPlan commercialPlan,
        CompanySubscriptionPeriodicity periodicity,
        DateTime startDateUtc,
        DateTime? expiresAtUtc,
        Guid activatedByUserPublicId,
        DateTime activatedAtUtc,
        SubscriptionStatusChangeReasonCode reasonCode,
        SubscriptionStatusChangeOrigin origin,
        string? observations)
    {
        var planVersion = commercialPlan.GetVersionEffectiveOn(startDateUtc);

        return new CompanySubscription(
            companyId,
            commercialPlan.Id,
            planVersion.Id,
            commercialPlan.Code,
            commercialPlan.Name,
            planVersion.VersionNumber,
            planVersion.BaseMonthlyFee,
            planVersion.PricePerActiveEmployee,
            periodicity,
            planVersion.CurrencyCode,
            SubscriptionStatus.Active,
            startDateUtc,
            expiresAtUtc,
            endDateUtc: null,
            activatedByUserPublicId,
            activatedAtUtc,
            reasonCode,
            origin,
            observations);
    }

    public static CompanySubscription Schedule(
        long companyId,
        CommercialPlan commercialPlan,
        CompanySubscriptionPeriodicity periodicity,
        DateTime startDateUtc,
        DateTime? expiresAtUtc,
        Guid activatedByUserPublicId,
        DateTime activatedAtUtc,
        SubscriptionStatusChangeReasonCode reasonCode,
        SubscriptionStatusChangeOrigin origin,
        string? observations)
    {
        var planVersion = commercialPlan.GetVersionEffectiveOn(startDateUtc);

        return new CompanySubscription(
            companyId,
            commercialPlan.Id,
            planVersion.Id,
            commercialPlan.Code,
            commercialPlan.Name,
            planVersion.VersionNumber,
            planVersion.BaseMonthlyFee,
            planVersion.PricePerActiveEmployee,
            periodicity,
            planVersion.CurrencyCode,
            SubscriptionStatus.Scheduled,
            startDateUtc,
            expiresAtUtc,
            endDateUtc: null,
            activatedByUserPublicId,
            activatedAtUtc,
            reasonCode,
            origin,
            observations);
    }

    public void Suspend(
        DateTime changedAtUtc,
        SubscriptionStatusChangeReasonCode reasonCode,
        string? observations,
        SubscriptionStatusChangeOrigin origin,
        Guid? actorUserPublicId)
    {
        ApplyStatusChange(
            SubscriptionStatus.Suspended,
            changedAtUtc,
            reasonCode,
            observations,
            origin,
            actorUserPublicId);

        EndDateUtc = null;
    }

    public void Reactivate(
        DateTime changedAtUtc,
        SubscriptionStatusChangeReasonCode reasonCode,
        string? observations,
        SubscriptionStatusChangeOrigin origin,
        Guid? actorUserPublicId)
    {
        if (ExpiresAtUtc.HasValue && ExpiresAtUtc.Value.Date < changedAtUtc.Date)
        {
            throw new InvalidOperationException("Suspended subscriptions past their expiration date cannot be reactivated.");
        }

        ApplyStatusChange(
            SubscriptionStatus.Active,
            changedAtUtc,
            reasonCode,
            observations,
            origin,
            actorUserPublicId);

        EndDateUtc = null;
    }

    public void Cancel(
        DateTime changedAtUtc,
        SubscriptionStatusChangeReasonCode reasonCode,
        string? observations,
        SubscriptionStatusChangeOrigin origin,
        Guid? actorUserPublicId)
    {
        var previousStatus = Status;

        ApplyStatusChange(
            SubscriptionStatus.Cancelled,
            changedAtUtc,
            reasonCode,
            observations,
            origin,
            actorUserPublicId);

        EndDateUtc = previousStatus == SubscriptionStatus.Scheduled
            ? null
            : MaxDate(StartDateUtc, changedAtUtc.Date);
    }

    public void PromoteScheduled(DateTime promotedAtUtc)
    {
        ApplyStatusChange(
            SubscriptionStatus.Active,
            promotedAtUtc,
            SubscriptionStatusChangeReasonCode.ScheduledStartReached,
            observations: null,
            SubscriptionStatusChangeOrigin.SystemProcess,
            actorUserPublicId: null);

        ActivatedByUserPublicId = Guid.Empty;
        ActivatedAtUtc = promotedAtUtc;
        EndDateUtc = null;
    }

    public void Expire(DateTime expiredAtUtc)
    {
        if (!ExpiresAtUtc.HasValue)
        {
            throw new InvalidOperationException("Only subscriptions with an expiration date can expire.");
        }

        ApplyStatusChange(
            SubscriptionStatus.Expired,
            expiredAtUtc,
            SubscriptionStatusChangeReasonCode.ExpirationReached,
            observations: null,
            SubscriptionStatusChangeOrigin.SystemProcess,
            actorUserPublicId: null);

        EndDateUtc = MaxDate(StartDateUtc, ExpiresAtUtc.Value.Date);
    }

    private void ApplyStatusChange(
        SubscriptionStatus nextStatus,
        DateTime changedAtUtc,
        SubscriptionStatusChangeReasonCode reasonCode,
        string? observations,
        SubscriptionStatusChangeOrigin origin,
        Guid? actorUserPublicId)
    {
        var previousStatus = Status;

        if (!SubscriptionStatusPolicy.CanTransition(previousStatus, nextStatus, origin))
        {
            throw new InvalidOperationException($"Transition from '{previousStatus}' to '{nextStatus}' is not allowed.");
        }

        if (!SubscriptionStatusPolicy.IsReasonAllowed(previousStatus, nextStatus, origin, reasonCode))
        {
            throw new InvalidOperationException($"Reason '{reasonCode}' is not allowed for the transition from '{previousStatus}' to '{nextStatus}'.");
        }

        if (nextStatus == SubscriptionStatus.Active &&
            previousStatus == SubscriptionStatus.Scheduled &&
            changedAtUtc.Date < StartDateUtc.Date)
        {
            throw new ArgumentOutOfRangeException(nameof(changedAtUtc), "Promotion cannot happen before the scheduled start date.");
        }

        Status = nextStatus;
        StatusChangedAtUtc = changedAtUtc;
        CurrentStatusReasonCode = reasonCode;
        CurrentStatusOrigin = origin;
        CurrentStatusObservations = CompanyNormalization.CleanOptional(observations);

        AddTransition(previousStatus, nextStatus, reasonCode, changedAtUtc, origin, actorUserPublicId, observations);
        RefreshConcurrencyToken();
    }

    private void AddTransition(
        SubscriptionStatus? previousStatus,
        SubscriptionStatus newStatus,
        SubscriptionStatusChangeReasonCode reasonCode,
        DateTime changedAtUtc,
        SubscriptionStatusChangeOrigin origin,
        Guid? actorUserPublicId,
        string? observations)
    {
        var transition = CompanySubscriptionStatusTransition.Create(
            previousStatus,
            newStatus,
            reasonCode,
            observations,
            changedAtUtc,
            origin,
            actorUserPublicId);

        if (Id > 0)
        {
            transition.BindToSubscription(Id);
        }

        _statusTransitions.Add(transition);
    }

    private static Guid? NormalizeActorId(Guid actorUserPublicId) =>
        actorUserPublicId == Guid.Empty ? null : actorUserPublicId;

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();

    private static DateTime MaxDate(DateTime firstDateUtc, DateTime secondDateUtc) =>
        firstDateUtc.Date >= secondDateUtc.Date ? firstDateUtc.Date : secondDateUtc.Date;

    private static DateTime? NormalizeEndDate(DateTime? endDateUtc) =>
        endDateUtc?.Date;

    public void BackfillInitialTransition(
        DateTime changedAtUtc,
        SubscriptionStatusChangeReasonCode reasonCode,
        SubscriptionStatusChangeOrigin origin)
    {
        if (_statusTransitions.Count > 0)
        {
            return;
        }

        AddTransition(
            previousStatus: null,
            newStatus: Status,
            reasonCode,
            changedAtUtc,
            origin,
            actorUserPublicId: NormalizeActorId(ActivatedByUserPublicId),
            observations: CurrentStatusObservations);
    }

    private static decimal NormalizeAmount(decimal value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Amount must be greater than or equal to zero.");
        }

        return value;
    }
}

using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Companies;

public sealed class CompanySubscriptionPlanChange : AuditableEntity
{
    private CompanySubscriptionPlanChange()
    {
    }

    private CompanySubscriptionPlanChange(
        long companyId,
        long companySubscriptionId,
        long? currentCommercialPlanId,
        long? currentCommercialPlanVersionId,
        string currentPlanCode,
        string currentPlanName,
        int currentPlanVersionNumber,
        decimal currentBaseMonthlyFee,
        decimal currentPricePerActiveEmployee,
        CompanySubscriptionPeriodicity currentPeriodicity,
        string currentCurrencyCode,
        long targetCommercialPlanId,
        long targetCommercialPlanVersionId,
        string targetPlanCode,
        string targetPlanName,
        int targetPlanVersionNumber,
        decimal targetBaseMonthlyFee,
        decimal targetPricePerActiveEmployee,
        CompanySubscriptionPeriodicity targetPeriodicity,
        string targetCurrencyCode,
        SubscriptionPlanChangeMode mode,
        SubscriptionPlanChangeReasonCode reasonCode,
        DateTime requestedAtUtc,
        DateTime effectiveDateUtc,
        Guid? requestedByUserPublicId,
        string? observations,
        decimal estimatedNextCharge,
        int activeEmployeeCount)
    {
        if (companyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(companyId), "Company id must be greater than zero.");
        }

        if (companySubscriptionId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(companySubscriptionId), "Company subscription id must be greater than zero.");
        }

        if (targetCommercialPlanId == 0 || targetCommercialPlanVersionId == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(targetCommercialPlanId), "Target plan identifiers must be persisted non-zero identifiers.");
        }

        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), "Plan change mode is invalid.");
        }

        if (!Enum.IsDefined(reasonCode))
        {
            throw new ArgumentOutOfRangeException(nameof(reasonCode), "Plan change reason code is invalid.");
        }

        if (requestedAtUtc == default)
        {
            throw new ArgumentException("RequestedAtUtc is required.", nameof(requestedAtUtc));
        }

        if (effectiveDateUtc == default)
        {
            throw new ArgumentException("EffectiveDateUtc is required.", nameof(effectiveDateUtc));
        }

        if (activeEmployeeCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(activeEmployeeCount), "Active employee count cannot be negative.");
        }

        CompanyId = companyId;
        CompanySubscriptionId = companySubscriptionId;
        CurrentCommercialPlanId = currentCommercialPlanId;
        CurrentCommercialPlanVersionId = currentCommercialPlanVersionId;
        CurrentPlanCode = CompanyNormalization.NormalizePlanCode(currentPlanCode);
        CurrentPlanName = CompanyNormalization.Clean(currentPlanName, nameof(currentPlanName));
        CurrentPlanVersionNumber = currentPlanVersionNumber;
        CurrentBaseMonthlyFee = NormalizeAmount(currentBaseMonthlyFee, nameof(currentBaseMonthlyFee));
        CurrentPricePerActiveEmployee = NormalizeAmount(currentPricePerActiveEmployee, nameof(currentPricePerActiveEmployee));
        CurrentPeriodicity = currentPeriodicity;
        CurrentCurrencyCode = CompanyNormalization.NormalizeCurrencyCode(currentCurrencyCode);
        TargetCommercialPlanId = targetCommercialPlanId;
        TargetCommercialPlanVersionId = targetCommercialPlanVersionId;
        TargetPlanCode = CompanyNormalization.NormalizePlanCode(targetPlanCode);
        TargetPlanName = CompanyNormalization.Clean(targetPlanName, nameof(targetPlanName));
        TargetPlanVersionNumber = targetPlanVersionNumber;
        TargetBaseMonthlyFee = NormalizeAmount(targetBaseMonthlyFee, nameof(targetBaseMonthlyFee));
        TargetPricePerActiveEmployee = NormalizeAmount(targetPricePerActiveEmployee, nameof(targetPricePerActiveEmployee));
        TargetPeriodicity = targetPeriodicity;
        TargetCurrencyCode = CompanyNormalization.NormalizeCurrencyCode(targetCurrencyCode);
        Mode = mode;
        ReasonCode = reasonCode;
        Status = effectiveDateUtc.Date <= requestedAtUtc.Date
            ? SubscriptionPlanChangeStatus.Applied
            : SubscriptionPlanChangeStatus.Scheduled;
        RequestedAtUtc = requestedAtUtc;
        EffectiveDateUtc = effectiveDateUtc.Date;
        RequestedByUserPublicId = requestedByUserPublicId;
        Observations = CompanyNormalization.CleanOptional(observations);
        EstimatedNextCharge = NormalizeAmount(estimatedNextCharge, nameof(estimatedNextCharge));
        ActiveEmployeeCount = activeEmployeeCount;

        if (Status == SubscriptionPlanChangeStatus.Applied)
        {
            AppliedAtUtc = requestedAtUtc;
        }
    }

    public long CompanyId { get; private set; }

    public long CompanySubscriptionId { get; private set; }

    public long? CurrentCommercialPlanId { get; private set; }

    public long? CurrentCommercialPlanVersionId { get; private set; }

    public string CurrentPlanCode { get; private set; } = string.Empty;

    public string CurrentPlanName { get; private set; } = string.Empty;

    public int CurrentPlanVersionNumber { get; private set; }

    public decimal CurrentBaseMonthlyFee { get; private set; }

    public decimal CurrentPricePerActiveEmployee { get; private set; }

    public CompanySubscriptionPeriodicity CurrentPeriodicity { get; private set; }

    public string CurrentCurrencyCode { get; private set; } = string.Empty;

    public long TargetCommercialPlanId { get; private set; }

    public long TargetCommercialPlanVersionId { get; private set; }

    public string TargetPlanCode { get; private set; } = string.Empty;

    public string TargetPlanName { get; private set; } = string.Empty;

    public int TargetPlanVersionNumber { get; private set; }

    public decimal TargetBaseMonthlyFee { get; private set; }

    public decimal TargetPricePerActiveEmployee { get; private set; }

    public CompanySubscriptionPeriodicity TargetPeriodicity { get; private set; }

    public string TargetCurrencyCode { get; private set; } = string.Empty;

    public SubscriptionPlanChangeMode Mode { get; private set; }

    public SubscriptionPlanChangeStatus Status { get; private set; }

    public SubscriptionPlanChangeReasonCode ReasonCode { get; private set; }

    public DateTime RequestedAtUtc { get; private set; }

    public DateTime EffectiveDateUtc { get; private set; }

    public Guid? RequestedByUserPublicId { get; private set; }

    public string? Observations { get; private set; }

    public decimal EstimatedNextCharge { get; private set; }

    public int ActiveEmployeeCount { get; private set; }

    public DateTime? AppliedAtUtc { get; private set; }

    public Guid? AppliedSubscriptionPublicId { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    public Guid? CancelledByUserPublicId { get; private set; }

    public string? CancellationObservations { get; private set; }

    public DateTime? RejectedAtUtc { get; private set; }

    public string? RejectionReason { get; private set; }

    public static CompanySubscriptionPlanChange Create(
        CompanySubscription currentSubscription,
        CommercialPlan? currentPlan,
        CommercialPlanVersion? currentPlanVersion,
        CommercialPlan targetPlan,
        CommercialPlanVersion targetPlanVersion,
        CompanySubscriptionPeriodicity targetPeriodicity,
        SubscriptionPlanChangeMode mode,
        SubscriptionPlanChangeReasonCode reasonCode,
        DateTime requestedAtUtc,
        DateTime effectiveDateUtc,
        Guid? requestedByUserPublicId,
        string? observations,
        decimal estimatedNextCharge,
        int activeEmployeeCount)
    {
        ArgumentNullException.ThrowIfNull(currentSubscription);
        ArgumentNullException.ThrowIfNull(targetPlan);
        ArgumentNullException.ThrowIfNull(targetPlanVersion);

        return new CompanySubscriptionPlanChange(
            currentSubscription.CompanyId,
            currentSubscription.Id,
            currentPlan?.Id,
            currentPlanVersion?.Id,
            currentSubscription.PlanCode,
            currentSubscription.PlanName,
            currentSubscription.PlanVersionNumber,
            currentSubscription.BaseMonthlyFee,
            currentSubscription.PricePerActiveEmployee,
            currentSubscription.Periodicity,
            currentSubscription.CurrencyCode,
            targetPlan.Id,
            targetPlanVersion.Id,
            targetPlan.Code,
            targetPlan.Name,
            targetPlanVersion.VersionNumber,
            targetPlanVersion.BaseMonthlyFee,
            targetPlanVersion.PricePerActiveEmployee,
            targetPeriodicity,
            targetPlanVersion.CurrencyCode,
            mode,
            reasonCode,
            requestedAtUtc,
            effectiveDateUtc,
            requestedByUserPublicId,
            observations,
            estimatedNextCharge,
            activeEmployeeCount);
    }

    public void MarkApplied(DateTime appliedAtUtc, Guid appliedSubscriptionPublicId)
    {
        if (Status is SubscriptionPlanChangeStatus.Cancelled or SubscriptionPlanChangeStatus.Rejected)
        {
            throw new InvalidOperationException("Cancelled or rejected plan changes cannot be applied.");
        }

        Status = SubscriptionPlanChangeStatus.Applied;
        AppliedAtUtc = appliedAtUtc;
        AppliedSubscriptionPublicId = appliedSubscriptionPublicId;
        RejectedAtUtc = null;
        RejectionReason = null;
    }

    public void Cancel(DateTime cancelledAtUtc, Guid? cancelledByUserPublicId, string observations)
    {
        if (Status != SubscriptionPlanChangeStatus.Scheduled)
        {
            throw new InvalidOperationException("Only scheduled plan changes can be cancelled.");
        }

        if (string.IsNullOrWhiteSpace(observations))
        {
            throw new ArgumentException("Cancellation observations are required.", nameof(observations));
        }

        Status = SubscriptionPlanChangeStatus.Cancelled;
        CancelledAtUtc = cancelledAtUtc;
        CancelledByUserPublicId = cancelledByUserPublicId;
        CancellationObservations = CompanyNormalization.Clean(observations, nameof(observations));
    }

    public void Reject(DateTime rejectedAtUtc, string rejectionReason)
    {
        if (Status != SubscriptionPlanChangeStatus.Scheduled)
        {
            throw new InvalidOperationException("Only scheduled plan changes can be rejected.");
        }

        if (string.IsNullOrWhiteSpace(rejectionReason))
        {
            throw new ArgumentException("Rejection reason is required.", nameof(rejectionReason));
        }

        Status = SubscriptionPlanChangeStatus.Rejected;
        RejectedAtUtc = rejectedAtUtc;
        RejectionReason = CompanyNormalization.Clean(rejectionReason, nameof(rejectionReason));
    }

    private static decimal NormalizeAmount(decimal amount, string paramName)
    {
        if (amount < 0m)
        {
            throw new ArgumentOutOfRangeException(paramName, "Amount cannot be negative.");
        }

        return decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    }
}

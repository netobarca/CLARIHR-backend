using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Companies;

public sealed class CompanyCommercialAddonChange : AuditableEntity
{
    private const string DefaultCurrencyCode = "USD";

    private CompanyCommercialAddonChange()
    {
    }

    private CompanyCommercialAddonChange(
        long companyId,
        long companySubscriptionId,
        long commercialAddonId,
        string addonCode,
        string addonName,
        CommercialAddonType addonType,
        CommercialAddonBillingModel billingModel,
        string measurementUnit,
        decimal unitPrice,
        int? minimumQuantity,
        decimal? minimumMonthlyFee,
        CommercialAddonPeriodicity periodicity,
        SubscriptionAddonChangeAction action,
        SubscriptionAddonChangeMode mode,
        SubscriptionAddonChangeReasonCode reasonCode,
        CompanyAddonStatus previousStatus,
        CompanyAddonStatus resultingStatus,
        DateTime requestedAtUtc,
        DateTime effectiveDateUtc,
        Guid? requestedByUserPublicId,
        string? observations,
        int quantityBasis,
        decimal estimatedNextChargeImpact)
    {
        if (companyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(companyId), "Company id must be greater than zero.");
        }

        if (companySubscriptionId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(companySubscriptionId), "Company subscription id must be greater than zero.");
        }

        if (commercialAddonId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(commercialAddonId), "Commercial addon id must be greater than zero.");
        }

        if (!Enum.IsDefined(addonType))
        {
            throw new ArgumentOutOfRangeException(nameof(addonType), "Commercial addon type is invalid.");
        }

        if (!Enum.IsDefined(billingModel))
        {
            throw new ArgumentOutOfRangeException(nameof(billingModel), "Commercial addon billing model is invalid.");
        }

        if (!Enum.IsDefined(periodicity))
        {
            throw new ArgumentOutOfRangeException(nameof(periodicity), "Commercial addon periodicity is invalid.");
        }

        if (!Enum.IsDefined(action))
        {
            throw new ArgumentOutOfRangeException(nameof(action), "Addon change action is invalid.");
        }

        if (!Enum.IsDefined(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode), "Addon change mode is invalid.");
        }

        if (!Enum.IsDefined(reasonCode))
        {
            throw new ArgumentOutOfRangeException(nameof(reasonCode), "Addon change reason code is invalid.");
        }

        if (!Enum.IsDefined(previousStatus))
        {
            throw new ArgumentOutOfRangeException(nameof(previousStatus), "Previous addon status is invalid.");
        }

        if (!Enum.IsDefined(resultingStatus))
        {
            throw new ArgumentOutOfRangeException(nameof(resultingStatus), "Resulting addon status is invalid.");
        }

        if (requestedAtUtc == default)
        {
            throw new ArgumentException("RequestedAtUtc is required.", nameof(requestedAtUtc));
        }

        if (effectiveDateUtc == default)
        {
            throw new ArgumentException("EffectiveDateUtc is required.", nameof(effectiveDateUtc));
        }

        if (quantityBasis < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantityBasis), "Quantity basis cannot be negative.");
        }

        CompanyId = companyId;
        CompanySubscriptionId = companySubscriptionId;
        CommercialAddonId = commercialAddonId;
        AddonCode = CompanyNormalization.NormalizePlanCode(addonCode);
        AddonName = CompanyNormalization.Clean(addonName, nameof(addonName));
        AddonType = addonType;
        BillingModel = billingModel;
        MeasurementUnit = CompanyNormalization.Clean(measurementUnit, nameof(measurementUnit));
        UnitPrice = NormalizeAmount(unitPrice, nameof(unitPrice));
        MinimumQuantity = NormalizeOptionalQuantity(minimumQuantity, nameof(minimumQuantity));
        MinimumMonthlyFee = NormalizeOptionalAmount(minimumMonthlyFee, nameof(minimumMonthlyFee));
        Periodicity = periodicity;
        CurrencyCode = DefaultCurrencyCode;
        Action = action;
        Mode = mode;
        ReasonCode = reasonCode;
        PreviousStatus = previousStatus;
        ResultingStatus = resultingStatus;
        Status = effectiveDateUtc.Date <= requestedAtUtc.Date
            ? SubscriptionAddonChangeStatus.Applied
            : SubscriptionAddonChangeStatus.Scheduled;
        RequestedAtUtc = requestedAtUtc;
        EffectiveDateUtc = effectiveDateUtc.Date;
        RequestedByUserPublicId = requestedByUserPublicId;
        Observations = CompanyNormalization.CleanOptional(observations);
        QuantityBasis = quantityBasis;
        EstimatedNextChargeImpact = NormalizeSignedAmount(estimatedNextChargeImpact, nameof(estimatedNextChargeImpact));

        if (Status == SubscriptionAddonChangeStatus.Applied)
        {
            AppliedAtUtc = requestedAtUtc;
        }

        RefreshConcurrencyToken();
    }

    public long CompanyId { get; private set; }

    public long CompanySubscriptionId { get; private set; }

    public long CommercialAddonId { get; private set; }

    public string AddonCode { get; private set; } = string.Empty;

    public string AddonName { get; private set; } = string.Empty;

    public CommercialAddonType AddonType { get; private set; }

    public CommercialAddonBillingModel BillingModel { get; private set; }

    public string MeasurementUnit { get; private set; } = string.Empty;

    public decimal UnitPrice { get; private set; }

    public int? MinimumQuantity { get; private set; }

    public decimal? MinimumMonthlyFee { get; private set; }

    public CommercialAddonPeriodicity Periodicity { get; private set; }

    public string CurrencyCode { get; private set; } = DefaultCurrencyCode;

    public SubscriptionAddonChangeAction Action { get; private set; }

    public SubscriptionAddonChangeMode Mode { get; private set; }

    public SubscriptionAddonChangeStatus Status { get; private set; }

    public SubscriptionAddonChangeReasonCode ReasonCode { get; private set; }

    public CompanyAddonStatus PreviousStatus { get; private set; }

    public CompanyAddonStatus ResultingStatus { get; private set; }

    public DateTime RequestedAtUtc { get; private set; }

    public DateTime EffectiveDateUtc { get; private set; }

    public Guid? RequestedByUserPublicId { get; private set; }

    public string? Observations { get; private set; }

    public int QuantityBasis { get; private set; }

    public decimal EstimatedNextChargeImpact { get; private set; }

    public DateTime? AppliedAtUtc { get; private set; }

    public Guid? AppliedSubscriptionPublicId { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    public Guid? CancelledByUserPublicId { get; private set; }

    public string? CancellationObservations { get; private set; }

    public DateTime? RejectedAtUtc { get; private set; }

    public string? RejectionReason { get; private set; }

    public Guid ConcurrencyToken { get; private set; } = Guid.NewGuid();

    public static CompanyCommercialAddonChange Create(
        CompanySubscription companySubscription,
        CommercialAddon commercialAddon,
        SubscriptionAddonChangeAction action,
        SubscriptionAddonChangeMode mode,
        SubscriptionAddonChangeReasonCode reasonCode,
        CompanyAddonStatus previousStatus,
        CompanyAddonStatus resultingStatus,
        DateTime requestedAtUtc,
        DateTime effectiveDateUtc,
        Guid? requestedByUserPublicId,
        string? observations,
        int quantityBasis,
        decimal estimatedNextChargeImpact)
    {
        ArgumentNullException.ThrowIfNull(companySubscription);
        ArgumentNullException.ThrowIfNull(commercialAddon);

        return new CompanyCommercialAddonChange(
            companySubscription.CompanyId,
            companySubscription.Id,
            commercialAddon.Id,
            commercialAddon.Code,
            commercialAddon.Name,
            commercialAddon.Type,
            commercialAddon.BillingModel,
            commercialAddon.MeasurementUnit,
            commercialAddon.UnitPrice,
            commercialAddon.MinimumQuantity,
            commercialAddon.MinimumMonthlyFee,
            commercialAddon.Periodicity,
            action,
            mode,
            reasonCode,
            previousStatus,
            resultingStatus,
            requestedAtUtc,
            effectiveDateUtc,
            requestedByUserPublicId,
            observations,
            quantityBasis,
            estimatedNextChargeImpact);
    }

    public void MarkApplied(DateTime appliedAtUtc, Guid appliedSubscriptionPublicId)
    {
        if (Status is SubscriptionAddonChangeStatus.Cancelled or SubscriptionAddonChangeStatus.Rejected)
        {
            throw new InvalidOperationException("Cancelled or rejected add-on changes cannot be applied.");
        }

        Status = SubscriptionAddonChangeStatus.Applied;
        AppliedAtUtc = appliedAtUtc;
        AppliedSubscriptionPublicId = appliedSubscriptionPublicId;
        ResultingStatus = Action == SubscriptionAddonChangeAction.Activate
            ? CompanyAddonStatus.Active
            : CompanyAddonStatus.Inactive;
        RejectedAtUtc = null;
        RejectionReason = null;
        RefreshConcurrencyToken();
    }

    public void Cancel(DateTime cancelledAtUtc, Guid? cancelledByUserPublicId, string observations)
    {
        if (Status != SubscriptionAddonChangeStatus.Scheduled)
        {
            throw new InvalidOperationException("Only scheduled add-on changes can be cancelled.");
        }

        if (string.IsNullOrWhiteSpace(observations))
        {
            throw new ArgumentException("Cancellation observations are required.", nameof(observations));
        }

        Status = SubscriptionAddonChangeStatus.Cancelled;
        CancelledAtUtc = cancelledAtUtc;
        CancelledByUserPublicId = cancelledByUserPublicId;
        CancellationObservations = CompanyNormalization.Clean(observations, nameof(observations));
        RefreshConcurrencyToken();
    }

    public void Reject(DateTime rejectedAtUtc, string rejectionReason)
    {
        if (Status != SubscriptionAddonChangeStatus.Scheduled)
        {
            throw new InvalidOperationException("Only scheduled add-on changes can be rejected.");
        }

        if (string.IsNullOrWhiteSpace(rejectionReason))
        {
            throw new ArgumentException("Rejection reason is required.", nameof(rejectionReason));
        }

        Status = SubscriptionAddonChangeStatus.Rejected;
        RejectedAtUtc = rejectedAtUtc;
        RejectionReason = CompanyNormalization.Clean(rejectionReason, nameof(rejectionReason));
        RefreshConcurrencyToken();
    }

    private static decimal NormalizeAmount(decimal amount, string paramName)
    {
        if (amount < 0m)
        {
            throw new ArgumentOutOfRangeException(paramName, "Amount cannot be negative.");
        }

        return decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal? NormalizeOptionalAmount(decimal? amount, string paramName)
    {
        if (!amount.HasValue)
        {
            return null;
        }

        return NormalizeAmount(amount.Value, paramName);
    }

    private static int? NormalizeOptionalQuantity(int? quantity, string paramName)
    {
        if (!quantity.HasValue)
        {
            return null;
        }

        if (quantity.Value < 0)
        {
            throw new ArgumentOutOfRangeException(paramName, "Quantity cannot be negative.");
        }

        return quantity.Value;
    }

    private static decimal NormalizeSignedAmount(decimal amount, string paramName) =>
        decimal.Round(amount, 2, MidpointRounding.AwayFromZero);

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}

using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Companies;

public sealed class CompanyCommercialAddon : AuditableEntity
{
    private const string DefaultCurrencyCode = "USD";

    private CompanyCommercialAddon()
    {
    }

    private CompanyCommercialAddon(
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
        CompanyAddonStatus status,
        DateTime statusEffectiveDateUtc)
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

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), "Company addon status is invalid.");
        }

        if (statusEffectiveDateUtc == default)
        {
            throw new ArgumentException("Status effective date is required.", nameof(statusEffectiveDateUtc));
        }

        CompanyId = companyId;
        CompanySubscriptionId = companySubscriptionId;
        CommercialAddonId = commercialAddonId;
        SetSnapshot(
            addonCode,
            addonName,
            addonType,
            billingModel,
            measurementUnit,
            unitPrice,
            minimumQuantity,
            minimumMonthlyFee,
            periodicity);
        Status = status;
        StatusEffectiveDateUtc = statusEffectiveDateUtc.Date;
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

    public CompanyAddonStatus Status { get; private set; }

    public DateTime StatusEffectiveDateUtc { get; private set; }

    public static CompanyCommercialAddon Create(
        CompanySubscription companySubscription,
        CommercialAddon commercialAddon,
        CompanyAddonStatus status,
        DateTime statusEffectiveDateUtc)
    {
        ArgumentNullException.ThrowIfNull(companySubscription);
        ArgumentNullException.ThrowIfNull(commercialAddon);

        return new CompanyCommercialAddon(
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
            status,
            statusEffectiveDateUtc);
    }

    public void ScheduleActivation(CompanySubscription companySubscription, CommercialAddon commercialAddon, DateTime effectiveDateUtc)
    {
        ArgumentNullException.ThrowIfNull(companySubscription);
        ArgumentNullException.ThrowIfNull(commercialAddon);

        SetAssociation(companySubscription, commercialAddon);
        SetSnapshot(
            commercialAddon.Code,
            commercialAddon.Name,
            commercialAddon.Type,
            commercialAddon.BillingModel,
            commercialAddon.MeasurementUnit,
            commercialAddon.UnitPrice,
            commercialAddon.MinimumQuantity,
            commercialAddon.MinimumMonthlyFee,
            commercialAddon.Periodicity);
        Status = CompanyAddonStatus.PendingActivation;
        StatusEffectiveDateUtc = effectiveDateUtc.Date;
    }

    public void ApplyActivation(CompanySubscription companySubscription, CommercialAddon commercialAddon, DateTime effectiveDateUtc)
    {
        ArgumentNullException.ThrowIfNull(companySubscription);
        ArgumentNullException.ThrowIfNull(commercialAddon);

        SetAssociation(companySubscription, commercialAddon);
        SetSnapshot(
            commercialAddon.Code,
            commercialAddon.Name,
            commercialAddon.Type,
            commercialAddon.BillingModel,
            commercialAddon.MeasurementUnit,
            commercialAddon.UnitPrice,
            commercialAddon.MinimumQuantity,
            commercialAddon.MinimumMonthlyFee,
            commercialAddon.Periodicity);
        Status = CompanyAddonStatus.Active;
        StatusEffectiveDateUtc = effectiveDateUtc.Date;
    }

    public void ScheduleDeactivation(DateTime effectiveDateUtc)
    {
        if (Status is not (CompanyAddonStatus.Active or CompanyAddonStatus.PendingDeactivation))
        {
            throw new InvalidOperationException("Only active add-ons can be scheduled for deactivation.");
        }

        Status = CompanyAddonStatus.PendingDeactivation;
        StatusEffectiveDateUtc = effectiveDateUtc.Date;
    }

    public void ApplyDeactivation(DateTime effectiveDateUtc)
    {
        if (Status is not (CompanyAddonStatus.Active or CompanyAddonStatus.PendingDeactivation))
        {
            throw new InvalidOperationException("Only active add-ons can be deactivated.");
        }

        Status = CompanyAddonStatus.Inactive;
        StatusEffectiveDateUtc = effectiveDateUtc.Date;
    }

    public void RestoreStatus(CompanyAddonStatus previousStatus, DateTime restoredAtUtc)
    {
        if (!Enum.IsDefined(previousStatus))
        {
            throw new ArgumentOutOfRangeException(nameof(previousStatus), "Company addon status is invalid.");
        }

        Status = previousStatus;
        StatusEffectiveDateUtc = restoredAtUtc.Date;
    }

    private void SetAssociation(CompanySubscription companySubscription, CommercialAddon commercialAddon)
    {
        if (companySubscription.CompanyId != CompanyId)
        {
            throw new InvalidOperationException("The addon state cannot be associated with a different company.");
        }

        CompanySubscriptionId = companySubscription.Id;
        CommercialAddonId = commercialAddon.Id;
    }

    private void SetSnapshot(
        string addonCode,
        string addonName,
        CommercialAddonType addonType,
        CommercialAddonBillingModel billingModel,
        string measurementUnit,
        decimal unitPrice,
        int? minimumQuantity,
        decimal? minimumMonthlyFee,
        CommercialAddonPeriodicity periodicity)
    {
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
}

using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Companies;

public sealed class CommercialAddon : AuditableEntity
{
    public const string MassiveMeasurementUnit = "active employee";
    private const int MeasurementUnitMaxLength = 80;

    private CommercialAddon()
    {
    }

    private CommercialAddon(
        Guid publicId,
        string code,
        string name,
        string? description,
        CommercialAddonType type,
        CommercialAddonBillingModel billingModel,
        string measurementUnit,
        decimal unitPrice,
        int? minimumQuantity,
        decimal? minimumMonthlyFee,
        CommercialAddonPeriodicity periodicity,
        CommercialAddonStatus status)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), "Commercial addon type is invalid.");
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
            throw new ArgumentOutOfRangeException(nameof(status), "Commercial addon status is invalid.");
        }

        PublicId = publicId;
        SetCode(code);
        SetName(name);
        Description = CompanyNormalization.CleanOptional(description);
        Type = type;
        ConfigurePricing(type, billingModel, measurementUnit, unitPrice, minimumQuantity, minimumMonthlyFee);
        Periodicity = periodicity;
        Status = status;
        ConcurrencyToken = Guid.NewGuid();
    }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public CommercialAddonType Type { get; private set; }

    public CommercialAddonBillingModel BillingModel { get; private set; }

    public string MeasurementUnit { get; private set; } = string.Empty;

    public decimal UnitPrice { get; private set; }

    public int? MinimumQuantity { get; private set; }

    public decimal? MinimumMonthlyFee { get; private set; }

    public CommercialAddonPeriodicity Periodicity { get; private set; }

    public CommercialAddonStatus Status { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static CommercialAddon Create(
        string code,
        string name,
        string? description,
        CommercialAddonType type,
        CommercialAddonBillingModel billingModel,
        string measurementUnit,
        decimal unitPrice,
        int? minimumQuantity,
        decimal? minimumMonthlyFee,
        CommercialAddonPeriodicity periodicity,
        CommercialAddonStatus status) =>
        new(
            Guid.NewGuid(),
            code,
            name,
            description,
            type,
            billingModel,
            measurementUnit,
            unitPrice,
            minimumQuantity,
            minimumMonthlyFee,
            periodicity,
            status);

    public void Update(
        string code,
        string name,
        string? description,
        CommercialAddonType type,
        CommercialAddonBillingModel billingModel,
        string measurementUnit,
        decimal unitPrice,
        int? minimumQuantity,
        decimal? minimumMonthlyFee,
        CommercialAddonPeriodicity periodicity)
    {
        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), "Commercial addon type is invalid.");
        }

        if (!Enum.IsDefined(billingModel))
        {
            throw new ArgumentOutOfRangeException(nameof(billingModel), "Commercial addon billing model is invalid.");
        }

        if (!Enum.IsDefined(periodicity))
        {
            throw new ArgumentOutOfRangeException(nameof(periodicity), "Commercial addon periodicity is invalid.");
        }

        SetCode(code);
        SetName(name);
        Description = CompanyNormalization.CleanOptional(description);
        Type = type;
        ConfigurePricing(type, billingModel, measurementUnit, unitPrice, minimumQuantity, minimumMonthlyFee);
        Periodicity = periodicity;
        RefreshConcurrencyToken();
    }

    public void Activate()
    {
        if (Status == CommercialAddonStatus.Active)
        {
            throw new InvalidOperationException("Commercial addon is already active.");
        }

        Status = CommercialAddonStatus.Active;
        RefreshConcurrencyToken();
    }

    public void Inactivate()
    {
        if (Status == CommercialAddonStatus.Inactive)
        {
            throw new InvalidOperationException("Commercial addon is already inactive.");
        }

        Status = CommercialAddonStatus.Inactive;
        RefreshConcurrencyToken();
    }

    private void SetCode(string code)
    {
        Code = CompanyNormalization.NormalizePlanCode(code);
        NormalizedCode = Code;
    }

    private void SetName(string name)
    {
        Name = CompanyNormalization.Clean(name, nameof(name));
        NormalizedName = CompanyNormalization.NormalizeName(name);
    }

    private void ConfigurePricing(
        CommercialAddonType type,
        CommercialAddonBillingModel billingModel,
        string measurementUnit,
        decimal unitPrice,
        int? minimumQuantity,
        decimal? minimumMonthlyFee)
    {
        var cleanedMeasurementUnit = CleanMeasurementUnit(measurementUnit);
        var normalizedMeasurementUnit = cleanedMeasurementUnit.ToLowerInvariant();
        var containsSeat = normalizedMeasurementUnit.Contains("seat", StringComparison.Ordinal);

        switch (type)
        {
            case CommercialAddonType.Massive:
                if (billingModel != CommercialAddonBillingModel.PerActiveEmployee)
                {
                    throw new ArgumentException("Massive commercial add-ons must be billed per active employee.", nameof(billingModel));
                }

                if (!string.Equals(normalizedMeasurementUnit, MassiveMeasurementUnit, StringComparison.Ordinal))
                {
                    throw new ArgumentException("Massive commercial add-ons must use the reserved active employee unit.", nameof(measurementUnit));
                }

                if (minimumQuantity.HasValue)
                {
                    throw new ArgumentException("Massive commercial add-ons cannot define a minimum quantity.", nameof(minimumQuantity));
                }

                BillingModel = billingModel;
                MeasurementUnit = MassiveMeasurementUnit;
                UnitPrice = NormalizeAmount(unitPrice, nameof(unitPrice));
                MinimumQuantity = null;
                MinimumMonthlyFee = NormalizeOptionalAmount(minimumMonthlyFee, nameof(minimumMonthlyFee));
                return;
            case CommercialAddonType.Specialized:
                if (billingModel == CommercialAddonBillingModel.PerActiveEmployee)
                {
                    throw new ArgumentException("Specialized commercial add-ons cannot be billed per active employee.", nameof(billingModel));
                }

                if (string.Equals(normalizedMeasurementUnit, MassiveMeasurementUnit, StringComparison.Ordinal))
                {
                    throw new ArgumentException("Specialized commercial add-ons cannot use the reserved active employee unit.", nameof(measurementUnit));
                }

                if (billingModel == CommercialAddonBillingModel.PerSeat && !containsSeat)
                {
                    throw new ArgumentException("Per-seat commercial add-ons must use a measurement unit containing 'seat'.", nameof(measurementUnit));
                }

                if (billingModel == CommercialAddonBillingModel.PerVolume && containsSeat)
                {
                    throw new ArgumentException("Per-volume commercial add-ons cannot use a measurement unit containing 'seat'.", nameof(measurementUnit));
                }

                if (minimumMonthlyFee.HasValue)
                {
                    throw new ArgumentException("Specialized commercial add-ons cannot define a minimum monthly fee.", nameof(minimumMonthlyFee));
                }

                BillingModel = billingModel;
                MeasurementUnit = cleanedMeasurementUnit;
                UnitPrice = NormalizeAmount(unitPrice, nameof(unitPrice));
                MinimumQuantity = NormalizeOptionalQuantity(minimumQuantity, nameof(minimumQuantity));
                MinimumMonthlyFee = null;
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), "Commercial addon type is invalid.");
        }
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();

    private static string CleanMeasurementUnit(string measurementUnit)
    {
        var cleaned = CompanyNormalization.Clean(measurementUnit, nameof(measurementUnit));
        return cleaned.Length <= MeasurementUnitMaxLength
            ? cleaned
            : throw new ArgumentOutOfRangeException(nameof(measurementUnit), $"Measurement unit cannot exceed {MeasurementUnitMaxLength} characters.");
    }

    private static decimal NormalizeAmount(decimal value, string parameterName)
    {
        if (value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Amount must be greater than or equal to zero.");
        }

        return value;
    }

    private static decimal? NormalizeOptionalAmount(decimal? value, string parameterName)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return NormalizeAmount(value.Value, parameterName);
    }

    private static int? NormalizeOptionalQuantity(int? value, string parameterName)
    {
        if (!value.HasValue)
        {
            return null;
        }

        if (value.Value < 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Quantity must be greater than or equal to zero.");
        }

        return value.Value;
    }
}

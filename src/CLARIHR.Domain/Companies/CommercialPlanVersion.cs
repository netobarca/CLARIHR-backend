using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Companies;

public sealed class CommercialPlanVersion : AuditableEntity
{
    private CommercialPlanVersion()
    {
    }

    private CommercialPlanVersion(
        long commercialPlanId,
        int versionNumber,
        string currencyCode,
        decimal baseMonthlyFee,
        decimal pricePerActiveEmployee,
        DateTime effectiveFromUtc,
        DateTime? effectiveToUtc)
    {
        if (commercialPlanId < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(commercialPlanId), "Commercial plan id cannot be negative.");
        }

        if (versionNumber <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(versionNumber), "Version number must be greater than zero.");
        }

        if (effectiveToUtc.HasValue && effectiveToUtc.Value <= effectiveFromUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(effectiveToUtc), "Effective end date must be greater than effective start date.");
        }

        CommercialPlanId = commercialPlanId;
        VersionNumber = versionNumber;
        CurrencyCode = CompanyNormalization.NormalizeCurrencyCode(currencyCode);
        BaseMonthlyFee = NormalizeAmount(baseMonthlyFee, nameof(baseMonthlyFee));
        PricePerActiveEmployee = NormalizeAmount(pricePerActiveEmployee, nameof(pricePerActiveEmployee));
        EffectiveFromUtc = effectiveFromUtc;
        EffectiveToUtc = effectiveToUtc;
    }

    public long CommercialPlanId { get; private set; }

    public int VersionNumber { get; private set; }

    public string CurrencyCode { get; private set; } = string.Empty;

    public decimal BaseMonthlyFee { get; private set; }

    public decimal PricePerActiveEmployee { get; private set; }

    public DateTime EffectiveFromUtc { get; private set; }

    public DateTime? EffectiveToUtc { get; private set; }

    public bool IsEffectiveOn(DateTime effectiveAtUtc) =>
        EffectiveFromUtc <= effectiveAtUtc &&
        (!EffectiveToUtc.HasValue || effectiveAtUtc < EffectiveToUtc.Value);

    public static CommercialPlanVersion Create(
        long commercialPlanId,
        int versionNumber,
        string currencyCode,
        decimal baseMonthlyFee,
        decimal pricePerActiveEmployee,
        DateTime effectiveFromUtc) =>
        new(
            commercialPlanId,
            versionNumber,
            currencyCode,
            baseMonthlyFee,
            pricePerActiveEmployee,
            effectiveFromUtc,
            effectiveToUtc: null);

    public void Close(DateTime effectiveToUtc)
    {
        if (EffectiveToUtc.HasValue)
        {
            throw new InvalidOperationException("Commercial plan version is already closed.");
        }

        if (effectiveToUtc <= EffectiveFromUtc)
        {
            throw new ArgumentOutOfRangeException(nameof(effectiveToUtc), "Effective end date must be greater than effective start date.");
        }

        EffectiveToUtc = effectiveToUtc;
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

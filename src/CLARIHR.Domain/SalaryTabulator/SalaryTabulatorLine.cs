using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.SalaryTabulator;

public sealed class SalaryTabulatorLine : TenantEntity
{
    private SalaryTabulatorLine()
    {
    }

    private SalaryTabulatorLine(
        Guid publicId,
        string salaryClassCode,
        string salaryScaleCode,
        string currencyCode,
        decimal baseAmount,
        decimal? minAmount,
        decimal? maxAmount,
        DateTime effectiveFromUtc,
        DateTime? effectiveToUtc,
        string? notes)
    {
        PublicId = publicId;
        SetSalaryClassCode(salaryClassCode);
        SetSalaryScaleCode(salaryScaleCode);
        SetCurrencyCode(currencyCode);
        ValidateAmountRange(baseAmount, minAmount, maxAmount);
        ValidateDateRange(effectiveFromUtc, effectiveToUtc);

        BaseAmount = baseAmount;
        MinAmount = minAmount;
        MaxAmount = maxAmount;
        EffectiveFromUtc = effectiveFromUtc;
        EffectiveToUtc = effectiveToUtc;
        Notes = SalaryTabulatorNormalization.CleanOptional(notes);
        IsActive = !effectiveToUtc.HasValue || effectiveToUtc.Value >= DateTime.UtcNow.Date;
        Version = 1;
        ConcurrencyToken = Guid.NewGuid();
    }

    public Guid PublicId { get; private set; }

    public string SalaryClassCode { get; private set; } = string.Empty;

    public string NormalizedSalaryClassCode { get; private set; } = string.Empty;

    public string SalaryScaleCode { get; private set; } = string.Empty;

    public string NormalizedSalaryScaleCode { get; private set; } = string.Empty;

    public string CurrencyCode { get; private set; } = string.Empty;

    public decimal BaseAmount { get; private set; }

    public decimal? MinAmount { get; private set; }

    public decimal? MaxAmount { get; private set; }

    public DateTime EffectiveFromUtc { get; private set; }

    public DateTime? EffectiveToUtc { get; private set; }

    public bool IsActive { get; private set; }

    public int Version { get; private set; }

    public string? Notes { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static SalaryTabulatorLine Create(
        string salaryClassCode,
        string salaryScaleCode,
        string currencyCode,
        decimal baseAmount,
        decimal? minAmount,
        decimal? maxAmount,
        DateTime effectiveFromUtc,
        DateTime? effectiveToUtc,
        string? notes) =>
        new(
            Guid.NewGuid(),
            salaryClassCode,
            salaryScaleCode,
            currencyCode,
            baseAmount,
            minAmount,
            maxAmount,
            effectiveFromUtc,
            effectiveToUtc,
            notes);

    public void ApplySameDateUpdate(
        string currencyCode,
        decimal baseAmount,
        decimal? minAmount,
        decimal? maxAmount,
        string? notes)
    {
        SetCurrencyCode(currencyCode);
        ValidateAmountRange(baseAmount, minAmount, maxAmount);

        BaseAmount = baseAmount;
        MinAmount = minAmount;
        MaxAmount = maxAmount;
        Notes = SalaryTabulatorNormalization.CleanOptional(notes);
        IsActive = !EffectiveToUtc.HasValue || EffectiveToUtc.Value >= DateTime.UtcNow.Date;

        Version++;
        ConcurrencyToken = Guid.NewGuid();
    }

    public void EndRange(DateTime effectiveToUtc)
    {
        if (effectiveToUtc < EffectiveFromUtc)
        {
            throw new InvalidOperationException("Effective end date cannot be less than effective start date.");
        }

        EffectiveToUtc = effectiveToUtc;
        IsActive = false;
        Version++;
        ConcurrencyToken = Guid.NewGuid();
    }

    public void Inactivate(DateTime asOfUtc)
    {
        if (asOfUtc <= EffectiveFromUtc)
        {
            EffectiveToUtc = EffectiveFromUtc;
        }
        else
        {
        EffectiveToUtc = asOfUtc.AddDays(-1);
        }

        IsActive = false;
        Version++;
        ConcurrencyToken = Guid.NewGuid();
    }

    private void SetSalaryClassCode(string code)
    {
        SalaryClassCode = SalaryTabulatorNormalization.Clean(code, nameof(code));
        NormalizedSalaryClassCode = SalaryTabulatorNormalization.NormalizeCode(code);
    }

    private void SetSalaryScaleCode(string code)
    {
        SalaryScaleCode = SalaryTabulatorNormalization.Clean(code, nameof(code));
        NormalizedSalaryScaleCode = SalaryTabulatorNormalization.NormalizeCode(code);
    }

    private void SetCurrencyCode(string code)
    {
        CurrencyCode = SalaryTabulatorNormalization.NormalizeCode(code);
    }

    private static void ValidateAmountRange(decimal baseAmount, decimal? minAmount, decimal? maxAmount)
    {
        if (baseAmount <= 0)
        {
            throw new InvalidOperationException("BaseAmount must be greater than zero.");
        }

        if (minAmount.HasValue && minAmount.Value < 0)
        {
            throw new InvalidOperationException("MinAmount cannot be negative.");
        }

        if (maxAmount.HasValue && maxAmount.Value < 0)
        {
            throw new InvalidOperationException("MaxAmount cannot be negative.");
        }

        if (minAmount.HasValue && maxAmount.HasValue && minAmount.Value > maxAmount.Value)
        {
            throw new InvalidOperationException("MinAmount cannot be greater than MaxAmount.");
        }

        if (minAmount.HasValue && baseAmount < minAmount.Value)
        {
            throw new InvalidOperationException("BaseAmount cannot be less than MinAmount.");
        }

        if (maxAmount.HasValue && baseAmount > maxAmount.Value)
        {
            throw new InvalidOperationException("BaseAmount cannot be greater than MaxAmount.");
        }
    }

    private static void ValidateDateRange(DateTime effectiveFromUtc, DateTime? effectiveToUtc)
    {
        if (effectiveFromUtc == default)
        {
            throw new InvalidOperationException("EffectiveFromUtc is required.");
        }

        if (effectiveToUtc.HasValue && effectiveToUtc.Value < effectiveFromUtc)
        {
            throw new InvalidOperationException("EffectiveToUtc cannot be less than EffectiveFromUtc.");
        }
    }
}

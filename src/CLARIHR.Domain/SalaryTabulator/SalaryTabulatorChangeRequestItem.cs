using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.SalaryTabulator;

public sealed class SalaryTabulatorChangeRequestItem : TenantEntity
{
    private SalaryTabulatorChangeRequestItem()
    {
    }

    private SalaryTabulatorChangeRequestItem(
        string salaryClassCode,
        string salaryScaleCode,
        string currencyCode,
        SalaryTabulatorChangeType changeType,
        decimal? currentBaseAmount,
        decimal? proposedBaseAmount,
        decimal? currentMinAmount,
        decimal? proposedMinAmount,
        decimal? currentMaxAmount,
        decimal? proposedMaxAmount,
        string? notes)
    {
        SetSalaryClassCode(salaryClassCode);
        SetSalaryScaleCode(salaryScaleCode);
        SetCurrencyCode(currencyCode);
        ValidateByChangeType(changeType, proposedBaseAmount, proposedMinAmount, proposedMaxAmount);

        ChangeType = changeType;
        CurrentBaseAmount = currentBaseAmount;
        ProposedBaseAmount = proposedBaseAmount;
        CurrentMinAmount = currentMinAmount;
        ProposedMinAmount = proposedMinAmount;
        CurrentMaxAmount = currentMaxAmount;
        ProposedMaxAmount = proposedMaxAmount;
        Notes = SalaryTabulatorNormalization.CleanOptional(notes);
    }

    public long SalaryTabulatorChangeRequestId { get; private set; }

    public SalaryTabulatorChangeRequest SalaryTabulatorChangeRequest { get; private set; } = null!;

    public string SalaryClassCode { get; private set; } = string.Empty;

    public string NormalizedSalaryClassCode { get; private set; } = string.Empty;

    public string SalaryScaleCode { get; private set; } = string.Empty;

    public string NormalizedSalaryScaleCode { get; private set; } = string.Empty;

    public string CurrencyCode { get; private set; } = string.Empty;

    public SalaryTabulatorChangeType ChangeType { get; private set; }

    public decimal? CurrentBaseAmount { get; private set; }

    public decimal? ProposedBaseAmount { get; private set; }

    public decimal? CurrentMinAmount { get; private set; }

    public decimal? ProposedMinAmount { get; private set; }

    public decimal? CurrentMaxAmount { get; private set; }

    public decimal? ProposedMaxAmount { get; private set; }

    public string? Notes { get; private set; }

    public static SalaryTabulatorChangeRequestItem Create(
        string salaryClassCode,
        string salaryScaleCode,
        string currencyCode,
        SalaryTabulatorChangeType changeType,
        decimal? currentBaseAmount,
        decimal? proposedBaseAmount,
        decimal? currentMinAmount,
        decimal? proposedMinAmount,
        decimal? currentMaxAmount,
        decimal? proposedMaxAmount,
        string? notes) =>
        new(
            salaryClassCode,
            salaryScaleCode,
            currencyCode,
            changeType,
            currentBaseAmount,
            proposedBaseAmount,
            currentMinAmount,
            proposedMinAmount,
            currentMaxAmount,
            proposedMaxAmount,
            notes);

    public void SetCurrentAmounts(decimal? currentBaseAmount, decimal? currentMinAmount, decimal? currentMaxAmount)
    {
        CurrentBaseAmount = currentBaseAmount;
        CurrentMinAmount = currentMinAmount;
        CurrentMaxAmount = currentMaxAmount;
    }

    private void SetSalaryClassCode(string value)
    {
        SalaryClassCode = SalaryTabulatorNormalization.Clean(value, nameof(value));
        NormalizedSalaryClassCode = SalaryTabulatorNormalization.NormalizeCode(value);
    }

    private void SetSalaryScaleCode(string value)
    {
        SalaryScaleCode = SalaryTabulatorNormalization.Clean(value, nameof(value));
        NormalizedSalaryScaleCode = SalaryTabulatorNormalization.NormalizeCode(value);
    }

    private void SetCurrencyCode(string value)
    {
        CurrencyCode = SalaryTabulatorNormalization.NormalizeCode(value);
    }

    private static void ValidateByChangeType(
        SalaryTabulatorChangeType changeType,
        decimal? proposedBaseAmount,
        decimal? proposedMinAmount,
        decimal? proposedMaxAmount)
    {
        if (changeType == SalaryTabulatorChangeType.Inactivate)
        {
            return;
        }

        if (!proposedBaseAmount.HasValue || proposedBaseAmount.Value <= 0)
        {
            throw new InvalidOperationException("ProposedBaseAmount must be greater than zero.");
        }

        if (proposedMinAmount.HasValue && proposedMinAmount.Value < 0)
        {
            throw new InvalidOperationException("ProposedMinAmount cannot be negative.");
        }

        if (proposedMaxAmount.HasValue && proposedMaxAmount.Value < 0)
        {
            throw new InvalidOperationException("ProposedMaxAmount cannot be negative.");
        }

        if (proposedMinAmount.HasValue && proposedMaxAmount.HasValue && proposedMinAmount.Value > proposedMaxAmount.Value)
        {
            throw new InvalidOperationException("ProposedMinAmount cannot be greater than ProposedMaxAmount.");
        }

        if (proposedMinAmount.HasValue && proposedBaseAmount.Value < proposedMinAmount.Value)
        {
            throw new InvalidOperationException("ProposedBaseAmount cannot be less than ProposedMinAmount.");
        }

        if (proposedMaxAmount.HasValue && proposedBaseAmount.Value > proposedMaxAmount.Value)
        {
            throw new InvalidOperationException("ProposedBaseAmount cannot be greater than ProposedMaxAmount.");
        }
    }
}

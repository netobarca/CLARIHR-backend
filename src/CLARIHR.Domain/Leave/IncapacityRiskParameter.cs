using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Leave;

/// <summary>
/// One subsidy tranche of an <see cref="IncapacityRisk"/> (day range → subsidy percent + payer).
/// Immutable child: the parent replaces the full set via <see cref="IncapacityRisk.ReplaceParameters"/>,
/// so it carries no mutators, no <c>IsActive</c> and no concurrency token of its own.
/// </summary>
public sealed class IncapacityRiskParameter : TenantEntity
{
    public const int MaxPayerCodeLength = 20;

    private IncapacityRiskParameter()
    {
    }

    private IncapacityRiskParameter(
        Guid publicId,
        int dayFrom,
        int? dayTo,
        decimal subsidyPercent,
        string payerCode,
        int sortOrder)
    {
        if (dayFrom < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(dayFrom), "Day from must be greater than or equal to one.");
        }

        if (dayTo is { } upperBound && upperBound < dayFrom)
        {
            throw new ArgumentOutOfRangeException(nameof(dayTo), "Day to must be greater than or equal to day from.");
        }

        if (subsidyPercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(subsidyPercent), "Subsidy percent must be between 0 and 100.");
        }

        if (sortOrder < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be greater than or equal to one.");
        }

        var normalizedPayerCode = LeaveNormalization.NormalizeCode(payerCode);
        if (normalizedPayerCode.Length > MaxPayerCodeLength)
        {
            throw new ArgumentException($"Payer code must be {MaxPayerCodeLength} characters or fewer.", nameof(payerCode));
        }

        PublicId = publicId;
        DayFrom = dayFrom;
        DayTo = dayTo;
        SubsidyPercent = subsidyPercent;
        PayerCode = normalizedPayerCode;
        SortOrder = sortOrder;
    }

    public long IncapacityRiskId { get; private set; }

    public int DayFrom { get; private set; }

    public int? DayTo { get; private set; }

    public decimal SubsidyPercent { get; private set; }

    public string PayerCode { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    internal static IncapacityRiskParameter Create(
        int dayFrom,
        int? dayTo,
        decimal subsidyPercent,
        string payerCode,
        int sortOrder) =>
        new(
            Guid.NewGuid(),
            dayFrom,
            dayTo,
            subsidyPercent,
            payerCode,
            sortOrder);
}

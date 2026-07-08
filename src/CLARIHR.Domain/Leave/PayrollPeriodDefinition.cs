using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Leave;

/// <summary>
/// Company-managed master of payroll periods ("quincenas / periodos de planilla") per pay-period type
/// and year. <see cref="PayPeriodTypeCode"/> references the country-scoped pay-periods general catalog
/// and is validated against it in the handler — the entity only stores the normalized code.
/// </summary>
public sealed class PayrollPeriodDefinition : TenantEntity
{
    public const int MaxPayPeriodTypeCodeLength = 80;
    public const int MaxLabelLength = 80;
    public const int MinYear = 2000;
    public const int MaxYear = 2100;

    private PayrollPeriodDefinition()
    {
    }

    private PayrollPeriodDefinition(
        Guid publicId,
        string payPeriodTypeCode,
        int year,
        int number,
        string label,
        DateOnly startDate,
        DateOnly endDate)
    {
        PublicId = publicId;
        SetPayPeriodTypeCode(payPeriodTypeCode);
        SetYear(year);
        SetNumber(number);
        SetLabel(label);
        SetDates(startDate, endDate);
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public string PayPeriodTypeCode { get; private set; } = string.Empty;

    public int Year { get; private set; }

    public int Number { get; private set; }

    public string Label { get; private set; } = string.Empty;

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static PayrollPeriodDefinition Create(
        string payPeriodTypeCode,
        int year,
        int number,
        string label,
        DateOnly startDate,
        DateOnly endDate) =>
        new(
            Guid.NewGuid(),
            payPeriodTypeCode,
            year,
            number,
            label,
            startDate,
            endDate);

    public void Update(
        string payPeriodTypeCode,
        int year,
        int number,
        string label,
        DateOnly startDate,
        DateOnly endDate)
    {
        SetPayPeriodTypeCode(payPeriodTypeCode);
        SetYear(year);
        SetNumber(number);
        SetLabel(label);
        SetDates(startDate, endDate);
        RefreshConcurrencyToken();
    }

    public void Activate()
    {
        IsActive = true;
        RefreshConcurrencyToken();
    }

    public void Inactivate()
    {
        IsActive = false;
        RefreshConcurrencyToken();
    }

    private void SetPayPeriodTypeCode(string payPeriodTypeCode)
    {
        var normalized = LeaveNormalization.NormalizeCode(payPeriodTypeCode);
        if (normalized.Length > MaxPayPeriodTypeCodeLength)
        {
            throw new ArgumentException(
                $"Pay period type code must be {MaxPayPeriodTypeCodeLength} characters or fewer.",
                nameof(payPeriodTypeCode));
        }

        PayPeriodTypeCode = normalized;
    }

    private void SetYear(int year)
    {
        if (year is < MinYear or > MaxYear)
        {
            throw new ArgumentOutOfRangeException(nameof(year), $"Year must be between {MinYear} and {MaxYear}.");
        }

        Year = year;
    }

    private void SetNumber(int number)
    {
        if (number < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(number), "Number must be greater than or equal to one.");
        }

        Number = number;
    }

    private void SetLabel(string label)
    {
        Label = LeaveNormalization.Clean(label, nameof(label));
        if (Label.Length > MaxLabelLength)
        {
            throw new ArgumentException($"Label must be {MaxLabelLength} characters or fewer.", nameof(label));
        }
    }

    private void SetDates(DateOnly startDate, DateOnly endDate)
    {
        if (startDate == default)
        {
            throw new ArgumentException("Start date is required.", nameof(startDate));
        }

        if (endDate < startDate)
        {
            throw new ArgumentException("End date must be greater than or equal to start date.", nameof(endDate));
        }

        StartDate = startDate;
        EndDate = endDate;
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}

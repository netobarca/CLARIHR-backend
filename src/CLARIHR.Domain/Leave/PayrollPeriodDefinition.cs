using CLARIHR.Domain.Common;
using CLARIHR.Domain.Payroll;

namespace CLARIHR.Domain.Leave;

/// <summary>
/// Company-managed master of payroll periods ("quincenas / periodos de planilla") per pay-period type
/// and year. <see cref="PayPeriodTypeCode"/> references the country-scoped pay-periods general catalog
/// and is validated against it in the handler — the entity only stores the normalized code.
/// EXTENDED by REQ-012 (D-03, M2): a period may hang from a Nómina (<see cref="PayrollDefinitionId"/> —
/// NULL for legacy rows, mandatory when the payroll travels on new creates), carries the schedule
/// (<see cref="CutoffDate"/>/<see cref="PaymentDate"/>/<see cref="Month"/>), the materialized
/// overtime/attendance entry windows (P-18: editable dates derived from the Nómina rule by the annual
/// calendar generator) and a lifecycle <see cref="StatusCode"/> (born GENERADO; CERRADO by its run's
/// closure — same transaction, PR-6; ANULADO only while no active run points at it).
/// </summary>
public sealed class PayrollPeriodDefinition : TenantEntity
{
    public const int MaxPayPeriodTypeCodeLength = 80;
    public const int MaxLabelLength = 80;
    public const int MaxCodeLength = 80;
    public const int MaxStatusCodeLength = 80;
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
        StatusCode = PayrollPeriodStatuses.Generado;
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public string PayPeriodTypeCode { get; private set; } = string.Empty;

    public int Year { get; private set; }

    public int Number { get; private set; }

    public string Label { get; private set; } = string.Empty;

    public DateOnly StartDate { get; private set; }

    public DateOnly EndDate { get; private set; }

    /// <summary>Period code (e.g. «{NOMINA}-{YYYY}-{NN}»). NULL only on rows older than the M2 backfill.</summary>
    public string? Code { get; private set; }

    /// <summary>The Nómina this period hangs from (REQ-012 D-03). NULL for legacy rows without a payroll.</summary>
    public long? PayrollDefinitionId { get; private set; }

    /// <summary>Administrative cutoff date; within [StartDate, EndDate] (editable — defaults to the end).</summary>
    public DateOnly? CutoffDate { get; private set; }

    /// <summary>Scheduled payment date (editable — defaults to the period end; may fall outside the range).</summary>
    public DateOnly? PaymentDate { get; private set; }

    /// <summary>Accounting month the period applies to (1-12; P-04 soft — may differ from the range's calendar month).</summary>
    public int? Month { get; private set; }

    /// <summary>Whether overtime entry is accepted against this period (D-05).</summary>
    public bool AllowsOvertimeEntry { get; private set; }

    public DateOnly? OvertimeEntryStart { get; private set; }

    public DateOnly? OvertimeEntryEnd { get; private set; }

    /// <summary>Whether attendance entry is accepted (anticipated config — F1 stores it, no consumer yet).</summary>
    public bool AllowsAttendance { get; private set; }

    public DateOnly? AttendanceEntryStart { get; private set; }

    public DateOnly? AttendanceEntryEnd { get; private set; }

    /// <summary>Lifecycle status (<see cref="PayrollPeriodStatuses"/>): GENERADO → CERRADO / ANULADO.</summary>
    public string StatusCode { get; private set; } = PayrollPeriodStatuses.Generado;

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    /// <summary>Editable while GENERADO; a closed/annulled period is immutable (PAYROLL_PERIOD_STATE_RULE_VIOLATION).</summary>
    public bool IsEditable => StatusCode == PayrollPeriodStatuses.Generado;

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
        EnsureEditable();
        SetPayPeriodTypeCode(payPeriodTypeCode);
        SetYear(year);
        SetNumber(number);
        SetLabel(label);
        SetDates(startDate, endDate);
        RefreshConcurrencyToken();
    }

    /// <summary>Links the period to its Nómina (NULL detaches — only meaningful pre-run).</summary>
    public void AssignDefinition(long? payrollDefinitionId)
    {
        EnsureEditable();
        PayrollDefinitionId = payrollDefinitionId;
        RefreshConcurrencyToken();
    }

    public void SetCode(string? code)
    {
        EnsureEditable();
        if (code is null)
        {
            Code = null;
        }
        else
        {
            var normalized = LeaveNormalization.NormalizeCode(code);
            if (normalized.Length > MaxCodeLength)
            {
                throw new ArgumentException($"Code must be {MaxCodeLength} characters or fewer.", nameof(code));
            }

            Code = normalized;
        }

        RefreshConcurrencyToken();
    }

    /// <summary>Cutoff (within the range), payment date and accounting month (1-12).</summary>
    public void SetSchedule(DateOnly? cutoffDate, DateOnly? paymentDate, int? month)
    {
        EnsureEditable();
        if (cutoffDate.HasValue && (cutoffDate.Value < StartDate || cutoffDate.Value > EndDate))
        {
            throw new ArgumentException("Cutoff date must fall within the period range.", nameof(cutoffDate));
        }

        if (month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(month), "Month must be between 1 and 12.");
        }

        CutoffDate = cutoffDate;
        PaymentDate = paymentDate;
        Month = month;
        RefreshConcurrencyToken();
    }

    /// <summary>Materialized entry windows (P-18). Dates require their flag; each pair must be coherent.</summary>
    public void SetWindows(
        bool allowsOvertimeEntry,
        DateOnly? overtimeEntryStart,
        DateOnly? overtimeEntryEnd,
        bool allowsAttendance,
        DateOnly? attendanceEntryStart,
        DateOnly? attendanceEntryEnd)
    {
        EnsureEditable();
        ValidateWindow(allowsOvertimeEntry, overtimeEntryStart, overtimeEntryEnd, "overtime");
        ValidateWindow(allowsAttendance, attendanceEntryStart, attendanceEntryEnd, "attendance");

        AllowsOvertimeEntry = allowsOvertimeEntry;
        OvertimeEntryStart = overtimeEntryStart;
        OvertimeEntryEnd = overtimeEntryEnd;
        AllowsAttendance = allowsAttendance;
        AttendanceEntryStart = attendanceEntryStart;
        AttendanceEntryEnd = attendanceEntryEnd;
        RefreshConcurrencyToken();
    }

    /// <summary>Closed by its run's closure (same transaction — REQ-012 PR-6).</summary>
    public void Close()
    {
        if (StatusCode != PayrollPeriodStatuses.Generado)
        {
            throw new InvalidOperationException("Only a GENERADO period can be closed.");
        }

        StatusCode = PayrollPeriodStatuses.Cerrado;
        RefreshConcurrencyToken();
    }

    /// <summary>Annullable only while no active run points at it (guard in the handler once runs exist).</summary>
    public void Annul()
    {
        if (StatusCode != PayrollPeriodStatuses.Generado)
        {
            throw new InvalidOperationException("Only a GENERADO period can be annulled.");
        }

        StatusCode = PayrollPeriodStatuses.Anulado;
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

    private void EnsureEditable()
    {
        if (!IsEditable)
        {
            throw new InvalidOperationException("A closed or annulled payroll period cannot be modified.");
        }
    }

    private static void ValidateWindow(bool allows, DateOnly? start, DateOnly? end, string windowName)
    {
        if (!allows && (start.HasValue || end.HasValue))
        {
            throw new ArgumentException($"The {windowName} entry window dates require the window to be enabled.");
        }

        if (start.HasValue && end.HasValue && end.Value < start.Value)
        {
            throw new ArgumentException($"The {windowName} entry window end must be on or after its start.");
        }
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

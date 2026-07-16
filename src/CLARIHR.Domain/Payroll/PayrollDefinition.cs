using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Payroll;

/// <summary>
/// Company-managed master "Nómina" (REQ-012 D-02 ajustada): the payroll a company runs — its type
/// (country catalog <c>payroll-types</c>, the code that matches the plaza's <c>PayrollTypeCode</c>),
/// pay frequency (<c>pay-periods</c>), total periods per year, minimum-income guarantee flag, currency
/// and the overtime/attendance entry-window rules (P-18 "regla fuente + offset": an offset in days over
/// the period end, materialized into EDITABLE dates when the annual calendar is generated in PR-2).
/// Deliberately WITHOUT "aplicación" nor "parámetros especiales" (P-02 eliminated / P-03 ignored in F1).
/// Mirrors the governed masters (<c>CostCenter</c> / <c>OvertimeType</c>): filtered unique
/// <c>(tenant, normalized_code) WHERE is_active</c>, logical activate/inactivate (an inactive payroll
/// accepts no new periods/runs; existing history keeps referencing it).
/// </summary>
public sealed class PayrollDefinition : TenantEntity
{
    public const int MaxCodeLength = 80;
    public const int MaxNameLength = 200;
    public const int MaxPayrollTypeCodeLength = 80;
    public const int MaxPayPeriodCodeLength = 80;
    public const int CurrencyCodeLength = 3;

    private PayrollDefinition()
    {
    }

    private PayrollDefinition(
        Guid publicId,
        string code,
        string name,
        string payrollTypeCode,
        string payPeriodCode,
        int totalPeriods,
        bool guaranteesMinimumIncome,
        string currencyCode,
        bool overtimeWindowEnabled,
        int? overtimeWindowOffsetDays,
        bool attendanceWindowEnabled,
        int? attendanceWindowOffsetDays)
    {
        PublicId = publicId;
        SetCode(code);
        SetName(name);
        SetPayrollTypeCode(payrollTypeCode);
        SetPayPeriodCode(payPeriodCode);
        SetTotalPeriods(totalPeriods);
        GuaranteesMinimumIncome = guaranteesMinimumIncome;
        SetCurrencyCode(currencyCode);
        SetOvertimeWindow(overtimeWindowEnabled, overtimeWindowOffsetDays);
        SetAttendanceWindow(attendanceWindowEnabled, attendanceWindowOffsetDays);
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    /// <summary>"Tipo" of the payroll — a code of the country catalog <c>payroll-types</c> (P-06/P-07).</summary>
    public string PayrollTypeCode { get; private set; } = string.Empty;

    /// <summary>Pay frequency — a code of the country catalog <c>pay-periods</c>.</summary>
    public string PayPeriodCode { get; private set; } = string.Empty;

    /// <summary>
    /// Periods per year. Free (≥ 1): only MENSUAL/QUINCENAL/SEMANAL have a canonical cadence
    /// (<see cref="PayrollFrequencies"/>) and deviations (a 13th run) are deliberate — soft validation.
    /// </summary>
    public int TotalPeriods { get; private set; }

    /// <summary>Whether the run must guarantee the legal minimum income after deductions (P-08).</summary>
    public bool GuaranteesMinimumIncome { get; private set; }

    public string CurrencyCode { get; private set; } = string.Empty;

    /// <summary>Whether periods of this payroll accept overtime entry within a window (D-05).</summary>
    public bool OvertimeWindowEnabled { get; private set; }

    /// <summary>
    /// Days added to the period end to close the overtime-entry window (P-18; may be negative — the
    /// window is [period start, period end + offset]). Only meaningful while the window is enabled.
    /// </summary>
    public int? OvertimeWindowOffsetDays { get; private set; }

    /// <summary>Whether periods of this payroll accept attendance entry (anticipated config — F1 stores it).</summary>
    public bool AttendanceWindowEnabled { get; private set; }

    /// <summary>Days added to the period end to close the attendance window; only with the window enabled.</summary>
    public int? AttendanceWindowOffsetDays { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static PayrollDefinition Create(
        string code,
        string name,
        string payrollTypeCode,
        string payPeriodCode,
        int totalPeriods,
        bool guaranteesMinimumIncome,
        string currencyCode,
        bool overtimeWindowEnabled,
        int? overtimeWindowOffsetDays,
        bool attendanceWindowEnabled,
        int? attendanceWindowOffsetDays) =>
        new(
            Guid.NewGuid(),
            code,
            name,
            payrollTypeCode,
            payPeriodCode,
            totalPeriods,
            guaranteesMinimumIncome,
            currencyCode,
            overtimeWindowEnabled,
            overtimeWindowOffsetDays,
            attendanceWindowEnabled,
            attendanceWindowOffsetDays);

    public void Update(
        string code,
        string name,
        string payrollTypeCode,
        string payPeriodCode,
        int totalPeriods,
        bool guaranteesMinimumIncome,
        string currencyCode,
        bool overtimeWindowEnabled,
        int? overtimeWindowOffsetDays,
        bool attendanceWindowEnabled,
        int? attendanceWindowOffsetDays)
    {
        SetCode(code);
        SetName(name);
        SetPayrollTypeCode(payrollTypeCode);
        SetPayPeriodCode(payPeriodCode);
        SetTotalPeriods(totalPeriods);
        GuaranteesMinimumIncome = guaranteesMinimumIncome;
        SetCurrencyCode(currencyCode);
        SetOvertimeWindow(overtimeWindowEnabled, overtimeWindowOffsetDays);
        SetAttendanceWindow(attendanceWindowEnabled, attendanceWindowOffsetDays);
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

    private void SetCode(string code)
    {
        Code = PayrollNormalization.NormalizeCode(code);
        if (Code.Length > MaxCodeLength)
        {
            throw new ArgumentException($"Code must be {MaxCodeLength} characters or fewer.", nameof(code));
        }

        NormalizedCode = Code;
    }

    private void SetName(string name)
    {
        Name = PayrollNormalization.Clean(name, nameof(name));
        if (Name.Length > MaxNameLength)
        {
            throw new ArgumentException($"Name must be {MaxNameLength} characters or fewer.", nameof(name));
        }

        NormalizedName = PayrollNormalization.NormalizeName(Name);
    }

    private void SetPayrollTypeCode(string payrollTypeCode)
    {
        PayrollTypeCode = PayrollNormalization.NormalizeCode(payrollTypeCode);
        if (PayrollTypeCode.Length > MaxPayrollTypeCodeLength)
        {
            throw new ArgumentException(
                $"Payroll type code must be {MaxPayrollTypeCodeLength} characters or fewer.",
                nameof(payrollTypeCode));
        }
    }

    private void SetPayPeriodCode(string payPeriodCode)
    {
        PayPeriodCode = PayrollNormalization.NormalizeCode(payPeriodCode);
        if (PayPeriodCode.Length > MaxPayPeriodCodeLength)
        {
            throw new ArgumentException(
                $"Pay period code must be {MaxPayPeriodCodeLength} characters or fewer.",
                nameof(payPeriodCode));
        }
    }

    private void SetTotalPeriods(int totalPeriods)
    {
        if (totalPeriods < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(totalPeriods), "Total periods must be at least 1.");
        }

        TotalPeriods = totalPeriods;
    }

    private void SetCurrencyCode(string currencyCode)
    {
        CurrencyCode = PayrollNormalization.NormalizeCode(currencyCode);
        if (CurrencyCode.Length != CurrencyCodeLength)
        {
            throw new ArgumentException(
                $"Currency code must be exactly {CurrencyCodeLength} characters.",
                nameof(currencyCode));
        }
    }

    private void SetOvertimeWindow(bool enabled, int? offsetDays)
    {
        if (!enabled && offsetDays.HasValue)
        {
            throw new ArgumentException(
                "Overtime window offset requires the overtime window to be enabled.",
                nameof(offsetDays));
        }

        OvertimeWindowEnabled = enabled;
        OvertimeWindowOffsetDays = offsetDays;
    }

    private void SetAttendanceWindow(bool enabled, int? offsetDays)
    {
        if (!enabled && offsetDays.HasValue)
        {
            throw new ArgumentException(
                "Attendance window offset requires the attendance window to be enabled.",
                nameof(offsetDays));
        }

        AttendanceWindowEnabled = enabled;
        AttendanceWindowOffsetDays = offsetDays;
    }

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}

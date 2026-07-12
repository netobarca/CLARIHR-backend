using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Leave;

/// <summary>
/// One type of "tiempo no trabajado" of the company (REQ-011 D-18): an unpaid absence, a paid one, a suspension with
/// discount, a late arrival… It carries the ten fields the levantamiento asked for, and the three counting flags are
/// deliberately the SAME ones as <see cref="IncapacityRisk"/> — the day-scan is the same scan, and two modules that
/// count the same calendar differently would be a bug waiting to happen.
///
/// <para>The one thing with no precedent is <see cref="CountsSeventhDayPenalty"/>: the incapacity engine EXCLUDES or
/// INCLUDES the rest day, but never ADDS one. Here an affected week costs the employee their paid day of rest too.</para>
/// </summary>
public sealed class NotWorkedTimeType : TenantEntity
{
    public const int MaxCodeLength = 50;
    public const int MaxNameLength = 150;
    public const int MaxConceptCodeLength = 80;

    private NotWorkedTimeType()
    {
    }

    private NotWorkedTimeType(
        string code,
        string name,
        bool appliesToPermission,
        bool usesWorkSchedule,
        bool countsHoliday,
        bool countsSaturday,
        bool countsRestDay,
        bool countsSeventhDayPenalty,
        decimal discountPercent,
        string? deductionConceptTypeCode,
        string? incomeConceptTypeCode)
    {
        PublicId = Guid.NewGuid();
        SetCode(code);
        SetName(name);
        ApplyRules(
            appliesToPermission,
            usesWorkSchedule,
            countsHoliday,
            countsSaturday,
            countsRestDay,
            countsSeventhDayPenalty,
            discountPercent,
            deductionConceptTypeCode,
            incomeConceptTypeCode);
        IsActive = true;
        ConcurrencyToken = Guid.NewGuid();
    }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    /// <summary>Classification only (P-17): it marks the types the FUTURE permission-request module will offer. No
    /// behaviour hangs off it today, and none should be invented.</summary>
    public bool AppliesToPermission { get; private set; }

    /// <summary>The absence is captured in HOURS (a late arrival), not in whole days. The hours are valued with the
    /// company's standard daily hours — plazas carry no real workday (verified: <c>WorkdayCode</c> is a free code).</summary>
    public bool UsesWorkSchedule { get; private set; }

    // The three scan flags — the SAME ones as IncapacityRisk, on purpose.
    public bool CountsHoliday { get; private set; }

    public bool CountsSaturday { get; private set; }

    public bool CountsRestDay { get; private set; }

    /// <summary>THE new rule (P-18): each affected week costs one extra full day — the paid day of rest the employee
    /// forfeits. The incapacity engine has no equivalent.</summary>
    public bool CountsSeventhDayPenalty { get; private set; }

    /// <summary>0 = "con goce" (the absence is recorded, the money is not touched) … 100 = fully unpaid.</summary>
    public decimal DiscountPercent { get; private set; }

    /// <summary>The Egreso concept the discount is imputed to. Mandatory when <see cref="DiscountPercent"/> &gt; 0 —
    /// a discount with nowhere to land would silently vanish from the payroll input.</summary>
    public string? DeductionConceptTypeCode { get; private set; }

    /// <summary>Optional Ingreso concept, for the partially-paid types.</summary>
    public string? IncomeConceptTypeCode { get; private set; }

    public bool IsActive { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static NotWorkedTimeType Create(
        string code,
        string name,
        bool appliesToPermission,
        bool usesWorkSchedule,
        bool countsHoliday,
        bool countsSaturday,
        bool countsRestDay,
        bool countsSeventhDayPenalty,
        decimal discountPercent,
        string? deductionConceptTypeCode,
        string? incomeConceptTypeCode) =>
        new(
            code,
            name,
            appliesToPermission,
            usesWorkSchedule,
            countsHoliday,
            countsSaturday,
            countsRestDay,
            countsSeventhDayPenalty,
            discountPercent,
            deductionConceptTypeCode,
            incomeConceptTypeCode);

    public void Update(
        string code,
        string name,
        bool appliesToPermission,
        bool usesWorkSchedule,
        bool countsHoliday,
        bool countsSaturday,
        bool countsRestDay,
        bool countsSeventhDayPenalty,
        decimal discountPercent,
        string? deductionConceptTypeCode,
        string? incomeConceptTypeCode)
    {
        SetCode(code);
        SetName(name);
        ApplyRules(
            appliesToPermission,
            usesWorkSchedule,
            countsHoliday,
            countsSaturday,
            countsRestDay,
            countsSeventhDayPenalty,
            discountPercent,
            deductionConceptTypeCode,
            incomeConceptTypeCode);
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

    private void ApplyRules(
        bool appliesToPermission,
        bool usesWorkSchedule,
        bool countsHoliday,
        bool countsSaturday,
        bool countsRestDay,
        bool countsSeventhDayPenalty,
        decimal discountPercent,
        string? deductionConceptTypeCode,
        string? incomeConceptTypeCode)
    {
        if (discountPercent is < 0m or > 100m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(discountPercent),
                "The discount percent must be between 0 and 100.");
        }

        var deduction = NormalizeOptionalCode(deductionConceptTypeCode);

        // A type that discounts must say WHERE the discount lands, or the money would never reach the payroll input.
        if (discountPercent > 0m && deduction is null)
        {
            throw new ArgumentException(
                "A type with a discount percent greater than 0 must carry a deduction concept.",
                nameof(deductionConceptTypeCode));
        }

        AppliesToPermission = appliesToPermission;
        UsesWorkSchedule = usesWorkSchedule;
        CountsHoliday = countsHoliday;
        CountsSaturday = countsSaturday;
        CountsRestDay = countsRestDay;
        CountsSeventhDayPenalty = countsSeventhDayPenalty;
        DiscountPercent = discountPercent;
        DeductionConceptTypeCode = deduction;
        IncomeConceptTypeCode = NormalizeOptionalCode(incomeConceptTypeCode);
    }

    private void SetCode(string code)
    {
        var cleaned = LeaveNormalization.Clean(code, nameof(code));
        if (cleaned.Length > MaxCodeLength)
        {
            throw new ArgumentException($"Code cannot exceed {MaxCodeLength} characters.", nameof(code));
        }

        Code = cleaned;
        NormalizedCode = LeaveNormalization.NormalizeCode(cleaned);
    }

    private void SetName(string name)
    {
        var cleaned = LeaveNormalization.Clean(name, nameof(name));
        if (cleaned.Length > MaxNameLength)
        {
            throw new ArgumentException($"Name cannot exceed {MaxNameLength} characters.", nameof(name));
        }

        Name = cleaned;
        NormalizedName = LeaveNormalization.NormalizeName(cleaned);
    }

    private static string? NormalizeOptionalCode(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : LeaveNormalization.NormalizeCode(code.Trim());

    private void RefreshConcurrencyToken() => ConcurrencyToken = Guid.NewGuid();
}

using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Preferences;

public sealed class CompanyPreference : TenantEntity
{
    private CompanyPreference()
    {
    }

    private CompanyPreference(
        Guid publicId,
        string currencyCode,
        string timeZone)
    {
        PublicId = publicId;
        CurrencyCode = PreferenceNormalization.NormalizeCurrencyCode(currencyCode);
        TimeZone = PreferenceNormalization.NormalizeTimeZone(timeZone);
        ConcurrencyToken = Guid.NewGuid();
    }

    public string CurrencyCode { get; private set; } = "USD";

    public string TimeZone { get; private set; } = "UTC";

    // HR analytics dashboard parametrization. Both nullable: when unset, the HR-ratio indicator reports
    // "not configured" (D-06) and the "expediente actualizado" rule falls back to the default threshold (D-08).
    public string? HrFunctionalAreaCode { get; private set; }

    public int? FileUpToDateThresholdMonths { get; private set; }

    // Economic-aid eligibility (D-08): minimum seniority (months) required for an employee to request economic
    // aid. Nullable — when unset or 0 there is no seniority restriction (baseline behavior).
    public int? MinimumSeniorityMonthsForEconomicAid { get; private set; }

    // Vacation & incapacity parametrization (D-20/D-24/D-26/D-27). All nullable: null means "use the
    // legal default", which is resolved when the policy is CONSUMED — the default is never stored here.

    /// <summary>
    /// Annual vacation days granted per cycle. Null = legal default of 15 days (Art. 177 CT).
    /// </summary>
    public int? AnnualVacationDaysDefault { get; private set; }

    /// <summary>
    /// Extra vacation days granted by the company on top of the legal entitlement. Null = legal default of 0.
    /// </summary>
    public int? AdditionalVacationBenefitDaysDefault { get; private set; }

    /// <summary>
    /// Whether a vacation period may start on a holiday. Null = legal default of false (Art. 178 CT).
    /// </summary>
    public bool? AllowVacationStartOnHoliday { get; private set; }

    /// <summary>
    /// Whether a vacation period may end on a holiday. Null = legal default of true.
    /// </summary>
    public bool? AllowVacationEndOnHoliday { get; private set; }

    /// <summary>
    /// Whether a vacation period may start on the employee's weekly rest day. Null = legal default of
    /// false (Art. 178 CT).
    /// </summary>
    public bool? AllowVacationStartOnRestDay { get; private set; }

    /// <summary>
    /// Whether new vacation funds default to the employee's anniversary cycle. Null = legal default of true.
    /// </summary>
    public bool? DefaultUseAnniversary { get; private set; }

    /// <summary>
    /// Company-wide weekly rest day, 0-6 with Sunday = 0. Null = legal default of 0 (Sunday).
    /// </summary>
    public int? CompanyRestDayOfWeek { get; private set; }

    /// <summary>
    /// Incapacity days per year covered by the employer. Null = legal default of 9 (D-27).
    /// </summary>
    public int? EmployerCoveredIncapacityDaysPerYear { get; private set; }

    /// <summary>
    /// Extra incapacity days per year granted by the company on top of the employer-covered days.
    /// Null = legal default of 0.
    /// </summary>
    public int? AdditionalIncapacityBenefitDaysPerYear { get; private set; }

    /// <summary>
    /// Whether registering an incapacity requires the supporting medical document (constancia).
    /// Null = legal default of true (D-22).
    /// </summary>
    public bool? IncapacityRequiresDocument { get; private set; }

    public Guid ConcurrencyToken { get; private set; }

    public static CompanyPreference Create(string currencyCode, string timeZone) =>
        new(Guid.NewGuid(), currencyCode, timeZone);

    public static CompanyPreference CreateDefault() => Create("USD", "UTC");

    public void Update(string currencyCode, string timeZone)
    {
        CurrencyCode = PreferenceNormalization.NormalizeCurrencyCode(currencyCode);
        TimeZone = PreferenceNormalization.NormalizeTimeZone(timeZone);
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Sets the HR-dashboard parametrization (D-06/D-08). <paramref name="hrFunctionalAreaCode"/> is the
    /// <c>FunctionalArea</c> code that identifies the HR area (normalized upper; null clears it);
    /// <paramref name="fileUpToDateThresholdMonths"/> is the "expediente actualizado" window in months
    /// (null falls back to the default in the dashboard rules).
    /// </summary>
    public void SetDashboardSettings(string? hrFunctionalAreaCode, int? fileUpToDateThresholdMonths)
    {
        if (fileUpToDateThresholdMonths is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fileUpToDateThresholdMonths), "Threshold months must be greater than zero when provided.");
        }

        HrFunctionalAreaCode = string.IsNullOrWhiteSpace(hrFunctionalAreaCode)
            ? null
            : hrFunctionalAreaCode.Trim().ToUpperInvariant();
        FileUpToDateThresholdMonths = fileUpToDateThresholdMonths;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Sets the economic-aid eligibility threshold (D-08): the minimum seniority in months an employee must have
    /// to request economic aid. Pass null to clear it (no restriction); when provided it must be positive.
    /// </summary>
    public void SetEconomicAidEligibility(int? minimumSeniorityMonthsForEconomicAid)
    {
        if (minimumSeniorityMonthsForEconomicAid is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumSeniorityMonthsForEconomicAid), "Minimum seniority months must be greater than zero when provided.");
        }

        MinimumSeniorityMonthsForEconomicAid = minimumSeniorityMonthsForEconomicAid;
        ConcurrencyToken = Guid.NewGuid();
    }

    /// <summary>
    /// Sets the vacation and incapacity parametrization (D-20/D-24/D-26/D-27). Every parameter is
    /// nullable: pass null to fall back to the legal default, which is resolved when the policy is
    /// consumed (it is never stored). Day counts must be within 0-365 and
    /// <paramref name="companyRestDayOfWeek"/> within 0 (Sunday) to 6 (Saturday) when provided.
    /// </summary>
    public void SetLeavePolicies(
        int? annualVacationDaysDefault,
        int? additionalVacationBenefitDaysDefault,
        bool? allowVacationStartOnHoliday,
        bool? allowVacationEndOnHoliday,
        bool? allowVacationStartOnRestDay,
        bool? defaultUseAnniversary,
        int? companyRestDayOfWeek,
        int? employerCoveredIncapacityDaysPerYear,
        int? additionalIncapacityBenefitDaysPerYear,
        bool? incapacityRequiresDocument)
    {
        if (annualVacationDaysDefault is < 0 or > 365)
        {
            throw new ArgumentOutOfRangeException(nameof(annualVacationDaysDefault), "Annual vacation days must be between 0 and 365 when provided.");
        }

        if (additionalVacationBenefitDaysDefault is < 0 or > 365)
        {
            throw new ArgumentOutOfRangeException(nameof(additionalVacationBenefitDaysDefault), "Additional vacation benefit days must be between 0 and 365 when provided.");
        }

        if (companyRestDayOfWeek is < 0 or > 6)
        {
            throw new ArgumentOutOfRangeException(nameof(companyRestDayOfWeek), "Company rest day of week must be between 0 (Sunday) and 6 (Saturday) when provided.");
        }

        if (employerCoveredIncapacityDaysPerYear is < 0 or > 365)
        {
            throw new ArgumentOutOfRangeException(nameof(employerCoveredIncapacityDaysPerYear), "Employer-covered incapacity days per year must be between 0 and 365 when provided.");
        }

        if (additionalIncapacityBenefitDaysPerYear is < 0 or > 365)
        {
            throw new ArgumentOutOfRangeException(nameof(additionalIncapacityBenefitDaysPerYear), "Additional incapacity benefit days per year must be between 0 and 365 when provided.");
        }

        AnnualVacationDaysDefault = annualVacationDaysDefault;
        AdditionalVacationBenefitDaysDefault = additionalVacationBenefitDaysDefault;
        AllowVacationStartOnHoliday = allowVacationStartOnHoliday;
        AllowVacationEndOnHoliday = allowVacationEndOnHoliday;
        AllowVacationStartOnRestDay = allowVacationStartOnRestDay;
        DefaultUseAnniversary = defaultUseAnniversary;
        CompanyRestDayOfWeek = companyRestDayOfWeek;
        EmployerCoveredIncapacityDaysPerYear = employerCoveredIncapacityDaysPerYear;
        AdditionalIncapacityBenefitDaysPerYear = additionalIncapacityBenefitDaysPerYear;
        IncapacityRequiresDocument = incapacityRequiresDocument;
        ConcurrencyToken = Guid.NewGuid();
    }
}

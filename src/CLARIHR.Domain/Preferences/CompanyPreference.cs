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
}

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
}

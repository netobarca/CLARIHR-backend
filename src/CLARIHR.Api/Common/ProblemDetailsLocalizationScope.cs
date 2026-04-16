using System.Globalization;
using System.Security.Claims;

namespace CLARIHR.Api.Common;

internal static class ProblemDetailsLocalizationScope
{
    private const string LocaleClaimType = "locale";

    public static IDisposable UseFrom(HttpContext httpContext)
    {
        var locale = httpContext.User.FindFirstValue(LocaleClaimType);
        if (string.IsNullOrWhiteSpace(locale))
        {
            return NoopDisposable.Instance;
        }

        CultureInfo? culture;
        try
        {
            culture = CultureInfo.GetCultureInfo(locale);
        }
        catch (CultureNotFoundException)
        {
            return NoopDisposable.Instance;
        }

        return new CultureScope(culture);
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _originalCulture = CultureInfo.CurrentCulture;
        private readonly CultureInfo _originalUiCulture = CultureInfo.CurrentUICulture;

        public CultureScope(CultureInfo culture)
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = culture;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _originalCulture;
            CultureInfo.CurrentUICulture = _originalUiCulture;
        }
    }

    private sealed class NoopDisposable : IDisposable
    {
        public static readonly NoopDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}

using System.Globalization;
using System.Security.Claims;
using CLARIHR.Infrastructure.Localization;

namespace CLARIHR.Api.Common;

internal static class ProblemDetailsLocalizationScope
{
    public static IDisposable UseFrom(HttpContext httpContext)
    {
        var preferredLanguage = httpContext.User.FindFirstValue(RequestLanguageResolver.LanguageClaimType);
        var acceptLanguageHeader = httpContext.Request.Headers.AcceptLanguage.ToString();
        var culture = RequestLanguageResolver.ResolveCulture(preferredLanguage, acceptLanguageHeader);
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

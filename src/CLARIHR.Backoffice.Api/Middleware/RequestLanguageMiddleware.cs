using System.Globalization;
using System.Security.Claims;
using CLARIHR.Infrastructure.Localization;

namespace CLARIHR.Backoffice.Api.Middleware;

internal sealed class RequestLanguageMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        var preferredLanguage = context.User.FindFirstValue(RequestLanguageResolver.LanguageClaimType);
        var acceptLanguageHeader = context.Request.Headers.AcceptLanguage.ToString();
        var culture = RequestLanguageResolver.ResolveCulture(preferredLanguage, acceptLanguageHeader);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        try
        {
            await next(context);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }
}

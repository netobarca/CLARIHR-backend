using System.Globalization;
using System.Security.Claims;
using CLARIHR.Application.Abstractions.Tenancy;

namespace CLARIHR.Api.Middleware;

internal sealed class RequestLocaleMiddleware(RequestDelegate next)
{
    private const string LocaleClaimType = "locale";
    private const string TenantClaimType = "tid";
    private const string FallbackLocale = "es-SV";

    public async Task InvokeAsync(HttpContext context, ITenantLocaleResolver tenantLocaleResolver)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        var locale = context.User.FindFirstValue(LocaleClaimType);
        if (string.IsNullOrWhiteSpace(locale))
        {
            locale = await ResolveTenantLocaleAsync(context, tenantLocaleResolver);
        }

        var culture = TryResolveCulture(locale) ?? CultureInfo.GetCultureInfo(FallbackLocale);
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

    private static async Task<string?> ResolveTenantLocaleAsync(
        HttpContext context,
        ITenantLocaleResolver tenantLocaleResolver)
    {
        var tenantClaim = context.User.FindFirstValue(TenantClaimType);
        if (!Guid.TryParse(tenantClaim, out var tenantId))
        {
            return null;
        }

        return await tenantLocaleResolver.ResolveDefaultLocaleAsync(tenantId, context.RequestAborted);
    }

    private static CultureInfo? TryResolveCulture(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            return null;
        }

        try
        {
            return CultureInfo.GetCultureInfo(locale);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }
}

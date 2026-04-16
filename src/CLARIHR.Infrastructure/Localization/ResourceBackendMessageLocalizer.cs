using System.Globalization;
using System.Resources;
using CLARIHR.Application.Abstractions.Localization;

namespace CLARIHR.Infrastructure.Localization;

internal sealed class ResourceBackendMessageLocalizer : IBackendMessageLocalizer
{
    private static readonly ResourceManager ResourceManager = new(
        "CLARIHR.Infrastructure.Localization.BackendMessages",
        typeof(ResourceBackendMessageLocalizer).Assembly);

    public string Localize(
        string key,
        string fallback,
        IReadOnlyList<object?>? arguments = null)
    {
        var template = string.IsNullOrWhiteSpace(key)
            ? fallback
            : ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? fallback;

        if (arguments is null || arguments.Count == 0)
        {
            return template;
        }

        try
        {
            return string.Format(CultureInfo.CurrentUICulture, template, arguments.ToArray());
        }
        catch (FormatException)
        {
            return fallback;
        }
    }
}

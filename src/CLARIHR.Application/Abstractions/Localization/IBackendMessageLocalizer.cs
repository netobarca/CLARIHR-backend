namespace CLARIHR.Application.Abstractions.Localization;

public interface IBackendMessageLocalizer
{
    string Localize(
        string key,
        string fallback,
        IReadOnlyList<object?>? arguments = null);
}

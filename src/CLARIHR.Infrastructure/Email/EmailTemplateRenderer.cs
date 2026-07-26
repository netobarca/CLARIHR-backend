using System.Net;
using System.Text.RegularExpressions;
using CLARIHR.Application.Abstractions.Email;

namespace CLARIHR.Infrastructure.Email;

/// <summary>
/// Substitutes <c>{{PLACEHOLDER}}</c> values and lifts the subject out of the template's
/// <c>&lt;title&gt;</c>. Provider-agnostic: the result is an <see cref="EmailMessage"/> that any
/// transport can send.
/// </summary>
internal sealed partial class EmailTemplateRenderer(IEmailTemplateSource templateSource) : IEmailTemplateRenderer
{
    public async Task<EmailMessage> RenderAsync(
        EmailTemplateKey key,
        EmailAddress to,
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken)
    {
        var template = await templateSource.GetAsync(key, cancellationToken);

        // HTML-encode in the HTML body, raw in the plain-text body. A user whose surname is
        // `<script>` must not be able to inject markup into a mail we send on their behalf.
        var html = Substitute(template.Html, values, WebUtility.HtmlEncode);
        var text = Substitute(template.Text, values, static value => value);

        var (subject, body) = ExtractSubject(html, key);

        return new EmailMessage(to, subject, body, text.Trim());
    }

    private static string Substitute(
        string template,
        IReadOnlyDictionary<string, string> values,
        Func<string, string> escape) =>
        PlaceholderPattern().Replace(template, match =>
        {
            var name = match.Groups["name"].Value;
            return values.TryGetValue(name, out var value)
                // An unknown placeholder is left verbatim rather than blanked: a mail that visibly
                // says {{FOO}} gets reported, one silently missing a link does not.
                ? escape(value ?? string.Empty)
                : match.Value;
        });

    private static (string Subject, string Body) ExtractSubject(string html, EmailTemplateKey key)
    {
        var match = TitlePattern().Match(html);
        if (!match.Success)
        {
            throw new InvalidOperationException(
                $"E-mail template '{key}' has no <title> element. The renderer takes the subject line " +
                $"from it so a single file owns the whole message.");
        }

        var subject = WebUtility.HtmlDecode(match.Groups["subject"].Value).Trim();
        if (subject.Length == 0)
        {
            throw new InvalidOperationException($"E-mail template '{key}' has an empty <title> (subject).");
        }

        // Drop the <title> from the body: it is metadata, and some clients would render it as text.
        return (subject, html.Remove(match.Index, match.Length).Trim());
    }

    [GeneratedRegex(@"\{\{(?<name>[A-Z0-9_]+)\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderPattern();

    [GeneratedRegex(@"<title>(?<subject>.*?)</title>\s*", RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex TitlePattern();
}

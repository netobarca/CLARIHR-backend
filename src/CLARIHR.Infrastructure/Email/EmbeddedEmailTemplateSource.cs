using System.Collections.Concurrent;
using System.Reflection;
using CLARIHR.Application.Abstractions.Email;

namespace CLARIHR.Infrastructure.Email;

/// <summary>
/// Reads templates from files embedded in this assembly (<c>Email/Templates/{Key}.html|.txt</c>).
///
/// <para>Chosen over the provider's own template editor so the copy is versioned, reviewable in a PR,
/// and — the point of the exercise — independent of who delivers it: changing provider never means
/// re-creating four templates in someone else's console.</para>
///
/// <para>The day templates need to be edited without deploying, or customised per tenant, that is a
/// second implementation of <see cref="IEmailTemplateSource"/> (database-backed) and nothing else in
/// the stack moves.</para>
/// </summary>
internal sealed class EmbeddedEmailTemplateSource : IEmailTemplateSource
{
    private const string ResourcePrefix = "CLARIHR.Infrastructure.Email.Templates.";

    private static readonly Assembly Assembly = typeof(EmbeddedEmailTemplateSource).Assembly;
    private static readonly ConcurrentDictionary<EmailTemplateKey, EmailTemplateContent> Cache = new();

    public Task<EmailTemplateContent> GetAsync(EmailTemplateKey key, CancellationToken cancellationToken) =>
        Task.FromResult(Cache.GetOrAdd(key, static resolvedKey => new EmailTemplateContent(
            ReadResource($"{ResourcePrefix}{resolvedKey}.html"),
            ReadResource($"{ResourcePrefix}{resolvedKey}.txt"))));

    private static string ReadResource(string resourceName)
    {
        using var stream = Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"E-mail template resource '{resourceName}' was not found. Templates live in " +
                $"src/CLARIHR.Infrastructure/Email/Templates and must be embedded (see the .csproj " +
                $"EmbeddedResource item); adding a new EmailTemplateKey requires adding both files.");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}

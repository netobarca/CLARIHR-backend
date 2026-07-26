using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CLARIHR.Application.Abstractions.Email;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CLARIHR.Infrastructure.Email.Providers.Brevo;

/// <summary>
/// Brevo transport (<c>POST /v3/smtp/email</c>). **The entire provider-specific surface of the
/// system**: its wire format, its auth header, its retry semantics. Nothing above it knows Brevo
/// exists — templates, subjects and copy are ours, so a second provider is one more class beside
/// this one, not a second content pipeline.
///
/// <para>Sends rendered HTML rather than a Brevo <c>templateId</c> on purpose: template ids are
/// per-account and per-environment, and keeping the content here means switching provider never
/// means re-creating the templates in another console.</para>
/// </summary>
internal sealed class BrevoEmailSender(
    HttpClient httpClient,
    IOptions<BrevoOptions> options,
    ILogger<BrevoEmailSender> logger) : IEmailSender
{
    private const string SendPath = "v3/smtp/email";

    public string Provider => EmailProviders.Brevo;

    public async Task<EmailDeliveryReceipt> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var payload = new BrevoSendRequest(
            new BrevoSender(settings.SenderName, settings.SenderEmail),
            [new BrevoRecipient(message.To.Email, string.IsNullOrWhiteSpace(message.To.Name) ? null : message.To.Name)],
            message.Subject,
            message.HtmlBody,
            message.TextBody);

        var maxAttempts = settings.NormalizedMaxRetries + 1;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var response = await httpClient.PostAsJsonAsync(SendPath, payload, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadFromJsonAsync<BrevoSendResponse>(cancellationToken);
                    return new EmailDeliveryReceipt(Provider, body?.MessageId);
                }

                var detail = await ReadErrorAsync(response, cancellationToken);

                // 4xx means the payload is rejected (unverified sender, malformed address): retrying
                // reproduces it exactly. 429 is the exception — the payload is fine, the account is
                // over quota this second — so it IS worth retrying.
                if (response.StatusCode != HttpStatusCode.TooManyRequests &&
                    (int)response.StatusCode is >= 400 and < 500)
                {
                    throw new EmailDeliveryException(
                        $"Brevo rejected the message with {(int)response.StatusCode} {response.StatusCode}: {detail}");
                }

                if (attempt >= maxAttempts)
                {
                    throw new EmailDeliveryException(
                        $"Brevo failed after {attempt} attempt(s) with {(int)response.StatusCode} {response.StatusCode}: {detail}");
                }

                logger.LogWarning(
                    "BrevoSendRetry attempt {Attempt}/{MaxAttempts} status {StatusCode}",
                    attempt,
                    maxAttempts,
                    (int)response.StatusCode);
            }
            catch (Exception exception) when (
                exception is HttpRequestException or TaskCanceledException &&
                !cancellationToken.IsCancellationRequested)
            {
                if (attempt >= maxAttempts)
                {
                    throw new EmailDeliveryException($"Brevo was unreachable after {attempt} attempt(s).", exception);
                }

                logger.LogWarning(
                    exception,
                    "BrevoSendRetry attempt {Attempt}/{MaxAttempts} transport failure",
                    attempt,
                    maxAttempts);
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200 * Math.Pow(2, attempt - 1)), cancellationToken);
        }
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            return content.Length > 500 ? content[..500] : content;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return "<unreadable response body>";
        }
    }

    private sealed record BrevoSendRequest(
        [property: JsonPropertyName("sender")] BrevoSender Sender,
        [property: JsonPropertyName("to")] IReadOnlyList<BrevoRecipient> To,
        [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("htmlContent")] string HtmlContent,
        [property: JsonPropertyName("textContent")] string TextContent);

    private sealed record BrevoSender(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("email")] string Email);

    private sealed record BrevoRecipient(
        [property: JsonPropertyName("email")] string Email,
        [property: JsonPropertyName("name")] string? Name);

    private sealed record BrevoSendResponse(
        [property: JsonPropertyName("messageId")] string? MessageId);
}

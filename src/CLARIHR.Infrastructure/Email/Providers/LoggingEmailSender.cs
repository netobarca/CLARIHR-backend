using CLARIHR.Application.Abstractions.Email;
using Microsoft.Extensions.Logging;

namespace CLARIHR.Infrastructure.Email.Providers;

/// <summary>
/// Development transport: records that a mail would have been sent, without sending it, so a fresh
/// clone and CI cannot mail real people.
///
/// <para>The action link is **never** written in usable form — only the destination with its token
/// masked, which is enough to catch the common misconfiguration (a link still pointing at
/// <c>localhost</c>) and useless to anyone reading the logs. The link is a single-use credential and
/// the e-mail is its only channel; to actually receive one, configure a real transport.</para>
/// </summary>
internal sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public string Provider => EmailProviders.Logging;

    public Task<EmailDeliveryReceipt> SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "EmailQueued to {Email} subject {Subject} bodyChars {BodyLength} links {Links}",
            message.To.Email,
            message.Subject,
            message.HtmlBody.Length,
            string.Join(" | ", SecretPreview.MaskLinksIn(message.TextBody)));

        return Task.FromResult(new EmailDeliveryReceipt(Provider, ProviderMessageId: null));
    }
}

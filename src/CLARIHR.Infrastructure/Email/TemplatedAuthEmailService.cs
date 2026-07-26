using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Email;
using Microsoft.Extensions.Logging;

namespace CLARIHR.Infrastructure.Email;

/// <summary>
/// The two account-security mails (password reset, e-mail verification). Provider-agnostic, like
/// <see cref="TemplatedEmailService"/>.
///
/// <para>Delivery failures are logged and swallowed: <c>password-reset/request</c> must answer 200
/// whatever happens, because answering differently when the address exists would turn it into a
/// user-enumeration oracle. The token is already persisted, so the user can ask again after the
/// cooldown.</para>
/// </summary>
internal sealed class TemplatedAuthEmailService(
    IEmailTemplateRenderer renderer,
    IEmailSender sender,
    ILogger<TemplatedAuthEmailService> logger) : IAuthEmailService
{
    public Task SendPasswordResetAsync(PasswordResetEmailMessage message, CancellationToken cancellationToken) =>
        SendAsync(
            EmailTemplateKey.PasswordReset,
            message.ToEmail,
            message.FirstName,
            message.LastName,
            message.ResetLink,
            message.ExpiresAtUtc,
            cancellationToken);

    public Task SendEmailVerificationAsync(EmailVerificationEmailMessage message, CancellationToken cancellationToken) =>
        SendAsync(
            EmailTemplateKey.EmailVerification,
            message.ToEmail,
            message.FirstName,
            message.LastName,
            message.VerificationLink,
            message.ExpiresAtUtc,
            cancellationToken);

    private async Task SendAsync(
        EmailTemplateKey key,
        string toEmail,
        string firstName,
        string lastName,
        string link,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            var rendered = await renderer.RenderAsync(
                key,
                new EmailAddress(toEmail, $"{firstName} {lastName}".Trim()),
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["FIRSTNAME"] = firstName,
                    ["LASTNAME"] = lastName,
                    ["ACTIONURL"] = link,
                    ["EXPIRESUTC"] = expiresAtUtc.ToString("u"),
                },
                cancellationToken);

            var receipt = await sender.SendAsync(rendered, cancellationToken);

            logger.LogInformation(
                "AuthEmailSent template {Template} email {Email} provider {Provider} messageId {MessageId}",
                key,
                toEmail,
                receipt.Provider,
                receipt.ProviderMessageId);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "AuthEmailDeliveryFailed template {Template} email {Email}. The token is committed; " +
                "the user can request a new one after the cooldown.",
                key,
                toEmail);
        }
    }
}

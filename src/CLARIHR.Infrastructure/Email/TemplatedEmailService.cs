using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.Email;
using Microsoft.Extensions.Logging;

namespace CLARIHR.Infrastructure.Email;

/// <summary>
/// Company-user invitations. **One implementation for every provider**: it renders our template and
/// hands the result to whichever <see cref="IEmailSender"/> is configured.
///
/// <para>This is what the earlier design got wrong — the mapping of "invitation" to its content lived
/// inside the Brevo class, so a second provider would have had to copy it. Content is a property of
/// the message, not of the transport.</para>
/// </summary>
internal sealed class TemplatedEmailService(
    IEmailTemplateRenderer renderer,
    IEmailSender sender,
    IInvitationLinkBuilder invitationLinkBuilder,
    ILogger<TemplatedEmailService> logger) : IEmailService
{
    public async Task SendCompanyUserInvitationAsync(
        CompanyUserInvitationEmailMessage message,
        CancellationToken cancellationToken)
    {
        var key = message.Kind == CompanyUserInvitationEmailKind.ResetInvitation
            ? EmailTemplateKey.ResetInvitation
            : EmailTemplateKey.Invitation;

        var rendered = await renderer.RenderAsync(
            key,
            new EmailAddress(message.Email, $"{message.FirstName} {message.LastName}".Trim()),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["FIRSTNAME"] = message.FirstName,
                ["LASTNAME"] = message.LastName,
                ["COMPANYNAME"] = message.CompanyName,
                ["ACTIONURL"] = invitationLinkBuilder.Build(message.Token),
                ["EXPIRESUTC"] = message.ExpiresUtc.ToString("u"),
            },
            cancellationToken);

        var receipt = await sender.SendAsync(rendered, cancellationToken);

        logger.LogInformation(
            "CompanyUserInvitationSent email {Email} kind {Kind} template {Template} " +
            "provider {Provider} messageId {MessageId}",
            message.Email,
            message.Kind,
            key,
            receipt.Provider,
            receipt.ProviderMessageId);
    }
}

using CLARIHR.Application.Abstractions.Companies;
using Microsoft.Extensions.Logging;

namespace CLARIHR.Infrastructure.Companies;

internal sealed class LoggingEmailService(ILogger<LoggingEmailService> logger) : IEmailService
{
    public Task SendCompanyUserInvitationAsync(CompanyUserInvitationEmailMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "CompanyUserInvitationQueued email {Email} kind {Kind} expiresUtc {ExpiresUtc} tokenPreview {TokenPreview}",
            message.Email,
            message.Kind,
            message.ExpiresUtc,
            CreatePreview(message.Token));

        return Task.CompletedTask;
    }

    private static string CreatePreview(string token) =>
        token.Length <= 8
            ? "****"
            : $"{token[..4]}...{token[^4..]}";
}

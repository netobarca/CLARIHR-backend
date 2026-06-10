using CLARIHR.Application.Abstractions.Auth;
using Microsoft.Extensions.Logging;

namespace CLARIHR.Infrastructure.Auth;

internal sealed class LoggingAuthEmailService(ILogger<LoggingAuthEmailService> logger) : IAuthEmailService
{
    public Task SendPasswordResetAsync(PasswordResetEmailMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Password reset email queued for {Email}. ExpiresAtUtc {ExpiresAtUtc}. ResetLink {ResetLink}",
            message.ToEmail,
            message.ExpiresAtUtc,
            message.ResetLink);

        return Task.CompletedTask;
    }

    public Task SendEmailVerificationAsync(EmailVerificationEmailMessage message, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Email verification email queued for {Email}. ExpiresAtUtc {ExpiresAtUtc}. VerificationLink {VerificationLink}",
            message.ToEmail,
            message.ExpiresAtUtc,
            message.VerificationLink);

        return Task.CompletedTask;
    }
}

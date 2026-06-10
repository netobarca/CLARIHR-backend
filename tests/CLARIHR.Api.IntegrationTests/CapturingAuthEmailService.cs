using System.Collections.Concurrent;
using CLARIHR.Application.Abstractions.Auth;

namespace CLARIHR.Api.IntegrationTests;

// Test double for IAuthEmailService that captures the (otherwise log-only) verification / reset emails so
// integration tests can redeem the real single-use token, exercising the AU-1 register -> verify flow
// end-to-end. Registered as a singleton on the factory so captures survive across requests.
internal sealed class CapturingAuthEmailService : IAuthEmailService
{
    private readonly ConcurrentQueue<EmailVerificationEmailMessage> _verifications = new();
    private readonly ConcurrentQueue<PasswordResetEmailMessage> _passwordResets = new();

    public Task SendPasswordResetAsync(PasswordResetEmailMessage message, CancellationToken cancellationToken)
    {
        _passwordResets.Enqueue(message);
        return Task.CompletedTask;
    }

    public Task SendEmailVerificationAsync(EmailVerificationEmailMessage message, CancellationToken cancellationToken)
    {
        _verifications.Enqueue(message);
        return Task.CompletedTask;
    }

    public string? LatestVerificationTokenFor(string email)
    {
        var message = _verifications
            .Where(candidate => string.Equals(candidate.ToEmail, email, StringComparison.OrdinalIgnoreCase))
            .LastOrDefault();

        return message is null ? null : ExtractToken(message.VerificationLink);
    }

    private static string? ExtractToken(string link)
    {
        var query = new Uri(link).Query.TrimStart('?');
        var tokenPair = query
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(pair => pair.StartsWith("token=", StringComparison.Ordinal));

        return tokenPair is null ? null : Uri.UnescapeDataString(tokenPair["token=".Length..]);
    }
}

using CLARIHR.Domain.Auth;

namespace CLARIHR.Application.Abstractions.Auth;

public sealed record EmailVerificationTokenResolution(
    EmailVerificationToken Token,
    Guid UserPublicId,
    string Email,
    string FirstName,
    string LastName);

public interface IEmailVerificationTokenRepository
{
    void Add(EmailVerificationToken token);

    Task<EmailVerificationTokenResolution?> GetActiveByHashAsync(
        string tokenHash,
        DateTime utcNow,
        CancellationToken cancellationToken);

    Task RevokeActiveTokensAsync(long userId, DateTime revokedUtc, CancellationToken cancellationToken);

    Task<bool> HasRecentRequestAsync(long userId, DateTime sinceUtc, CancellationToken cancellationToken);
}

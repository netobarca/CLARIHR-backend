using CLARIHR.Domain.Auth;

namespace CLARIHR.Application.Abstractions.Auth;

public sealed record PasswordResetTokenResolution(
    PasswordResetToken Token,
    Guid UserPublicId,
    string Email,
    string FirstName,
    string LastName);

public interface IPasswordResetTokenRepository
{
    void Add(PasswordResetToken token);

    Task<PasswordResetTokenResolution?> GetActiveByHashAsync(
        string tokenHash,
        DateTime utcNow,
        CancellationToken cancellationToken);

    Task RevokeActiveTokensAsync(long userId, DateTime revokedUtc, CancellationToken cancellationToken);

    Task<bool> HasRecentRequestAsync(long userId, DateTime sinceUtc, CancellationToken cancellationToken);
}

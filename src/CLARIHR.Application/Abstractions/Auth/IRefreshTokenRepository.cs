using CLARIHR.Domain.Auth;

namespace CLARIHR.Application.Abstractions.Auth;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken);

    Task RevokeFamilyAsync(Guid familyId, DateTime revokedUtc, string reason, CancellationToken cancellationToken);

    Task RevokeUserTokensAsync(
        long userId,
        AuthClientType clientType,
        DateTime revokedUtc,
        string reason,
        CancellationToken cancellationToken);

    /// <summary>
    /// AC-1: bulk variant of <see cref="RevokeUserTokensAsync"/> — revokes every active token of the given
    /// client type for a set of users in a single query (used when archiving a company revokes its members).
    /// The default is a no-op (test fakes that do not exercise archive revocation); the EF repository revokes.
    /// </summary>
    Task RevokeUsersTokensAsync(
        IReadOnlyCollection<long> userIds,
        AuthClientType clientType,
        DateTime revokedUtc,
        string reason,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

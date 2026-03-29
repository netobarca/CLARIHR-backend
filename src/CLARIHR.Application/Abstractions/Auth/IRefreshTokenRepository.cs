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

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

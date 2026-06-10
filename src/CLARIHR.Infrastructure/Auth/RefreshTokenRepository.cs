using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Domain.Auth;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Auth;

internal sealed class RefreshTokenRepository(ApplicationDbContext dbContext) : IRefreshTokenRepository
{
    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        dbContext.RefreshTokens
            .SingleOrDefaultAsync(refreshToken => refreshToken.TokenHash == tokenHash, cancellationToken);

    public async Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
    {
        _ = await dbContext.RefreshTokens.AddAsync(refreshToken, cancellationToken);
    }

    public async Task RevokeFamilyAsync(Guid familyId, DateTime revokedUtc, string reason, CancellationToken cancellationToken)
    {
        var activeTokens = await dbContext.RefreshTokens
            .Where(refreshToken => refreshToken.FamilyId == familyId && refreshToken.RevokedUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var refreshToken in activeTokens)
        {
            refreshToken.Revoke(revokedUtc, reason);
        }
    }

    public async Task RevokeUserTokensAsync(
        long userId,
        AuthClientType clientType,
        DateTime revokedUtc,
        string reason,
        CancellationToken cancellationToken)
    {
        var activeTokens = await dbContext.RefreshTokens
            .Where(refreshToken =>
                refreshToken.UserId == userId &&
                refreshToken.ClientType == clientType &&
                refreshToken.RevokedUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var refreshToken in activeTokens)
        {
            refreshToken.Revoke(revokedUtc, reason);
        }
    }

    public async Task RevokeUsersTokensAsync(
        IReadOnlyCollection<long> userIds,
        AuthClientType clientType,
        DateTime revokedUtc,
        string reason,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
        {
            return;
        }

        var activeTokens = await dbContext.RefreshTokens
            .Where(refreshToken =>
                userIds.Contains(refreshToken.UserId) &&
                refreshToken.ClientType == clientType &&
                refreshToken.RevokedUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var refreshToken in activeTokens)
        {
            refreshToken.Revoke(revokedUtc, reason);
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Domain.Auth;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Auth;

internal sealed class PasswordResetTokenRepository(ApplicationDbContext dbContext) : IPasswordResetTokenRepository
{
    public void Add(PasswordResetToken token) => dbContext.PasswordResetTokens.Add(token);

    public Task<PasswordResetTokenResolution?> GetActiveByHashAsync(
        string tokenHash,
        DateTime utcNow,
        CancellationToken cancellationToken) =>
        dbContext.PasswordResetTokens
            .Where(token =>
                token.TokenHash == tokenHash &&
                !token.IsUsed &&
                token.RevokedUtc == null &&
                token.ExpirationUtc > utcNow)
            .Join(
                dbContext.AuthUsers,
                token => token.UserId,
                user => user.Id,
                (token, user) => new PasswordResetTokenResolution(
                    token,
                    user.PublicId,
                    user.Email,
                    user.FirstName,
                    user.LastName))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task RevokeActiveTokensAsync(long userId, DateTime revokedUtc, CancellationToken cancellationToken)
    {
        var activeTokens = await dbContext.PasswordResetTokens
            .Where(token =>
                token.UserId == userId &&
                !token.IsUsed &&
                token.RevokedUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var activeToken in activeTokens)
        {
            activeToken.Revoke(revokedUtc);
        }
    }

    public Task<bool> HasRecentRequestAsync(long userId, DateTime sinceUtc, CancellationToken cancellationToken) =>
        dbContext.PasswordResetTokens
            .AsNoTracking()
            .AnyAsync(token => token.UserId == userId && token.CreatedUtc >= sinceUtc, cancellationToken);
}

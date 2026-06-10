using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Domain.Auth;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Auth;

internal sealed class EmailVerificationTokenRepository(ApplicationDbContext dbContext) : IEmailVerificationTokenRepository
{
    public void Add(EmailVerificationToken token) => dbContext.EmailVerificationTokens.Add(token);

    public Task<EmailVerificationTokenResolution?> GetActiveByHashAsync(
        string tokenHash,
        DateTime utcNow,
        CancellationToken cancellationToken) =>
        dbContext.EmailVerificationTokens
            .Where(token =>
                token.TokenHash == tokenHash &&
                !token.IsUsed &&
                token.RevokedUtc == null &&
                token.ExpirationUtc > utcNow)
            .Join(
                dbContext.AuthUsers,
                token => token.UserId,
                user => user.Id,
                (token, user) => new EmailVerificationTokenResolution(
                    token,
                    user.PublicId,
                    user.Email,
                    user.FirstName,
                    user.LastName))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task RevokeActiveTokensAsync(long userId, DateTime revokedUtc, CancellationToken cancellationToken)
    {
        var activeTokens = await dbContext.EmailVerificationTokens
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
        dbContext.EmailVerificationTokens
            .AsNoTracking()
            .AnyAsync(token => token.UserId == userId && token.CreatedUtc >= sinceUtc, cancellationToken);
}

using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Domain.Companies;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.Companies;

internal sealed class InvitationTokenRepository(ApplicationDbContext dbContext) : IInvitationTokenRepository
{
    public void Add(InvitationToken invitationToken) => dbContext.InvitationTokens.Add(invitationToken);

    public Task<InvitationTokenResolution?> GetActiveByHashAsync(
        string tokenHash,
        DateTime utcNow,
        CancellationToken cancellationToken) =>
        dbContext.InvitationTokens
            .Where(invitationToken =>
                invitationToken.TokenHash == tokenHash &&
                !invitationToken.IsUsed &&
                invitationToken.RevokedUtc == null &&
                invitationToken.ExpirationUtc > utcNow)
            .Join(
                dbContext.Companies,
                invitationToken => invitationToken.CompanyId,
                company => company.Id,
                (invitationToken, company) => new InvitationTokenResolution(invitationToken, company.PublicId))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task RevokeActiveTokensAsync(long userId, long companyId, DateTime revokedUtc, CancellationToken cancellationToken)
    {
        var activeTokens = await dbContext.InvitationTokens
            .Where(invitationToken =>
                invitationToken.UserId == userId &&
                invitationToken.CompanyId == companyId &&
                !invitationToken.IsUsed &&
                invitationToken.RevokedUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var invitationToken in activeTokens)
        {
            invitationToken.Revoke(revokedUtc);
        }
    }
}

using CLARIHR.Domain.Companies;

namespace CLARIHR.Application.Abstractions.Companies;

public sealed record InvitationTokenResolution(
    InvitationToken Token,
    Guid CompanyPublicId);

public interface IInvitationTokenRepository
{
    void Add(InvitationToken invitationToken);

    Task<InvitationTokenResolution?> GetActiveByHashAsync(
        string tokenHash,
        DateTime utcNow,
        CancellationToken cancellationToken);

    Task RevokeActiveTokensAsync(long userId, long companyId, DateTime revokedUtc, CancellationToken cancellationToken);
}

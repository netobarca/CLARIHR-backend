using CLARIHR.Domain.Companies;

namespace CLARIHR.Application.Abstractions.Companies;

public interface IInvitationTokenRepository
{
    void Add(InvitationToken invitationToken);

    Task RevokeActiveTokensAsync(long userId, long companyId, DateTime revokedUtc, CancellationToken cancellationToken);
}

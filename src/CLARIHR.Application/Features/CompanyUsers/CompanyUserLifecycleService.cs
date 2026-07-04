using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Domain.Auth;

namespace CLARIHR.Application.Features.CompanyUsers;

/// <summary>
/// Reusable CORE of the company-user deactivation/reactivation lifecycle (the same moves
/// <c>DeactivateCompanyUserCommandHandler</c> / <c>ReactivateCompanyUserCommandHandler</c> perform: user +
/// membership status, IAM user sync, refresh-token revocation) WITHOUT authorization, ETag, audit or
/// SaveChanges — the caller owns the unit of work/transaction. Built for orchestrated flows that must
/// deactivate/reactivate a login INSIDE their own transaction (retirement execution/reversal, D-06/D-11);
/// the interactive IAM endpoints keep their own handlers untouched.
/// </summary>
public interface ICompanyUserLifecycleService
{
    /// <summary>Whether the login is currently active; <c>null</c> when the user does not exist.</summary>
    Task<bool?> GetLoginIsActiveAsync(Guid userPublicId, CancellationToken cancellationToken);

    /// <summary>Same invariant the IAM deactivation enforces: never leave the company without an active admin.</summary>
    Task<bool> IsLastActiveAdministratorAsync(Guid companyId, Guid userPublicId, CancellationToken cancellationToken);

    /// <summary>
    /// Deactivates user + membership + IAM user and revokes refresh tokens. Returns <c>false</c> when the
    /// user or its membership in the company does not exist (nothing mutated). No SaveChanges.
    /// </summary>
    Task<bool> DeactivateCoreAsync(Guid companyId, Guid userPublicId, string revocationReason, CancellationToken cancellationToken);

    /// <summary>
    /// Reactivates user + membership and re-syncs the IAM user active flag. Returns <c>false</c> when the
    /// user or its membership does not exist. No SaveChanges.
    /// </summary>
    Task<bool> ReactivateCoreAsync(Guid companyId, Guid userPublicId, CancellationToken cancellationToken);
}

internal sealed class CompanyUserLifecycleService(
    IUserRepository userRepository,
    IUserCompanyRepository userCompanyRepository,
    IIamAdministrationRepository iamRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IDateTimeProvider dateTimeProvider) : ICompanyUserLifecycleService
{
    public async Task<bool?> GetLoginIsActiveAsync(Guid userPublicId, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByPublicIdAsync(userPublicId, cancellationToken);
        return user is null ? null : user.Status == UserStatus.Active;
    }

    public Task<bool> IsLastActiveAdministratorAsync(Guid companyId, Guid userPublicId, CancellationToken cancellationToken) =>
        userCompanyRepository.IsLastActiveAdministratorAsync(companyId, userPublicId, cancellationToken);

    public async Task<bool> DeactivateCoreAsync(Guid companyId, Guid userPublicId, string revocationReason, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByPublicIdAsync(userPublicId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        var membership = await userCompanyRepository.FindByUserPublicIdAsync(companyId, userPublicId, cancellationToken);
        if (membership is null)
        {
            return false;
        }

        user.Deactivate();
        membership.Deactivate();

        var iamUser = await iamRepository.FindUserByTenantAndLinkedUserPublicIdAsync(
            companyId, user.PublicId, includeRoles: false, cancellationToken);
        iamUser?.SetActive(false);

        await refreshTokenRepository.RevokeUserTokensAsync(
            user.Id, AuthClientType.Core, dateTimeProvider.UtcNow, revocationReason, cancellationToken);

        return true;
    }

    public async Task<bool> ReactivateCoreAsync(Guid companyId, Guid userPublicId, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByPublicIdAsync(userPublicId, cancellationToken);
        if (user is null)
        {
            return false;
        }

        var membership = await userCompanyRepository.FindByUserPublicIdAsync(companyId, userPublicId, cancellationToken);
        if (membership is null)
        {
            return false;
        }

        user.Reactivate();
        membership.Reactivate();

        var iamUser = await iamRepository.FindUserByTenantAndLinkedUserPublicIdAsync(
            companyId, user.PublicId, includeRoles: false, cancellationToken);
        iamUser?.SetActive(user.Status == UserStatus.Active);

        return true;
    }
}

using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.CompanyUsers;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.Auth;

namespace CLARIHR.Application.Abstractions.Companies;

public interface IUserCompanyRepository
{
    void Add(UserCompanyMembership membership);

    Task<bool> ExistsInCompanyAsync(Guid companyPublicId, string normalizedEmail, CancellationToken cancellationToken);

    Task<bool> HasAnyMembershipAsync(long userId, CancellationToken cancellationToken);

    Task<bool> HasPrimaryCompanyAsync(long userId, CancellationToken cancellationToken);

    Task<Guid?> GetPrimaryCompanyPublicIdAsync(long userId, CancellationToken cancellationToken);

    Task<UserCompanyMembership?> GetPrimaryMembershipAsync(long userId, CancellationToken cancellationToken);

    Task<UserCompanyMembership?> GetMembershipAsync(long userId, Guid companyPublicId, CancellationToken cancellationToken);

    /// <summary>
    /// Batch variant of <see cref="GetMembershipAsync"/> for a set of user ids within one company.
    /// The default falls back to per-user lookups; the EF repository overrides it with a single
    /// batched query (used to de-N+1 the position-slot role cascade).
    /// </summary>
    async Task<IReadOnlyList<UserCompanyMembership>> GetMembershipsAsync(
        IReadOnlyCollection<long> userIds,
        Guid companyPublicId,
        CancellationToken cancellationToken)
    {
        var memberships = new List<UserCompanyMembership>(userIds.Count);
        foreach (var userId in userIds)
        {
            var membership = await GetMembershipAsync(userId, companyPublicId, cancellationToken);
            if (membership is not null)
            {
                memberships.Add(membership);
            }
        }

        return memberships;
    }

    Task<string?> GetRoleNormalizedNameAsync(long userId, Guid companyPublicId, CancellationToken cancellationToken);

    Task<UserCompanyMembership?> FindByUserPublicIdAsync(Guid companyPublicId, Guid userPublicId, CancellationToken cancellationToken);

    Task<bool> UserExistsOutsideCompanyAsync(Guid companyPublicId, Guid userPublicId, CancellationToken cancellationToken);

    Task<bool> HasActiveMembershipAsync(long userId, Guid companyPublicId, CancellationToken cancellationToken);

    Task<bool> HasAnyActiveAdministratorAsync(Guid companyPublicId, CancellationToken cancellationToken);

    Task SetPrimaryCompanyAsync(long userId, Guid companyPublicId, CancellationToken cancellationToken);

    Task<bool> IsLastActiveAdministratorAsync(Guid companyPublicId, Guid userPublicId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Guid>> GetActiveAdministratorUserIdsAsync(Guid companyPublicId, CancellationToken cancellationToken);

    Task<PagedResponse<CompanyUserSummaryResponse>> GetUsersAsync(
        Guid companyPublicId,
        int pageNumber,
        int pageSize,
        UserStatus? status,
        Guid? roleId,
        string? search,
        CancellationToken cancellationToken);

    Task<CompanyUserResponse?> GetUserAsync(Guid companyPublicId, Guid userPublicId, CancellationToken cancellationToken);
}

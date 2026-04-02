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

    Task<string?> GetRoleNormalizedNameAsync(long userId, Guid companyPublicId, CancellationToken cancellationToken);

    Task<UserCompanyMembership?> FindByUserPublicIdAsync(Guid companyPublicId, Guid userPublicId, CancellationToken cancellationToken);

    Task<bool> UserExistsOutsideCompanyAsync(Guid companyPublicId, Guid userPublicId, CancellationToken cancellationToken);

    Task<bool> HasActiveMembershipAsync(long userId, Guid companyPublicId, CancellationToken cancellationToken);

    Task<bool> HasAnyActiveAdministratorAsync(Guid companyPublicId, CancellationToken cancellationToken);

    Task SetPrimaryCompanyAsync(long userId, Guid companyPublicId, CancellationToken cancellationToken);

    Task<bool> IsLastActiveAdministratorAsync(Guid companyPublicId, Guid userPublicId, CancellationToken cancellationToken);

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

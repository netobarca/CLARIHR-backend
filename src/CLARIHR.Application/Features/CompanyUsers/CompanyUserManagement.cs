using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Domain.Auth;

namespace CLARIHR.Application.Features.CompanyUsers;

public sealed record CompanyUserRoleResponse(
    Guid Id,
    string Name,
    string? Description,
    bool IsSystemRole);

public sealed record CompanyUserSummaryResponse(
    Guid Id,
    string? Email,
    string? FirstName,
    string? LastName,
    IReadOnlyCollection<CompanyUserRoleResponse> Roles,
    UserStatus? Status,
    AllowedActionsResponse? AllowedActions = null);

public sealed record CompanyUserResponse(
    Guid Id,
    string? Email,
    string? FirstName,
    string? LastName,
    IReadOnlyCollection<CompanyUserRoleResponse> Roles,
    UserStatus? Status);

public sealed record CompanyUserInvitationResponse(
    CompanyUserResponse User,
    DateTime InvitationExpiresUtc);

public sealed record GetCompanyUsersQuery(
    int Page = 1,
    int PageSize = 20,
    UserStatus? Status = null,
    Guid? RoleId = null,
    string? Search = null,
    bool IncludeAllowedActions = false) : IQuery<PagedResponse<CompanyUserSummaryResponse>>;

public sealed record CreateCompanyUserCommand(
    string Email,
    string FirstName,
    string LastName,
    IReadOnlyCollection<Guid> RoleIds) : ICommand<CompanyUserInvitationResponse>;

public sealed record UpdateCompanyUserCommand(
    Guid UserId,
    string FirstName,
    string LastName,
    IReadOnlyCollection<Guid> RoleIds) : ICommand<CompanyUserResponse>;

public sealed record DeactivateCompanyUserCommand(Guid UserId) : ICommand<CompanyUserResponse>;

public sealed record ReactivateCompanyUserCommand(Guid UserId) : ICommand<CompanyUserResponse>;

public sealed record ResetInvitationCommand(Guid UserId) : ICommand<CompanyUserInvitationResponse>;

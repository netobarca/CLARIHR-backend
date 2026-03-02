using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Domain.Auth;

namespace CLARIHR.Application.Features.CompanyUsers;

public sealed record CompanyUserSummaryResponse(
    Guid Id,
    string? Email,
    string? FirstName,
    string? LastName,
    Guid? RoleId,
    string? Role,
    UserStatus? Status);

public sealed record CompanyUserResponse(
    Guid Id,
    string? Email,
    string? FirstName,
    string? LastName,
    Guid? RoleId,
    string? Role,
    UserStatus? Status);

public sealed record CompanyUserInvitationResponse(
    CompanyUserResponse User,
    DateTime InvitationExpiresUtc);

public sealed record GetCompanyUsersQuery(
    int Page = 1,
    int PageSize = 20,
    UserStatus? Status = null,
    Guid? RoleId = null,
    string? Search = null) : IQuery<PagedResponse<CompanyUserSummaryResponse>>;

public sealed record CreateCompanyUserCommand(
    string Email,
    string FirstName,
    string LastName,
    Guid RoleId) : ICommand<CompanyUserInvitationResponse>;

public sealed record UpdateCompanyUserCommand(
    Guid UserId,
    string FirstName,
    string LastName,
    Guid RoleId) : ICommand<CompanyUserResponse>;

public sealed record DeactivateCompanyUserCommand(Guid UserId) : ICommand<CompanyUserResponse>;

public sealed record ReactivateCompanyUserCommand(Guid UserId) : ICommand<CompanyUserResponse>;

public sealed record ResetInvitationCommand(Guid UserId) : ICommand<CompanyUserInvitationResponse>;

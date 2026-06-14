using System.Text.Json.Serialization;
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
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

public sealed record CompanyUserResponse(
    Guid Id,
    string? Email,
    string? FirstName,
    string? LastName,
    IReadOnlyCollection<CompanyUserRoleResponse> Roles,
    UserStatus? Status) : ISupportsAllowedActions
{
    // Weak ETag for the resource (a hash of the unfiltered projection — see CompanyUserETag). Carried
    // out-of-band to the API layer for the `ETag` header / `If-Match` concurrency check; never serialized
    // into the response body.
    [JsonIgnore]
    public string? WeakETag { get; init; }

    public AllowedActionsResponse? AllowedActions { get; init; }
}

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
    IReadOnlyCollection<Guid> RoleIds,
    string? ExpectedETag = null) : ICommand<CompanyUserResponse>;

public sealed record DeactivateCompanyUserCommand(Guid UserId, string? ExpectedETag = null) : ICommand<CompanyUserResponse>;

public sealed record ReactivateCompanyUserCommand(Guid UserId, string? ExpectedETag = null) : ICommand<CompanyUserResponse>;

public sealed record ResetInvitationCommand(Guid UserId) : ICommand<CompanyUserInvitationResponse>;

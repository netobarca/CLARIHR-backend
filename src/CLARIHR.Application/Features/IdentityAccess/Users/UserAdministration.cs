using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.IdentityAccess.Contracts;
using CLARIHR.Application.Features.IdentityAccess.Roles;
using CLARIHR.Domain.IdentityAccess;
using FluentValidation;

namespace CLARIHR.Application.Features.IdentityAccess.Users;

public sealed record CreateIamUserCommand(
    string FirstName,
    string LastName,
    string Email,
    bool IsActive = true,
    IReadOnlyCollection<Guid>? RoleIds = null) : ICommand<IamUserResponse>;

public sealed record GetIamUserByIdQuery(Guid UserId) : IQuery<IamUserResponse>;

public sealed record ListIamUsersQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? Search = null,
    bool IncludeAllowedActions = false) : IQuery<PagedResponse<IamUserSummaryResponse>>;

public sealed record SyncIamUserRolesCommand(
    Guid UserId,
    IReadOnlyCollection<Guid> RoleIds) : ICommand<IamUserResponse>;

internal sealed class CreateIamUserCommandValidator : AbstractValidator<CreateIamUserCommand>
{
    public CreateIamUserCommandValidator()
    {
        RuleFor(command => command.FirstName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.LastName)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(320);

        RuleForEach(command => command.RoleIds)
            .NotEqual(Guid.Empty);
    }
}

internal sealed class GetIamUserByIdQueryValidator : AbstractValidator<GetIamUserByIdQuery>
{
    public GetIamUserByIdQueryValidator()
    {
        RuleFor(query => query.UserId)
            .NotEmpty();
    }
}

internal sealed class ListIamUsersQueryValidator : AbstractValidator<ListIamUsersQuery>
{
    public ListIamUsersQueryValidator()
    {
        RuleFor(query => query.PageNumber)
            .GreaterThan(0);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);

        RuleFor(query => query.Search)
            .MaximumLength(100)
            .When(static query => !string.IsNullOrWhiteSpace(query.Search));
    }
}

internal sealed class SyncIamUserRolesCommandValidator : AbstractValidator<SyncIamUserRolesCommand>
{
    public SyncIamUserRolesCommandValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty();

        RuleFor(command => command.RoleIds)
            .NotNull();

        RuleForEach(command => command.RoleIds)
            .NotEqual(Guid.Empty);
    }
}

internal sealed class CreateIamUserCommandHandler(
    IIamAdministrationRepository repository,
    IIamAdministrationAuthorizationService authorizationService,
    IAuditService auditService)
    : ICommandHandler<CreateIamUserCommand, IamUserResponse>
{
    public async Task<Result<IamUserResponse>> Handle(CreateIamUserCommand command, CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureAuthorizedAsync(
            RbacPermissionScreen.Users,
            RbacPermissionAction.Create,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IamUserResponse>.Failure(authorizationResult.Error);
        }

        if (await repository.UserEmailExistsAsync(Normalize(command.Email), cancellationToken))
        {
            return Result<IamUserResponse>.Failure(IdentityAccessErrors.UserAlreadyExists);
        }

        var roleIds = DistinctIds(command.RoleIds);
        var roles = roleIds.Count == 0
            ? Array.Empty<IamRole>()
            : await repository.GetRolesByPublicIdsAsync(roleIds, cancellationToken);

        if (roles.Count != roleIds.Count)
        {
            return Result<IamUserResponse>.Failure(IdentityAccessErrors.RolesNotFound);
        }

        var user = IamUser.Create(command.FirstName, command.LastName, command.Email, command.IsActive);
        user.SyncRoles(roles);

        repository.AddUser(user);
        await auditService.LogAsync(
            new AuditLogEntry(
                AuditEventTypes.UserCreated,
                AuditEntityTypes.User,
                user.PublicId,
                EntityKey: user.Email,
                AuditActions.Create,
                $"Created IAM user {user.Email}.",
                After: IdentityAccessAuditMapper.CreateIamUserSnapshot(user, roles)),
            cancellationToken);
        _ = await repository.SaveChangesAsync(cancellationToken);

        var createdUser = await repository.GetUserAsync(user.PublicId, cancellationToken);
        return createdUser is null
            ? Result<IamUserResponse>.Failure(IdentityAccessErrors.UserNotFound)
            : Result<IamUserResponse>.Success(createdUser);
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();

    private static IReadOnlyCollection<Guid> DistinctIds(IReadOnlyCollection<Guid>? ids) =>
        ids is null
            ? Array.Empty<Guid>()
            : ids.Distinct().ToArray();
}

internal sealed class GetIamUserByIdQueryHandler(
    IIamAdministrationRepository repository,
    IIamAdministrationAuthorizationService authorizationService,
    ITenantContext tenantContext)
    : IQueryHandler<GetIamUserByIdQuery, IamUserResponse>
{
    public async Task<Result<IamUserResponse>> Handle(GetIamUserByIdQuery query, CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureAuthorizedAsync(
            RbacPermissionScreen.Users,
            RbacPermissionAction.Read,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IamUserResponse>.Failure(authorizationResult.Error);
        }

        var user = await UserAdministrationLookups.ResolveUserResponseAsync(
            repository,
            tenantContext,
            query.UserId,
            cancellationToken);
        return user is null
            ? Result<IamUserResponse>.Failure(await UserAdministrationErrors.ResolveUserLookupErrorAsync(repository, query.UserId, RbacPermissionAction.Read, cancellationToken))
            : Result<IamUserResponse>.Success(user);
    }
}

internal sealed class ListIamUsersQueryHandler(
    IIamAdministrationRepository repository,
    IIamAdministrationAuthorizationService authorizationService,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<ListIamUsersQuery, PagedResponse<IamUserSummaryResponse>>
{
    public async Task<Result<PagedResponse<IamUserSummaryResponse>>> Handle(
        ListIamUsersQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureAuthorizedAsync(
            RbacPermissionScreen.Users,
            RbacPermissionAction.Read,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<IamUserSummaryResponse>>.Failure(authorizationResult.Error);
        }

        var users = await repository.GetUsersAsync(query.PageNumber, query.PageSize, query.Search, cancellationToken);

        if (query.IncludeAllowedActions)
        {
            var canManageUsers = (await authorizationService.EnsureAuthorizedAsync(
                RbacPermissionScreen.Users,
                RbacPermissionAction.Update,
                cancellationToken)).IsSuccess;
            var activeAdministratorUserIds = await repository.GetActiveAdministratorUserIdsAsync(cancellationToken);
            var enrichedItems = users.Items
                .Select(user => user with
                {
                    AllowedActions = resourceActionPolicyService.Evaluate(
                        new ResourceActionContext(
                            ResourceKey: RbacPermissionScreen.Users.ToString(),
                            State: user.IsActive ? "Active" : "Inactive",
                            IsActive: user.IsActive,
                            HasDependencies: user.IsActive &&
                                             activeAdministratorUserIds.Count == 1 &&
                                             activeAdministratorUserIds.Contains(user.Id),
                            SupportsEdit: true,
                            EditAllowed: canManageUsers))
                })
                .ToArray();

            users = new PagedResponse<IamUserSummaryResponse>(
                enrichedItems,
                users.PageNumber,
                users.PageSize,
                users.TotalCount);
        }

        return Result<PagedResponse<IamUserSummaryResponse>>.Success(users);
    }
}

internal sealed class SyncIamUserRolesCommandHandler(
    IIamAdministrationRepository repository,
    IIamAdministrationAuthorizationService authorizationService,
    IAuditService auditService,
    ITenantContext tenantContext)
    : ICommandHandler<SyncIamUserRolesCommand, IamUserResponse>
{
    public async Task<Result<IamUserResponse>> Handle(SyncIamUserRolesCommand command, CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureAuthorizedAsync(
            RbacPermissionScreen.Users,
            RbacPermissionAction.Update,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IamUserResponse>.Failure(authorizationResult.Error);
        }

        var user = await UserAdministrationLookups.ResolveUserEntityAsync(
            repository,
            tenantContext,
            command.UserId,
            includeRoles: true,
            cancellationToken);
        if (user is null)
        {
            return Result<IamUserResponse>.Failure(await UserAdministrationErrors.ResolveUserLookupErrorAsync(repository, command.UserId, RbacPermissionAction.Update, cancellationToken));
        }

        var roleIds = command.RoleIds.Distinct().ToArray();
        var roles = roleIds.Length == 0
            ? Array.Empty<IamRole>()
            : await repository.GetRolesByPublicIdsAsync(roleIds, cancellationToken);

        if (roles.Count != roleIds.Length)
        {
            return Result<IamUserResponse>.Failure(IdentityAccessErrors.RolesNotFound);
        }

        if (user.IsActive &&
            !roles.Any(RoleAdministrationGuards.IsAdministrativeRole) &&
            (await repository.GetActiveAdministratorUserIdsAsync(cancellationToken)).Count == 1 &&
            user.RoleAssignments
                .Select(static assignment => assignment.Role)
                .Any(RoleAdministrationGuards.IsAdministrativeRole))
        {
            return Result<IamUserResponse>.Failure(IdentityAccessErrors.LastAdministratorRequired);
        }

        var beforeRoles = user.RoleAssignments
            .Select(static assignment => assignment.Role)
            .ToArray();
        user.SyncRoles(roles);
        await auditService.LogAsync(
            new AuditLogEntry(
                AuditEventTypes.UserUpdated,
                AuditEntityTypes.User,
                user.PublicId,
                EntityKey: user.Email,
                AuditActions.Update,
                $"Updated IAM user roles for {user.Email}.",
                IdentityAccessAuditMapper.CreateIamUserSnapshot(user, beforeRoles),
                IdentityAccessAuditMapper.CreateIamUserSnapshot(user, roles),
                IdentityAccessAuditMapper.CreateIamUserRolesDiff(beforeRoles, roles)),
            cancellationToken);
        _ = await repository.SaveChangesAsync(cancellationToken);

        var updatedUser = await repository.GetUserAsync(user.PublicId, cancellationToken);
        return updatedUser is null
            ? Result<IamUserResponse>.Failure(IdentityAccessErrors.UserNotFound)
            : Result<IamUserResponse>.Success(updatedUser);
    }

}

internal static class UserAdministrationLookups
{
    public static async Task<IamUserResponse?> ResolveUserResponseAsync(
        IIamAdministrationRepository repository,
        ITenantContext tenantContext,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var user = await repository.GetUserAsync(userId, cancellationToken);
        if (user is not null || !tenantContext.TenantId.HasValue)
        {
            return user;
        }

        var linkedUser = await repository.FindUserByTenantAndLinkedUserPublicIdAsync(
            tenantContext.TenantId.Value,
            userId,
            includeRoles: false,
            cancellationToken);

        return linkedUser is null
            ? null
            : await repository.GetUserAsync(linkedUser.PublicId, cancellationToken);
    }

    public static async Task<IamUser?> ResolveUserEntityAsync(
        IIamAdministrationRepository repository,
        ITenantContext tenantContext,
        Guid userId,
        bool includeRoles,
        CancellationToken cancellationToken)
    {
        var user = await repository.FindUserByPublicIdAsync(userId, includeRoles, cancellationToken);
        if (user is not null || !tenantContext.TenantId.HasValue)
        {
            return user;
        }

        return await repository.FindUserByTenantAndLinkedUserPublicIdAsync(
            tenantContext.TenantId.Value,
            userId,
            includeRoles,
            cancellationToken);
    }
}

internal static class UserAdministrationErrors
{
    public static async Task<Error> ResolveUserLookupErrorAsync(
        IIamAdministrationRepository repository,
        Guid userId,
        RbacPermissionAction action,
        CancellationToken cancellationToken) =>
        await repository.UserPublicIdExistsAsync(userId, cancellationToken)
            ? AuthorizationErrors.TenantMismatch(PermissionMatrixCatalog.Get(RbacPermissionScreen.Users).ResourceKey, action)
            : IdentityAccessErrors.UserNotFound;
}

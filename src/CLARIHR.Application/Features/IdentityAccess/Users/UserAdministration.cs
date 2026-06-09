using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.IdentityAccess.Contracts;
using CLARIHR.Application.Features.IdentityAccess.Roles;
using CLARIHR.Domain.IdentityAccess;
using FluentValidation;

namespace CLARIHR.Application.Features.IdentityAccess.Users;

public sealed record SyncIamUserRolesCommand(
    Guid UserId,
    IReadOnlyCollection<Guid> RoleIds,
    string? ExpectedETag = null) : ICommand<IamUserResponse>;

internal sealed class SyncIamUserRolesCommandValidator : AbstractValidator<SyncIamUserRolesCommand>
{
    public SyncIamUserRolesCommandValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty();

        RuleFor(command => command.RoleIds)
            .NotNull();

        RuleFor(command => command.RoleIds)
            .Must(static roleIds => roleIds is null || roleIds.Count <= IdentityAccessValidationRules.MaxRoleIdsPerUser)
            .WithMessage("A maximum of 200 roles per user is allowed.");

        RuleForEach(command => command.RoleIds)
            .NotEqual(Guid.Empty);
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

        // Weak-ETag concurrency check (iam_users has no persisted token — see IamUserRolesETag). The API
        // layer rejects a missing If-Match with 400 before dispatch, so ExpectedETag is non-null on the
        // request path; it is null only for direct (non-HTTP) callers, which skip the check.
        var currentProjection = await repository.GetUserAsync(user.PublicId, cancellationToken);
        if (command.ExpectedETag is not null &&
            currentProjection is not null &&
            !IamUserRolesETag.Matches(command.ExpectedETag, currentProjection))
        {
            return Result<IamUserResponse>.Failure(IdentityAccessErrors.ConcurrencyConflict);
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
            : Result<IamUserResponse>.Success(updatedUser with { WeakETag = IamUserRolesETag.Compute(updatedUser) });
    }
}

internal static class UserAdministrationLookups
{
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

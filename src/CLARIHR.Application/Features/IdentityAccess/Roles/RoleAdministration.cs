using CLARIHR.Application.Abstractions.Authentication;
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
using CLARIHR.Domain.IdentityAccess;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace CLARIHR.Application.Features.IdentityAccess.Roles;

public sealed record CreateIamRoleCommand(
    string Name,
    string? Description,
    IReadOnlyCollection<Guid>? PermissionIds = null) : ICommand<IamRoleResponse>;

public sealed record GetIamRoleByIdQuery(Guid RoleId) : IQuery<IamRoleResponse>;

public sealed record ListIamRolesQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? Search = null,
    bool IncludeAllowedActions = false) : IQuery<PagedResponse<IamRoleSummaryResponse>>;

public sealed record UpdateIamRoleCommand(
    Guid RoleId,
    string Name,
    string? Description,
    Guid ConcurrencyToken) : ICommand<IamRoleResponse>;

public sealed record DeleteIamRoleCommand(Guid RoleId, Guid ConcurrencyToken) : ICommand<bool>;

public sealed record SyncIamRolePermissionsCommand(
    Guid RoleId,
    IReadOnlyCollection<Guid> PermissionIds,
    Guid ConcurrencyToken) : ICommand<IamRoleResponse>;

internal sealed class CreateIamRoleCommandValidator : AbstractValidator<CreateIamRoleCommand>
{
    public CreateIamRoleCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.Description)
            .MaximumLength(500)
            .When(static command => !string.IsNullOrWhiteSpace(command.Description));

        RuleFor(command => command.PermissionIds)
            .Must(static permissionIds => permissionIds is null || permissionIds.Count <= IdentityAccessValidationRules.MaxPermissionIdsPerRole)
            .WithMessage("A maximum of 1000 permissions per role is allowed.");

        RuleForEach(command => command.PermissionIds)
            .NotEqual(Guid.Empty);
    }
}

internal sealed class GetIamRoleByIdQueryValidator : AbstractValidator<GetIamRoleByIdQuery>
{
    public GetIamRoleByIdQueryValidator()
    {
        RuleFor(query => query.RoleId)
            .NotEmpty();
    }
}

internal sealed class UpdateIamRoleCommandValidator : AbstractValidator<UpdateIamRoleCommand>
{
    public UpdateIamRoleCommandValidator()
    {
        RuleFor(command => command.RoleId)
            .NotEmpty();

        RuleFor(command => command.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(command => command.Description)
            .MaximumLength(500)
            .When(static command => !string.IsNullOrWhiteSpace(command.Description));

        RuleFor(command => command.ConcurrencyToken)
            .NotEmpty();
    }
}

internal sealed class DeleteIamRoleCommandValidator : AbstractValidator<DeleteIamRoleCommand>
{
    public DeleteIamRoleCommandValidator()
    {
        RuleFor(command => command.RoleId)
            .NotEmpty();
    }
}

internal sealed class ListIamRolesQueryValidator : AbstractValidator<ListIamRolesQuery>
{
    public ListIamRolesQueryValidator()
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

internal sealed class SyncIamRolePermissionsCommandValidator : AbstractValidator<SyncIamRolePermissionsCommand>
{
    public SyncIamRolePermissionsCommandValidator()
    {
        RuleFor(command => command.RoleId)
            .NotEmpty();

        RuleFor(command => command.PermissionIds)
            .NotNull();

        RuleFor(command => command.PermissionIds)
            .Must(static permissionIds => permissionIds is null || permissionIds.Count <= IdentityAccessValidationRules.MaxPermissionIdsPerRole)
            .WithMessage("A maximum of 1000 permissions per role is allowed.");

        RuleForEach(command => command.PermissionIds)
            .NotEqual(Guid.Empty);

        RuleFor(command => command.ConcurrencyToken)
            .NotEmpty();
    }
}

internal sealed class CreateIamRoleCommandHandler(
    IIamAdministrationRepository repository,
    IIamAdministrationAuthorizationService authorizationService,
    IAuditService auditService)
    : ICommandHandler<CreateIamRoleCommand, IamRoleResponse>
{
    public async Task<Result<IamRoleResponse>> Handle(CreateIamRoleCommand command, CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureAuthorizedAsync(
            RbacPermissionScreen.Roles,
            RbacPermissionAction.Create,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IamRoleResponse>.Failure(authorizationResult.Error);
        }

        if (await repository.RoleNameExistsAsync(Normalize(command.Name), cancellationToken))
        {
            return Result<IamRoleResponse>.Failure(IdentityAccessErrors.RoleAlreadyExists);
        }

        var permissionIds = DistinctIds(command.PermissionIds);
        var permissions = permissionIds.Count == 0
            ? Array.Empty<IamPermission>()
            : await repository.GetPermissionsByPublicIdsAsync(permissionIds, cancellationToken);

        if (permissions.Count != permissionIds.Count)
        {
            return Result<IamRoleResponse>.Failure(IdentityAccessErrors.PermissionsNotFound);
        }

        var role = IamRole.Create(command.Name, command.Description);
        role.SyncPermissions(permissions);

        repository.AddRole(role);
        await auditService.LogAsync(
            new AuditLogEntry(
                AuditEventTypes.RoleCreated,
                AuditEntityTypes.Role,
                role.PublicId,
                EntityKey: role.Name,
                AuditActions.Create,
                $"Created role {role.Name}.",
                After: IdentityAccessAuditMapper.CreateRoleSnapshot(role, permissions)),
            cancellationToken);
        _ = await repository.SaveChangesAsync(cancellationToken);

        var createdRole = await repository.GetRoleAsync(role.PublicId, cancellationToken);
        return createdRole is null
            ? Result<IamRoleResponse>.Failure(IdentityAccessErrors.RoleNotFound)
            : Result<IamRoleResponse>.Success(createdRole);
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();

    private static IReadOnlyCollection<Guid> DistinctIds(IReadOnlyCollection<Guid>? ids) =>
        ids is null
            ? Array.Empty<Guid>()
            : ids.Distinct().ToArray();
}

internal sealed class UpdateIamRoleCommandHandler(
    IIamAdministrationRepository repository,
    IIamAdministrationAuthorizationService authorizationService,
    IAuditService auditService)
    : ICommandHandler<UpdateIamRoleCommand, IamRoleResponse>
{
    public async Task<Result<IamRoleResponse>> Handle(UpdateIamRoleCommand command, CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureAuthorizedAsync(
            RbacPermissionScreen.Roles,
            RbacPermissionAction.Update,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IamRoleResponse>.Failure(authorizationResult.Error);
        }

        var role = await repository.FindRoleByPublicIdAsync(command.RoleId, includePermissions: true, cancellationToken);
        if (role is null)
        {
            return Result<IamRoleResponse>.Failure(await RoleAdministrationErrors.ResolveRoleLookupErrorAsync(
                repository,
                command.RoleId,
                RbacPermissionAction.Update,
                cancellationToken));
        }

        if (role.IsSystemRole)
        {
            return Result<IamRoleResponse>.Failure(IdentityAccessErrors.ProtectedRoleModificationForbidden);
        }

        // Optimistic concurrency: the strong token travels in the If-Match header (validator enforces
        // NotEmpty, so a stale/blank token never silently bypasses this guard).
        if (role.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<IamRoleResponse>.Failure(IdentityAccessErrors.ConcurrencyConflict);
        }

        var normalizedName = Normalize(command.Name);
        if (!string.Equals(role.NormalizedName, normalizedName, StringComparison.Ordinal) &&
            await repository.RoleNameExistsAsync(normalizedName, cancellationToken))
        {
            return Result<IamRoleResponse>.Failure(IdentityAccessErrors.RoleAlreadyExists);
        }

        var beforeName = role.Name;
        var beforeDescription = role.Description;
        role.UpdateDetails(command.Name, command.Description);
        await auditService.LogAsync(
            new AuditLogEntry(
                AuditEventTypes.RoleUpdated,
                AuditEntityTypes.Role,
                role.PublicId,
                EntityKey: role.Name,
                AuditActions.Update,
                $"Updated role {role.Name}.",
                IdentityAccessAuditMapper.CreateRoleSnapshot(
                    role.PublicId,
                    beforeName,
                    beforeDescription,
                    role.IsSystemRole,
                    role.PermissionAssignments.Select(static assignment => assignment.Permission)),
                IdentityAccessAuditMapper.CreateRoleSnapshot(
                    role,
                    role.PermissionAssignments.Select(static assignment => assignment.Permission)),
                IdentityAccessAuditMapper.CreateRoleDiff(beforeName, role.Name, beforeDescription, role.Description)),
            cancellationToken);
        _ = await repository.SaveChangesAsync(cancellationToken);

        var updatedRole = await repository.GetRoleAsync(role.PublicId, cancellationToken);
        return updatedRole is null
            ? Result<IamRoleResponse>.Failure(IdentityAccessErrors.RoleNotFound)
            : Result<IamRoleResponse>.Success(updatedRole);
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}

internal sealed class GetIamRoleByIdQueryHandler(
    IIamAdministrationRepository repository,
    IIamAdministrationAuthorizationService authorizationService)
    : IQueryHandler<GetIamRoleByIdQuery, IamRoleResponse>
{
    public async Task<Result<IamRoleResponse>> Handle(GetIamRoleByIdQuery query, CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureAuthorizedAsync(
            RbacPermissionScreen.Roles,
            RbacPermissionAction.Read,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IamRoleResponse>.Failure(authorizationResult.Error);
        }

        var role = await repository.GetRoleAsync(query.RoleId, cancellationToken);
        return role is null
            ? Result<IamRoleResponse>.Failure(await RoleAdministrationErrors.ResolveRoleLookupErrorAsync(
                repository,
                query.RoleId,
                RbacPermissionAction.Read,
                cancellationToken))
            : Result<IamRoleResponse>.Success(role);
    }
}

internal sealed class DeleteIamRoleCommandHandler(
    IIamAdministrationRepository repository,
    IIamAdministrationAuthorizationService authorizationService,
    IAuditService auditService)
    : ICommandHandler<DeleteIamRoleCommand, bool>
{
    public async Task<Result<bool>> Handle(DeleteIamRoleCommand command, CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureAuthorizedAsync(
            RbacPermissionScreen.Roles,
            RbacPermissionAction.Delete,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<bool>.Failure(authorizationResult.Error);
        }

        // includePermissions:true so the deletion audit snapshot captures the role's grant set before
        // the cascade removes it (the delete cascades to iam_role_permission_assignments).
        var role = await repository.FindRoleByPublicIdAsync(command.RoleId, includePermissions: true, cancellationToken);
        if (role is null)
        {
            return Result<bool>.Failure(await RoleAdministrationErrors.ResolveRoleLookupErrorAsync(
                repository,
                command.RoleId,
                RbacPermissionAction.Delete,
                cancellationToken));
        }

        if (role.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<bool>.Failure(IdentityAccessErrors.ConcurrencyConflict);
        }

        if (role.IsSystemRole)
        {
            return Result<bool>.Failure(IdentityAccessErrors.ProtectedRoleDeletionForbidden);
        }

        var assignedUsers = await repository.GetUsersAssignedToRoleAsync(role.PublicId, includeRoles: false, cancellationToken);
        if (assignedUsers.Count > 0)
        {
            return Result<bool>.Failure(IdentityAccessErrors.RoleAssignedToUsers);
        }

        await auditService.LogAsync(
            new AuditLogEntry(
                AuditEventTypes.RoleDeleted,
                AuditEntityTypes.Role,
                role.PublicId,
                EntityKey: role.Name,
                AuditActions.Delete,
                $"Deleted role {role.Name}.",
                Before: IdentityAccessAuditMapper.CreateRoleSnapshot(
                    role,
                    role.PermissionAssignments.Select(static assignment => assignment.Permission))),
            cancellationToken);

        repository.RemoveRole(role);
        _ = await repository.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}

internal sealed class ListIamRolesQueryHandler(
    IIamAdministrationRepository repository,
    IIamAdministrationAuthorizationService authorizationService,
    IResourceActionPolicyService resourceActionPolicyService)
    : IQueryHandler<ListIamRolesQuery, PagedResponse<IamRoleSummaryResponse>>
{
    public async Task<Result<PagedResponse<IamRoleSummaryResponse>>> Handle(
        ListIamRolesQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureAuthorizedAsync(
            RbacPermissionScreen.Roles,
            RbacPermissionAction.Read,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PagedResponse<IamRoleSummaryResponse>>.Failure(authorizationResult.Error);
        }

        var roles = await repository.GetRolesAsync(query.PageNumber, query.PageSize, query.Search, cancellationToken);

        if (query.IncludeAllowedActions)
        {
            var canUpdateRoles = (await authorizationService.EnsureAuthorizedAsync(
                RbacPermissionScreen.Roles,
                RbacPermissionAction.Update,
                cancellationToken)).IsSuccess;
            var canDeleteRoles = (await authorizationService.EnsureAuthorizedAsync(
                RbacPermissionScreen.Roles,
                RbacPermissionAction.Delete,
                cancellationToken)).IsSuccess;
            var enrichedItems = roles.Items
                .Select(role => role with
                {
                    AllowedActions = resourceActionPolicyService.Evaluate(
                        new ResourceActionContext(
                            ResourceKey: RbacPermissionScreen.Roles.ToString(),
                            State: role.IsSystemRole ? "System" : "Custom",
                            IsActive: true,
                            IsSystem: role.IsSystemRole,
                            HasDependencies: role.UserCount > 0,
                            SupportsEdit: true,
                            EditAllowed: canUpdateRoles,
                            SupportsDelete: true,
                            DeleteAllowed: canDeleteRoles))
                })
                .ToArray();

            roles = new PagedResponse<IamRoleSummaryResponse>(
                enrichedItems,
                roles.PageNumber,
                roles.PageSize,
                roles.TotalCount);
        }

        return Result<PagedResponse<IamRoleSummaryResponse>>.Success(roles);
    }
}

internal sealed class SyncIamRolePermissionsCommandHandler(
    IIamAdministrationRepository repository,
    IIamAdministrationAuthorizationService authorizationService,
    ICurrentUserService currentUserService,
    IAuditService auditService,
    ILogger<SyncIamRolePermissionsCommandHandler> logger)
    : ICommandHandler<SyncIamRolePermissionsCommand, IamRoleResponse>
{
    public async Task<Result<IamRoleResponse>> Handle(SyncIamRolePermissionsCommand command, CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureAuthorizedAsync(
            RbacPermissionScreen.Permissions,
            RbacPermissionAction.Update,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IamRoleResponse>.Failure(authorizationResult.Error);
        }

        var role = await repository.FindRoleByPublicIdAsync(command.RoleId, includePermissions: true, cancellationToken);
        if (role is null)
        {
            return Result<IamRoleResponse>.Failure(await RoleAdministrationErrors.ResolveRoleLookupErrorAsync(
                repository,
                command.RoleId,
                RbacPermissionAction.Update,
                cancellationToken));
        }

        if (role.IsSystemRole)
        {
            return Result<IamRoleResponse>.Failure(IdentityAccessErrors.ProtectedRoleModificationForbidden);
        }

        // Optimistic concurrency: the role's strong token travels in the If-Match header. The grants
        // sync rotates the token (IamRole.SyncPermissions), so concurrent grant edits collide here.
        if (role.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<IamRoleResponse>.Failure(IdentityAccessErrors.ConcurrencyConflict);
        }

        var permissionIds = command.PermissionIds.Distinct().ToArray();
        var permissions = permissionIds.Length == 0
            ? Array.Empty<IamPermission>()
            : await repository.GetPermissionsByPublicIdsAsync(permissionIds, cancellationToken);

        if (permissions.Count != permissionIds.Length)
        {
            return Result<IamRoleResponse>.Failure(IdentityAccessErrors.PermissionsNotFound);
        }

        var actorUserId = TryParseCurrentUserId(currentUserService.UserId);
        if (!actorUserId.HasValue)
        {
            return Result<IamRoleResponse>.Failure(IdentityAccessErrors.InvalidCurrentUser);
        }

        var currentPermissions = role.PermissionAssignments
            .Select(static assignment => assignment.Permission)
            .ToArray();
        var activeUsers = await repository.GetActiveUsersAsync(includeRoles: true, cancellationToken);
        if (RoleAdministrationGuards.WouldRemoveLastRbacSecurityAdministrator(role, permissions, activeUsers))
        {
            return Result<IamRoleResponse>.Failure(IdentityAccessErrors.LastAdministratorRequired);
        }

        var beforeSnapshot = IdentityAccessAuditMapper.CreateRoleSnapshot(
            role.PublicId,
            role.Name,
            role.Description,
            role.IsSystemRole,
            currentPermissions);
        var currentPermissionCodes = role.PermissionAssignments
            .Select(static assignment => assignment.Permission.NormalizedCode)
            .ToHashSet(StringComparer.Ordinal);

        role.SyncPermissions(permissions);

        var updatedPermissionCodes = permissions
            .Select(static permission => permission.NormalizedCode)
            .ToHashSet(StringComparer.Ordinal);
        var afterSnapshot = IdentityAccessAuditMapper.CreateRoleSnapshot(
            role.PublicId,
            role.Name,
            role.Description,
            role.IsSystemRole,
            permissions);

        await auditService.LogAsync(
            new AuditLogEntry(
                AuditEventTypes.RoleResourcePermissionsUpdated,
                AuditEntityTypes.Role,
                role.PublicId,
                EntityKey: role.Name,
                AuditActions.Update,
                $"Updated grants for role {role.Name}.",
                Before: beforeSnapshot,
                After: afterSnapshot,
                Diff: CreateRolePermissionDiff(currentPermissionCodes, updatedPermissionCodes)),
            cancellationToken);

        _ = await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Authorization grants updated for role {RoleId} by {ActorUserId}. Added {AddedPermissionCodes}. Removed {RemovedPermissionCodes}",
            role.PublicId,
            currentUserService.UserId,
            updatedPermissionCodes.Except(currentPermissionCodes, StringComparer.Ordinal).ToArray(),
            currentPermissionCodes.Except(updatedPermissionCodes, StringComparer.Ordinal).ToArray());

        var updatedRole = await repository.GetRoleAsync(role.PublicId, cancellationToken);
        return updatedRole is null
            ? Result<IamRoleResponse>.Failure(IdentityAccessErrors.RoleNotFound)
            : Result<IamRoleResponse>.Success(updatedRole);
    }

    private static Guid? TryParseCurrentUserId(string? currentUserId) =>
        Guid.TryParse(currentUserId, out var actorUserId) ? actorUserId : null;

    private static IReadOnlyDictionary<string, object> CreateRolePermissionDiff(
        IReadOnlyCollection<string> beforePermissionCodes,
        IReadOnlyCollection<string> afterPermissionCodes) =>
        new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
        {
            ["permissionCodes"] = AuditPayloads.Change(
                beforePermissionCodes.OrderBy(static code => code).ToArray(),
                afterPermissionCodes.OrderBy(static code => code).ToArray())
        };
}

internal static class RoleAdministrationGuards
{
    public static bool IsAdministrativeRole(IamRole role) =>
        RbacAuthorizationEvaluator.IsRbacSecurityAdministrator(
            role.PermissionAssignments.Select(static assignment => assignment.Permission.NormalizedCode));

    public static bool WouldRemoveLastRbacSecurityAdministrator(
        IamRole role,
        IReadOnlyCollection<IamPermission> replacementPermissions,
        IReadOnlyCollection<IamUser> activeUsers)
    {
        var remainingSecurityAdministrators = activeUsers.Count(user =>
            RbacAuthorizationEvaluator.IsRbacSecurityAdministrator(
                ResolveGrantedPermissionCodes(user, role, replacementPermissions)));

        return remainingSecurityAdministrators == 0;
    }

    private static IEnumerable<string> ResolveGrantedPermissionCodes(
        IamUser user,
        IamRole role,
        IReadOnlyCollection<IamPermission> replacementPermissions)
    {
        foreach (var assignment in user.RoleAssignments)
        {
            if (assignment.Role.PublicId == role.PublicId)
            {
                foreach (var permissionCode in replacementPermissions.Select(static permission => permission.NormalizedCode))
                {
                    yield return permissionCode;
                }

                continue;
            }

            foreach (var permissionCode in assignment.Role.PermissionAssignments.Select(static permissionAssignment => permissionAssignment.Permission.NormalizedCode))
            {
                yield return permissionCode;
            }
        }
    }
}

internal static class RoleAdministrationErrors
{
    public static async Task<Error> ResolveRoleLookupErrorAsync(
        IIamAdministrationRepository repository,
        Guid roleId,
        RbacPermissionAction action,
        CancellationToken cancellationToken) =>
        await repository.RolePublicIdExistsAsync(roleId, cancellationToken)
            ? AuthorizationErrors.TenantMismatch(PermissionMatrixCatalog.Get(RbacPermissionScreen.Roles).ResourceKey, action)
            : IdentityAccessErrors.RoleNotFound;
}

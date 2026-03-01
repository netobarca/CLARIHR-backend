using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
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
    string? Search = null) : IQuery<PagedResponse<IamRoleSummaryResponse>>;

public sealed record UpdateIamRoleCommand(
    Guid RoleId,
    string Name,
    string? Description) : ICommand<IamRoleResponse>;

public sealed record CloneIamRoleCommand(
    Guid RoleId,
    string? Name = null,
    string? Description = null) : ICommand<IamRoleResponse>;

public sealed record DeleteIamRoleCommand(Guid RoleId) : ICommand<bool>;

public sealed record SyncIamRolePermissionsCommand(
    Guid RoleId,
    IReadOnlyCollection<Guid> PermissionIds) : ICommand<IamRoleResponse>;

public sealed record SyncIamRoleUsersCommand(
    Guid RoleId,
    IReadOnlyCollection<Guid> UserIds) : ICommand<IamRoleResponse>;

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
    }
}

internal sealed class CloneIamRoleCommandValidator : AbstractValidator<CloneIamRoleCommand>
{
    public CloneIamRoleCommandValidator()
    {
        RuleFor(command => command.RoleId)
            .NotEmpty();

        RuleFor(command => command.Name)
            .MaximumLength(100)
            .When(static command => !string.IsNullOrWhiteSpace(command.Name));

        RuleFor(command => command.Description)
            .MaximumLength(500)
            .When(static command => !string.IsNullOrWhiteSpace(command.Description));
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

        RuleForEach(command => command.PermissionIds)
            .NotEqual(Guid.Empty);
    }
}

internal sealed class SyncIamRoleUsersCommandValidator : AbstractValidator<SyncIamRoleUsersCommand>
{
    public SyncIamRoleUsersCommandValidator()
    {
        RuleFor(command => command.RoleId)
            .NotEmpty();

        RuleFor(command => command.UserIds)
            .NotNull();

        RuleForEach(command => command.UserIds)
            .NotEqual(Guid.Empty);
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

internal sealed class CloneIamRoleCommandHandler(
    IIamAdministrationRepository repository,
    IIamAdministrationAuthorizationService authorizationService,
    IAuditService auditService)
    : ICommandHandler<CloneIamRoleCommand, IamRoleResponse>
{
    public async Task<Result<IamRoleResponse>> Handle(CloneIamRoleCommand command, CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureAuthorizedAsync(
            RbacPermissionScreen.Roles,
            RbacPermissionAction.Create,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<IamRoleResponse>.Failure(authorizationResult.Error);
        }

        var sourceRole = await repository.FindRoleByPublicIdAsync(command.RoleId, includePermissions: true, cancellationToken);
        if (sourceRole is null)
        {
            return Result<IamRoleResponse>.Failure(await RoleAdministrationErrors.ResolveRoleLookupErrorAsync(
                repository,
                command.RoleId,
                RbacPermissionAction.Create,
                cancellationToken));
        }

        if (!string.IsNullOrWhiteSpace(command.Name) &&
            await repository.RoleNameExistsAsync(Normalize(command.Name), cancellationToken))
        {
            return Result<IamRoleResponse>.Failure(IdentityAccessErrors.RoleAlreadyExists);
        }

        var targetName = await ResolveCloneNameAsync(sourceRole.Name, command.Name, cancellationToken);
        var clone = sourceRole.Clone(targetName, command.Description);
        clone.SyncPermissions(sourceRole.PermissionAssignments.Select(static assignment => assignment.Permission));

        repository.AddRole(clone);
        await auditService.LogAsync(
            new AuditLogEntry(
                AuditEventTypes.RoleCloned,
                AuditEntityTypes.Role,
                clone.PublicId,
                EntityKey: clone.Name,
                AuditActions.Clone,
                $"Cloned role {sourceRole.Name} into {clone.Name}.",
                IdentityAccessAuditMapper.CreateRoleSnapshot(
                    sourceRole,
                    sourceRole.PermissionAssignments.Select(static assignment => assignment.Permission)),
                IdentityAccessAuditMapper.CreateRoleSnapshot(
                    clone,
                    sourceRole.PermissionAssignments.Select(static assignment => assignment.Permission)),
                new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
                {
                    ["sourceRoleId"] = AuditPayloads.Change(sourceRole.PublicId, clone.PublicId),
                    ["sourceRoleName"] = AuditPayloads.Change(sourceRole.Name, clone.Name)
                }),
            cancellationToken);
        _ = await repository.SaveChangesAsync(cancellationToken);

        var createdRole = await repository.GetRoleAsync(clone.PublicId, cancellationToken);
        return createdRole is null
            ? Result<IamRoleResponse>.Failure(IdentityAccessErrors.RoleNotFound)
            : Result<IamRoleResponse>.Success(createdRole);
    }

    private async Task<string> ResolveCloneNameAsync(string sourceName, string? requestedName, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(requestedName))
        {
            return requestedName.Trim();
        }

        var baseName = $"{sourceName.Trim()} Copy";
        if (!await repository.RoleNameExistsAsync(Normalize(baseName), cancellationToken))
        {
            return baseName;
        }

        for (var suffix = 2; suffix <= 100; suffix++)
        {
            var candidate = $"{baseName} {suffix}";
            if (!await repository.RoleNameExistsAsync(Normalize(candidate), cancellationToken))
            {
                return candidate;
            }
        }

        return $"{baseName} {Guid.NewGuid():N}"[..Math.Min(100, baseName.Length + 9)];
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
    IIamAdministrationAuthorizationService authorizationService)
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

        var role = await repository.FindRoleByPublicIdAsync(command.RoleId, includePermissions: false, cancellationToken);
        if (role is null)
        {
            return Result<bool>.Failure(await RoleAdministrationErrors.ResolveRoleLookupErrorAsync(
                repository,
                command.RoleId,
                RbacPermissionAction.Delete,
                cancellationToken));
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

        repository.RemoveRole(role);
        _ = await repository.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}

internal sealed class ListIamRolesQueryHandler(
    IIamAdministrationRepository repository,
    IIamAdministrationAuthorizationService authorizationService)
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
        return Result<PagedResponse<IamRoleSummaryResponse>>.Success(roles);
    }
}

internal sealed class SyncIamRolePermissionsCommandHandler(
    IIamAdministrationRepository repository,
    IIamAdministrationAuthorizationService authorizationService,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext,
    IDateTimeProvider dateTimeProvider,
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
        var beforeStates = RbacPermissionChangeTracker.CaptureStates(currentPermissions);
        var activeUsers = await repository.GetActiveUsersAsync(includeRoles: true, cancellationToken);
        if (RoleAdministrationGuards.WouldRemoveLastRbacSecurityAdministrator(role, permissions, activeUsers))
        {
            return Result<IamRoleResponse>.Failure(IdentityAccessErrors.LastAdministratorRequired);
        }

        var currentPermissionCodes = role.PermissionAssignments
            .Select(static assignment => assignment.Permission.NormalizedCode)
            .ToHashSet(StringComparer.Ordinal);
        var afterStates = RbacPermissionChangeTracker.CaptureStates(permissions);
        var changedScreens = PermissionMatrixCatalog.Screens
            .Where(definition => beforeStates[definition.ScreenKey] != afterStates[definition.ScreenKey])
            .Select(static definition => definition.ScreenKey)
            .ToArray();
        var auditLogs = RbacPermissionChangeTracker.CreateAuditLogs(
            role.PublicId,
            actorUserId.Value,
            dateTimeProvider.UtcNow,
            changedScreens,
            beforeStates,
            afterStates);

        role.SyncPermissions(permissions);
        foreach (var auditLog in auditLogs)
        {
            repository.AddPermissionAuditLog(auditLog);
        }

        _ = await repository.SaveChangesAsync(cancellationToken);

        var updatedPermissionCodes = permissions
            .Select(static permission => permission.NormalizedCode)
            .ToHashSet(StringComparer.Ordinal);

        logger.LogInformation(
            "RBAC role permissions updated for role {RoleId} by {ActorUserId} in tenant {TenantId}. Added {AddedPermissionCodes}. Removed {RemovedPermissionCodes}",
            role.PublicId,
            currentUserService.UserId,
            tenantContext.TenantId,
            updatedPermissionCodes.Except(currentPermissionCodes, StringComparer.Ordinal).ToArray(),
            currentPermissionCodes.Except(updatedPermissionCodes, StringComparer.Ordinal).ToArray());

        var updatedRole = await repository.GetRoleAsync(role.PublicId, cancellationToken);
        return updatedRole is null
            ? Result<IamRoleResponse>.Failure(IdentityAccessErrors.RoleNotFound)
            : Result<IamRoleResponse>.Success(updatedRole);
    }

    private static Guid? TryParseCurrentUserId(string? currentUserId) =>
        Guid.TryParse(currentUserId, out var actorUserId) ? actorUserId : null;
}

internal sealed class SyncIamRoleUsersCommandHandler(
    IIamAdministrationRepository repository,
    IIamAdministrationAuthorizationService authorizationService)
    : ICommandHandler<SyncIamRoleUsersCommand, IamRoleResponse>
{
    public async Task<Result<IamRoleResponse>> Handle(SyncIamRoleUsersCommand command, CancellationToken cancellationToken)
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

        var userIds = command.UserIds.Distinct().ToArray();
        var requestedUsers = userIds.Length == 0
            ? Array.Empty<IamUser>()
            : await repository.GetUsersByPublicIdsAsync(userIds, includeRoles: true, cancellationToken);

        if (requestedUsers.Count != userIds.Length)
        {
            return Result<IamRoleResponse>.Failure(IdentityAccessErrors.UsersNotFound);
        }

        var currentUsers = await repository.GetUsersAssignedToRoleAsync(role.PublicId, includeRoles: true, cancellationToken);
        if (RoleAdministrationGuards.WouldRemoveLastAdministrator(role, currentUsers, requestedUsers, await repository.GetActiveAdministratorUserIdsAsync(cancellationToken)))
        {
            return Result<IamRoleResponse>.Failure(IdentityAccessErrors.LastAdministratorRequired);
        }

        var requestedUserIds = requestedUsers
            .Select(static user => user.PublicId)
            .ToHashSet();

        foreach (var user in currentUsers.Where(user => !requestedUserIds.Contains(user.PublicId)))
        {
            var remainingRoles = user.RoleAssignments
                .Select(static assignment => assignment.Role)
                .Where(assignedRole => assignedRole.PublicId != role.PublicId)
                .ToArray();

            user.SyncRoles(remainingRoles);
        }

        foreach (var user in requestedUsers)
        {
            var desiredRoles = user.RoleAssignments
                .Select(static assignment => assignment.Role)
                .Concat([role])
                .GroupBy(static assignedRole => assignedRole.Id)
                .Select(static group => group.First())
                .ToArray();

            user.SyncRoles(desiredRoles);
        }

        _ = await repository.SaveChangesAsync(cancellationToken);

        var updatedRole = await repository.GetRoleAsync(role.PublicId, cancellationToken);
        return updatedRole is null
            ? Result<IamRoleResponse>.Failure(IdentityAccessErrors.RoleNotFound)
            : Result<IamRoleResponse>.Success(updatedRole);
    }
}

internal static class RoleAdministrationGuards
{
    public static bool WouldRemoveLastAdministrator(
        IamRole role,
        IReadOnlyCollection<IamUser> currentUsers,
        IReadOnlyCollection<IamUser> requestedUsers,
        IReadOnlyCollection<Guid> currentAdministratorUserIds)
    {
        if (!IsAdministrativeRole(role))
        {
            return false;
        }

        var resultingAdministratorIds = currentAdministratorUserIds.ToHashSet();
        var requestedUserIds = requestedUsers
            .Where(static user => user.IsActive)
            .Select(static user => user.PublicId)
            .ToHashSet();

        foreach (var user in currentUsers.Where(user => user.IsActive && !requestedUserIds.Contains(user.PublicId)))
        {
            var keepsAdministrativeAccess = user.RoleAssignments
                .Select(static assignment => assignment.Role)
                .Where(assignedRole => assignedRole.PublicId != role.PublicId)
                .Any(IsAdministrativeRole);

            if (!keepsAdministrativeAccess)
            {
                _ = resultingAdministratorIds.Remove(user.PublicId);
            }
        }

        foreach (var userPublicId in requestedUserIds)
        {
            _ = resultingAdministratorIds.Add(userPublicId);
        }

        return resultingAdministratorIds.Count == 0;
    }

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

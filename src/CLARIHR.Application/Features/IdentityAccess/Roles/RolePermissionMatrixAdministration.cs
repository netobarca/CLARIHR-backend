using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.IdentityAccess.Contracts;
using CLARIHR.Domain.IdentityAccess;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace CLARIHR.Application.Features.IdentityAccess.Roles;

public sealed record GetRolePermissionMatrixQuery(Guid RoleId) : IQuery<RolePermissionMatrixResponse>;

public sealed record UpdateRolePermissionMatrixCommand(
    Guid RoleId,
    IReadOnlyCollection<RolePermissionMatrixScreenUpdate> Screens) : ICommand<RolePermissionMatrixResponse>;

public sealed record RolePermissionMatrixScreenUpdate(
    string ResourceKey,
    bool Access,
    bool Read,
    bool Create,
    bool Update,
    bool Delete);

internal sealed class GetRolePermissionMatrixQueryValidator : AbstractValidator<GetRolePermissionMatrixQuery>
{
    public GetRolePermissionMatrixQueryValidator()
    {
        RuleFor(query => query.RoleId)
            .NotEmpty();
    }
}

internal sealed class UpdateRolePermissionMatrixCommandValidator : AbstractValidator<UpdateRolePermissionMatrixCommand>
{
    public UpdateRolePermissionMatrixCommandValidator()
    {
        RuleFor(command => command.RoleId)
            .NotEmpty();

        RuleFor(command => command.Screens)
            .NotNull()
            .Must(static screens => screens.Count > 0)
            .WithMessage("At least one screen update must be provided.");

        RuleForEach(command => command.Screens)
            .SetValidator(new RolePermissionMatrixScreenUpdateValidator());
    }
}

internal sealed class RolePermissionMatrixScreenUpdateValidator : AbstractValidator<RolePermissionMatrixScreenUpdate>
{
    public RolePermissionMatrixScreenUpdateValidator()
    {
        RuleFor(screen => screen.ResourceKey)
            .NotEmpty()
            .MaximumLength(100);
    }
}

internal sealed class GetRolePermissionMatrixQueryHandler(
    IIamAdministrationRepository repository,
    IIamAdministrationAuthorizationService authorizationService)
    : IQueryHandler<GetRolePermissionMatrixQuery, RolePermissionMatrixResponse>
{
    public async Task<Result<RolePermissionMatrixResponse>> Handle(
        GetRolePermissionMatrixQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureAuthorizedAsync(
            RbacPermissionScreen.Permissions,
            RbacPermissionAction.Read,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<RolePermissionMatrixResponse>.Failure(authorizationResult.Error);
        }

        var role = await repository.FindRoleByPublicIdAsync(query.RoleId, includePermissions: true, cancellationToken);
        if (role is null)
        {
            return Result<RolePermissionMatrixResponse>.Failure(await RoleAdministrationErrors.ResolveRoleLookupErrorAsync(
                repository,
                query.RoleId,
                RbacPermissionAction.Read,
                cancellationToken));
        }

        var catalogPermissions = await repository.GetPermissionsByNormalizedCodesAsync(
            PermissionMatrixCatalog.AllMatrixCodes.ToArray(),
            cancellationToken);

        return Result<RolePermissionMatrixResponse>.Success(
            RolePermissionMatrixMapper.Map(role, catalogPermissions));
    }
}

internal sealed class UpdateRolePermissionMatrixCommandHandler(
    IIamAdministrationRepository repository,
    IIamAdministrationAuthorizationService authorizationService,
    ICurrentUserService currentUserService,
    ITenantContext tenantContext,
    IDateTimeProvider dateTimeProvider,
    IAuditService auditService,
    ILogger<UpdateRolePermissionMatrixCommandHandler> logger)
    : ICommandHandler<UpdateRolePermissionMatrixCommand, RolePermissionMatrixResponse>
{
    public async Task<Result<RolePermissionMatrixResponse>> Handle(
        UpdateRolePermissionMatrixCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureAuthorizedAsync(
            RbacPermissionScreen.Permissions,
            RbacPermissionAction.Update,
            cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<RolePermissionMatrixResponse>.Failure(authorizationResult.Error);
        }

        var role = await repository.FindRoleByPublicIdAsync(command.RoleId, includePermissions: true, cancellationToken);
        if (role is null)
        {
            return Result<RolePermissionMatrixResponse>.Failure(await RoleAdministrationErrors.ResolveRoleLookupErrorAsync(
                repository,
                command.RoleId,
                RbacPermissionAction.Update,
                cancellationToken));
        }

        if (role.IsSystemRole)
        {
            return Result<RolePermissionMatrixResponse>.Failure(IdentityAccessErrors.ProtectedRoleModificationForbidden);
        }

        var parseResult = ParseScreens(command.Screens);
        if (parseResult.Error is not null)
        {
            return Result<RolePermissionMatrixResponse>.Failure(parseResult.Error);
        }

        var updatedScreens = parseResult.Value!.Keys.ToHashSet();
        var currentPermissions = role.PermissionAssignments
            .Select(static assignment => assignment.Permission)
            .ToArray();
        var beforeStates = RbacPermissionChangeTracker.CaptureStates(currentPermissions);

        var currentMatrixCodesForUpdatedScreens = currentPermissions
            .Where(permission => updatedScreens.Any(screen => PermissionMatrixCatalog.BelongsToScreen(permission.NormalizedCode, screen)))
            .Select(static permission => permission.NormalizedCode)
            .ToHashSet(StringComparer.Ordinal);

        var desiredMatrixDefinitions = BuildDesiredDefinitions(parseResult.Value);
        var existingDesiredPermissions = await repository.GetPermissionsByNormalizedCodesAsync(
            desiredMatrixDefinitions.Keys.ToArray(),
            cancellationToken);

        var desiredPermissions = existingDesiredPermissions
            .ToDictionary(static permission => permission.NormalizedCode, StringComparer.Ordinal);

        foreach (var desiredCode in desiredMatrixDefinitions.Keys.Except(desiredPermissions.Keys, StringComparer.Ordinal))
        {
            var definition = desiredMatrixDefinitions[desiredCode];
            var createdPermission = PermissionMatrixCatalog.CreatePermission(definition.Screen, definition.Action);
            repository.AddPermission(createdPermission);
            desiredPermissions[desiredCode] = createdPermission;
        }

        var preservedPermissions = currentPermissions
            .Where(permission =>
                !PermissionMatrixCatalog.IsMatrixPermissionCode(permission.NormalizedCode) ||
                !updatedScreens.Any(screen => PermissionMatrixCatalog.BelongsToScreen(permission.NormalizedCode, screen)))
            .ToArray();

        var finalPermissions = preservedPermissions
            .Concat(desiredPermissions.Values)
            .GroupBy(static permission => permission.NormalizedCode, StringComparer.Ordinal)
            .Select(static group => group.First())
            .ToArray();

        var actorUserId = TryParseCurrentUserId(currentUserService.UserId);
        if (!actorUserId.HasValue)
        {
            return Result<RolePermissionMatrixResponse>.Failure(IdentityAccessErrors.InvalidCurrentUser);
        }

        var activeUsers = await repository.GetActiveUsersAsync(includeRoles: true, cancellationToken);
        if (RoleAdministrationGuards.WouldRemoveLastRbacSecurityAdministrator(role, finalPermissions, activeUsers))
        {
            return Result<RolePermissionMatrixResponse>.Failure(IdentityAccessErrors.LastAdministratorRequired);
        }

        var afterStates = RbacPermissionChangeTracker.CaptureStates(finalPermissions);
        var auditLogs = RbacPermissionChangeTracker.CreateAuditLogs(
            role.PublicId,
            actorUserId.Value,
            dateTimeProvider.UtcNow,
            updatedScreens,
            beforeStates,
            afterStates);

        role.SyncPermissions(finalPermissions);
        foreach (var auditLog in auditLogs)
        {
            repository.AddPermissionAuditLog(auditLog);
        }

        await auditService.LogAsync(
            new AuditLogEntry(
                AuditEventTypes.RoleResourcePermissionsUpdated,
                AuditEntityTypes.Permission,
                role.PublicId,
                EntityKey: string.Join(',', updatedScreens.Select(screen => PermissionMatrixCatalog.Get(screen).ResourceKey)),
                AuditActions.PermissionChange,
                $"Updated resource permissions for role {role.Name}.",
                IdentityAccessAuditMapper.CreatePermissionMatrixSnapshot(beforeStates, updatedScreens),
                IdentityAccessAuditMapper.CreatePermissionMatrixSnapshot(afterStates, updatedScreens),
                IdentityAccessAuditMapper.CreatePermissionMatrixDiff(beforeStates, afterStates, updatedScreens)),
            cancellationToken);

        _ = await repository.SaveChangesAsync(cancellationToken);

        var addedCodes = desiredPermissions.Keys
            .Except(currentMatrixCodesForUpdatedScreens, StringComparer.Ordinal)
            .ToArray();
        var removedCodes = currentMatrixCodesForUpdatedScreens
            .Except(desiredPermissions.Keys, StringComparer.Ordinal)
            .ToArray();

        logger.LogInformation(
            "RBAC permission matrix updated for role {RoleId} by {ActorUserId} in tenant {TenantId}. Added {AddedPermissionCodes}. Removed {RemovedPermissionCodes}",
            role.PublicId,
            currentUserService.UserId,
            tenantContext.TenantId,
            addedCodes,
            removedCodes);

        var reloadedRole = await repository.FindRoleByPublicIdAsync(role.PublicId, includePermissions: true, cancellationToken);
        if (reloadedRole is null)
        {
            return Result<RolePermissionMatrixResponse>.Failure(IdentityAccessErrors.RoleNotFound);
        }

        var catalogPermissions = await repository.GetPermissionsByNormalizedCodesAsync(
            PermissionMatrixCatalog.AllMatrixCodes.ToArray(),
            cancellationToken);

        return Result<RolePermissionMatrixResponse>.Success(
            RolePermissionMatrixMapper.Map(reloadedRole, catalogPermissions));
    }

    private static Dictionary<string, (RbacPermissionScreen Screen, RbacPermissionAction Action)> BuildDesiredDefinitions(
        IReadOnlyDictionary<RbacPermissionScreen, RolePermissionMatrixScreenUpdate> screens)
    {
        var desired = new Dictionary<string, (RbacPermissionScreen Screen, RbacPermissionAction Action)>(StringComparer.Ordinal);

        foreach (var (screen, update) in screens)
        {
            if (!update.Access)
            {
                continue;
            }

            AddDesired(screen, RbacPermissionAction.Access);

            if (update.Read)
            {
                AddDesired(screen, RbacPermissionAction.Read);
            }

            if (update.Create)
            {
                AddDesired(screen, RbacPermissionAction.Create);
            }

            if (update.Update)
            {
                AddDesired(screen, RbacPermissionAction.Update);
            }

            if (update.Delete)
            {
                AddDesired(screen, RbacPermissionAction.Delete);
            }
        }

        return desired;

        void AddDesired(RbacPermissionScreen screen, RbacPermissionAction action)
        {
            desired[PermissionMatrixCatalog.BuildPermissionCode(screen, action).ToUpperInvariant()] = (screen, action);
        }
    }

    private static (IReadOnlyDictionary<RbacPermissionScreen, RolePermissionMatrixScreenUpdate>? Value, Error? Error) ParseScreens(
        IReadOnlyCollection<RolePermissionMatrixScreenUpdate> screens)
    {
        var parsed = new Dictionary<RbacPermissionScreen, RolePermissionMatrixScreenUpdate>();
        var errors = new Dictionary<string, string[]>();

        foreach (var screen in screens)
        {
            if (!PermissionMatrixCatalog.TryGet(screen.ResourceKey, out var definition))
            {
                errors[nameof(UpdateRolePermissionMatrixCommand.Screens)] = [$"Unsupported resource '{screen.ResourceKey}'."];
                continue;
            }

            if (!parsed.TryAdd(definition.ScreenKey, screen))
            {
                errors[nameof(UpdateRolePermissionMatrixCommand.Screens)] = [$"Duplicate resource '{screen.ResourceKey}'."];
                continue;
            }

            ValidateUnsupportedAction(definition, RbacPermissionAction.Delete, screen.Delete, errors);
            ValidateUnsupportedAction(definition, RbacPermissionAction.Update, screen.Update, errors);
            ValidateUnsupportedAction(definition, RbacPermissionAction.Create, screen.Create, errors);
            ValidateUnsupportedAction(definition, RbacPermissionAction.Read, screen.Read, errors);
        }

        return errors.Count > 0
            ? (null, ErrorCatalog.Validation(errors))
            : (parsed, null);
    }

    private static Guid? TryParseCurrentUserId(string? currentUserId) =>
        Guid.TryParse(currentUserId, out var actorUserId) ? actorUserId : null;

    private static void ValidateUnsupportedAction(
        PermissionMatrixScreenDefinition definition,
        RbacPermissionAction action,
        bool requested,
        IDictionary<string, string[]> errors)
    {
        if (requested && !definition.Supports(action))
        {
            errors[nameof(UpdateRolePermissionMatrixCommand.Screens)] =
                [$"Action '{action}' is not supported for screen '{definition.Screen}'."];
        }
    }
}

internal static class RolePermissionMatrixMapper
{
    public static RolePermissionMatrixResponse Map(
        IamRole role,
        IReadOnlyCollection<IamPermission> catalogPermissions)
    {
        var catalogPermissionsByCode = catalogPermissions
            .ToDictionary(static permission => permission.NormalizedCode, StringComparer.Ordinal);
        var grantedCodes = role.PermissionAssignments
            .Select(static assignment => assignment.Permission.NormalizedCode)
            .ToHashSet(StringComparer.Ordinal);

        var screens = PermissionMatrixCatalog.Screens
            .Select(definition => MapScreen(definition, grantedCodes, catalogPermissionsByCode))
            .ToArray();

        return new RolePermissionMatrixResponse(role.PublicId, role.Name, role.IsSystemRole, screens);
    }

    private static PermissionMatrixScreenResponse MapScreen(
        PermissionMatrixScreenDefinition definition,
        IReadOnlySet<string> grantedCodes,
        IReadOnlyDictionary<string, IamPermission> catalogPermissionsByCode)
    {
        var hasManageOverride = grantedCodes.Contains(IdentityPermissionCodes.ManageAdministration.ToUpperInvariant()) ||
            grantedCodes.Contains(definition.ManagePermissionCode.ToUpperInvariant());

        return new PermissionMatrixScreenResponse(
            definition.ResourceKey,
            definition.DisplayName,
            definition.Module,
            definition.Screen,
            hasManageOverride,
            BuildAction(definition, RbacPermissionAction.Access, hasManageOverride, grantedCodes, catalogPermissionsByCode),
            BuildAction(definition, RbacPermissionAction.Read, hasManageOverride, grantedCodes, catalogPermissionsByCode),
            BuildAction(definition, RbacPermissionAction.Create, hasManageOverride, grantedCodes, catalogPermissionsByCode),
            BuildAction(definition, RbacPermissionAction.Update, hasManageOverride, grantedCodes, catalogPermissionsByCode),
            BuildAction(definition, RbacPermissionAction.Delete, hasManageOverride, grantedCodes, catalogPermissionsByCode));
    }

    private static PermissionMatrixActionResponse BuildAction(
        PermissionMatrixScreenDefinition definition,
        RbacPermissionAction action,
        bool hasManageOverride,
        IReadOnlySet<string> grantedCodes,
        IReadOnlyDictionary<string, IamPermission> catalogPermissionsByCode)
    {
        if (!definition.Supports(action))
        {
            return new PermissionMatrixActionResponse(false, false, null, null);
        }

        var code = PermissionMatrixCatalog.BuildPermissionCode(definition.ScreenKey, action).ToUpperInvariant();
        catalogPermissionsByCode.TryGetValue(code, out var permission);

        return new PermissionMatrixActionResponse(
            Supported: true,
            Granted: hasManageOverride || grantedCodes.Contains(code),
            PermissionId: permission?.PublicId,
            PermissionCode: code);
    }
}

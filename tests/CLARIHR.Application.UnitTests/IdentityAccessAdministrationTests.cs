using System.Reflection;
using CLARIHR.Application;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.IdentityAccess.Contracts;
using CLARIHR.Application.Features.IdentityAccess.Permissions;
using CLARIHR.Application.Features.IdentityAccess.Rbac;
using CLARIHR.Application.Features.IdentityAccess.Roles;
using CLARIHR.Application.Features.IdentityAccess.Users;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.IdentityAccess;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Application.UnitTests;

public sealed class IdentityAccessAdministrationTests
{
    [Fact]
    public async Task CreateIamUserCommand_WhenEmailAlreadyExists_ShouldReturnConflict()
    {
        var repository = new TestIamAdministrationRepository();
        repository.AddUser(IamUser.Create("Ana", "Mendoza", "ana@clarihr.test", true));

        await using var serviceProvider = CreateServiceProvider(repository);
        var dispatcher = serviceProvider.GetRequiredService<ICommandDispatcher>();

        var result = await dispatcher.SendAsync(new CreateIamUserCommand("Ana", "Mendoza", "ana@clarihr.test"));

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityAccessErrors.UserAlreadyExists.Code, result.Error.Code);
    }

    [Fact]
    public async Task CreateIamUserCommand_WhenRoleIdsAreValid_ShouldCreateUserWithRoles()
    {
        var repository = new TestIamAdministrationRepository();
        var adminRole = IamRole.Create("Tenant Admin", "Can administer tenant access");
        repository.AddRole(adminRole);

        await using var serviceProvider = CreateServiceProvider(repository);
        var dispatcher = serviceProvider.GetRequiredService<ICommandDispatcher>();

        var result = await dispatcher.SendAsync(new CreateIamUserCommand(
            "Carla",
            "Lopez",
            "carla@clarihr.test",
            RoleIds: [adminRole.PublicId]));

        Assert.True(result.IsSuccess, $"{result.Error.Code} - {result.Error.Message}");
        Assert.Single(result.Value.Roles);
        Assert.Equal(adminRole.PublicId, result.Value.Roles.Single().Id);
    }

    [Fact]
    public async Task SyncIamUserRolesCommand_WhenRoleIsMissing_ShouldReturnNotFound()
    {
        var repository = new TestIamAdministrationRepository();
        var user = IamUser.Create("Luis", "Rivera", "luis@clarihr.test", true);
        repository.AddUser(user);

        await using var serviceProvider = CreateServiceProvider(repository);
        var dispatcher = serviceProvider.GetRequiredService<ICommandDispatcher>();

        var result = await dispatcher.SendAsync(new SyncIamUserRolesCommand(user.PublicId, [Guid.NewGuid()]));

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityAccessErrors.RolesNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task CreateIamRoleCommand_WhenNameAlreadyExists_ShouldReturnConflict()
    {
        var repository = new TestIamAdministrationRepository();
        repository.AddRole(IamRole.Create("Supervisor", "Existing role"));

        await using var serviceProvider = CreateServiceProvider(repository);
        var dispatcher = serviceProvider.GetRequiredService<ICommandDispatcher>();

        var result = await dispatcher.SendAsync(new CreateIamRoleCommand("Supervisor", "Duplicate role"));

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityAccessErrors.RoleAlreadyExists.Code, result.Error.Code);
    }

    [Fact]
    public async Task CreateIamRoleCommand_WhenRoleIsCreated_ShouldWriteAuditLog()
    {
        var repository = new TestIamAdministrationRepository();

        await using var serviceProvider = CreateServiceProvider(repository);
        var dispatcher = serviceProvider.GetRequiredService<ICommandDispatcher>();
        var auditService = Assert.IsType<TestAuditService>(serviceProvider.GetRequiredService<IAuditService>());

        var result = await dispatcher.SendAsync(new CreateIamRoleCommand("Supervisor", "Existing role"));

        Assert.True(result.IsSuccess);
        var auditEntry = Assert.Single(auditService.Entries);
        Assert.Equal(AuditEventTypes.RoleCreated, auditEntry.EventType);
        Assert.Equal(AuditEntityTypes.Role, auditEntry.EntityType);
        Assert.Equal(AuditActions.Create, auditEntry.Action);
    }

    [Fact]
    public async Task SyncIamRolePermissionsCommand_WhenPermissionIsMissing_ShouldReturnNotFound()
    {
        var repository = new TestIamAdministrationRepository();
        var role = IamRole.Create("HR Manager", "Role for HR management");
        repository.AddRole(role);

        await using var serviceProvider = CreateServiceProvider(repository);
        var dispatcher = serviceProvider.GetRequiredService<ICommandDispatcher>();

        var result = await dispatcher.SendAsync(new SyncIamRolePermissionsCommand(role.PublicId, [Guid.NewGuid()]));

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityAccessErrors.PermissionsNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task UpdateIamRoleCommand_WhenRoleIsProtected_ShouldReturnForbidden()
    {
        var repository = new TestIamAdministrationRepository();
        repository.AddRole(IamRole.Create("Admin de Empresa", "Protected role", isSystemRole: true));

        await using var serviceProvider = CreateServiceProvider(repository);
        var dispatcher = serviceProvider.GetRequiredService<ICommandDispatcher>();

        var protectedRole = repository.Roles.Single();
        var result = await dispatcher.SendAsync(
            new UpdateIamRoleCommand(protectedRole.PublicId, "Admin ajustado", protectedRole.Description));

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityAccessErrors.ProtectedRoleModificationForbidden.Code, result.Error.Code);
    }

    [Fact]
    public async Task CloneIamRoleCommand_WhenRequestedNameAlreadyExists_ShouldReturnConflict()
    {
        var repository = new TestIamAdministrationRepository();
        var exportPermission = IamPermission.CreateScreenAction(
            "employees.directory.export",
            "Export employees",
            "Allows employee export.",
            "Employees",
            "Directory",
            "Export");
        repository.AddPermission(exportPermission);

        var sourceRole = IamRole.Create("Supervisor", "Source role");
        sourceRole.SyncPermissions([exportPermission]);
        repository.AddRole(sourceRole);
        repository.AddRole(IamRole.Create("Supervisor Copy", "Existing copy"));

        await using var serviceProvider = CreateServiceProvider(repository);
        var dispatcher = serviceProvider.GetRequiredService<ICommandDispatcher>();

        var result = await dispatcher.SendAsync(
            new CloneIamRoleCommand(sourceRole.PublicId, "Supervisor Copy", null));

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityAccessErrors.RoleAlreadyExists.Code, result.Error.Code);
    }

    [Fact]
    public async Task CloneIamRoleCommand_WhenRoleExists_ShouldCopyPermissionsAndCreateEditableClone()
    {
        var repository = new TestIamAdministrationRepository();
        var readPermission = IamPermission.CreateScreenAction(
            "employees.directory.read",
            "Read employees",
            "Allows reading employees.",
            "Employees",
            "Directory",
            "Read");
        var exportPermission = IamPermission.CreateScreenAction(
            "employees.directory.export",
            "Export employees",
            "Allows employee export.",
            "Employees",
            "Directory",
            "Export");
        repository.AddPermission(readPermission);
        repository.AddPermission(exportPermission);

        var sourceRole = IamRole.Create("Supervisor", "Source role", isSystemRole: true);
        sourceRole.SyncPermissions([readPermission, exportPermission]);
        repository.AddRole(sourceRole);

        await using var serviceProvider = CreateServiceProvider(repository);
        var dispatcher = serviceProvider.GetRequiredService<ICommandDispatcher>();
        var auditService = Assert.IsType<TestAuditService>(serviceProvider.GetRequiredService<IAuditService>());

        var result = await dispatcher.SendAsync(new CloneIamRoleCommand(sourceRole.PublicId));

        Assert.True(result.IsSuccess, $"{result.Error.Code} - {result.Error.Message}");
        Assert.Equal("Supervisor Copy", result.Value.Name);
        Assert.False(result.Value.IsSystemRole);
        Assert.Equal(2, result.Value.Permissions.Count);
        var auditEntry = Assert.Single(auditService.Entries);
        Assert.Equal(AuditEventTypes.RoleCloned, auditEntry.EventType);
        Assert.Equal(AuditEntityTypes.Role, auditEntry.EntityType);
        Assert.Equal(AuditActions.Clone, auditEntry.Action);
    }

    [Fact]
    public async Task DeleteIamRoleCommand_WhenRoleIsProtected_ShouldReturnForbidden()
    {
        var repository = new TestIamAdministrationRepository();
        var role = IamRole.Create("Admin de Empresa", "Protected role", isSystemRole: true);
        repository.AddRole(role);

        await using var serviceProvider = CreateServiceProvider(repository);
        var dispatcher = serviceProvider.GetRequiredService<ICommandDispatcher>();

        var result = await dispatcher.SendAsync(new DeleteIamRoleCommand(role.PublicId));

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityAccessErrors.ProtectedRoleDeletionForbidden.Code, result.Error.Code);
    }

    [Fact]
    public async Task DeleteIamRoleCommand_WhenRoleHasAssignedUsers_ShouldReturnConflict()
    {
        var repository = new TestIamAdministrationRepository();
        var role = IamRole.Create("Analyst", "Role for analysts");
        repository.AddRole(role);

        var user = IamUser.Create("Ana", "Mendoza", "ana@clarihr.test", true);
        user.SyncRoles([role]);
        repository.AddUser(user);

        await using var serviceProvider = CreateServiceProvider(repository);
        var dispatcher = serviceProvider.GetRequiredService<ICommandDispatcher>();

        var result = await dispatcher.SendAsync(new DeleteIamRoleCommand(role.PublicId));

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityAccessErrors.RoleAssignedToUsers.Code, result.Error.Code);
    }

    [Fact]
    public async Task GetRolePermissionMatrixQuery_WhenRoleHasMatrixPermissions_ShouldReturnGrantedActions()
    {
        var repository = new TestIamAdministrationRepository();
        var accessPermission = IamPermission.CreateScreenAction(
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Users, RbacPermissionAction.Access),
            "Users Access",
            "Allows accessing users.",
            "RBAC",
            "Users",
            "Access");
        var readPermission = IamPermission.CreateScreenAction(
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Users, RbacPermissionAction.Read),
            "Users Read",
            "Allows reading users.",
            "RBAC",
            "Users",
            "Read");
        repository.AddPermission(accessPermission);
        repository.AddPermission(readPermission);

        var role = IamRole.Create("Supervisor", "Users supervisor");
        role.SyncPermissions([accessPermission, readPermission]);
        repository.AddRole(role);

        await using var serviceProvider = CreateServiceProvider(repository);
        var dispatcher = serviceProvider.GetRequiredService<IQueryDispatcher>();

        var result = await dispatcher.SendAsync(new GetRolePermissionMatrixQuery(role.PublicId));

        Assert.True(result.IsSuccess);
        var usersScreen = result.Value.Screens.Single(screen => screen.Screen == "Users");
        Assert.True(usersScreen.Access.Granted);
        Assert.True(usersScreen.Read.Granted);
        Assert.False(usersScreen.Create.Granted);
    }

    [Fact]
    public async Task UpdateRolePermissionMatrixCommand_WhenPermissionsAreMissing_ShouldCreateAndAssignThem()
    {
        var repository = new TestIamAdministrationRepository();
        SeedSecurityAdministrator(repository);
        var role = IamRole.Create("Supervisor", "Users supervisor");
        repository.AddRole(role);

        await using var serviceProvider = CreateServiceProvider(repository);
        var dispatcher = serviceProvider.GetRequiredService<ICommandDispatcher>();

        var result = await dispatcher.SendAsync(
            new UpdateRolePermissionMatrixCommand(
                role.PublicId,
                [
                    new RolePermissionMatrixScreenUpdate(
                        "Users",
                        Access: true,
                        Read: true,
                        Create: true,
                        Update: false,
                        Delete: false)
                ]));

        Assert.True(result.IsSuccess);
        var usersScreen = result.Value.Screens.Single(screen => screen.Screen == "Users");
        Assert.True(usersScreen.Access.Granted);
        Assert.True(usersScreen.Read.Granted);
        Assert.True(usersScreen.Create.Granted);
        Assert.Equal(
            3,
            repository.Permissions.Count(permission => PermissionMatrixCatalog.BelongsToScreen(permission.NormalizedCode, RbacPermissionScreen.Users)));
    }

    [Fact]
    public async Task UpdateRolePermissionMatrixCommand_WhenRemovingLastRbacSecurityAdministrator_ShouldReturnConflict()
    {
        var repository = new TestIamAdministrationRepository();
        var accessRoles = PermissionMatrixCatalog.CreatePermission(RbacPermissionScreen.Roles, RbacPermissionAction.Access);
        var updateRoles = PermissionMatrixCatalog.CreatePermission(RbacPermissionScreen.Roles, RbacPermissionAction.Update);
        var accessPermissions = PermissionMatrixCatalog.CreatePermission(RbacPermissionScreen.Permissions, RbacPermissionAction.Access);
        var updatePermissions = PermissionMatrixCatalog.CreatePermission(RbacPermissionScreen.Permissions, RbacPermissionAction.Update);
        repository.AddPermission(accessRoles);
        repository.AddPermission(updateRoles);
        repository.AddPermission(accessPermissions);
        repository.AddPermission(updatePermissions);

        var adminRole = IamRole.Create("Security Admin", "Manages RBAC");
        adminRole.SyncPermissions([accessRoles, updateRoles, accessPermissions, updatePermissions]);
        repository.AddRole(adminRole);

        var adminUser = IamUser.Create("Ana", "Mendoza", "ana@clarihr.test", true);
        adminUser.SyncRoles([adminRole]);
        repository.AddUser(adminUser);

        await using var serviceProvider = CreateServiceProvider(repository);
        var dispatcher = serviceProvider.GetRequiredService<ICommandDispatcher>();

        var result = await dispatcher.SendAsync(
            new UpdateRolePermissionMatrixCommand(
                adminRole.PublicId,
                [
                    new RolePermissionMatrixScreenUpdate("RBAC_ROLES", true, false, false, false, false),
                    new RolePermissionMatrixScreenUpdate("RBAC_PERMISSIONS", true, false, false, false, false)
                ]));

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityAccessErrors.LastAdministratorRequired.Code, result.Error.Code);
    }

    [Fact]
    public async Task UpdateRolePermissionMatrixCommand_WhenPermissionsChange_ShouldPersistAuditEntries()
    {
        var repository = new TestIamAdministrationRepository();
        SeedSecurityAdministrator(repository);
        repository.AddRbacResource(RbacResource.Create("RBAC_USERS", "Users"));

        var role = IamRole.Create("Supervisor", "Users supervisor");
        repository.AddRole(role);

        await using var serviceProvider = CreateServiceProvider(repository);
        var dispatcher = serviceProvider.GetRequiredService<ICommandDispatcher>();
        var auditService = Assert.IsType<TestAuditService>(serviceProvider.GetRequiredService<IAuditService>());

        var result = await dispatcher.SendAsync(
            new UpdateRolePermissionMatrixCommand(
                role.PublicId,
                [
                    new RolePermissionMatrixScreenUpdate("RBAC_USERS", true, true, false, false, false)
                ]));

        Assert.True(result.IsSuccess);
        var auditEntry = Assert.Single(repository.PermissionAuditLogs);
        Assert.Equal(role.PublicId, auditEntry.RolePublicId);
        Assert.Equal("RBAC_USERS", auditEntry.ResourceKey);
        Assert.Equal(RbacPermissionAuditChangeType.Upsert, auditEntry.ChangeType);
        var administrativeAudit = Assert.Single(auditService.Entries);
        Assert.Equal(AuditEventTypes.RoleResourcePermissionsUpdated, administrativeAudit.EventType);
        Assert.Equal(AuditEntityTypes.Permission, administrativeAudit.EntityType);
        Assert.Equal(AuditActions.PermissionChange, administrativeAudit.Action);
    }

    [Fact]
    public async Task UpdateRolePermissionMatrixCommand_WhenScreenHasManageOverride_ShouldReturnAllActionsGranted()
    {
        var repository = new TestIamAdministrationRepository();
        var managePermission = IamPermission.CreateScreenAction(
            IdentityPermissionCodes.ManagePermissions,
            "Manage permissions",
            "Allows managing permissions.",
            "RBAC",
            "Permissions",
            "Manage");
        repository.AddPermission(managePermission);

        var role = IamRole.Create("Permissions Admin", "Permissions administrator");
        role.SyncPermissions([managePermission]);
        repository.AddRole(role);

        await using var serviceProvider = CreateServiceProvider(repository);
        var dispatcher = serviceProvider.GetRequiredService<IQueryDispatcher>();

        var result = await dispatcher.SendAsync(new GetRolePermissionMatrixQuery(role.PublicId));

        Assert.True(result.IsSuccess);
        var permissionsScreen = result.Value.Screens.Single(screen => screen.Screen == "Permissions");
        Assert.True(permissionsScreen.ManagedByOverride);
        Assert.True(permissionsScreen.Access.Granted);
        Assert.True(permissionsScreen.Read.Granted);
        Assert.True(permissionsScreen.Create.Granted);
        Assert.True(permissionsScreen.Update.Granted);
    }

    [Fact]
    public async Task GetRolePermissionsQuery_WhenRoleHasMatrixPermissions_ShouldReturnResourceBasedContract()
    {
        var repository = new TestIamAdministrationRepository();
        repository.AddRbacResource(RbacResource.Create("RBAC_USERS", "Users"));

        var accessPermission = PermissionMatrixCatalog.CreatePermission(RbacPermissionScreen.Users, RbacPermissionAction.Access);
        var readPermission = PermissionMatrixCatalog.CreatePermission(RbacPermissionScreen.Users, RbacPermissionAction.Read);
        repository.AddPermission(accessPermission);
        repository.AddPermission(readPermission);

        var role = IamRole.Create("Supervisor", "Users supervisor");
        role.SyncPermissions([accessPermission, readPermission]);
        repository.AddRole(role);

        await using var serviceProvider = CreateServiceProvider(repository);
        var dispatcher = serviceProvider.GetRequiredService<IQueryDispatcher>();

        var result = await dispatcher.SendAsync(new GetRolePermissionsQuery(role.PublicId));

        Assert.True(result.IsSuccess);
        var usersPermission = result.Value.Permissions.Single(permission => permission.ResourceKey == "RBAC_USERS");
        Assert.True(usersPermission.HasAccess);
        Assert.True(usersPermission.CanRead);
        Assert.False(usersPermission.CanCreate);
    }

    [Fact]
    public async Task GetPermissionAuditQuery_WhenAuditExists_ShouldReturnParsedEntries()
    {
        var repository = new TestIamAdministrationRepository();
        repository.AddPermissionAuditLog(RbacPermissionAuditLog.Create(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "RBAC_USERS",
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            RbacPermissionAuditChangeType.Upsert,
            """{"hasAccess":false,"canRead":false,"canCreate":false,"canUpdate":false,"canDelete":false}""",
            """{"hasAccess":true,"canRead":true,"canCreate":false,"canUpdate":false,"canDelete":false}""",
            DateTime.Parse("2026-02-28T12:00:00Z").ToUniversalTime()));

        await using var serviceProvider = CreateServiceProvider(repository);
        var dispatcher = serviceProvider.GetRequiredService<IQueryDispatcher>();

        var result = await dispatcher.SendAsync(new GetPermissionAuditQuery(null, "RBAC_USERS", null, null, 1, 20));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.PageNumber);
        Assert.Equal(20, result.Value.PageSize);
        Assert.Equal(1, result.Value.TotalCount);
        var entry = Assert.Single(result.Value.Items);
        Assert.Equal("RBAC_USERS", entry.ResourceKey);
        Assert.False(entry.Before.HasAccess);
        Assert.True(entry.After.HasAccess);
        Assert.Equal("Upsert", entry.ChangeType);
    }

    [Fact]
    public async Task GetPermissionAuditQuery_WhenPaged_ShouldReturnRequestedSlice()
    {
        var repository = new TestIamAdministrationRepository();
        repository.AddPermissionAuditLog(RbacPermissionAuditLog.Create(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "RBAC_USERS",
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            RbacPermissionAuditChangeType.Upsert,
            """{"hasAccess":false,"canRead":false,"canCreate":false,"canUpdate":false,"canDelete":false}""",
            """{"hasAccess":true,"canRead":true,"canCreate":false,"canUpdate":false,"canDelete":false}""",
            DateTime.Parse("2026-02-28T12:00:00Z").ToUniversalTime()));
        repository.AddPermissionAuditLog(RbacPermissionAuditLog.Create(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "RBAC_USERS",
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            RbacPermissionAuditChangeType.Disable,
            """{"hasAccess":true,"canRead":true,"canCreate":false,"canUpdate":false,"canDelete":false}""",
            """{"hasAccess":true,"canRead":false,"canCreate":false,"canUpdate":false,"canDelete":false}""",
            DateTime.Parse("2026-02-28T13:00:00Z").ToUniversalTime()));

        await using var serviceProvider = CreateServiceProvider(repository);
        var dispatcher = serviceProvider.GetRequiredService<IQueryDispatcher>();

        var result = await dispatcher.SendAsync(new GetPermissionAuditQuery(null, "RBAC_USERS", null, null, 2, 1));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.PageNumber);
        Assert.Equal(1, result.Value.PageSize);
        Assert.Equal(2, result.Value.TotalCount);
        var entry = Assert.Single(result.Value.Items);
        Assert.Equal("Upsert", entry.ChangeType);
    }

    [Fact]
    public async Task SyncIamRoleUsersCommand_WhenAssigningUsers_ShouldUpdateRoleMembership()
    {
        var repository = new TestIamAdministrationRepository();
        var role = IamRole.Create("Analyst", "Role for analysts");
        repository.AddRole(role);

        var firstUser = IamUser.Create("Ana", "Mendoza", "ana@clarihr.test", true);
        var secondUser = IamUser.Create("Luis", "Rivera", "luis@clarihr.test", true);
        repository.AddUser(firstUser);
        repository.AddUser(secondUser);

        await using var serviceProvider = CreateServiceProvider(repository);
        var dispatcher = serviceProvider.GetRequiredService<ICommandDispatcher>();

        var result = await dispatcher.SendAsync(
            new SyncIamRoleUsersCommand(role.PublicId, [firstUser.PublicId, secondUser.PublicId]));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.UserCount);
        Assert.All(repository.Users, user => Assert.Contains(user.RoleAssignments, assignment => assignment.RoleId == role.Id));
    }

    [Fact]
    public async Task SyncIamRoleUsersCommand_WhenRemovingLastAdministrator_ShouldReturnConflict()
    {
        var repository = new TestIamAdministrationRepository();
        var permission = IamPermission.CreateScreenAction(
            IdentityPermissionCodes.ManageAdministration,
            "Manage administration",
            "Allows tenant administration.",
            "IAM",
            "Administration",
            "Manage");
        repository.AddPermission(permission);

        var adminRole = IamRole.Create("Admin de Empresa", "Protected role", isSystemRole: true);
        adminRole.SyncPermissions([permission]);
        repository.AddRole(adminRole);

        var adminUser = IamUser.Create("Ana", "Mendoza", "ana@clarihr.test", true);
        adminUser.SyncRoles([adminRole]);
        repository.AddUser(adminUser);

        await using var serviceProvider = CreateServiceProvider(repository);
        var dispatcher = serviceProvider.GetRequiredService<ICommandDispatcher>();

        var result = await dispatcher.SendAsync(new SyncIamRoleUsersCommand(adminRole.PublicId, []));

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityAccessErrors.LastAdministratorRequired.Code, result.Error.Code);
    }

    [Fact]
    public async Task SyncIamUserRolesCommand_WhenRemovingLastAdministrator_ShouldReturnConflict()
    {
        var repository = new TestIamAdministrationRepository();
        var permission = IamPermission.CreateScreenAction(
            IdentityPermissionCodes.ManageAdministration,
            "Manage administration",
            "Allows tenant administration.",
            "IAM",
            "Administration",
            "Manage");
        repository.AddPermission(permission);

        var adminRole = IamRole.Create("Admin de Empresa", "Protected role", isSystemRole: true);
        adminRole.SyncPermissions([permission]);
        repository.AddRole(adminRole);

        var adminUser = IamUser.Create("Ana", "Mendoza", "ana@clarihr.test", true);
        adminUser.SyncRoles([adminRole]);
        repository.AddUser(adminUser);

        await using var serviceProvider = CreateServiceProvider(repository);
        var dispatcher = serviceProvider.GetRequiredService<ICommandDispatcher>();

        var result = await dispatcher.SendAsync(new SyncIamUserRolesCommand(adminUser.PublicId, []));

        Assert.True(result.IsFailure);
        Assert.Equal(IdentityAccessErrors.LastAdministratorRequired.Code, result.Error.Code);
    }

    [Fact]
    public async Task CreateIamPermissionCommand_WhenCodeIsMissing_ShouldGenerateCode()
    {
        var repository = new TestIamAdministrationRepository();

        await using var serviceProvider = CreateServiceProvider(repository);
        var dispatcher = serviceProvider.GetRequiredService<ICommandDispatcher>();

        var result = await dispatcher.SendAsync(new CreateIamPermissionCommand(
            Name: "Export employees",
            Description: "Allows exporting the employee listing",
            Code: null,
            Module: "Employees",
            Screen: "Directory",
            Kind: IamPermissionKind.ScreenAction,
            Action: "Export",
            FieldName: null,
            FieldAccess: null));

        Assert.True(result.IsSuccess);
        Assert.Equal("employees.directory.export", result.Value.Code);
    }

    [Fact]
    public async Task CreateIamPermissionCommand_WhenFieldPermissionPayloadIsInvalid_ShouldReturnValidationError()
    {
        var repository = new TestIamAdministrationRepository();

        await using var serviceProvider = CreateServiceProvider(repository);
        var dispatcher = serviceProvider.GetRequiredService<ICommandDispatcher>();

        var result = await dispatcher.SendAsync(new CreateIamPermissionCommand(
            Name: "Salary field write",
            Description: null,
            Code: null,
            Module: "Employees",
            Screen: "Profile",
            Kind: IamPermissionKind.Field,
            Action: "Edit",
            FieldName: "",
            FieldAccess: null));

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Validation, result.Error.Type);
        Assert.Contains(nameof(CreateIamPermissionCommand.Action), result.Error.ValidationErrors!.Keys);
        Assert.Contains(nameof(CreateIamPermissionCommand.FieldName), result.Error.ValidationErrors.Keys);
        Assert.Contains(nameof(CreateIamPermissionCommand.FieldAccess), result.Error.ValidationErrors.Keys);
    }

    private static ServiceProvider CreateServiceProvider(
        TestIamAdministrationRepository repository,
        Result? authorizationResult = null)
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddLogging();
        services.AddSingleton<IIamAdministrationRepository>(repository);
        services.AddSingleton<IIamAdministrationAuthorizationService>(
            new TestIamAdministrationAuthorizationService(authorizationResult ?? Result.Success()));
        services.AddSingleton<ICurrentUserService>(new TestCurrentUserService());
        services.AddSingleton<ITenantContext>(new TestTenantContext(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa")));
        services.AddSingleton<IDateTimeProvider>(new TestDateTimeProvider(DateTime.Parse("2026-02-28T12:00:00Z").ToUniversalTime()));
        services.AddSingleton<IAuditService, TestAuditService>();

        return services.BuildServiceProvider();
    }

    private sealed class TestIamAdministrationAuthorizationService(Result result) : IIamAdministrationAuthorizationService
    {
        public Task<Result> EnsureCanManageAsync(CancellationToken cancellationToken) => Task.FromResult(result);

        public Task<Result> EnsureAuthorizedAsync(
            RbacPermissionScreen screen,
            RbacPermissionAction action,
            CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public bool IsAuthenticated => true;

        public string? UserId => "11111111-1111-1111-1111-111111111111";

        public IReadOnlyCollection<string> Roles => [];

        public IReadOnlyCollection<string> Permissions => [];
    }

    private sealed class TestTenantContext(Guid? tenantId) : ITenantContext
    {
        public Guid? TenantId { get; } = tenantId;
    }

    private sealed class TestDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class TestAuditService : IAuditService
    {
        public List<AuditLogEntry> Entries { get; } = [];

        public Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken)
        {
            Entries.Add(entry);
            return Task.CompletedTask;
        }
    }

    private sealed class TestIamAdministrationRepository : IIamAdministrationRepository
    {
        private static readonly MethodInfo EntityIdSetter = typeof(Entity)
            .GetProperty(nameof(Entity.Id), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetSetMethod(nonPublic: true)!;

        private readonly List<IamUser> _users = [];
        private readonly List<IamRole> _roles = [];
        private readonly List<IamPermission> _permissions = [];
        private readonly List<RbacResource> _rbacResources = [];
        private readonly List<RbacPermissionAuditLog> _permissionAuditLogs = [];
        private long _nextId = 1;

        public IReadOnlyList<IamUser> Users => _users;

        public IReadOnlyList<IamRole> Roles => _roles;

        public IReadOnlyList<IamPermission> Permissions => _permissions;

        public IReadOnlyList<RbacPermissionAuditLog> PermissionAuditLogs => _permissionAuditLogs;

        public void AddUser(IamUser user)
        {
            EnsureId(user);
            BindUserAssignments(user);
            _users.Add(user);
        }

        public void AddRole(IamRole role)
        {
            EnsureId(role);
            BindRolePermissions(role);
            _roles.Add(role);
        }

        public void RemoveRole(IamRole role) => _roles.Remove(role);

        public void AddPermission(IamPermission permission)
        {
            EnsureId(permission);
            _permissions.Add(permission);
        }

        public void AddPermissionAuditLog(RbacPermissionAuditLog auditLog)
        {
            EnsureId(auditLog);
            auditLog.SetTenantId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
            _permissionAuditLogs.Add(auditLog);
        }

        public void AddRbacResource(RbacResource resource)
        {
            EnsureAuditMetadata(resource);
            _rbacResources.Add(resource);
        }

        public Task<bool> UserEmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult(_users.Any(user => user.NormalizedEmail == normalizedEmail));

        public Task<bool> RoleNameExistsAsync(string normalizedRoleName, CancellationToken cancellationToken) =>
            Task.FromResult(_roles.Any(role => role.NormalizedName == normalizedRoleName));

        public Task<bool> PermissionCodeExistsAsync(string normalizedPermissionCode, CancellationToken cancellationToken) =>
            Task.FromResult(_permissions.Any(permission => permission.NormalizedCode == normalizedPermissionCode));

        public Task<bool> UserPublicIdExistsAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(_users.Any(user => user.PublicId == userId));

        public Task<bool> RolePublicIdExistsAsync(Guid roleId, CancellationToken cancellationToken) =>
            Task.FromResult(_roles.Any(role => role.PublicId == roleId));

        public Task<bool> PermissionPublicIdExistsAsync(Guid permissionId, CancellationToken cancellationToken) =>
            Task.FromResult(_permissions.Any(permission => permission.PublicId == permissionId));

        public Task<IamUser?> FindUserByPublicIdAsync(Guid userId, bool includeRoles, CancellationToken cancellationToken) =>
            Task.FromResult(BindUserResult(_users.SingleOrDefault(user => user.PublicId == userId), includeRoles));

        public Task<IamRole?> FindRoleByPublicIdAsync(Guid roleId, bool includePermissions, CancellationToken cancellationToken) =>
            Task.FromResult(BindRoleResult(_roles.SingleOrDefault(role => role.PublicId == roleId), includePermissions));

        public Task<IamPermission?> FindPermissionByPublicIdAsync(Guid permissionId, CancellationToken cancellationToken) =>
            Task.FromResult(_permissions.SingleOrDefault(permission => permission.PublicId == permissionId));

        public Task<IReadOnlyList<IamRole>> GetRolesByPublicIdsAsync(
            IReadOnlyCollection<Guid> roleIds,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<IamRole> roles = _roles
                .Where(role => roleIds.Contains(role.PublicId))
                .Select(BindRolePermissions)
                .ToList();

            return Task.FromResult(roles);
        }

        public Task<IReadOnlyList<IamUser>> GetUsersByPublicIdsAsync(
            IReadOnlyCollection<Guid> userIds,
            bool includeRoles,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<IamUser> users = _users
                .Where(user => userIds.Contains(user.PublicId))
                .Select(user => BindUserResult(user, includeRoles)!)
                .ToList();

            return Task.FromResult(users);
        }

        public Task<IReadOnlyList<IamUser>> GetUsersAssignedToRoleAsync(
            Guid roleId,
            bool includeRoles,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<IamUser> users = _users
                .Where(user => user.RoleAssignments.Any(assignment => _roles.Single(role => role.Id == assignment.RoleId).PublicId == roleId))
                .Select(user => BindUserResult(user, includeRoles)!)
                .ToList();

            return Task.FromResult(users);
        }

        public Task<IReadOnlyList<IamUser>> GetActiveUsersAsync(
            bool includeRoles,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<IamUser> users = _users
                .Where(static user => user.IsActive)
                .Select(user => BindUserResult(user, includeRoles)!)
                .ToList();

            return Task.FromResult(users);
        }

        public Task<IReadOnlyCollection<Guid>> GetActiveAdministratorUserIdsAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<Guid> users = _users
                .Where(user => user.IsActive)
                .Where(user => user.RoleAssignments.Any(assignment =>
                    _roles.Single(role => role.Id == assignment.RoleId).PermissionAssignments.Any(permissionAssignment =>
                        _permissions.Single(permission => permission.Id == permissionAssignment.PermissionId).NormalizedCode ==
                            IdentityPermissionCodes.ManageAdministration.ToUpperInvariant() ||
                        _permissions.Single(permission => permission.Id == permissionAssignment.PermissionId).NormalizedCode ==
                            IdentityPermissionCodes.ManageUsers.ToUpperInvariant())))
                .Select(user => user.PublicId)
                .Distinct()
                .ToArray();

            return Task.FromResult(users);
        }

        public Task<IReadOnlyList<IamPermission>> GetPermissionsByNormalizedCodesAsync(
            IReadOnlyCollection<string> normalizedPermissionCodes,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<IamPermission> permissions = _permissions
                .Where(permission => normalizedPermissionCodes.Contains(permission.NormalizedCode))
                .ToList();

            return Task.FromResult(permissions);
        }

        public Task<IReadOnlyList<IamPermission>> GetPermissionsByPublicIdsAsync(
            IReadOnlyCollection<Guid> permissionIds,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<IamPermission> permissions = _permissions
                .Where(permission => permissionIds.Contains(permission.PublicId))
                .ToList();

            return Task.FromResult(permissions);
        }

        public Task<PagedResponse<IamUserSummaryResponse>> GetUsersAsync(
            int pageNumber,
            int pageSize,
            string? search,
            CancellationToken cancellationToken)
        {
            var query = _users.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(user =>
                    user.Email.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    user.FirstName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    user.LastName.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            var totalCount = query.Count();
            var items = query
                .OrderBy(user => user.LastName)
                .ThenBy(user => user.FirstName)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(user => new IamUserSummaryResponse(
                    user.PublicId,
                    user.Email,
                    user.FirstName,
                    user.LastName,
                    user.IsActive,
                    user.RoleAssignments.Count))
                .ToList();

            return Task.FromResult(new PagedResponse<IamUserSummaryResponse>(items, pageNumber, pageSize, totalCount));
        }

        public Task<IamUserResponse?> GetUserAsync(Guid userId, CancellationToken cancellationToken)
        {
            var user = _users.SingleOrDefault(candidate => candidate.PublicId == userId);
            return Task.FromResult(user is null ? null : MapUser(user));
        }

        public Task<PagedResponse<IamRoleSummaryResponse>> GetRolesAsync(
            int pageNumber,
            int pageSize,
            string? search,
            CancellationToken cancellationToken)
        {
            var query = _roles.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(role =>
                    role.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    (role.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            var totalCount = query.Count();
            var items = query
                .OrderBy(role => role.Name)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(role => new IamRoleSummaryResponse(
                    role.PublicId,
                    role.Name,
                    role.Description,
                    role.IsSystemRole,
                    role.PermissionAssignments.Count,
                    _users.Count(user => user.RoleAssignments.Any(assignment => assignment.RoleId == role.Id))))
                .ToList();

            return Task.FromResult(new PagedResponse<IamRoleSummaryResponse>(items, pageNumber, pageSize, totalCount));
        }

        public Task<IamRoleResponse?> GetRoleAsync(Guid roleId, CancellationToken cancellationToken)
        {
            var role = _roles.SingleOrDefault(candidate => candidate.PublicId == roleId);
            if (role is null)
            {
                return Task.FromResult<IamRoleResponse?>(null);
            }

            var permissions = role.PermissionAssignments
                .Select(assignment => _permissions.Single(permission => permission.Id == assignment.PermissionId))
                .Select(MapPermissionReference)
                .ToArray();

            var userCount = _users.Count(user => user.RoleAssignments.Any(assignment => assignment.RoleId == role.Id));

            return Task.FromResult<IamRoleResponse?>(new IamRoleResponse(
                role.PublicId,
                role.Name,
                role.Description,
                role.IsSystemRole,
                userCount,
                permissions));
        }

        public Task<PagedResponse<IamPermissionSummaryResponse>> GetPermissionsAsync(
            int pageNumber,
            int pageSize,
            string? search,
            CancellationToken cancellationToken)
        {
            var query = _permissions.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(permission =>
                    permission.Code.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    permission.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    permission.Module.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                    permission.Screen.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            var totalCount = query.Count();
            var items = query
                .OrderBy(permission => permission.Module)
                .ThenBy(permission => permission.Screen)
                .ThenBy(permission => permission.Code)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(permission => new IamPermissionSummaryResponse(
                    permission.PublicId,
                    permission.Code,
                    permission.Name,
                    permission.Description,
                    permission.Module,
                    permission.Screen,
                    permission.Kind,
                    permission.Action,
                    permission.FieldName,
                    permission.FieldAccess))
                .ToList();

            return Task.FromResult(new PagedResponse<IamPermissionSummaryResponse>(items, pageNumber, pageSize, totalCount));
        }

        public Task<IamPermissionResponse?> GetPermissionAsync(Guid permissionId, CancellationToken cancellationToken)
        {
            var permission = _permissions.SingleOrDefault(candidate => candidate.PublicId == permissionId);
            return Task.FromResult(permission is null
                ? null
                : new IamPermissionResponse(
                    permission.PublicId,
                    permission.Code,
                    permission.Name,
                    permission.Description,
                    permission.Module,
                    permission.Screen,
                    permission.Kind,
                    permission.Action,
                    permission.FieldName,
                    permission.FieldAccess));
        }

        public Task<IReadOnlyList<RbacResource>> GetActiveRbacResourcesAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<RbacResource> resources = _rbacResources
                .Where(static resource => resource.IsActive)
                .OrderBy(static resource => resource.DisplayName)
                .ToList();

            return Task.FromResult(resources);
        }

        public Task<PagedResponse<RbacPermissionAuditLog>> GetPermissionAuditLogsAsync(
            Guid? roleId,
            string? normalizedResourceKey,
            DateTime? fromUtc,
            DateTime? toUtc,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken)
        {
            var query = _permissionAuditLogs.AsEnumerable();

            if (roleId.HasValue)
            {
                query = query.Where(log => log.RolePublicId == roleId.Value);
            }

            if (!string.IsNullOrWhiteSpace(normalizedResourceKey))
            {
                query = query.Where(log => log.NormalizedResourceKey == normalizedResourceKey);
            }

            if (fromUtc.HasValue)
            {
                query = query.Where(log => log.ChangedAtUtc >= fromUtc.Value);
            }

            if (toUtc.HasValue)
            {
                query = query.Where(log => log.ChangedAtUtc <= toUtc.Value);
            }

            var ordered = query
                .OrderByDescending(log => log.ChangedAtUtc)
                .ThenByDescending(log => log.Id)
                .ToList();

            var items = ordered
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToArray();

            return Task.FromResult(new PagedResponse<RbacPermissionAuditLog>(items, pageNumber, pageSize, ordered.Count));
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);

        private void EnsureId(Entity entity)
        {
            if (entity.Id != 0)
            {
                return;
            }

            EntityIdSetter.Invoke(entity, [_nextId++]);
        }

        private static void EnsureAuditMetadata(RbacResource resource)
        {
            resource.MarkCreated(DateTime.UtcNow);
        }

        private IamUserResponse MapUser(IamUser user)
        {
            var roles = user.RoleAssignments
                .Select(assignment => _roles.Single(role => role.Id == assignment.RoleId))
                .Select(role => new IamRoleReferenceResponse(role.PublicId, role.Name, role.Description))
                .ToArray();

            return new IamUserResponse(
                user.PublicId,
                user.Email,
                user.FirstName,
                user.LastName,
                user.IsActive,
                roles);
        }

        private static IamPermissionReferenceResponse MapPermissionReference(IamPermission permission) =>
            new(
                permission.PublicId,
                permission.Code,
                permission.Name,
                permission.Description,
                permission.Module,
                permission.Screen,
                permission.Kind,
                permission.Action,
                permission.FieldName,
                permission.FieldAccess);

        private IamUser? BindUserResult(IamUser? user, bool includeRoles)
        {
            if (user is null || !includeRoles)
            {
                return user;
            }

            BindUserAssignments(user);
            return user;
        }

        private IamRole? BindRoleResult(IamRole? role, bool includePermissions)
        {
            if (role is null || !includePermissions)
            {
                return role;
            }

            BindRolePermissions(role);
            return role;
        }

        private IamRole BindRolePermissions(IamRole role)
        {
            foreach (var assignment in role.PermissionAssignments)
            {
                SetPrivateProperty(assignment, nameof(IamRolePermissionAssignment.Role), role);

                var permission = _permissions.SingleOrDefault(candidate => candidate.Id == assignment.PermissionId);
                if (permission is not null)
                {
                    SetPrivateProperty(assignment, nameof(IamRolePermissionAssignment.Permission), permission);
                }
            }

            return role;
        }

        private void BindUserAssignments(IamUser user)
        {
            foreach (var assignment in user.RoleAssignments)
            {
                SetPrivateProperty(assignment, nameof(IamUserRoleAssignment.User), user);

                var role = _roles.SingleOrDefault(candidate => candidate.Id == assignment.RoleId);
                if (role is not null)
                {
                    SetPrivateProperty(assignment, nameof(IamUserRoleAssignment.Role), BindRolePermissions(role));
                }
            }
        }

        private static void SetPrivateProperty(object target, string propertyName, object value)
        {
            target.GetType()
                .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
                .GetSetMethod(nonPublic: true)!
                .Invoke(target, [value]);
        }
    }

    private static void SeedSecurityAdministrator(TestIamAdministrationRepository repository)
    {
        var accessRoles = PermissionMatrixCatalog.CreatePermission(RbacPermissionScreen.Roles, RbacPermissionAction.Access);
        var updateRoles = PermissionMatrixCatalog.CreatePermission(RbacPermissionScreen.Roles, RbacPermissionAction.Update);
        var accessPermissions = PermissionMatrixCatalog.CreatePermission(RbacPermissionScreen.Permissions, RbacPermissionAction.Access);
        var updatePermissions = PermissionMatrixCatalog.CreatePermission(RbacPermissionScreen.Permissions, RbacPermissionAction.Update);
        repository.AddPermission(accessRoles);
        repository.AddPermission(updateRoles);
        repository.AddPermission(accessPermissions);
        repository.AddPermission(updatePermissions);

        var adminRole = IamRole.Create("Security Admin Seed", "Maintains RBAC security");
        adminRole.SyncPermissions([accessRoles, updateRoles, accessPermissions, updatePermissions]);
        repository.AddRole(adminRole);

        var adminUser = IamUser.Create("Seed", "Admin", "seed-admin@clarihr.test", true);
        adminUser.SyncRoles([adminRole]);
        repository.AddUser(adminUser);
    }
}

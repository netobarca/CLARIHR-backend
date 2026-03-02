using System.Reflection;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.CompanyUsers;
using CLARIHR.Application.Features.CompanyUsers.Common;
using CLARIHR.Application.Features.IdentityAccess.Contracts;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.IdentityAccess;
using CLARIHR.Infrastructure.IdentityAccess;
using Microsoft.Extensions.Logging.Abstractions;

namespace CLARIHR.Application.UnitTests;

public sealed class CompanyUserManagementTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherTenantId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static TestFieldPermissionService CreateFieldPermissionService(FieldAccessProfile? profile = null) =>
        new(profile ?? CreateFullFieldAccessProfile());

    private static FieldAccessProfile CreateFullFieldAccessProfile() =>
        CreateFieldAccessProfile(
            new Dictionary<string, FieldPermissionOverrideState>(StringComparer.OrdinalIgnoreCase),
            new RbacPermissionState(true, true, true, true, true));

    private static FieldAccessProfile CreateFieldAccessProfile(
        IReadOnlyDictionary<string, FieldPermissionOverrideState> overrides,
        RbacPermissionState screenState) =>
        FieldPermissionEvaluator.BuildProfile(
            CompanyUserFieldKeys.ResourceKey,
            FieldCatalogRegistry.GetResourceFields(CompanyUserFieldKeys.ResourceKey),
            overrides,
            screenState);

    private static IFieldSerializationService CreateFieldSerializationService() => new FieldSerializationService();

    private static IRbacAuthorizationService CreateRbacAuthorizationService(Result? result = null) =>
        new TestRbacAuthorizationService(result ?? Result.Success());

    private static TestAuditService CreateAuditService() => new();

    [Fact]
    public async Task Handle_WhenInvitingNewUser_ShouldCreatePendingUserMembershipInvitationAndIamUser()
    {
        var userRepository = new TestUserRepository();
        var companyRepository = new TestCompanyRepository();
        var iamRepository = new TestIamAdministrationRepository();
        var userCompanyRepository = new TestUserCompanyRepository(companyRepository, userRepository, iamRepository);
        var invitationTokenRepository = new TestInvitationTokenRepository();
        var emailService = new TestEmailService();
        var unitOfWork = new TestUnitOfWork();
        var auditService = CreateAuditService();

        var company = CreateCompany(TenantId, "Acme HR");
        companyRepository.Seed(company);
        var standardRole = CreateRole(iamRepository, TenantId, "Usuario Estandar");

        var handler = new CreateCompanyUserCommandHandler(
            userRepository,
            userCompanyRepository,
            companyRepository,
            iamRepository,
            invitationTokenRepository,
            new TestInvitationTokenHasher(),
            emailService,
            new AllowCompanyUserAuthorizationService(),
            CreateRbacAuthorizationService(),
            new TestTenantContext(TenantId),
            CreateFieldPermissionService(),
            CreateFieldSerializationService(),
            unitOfWork,
            new FixedDateTimeProvider(new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc)),
            auditService,
            NullLogger<CreateCompanyUserCommandHandler>.Instance);

        var result = await handler.Handle(
            new CreateCompanyUserCommand("ana@acme.test", "Ana", "Mendoza", standardRole.PublicId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.PendingActivation, result.Value.User.Status);
        Assert.Single(userRepository.Items);
        Assert.Equal(UserStatus.PendingActivation, userRepository.Items[0].Status);
        Assert.Single(userCompanyRepository.Items);
        Assert.True(userCompanyRepository.Items[0].IsPrimary);
        Assert.Single(iamRepository.Users);
        Assert.False(iamRepository.Users[0].IsActive);
        Assert.Single(invitationTokenRepository.Items);
        Assert.Single(emailService.Messages);
        Assert.Equal(standardRole.PublicId, result.Value.User.RoleId);
        Assert.True(unitOfWork.Transaction.CommitCalled);
        Assert.Single(auditService.Entries);
        Assert.Equal(AuditEventTypes.UserInvited, auditService.Entries[0].EventType);
    }

    [Fact]
    public async Task Handle_WhenUpdatingUser_ShouldWriteAuditLog()
    {
        var userRepository = new TestUserRepository();
        var companyRepository = SeedCompanyRepository(TenantId, "Acme HR");
        var iamRepository = new TestIamAdministrationRepository();
        var userCompanyRepository = new TestUserCompanyRepository(companyRepository, userRepository, iamRepository);
        var auditService = CreateAuditService();

        var currentRole = CreateRole(iamRepository, TenantId, "Viewer");
        var nextRole = CreateRole(iamRepository, TenantId, "Supervisor");
        var user = CreatePersistedLocalUser("ana@acme.test");
        userRepository.Seed(user);
        var membership = UserCompanyMembership.Create(user.Id, companyRepository.Items[0].Id, currentRole.Id, isPrimary: true);
        userCompanyRepository.Add(membership);

        var iamUser = IamUser.CreateLinked(user.PublicId, user.FirstName, user.LastName, user.Email, isActive: true);
        iamUser.SetTenantId(TenantId);
        iamUser.SyncRoles([currentRole]);
        SetTenantOnAssignments(iamUser.RoleAssignments, TenantId);
        iamRepository.AddUser(iamUser);

        var handler = new UpdateCompanyUserCommandHandler(
            userRepository,
            userCompanyRepository,
            iamRepository,
            new AllowCompanyUserAuthorizationService(),
            CreateRbacAuthorizationService(),
            new TestTenantContext(TenantId),
            CreateFieldPermissionService(),
            CreateFieldSerializationService(),
            new TestUnitOfWork(),
            auditService,
            NullLogger<UpdateCompanyUserCommandHandler>.Instance);

        var result = await handler.Handle(
            new UpdateCompanyUserCommand(user.PublicId, "Carla", "Mendoza", nextRole.PublicId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var auditEntry = Assert.Single(auditService.Entries);
        Assert.Equal(AuditEventTypes.UserUpdated, auditEntry.EventType);
        Assert.Equal(AuditActions.Update, auditEntry.Action);
        Assert.Equal(AuditEntityTypes.User, auditEntry.EntityType);
    }

    [Fact]
    public async Task Handle_WhenDeactivatingUser_ShouldWriteAuditLog()
    {
        var userRepository = new TestUserRepository();
        var companyRepository = SeedCompanyRepository(TenantId, "Acme HR");
        var iamRepository = new TestIamAdministrationRepository();
        var userCompanyRepository = new TestUserCompanyRepository(companyRepository, userRepository, iamRepository);
        var refreshTokenRepository = new TestRefreshTokenRepository();
        var auditService = CreateAuditService();

        var user = CreatePersistedLocalUser("ana@acme.test");
        userRepository.Seed(user);
        var role = CreateRole(iamRepository, TenantId, "Usuario Estandar");
        userCompanyRepository.Add(UserCompanyMembership.Create(user.Id, companyRepository.Items[0].Id, role.Id, isPrimary: true));

        var iamUser = IamUser.CreateLinked(user.PublicId, user.FirstName, user.LastName, user.Email, isActive: true);
        iamUser.SetTenantId(TenantId);
        iamUser.SyncRoles([role]);
        SetTenantOnAssignments(iamUser.RoleAssignments, TenantId);
        iamRepository.AddUser(iamUser);

        var handler = new DeactivateCompanyUserCommandHandler(
            userRepository,
            userCompanyRepository,
            iamRepository,
            refreshTokenRepository,
            new AllowCompanyUserAuthorizationService(),
            new TestTenantContext(TenantId),
            CreateFieldPermissionService(),
            CreateFieldSerializationService(),
            new TestUnitOfWork(),
            new FixedDateTimeProvider(new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc)),
            auditService,
            NullLogger<DeactivateCompanyUserCommandHandler>.Instance);

        var result = await handler.Handle(new DeactivateCompanyUserCommand(user.PublicId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var auditEntry = Assert.Single(auditService.Entries);
        Assert.Equal(AuditEventTypes.UserDeactivated, auditEntry.EventType);
        Assert.Equal(AuditActions.Deactivate, auditEntry.Action);
        Assert.Equal(AuditEntityTypes.User, auditEntry.EntityType);
    }

    [Fact]
    public async Task Handle_WhenRoleIsInvalid_ShouldReturnNotFound()
    {
        var handler = new CreateCompanyUserCommandHandler(
            new TestUserRepository(),
            new TestUserCompanyRepository(new TestCompanyRepository(), new TestUserRepository(), new TestIamAdministrationRepository()),
            SeedCompanyRepository(TenantId, "Acme HR"),
            new TestIamAdministrationRepository(),
            new TestInvitationTokenRepository(),
            new TestInvitationTokenHasher(),
            new TestEmailService(),
            new AllowCompanyUserAuthorizationService(),
            CreateRbacAuthorizationService(),
            new TestTenantContext(TenantId),
            CreateFieldPermissionService(),
            CreateFieldSerializationService(),
            new TestUnitOfWork(),
            new FixedDateTimeProvider(new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc)),
            CreateAuditService(),
            NullLogger<CreateCompanyUserCommandHandler>.Instance);

        var result = await handler.Handle(
            new CreateCompanyUserCommand("ana@acme.test", "Ana", "Mendoza", Guid.NewGuid()),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CompanyUserErrors.RoleNotFound.Code, result.Error.Code);
    }

    [Fact]
    public async Task Handle_WhenUserAlreadyBelongsToCompany_ShouldReturnConflict()
    {
        var userRepository = new TestUserRepository();
        var companyRepository = SeedCompanyRepository(TenantId, "Acme HR");
        var iamRepository = new TestIamAdministrationRepository();
        var userCompanyRepository = new TestUserCompanyRepository(companyRepository, userRepository, iamRepository);

        var user = CreatePersistedLocalUser("ana@acme.test");
        userRepository.Seed(user);
        var role = CreateRole(iamRepository, TenantId, "Usuario Estandar");
        userCompanyRepository.Add(UserCompanyMembership.Create(user.Id, companyRepository.Items[0].Id, role.Id, isPrimary: true));

        var handler = new CreateCompanyUserCommandHandler(
            userRepository,
            userCompanyRepository,
            companyRepository,
            iamRepository,
            new TestInvitationTokenRepository(),
            new TestInvitationTokenHasher(),
            new TestEmailService(),
            new AllowCompanyUserAuthorizationService(),
            CreateRbacAuthorizationService(),
            new TestTenantContext(TenantId),
            CreateFieldPermissionService(),
            CreateFieldSerializationService(),
            new TestUnitOfWork(),
            new FixedDateTimeProvider(new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc)),
            CreateAuditService(),
            NullLogger<CreateCompanyUserCommandHandler>.Instance);

        var result = await handler.Handle(
            new CreateCompanyUserCommand("ana@acme.test", "Ana", "Mendoza", role.PublicId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CompanyUserErrors.UserAlreadyInCompany.Code, result.Error.Code);
    }

    [Fact]
    public async Task Handle_WhenDeactivatingLastAdministrator_ShouldReturnConflict()
    {
        var userRepository = new TestUserRepository();
        var companyRepository = SeedCompanyRepository(TenantId, "Acme HR");
        var iamRepository = new TestIamAdministrationRepository();
        var userCompanyRepository = new TestUserCompanyRepository(companyRepository, userRepository, iamRepository);
        var refreshTokenRepository = new TestRefreshTokenRepository();

        var adminUser = CreatePersistedLocalUser("admin@acme.test");
        userRepository.Seed(adminUser);
        var adminRole = CreateAdminRole(iamRepository, TenantId, "Admin de Empresa");
        userCompanyRepository.Add(UserCompanyMembership.Create(adminUser.Id, companyRepository.Items[0].Id, adminRole.Id, isPrimary: true));

        var iamUser = IamUser.CreateLinked(adminUser.PublicId, adminUser.FirstName, adminUser.LastName, adminUser.Email, isActive: true);
        iamUser.SetTenantId(TenantId);
        iamUser.SyncRoles([adminRole]);
        SetTenantOnAssignments(iamUser.RoleAssignments, TenantId);
        iamRepository.AddUser(iamUser);

        var handler = new DeactivateCompanyUserCommandHandler(
            userRepository,
            userCompanyRepository,
            iamRepository,
            refreshTokenRepository,
            new AllowCompanyUserAuthorizationService(),
            new TestTenantContext(TenantId),
            CreateFieldPermissionService(),
            CreateFieldSerializationService(),
            new TestUnitOfWork(),
            new FixedDateTimeProvider(new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc)),
            CreateAuditService(),
            NullLogger<DeactivateCompanyUserCommandHandler>.Instance);

        var result = await handler.Handle(new DeactivateCompanyUserCommand(adminUser.PublicId), CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(CompanyUserErrors.LastActiveAdministratorRequired.Code, result.Error.Code);
        Assert.Equal(UserStatus.Active, adminUser.Status);
        Assert.Empty(refreshTokenRepository.RevokedUsers);
    }

    [Fact]
    public async Task Handle_WhenReactivatingInactiveUser_ShouldReactivateMembershipAndIamUser()
    {
        var userRepository = new TestUserRepository();
        var companyRepository = SeedCompanyRepository(TenantId, "Acme HR");
        var iamRepository = new TestIamAdministrationRepository();
        var userCompanyRepository = new TestUserCompanyRepository(companyRepository, userRepository, iamRepository);

        var user = CreatePersistedLocalUser("ana@acme.test");
        user.Deactivate();
        userRepository.Seed(user);

        var role = CreateRole(iamRepository, TenantId, "Usuario Estandar");
        var membership = UserCompanyMembership.Create(user.Id, companyRepository.Items[0].Id, role.Id, isPrimary: true);
        membership.Deactivate();
        userCompanyRepository.Add(membership);

        var iamUser = IamUser.CreateLinked(user.PublicId, user.FirstName, user.LastName, user.Email, isActive: false);
        iamUser.SetTenantId(TenantId);
        iamUser.SyncRoles([role]);
        SetTenantOnAssignments(iamUser.RoleAssignments, TenantId);
        iamRepository.AddUser(iamUser);

        var handler = new ReactivateCompanyUserCommandHandler(
            userRepository,
            userCompanyRepository,
            iamRepository,
            new AllowCompanyUserAuthorizationService(),
            new TestTenantContext(TenantId),
            CreateFieldPermissionService(),
            CreateFieldSerializationService(),
            new TestUnitOfWork(),
            CreateAuditService(),
            NullLogger<ReactivateCompanyUserCommandHandler>.Instance);

        var result = await handler.Handle(new ReactivateCompanyUserCommand(user.PublicId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(UserStatus.Active, user.Status);
        Assert.Equal(UserCompanyStatus.Active, membership.Status);
        Assert.True(iamUser.IsActive);
    }

    [Fact]
    public async Task Handle_WhenResettingInvitation_ShouldRevokePreviousTokenAndIssueNewOne()
    {
        var userRepository = new TestUserRepository();
        var companyRepository = SeedCompanyRepository(TenantId, "Acme HR");
        var iamRepository = new TestIamAdministrationRepository();
        var userCompanyRepository = new TestUserCompanyRepository(companyRepository, userRepository, iamRepository);
        var invitationTokenRepository = new TestInvitationTokenRepository();
        var emailService = new TestEmailService();
        var unitOfWork = new TestUnitOfWork();

        var user = CreatePersistedInvitedUser("invitee@acme.test");
        userRepository.Seed(user);
        var role = CreateRole(iamRepository, TenantId, "Usuario Estandar");
        userCompanyRepository.Add(UserCompanyMembership.Create(user.Id, companyRepository.Items[0].Id, role.Id, isPrimary: true));

        var previousToken = InvitationToken.Issue(user.Id, companyRepository.Items[0].Id, "OLDTOKENHASH", new DateTime(2026, 3, 5, 12, 0, 0, DateTimeKind.Utc));
        SetEntityId(previousToken, 1);
        invitationTokenRepository.Items.Add(previousToken);

        var handler = new ResetInvitationCommandHandler(
            userRepository,
            userCompanyRepository,
            companyRepository,
            invitationTokenRepository,
            new TestInvitationTokenHasher(),
            emailService,
            new AllowCompanyUserAuthorizationService(),
            new TestTenantContext(TenantId),
            CreateFieldPermissionService(),
            CreateFieldSerializationService(),
            unitOfWork,
            new FixedDateTimeProvider(new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc)),
            CreateAuditService(),
            NullLogger<ResetInvitationCommandHandler>.Instance);

        var result = await handler.Handle(new ResetInvitationCommand(user.PublicId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, invitationTokenRepository.Items.Count);
        Assert.NotNull(invitationTokenRepository.Items[0].RevokedUtc);
        Assert.Single(emailService.Messages);
        Assert.True(unitOfWork.Transaction.CommitCalled);
    }

    [Fact]
    public async Task Handle_WhenListingUsers_ShouldReturnOnlyCurrentTenantUsers()
    {
        var userRepository = new TestUserRepository();
        var companyRepository = new TestCompanyRepository();
        var iamRepository = new TestIamAdministrationRepository();
        var userCompanyRepository = new TestUserCompanyRepository(companyRepository, userRepository, iamRepository);

        var currentCompany = CreateCompany(TenantId, "Acme HR");
        var otherCompany = CreateCompany(OtherTenantId, "Other HR");
        companyRepository.Seed(currentCompany);
        companyRepository.Seed(otherCompany);

        var currentRole = CreateRole(iamRepository, TenantId, "Usuario Estandar");
        var otherRole = CreateRole(iamRepository, OtherTenantId, "Usuario Estandar");

        var currentUser = CreatePersistedLocalUser("ana@acme.test");
        var otherUser = CreatePersistedLocalUser("bruno@other.test");
        userRepository.Seed(currentUser);
        userRepository.Seed(otherUser);

        userCompanyRepository.Add(UserCompanyMembership.Create(currentUser.Id, currentCompany.Id, currentRole.Id, isPrimary: true));
        userCompanyRepository.Add(UserCompanyMembership.Create(otherUser.Id, otherCompany.Id, otherRole.Id, isPrimary: true));

        var handler = new GetCompanyUsersQueryHandler(
            userCompanyRepository,
            new AllowCompanyUserAuthorizationService(),
            new TestTenantContext(TenantId),
            CreateFieldPermissionService(),
            CreateFieldSerializationService());

        var result = await handler.Handle(new GetCompanyUsersQuery(Page: 1, PageSize: 20), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Items);
        Assert.Equal(currentUser.PublicId, result.Value.Items.Single().Id);
    }

    [Fact]
    public async Task Handle_WhenEmailVisibilityIsDisabled_ShouldHideEmailInListing()
    {
        var userRepository = new TestUserRepository();
        var companyRepository = SeedCompanyRepository(TenantId, "Acme HR");
        var iamRepository = new TestIamAdministrationRepository();
        var userCompanyRepository = new TestUserCompanyRepository(companyRepository, userRepository, iamRepository);

        var role = CreateRole(iamRepository, TenantId, "Usuario Estandar");
        var user = CreatePersistedLocalUser("ana@acme.test");
        userRepository.Seed(user);
        userCompanyRepository.Add(UserCompanyMembership.Create(user.Id, companyRepository.Items[0].Id, role.Id, isPrimary: true));

        var profile = CreateFieldAccessProfile(
            new Dictionary<string, FieldPermissionOverrideState>(StringComparer.OrdinalIgnoreCase)
            {
                [CompanyUserFieldKeys.Email] = FieldPermissionEvaluator.NormalizeOverride(
                    isVisible: false,
                    isEditable: false,
                    isRequired: false,
                    isMasked: false)
            },
            new RbacPermissionState(true, true, true, true, true));

        var handler = new GetCompanyUsersQueryHandler(
            userCompanyRepository,
            new AllowCompanyUserAuthorizationService(),
            new TestTenantContext(TenantId),
            CreateFieldPermissionService(profile),
            CreateFieldSerializationService());

        var result = await handler.Handle(new GetCompanyUsersQuery(Page: 1, PageSize: 20), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Items.Single().Email);
    }

    [Fact]
    public async Task Handle_WhenUserBelongsToAnotherTenant_ShouldReturnTenantMismatch()
    {
        var userRepository = new TestUserRepository();
        var companyRepository = new TestCompanyRepository();
        var iamRepository = new TestIamAdministrationRepository();
        var userCompanyRepository = new TestUserCompanyRepository(companyRepository, userRepository, iamRepository);

        var currentCompany = CreateCompany(TenantId, "Acme HR");
        var otherCompany = CreateCompany(OtherTenantId, "Other HR");
        companyRepository.Seed(currentCompany);
        companyRepository.Seed(otherCompany);

        var currentRole = CreateRole(iamRepository, TenantId, "Usuario Estandar");
        var otherRole = CreateRole(iamRepository, OtherTenantId, "Usuario Estandar");

        var otherUser = CreatePersistedLocalUser("bruno@other.test");
        userRepository.Seed(otherUser);
        userCompanyRepository.Add(UserCompanyMembership.Create(otherUser.Id, otherCompany.Id, otherRole.Id, isPrimary: true));

        var handler = new UpdateCompanyUserCommandHandler(
            userRepository,
            userCompanyRepository,
            iamRepository,
            new AllowCompanyUserAuthorizationService(),
            CreateRbacAuthorizationService(),
            new TestTenantContext(TenantId),
            CreateFieldPermissionService(),
            CreateFieldSerializationService(),
            new TestUnitOfWork(),
            CreateAuditService(),
            NullLogger<UpdateCompanyUserCommandHandler>.Instance);

        var result = await handler.Handle(
            new UpdateCompanyUserCommand(otherUser.PublicId, "Bruno", "Tenant", currentRole.PublicId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("TENANT_MISMATCH", result.Error.Code);
    }

    [Fact]
    public async Task Handle_WhenUpdatingNonEditableField_ShouldReturnFieldEditForbidden()
    {
        var userRepository = new TestUserRepository();
        var companyRepository = SeedCompanyRepository(TenantId, "Acme HR");
        var iamRepository = new TestIamAdministrationRepository();
        var userCompanyRepository = new TestUserCompanyRepository(companyRepository, userRepository, iamRepository);

        var currentRole = CreateRole(iamRepository, TenantId, "Usuario Estandar");
        var user = CreatePersistedLocalUser("ana@acme.test");
        userRepository.Seed(user);
        userCompanyRepository.Add(UserCompanyMembership.Create(user.Id, companyRepository.Items[0].Id, currentRole.Id, isPrimary: true));

        var profile = CreateFieldAccessProfile(
            new Dictionary<string, FieldPermissionOverrideState>(StringComparer.OrdinalIgnoreCase)
            {
                [CompanyUserFieldKeys.FirstName] = FieldPermissionEvaluator.NormalizeOverride(
                    isVisible: true,
                    isEditable: false,
                    isRequired: false,
                    isMasked: false)
            },
            new RbacPermissionState(true, true, true, true, true));

        var handler = new UpdateCompanyUserCommandHandler(
            userRepository,
            userCompanyRepository,
            iamRepository,
            new AllowCompanyUserAuthorizationService(),
            CreateRbacAuthorizationService(Result.Failure(
                AuthorizationErrors.FieldEditForbidden(
                    CompanyUserFieldKeys.ResourceKey,
                    RbacPermissionAction.Update,
                    [CompanyUserFieldKeys.FirstName]))),
            new TestTenantContext(TenantId),
            CreateFieldPermissionService(profile),
            CreateFieldSerializationService(),
            new TestUnitOfWork(),
            CreateAuditService(),
            NullLogger<UpdateCompanyUserCommandHandler>.Instance);

        var result = await handler.Handle(
            new UpdateCompanyUserCommand(user.PublicId, "Carla", "Mendoza", currentRole.PublicId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("FIELD_EDIT_FORBIDDEN", result.Error.Code);
    }

    private static TestCompanyRepository SeedCompanyRepository(Guid companyPublicId, string name)
    {
        var repository = new TestCompanyRepository();
        repository.Seed(CreateCompany(companyPublicId, name));
        return repository;
    }

    private static Company CreateCompany(Guid companyPublicId, string name)
    {
        var company = Company.Create(name, name.ToLowerInvariant().Replace(' ', '-'), companyPublicId);
        SetEntityId(company, companyPublicId == TenantId ? 1 : 2);
        SetPrivateProperty(company, nameof(Company.PublicId), companyPublicId);
        return company;
    }

    private static User CreatePersistedLocalUser(string email)
    {
        var user = User.RegisterLocal("Ana", "Mendoza", email, "hashed-password", "SV", "seed");
        SetEntityId(user, Math.Abs(email.GetHashCode()));
        return user;
    }

    private static User CreatePersistedInvitedUser(string email)
    {
        var user = User.InviteLocal("Ana", "Mendoza", email, country: null, source: CompanyUserConstants.InvitationSource);
        SetEntityId(user, Math.Abs(email.GetHashCode()));
        return user;
    }

    private static IamRole CreateRole(TestIamAdministrationRepository repository, Guid tenantId, string name)
    {
        var role = IamRole.Create(name, $"{name} role", isSystemRole: false);
        role.SetTenantId(tenantId);
        repository.AddRole(role);
        return role;
    }

    private static IamRole CreateAdminRole(TestIamAdministrationRepository repository, Guid tenantId, string name)
    {
        var permission = IamPermission.CreateScreenAction(
            CompanyUserPermissionCodes.ManageUsers,
            "Manage users",
            "Allows company user administration.",
            "RBAC",
            "Users",
            "Manage");
        permission.SetTenantId(tenantId);
        repository.AddPermission(permission);

        var role = IamRole.Create(name, $"{name} role", isSystemRole: true);
        role.SetTenantId(tenantId);
        role.SyncPermissions([permission]);
        SetTenantOnPermissionAssignments(role.PermissionAssignments, tenantId);
        BindPermissionAssignments(role, permission);
        repository.AddRole(role);
        return role;
    }

    private static void BindPermissionAssignments(IamRole role, params IamPermission[] permissions)
    {
        foreach (var assignment in role.PermissionAssignments)
        {
            SetPrivateProperty(assignment, nameof(IamRolePermissionAssignment.Role), role);
            SetPrivateProperty(
                assignment,
                nameof(IamRolePermissionAssignment.Permission),
                permissions.Single(permission => permission.Id == assignment.PermissionId));
        }
    }

    private static void SetTenantOnAssignments(IEnumerable<IamUserRoleAssignment> assignments, Guid tenantId)
    {
        foreach (var assignment in assignments)
        {
            assignment.SetTenantId(tenantId);
        }
    }

    private static void SetTenantOnPermissionAssignments(IEnumerable<IamRolePermissionAssignment> assignments, Guid tenantId)
    {
        foreach (var assignment in assignments)
        {
            assignment.SetTenantId(tenantId);
        }
    }

    private static void SetEntityId(Entity entity, long id)
    {
        typeof(Entity)
            .GetProperty(nameof(Entity.Id), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetSetMethod(nonPublic: true)!
            .Invoke(entity, [id]);
    }

    private static void SetPrivateProperty(object target, string propertyName, object? value)
    {
        target.GetType()
            .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetSetMethod(nonPublic: true)!
            .Invoke(target, [value]);
    }

    private sealed class AllowCompanyUserAuthorizationService : ICompanyUserAuthorizationService
    {
        public Task<Result> EnsureCanManageAsync(CancellationToken cancellationToken) => Task.FromResult(Result.Success());

        public Task<Result> EnsureAuthorizedAsync(RbacPermissionAction action, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());
    }

    private sealed class TestTenantContext(Guid? tenantId) : ITenantContext
    {
        public Guid? TenantId { get; } = tenantId;
    }

    private sealed class FixedDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow => utcNow;
    }

    private sealed class TestInvitationTokenHasher : IInvitationTokenHasher
    {
        public string Hash(string token) => $"HASH::{token}";
    }

    private sealed class TestEmailService : IEmailService
    {
        public List<CompanyUserInvitationEmailMessage> Messages { get; } = [];

        public Task SendCompanyUserInvitationAsync(CompanyUserInvitationEmailMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class TestCompanyRepository : ICompanyRepository
    {
        public List<Company> Items { get; } = [];

        public void Add(Company company)
        {
            if (company.Id == 0)
            {
                SetEntityId(company, Items.Count + 1);
            }

            Items.Add(company);
        }

        public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken) =>
            Task.FromResult(Items.Any(company => company.Slug == slug));

        public Task<Company?> FindByPublicIdAsync(Guid companyPublicId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(company => company.PublicId == companyPublicId));

        public void Seed(Company company) => Items.Add(company);
    }

    private sealed class TestUserRepository : IUserRepository
    {
        public List<User> Items { get; } = [];

        public Task<User?> GetByIdAsync(long userId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(user => user.Id == userId));

        public Task<User?> GetByPublicIdAsync(Guid userPublicId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(user => user.PublicId == userPublicId));

        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
        {
            var normalizedEmail = User.NormalizeEmail(email);
            return Task.FromResult(Items.SingleOrDefault(user => user.NormalizedEmail == normalizedEmail));
        }

        public Task<User?> GetByExternalProviderAsync(AuthProvider authProvider, string providerUserId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.SingleOrDefault(user =>
                user.AuthProvider == authProvider &&
                user.ProviderUserId == providerUserId));

        public Task AddAsync(User user, CancellationToken cancellationToken)
        {
            if (user.Id == 0)
            {
                SetEntityId(user, Items.Count + 1);
            }

            Items.Add(user);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void Seed(User user) => Items.Add(user);
    }

    private sealed class TestUserCompanyRepository(
        TestCompanyRepository companyRepository,
        TestUserRepository userRepository,
        TestIamAdministrationRepository iamRepository) : IUserCompanyRepository
    {
        private long _nextId = 1;

        public List<UserCompanyMembership> Items { get; } = [];

        public void Add(UserCompanyMembership membership)
        {
            if (membership.Id == 0)
            {
                SetEntityId(membership, _nextId++);
            }

            Items.Add(membership);
        }

        public Task<bool> ExistsInCompanyAsync(Guid companyPublicId, string normalizedEmail, CancellationToken cancellationToken)
        {
            var company = companyRepository.Items.SingleOrDefault(item => item.PublicId == companyPublicId);
            var user = userRepository.Items.SingleOrDefault(item => item.NormalizedEmail == normalizedEmail);

            var exists = company is not null &&
                user is not null &&
                Items.Any(item => item.CompanyId == company.Id && item.UserId == user.Id);

            return Task.FromResult(exists);
        }

        public Task<bool> HasAnyMembershipAsync(long userId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.Any(item => item.UserId == userId));

        public Task<bool> HasPrimaryCompanyAsync(long userId, CancellationToken cancellationToken) =>
            Task.FromResult(Items.Any(item => item.UserId == userId && item.IsPrimary));

        public Task<Guid?> GetPrimaryCompanyPublicIdAsync(long userId, CancellationToken cancellationToken)
        {
            var membership = Items.SingleOrDefault(item => item.UserId == userId && item.IsPrimary);
            var companyPublicId = membership is null
                ? (Guid?)null
                : companyRepository.Items.Single(item => item.Id == membership.CompanyId).PublicId;

            return Task.FromResult(companyPublicId);
        }

        public Task<UserCompanyMembership?> FindByUserPublicIdAsync(Guid companyPublicId, Guid userPublicId, CancellationToken cancellationToken)
        {
            var company = companyRepository.Items.SingleOrDefault(item => item.PublicId == companyPublicId);
            var user = userRepository.Items.SingleOrDefault(item => item.PublicId == userPublicId);

            var membership = company is null || user is null
                ? null
                : Items.SingleOrDefault(item => item.CompanyId == company.Id && item.UserId == user.Id);

            return Task.FromResult(membership);
        }

        public Task<bool> UserExistsOutsideCompanyAsync(Guid companyPublicId, Guid userPublicId, CancellationToken cancellationToken)
        {
            var companyIds = Items
                .Join(
                    userRepository.Items.Where(user => user.PublicId == userPublicId),
                    membership => membership.UserId,
                    user => user.Id,
                    (membership, _) => membership.CompanyId)
                .Distinct()
                .ToArray();

            if (companyIds.Length == 0)
            {
                return Task.FromResult(false);
            }

            var outside = companyRepository.Items
                .Where(company => companyIds.Contains(company.Id))
                .Any(company => company.PublicId != companyPublicId);

            return Task.FromResult(outside);
        }

        public Task<bool> IsLastActiveAdministratorAsync(Guid companyPublicId, Guid userPublicId, CancellationToken cancellationToken)
        {
            var company = companyRepository.Items.SingleOrDefault(item => item.PublicId == companyPublicId);
            if (company is null)
            {
                return Task.FromResult(false);
            }

            var adminUsers = Items
                .Where(item => item.CompanyId == company.Id && item.Status == UserCompanyStatus.Active)
                .Join(
                    userRepository.Items.Where(user => user.Status == UserStatus.Active),
                    membership => membership.UserId,
                    user => user.Id,
                    (membership, user) => new { user.PublicId, membership.RoleId })
                .Where(item => IsAdministrativeRole(item.RoleId))
                .Select(item => item.PublicId)
                .Distinct()
                .ToList();

            return Task.FromResult(adminUsers.Count == 1 && adminUsers[0] == userPublicId);
        }

        public Task<PagedResponse<CompanyUserSummaryResponse>> GetUsersAsync(
            Guid companyPublicId,
            int pageNumber,
            int pageSize,
            UserStatus? status,
            Guid? roleId,
            string? search,
            CancellationToken cancellationToken)
        {
            var query = BuildQuery(companyPublicId);

            if (status.HasValue)
            {
                query = query.Where(item => item.User.Status == status.Value);
            }

            if (roleId.HasValue)
            {
                query = query.Where(item => item.Role.PublicId == roleId.Value);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var normalizedSearch = search.Trim().ToUpperInvariant();
                query = query.Where(item =>
                    item.User.NormalizedEmail.Contains(normalizedSearch, StringComparison.Ordinal) ||
                    item.User.FirstName.ToUpperInvariant().Contains(normalizedSearch, StringComparison.Ordinal) ||
                    item.User.LastName.ToUpperInvariant().Contains(normalizedSearch, StringComparison.Ordinal));
            }

            var items = query
                .OrderBy(item => item.User.LastName)
                .ThenBy(item => item.User.FirstName)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .Select(item => new CompanyUserSummaryResponse(
                    item.User.PublicId,
                    item.User.Email,
                    item.User.FirstName,
                    item.User.LastName,
                    item.Role.PublicId,
                    item.Role.Name,
                    item.User.Status))
                .ToArray();

            return Task.FromResult(new PagedResponse<CompanyUserSummaryResponse>(items, pageNumber, pageSize, query.Count()));
        }

        public Task<CompanyUserResponse?> GetUserAsync(Guid companyPublicId, Guid userPublicId, CancellationToken cancellationToken)
        {
            var item = BuildQuery(companyPublicId).SingleOrDefault(entry => entry.User.PublicId == userPublicId);
            return Task.FromResult(item is null
                ? null
                : new CompanyUserResponse(
                    item.User.PublicId,
                    item.User.Email,
                    item.User.FirstName,
                    item.User.LastName,
                    item.Role.PublicId,
                    item.Role.Name,
                    item.User.Status));
        }

        private IEnumerable<QueryItem> BuildQuery(Guid companyPublicId)
        {
            var company = companyRepository.Items.SingleOrDefault(item => item.PublicId == companyPublicId);
            if (company is null)
            {
                return [];
            }

            return Items
                .Where(item => item.CompanyId == company.Id)
                .Join(
                    userRepository.Items,
                    membership => membership.UserId,
                    user => user.Id,
                    (membership, user) => new { Membership = membership, User = user })
                .Join(
                    iamRepository.Roles,
                    item => item.Membership.RoleId,
                    role => role.Id,
                    (item, role) => new QueryItem(item.Membership, item.User, role));
        }

        private bool IsAdministrativeRole(long roleId)
        {
            var role = iamRepository.Roles.Single(role => role.Id == roleId);
            return role.PermissionAssignments.Any(assignment =>
                assignment.Permission.NormalizedCode == CompanyUserPermissionCodes.ManageUsers.ToUpperInvariant() ||
                assignment.Permission.NormalizedCode == IdentityPermissionCodes.ManageAdministration.ToUpperInvariant());
        }

        private sealed record QueryItem(UserCompanyMembership Membership, User User, IamRole Role);
    }

    private sealed class TestInvitationTokenRepository : IInvitationTokenRepository
    {
        private long _nextId = 1;

        public List<InvitationToken> Items { get; } = [];

        public void Add(InvitationToken invitationToken)
        {
            if (invitationToken.Id == 0)
            {
                SetEntityId(invitationToken, _nextId++);
            }

            Items.Add(invitationToken);
        }

        public Task RevokeActiveTokensAsync(long userId, long companyId, DateTime revokedUtc, CancellationToken cancellationToken)
        {
            foreach (var invitationToken in Items.Where(item =>
                         item.UserId == userId &&
                         item.CompanyId == companyId &&
                         !item.IsUsed &&
                         item.RevokedUtc is null))
            {
                invitationToken.Revoke(revokedUtc);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class TestRefreshTokenRepository : IRefreshTokenRepository
    {
        public List<long> RevokedUsers { get; } = [];

        public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task RevokeFamilyAsync(Guid familyId, DateTime revokedUtc, string reason, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task RevokeUserTokensAsync(long userId, DateTime revokedUtc, string reason, CancellationToken cancellationToken)
        {
            RevokedUsers.Add(userId);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class TestFieldPermissionService(FieldAccessProfile profile) : IFieldPermissionService
    {
        public Task<Result<ResourceFieldsResponse>> GetResourceFieldsAsync(
            string resourceKey,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<RoleFieldPermissionsResponse>> GetRoleFieldPermissionsAsync(
            Guid roleId,
            string resourceKey,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<RoleFieldPermissionsResponse>> UpsertRoleFieldPermissionsAsync(
            Guid roleId,
            string resourceKey,
            IReadOnlyCollection<RoleFieldPermissionUpdateModel> fields,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Result<FieldAccessProfile>> GetCurrentUserAccessProfileAsync(
            string resourceKey,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result<FieldAccessProfile>.Success(profile));
    }

    private sealed class TestRbacAuthorizationService(Result result) : IRbacAuthorizationService
    {
        public Task<Result> AuthorizeAsync(
            string resourceKey,
            RbacPermissionAction action,
            CancellationToken cancellationToken) =>
            Task.FromResult(result);

        public Task<Result> AuthorizeFieldsAsync(
            string resourceKey,
            RbacPermissionAction action,
            IReadOnlyCollection<string> fieldKeys,
            CancellationToken cancellationToken) =>
            Task.FromResult(result);
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
        private long _nextUserId = 1;
        private long _nextRoleId = 1;
        private long _nextPermissionId = 1;

        public List<IamUser> Users { get; } = [];
        public List<IamRole> Roles { get; } = [];
        public List<IamPermission> Permissions { get; } = [];

        public void AddUser(IamUser user)
        {
            if (user.Id == 0)
            {
                SetEntityId(user, _nextUserId++);
            }

            Users.Add(user);
        }

        public void AddRole(IamRole role)
        {
            if (role.Id == 0)
            {
                SetEntityId(role, _nextRoleId++);
            }

            foreach (var assignment in role.PermissionAssignments)
            {
                SetPrivateProperty(assignment, nameof(IamRolePermissionAssignment.Role), role);
            }

            Roles.Add(role);
        }

        public void RemoveRole(IamRole role) => Roles.Remove(role);

        public void AddPermission(IamPermission permission)
        {
            if (permission.Id == 0)
            {
                SetEntityId(permission, _nextPermissionId++);
            }

            Permissions.Add(permission);
        }

        public void AddPermissionAuditLog(RbacPermissionAuditLog auditLog)
        {
        }

        public Task<bool> UserEmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<bool> RoleNameExistsAsync(string normalizedRoleName, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<bool> PermissionCodeExistsAsync(string normalizedPermissionCode, CancellationToken cancellationToken) => Task.FromResult(false);

        public Task<bool> UserPublicIdExistsAsync(Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(Users.Any(user => user.PublicId == userId));

        public Task<bool> RolePublicIdExistsAsync(Guid roleId, CancellationToken cancellationToken) =>
            Task.FromResult(Roles.Any(role => role.PublicId == roleId));

        public Task<bool> PermissionPublicIdExistsAsync(Guid permissionId, CancellationToken cancellationToken) =>
            Task.FromResult(Permissions.Any(permission => permission.PublicId == permissionId));

        public Task<IamUser?> FindUserByPublicIdAsync(Guid userId, bool includeRoles, CancellationToken cancellationToken) =>
            Task.FromResult(Users.SingleOrDefault(user => user.PublicId == userId));

        public Task<IamRole?> FindRoleByPublicIdAsync(Guid roleId, bool includePermissions, CancellationToken cancellationToken) =>
            Task.FromResult(Roles.SingleOrDefault(role => role.PublicId == roleId));

        public Task<IamPermission?> FindPermissionByPublicIdAsync(Guid permissionId, CancellationToken cancellationToken) =>
            Task.FromResult(Permissions.SingleOrDefault(permission => permission.PublicId == permissionId));

        public Task<IReadOnlyList<IamRole>> GetRolesByPublicIdsAsync(IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<IamRole>>(Roles.Where(role => roleIds.Contains(role.PublicId)).ToArray());

        public Task<IReadOnlyList<IamUser>> GetUsersByPublicIdsAsync(
            IReadOnlyCollection<Guid> userIds,
            bool includeRoles,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<IamUser>>(Users.Where(user => userIds.Contains(user.PublicId)).ToArray());

        public Task<IReadOnlyList<IamUser>> GetUsersAssignedToRoleAsync(
            Guid roleId,
            bool includeRoles,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<IamUser>>(Users
                .Where(user => user.RoleAssignments.Any(assignment => Roles.Single(role => role.Id == assignment.RoleId).PublicId == roleId))
                .ToArray());

        public Task<IReadOnlyList<IamUser>> GetActiveUsersAsync(
            bool includeRoles,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<IamUser>>(Users.Where(static user => user.IsActive).ToArray());

        public Task<IReadOnlyCollection<Guid>> GetActiveAdministratorUserIdsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyCollection<Guid>>(Users
                .Where(user => user.IsActive)
                .Where(user => user.RoleAssignments.Any(assignment =>
                    Roles.Single(role => role.Id == assignment.RoleId).PermissionAssignments.Any(permissionAssignment =>
                        Permissions.Single(permission => permission.Id == permissionAssignment.PermissionId).NormalizedCode ==
                            IdentityPermissionCodes.ManageAdministration.ToUpperInvariant() ||
                        Permissions.Single(permission => permission.Id == permissionAssignment.PermissionId).NormalizedCode ==
                            IdentityPermissionCodes.ManageUsers.ToUpperInvariant())))
                .Select(user => user.PublicId)
                .Distinct()
                .ToArray());

        public Task<IReadOnlyList<IamPermission>> GetPermissionsByNormalizedCodesAsync(
            IReadOnlyCollection<string> normalizedPermissionCodes,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<IamPermission>>(Permissions
                .Where(permission => normalizedPermissionCodes.Contains(permission.NormalizedCode))
                .ToArray());

        public Task<IReadOnlyList<IamPermission>> GetPermissionsByPublicIdsAsync(IReadOnlyCollection<Guid> permissionIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<IamPermission>>(Permissions.Where(permission => permissionIds.Contains(permission.PublicId)).ToArray());

        public Task<PagedResponse<IamUserSummaryResponse>> GetUsersAsync(int pageNumber, int pageSize, string? search, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IamUserResponse?> GetUserAsync(Guid userId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PagedResponse<IamRoleSummaryResponse>> GetRolesAsync(int pageNumber, int pageSize, string? search, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IamRoleResponse?> GetRoleAsync(Guid roleId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PagedResponse<IamPermissionSummaryResponse>> GetPermissionsAsync(int pageNumber, int pageSize, string? search, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IamPermissionResponse?> GetPermissionAsync(Guid permissionId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<RbacResource>> GetActiveRbacResourcesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RbacResource>>([]);

        public Task<PagedResponse<RbacPermissionAuditLog>> GetPermissionAuditLogsAsync(
            Guid? roleId,
            string? normalizedResourceKey,
            DateTime? fromUtc,
            DateTime? toUtc,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken) =>
            Task.FromResult(new PagedResponse<RbacPermissionAuditLog>([], pageNumber, pageSize, 0));

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => Task.FromResult(1);
    }
}

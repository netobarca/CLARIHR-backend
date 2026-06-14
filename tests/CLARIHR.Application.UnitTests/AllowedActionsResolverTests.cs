using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.CompetencyFramework.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Infrastructure.Policies;

namespace CLARIHR.Application.UnitTests;

public sealed class AllowedActionsResolverTests
{
    [Fact]
    public void Resolve_UnknownResourceKey_ReturnsNull()
    {
        var resolver = CreateResolver();

        Assert.Null(resolver.Resolve("DOES_NOT_EXIST", dto: null));
    }

    [Fact]
    public void Resolve_PolicyResource_WithAdmin_AllowsManageActions()
    {
        var resolver = CreateResolver(CompetencyFrameworkPermissionCodes.Admin);

        var result = resolver.Resolve(CompetencyFrameworkPermissionCodes.ResourceKey, new ActiveDto());

        Assert.NotNull(result);
        Assert.True(result!.CanView);
        Assert.True(result.CanCreate);
        Assert.True(result.CanEdit);
        Assert.True(result.CanInactivate);
        Assert.False(result.CanActivate); // already active
    }

    [Fact]
    public void Resolve_PolicyResource_WithReadOnly_AllowsViewButNotManage()
    {
        var resolver = CreateResolver(CompetencyFrameworkPermissionCodes.Read);

        var result = resolver.Resolve(CompetencyFrameworkPermissionCodes.ResourceKey, new ActiveDto());

        Assert.NotNull(result);
        Assert.True(result!.CanView);
        Assert.False(result.CanEdit);
        Assert.False(result.CanCreate);
        Assert.Contains(AllowedActionReasonCodes.NotAuthorized, result.Reasons);
    }

    [Fact]
    public void Resolve_PolicyResource_WithNoPermissions_DeniesAll()
    {
        var resolver = CreateResolver();

        var result = resolver.Resolve(CompetencyFrameworkPermissionCodes.ResourceKey, new ActiveDto());

        Assert.NotNull(result);
        Assert.False(result!.CanView);
        Assert.False(result.CanEdit);
        Assert.False(result.CanCreate);
    }

    [Fact]
    public void Resolve_RbacResource_EnforcesAccessGate()
    {
        var access = PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Users, RbacPermissionAction.Access);
        var update = PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Users, RbacPermissionAction.Update);

        // UPDATE without the ACCESS gate must NOT advertise edit (mirrors enforcement).
        var withoutAccess = CreateResolver(update).Resolve("RBAC_USERS", dto: null);
        Assert.NotNull(withoutAccess);
        Assert.False(withoutAccess!.CanEdit);

        // ACCESS + UPDATE advertises edit.
        var withAccess = CreateResolver(access, update).Resolve("RBAC_USERS", dto: null);
        Assert.NotNull(withAccess);
        Assert.True(withAccess!.CanEdit);
    }

    [Fact]
    public void Resolve_PopulatesActionPermissionsDetail()
    {
        var resolver = CreateResolver(CompetencyFrameworkPermissionCodes.Admin);

        var result = resolver.Resolve(CompetencyFrameworkPermissionCodes.ResourceKey, new ActiveDto());

        Assert.NotNull(result);
        var update = Assert.Single(result!.ActionPermissions, permission => permission.Action == "Update");
        Assert.True(update.Allowed);
        Assert.Equal(CompetencyFrameworkPermissionCodes.Admin, update.PermissionCode);
    }

    [Fact]
    public void Resolve_SystemRecord_BlocksEditAndDelete_EvenWithFullPermissions()
    {
        var access = PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Roles, RbacPermissionAction.Access);
        var update = PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Roles, RbacPermissionAction.Update);
        var delete = PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Roles, RbacPermissionAction.Delete);
        var resolver = CreateResolver(access, update, delete);

        var result = resolver.Resolve("RBAC_ROLES", new SystemRecordDto());

        Assert.NotNull(result);
        Assert.False(result!.CanEdit);
        Assert.False(result.CanDelete);
        Assert.Contains(AllowedActionReasonCodes.SystemRecord, result.Reasons);
    }

    private static AllowedActionsResolver CreateResolver(params string[] permissions) =>
        new(new StubCurrentUserService(permissions), new ResourceActionPolicyService());

    private sealed class StubCurrentUserService(IReadOnlyCollection<string> permissions) : ICurrentUserService
    {
        public bool IsAuthenticated => true;

        public string? UserId => "00000000-0000-0000-0000-000000000001";

        public IReadOnlyCollection<string> Roles => [];

        public IReadOnlyCollection<string> Permissions { get; } = permissions;
    }

    private sealed record ActiveDto : IHasActivationState
    {
        public bool IsActive => true;
    }

    // Exposes a conventional IsSystemRole the resolver reads generically (no marker needed).
    private sealed record SystemRecordDto
    {
        public bool IsSystemRole => true;
    }
}

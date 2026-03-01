using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.UnitTests;

public sealed class RbacAuthorizationEvaluatorTests
{
    [Fact]
    public void IsAllowed_WhenScreenAccessAndActionAreGranted_ShouldReturnTrue()
    {
        var grantedCodes = new[]
        {
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Users, RbacPermissionAction.Access),
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Users, RbacPermissionAction.Read)
        };

        var result = RbacAuthorizationEvaluator.IsAllowed(grantedCodes, RbacPermissionScreen.Users, RbacPermissionAction.Read);

        Assert.True(result);
    }

    [Fact]
    public void IsAllowed_WhenActionIsGrantedWithoutAccess_ShouldReturnFalse()
    {
        var grantedCodes = new[]
        {
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Users, RbacPermissionAction.Read)
        };

        var result = RbacAuthorizationEvaluator.IsAllowed(grantedCodes, RbacPermissionScreen.Users, RbacPermissionAction.Read);

        Assert.False(result);
    }

    [Fact]
    public void IsAllowed_WhenManageOverrideIsGranted_ShouldReturnTrue()
    {
        var result = RbacAuthorizationEvaluator.IsAllowed(
            [IdentityPermissionCodes.ManagePermissions],
            RbacPermissionScreen.Permissions,
            RbacPermissionAction.Update);

        Assert.True(result);
    }

    [Fact]
    public void IsRbacSecurityAdministrator_WhenRoleAndPermissionManagementExist_ShouldReturnTrue()
    {
        var grantedCodes = new[]
        {
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Roles, RbacPermissionAction.Access),
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Roles, RbacPermissionAction.Update),
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Permissions, RbacPermissionAction.Access),
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Permissions, RbacPermissionAction.Update)
        };

        var result = RbacAuthorizationEvaluator.IsRbacSecurityAdministrator(grantedCodes);

        Assert.True(result);
    }

    [Fact]
    public void IsRbacSecurityAdministrator_WhenPermissionsManagementIsMissing_ShouldReturnFalse()
    {
        var grantedCodes = new[]
        {
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Roles, RbacPermissionAction.Access),
            PermissionMatrixCatalog.BuildPermissionCode(RbacPermissionScreen.Roles, RbacPermissionAction.Update)
        };

        var result = RbacAuthorizationEvaluator.IsRbacSecurityAdministrator(grantedCodes);

        Assert.False(result);
    }
}

using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.IdentityAccess.Roles;
using CLARIHR.Application.Features.IdentityAccess.Users;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// A-4 (security audit doc 27): the IAM role/user write validators bound the *count* of submitted ids so
/// one privileged request cannot drive an unbounded <c>WHERE PublicId IN (@p0…@pN)</c> against
/// <c>iam_permissions</c>/<c>iam_roles</c>. Drift-proof: each boundary is built from the
/// <see cref="IdentityAccessValidationRules"/> constant (Max accepted, Max+1 rejected), so removing a cap
/// rule or moving a bound off the constant turns one of these red. Mirrors
/// <c>CompetencyFrameworkCollectionCapGuardrailTests</c>.
/// </summary>
public sealed class IdentityAccessCollectionCapGuardrailTests
{
    [Fact]
    public void CreateRoleValidator_WhenPermissionsExceedMax_ShouldReportPermissionIdsViolation()
    {
        var validator = new CreateIamRoleCommandValidator();

        var result = validator.Validate(new CreateIamRoleCommand(
            "Role",
            Description: null,
            Guids(IdentityAccessValidationRules.MaxPermissionIdsPerRole + 1)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "PermissionIds");
    }

    [Fact]
    public void CreateRoleValidator_WhenPermissionsAtMax_ShouldBeValid()
    {
        var validator = new CreateIamRoleCommandValidator();

        var result = validator.Validate(new CreateIamRoleCommand(
            "Role",
            Description: null,
            Guids(IdentityAccessValidationRules.MaxPermissionIdsPerRole)));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void SyncRolePermissionsValidator_WhenPermissionsExceedMax_ShouldReportPermissionIdsViolation()
    {
        var validator = new SyncIamRolePermissionsCommandValidator();

        var result = validator.Validate(new SyncIamRolePermissionsCommand(
            Guid.NewGuid(),
            Guids(IdentityAccessValidationRules.MaxPermissionIdsPerRole + 1),
            Guid.NewGuid()));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "PermissionIds");
    }

    [Fact]
    public void SyncRolePermissionsValidator_WhenPermissionsAtMax_ShouldBeValid()
    {
        var validator = new SyncIamRolePermissionsCommandValidator();

        var result = validator.Validate(new SyncIamRolePermissionsCommand(
            Guid.NewGuid(),
            Guids(IdentityAccessValidationRules.MaxPermissionIdsPerRole),
            Guid.NewGuid()));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void SyncUserRolesValidator_WhenRolesExceedMax_ShouldReportRoleIdsViolation()
    {
        var validator = new SyncIamUserRolesCommandValidator();

        var result = validator.Validate(new SyncIamUserRolesCommand(
            Guid.NewGuid(),
            Guids(IdentityAccessValidationRules.MaxRoleIdsPerUser + 1)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "RoleIds");
    }

    [Fact]
    public void SyncUserRolesValidator_WhenRolesAtMax_ShouldBeValid()
    {
        var validator = new SyncIamUserRolesCommandValidator();

        var result = validator.Validate(new SyncIamUserRolesCommand(
            Guid.NewGuid(),
            Guids(IdentityAccessValidationRules.MaxRoleIdsPerUser)));

        Assert.True(result.IsValid);
    }

    private static Guid[] Guids(int count) =>
        Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray();
}

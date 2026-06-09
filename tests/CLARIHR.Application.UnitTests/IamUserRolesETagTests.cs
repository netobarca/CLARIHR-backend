using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.IdentityAccess.Contracts;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// A-6 (security audit doc 27): unit coverage for the weak computed ETag that guards the user-roles
/// write surface (the user-roles PUT has no persisted concurrency token). Mirrors
/// <c>CompanyUserETagTests</c> — determinism, role-order independence, rotation on any observable change,
/// and the RFC 7232 <c>*</c> wildcard.
/// </summary>
public sealed class IamUserRolesETagTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid RoleA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid RoleB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static IamUserResponse Projection(
        string firstName = "Ada",
        string lastName = "Lovelace",
        bool isActive = true,
        params Guid[] roleIds) =>
        new(
            UserId,
            "ada@example.com",
            firstName,
            lastName,
            isActive,
            roleIds
                .Select(id => new IamUserRoleResponse(id, id.ToString(), null, false, Array.Empty<IamPermissionReferenceResponse>()))
                .ToArray());

    [Fact]
    public void Compute_IsDeterministic_ForTheSameProjection()
    {
        var first = IamUserRolesETag.Compute(Projection(roleIds: RoleA));
        var second = IamUserRolesETag.Compute(Projection(roleIds: RoleA));

        Assert.Equal(first, second);
    }

    [Fact]
    public void Compute_IsIndependentOfRoleOrder()
    {
        var ordered = IamUserRolesETag.Compute(Projection(roleIds: new[] { RoleA, RoleB }));
        var reversed = IamUserRolesETag.Compute(Projection(roleIds: new[] { RoleB, RoleA }));

        Assert.Equal(ordered, reversed);
    }

    [Fact]
    public void Compute_ChangesWhenRoleSetChanges()
    {
        // The correctness mitigation: a role change must rotate the ETag even though no aggregate
        // timestamp is hashed (the user-roles write only mutates the role set).
        var before = IamUserRolesETag.Compute(Projection(roleIds: RoleA));
        var after = IamUserRolesETag.Compute(Projection(roleIds: new[] { RoleA, RoleB }));

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void Compute_ChangesWhenProfileOrStatusChanges()
    {
        var baseline = IamUserRolesETag.Compute(Projection(roleIds: RoleA));

        Assert.NotEqual(baseline, IamUserRolesETag.Compute(Projection(firstName: "Grace", roleIds: RoleA)));
        Assert.NotEqual(baseline, IamUserRolesETag.Compute(Projection(lastName: "Hopper", roleIds: RoleA)));
        Assert.NotEqual(baseline, IamUserRolesETag.Compute(Projection(isActive: false, roleIds: RoleA)));
    }

    [Fact]
    public void Matches_ReturnsTrue_OnlyForTheCurrentHash()
    {
        var projection = Projection(roleIds: RoleA);
        var current = IamUserRolesETag.Compute(projection);

        Assert.True(IamUserRolesETag.Matches(current, projection));
        Assert.True(IamUserRolesETag.Matches("*", projection)); // RFC 7232 wildcard matches any representation
        Assert.False(IamUserRolesETag.Matches("not-the-hash", projection));
        Assert.False(IamUserRolesETag.Matches("", projection));
    }
}

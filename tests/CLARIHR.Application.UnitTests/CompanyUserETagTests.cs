using CLARIHR.Application.Features.CompanyUsers;
using CLARIHR.Application.Features.CompanyUsers.Common;
using CLARIHR.Domain.Auth;

namespace CLARIHR.Application.UnitTests;

public sealed class CompanyUserETagTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid RoleA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid RoleB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static CompanyUserResponse Projection(
        string? firstName = "Ada",
        string? lastName = "Lovelace",
        UserStatus? status = UserStatus.Active,
        params Guid[] roleIds) =>
        new(
            UserId,
            "ada@example.com",
            firstName,
            lastName,
            roleIds.Select(id => new CompanyUserRoleResponse(id, id.ToString(), null, false)).ToArray(),
            status);

    [Fact]
    public void Compute_IsDeterministic_ForTheSameProjection()
    {
        var first = CompanyUserETag.Compute(Projection(roleIds: RoleA));
        var second = CompanyUserETag.Compute(Projection(roleIds: RoleA));

        Assert.Equal(first, second);
    }

    [Fact]
    public void Compute_IsIndependentOfRoleOrder()
    {
        var ordered = CompanyUserETag.Compute(Projection(roleIds: new[] { RoleA, RoleB }));
        var reversed = CompanyUserETag.Compute(Projection(roleIds: new[] { RoleB, RoleA }));

        Assert.Equal(ordered, reversed);
    }

    [Fact]
    public void Compute_ChangesWhenRoleSetChanges()
    {
        // The correctness mitigation: a role change must rotate the ETag even though no aggregate
        // timestamp is hashed.
        var before = CompanyUserETag.Compute(Projection(roleIds: RoleA));
        var after = CompanyUserETag.Compute(Projection(roleIds: new[] { RoleA, RoleB }));

        Assert.NotEqual(before, after);
    }

    [Fact]
    public void Compute_ChangesWhenProfileOrStatusChanges()
    {
        var baseline = CompanyUserETag.Compute(Projection(roleIds: RoleA));

        Assert.NotEqual(baseline, CompanyUserETag.Compute(Projection(firstName: "Grace", roleIds: RoleA)));
        Assert.NotEqual(baseline, CompanyUserETag.Compute(Projection(lastName: "Hopper", roleIds: RoleA)));
        Assert.NotEqual(baseline, CompanyUserETag.Compute(Projection(status: UserStatus.Inactive, roleIds: RoleA)));
    }

    [Fact]
    public void Matches_ReturnsTrue_OnlyForTheCurrentHash()
    {
        var projection = Projection(roleIds: RoleA);
        var current = CompanyUserETag.Compute(projection);

        Assert.True(CompanyUserETag.Matches(current, projection));
        Assert.True(CompanyUserETag.Matches("*", projection)); // RFC 7232 wildcard matches any representation
        Assert.False(CompanyUserETag.Matches("not-the-hash", projection));
        Assert.False(CompanyUserETag.Matches("", projection));
    }
}

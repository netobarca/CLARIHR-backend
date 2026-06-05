using System.Text.Json;
using CLARIHR.Application.Features.CompanyUsers;
using CLARIHR.Domain.Auth;

namespace CLARIHR.Application.UnitTests;

public sealed class CompanyUserPatchApplierTests
{
    private static readonly Guid RoleId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherRoleId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static CompanyUserPatchState CreateState() =>
        CompanyUserPatchState.From(new CompanyUserResponse(
            Guid.NewGuid(),
            "ana@acme.test",
            "Ana",
            "Mendoza",
            new[] { new CompanyUserRoleResponse(RoleId, "Viewer", null, false) },
            UserStatus.Active));

    private static CompanyUserPatchOperation Op(string op, string path, object? value) =>
        new(op, path, null, value is null ? null : JsonSerializer.SerializeToElement(value));

    [Fact]
    public void Apply_ReplaceFirstAndLastName_UpdatesState()
    {
        var state = CreateState();

        var result = CompanyUserPatchApplier.Apply(
            new[]
            {
                Op("replace", "/firstName", "Patched"),
                Op("replace", "/lastName", "Name")
            },
            state);

        Assert.True(result.IsSuccess);
        Assert.Equal("Patched", state.FirstName);
        Assert.Equal("Name", state.LastName);
    }

    [Fact]
    public void Apply_ReplaceRolePublicIds_ReplacesTheFullSet()
    {
        var state = CreateState();

        var result = CompanyUserPatchApplier.Apply(
            new[] { Op("replace", "/rolePublicIds", new[] { OtherRoleId }) },
            state);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { OtherRoleId }, state.RolePublicIds);
    }

    [Theory]
    [InlineData("/email")]
    [InlineData("/status")]
    [InlineData("/isActive")]
    [InlineData("/id")]
    [InlineData("/publicId")]
    [InlineData("/roles")]
    public void Apply_DisallowedOrReadOnlyPath_ReturnsFailure(string path)
    {
        var state = CreateState();

        var result = CompanyUserPatchApplier.Apply(
            new[] { Op("replace", path, "whatever") },
            state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_RemoveFirstName_ReturnsFailure()
    {
        var state = CreateState();

        var result = CompanyUserPatchApplier.Apply(
            new[] { new CompanyUserPatchOperation("remove", "/firstName", null, null) },
            state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_RolePublicIds_NonArrayValue_ReturnsFailure()
    {
        var state = CreateState();

        var result = CompanyUserPatchApplier.Apply(
            new[] { Op("replace", "/rolePublicIds", "not-an-array") },
            state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_FirstName_NonStringValue_ReturnsFailure()
    {
        var state = CreateState();

        var result = CompanyUserPatchApplier.Apply(
            new[] { Op("replace", "/firstName", 42) },
            state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_UnknownPath_ReturnsFailure()
    {
        var state = CreateState();

        var result = CompanyUserPatchApplier.Apply(
            new[] { Op("replace", "/unknown", "x") },
            state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_NestedPath_ReturnsFailure()
    {
        var state = CreateState();

        var result = CompanyUserPatchApplier.Apply(
            new[] { Op("replace", "/roles/0/name", "x") },
            state);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public void Apply_UnsupportedOperation_ReturnsFailure()
    {
        var state = CreateState();

        var result = CompanyUserPatchApplier.Apply(
            new[] { Op("copy", "/firstName", "x") },
            state);

        Assert.True(result.IsFailure);
    }
}

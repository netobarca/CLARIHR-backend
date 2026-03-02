using CLARIHR.Domain.Locations;

namespace CLARIHR.Application.UnitTests;

public sealed class LocationDomainTests
{
    [Fact]
    public void LocationLevel_Create_WhenRequiredButInactive_ShouldThrow()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            LocationLevel.Create(
                levelOrder: 1,
                displayName: "General",
                isActive: false,
                isRequired: true,
                allowsWorkCenters: false));

        Assert.Equal("Required levels must be active.", exception.Message);
    }

    [Fact]
    public void LocationGroup_Update_WhenDefaultIdentityChanges_ShouldThrow()
    {
        var group = LocationGroup.Create(
            levelOrder: 1,
            code: "GENERAL",
            name: "General",
            parentId: null,
            description: "Default group",
            isDefault: true);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            group.Update("GENERAL-2", "General Updated", "Changed"));

        Assert.Equal("The default group identity cannot be changed.", exception.Message);
    }

    [Fact]
    public void LocationGroup_Inactivate_WhenDefaultGroup_ShouldThrow()
    {
        var group = LocationGroup.Create(
            levelOrder: 1,
            code: "GENERAL",
            name: "General",
            parentId: null,
            description: "Default group",
            isDefault: true);

        var exception = Assert.Throws<InvalidOperationException>(() => group.Inactivate());

        Assert.Equal("The default group cannot be inactivated.", exception.Message);
    }
}

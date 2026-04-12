using CLARIHR.Infrastructure.Locations;

namespace CLARIHR.Application.UnitTests;

public sealed class LocationSeedTemplateTests
{
    [Fact]
    public void CountryLocationTemplateRegistry_ForSV_ShouldExpose14DepartmentsAnd44Municipalities()
    {
        var found = CountryLocationTemplateRegistry.TryGet("SV", out var template);

        Assert.True(found);
        Assert.Equal("SV", template.CountryCode);

        var departments = template.Root.Children.ToArray();
        Assert.Equal(14, departments.Length);

        var municipalities = departments.SelectMany(static department => department.Children).ToArray();
        Assert.Equal(44, municipalities.Length);
        Assert.Contains(departments, static department => department.Code == "SAN_SALVADOR");
        Assert.Contains(municipalities, static municipality => municipality.Code == "SAN_SALVADOR_CENTRO");
    }
}

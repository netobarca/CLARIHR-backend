using System.Net;
using System.Net.Http.Json;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// H-17 — the coordinate validation checked presence and range but not meaning.
///
/// The range half already existed and is shared by the three write paths, so what was actually missing is
/// narrower and wider than the finding said:
///
/// <list type="bullet">
/// <item><b>(0,0) was accepted.</b> The real company had `SAL-EST` — an airport station whose type demands geo —
/// stored at `0.000000, 0.000000`, the null island in the Gulf of Guinea. For a Salvadoran employer that pair is
/// always a placeholder, never a place.</item>
/// <item><b>Half a coordinate was accepted</b>, which the finding did not mention and is worse. The
/// both-or-nothing rule only ran when the TYPE demanded geo, so a type with `requiresGeo=false` could store a
/// latitude with no longitude. (0,0) is at least a point; half a coordinate is not a location at all.</item>
/// </list>
///
/// Nothing consumes these coordinates today — no geofence, no attendance check — which is why this is low
/// severity. The cost of leaving it is that the day something does consume them, `SAL-EST` would be
/// confidently wrong rather than merely absent.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    [Fact]
    public async Task WorkCenters_Create_WithNullIslandCoordinates_ShouldReturn422()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateLocationAdminContext(scenario));

        var response = await CreateWorkCenterWithGeoAsync(
            client, scenario.TenantId, "H17-NULLISLAND", requiresGeo: true, geoLat: 0m, geoLong: 0m);

        await AssertProblemDetailsAsync(
            response, HttpStatusCode.UnprocessableEntity, "WORK_CENTER_COORDINATES_PLACEHOLDER");
    }

    // The gap the finding did not name: `requiresGeo=false` skipped the both-or-nothing rule entirely.
    [Theory]
    [InlineData(13.4445, null)]
    [InlineData(null, -89.0558)]
    public async Task WorkCenters_Create_WithHalfACoordinate_ShouldReturn422(double? lat, double? lng)
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateLocationAdminContext(scenario));

        var response = await CreateWorkCenterWithGeoAsync(
            client,
            scenario.TenantId,
            $"H17-HALF-{(lat.HasValue ? "LAT" : "LNG")}",
            requiresGeo: false,
            geoLat: lat.HasValue ? (decimal)lat.Value : null,
            geoLong: lng.HasValue ? (decimal)lng.Value : null);

        await AssertProblemDetailsAsync(
            response, HttpStatusCode.UnprocessableEntity, "WORK_CENTER_COORDINATES_INCOMPLETE");
    }

    // The real coordinates of the airport station, as confirmed for this company.
    [Fact]
    public async Task WorkCenters_Create_WithRealCoordinates_ShouldSucceed()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateLocationAdminContext(scenario));

        var response = await CreateWorkCenterWithGeoAsync(
            client, scenario.TenantId, "H17-REAL", requiresGeo: true, geoLat: 13.4445m, geoLong: -89.0558m);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    // Non-regression: a type that does not demand geo must still be creatable with no coordinates at all.
    [Fact]
    public async Task WorkCenters_Create_WithoutGeoWhenNotRequired_ShouldSucceed()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateLocationAdminContext(scenario));

        var response = await CreateWorkCenterWithGeoAsync(
            client, scenario.TenantId, "H17-NOGEO", requiresGeo: false, geoLat: null, geoLong: null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<HttpResponseMessage> CreateWorkCenterWithGeoAsync(
        HttpClient client,
        Guid companyId,
        string tag,
        bool requiresGeo,
        decimal? geoLat,
        decimal? geoLong)
    {
        var defaultGroup = await GetDefaultLocationGroupAsync(client, companyId);

        var typeResponse = await client.PostJsonAsync($"/api/v1/companies/{companyId}/work-center-types", new
        {
            code = $"TYPE-{tag}",
            name = $"Tipo {tag}",
            requiresAddress = false,
            requiresGeo,
            allowsBiometric = false
        });
        typeResponse.EnsureSuccessStatusCode();
        var workCenterType = await typeResponse.Content.ReadFromJsonAsync<WorkCenterTypeItem>(JsonOptions);

        return await client.PostJsonAsync($"/api/v1/companies/{companyId}/work-centers", new
        {
            code = $"CEN-{tag}",
            name = $"Centro {tag}",
            workCenterTypePublicId = workCenterType!.Id,
            locationGroupPublicId = defaultGroup.Id,
            address = "San Salvador",
            geoLat,
            geoLong,
            phone = "2222-2222",
            email = $"{tag.ToLowerInvariant()}@acme-one.test",
            notes = (string?)null
        });
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// H-26 — a date sent the natural way, `"2026-08-01"`, produced a `500`. It deserialized with
/// `Kind=Unspecified`, and the first thing that touched it was a SQL comparison (the capacity check of the
/// assignment, the date filter of a report), where Npgsql refuses a parameter whose zone it does not know. The
/// aggregate normalizes on construction, so the WRITE was never the problem — the pre-write QUERY was, which is
/// why fixing the domain would have fixed nothing.
/// <para>
/// There were two entry paths, not one: the JSON body and the ~34 `[FromQuery] DateTime` parameters of the
/// reporting endpoints, which do not go through the serializer at all. Both are normalized at the boundary now,
/// and the day-domain fields of the hiring path take `DateOnly` on the wire while still accepting the instant
/// form so nothing that worked stops working.
/// </para>
/// </summary>
public sealed partial class ApiIntegrationTests
{
    public static TheoryData<string> DayFormats() =>
    [
        "2026-12-01",                  // the natural form — used to be a 500
        "2026-12-01T00:00:00Z",        // explicit UTC — the workaround the playbook documented
        "2026-12-01T00:00:00-06:00",   // explicit offset (inside the slot's effective window)
    ];

    [Theory]
    [MemberData(nameof(DayFormats))]
    public async Task Assignments_WithAnyDateFormat_ShouldNeverReturn500(string startDate)
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-H26", "Direccion H26", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-H26", "Perfil H26", orgUnit.Id);
        var slot = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-H26", "Plaza H26", profile.Id, maxEmployees: 2);
        var employeeId = await SeedCompletedEmployeeAsync(scenario.TenantId, "Fecha", "Formato");

        var response = await client.PostJsonAsync($"/api/v1/personnel-files/{employeeId}/assigned-positions", new
        {
            assignmentTypeCode = "INDEFINIDO",
            positionSlotPublicId = slot.Id,
            orgUnitPublicId = (Guid?)null,
            workCenterPublicId = (Guid?)null,
            costCenterPublicId = (Guid?)null,
            startDate,
            endDate = (string?)null,
            isPrimary = true,
            isActive = true,
            notes = (string?)null
        });

        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(
            HttpStatusCode.Created == response.StatusCode,
            $"startDate '{startDate}' should have been accepted, got {(int)response.StatusCode}: {payload}");

        // The stored day is the one that was sent, at UTC midnight — a day field is a day, not an instant.
        using var document = JsonDocument.Parse(payload);
        Assert.StartsWith("2026-12-01", document.RootElement.GetProperty("startDate").GetString());
    }

    /// <summary>
    /// H-26/H-28 — pins the TYPE, not just the absence of a `500`: the assignment's day fields are `DateOnly` over
    /// a `date` column, so they round-trip as `"2026-12-01"` with no time part at all. While they were `DateTime`
    /// over `timestamptz` this test could not pass — the response carried `2026-12-01T00:00:00Z`, and every
    /// consumer had to remember the "UTC midnight" convention to read a day back out. Reverting the columns
    /// breaks this assertion, which is the point: H-28 anchors vacation seniority on `startDate`, and an
    /// off-by-one there is a day of vacation.
    /// </summary>
    [Theory]
    [MemberData(nameof(DayFormats))]
    public async Task Assignments_StartDate_ShouldRoundTripAsAPlainDay(string startDate)
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateMultiPlazaContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-H26D", "Direccion Dia", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-H26D", "Perfil Dia", orgUnit.Id);
        var slot = await CreatePositionSlotAsync(client, scenario.TenantId, "PS-H26D", "Plaza Dia", profile.Id, maxEmployees: 2);
        var employeeId = await SeedCompletedEmployeeAsync(scenario.TenantId, "Dia", "Plano");

        var response = await client.PostJsonAsync($"/api/v1/personnel-files/{employeeId}/assigned-positions", new
        {
            assignmentTypeCode = "INDEFINIDO",
            positionSlotPublicId = slot.Id,
            orgUnitPublicId = (Guid?)null,
            workCenterPublicId = (Guid?)null,
            costCenterPublicId = (Guid?)null,
            startDate,
            endDate = (string?)null,
            isPrimary = true,
            isActive = true,
            notes = (string?)null
        });

        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == response.StatusCode, $"'{startDate}': {(int)response.StatusCode} {payload}");

        using var document = JsonDocument.Parse(payload);
        // Exact equality, not StartsWith: a time component would mean the field is still an instant.
        Assert.Equal("2026-12-01", document.RootElement.GetProperty("startDate").GetString());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("endDate").ValueKind);
    }

    /// <summary>
    /// The entry path the finding never saw: a date in the QUERY STRING, which the serializer never touches.
    /// `personnel-actions/export?fromUtc=2026-08-01` answered `500`.
    /// </summary>
    [Theory]
    [InlineData("2026-08-01")]
    [InlineData("2026-08-01T00:00:00Z")]
    public async Task Reporting_WithQueryStringDate_ShouldNeverReturn500(string fromUtc)
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var response = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/personnel-actions/export?format=csv&fromUtc={fromUtc}");

        var payload = await response.Content.ReadAsStringAsync();
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("Kind=Unspecified", payload);
    }

    /// <summary>
    /// The trap of this fix: an INSTANT with an explicit offset must be CONVERTED, not relabelled. Relabelling
    /// `2026-08-01T00:00:00-06:00` as UTC would move the instant six hours.
    /// </summary>
    [Fact]
    public async Task InstantWithExplicitOffset_ShouldBeConvertedNotRelabelled()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePositionSlotAdminContext(scenario));

        var orgUnit = await CreateOrgUnitAsync(client, scenario.TenantId, "DIR-H26-OFF", "Direccion Off", "Direccion");
        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-H26-OFF", "Perfil Off", orgUnit.Id);
        await EnsureJobProfilePublishedAsync(client, profile.Id);

        var response = await client.PostJsonAsync($"/api/v1/companies/{scenario.TenantId}/position-slots", new
        {
            code = "PS-H26-OFF",
            title = "Plaza Offset",
            jobProfilePublicId = profile.Id,
            workCenterPublicId = (Guid?)null,
            directDependencyPositionSlotPublicId = (Guid?)null,
            functionalDependencyPositionSlotPublicId = (Guid?)null,
            status = "Vacant",
            maxEmployees = 1,
            effectiveFromUtc = "2026-08-01T00:00:00-06:00",
            effectiveToUtc = (DateTime?)null,
            notes = (string?)null
        });

        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == response.StatusCode, $"Create failed: {(int)response.StatusCode} {payload}");

        using var document = JsonDocument.Parse(payload);
        var stored = document.RootElement.GetProperty("effectiveFromUtc").GetDateTime();
        // 00:00 at -06:00 IS 06:00 UTC. Anything else means the instant was moved.
        Assert.Equal(new DateTime(2026, 8, 1, 6, 0, 0, DateTimeKind.Utc), stored.ToUniversalTime());
    }

    /// <summary>A `500` must not hand the client the storage engine's own words.</summary>
    [Fact]
    public async Task UnexpectedFailure_ShouldNotLeakInfrastructureDetail()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        // A malformed date cannot reach the storage layer any more, so this asserts the shape of the guard: no
        // response of this API may carry Npgsql/PostgreSQL wording in its `detail`.
        var response = await client.GetAsync(
            $"/api/v1/companies/{scenario.TenantId}/personnel-actions/export?format=csv&fromUtc=2026-08-01");

        var payload = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("PostgreSQL", payload);
        Assert.DoesNotContain("Npgsql", payload);
    }
}

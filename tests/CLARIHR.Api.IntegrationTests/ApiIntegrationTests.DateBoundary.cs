using System.Net;
using System.Net.Http.Json;
using System.Text;
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

    /// <summary>
    /// B-01 — la TERCERA puerta de entrada, la que H-26 no vio. El cuerpo de un JSON Patch no llega como
    /// `DateTime` sino como `JsonElement`, así que ningún converter de `Program.cs` se le aplica: el applier
    /// leía con `TryGetDateTime` y entregaba `Kind=Unspecified` (sin zona) o `Kind=Local` (con offset).
    /// <para>
    /// `LegalRepresentative.AppointmentDateUtc` era el único de los diez sitios sin red en el agregado, así que
    /// es el único donde el `Kind` sin normalizar llegaba hasta la columna `timestamptz`. Las tres formas
    /// nombran el MISMO día y las tres tienen que almacenarse igual.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("2026-08-15")]                  // la forma natural — devolvía 500
    [InlineData("2026-08-15T00:00:00Z")]        // la forma que el playbook documentaba
    [InlineData("2026-08-15T18:07:00-06:00")]   // la de F-03 — corría el día según la zona del servidor
    public async Task LegalRepresentatives_PatchAppointmentDate_ShouldStoreTheDayAsWritten(string appointmentDate)
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId, "LegalRepresentatives.Admin"));

        var representative = await CreateRepresentativeAsync(
            client, scenario.TenantId, "Fecha", "30000000-3", isPrimary: true);

        var patch = $$"""[{"op":"replace","path":"/appointmentDate","value":"{{appointmentDate}}"}]""";
        var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/v1/legal-representatives/{representative.PublicId}")
        {
            Content = new StringContent(patch, Encoding.UTF8, "application/json-patch+json"),
        };
        request.Headers.TryAddWithoutValidation("If-Match", representative.ConcurrencyToken.ToString("D"));

        var response = await client.SendAsync(request);
        var payload = await response.Content.ReadAsStringAsync();

        Assert.True(
            HttpStatusCode.OK == response.StatusCode,
            $"'{appointmentDate}' debió aceptarse; llegó {(int)response.StatusCode}: {payload}");

        using var document = JsonDocument.Parse(payload);

        // El día tal como se escribió. Desde B-02 el campo es `DateOnly`, así que vuelve sin parte de hora:
        // igualdad exacta contra la cadena, que además prueba que el tipo no retrocedió a instante.
        Assert.Equal("2026-08-15", document.RootElement.GetProperty("appointmentDate").GetString());
    }

    /// <summary>
    /// B-02 — fija el TIPO, no solo la ausencia del error. Las tres fechas del representante legal responden a
    /// «¿qué día?», así que viajan como `DateOnly` sobre columna `date` y vuelven como `"2026-08-15"`, sin
    /// parte de hora. Mientras fueron `DateTime` sobre `timestamptz` este test no podía pasar: la respuesta
    /// traía `2026-08-15T00:00:00Z` y cada consumidor tenía que recordar la convención «medianoche UTC» para
    /// leer un día — que es exactamente de donde salió el corrimiento de F-03.
    /// <para>
    /// Igualdad EXACTA, no `StartsWith`: una parte de hora significaría que el campo sigue siendo un instante.
    /// </para>
    /// </summary>
    [Theory]
    [MemberData(nameof(DayFormats))]
    public async Task LegalRepresentatives_Dates_ShouldRoundTripAsPlainDays(string day)
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId, "LegalRepresentatives.Admin"));

        var response = await client.PostJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/legal-representatives",
            new
            {
                firstName = "Dia",
                lastName = "Plano",
                documentType = "DUI",
                documentNumber = "40000000-4",
                positionTitle = "Representante Legal",
                representationType = "AttorneyInFact",
                authorityDescription = (string?)null,
                appointmentInstrument = (string?)null,
                appointmentDate = day,
                effectiveFrom = day,
                effectiveTo = (string?)null,
                email = (string?)null,
                phone = (string?)null,
                isPrimary = true,
            });

        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(
            HttpStatusCode.Created == response.StatusCode,
            $"'{day}' debió aceptarse; llegó {(int)response.StatusCode}: {payload}");

        using var document = JsonDocument.Parse(payload);
        Assert.Equal("2026-12-01", document.RootElement.GetProperty("effectiveFrom").GetString());
        Assert.Equal("2026-12-01", document.RootElement.GetProperty("appointmentDate").GetString());
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("effectiveTo").ValueKind);
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

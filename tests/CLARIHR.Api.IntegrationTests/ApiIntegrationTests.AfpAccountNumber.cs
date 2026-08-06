using System.Net;
using System.Text;
using System.Text.Json;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// REQ-016: the AFP account number is one of the two previsional fields whose absence blocks an employee's
/// payroll line once the compliance gates are switched on. It existed in the domain and the database — and
/// the gate already read it — but no public endpoint could write or return it, so the capture campaign that
/// must precede the activation had no way to run.
///
/// It lives on the personal-info surface, alongside <c>afpCode</c>; the shell response carries neither.
///
/// These assert against the raw JSON rather than a typed test DTO on purpose: the thing being verified is
/// the wire contract the frontend will bind to (the camelCase property name), not just that a value survives
/// a round-trip through types the test itself defines.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private const string AfpAccountNumberProperty = "afpAccountNumber";

    [Fact]
    public async Task PersonnelFiles_AfpAccountNumber_ShouldRoundTripFromCreateToPersonalInfo()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var publicId = await CreatePersonnelFileForAfpAsync(client, scenario.TenantId, "Capture", "AFP-000123456");

        using var personalInfo = await GetPersonalInfoAsync(client, publicId);
        Assert.Equal("AFP-000123456", ReadAfpAccountNumber(personalInfo.RootElement));
    }

    [Fact]
    public async Task PersonnelFiles_AfpAccountNumber_ShouldBePatchable_AndRemovable()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var publicId = await CreatePersonnelFileForAfpAsync(client, scenario.TenantId, "Patchable", afpAccountNumber: null);

        using var initial = await GetPersonalInfoAsync(client, publicId);
        Assert.Null(ReadAfpAccountNumber(initial.RootElement));

        using var patched = await PatchAfpAccountNumberAsync(
            client,
            publicId,
            initial.RootElement.GetProperty("concurrencyToken").GetGuid(),
            "replace",
            "AFP-999888777");
        Assert.Equal("AFP-999888777", ReadAfpAccountNumber(patched.RootElement));

        // Removing it is what puts an employee back on the wrong side of the compliance gate, so the path
        // has to actually clear the value rather than be silently ignored.
        using var afterRemove = await PatchAfpAccountNumberAsync(
            client,
            publicId,
            patched.RootElement.GetProperty("concurrencyToken").GetGuid(),
            "remove",
            value: null);
        Assert.Null(ReadAfpAccountNumber(afterRemove.RootElement));
    }

    private static async Task<Guid> CreatePersonnelFileForAfpAsync(
        HttpClient client,
        Guid tenantId,
        string lastName,
        string? afpAccountNumber)
    {
        var response = await client.PostJsonAsync($"/api/v1/companies/{tenantId}/personnel-files", new
        {
            recordType = "Candidate",
            firstName = "Previsional",
            lastName,
            birthDate = new DateTime(1990, 3, 12),
            nationality = "SV",
            afpAccountNumber,
            customDataJson = (string?)null
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var created = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return created.RootElement.GetProperty("publicId").GetGuid();
    }

    private static async Task<JsonDocument> GetPersonalInfoAsync(HttpClient client, Guid publicId)
    {
        var response = await client.GetAsync($"/api/v1/personnel-files/{publicId}/personal-info");
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static async Task<JsonDocument> PatchAfpAccountNumberAsync(
        HttpClient client,
        Guid publicId,
        Guid concurrencyToken,
        string op,
        string? value)
    {
        var operation = value is null
            ? JsonSerializer.Serialize(new[] { new { op, path = "/afpAccountNumber" } })
            : JsonSerializer.Serialize(new[] { new { op, path = "/afpAccountNumber", value } });

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/personnel-files/{publicId}")
        {
            Content = new StringContent(operation, Encoding.UTF8, "application/json-patch+json")
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{concurrencyToken}\"");

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static string? ReadAfpAccountNumber(JsonElement element)
    {
        Assert.True(
            element.TryGetProperty(AfpAccountNumberProperty, out var value),
            $"The response does not expose '{AfpAccountNumberProperty}'. Payload: {element}");

        return value.ValueKind == JsonValueKind.Null ? null : value.GetString();
    }
}

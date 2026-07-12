using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Application.Features.PersonnelFiles.Common;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// Integration coverage for the indebtedness PARAMETERS (REQ-010 PR-1): the company-wide ceiling (a PUT-only
/// preference — the PATCH is scalar-only) and the per-type ceilings (a replace-all set). The property that makes
/// this REQ safe to deploy is asserted here too: <b>a company with no parameters has no indebtedness control</b>,
/// and that is a legitimate state, not an error.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private static TestUserContext IndebtednessManagerContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            PersonnelFilePermissionCodes.ViewIndebtedness,
            PersonnelFilePermissionCodes.ManageIndebtednessParameters,
            // The company-wide ceiling lives on the preferences aggregate, which has its own grant.
            "CompanyPreferences.Admin");

    private static TestUserContext IndebtednessReaderContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            PersonnelFilePermissionCodes.ViewIndebtedness);

    private static async Task<JsonElement> PutIndebtednessLimitsAsync(
        HttpClient client, Guid companyId, object body, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await client.PutAsJsonAsync($"/api/v1/companies/{companyId}/indebtedness-limits", body);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(expected == response.StatusCode, $"Expected {expected}, got {(int)response.StatusCode}: {payload}");
        return JsonDocument.Parse(payload).RootElement.Clone();
    }

    [Fact]
    public async Task IndebtednessLimits_AreEmptyByDefault_WhichMeansTheCompanyHasNoControl()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(IndebtednessManagerContext(scenario));

        var response = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/indebtedness-limits");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(0, doc.RootElement.GetArrayLength());

        // And the company-wide ceiling is null too: no parameters ⇒ no indebtedness validation at all. That is
        // the opt-in-by-configuration behaviour REQ-008/009 depend on to stay retrocompatible.
        var preferences = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/preferences");
        preferences.EnsureSuccessStatusCode();
        using var prefDoc = JsonDocument.Parse(await preferences.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Null, prefDoc.RootElement.GetProperty("maxIndebtednessPercent").ValueKind);
    }

    [Fact]
    public async Task IndebtednessCeiling_IsSetThroughThePreferencesPut_NotThePatch()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(IndebtednessManagerContext(scenario));

        var current = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/preferences");
        current.EnsureSuccessStatusCode();
        using var currentDoc = JsonDocument.Parse(await current.Content.ReadAsStringAsync());
        var token = currentDoc.RootElement.GetProperty("concurrencyToken").GetGuid();

        using var request = new HttpRequestMessage(
            HttpMethod.Put, $"/api/v1/companies/{scenario.TenantId}/preferences")
        {
            Content = JsonContent.Create(new
            {
                currencyCode = "USD",
                timeZone = "UTC",
                maxIndebtednessPercent = 30m,
            })
        };
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{token}\"");

        var updated = await client.SendAsync(request);
        var payload = await updated.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == updated.StatusCode, $"PUT failed: {(int)updated.StatusCode} {payload}");

        using var doc = JsonDocument.Parse(payload);
        Assert.Equal(30m, doc.RootElement.GetProperty("maxIndebtednessPercent").GetDecimal());
    }

    [Fact]
    public async Task IndebtednessLimits_ReplaceAll_SetsTheCeilingsAndDropsWhatIsNotSent()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(IndebtednessManagerContext(scenario));

        var created = await PutIndebtednessLimitsAsync(client, scenario.TenantId, new
        {
            limits = new[]
            {
                new { recurringDeductionTypeCode = "PRESTAMO_BANCARIO", maxPercent = 25m },
                new { recurringDeductionTypeCode = "PROCURADURIA", maxPercent = 40m },
            }
        });

        Assert.Equal(2, created.GetArrayLength());
        Assert.Equal(25m, created[0].GetProperty("maxPercent").GetDecimal());
        Assert.Equal("PRESTAMO_BANCARIO", created[0].GetProperty("recurringDeductionTypeCode").GetString());

        // Replace-all: sending only one row REMOVES the other. The set is edited as a whole.
        var replaced = await PutIndebtednessLimitsAsync(client, scenario.TenantId, new
        {
            limits = new[] { new { recurringDeductionTypeCode = "PRESTAMO_BANCARIO", maxPercent = 30m } }
        });

        Assert.Equal(1, replaced.GetArrayLength());
        Assert.Equal(30m, replaced[0].GetProperty("maxPercent").GetDecimal());

        // An empty set clears the per-type ceilings entirely.
        var cleared = await PutIndebtednessLimitsAsync(client, scenario.TenantId, new { limits = Array.Empty<object>() });
        Assert.Equal(0, cleared.GetArrayLength());
    }

    [Fact]
    public async Task IndebtednessLimits_RejectAPhantomType_AndADuplicatedOne()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(IndebtednessManagerContext(scenario));

        // A ceiling over a type that does not exist would never apply — it is a silent misconfiguration.
        var phantom = await PutIndebtednessLimitsAsync(
            client, scenario.TenantId,
            new { limits = new[] { new { recurringDeductionTypeCode = "TIPO_FANTASMA", maxPercent = 25m } } },
            HttpStatusCode.UnprocessableEntity);
        Assert.Equal("INDEBTEDNESS_LIMIT_TYPE_INVALID", phantom.GetProperty("code").GetString());

        // The filtered-unique index would surface a duplicate as a 500; the handler turns it into a 422.
        var duplicated = await PutIndebtednessLimitsAsync(
            client, scenario.TenantId,
            new
            {
                limits = new[]
                {
                    new { recurringDeductionTypeCode = "PRESTAMO_BANCARIO", maxPercent = 25m },
                    new { recurringDeductionTypeCode = "prestamo_bancario", maxPercent = 30m },
                }
            },
            HttpStatusCode.UnprocessableEntity);
        Assert.Equal("INDEBTEDNESS_LIMIT_TYPE_DUPLICATED", duplicated.GetProperty("code").GetString());

        // Neither attempt persisted anything.
        var current = await client.GetAsync($"/api/v1/companies/{scenario.TenantId}/indebtedness-limits");
        using var doc = JsonDocument.Parse(await current.Content.ReadAsStringAsync());
        Assert.Equal(0, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task IndebtednessLimits_APercentOutOfRange_IsRejected()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(IndebtednessManagerContext(scenario));

        foreach (var invalid in new[] { 0m, -5m, 100.01m })
        {
            var response = await client.PutAsJsonAsync(
                $"/api/v1/companies/{scenario.TenantId}/indebtedness-limits",
                new { limits = new[] { new { recurringDeductionTypeCode = "PRESTAMO_BANCARIO", maxPercent = invalid } } });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // The boundary itself is legal: 100% means "the whole income may be consumed".
        var boundary = await PutIndebtednessLimitsAsync(
            client, scenario.TenantId,
            new { limits = new[] { new { recurringDeductionTypeCode = "PRESTAMO_BANCARIO", maxPercent = 100m } } });
        Assert.Equal(100m, boundary[0].GetProperty("maxPercent").GetDecimal());
    }

    [Fact]
    public async Task IndebtednessLimits_AReaderCannotWriteThem()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var reader = factory.CreateClientFor(IndebtednessReaderContext(scenario));

        // ViewIndebtedness reads...
        var read = await reader.GetAsync($"/api/v1/companies/{scenario.TenantId}/indebtedness-limits");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        // ...but does not write: the ceilings are a ManageIndebtednessParameters grant.
        var write = await reader.PutAsJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/indebtedness-limits",
            new { limits = new[] { new { recurringDeductionTypeCode = "PRESTAMO_BANCARIO", maxPercent = 25m } } });
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
    }
}

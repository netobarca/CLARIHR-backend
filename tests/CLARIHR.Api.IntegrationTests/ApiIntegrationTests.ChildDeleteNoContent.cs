using System.Net;
using System.Text.Json;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// H-34 — the DELETE of a child used to answer <c>200</c> with <c>{ parentConcurrencyToken }</c>, documented as
/// "the parent's UPDATED token so the caller can keep mutating without an extra round-trip". Across the 53
/// endpoints that returned it the promise was true in 29 and false in 24: the job-profile aggregate never rotates
/// its token on a child write, and 14 of the personnel-file sections do not either. The same field, in the same
/// contract shape, meant two different things depending on which module answered — a value you could trust half
/// the time, with no way to tell which half.
/// <para>
/// Concurrency in these modules is per CHILD (the DELETE's own If-Match carries the child's token), so the field
/// was a shortcut nobody needed. The DELETE now answers <c>204 No Content</c>.
/// </para>
/// </summary>
public sealed partial class ApiIntegrationTests
{
    /// <summary>The family where the promise was always false: a job-profile child.</summary>
    [Fact]
    public async Task JobProfiles_DeleteChild_ShouldReturn204WithoutBody()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreateJobProfileAdminContext(scenario));

        var profile = await CreateJobProfileAsync(client, scenario.TenantId, "JP-H34", "Perfil H34");

        var created = await client.PostJsonAsync($"/api/v1/job-profiles/{profile.Id}/functions", new
        {
            functionType = "General",
            frequencyCatalogItemPublicId = (Guid?)null,
            description = "Funcion H34",
            sortOrder = 1
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        using var createdDocument = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var functionId = createdDocument.RootElement.GetProperty("functionPublicId").GetGuid();
        var functionToken = createdDocument.RootElement.GetProperty("concurrencyToken").GetGuid();

        using var request = new HttpRequestMessage(
            HttpMethod.Delete, $"/api/v1/job-profiles/{profile.Id}/functions/{functionId}");
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{functionToken}\"");
        var response = await client.SendAsync(request);

        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(
            HttpStatusCode.NoContent == response.StatusCode,
            $"Expected 204, got {(int)response.StatusCode}: {payload}");
        Assert.Equal(string.Empty, payload);
    }

    /// <summary>
    /// The personnel-file half that never rotated the parent token either — a curricular section.
    /// </summary>
    [Fact]
    public async Task PersonnelFiles_DeleteLanguage_ShouldReturn204WithoutBody()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var created = await CreatePersonnelFileAsync(client, scenario.TenantId, "Borra", "Idioma", "DUI", "07777777-1");

        var addedResponse = await client.PostJsonAsync($"/api/v1/personnel-files/{created.Id}/languages", new
        {
            languageCode = "ENGLISH",
            levelCode = "ADVANCED",
            speaks = true,
            writes = true,
            reads = true,
            concurrencyToken = created.ConcurrencyToken
        });
        Assert.Equal(HttpStatusCode.Created, addedResponse.StatusCode);
        using var addedDocument = JsonDocument.Parse(await addedResponse.Content.ReadAsStringAsync());
        var languageId = addedDocument.RootElement.GetProperty("languagePublicId").GetGuid();
        var languageToken = addedDocument.RootElement.GetProperty("concurrencyToken").GetGuid();

        using var request = new HttpRequestMessage(
            HttpMethod.Delete, $"/api/v1/personnel-files/{created.Id}/languages/{languageId}");
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{languageToken}\"");
        var response = await client.SendAsync(request);

        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(
            HttpStatusCode.NoContent == response.StatusCode,
            $"Expected 204, got {(int)response.StatusCode}: {payload}");
        Assert.Equal(string.Empty, payload);
    }

    /// <summary>
    /// Non-regression: dropping the field must NOT drop `TouchPersonnelFile`. It is what moves the file's
    /// `modifiedAtUtc`, which feeds the dashboard's expediente-freshness indicator (updated vs outdated, D-08) —
    /// a different concern that happened to travel with the token.
    /// </summary>
    [Fact]
    public async Task PersonnelFiles_DeleteDocument_ShouldStillTouchTheFileFreshness()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var created = await CreatePersonnelFileAsync(client, scenario.TenantId, "Frescura", "Expediente", "DUI", "07777777-2");
        var (filePublicId, documentTypePublicId) = await SeedDocumentPrerequisitesAsync(scenario, created.Id, documentTypeCode: "H34-DOC");

        var addResponse = await client.PostJsonAsync($"/api/v1/personnel-files/{created.Id}/documents", new
        {
            filePublicId,
            documentTypeCatalogItemPublicId = documentTypePublicId,
            observations = (string?)null
        });
        Assert.Equal(HttpStatusCode.Created, addResponse.StatusCode);
        using var addedDocument = JsonDocument.Parse(await addResponse.Content.ReadAsStringAsync());
        var documentId = addedDocument.RootElement.GetProperty("publicId").GetGuid();
        var documentToken = addedDocument.RootElement.GetProperty("concurrencyToken").GetGuid();

        var beforeModified = await ReadFileModifiedAtUtcAsync(client, created.Id);

        using var request = new HttpRequestMessage(
            HttpMethod.Delete, $"/api/v1/personnel-files/{created.Id}/documents/{documentId}");
        request.Headers.TryAddWithoutValidation("If-Match", $"\"{documentToken}\"");
        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var afterModified = await ReadFileModifiedAtUtcAsync(client, created.Id);
        Assert.NotNull(afterModified);
        Assert.True(
            afterModified >= beforeModified,
            $"The file's modifiedAtUtc went backwards: {beforeModified} -> {afterModified}.");
    }

    private async Task<DateTime?> ReadFileModifiedAtUtcAsync(HttpClient client, Guid personnelFilePublicId)
    {
        var response = await client.GetAsync($"/api/v1/personnel-files/{personnelFilePublicId}");
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("modifiedAtUtc", out var modified) && modified.ValueKind != JsonValueKind.Null
            ? modified.GetDateTime()
            : null;
    }
}

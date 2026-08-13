using System.Net;
using System.Text.Json;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// H-21 — the country-scoped catalog reads used to answer <c>200 []</c> when <c>countryCode</c> was missing, which
/// is indistinguishable from "this catalog was never seeded". That is the worst possible answer: §5.6 of the
/// playbook tells the reader that an empty concept catalog means the payroll engine cannot calculate, so a missing
/// query parameter reads as a broken environment. The country was never actually unknown — the token carries the
/// tenant, the tenant IS the company, and the company has a country code.
/// <para>
/// The assertions compare the parameter-less response against the explicit <c>?countryCode=SV</c> one instead of
/// pinning item counts: what must hold is that omitting the parameter yields the tenant's country, and that
/// property cannot drift when a seed grows.
/// </para>
/// </summary>
public sealed partial class ApiIntegrationTests
{
    /// <summary>The four dedicated country-scoped catalog endpoints plus a sample of the general/reference families.</summary>
    public static TheoryData<string> CountryScopedCatalogPaths() =>
    [
        "/api/v1/compensation-concept-types",
        "/api/v1/settlement-concepts",
        "/api/v1/contract-types",
        "/api/v1/afps",
        "/api/v1/general-catalogs/banks",
        "/api/v1/general-catalogs/payroll-types",
        "/api/v1/general-catalogs/employment-statuses",
        "/api/v1/general-catalogs/currencies",
        "/api/v1/reference-catalogs/professions",
        "/api/v1/reference-catalogs/identification-types",
    ];

    [Theory]
    [MemberData(nameof(CountryScopedCatalogPaths))]
    public async Task Catalogs_WithoutCountryCode_ShouldResolveTheTenantCountry(string path)
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var implicitResponse = await client.GetAsync(path);
        var explicitResponse = await client.GetAsync($"{path}?countryCode=SV");

        var implicitPayload = await implicitResponse.Content.ReadAsStringAsync();
        Assert.True(
            HttpStatusCode.OK == implicitResponse.StatusCode,
            $"{path} without countryCode: {(int)implicitResponse.StatusCode} {implicitPayload}");
        explicitResponse.EnsureSuccessStatusCode();

        using var implicitDocument = JsonDocument.Parse(implicitPayload);
        using var explicitDocument = JsonDocument.Parse(await explicitResponse.Content.ReadAsStringAsync());

        var implicitCodes = CatalogCodes(implicitDocument);
        var explicitCodes = CatalogCodes(explicitDocument);

        // The seeded company is SV, so both must be the same catalog — and it must not be the empty list that
        // used to come back, which would make the comparison vacuously true.
        Assert.NotEmpty(explicitCodes);
        Assert.Equal(explicitCodes, implicitCodes);
    }

    /// <summary>
    /// H-21 — an ISO-shaped code that matches no active country is a bad parameter, not an empty catalog. It used
    /// to answer <c>200 []</c> in BOTH families, including the one that already required the parameter.
    /// </summary>
    [Theory]
    [InlineData("/api/v1/compensation-concept-types")]
    [InlineData("/api/v1/settlement-concepts")]
    [InlineData("/api/v1/contract-types")]
    [InlineData("/api/v1/afps")]
    [InlineData("/api/v1/general-catalogs/banks")]
    [InlineData("/api/v1/reference-catalogs/professions")]
    public async Task Catalogs_WithUnknownCountryCode_ShouldReturn400(string path)
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var response = await client.GetAsync($"{path}?countryCode=XX");

        await AssertProblemDetailsAsync(response, HttpStatusCode.BadRequest, "CATALOG_COUNTRY_UNKNOWN");

        // The offending field is named, like every other 400 of the family.
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("errors").TryGetProperty("countryCode", out _));
    }

    /// <summary>
    /// Non-regression for onboarding: the system-scoped catalogs are the same for every country and must keep
    /// answering without any country context at all. If the country resolution ran before the system branch, these
    /// would start failing for a caller with no company — the exact flow the company-less surface exists for.
    /// </summary>
    [Theory]
    [InlineData("/api/v1/general-catalogs/countries")]
    [InlineData("/api/v1/general-catalogs/education-statuses")]
    [InlineData("/api/v1/general-catalogs/education-study-types")]
    [InlineData("/api/v1/general-catalogs/file-document-types")]
    public async Task Catalogs_SystemScoped_WithoutCountryCode_ShouldStillAnswer(string path)
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var response = await client.GetAsync(path);

        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == response.StatusCode, $"{path}: {(int)response.StatusCode} {payload}");
    }

    // H-22 cerró el hueco que este archivo fijaba: `file-document-types` ya no llega vacío. La cobertura vive
    // ahora en ApiIntegrationTests.DocumentTypeCatalogSeed.cs.

    private static string[] CatalogCodes(JsonDocument document) =>
        [.. document.RootElement.EnumerateArray()
            .Select(item => item.TryGetProperty("code", out var code) ? code.GetString() ?? string.Empty : string.Empty)
            .Order(StringComparer.Ordinal)];
}

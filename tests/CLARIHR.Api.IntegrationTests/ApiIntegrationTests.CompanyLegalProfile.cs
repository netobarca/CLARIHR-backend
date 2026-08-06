using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Application.Features.Preferences.Common;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// REQ-016 RF-006 — the employer legal identity (razón social, NIT patronal, registro ISSS, dirección
/// fiscal) that heads F-14, Planilla Única and Planilla Patronal.
///
/// This surface had NO integration coverage at all, which matters more than for an ordinary CRUD: once the
/// compliance activation switch is on, a company without this record **cannot generate payroll** (Gate A,
/// ratified P-03). The record is also the target of the capture campaign that has to run before that switch
/// is flipped, so every one of these endpoints is on the critical path of a deployment.
///
/// It carries no <c>[AuthorizationPolicySet]</c>: the gate lives in the handlers and reuses the
/// CompanyPreferences codes, so read/write separation is only enforced there — which is exactly why it
/// needs a test rather than a reading of the controller.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private const string LegalProfileNit = "0614-123456-101-5";
    private const string LegalProfileIsss = "123456-7";

    [Fact]
    public async Task CompanyLegalProfile_CreateAndGet_ShouldRoundTrip_AndReturn404BeforeItExists()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var admin = factory.CreateClientFor(LegalProfileAdminContext(scenario));

        var before = await admin.GetAsync(LegalProfileUrl(scenario.TenantId));
        Assert.Equal(HttpStatusCode.NotFound, before.StatusCode);
        Assert.Equal("COMPANY_LEGAL_PROFILE_NOT_FOUND", await ReadErrorCodeAsync(before));

        var created = await CreateLegalProfileAsync(admin, scenario.TenantId);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        using var createdBody = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        Assert.Equal("Acme El Salvador, S.A. de C.V.", createdBody.RootElement.GetProperty("legalName").GetString());
        Assert.Equal(LegalProfileNit, createdBody.RootElement.GetProperty("employerNitNumber").GetString());

        var after = await admin.GetAsync(LegalProfileUrl(scenario.TenantId));
        after.EnsureSuccessStatusCode();

        using var fetched = JsonDocument.Parse(await after.Content.ReadAsStringAsync());
        Assert.Equal(LegalProfileIsss, fetched.RootElement.GetProperty("isssEmployerRegistrationNumber").GetString());
        Assert.Equal(
            createdBody.RootElement.GetProperty("concurrencyToken").GetGuid(),
            fetched.RootElement.GetProperty("concurrencyToken").GetGuid());
    }

    [Fact]
    public async Task CompanyLegalProfile_CreateTwice_ShouldConflict()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var admin = factory.CreateClientFor(LegalProfileAdminContext(scenario));

        Assert.Equal(HttpStatusCode.Created, (await CreateLegalProfileAsync(admin, scenario.TenantId)).StatusCode);

        // One profile per company: the second create must be refused rather than silently overwrite the
        // identity the compliance reports are built from.
        var duplicate = await CreateLegalProfileAsync(admin, scenario.TenantId);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("COMPANY_LEGAL_PROFILE_ALREADY_EXISTS", await ReadErrorCodeAsync(duplicate));
    }

    [Fact]
    public async Task CompanyLegalProfile_Update_ShouldRequireIfMatch_AndRejectAStaleToken()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var admin = factory.CreateClientFor(LegalProfileAdminContext(scenario));

        var created = await CreateLegalProfileAsync(admin, scenario.TenantId);
        using var createdBody = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var token = createdBody.RootElement.GetProperty("concurrencyToken").GetGuid();

        // Repo-wide convention: If-Match missing → 400, stale → 409.
        var withoutIfMatch = await UpdateLegalProfileAsync(admin, scenario.TenantId, concurrencyToken: null);
        Assert.Equal(HttpStatusCode.BadRequest, withoutIfMatch.StatusCode);

        var updated = await UpdateLegalProfileAsync(admin, scenario.TenantId, token);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);

        using var updatedBody = JsonDocument.Parse(await updated.Content.ReadAsStringAsync());
        Assert.Equal("Acme El Salvador, S.A. de C.V. (actualizada)", updatedBody.RootElement.GetProperty("legalName").GetString());
        Assert.NotEqual(token, updatedBody.RootElement.GetProperty("concurrencyToken").GetGuid());

        // The token consumed above is now stale.
        var stale = await UpdateLegalProfileAsync(admin, scenario.TenantId, token);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("COMPANY_LEGAL_PROFILE_CONCURRENCY_CONFLICT", await ReadErrorCodeAsync(stale));
    }

    [Fact]
    public async Task CompanyLegalProfile_Reader_ShouldReadButNotWrite()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var admin = factory.CreateClientFor(LegalProfileAdminContext(scenario));
        using var reader = factory.CreateClientFor(LegalProfileReaderContext(scenario));

        var created = await CreateLegalProfileAsync(admin, scenario.TenantId);
        using var createdBody = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var token = createdBody.RootElement.GetProperty("concurrencyToken").GetGuid();

        var read = await reader.GetAsync(LegalProfileUrl(scenario.TenantId));
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        // CompanyPreferences.Read grants view only. Nothing on this controller declares a policy, so if the
        // handler gate ever loses the manage check this is the only thing standing between a read-only role
        // and the identity that goes on a legal filing.
        Assert.Equal(HttpStatusCode.Forbidden, (await CreateLegalProfileAsync(reader, scenario.TenantId)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await UpdateLegalProfileAsync(reader, scenario.TenantId, token)).StatusCode);
    }

    [Fact]
    public async Task CompanyLegalProfile_ForeignTenant_ShouldNotBeReachable()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var admin = factory.CreateClientFor(LegalProfileAdminContext(scenario));

        // Authenticated in TenantId, asking about OtherTenantId: the tenant check runs before anything is
        // read, so a foreign profile must not be readable NOR creatable.
        var foreignRead = await admin.GetAsync(LegalProfileUrl(scenario.OtherTenantId));
        Assert.True(
            foreignRead.StatusCode == HttpStatusCode.Forbidden,
            $"expected 403 for a foreign tenant, got {(int)foreignRead.StatusCode}: " +
            await foreignRead.Content.ReadAsStringAsync());

        var foreignCreate = await CreateLegalProfileAsync(admin, scenario.OtherTenantId);
        Assert.True(
            foreignCreate.StatusCode == HttpStatusCode.Forbidden,
            $"expected 403 creating for a foreign tenant, got {(int)foreignCreate.StatusCode}: " +
            await foreignCreate.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task CompanyLegalProfile_WithUnknownLegalRepresentative_ShouldBeUnprocessable()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var admin = factory.CreateClientFor(LegalProfileAdminContext(scenario));

        var response = await admin.PostJsonAsync(LegalProfileUrl(scenario.TenantId), new
        {
            legalName = "Acme El Salvador, S.A. de C.V.",
            employerNitNumber = LegalProfileNit,
            isssEmployerRegistrationNumber = LegalProfileIsss,
            fiscalAddress = "Col. Escalón, San Salvador",
            economicActivityDescription = (string?)null,
            legalRepresentativePublicId = Guid.NewGuid(),
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("COMPANY_LEGAL_PROFILE_LEGAL_REPRESENTATIVE_NOT_FOUND", await ReadErrorCodeAsync(response));
    }

    [Fact]
    public async Task CompanyLegalProfile_WithMalformedEmployerNit_ShouldBeRejected()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var admin = factory.CreateClientFor(LegalProfileAdminContext(scenario));

        // The NIT goes on a filing to Hacienda; the ####-######-###-# shape is quoted verbatim in the
        // frontend guide, so it has to be enforced server-side too.
        var response = await admin.PostJsonAsync(LegalProfileUrl(scenario.TenantId), new
        {
            legalName = "Acme El Salvador, S.A. de C.V.",
            employerNitNumber = "0614123456101 5",
            isssEmployerRegistrationNumber = LegalProfileIsss,
            fiscalAddress = "Col. Escalón, San Salvador",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static string LegalProfileUrl(Guid companyId) => $"/api/v1/companies/{companyId}/legal-profile";

    private static TestUserContext LegalProfileAdminContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            CompanyPreferencePermissionCodes.Admin);

    private static TestUserContext LegalProfileReaderContext(IntegrationTestScenario scenario) =>
        TestUserContext.Authenticated(
            scenario.ActorUserId,
            scenario.TenantId,
            CompanyPreferencePermissionCodes.Read);

    private static Task<HttpResponseMessage> CreateLegalProfileAsync(HttpClient client, Guid companyId) =>
        client.PostJsonAsync(LegalProfileUrl(companyId), new
        {
            legalName = "Acme El Salvador, S.A. de C.V.",
            employerNitNumber = LegalProfileNit,
            isssEmployerRegistrationNumber = LegalProfileIsss,
            fiscalAddress = "Col. Escalón, San Salvador",
            economicActivityDescription = "Servicios de consultoría",
        });

    private static async Task<HttpResponseMessage> UpdateLegalProfileAsync(
        HttpClient client,
        Guid companyId,
        Guid? concurrencyToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, LegalProfileUrl(companyId))
        {
            Content = JsonContent.Create(new
            {
                legalName = "Acme El Salvador, S.A. de C.V. (actualizada)",
                employerNitNumber = LegalProfileNit,
                isssEmployerRegistrationNumber = LegalProfileIsss,
                fiscalAddress = "Col. Escalón, San Salvador",
                economicActivityDescription = "Servicios de consultoría",
            })
        };

        if (concurrencyToken.HasValue)
        {
            request.Headers.IfMatch.Add(new EntityTagHeaderValue($"\"{concurrencyToken.Value}\""));
        }

        return await client.SendAsync(request);
    }

    private static async Task<string?> ReadErrorCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }
}

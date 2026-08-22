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

    /// <summary>
    /// 00001 / B-02 — el `400` tiene que decir **qué campo** falló. Los validadores compartían las reglas a
    /// través de accesores lambda (`RuleFor(c => legalName(c))`), y FluentValidation no puede derivar el
    /// nombre de una propiedad de una lambda *invocada*: emitía todo bajo la clave `""`, dejando al cliente
    /// una lista de textos sin saber a qué input pertenecen.
    /// <para>
    /// Se mandan **dos** campos malformados a la vez a propósito: con uno solo, un `errors` de una entrada
    /// pasaría igual aunque la clave siguiera siendo la vacía.
    /// </para>
    /// </summary>
    [Fact]
    public async Task CompanyLegalProfile_WhenValidationFails_ShouldKeyErrorsByField()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var admin = factory.CreateClientFor(LegalProfileAdminContext(scenario));

        var response = await admin.PostJsonAsync(LegalProfileUrl(scenario.TenantId), new
        {
            legalName = "Acme El Salvador, S.A. de C.V.",
            employerNitNumber = "0614123456101 5",     // formato inválido
            isssEmployerRegistrationNumber = "ABC/123", // solo dígitos y guiones
            fiscalAddress = "Col. Escalón, San Salvador",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors");

        Assert.False(
            errors.TryGetProperty("", out _),
            "El `400` no debe agrupar los mensajes bajo la clave vacía: el cliente no puede señalar el campo.");
        Assert.True(errors.TryGetProperty("employerNitNumber", out _), "Falta la clave `employerNitNumber`.");
        Assert.True(
            errors.TryGetProperty("isssEmployerRegistrationNumber", out _),
            "Falta la clave `isssEmployerRegistrationNumber`.");
    }

    /// <summary>
    /// 00001 / B-01 — el recurso no exponía `allowedActions`, así que el frontend no tenía forma de saber si
    /// el usuario puede escribir sin replicar a mano los códigos de permiso — que es justo donde ya se
    /// equivocó (F-01: el guard pedía `PersonnelFiles.Read`).
    /// <para>
    /// Se comprueba con los DOS perfiles, no solo con el admin: un `canEdit` que saliera `true` para todos
    /// sería tan inútil como no tenerlo.
    /// </para>
    /// </summary>
    [Fact]
    public async Task CompanyLegalProfile_Get_ShouldExposeAllowedActionsPerPermission()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var admin = factory.CreateClientFor(LegalProfileAdminContext(scenario));
        Assert.Equal(HttpStatusCode.Created, (await CreateLegalProfileAsync(admin, scenario.TenantId)).StatusCode);

        var adminGet = await admin.GetAsync(LegalProfileUrl(scenario.TenantId));
        adminGet.EnsureSuccessStatusCode();
        using var adminBody = JsonDocument.Parse(await adminGet.Content.ReadAsStringAsync());
        var adminActions = adminBody.RootElement.GetProperty("allowedActions");
        Assert.True(adminActions.GetProperty("canView").GetBoolean());
        Assert.True(adminActions.GetProperty("canEdit").GetBoolean());

        using var reader = factory.CreateClientFor(LegalProfileReaderContext(scenario));
        var readerGet = await reader.GetAsync(LegalProfileUrl(scenario.TenantId));
        readerGet.EnsureSuccessStatusCode();
        using var readerBody = JsonDocument.Parse(await readerGet.Content.ReadAsStringAsync());
        var readerActions = readerBody.RootElement.GetProperty("allowedActions");
        Assert.True(readerActions.GetProperty("canView").GetBoolean());
        Assert.False(readerActions.GetProperty("canEdit").GetBoolean());
    }

    /// <summary>
    /// 00003 / B-03 — <b>el canal de localización entrega español cuando se pide.</b>
    /// <para>
    /// El producto tiene 1051 claves traducidas y respondía en inglés siempre. La causa no era que faltaran
    /// traducciones: <c>JwtTokenService</c> emitía el claim <c>language</c> incluso cuando el usuario no había
    /// elegido idioma (<c>?? "en"</c>), y <c>RequestLanguageResolver</c> resuelve
    /// <c>claim → Accept-Language → "en"</c>, así que la cabecera nunca llegaba a consultarse.
    /// </para>
    /// <para>
    /// ⚠️ <b>Este test NO habría cazado el defecto por sí solo</b>, y conviene saberlo: el harness de
    /// integración no emite el claim <c>language</c>, así que aquí la cabecera siempre pudo ganar. El rojo que
    /// sí lo cazó está en <c>JwtTokenServiceTests</c>, que es donde vive la causa. Éste prueba lo otro: que el
    /// canal entrega de punta a punta, que es lo que quedaba sin demostrar.
    /// </para>
    /// </summary>
    [Theory]
    [InlineData("es")]
    [InlineData("es-SV,es;q=0.9")]
    public async Task Localization_WhenSpanishIsRequested_ShouldAnswerInSpanish(string acceptLanguage)
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var admin = factory.CreateClientFor(LegalProfileAdminContext(scenario));
        admin.DefaultRequestHeaders.Add("Accept-Language", acceptLanguage);

        // Sin perfil legal configurado todavía: 404 con una clave que sí tiene traducción completa.
        var response = await admin.GetAsync(LegalProfileUrl(scenario.TenantId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync();

        using var document = JsonDocument.Parse(payload);
        // `code` va en la RAÍZ del ProblemDetails (§5.3 de las definiciones): `extensions` no existe en el wire.
        Assert.Equal("COMPANY_LEGAL_PROFILE_NOT_FOUND", document.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            "La empresa todavía no tiene un perfil legal configurado.",
            document.RootElement.GetProperty("title").GetString());
    }

    /// <summary>
    /// El contrapeso: sin cabecera se responde en inglés. Sin este test, «localiza siempre en español» pasaría
    /// por bueno y el producto habría cambiado de idioma para todos.
    /// </summary>
    [Fact]
    public async Task Localization_WhenNoLanguageIsRequested_ShouldFallBackToEnglish()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var admin = factory.CreateClientFor(LegalProfileAdminContext(scenario));

        var response = await admin.GetAsync(LegalProfileUrl(scenario.TenantId));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            "The company does not have a legal profile configured yet.",
            document.RootElement.GetProperty("title").GetString());
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

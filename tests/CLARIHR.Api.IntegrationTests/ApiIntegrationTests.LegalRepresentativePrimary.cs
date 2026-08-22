using System.Net;
using System.Net.Http.Json;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// Regression coverage for the primary-representative switch. `ux_legal_representatives__tenant_primary_active`
/// is a PARTIAL UNIQUE INDEX (`WHERE is_primary = true AND is_active = true`), evaluated per statement and not
/// deferrable. The three handlers that move the flag used to demote the current primary and promote the new one
/// in a SINGLE SaveChanges; EF is free to order the promotion first, which leaves two (primary, active) rows for
/// an instant and makes Postgres reject the batch — the caller got a `500`.
///
/// These cases only fail against a real database: a unit test with a fake repository never issues the SQL, so
/// nothing catches the ordering. Two of the three paths were reproducibly broken in production.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private sealed record PrimaryRepItem(Guid PublicId, bool? IsPrimary, bool IsActive, Guid ConcurrencyToken);

    private static object BuildRepresentativeBody(string firstName, string documentNumber, bool isPrimary) =>
        new
        {
            firstName,
            lastName = "Prueba",
            documentType = "DUI",
            documentNumber,
            positionTitle = "Representante Legal",
            representationType = "AttorneyInFact",
            authorityDescription = (string?)null,
            appointmentInstrument = (string?)null,
            appointmentDate = (DateOnly?)null,
            effectiveFrom = new DateOnly(2026, 1, 1),
            effectiveTo = (DateOnly?)null,
            email = (string?)null,
            phone = (string?)null,
            isPrimary
        };

    private async Task<PrimaryRepItem> CreateRepresentativeAsync(
        HttpClient client, Guid tenantId, string firstName, string documentNumber, bool isPrimary)
    {
        var response = await client.PostJsonAsync(
            $"/api/v1/companies/{tenantId}/legal-representatives",
            BuildRepresentativeBody(firstName, documentNumber, isPrimary));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<PrimaryRepItem>(JsonOptions);
        Assert.NotNull(created);
        return created!;
    }

    /// <summary>Exactly one active primary must remain, and it must be the expected one.</summary>
    private async Task AssertSingleActivePrimaryAsync(Guid tenantId, Guid expectedPublicId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var primaries = await dbContext.LegalRepresentatives
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && item.IsPrimary == true && item.IsActive)
            .Select(item => item.PublicId)
            .ToListAsync();

        Assert.Single(primaries);
        Assert.Equal(expectedPublicId, primaries[0]);
    }

    private static HttpRequestMessage IfMatch(HttpMethod method, string uri, Guid concurrencyToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, uri);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        request.Headers.TryAddWithoutValidation("If-Match", concurrencyToken.ToString("D"));
        return request;
    }


    /// <summary>
    /// B-04 — el seeder de integración crea un representante <b>principal</b> en el tenant del escenario
    /// (<c>IntegrationTestSeeder</c>), así que «el primero» nunca lo era y la promoción elegía a ése por ser el
    /// más antiguo. Los tests del invariante arrancan de una empresa vacía para que su premisa sea cierta y
    /// para que se sepa a quién debe tocarle el puesto.
    /// </summary>
    private Task<IntegrationTestScenario> ResetWithoutLegalRepresentativesAsync() =>
        factory.ResetDatabaseAsync(async dbContext =>
        {
            dbContext.LegalRepresentatives.RemoveRange(
                await dbContext.LegalRepresentatives.IgnoreQueryFilters().ToListAsync());
            await dbContext.SaveChangesAsync();
        });

    [Fact]
    public async Task SetPrimary_WhenAnotherRepresentativeIsAlreadyPrimary_ShouldPromoteAndDemote()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId, "LegalRepresentatives.Admin"));

        await CreateRepresentativeAsync(client, scenario.TenantId, "Primero", "10000000-1", isPrimary: true);
        var second = await CreateRepresentativeAsync(client, scenario.TenantId, "Segundo", "20000000-2", isPrimary: false);

        var response = await client.SendAsync(IfMatch(
            HttpMethod.Patch,
            $"/api/v1/legal-representatives/{second.PublicId}/set-primary",
            second.ConcurrencyToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var promoted = await response.Content.ReadFromJsonAsync<PrimaryRepItem>(JsonOptions);
        Assert.NotNull(promoted);
        Assert.True(promoted!.IsPrimary);
        Assert.NotEqual(second.ConcurrencyToken, promoted.ConcurrencyToken);

        await AssertSingleActivePrimaryAsync(scenario.TenantId, second.PublicId);
    }

    [Fact]
    public async Task Update_WithIsPrimaryTrue_WhenAnotherRepresentativeIsAlreadyPrimary_ShouldPromoteAndDemote()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId, "LegalRepresentatives.Admin"));

        await CreateRepresentativeAsync(client, scenario.TenantId, "Primero", "10000000-1", isPrimary: true);
        var second = await CreateRepresentativeAsync(client, scenario.TenantId, "Segundo", "20000000-2", isPrimary: false);

        var response = await client.SendAsync(IfMatch(
            HttpMethod.Put,
            $"/api/v1/legal-representatives/{second.PublicId}",
            second.ConcurrencyToken,
            BuildRepresentativeBody("Segundo", "20000000-2", isPrimary: true)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertSingleActivePrimaryAsync(scenario.TenantId, second.PublicId);
    }

    [Fact]
    public async Task Create_WithIsPrimaryTrue_WhenAnotherRepresentativeIsAlreadyPrimary_ShouldDemoteTheIncumbent()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId, "LegalRepresentatives.Admin"));

        await CreateRepresentativeAsync(client, scenario.TenantId, "Primero", "10000000-1", isPrimary: true);
        var second = await CreateRepresentativeAsync(client, scenario.TenantId, "Segundo", "20000000-2", isPrimary: true);

        await AssertSingleActivePrimaryAsync(scenario.TenantId, second.PublicId);
    }

    /// <summary>
    /// B-04 — <b>el primero es principal aunque no se pida.</b> Una empresa con representantes activos y ninguno
    /// principal no es un estado que el negocio quiera, y el flag no se expone en el formulario (F-06): la
    /// garantía tiene que ser del servidor, no del cliente.
    /// <para>
    /// Antes, crear el primero con <c>isPrimary: false</c> lo dejaba sin principal — el handler solo tenía lógica
    /// para <i>degradar</i> al anterior cuando llegaba un <c>true</c>, nunca para promover.
    /// </para>
    /// </summary>
    [Fact]
    public async Task Create_WhenItIsTheFirstRepresentative_ShouldBePrimaryEvenIfNotRequested()
    {
        var scenario = await ResetWithoutLegalRepresentativesAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId, "LegalRepresentatives.Admin"));

        var only = await CreateRepresentativeAsync(client, scenario.TenantId, "Unico", "10000000-1", isPrimary: false);

        Assert.True(only.IsPrimary, "El primer representante de una empresa debe quedar marcado como principal.");
        await AssertSingleActivePrimaryAsync(scenario.TenantId, only.PublicId);
    }

    /// <summary>
    /// B-04 — el segundo NO se promueve solo: ya hay un principal y elegir es del usuario. Es el contrapeso del
    /// test anterior; sin él, «promover siempre» pasaría por bueno.
    /// </summary>
    [Fact]
    public async Task Create_WhenAPrimaryAlreadyExists_ShouldRespectIsPrimaryFalse()
    {
        var scenario = await ResetWithoutLegalRepresentativesAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId, "LegalRepresentatives.Admin"));

        var first = await CreateRepresentativeAsync(client, scenario.TenantId, "Primero", "10000000-1", isPrimary: true);
        var second = await CreateRepresentativeAsync(client, scenario.TenantId, "Segundo", "20000000-2", isPrimary: false);

        Assert.False(second.IsPrimary);
        await AssertSingleActivePrimaryAsync(scenario.TenantId, first.PublicId);
    }

    /// <summary>
    /// B-04 — el tercer agujero del mismo invariante, y el que no estaba medido: <c>Inactivate()</c> pone
    /// <c>IsPrimary = false</c> y nadie promueve un reemplazo, así que dar de baja al principal con otros activos
    /// presentes dejaba la empresa <b>sin ninguno</b>.
    /// </summary>
    [Fact]
    public async Task Inactivate_WhenTheRepresentativeWasPrimary_ShouldPromoteAnotherActiveOne()
    {
        var scenario = await ResetWithoutLegalRepresentativesAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId, "LegalRepresentatives.Admin"));

        var primary = await CreateRepresentativeAsync(client, scenario.TenantId, "Principal", "10000000-1", isPrimary: true);
        var other = await CreateRepresentativeAsync(client, scenario.TenantId, "Suplente", "20000000-2", isPrimary: false);

        var response = await client.SendAsync(IfMatch(
            HttpMethod.Patch,
            $"/api/v1/legal-representatives/{primary.PublicId}/inactivate",
            primary.ConcurrencyToken));

        var payload = await response.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.OK == response.StatusCode, $"{(int)response.StatusCode}: {payload}");

        // La empresa sigue teniendo exactamente un principal activo, y es el que quedaba.
        await AssertSingleActivePrimaryAsync(scenario.TenantId, other.PublicId);
    }

    /// <summary>
    /// B-04 · guardrail del invariante (§5.4). Los tests de arriba fijan cada camino por separado; éste recorre
    /// la máquina de estados y comprueba <b>después de cada paso</b> que la empresa tiene exactamente un
    /// principal activo. Las combinaciones son donde se escondía el defecto: crear, promover y dar de baja
    /// funcionaban por separado.
    /// </summary>
    [Fact]
    public async Task LegalRepresentatives_ThroughTheWholeLifecycle_ShouldAlwaysKeepExactlyOneActivePrimary()
    {
        var scenario = await ResetWithoutLegalRepresentativesAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId, "LegalRepresentatives.Admin"));

        // 1. El primero, sin pedir que sea principal.
        var first = await CreateRepresentativeAsync(client, scenario.TenantId, "Uno", "10000000-1", isPrimary: false);
        await AssertSingleActivePrimaryAsync(scenario.TenantId, first.PublicId);

        // 2. Un segundo, tampoco principal: no debe robarle el puesto al primero.
        var second = await CreateRepresentativeAsync(client, scenario.TenantId, "Dos", "20000000-2", isPrimary: false);
        await AssertSingleActivePrimaryAsync(scenario.TenantId, first.PublicId);

        // 3. Promoción explícita del segundo.
        var promote = await client.SendAsync(IfMatch(
            HttpMethod.Patch,
            $"/api/v1/legal-representatives/{second.PublicId}/set-primary",
            second.ConcurrencyToken));
        Assert.Equal(HttpStatusCode.OK, promote.StatusCode);
        await AssertSingleActivePrimaryAsync(scenario.TenantId, second.PublicId);

        // 4. Baja del principal: el que queda hereda.
        var promoted = await promote.Content.ReadFromJsonAsync<PrimaryRepItem>(JsonOptions);
        var inactivate = await client.SendAsync(IfMatch(
            HttpMethod.Patch,
            $"/api/v1/legal-representatives/{second.PublicId}/inactivate",
            promoted!.ConcurrencyToken));
        Assert.Equal(HttpStatusCode.OK, inactivate.StatusCode);
        await AssertSingleActivePrimaryAsync(scenario.TenantId, first.PublicId);

        // 5. Reactivar al que se dio de baja NO le devuelve el puesto: sigue habiendo exactamente uno.
        var reactivated = await inactivate.Content.ReadFromJsonAsync<PrimaryRepItem>(JsonOptions);
        var activate = await client.SendAsync(IfMatch(
            HttpMethod.Patch,
            $"/api/v1/legal-representatives/{second.PublicId}/activate",
            reactivated!.ConcurrencyToken));
        Assert.Equal(HttpStatusCode.OK, activate.StatusCode);
        await AssertSingleActivePrimaryAsync(scenario.TenantId, first.PublicId);
    }
}

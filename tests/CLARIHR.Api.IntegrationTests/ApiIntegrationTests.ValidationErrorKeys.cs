using System.Net;
using System.Text.Json;
using CLARIHR.Application.Features.OrgStructureCatalogs.Common;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// 00005 / B-02 (reencuadra 00002 / B-02) — <b>la clave de un error de validación nombra el campo que el
/// cliente envió, no la propiedad interna.</b>
/// <para>
/// El frontend mapea <c>errors[clave]</c> a su control para pintar el mensaje junto al input. Cuando la clave
/// es el nombre interno —<c>search</c> para el parámetro público <c>q</c>, <c>locationGroupId</c> para el
/// campo <c>locationGroupPublicId</c>— ese mapeo no encuentra el control y el mensaje queda suelto al pie del
/// formulario, o no se muestra.
/// </para>
/// <para>
/// Medido al abrir el hallazgo: <b>40</b> parámetros <c>q→search</c> y <b>10</b> <c>page→pageNumber</c>, más
/// los campos de cuerpo que la convención <c>XxxId → xxxPublicId</c> renombra.
/// </para>
/// <para>
/// ⚠️ Hay <b>dos</b> caminos que producen un <c>400</c> y sólo uno normalizaba las claves: el de model-binding
/// pasa por <c>ProblemDetailsDefaults</c>, pero el de FluentValidation sale por
/// <c>ProblemDetailsFactory</c> — y es el que produce estos defectos.
/// </para>
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private static string UnitTypesUrl(Guid companyId) =>
        $"/api/v1/companies/{companyId}/organization-structure-catalogs/unit-types";

    /// <summary>
    /// El parámetro público es <c>q</c>; la propiedad de la query se llama <c>Search</c>. La clave del error
    /// tiene que decir <c>q</c>, que es lo único que el cliente conoce.
    /// </summary>
    [Fact]
    public async Task ValidationErrorKeys_ForARenamedQueryParameter_ShouldUseThePublicName()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(
                scenario.ActorUserId, scenario.TenantId, OrgStructureCatalogPermissionCodes.Read));

        // `q=a` incumple el mínimo de 2 caracteres del validador.
        var response = await client.GetAsync($"{UnitTypesUrl(scenario.TenantId)}?q=a");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors");

        Assert.True(
            errors.TryGetProperty("q", out _),
            "La clave debe ser `q`, que es el parámetro que el cliente envía.");
        Assert.False(
            errors.TryGetProperty("search", out _),
            "`search` es el nombre interno de la propiedad: el cliente no puede casarlo con ningún control suyo.");
    }

    /// <summary>
    /// El contrapeso: un parámetro que NO se renombra debe seguir saliendo con su propio nombre. Sin esto,
    /// una traducción demasiado agresiva pasaría por buena.
    /// </summary>
    [Fact]
    public async Task ValidationErrorKeys_ForAParameterThatIsNotRenamed_ShouldStayUnchanged()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(
                scenario.ActorUserId, scenario.TenantId, OrgStructureCatalogPermissionCodes.Read));

        var response = await client.GetAsync($"{UnitTypesUrl(scenario.TenantId)}?page=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = document.RootElement.GetProperty("errors");

        // El parámetro público es `page`; la propiedad se llama `PageNumber`. Mismo defecto que `q`.
        Assert.True(
            errors.TryGetProperty("page", out _),
            "La clave debe ser `page`, no `pageNumber`.");
    }
}

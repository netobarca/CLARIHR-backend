using System.Net;
using System.Text.Json;
using CLARIHR.Application.Features.Locations.Common;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// En un <c>400</c> de <i>model-binding</i> —un <c>uuid</c> mal formado, un JSON que no convierte— los
/// mensajes de <c>errors</c> salían traducidos pero <c>title</c> se quedaba en inglés, así que la misma
/// respuesta mezclaba los dos idiomas.
/// <para>
/// La causa es que ASP.NET deja el título por defecto ya puesto antes de llegar a
/// <c>ProblemDetailsDefaults</c>, y la asignación era <c>Title ??= …</c>: al no ser nulo, nunca
/// asignaba. El camino de FluentValidation no pasa por ahí y sí traducía, que es lo que ocultaba el
/// defecto.
/// </para>
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private const string TituloValidacionIngles = "One or more validation errors occurred.";
    private const string TituloValidacionEspanol = "Se encontraron uno o más errores de validación.";

    [Fact]
    public async Task ModelBindingProblem_WhenSpanishIsRequested_ShouldLocalizeTitleAndDetail()
    {
        var problema = await PedirConGuidMalFormadoAsync("es");

        var title = problema.GetProperty("title").GetString();
        Assert.Equal(TituloValidacionEspanol, title);

        // `detail` se deriva del título; si el título viaja en inglés, el detalle lo arrastra.
        if (problema.TryGetProperty("detail", out var detail) &&
            detail.ValueKind == JsonValueKind.String)
        {
            Assert.Equal(TituloValidacionEspanol, detail.GetString());
        }

        // Lo que ya funcionaba no debe cambiar: el mensaje del campo sigue en español.
        var mensajes = problema.GetProperty("errors").EnumerateObject()
            .SelectMany(campo => campo.Value.EnumerateArray().Select(m => m.GetString() ?? string.Empty))
            .ToArray();

        Assert.True(
            mensajes.Any(m => m.Contains("UUID", StringComparison.OrdinalIgnoreCase)),
            $"Se esperaba el mensaje de uuid inválido. Recibido: [{string.Join(" | ", mensajes)}]");
        Assert.DoesNotContain(mensajes, m => m.Contains("must be a valid", StringComparison.Ordinal));
    }

    /// <summary>
    /// Control: sin idioma pedido la respuesta sigue en inglés. Traducir el título no debe convertir el
    /// español en el idioma por defecto.
    /// </summary>
    [Fact]
    public async Task ModelBindingProblem_WhenNoLanguageIsRequested_ShouldStayInEnglish()
    {
        var problema = await PedirConGuidMalFormadoAsync(null);

        Assert.Equal(TituloValidacionIngles, problema.GetProperty("title").GetString());
    }

    private async Task<JsonElement> PedirConGuidMalFormadoAsync(string? acceptLanguage)
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(TestUserContext.Authenticated(
            scenario.ActorUserId, scenario.TenantId, LocationPermissionCodes.Admin));

        if (acceptLanguage is not null)
        {
            client.DefaultRequestHeaders.Add("Accept-Language", acceptLanguage);
        }

        // `locationGroupPublicId` es `Guid` en el request: un texto que no convierte falla en el
        // deserializador, antes de que ningún validador de negocio llegue a correr. Ése es el camino
        // que dejaba el título sin traducir.
        var response = await client.PostJsonAsync(
            $"/api/v1/companies/{scenario.TenantId}/work-centers",
            new
            {
                code = "CT-01",
                name = "Centro de prueba",
                workCenterTypePublicId = Guid.NewGuid(),
                locationGroupPublicId = "no-es-un-guid",
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var cuerpo = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(cuerpo).RootElement.Clone();
    }
}

using System.Net;
using System.Text;
using System.Text.Json;
using CLARIHR.Application.Features.OrgStructureCatalogs.Common;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// 00002 / B-01 — <b>los mensajes de validación deben salir en el idioma pedido.</b>
/// <para>
/// La ficha midió la cobertura contando usos de validador contra los patrones escritos a mano de
/// <c>TryTranslateFallback</c> y concluyó un 5 %. Esa cuenta asume que FluentValidation emite inglés
/// y que el traductor por patrones es el único camino. Estas pruebas miden el resultado real por el
/// cable, que es lo que ve el usuario.
/// </para>
/// </summary>
public sealed partial class ApiIntegrationTests
{
    /// <summary>
    /// Reglas por defecto de FluentValidation: <c>NotEmpty</c> (2 389 usos en <c>src/</c>) y
    /// <c>GreaterThanOrEqualTo</c>. Son las dos que más se emiten en todo el producto.
    /// </summary>
    [Fact]
    public async Task ValidationLocalization_ForBuiltInRules_ShouldAnswerInSpanish()
    {
        var mensajes = await PostInvalidUnitTypeAsync("es");

        Assert.True(
            mensajes.Any(m => m.Contains("vacío", StringComparison.Ordinal)),
            $"NotEmpty debe salir en español. Recibido:\n{Volcar(mensajes)}");
        Assert.True(
            mensajes.Any(m => m.Contains("mayor o igual", StringComparison.Ordinal)),
            $"GreaterThanOrEqualTo debe salir en español. Recibido:\n{Volcar(mensajes)}");
        Assert.False(
            mensajes.Any(m => m.Contains("must not be empty", StringComparison.Ordinal)
                           || m.Contains("must be greater than", StringComparison.Ordinal)),
            $"No debe quedar inglés. Recibido:\n{Volcar(mensajes)}");
    }

    /// <summary>
    /// Mensaje propio del producto vía <c>.WithMessage(...)</c>: «Code format is invalid.». Es una de las
    /// 155 frases escritas a mano, y de las 54 que sí casan un patrón del traductor.
    /// </summary>
    [Fact]
    public async Task ValidationLocalization_ForACustomMessage_ShouldAnswerInSpanish()
    {
        var mensajes = await PostInvalidUnitTypeAsync("es");

        Assert.False(
            mensajes.Any(m => m.Contains("format is invalid", StringComparison.Ordinal)),
            $"El mensaje propio debe traducirse. Recibido:\n{Volcar(mensajes)}");

        // 00002 / B-03 — y además tiene que DECIR cuál es el formato. «El formato del código no es
        // válido» está en español y sigue sin ser accionable: el usuario no sabe qué corregir.
        Assert.True(
            mensajes.Any(m => m.Contains("guion bajo", StringComparison.Ordinal)
                           && m.Contains("50 caracteres", StringComparison.Ordinal)),
            $"El mensaje debe nombrar los caracteres permitidos y el largo. Recibido:\n{Volcar(mensajes)}");
    }

    /// <summary>
    /// 00002 / B-04 — el nombre del campo dentro del mensaje también tiene que estar en español.
    /// Antes salía <c>'Sort Order' debe ser mayor o igual que '0'.</c>: frase en español, campo en inglés.
    /// </summary>
    [Fact]
    public async Task ValidationLocalization_ShouldUseBusinessLabelsForFieldNames()
    {
        var mensajes = await PostInvalidUnitTypeAsync("es");

        Assert.True(
            mensajes.Any(m => m.Contains("'Orden'", StringComparison.Ordinal)),
            $"`SortOrder` debe salir como 'Orden'. Recibido:\n{Volcar(mensajes)}");
        Assert.False(
            mensajes.Any(m => m.Contains("'Sort Order'", StringComparison.Ordinal)
                           || m.Contains("'Code'", StringComparison.Ordinal)
                           || m.Contains("'Name'", StringComparison.Ordinal)),
            $"No debe quedar ningún nombre de propiedad en inglés. Recibido:\n{Volcar(mensajes)}");
    }

    /// <summary>
    /// El contrapeso. Sin cabecera se responde en inglés: sin esta prueba, «localiza siempre en español»
    /// pasaría por buena y el producto habría cambiado de idioma para todos.
    /// </summary>
    [Fact]
    public async Task ValidationLocalization_WhenNoLanguageIsRequested_ShouldStayInEnglish()
    {
        var mensajes = await PostInvalidUnitTypeAsync(acceptLanguage: null);

        Assert.True(
            mensajes.Any(m => m.Contains("must not be empty", StringComparison.Ordinal)),
            $"Sin cabecera debe seguir en inglés. Recibido:\n{Volcar(mensajes)}");
    }

    private async Task<IReadOnlyList<string>> PostInvalidUnitTypeAsync(string? acceptLanguage)
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(
                scenario.ActorUserId, scenario.TenantId, OrgStructureCatalogPermissionCodes.Admin));

        if (acceptLanguage is not null)
        {
            client.DefaultRequestHeaders.Add("Accept-Language", acceptLanguage);
        }

        // `code` vacío dispara NotEmpty y el formato; `sortOrder` negativo dispara GreaterThanOrEqualTo.
        var response = await client.PostJsonAsync(
            UnitTypesUrl(scenario.TenantId),
            new { code = "", name = "", description = (string?)null, sortOrder = -1 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var mensajes = new List<string>();
        foreach (var campo in document.RootElement.GetProperty("errors").EnumerateObject())
        {
            foreach (var mensaje in campo.Value.EnumerateArray())
            {
                mensajes.Add($"{campo.Name}: {mensaje.GetString()}");
            }
        }

        return mensajes;
    }

    private static string Volcar(IReadOnlyList<string> mensajes)
    {
        var sb = new StringBuilder();
        foreach (var m in mensajes)
        {
            sb.AppendLine($"    {m}");
        }

        return sb.ToString();
    }
}

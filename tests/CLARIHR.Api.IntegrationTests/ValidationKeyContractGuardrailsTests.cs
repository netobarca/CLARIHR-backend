using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// La clave de un error de validación tiene que ser el <b>nombre público</b> del campo: es lo único que
/// permite al frontend pintar el error junto a su control. <c>PublicFieldNameMap</c> lo consigue leyendo
/// el renombre que declara MVC (<c>[FromQuery(Name = "q")] string? search</c>).
/// <para>
/// De ahí sale la invariante que fija este guardrail: <b>el parámetro de búsqueda se declara
/// <c>search</c> y se expone <c>q</c></b>. Si se declara directamente <c>q</c> no hay renombre que MVC
/// pueda contar, el mapa se queda sin la entrada <c>search → q</c> y el error sale con la clave interna
/// <c>search</c> mientras el parámetro público es <c>q</c> — justo el desajuste que el mapa existe para
/// cerrar.
/// </para>
/// </summary>
public sealed class ValidationKeyContractGuardrailsTests(IntegrationTestWebApplicationFactory factory)
    : IClassFixture<IntegrationTestWebApplicationFactory>
{
    [Fact]
    public void SearchParameter_ExposedAsQ_ShouldBeDeclaredAsSearch_SoItsValidationKeyIsPublic()
    {
        var actions = factory.Services
            .GetRequiredService<IActionDescriptorCollectionProvider>()
            .ActionDescriptors.Items
            .OfType<ControllerActionDescriptor>();

        var offenders = new List<string>();

        foreach (var action in actions)
        {
            foreach (var parameter in action.Parameters)
            {
                var publicName = parameter.BindingInfo?.BinderModelName ?? parameter.Name;

                // Sólo el parámetro que el cliente envía como `q`. Los que se exponen como `search`
                // ya coinciden con su clave interna y no tienen desajuste que cerrar.
                if (!string.Equals(publicName, "q", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.Equals(parameter.Name, "search", StringComparison.Ordinal))
                {
                    offenders.Add(
                        $"{action.ControllerName}.{action.ActionName} declara `{parameter.Name}` " +
                        "y lo expone como `q`");
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "El parámetro de búsqueda expuesto como `q` debe declararse " +
            "`[FromQuery(Name = \"q\")] string? search`; declarado de otra forma, el error de validación " +
            "sale con la clave interna en vez de `q`. Incumplen:" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders.Order()));
    }
}

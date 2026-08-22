using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CLARIHR.Api.Common;

/// <summary>
/// 00005 / B-02 — traduce el nombre interno de un campo al <b>nombre público</b> con el que el cliente lo
/// envió, para que la clave de un error de validación case con su control en el formulario.
/// <para>
/// Hay <b>dos</b> desajustes distintos y cada uno necesita su fuente de verdad:
/// </para>
/// <list type="number">
/// <item><b>Parámetros renombrados en el binding</b> — <c>[FromQuery(Name = "q")] string? search</c>. El
/// nombre público lo sabe MVC (<c>BinderModelName</c>): se lee de ahí, no se adivina. Medido: 40 casos
/// <c>q→search</c> y 10 <c>page→pageNumber</c>.</item>
/// <item><b>Campos del cuerpo renombrados por la convención <c>XxxId → xxxPublicId</c></b> (§9 de las
/// definiciones técnicas). Aquí la fuente de verdad es el <b>DTO público de la petición</b>: sólo se traduce
/// si ese DTO tiene realmente la propiedad <c>…PublicId</c>.</item>
/// </list>
/// <para>
/// ⚠️ <b>Por qué no se aplica la convención a ciegas.</b> §9 dice que los <c>Guid *Id</c> se exponen como
/// <c>*PublicId</c>, pero no es universal: <c>companyId</c> viaja en la ruta llamándose <c>companyId</c>.
/// Un <c>Id → PublicId</c> ciego habría renombrado esa clave a un campo que no existe, cambiando un desajuste
/// por otro.
/// </para>
/// </summary>
internal sealed class PublicFieldNameMap
{
    private static readonly PublicFieldNameMap Empty = new(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

    // El descriptor de la acción es estable por endpoint; el mapa se calcula una vez por acción.
    private static readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> Cache = new(StringComparer.Ordinal);

    private readonly IReadOnlyDictionary<string, string> _publicNames;

    private PublicFieldNameMap(IReadOnlyDictionary<string, string> publicNames) => _publicNames = publicNames;

    public static PublicFieldNameMap For(HttpContext? httpContext)
    {
        if (httpContext?.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>() is not { } descriptor)
        {
            return Empty;
        }

        return new PublicFieldNameMap(Cache.GetOrAdd(descriptor.Id, _ => Build(descriptor)));
    }

    /// <summary>
    /// Devuelve el nombre público del campo, o el mismo nombre si no se renombró. Nunca inventa: si el mapa no
    /// lo conoce, la clave sale tal cual.
    /// </summary>
    public string Resolve(string key) =>
        _publicNames.TryGetValue(key, out var publicName) ? publicName : key;

    /// <summary>
    /// §4.3 de las definiciones técnicas está 🔒: <b>«Todo listado: <c>page</c>, <c>pageSize</c>»</b>. Ése es el
    /// nombre público del producto; <c>PageNumber</c> es el nombre interno del <i>query object</i>.
    /// <para>
    /// Va aquí y no en el mapa por binding porque en la mayoría de los controllers el parámetro ya se llama
    /// <c>page</c> —no hay renombre que MVC pueda contar— y la traducción ocurre al construir la query. Es una
    /// convención <b>definida</b>, no un renombre ad-hoc: por eso se declara una vez y no controller a controller.
    /// </para>
    /// </summary>
    private static readonly KeyValuePair<string, string>[] ContractConventions =
    [
        new("PageNumber", "page"),
    ];

    private static IReadOnlyDictionary<string, string> Build(ControllerActionDescriptor descriptor)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var convention in ContractConventions)
        {
            map[convention.Key] = convention.Value;
        }

        foreach (var parameter in descriptor.Parameters)
        {
            // (1) Parámetro renombrado en el binding: el nombre público lo dice MVC.
            var binderName = parameter.BindingInfo?.BinderModelName;
            if (!string.IsNullOrWhiteSpace(binderName) &&
                !string.Equals(binderName, parameter.Name, StringComparison.Ordinal))
            {
                map[parameter.Name] = binderName!;
            }

            // (2) DTO del cuerpo: sólo se traduce a `…PublicId` si el DTO público lo declara así.
            if (parameter.BindingInfo?.BindingSource == BindingSource.Body)
            {
                AddPublicIdAliases(map, parameter.ParameterType);
            }
        }

        return map;
    }

    private static void AddPublicIdAliases(Dictionary<string, string> map, Type requestType)
    {
        foreach (var property in requestType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            const string suffix = "PublicId";
            if (!property.Name.EndsWith(suffix, StringComparison.Ordinal))
            {
                continue;
            }

            // `LocationGroupPublicId` (público) ← `LocationGroupId` (interno del comando).
            var internalName = string.Concat(property.Name.AsSpan(0, property.Name.Length - suffix.Length), "Id");
            if (!map.ContainsKey(internalName))
            {
                map[internalName] = property.Name;
            }
        }
    }
}

using System.Linq.Expressions;
using System.Reflection;
using FluentValidation;

namespace CLARIHR.Application.Common.Validation;

/// <summary>
/// 00002 / B-04 — <b>etiquetas de negocio para los nombres de propiedad dentro de los mensajes de
/// validación.</b>
/// <para>
/// FluentValidation sustituye el nombre de la propiedad C# partido en palabras, así que con
/// <c>Accept-Language: es</c> salía <c>'Sort Order' debe ser mayor o igual que '0'.</c> — frase en
/// español, campo en inglés.
/// </para>
/// <para>
/// El resolvedor es <b>inerte mientras no haya etiqueta</b>: si no hay ninguna catalogada devuelve
/// <c>null</c> y FluentValidation usa su comportamiento de siempre. Por eso poblar el catálogo es
/// incremental y no puede empeorar ningún mensaje: un campo sin etiqueta se comporta exactamente como
/// antes de existir este archivo.
/// </para>
/// <para>
/// ⚠️ <c>ValidatorOptions.Global</c> es estado estático de la librería. La búsqueda vive detrás de un
/// delegado que instala la capa de infraestructura —que es donde están los recursos—, de modo que la
/// capa de aplicación no necesita conocer el <c>.resx</c>. Sin ese delegado, todo cae al default.
/// </para>
/// </summary>
public static class ValidationDisplayNames
{
    private static Func<string, string?>? _resolver;

    /// <summary>Instala la búsqueda de etiquetas. La llama la composición de infraestructura.</summary>
    public static void UseResolver(Func<string, string?> resolver) =>
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));

    /// <summary>Deja el resolvedor sin instalar. Existe para que una prueba pueda volver al default.</summary>
    public static void ClearResolver() => _resolver = null;

    /// <summary>Engancha el resolvedor en FluentValidation. Idempotente.</summary>
    public static void Install() =>
        ValidatorOptions.Global.DisplayNameResolver = ResolveDisplayName;

    internal static string? ResolveDisplayName(Type type, MemberInfo? member, LambdaExpression? expression)
    {
        var resolver = _resolver;
        return resolver is null || member is null
            ? null
            : resolver(member.Name);
    }
}

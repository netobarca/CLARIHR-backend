using System.Globalization;
using System.Text;

namespace CLARIHR.Domain.Common;

/// <summary>
/// 00005 / B-01 (§2.10) — <b>la única regla de plegado para buscar y para guardar.</b>
/// <para>
/// El defecto que cierra: los repositorios comparaban <c>NormalizedName.Contains(search.ToUpperInvariant())</c>
/// y ninguno de los dos lados quitaba las tildes, así que <b>un nombre acentuado no se encontraba
/// escribiéndolo sin tilde</b>. Al medirlo había 14 filas creadas por usuarios cuyo <c>normalized_name</c>
/// ya llevaba tildes: quien buscara «estacion» no encontraba «Estación SAL».
/// </para>
/// <para>
/// ⚠️ <b>Tiene que usarse en los DOS lados.</b> Plegar solo la entrada de búsqueda dejaría de encontrar lo
/// que hoy sí se encuentra; plegar solo lo almacenado tampoco casaría. Por eso vive aquí y no en cada
/// módulo: una sola implementación que ambos lados comparten no puede divergir.
/// </para>
/// <para>
/// No es una regla nueva en el producto: <c>PositionDescriptionCatalogNormalization</c> e
/// <c>InternalCatalogValue</c> ya plegaban. Esto generaliza lo que ya era la convención en parte.
/// </para>
/// </summary>
public static class SearchTextNormalization
{
    /// <summary>
    /// Mayúsculas sin diacríticos: «Estación» → <c>ESTACION</c>, «Cañas» → <c>CANAS</c>.
    /// </summary>
    /// <remarks>
    /// La eñe se pliega a <c>N</c> igual que las tildes. Es deliberado y es lo que hace que buscar
    /// «canas» encuentre «Cañas» — que es justo el caso que motivó el hallazgo hermano de los
    /// departamentos sembrados.
    /// </remarks>
    public static string Fold(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var descompuesto = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(descompuesto.Length);

        foreach (var caracter in descompuesto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(caracter) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(caracter);
            }
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC)
            .ToUpperInvariant();
    }

    /// <summary>Recorta y pliega: la forma que espera el término de búsqueda que llega del cliente.</summary>
    public static string FoldSearchTerm(string? value) => Fold(value?.Trim());
}

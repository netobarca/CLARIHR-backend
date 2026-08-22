namespace CLARIHR.Application.Abstractions.Localization;

public interface IBackendMessageLocalizer
{
    string Localize(
        string key,
        string fallback,
        IReadOnlyList<object?>? arguments = null);

    string LocalizeValidationMessage(string fallback);

    /// <summary>
    /// Etiqueta de negocio para el nombre de una propiedad, o <c>null</c> si no hay ninguna catalogada.
    /// </summary>
    /// <remarks>
    /// 00002 / B-04 — FluentValidation sustituye el nombre de la propiedad C# partido en palabras
    /// (<c>SortOrder</c> → <c>'Sort Order'</c>), lo que deja una palabra inglesa dentro de una frase en
    /// español. Devolver <c>null</c> conserva ese comportamiento por defecto: el mecanismo es inerte
    /// mientras no exista una etiqueta, así que no puede empeorar ningún mensaje.
    /// </remarks>
    string? LocalizePropertyName(string propertyName);
}

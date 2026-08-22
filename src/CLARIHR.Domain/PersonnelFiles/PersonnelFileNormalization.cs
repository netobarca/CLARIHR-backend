using System.Text.RegularExpressions;
using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.PersonnelFiles;

public static partial class PersonnelFileNormalization
{
    public static string Clean(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{paramName} is required.", paramName);
        }

        return value.Trim();
    }

    public static string? CleanOptional(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    public static string NormalizeName(string value) =>
        SearchTextNormalization.Fold(Clean(value, nameof(value)));

    /// <summary>
    /// H-27 — normaliza un número de CUENTA BANCARIA quitando todo separador antes de pasar a mayúsculas, así la
    /// clave de unicidad es la cuenta y no cómo alguien la puntuó: <c>0001-1111-2222</c> y <c>000111112222</c> son
    /// una cuenta, no dos, y la plata va al mismo lugar. Mismo criterio que
    /// <c>LegalRepresentativeNormalization.NormalizeDocumentNumber</c>, que ya lo decidió para los documentos.
    /// Respalda el índice único de (expediente, banco, número normalizado, moneda).
    /// </summary>
    public static string NormalizeAccountNumber(string value) =>
        AccountSeparatorRegex().Replace(Clean(value, nameof(value)), string.Empty).ToUpperInvariant();

    [GeneratedRegex(@"[^A-Za-z0-9]", RegexOptions.CultureInvariant)]
    private static partial Regex AccountSeparatorRegex();

    public static string NormalizeCode(string value) =>
        Clean(value, nameof(value)).ToUpperInvariant();

    public static DateTime NormalizeDate(DateTime value) =>
        DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);

    public static DateTime? NormalizeDate(DateTime? value) =>
        value.HasValue ? NormalizeDate(value.Value) : null;
}

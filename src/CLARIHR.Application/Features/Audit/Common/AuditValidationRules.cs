namespace CLARIHR.Application.Features.Audit.Common;

/// <summary>
/// Canonical validation constants for the audit-log query surface. Mirrors the sibling
/// <c>*ValidationRules</c> classes (Locations / LegalRepresentatives / OrgUnits): the free-text search
/// must be at least <see cref="MinSearchLength"/> characters <b>after trimming</b>, so a whitespace-padded
/// single character (e.g. <c>" a"</c>) cannot bypass the floor and reach the repository as a broad
/// <c>LIKE '%a%'</c> scan.
/// </summary>
public static class AuditValidationRules
{
    public const int MinSearchLength = 2;

    // 00002 / B-03 — el texto es LITERAL, no interpolado. Con `$"…{MinSearchLength}…"` la clave que el
    // localizador deriva del mensaje se calcula en tiempo de ejecución, así que no se puede verificar de
    // forma estática que exista en el .resx — y no existía: los 37 sitios salían en inglés. Que el número
    // siga coincidiendo con la constante lo comprueba `CodeFormatMessageTests`.
    public const string SearchLengthMessage =
        "Search must be at least 2 characters when provided.";

    public static bool HasValidSearchLength(string? search) =>
        string.IsNullOrWhiteSpace(search) || search.Trim().Length >= MinSearchLength;
}

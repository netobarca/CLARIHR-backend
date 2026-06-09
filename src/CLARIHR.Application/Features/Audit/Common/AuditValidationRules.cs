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

    public static bool HasValidSearchLength(string? search) =>
        string.IsNullOrWhiteSpace(search) || search.Trim().Length >= MinSearchLength;
}

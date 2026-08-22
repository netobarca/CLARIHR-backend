using System.Text.RegularExpressions;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.Features.CostCenters.Common;

public static partial class CostCenterValidationRules
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    // §LR3 / §12.8 — free-text search (NormalizedCode/NormalizedName Contains → non-sargable
    // LIKE '%x%') must enforce a minimum trimmed length in the validator (rejected 400 before DB).
    // Threshold aligned with the LegalRepresentatives §LR3 / PositionSlots §PS2 precedent (2). Scale
    // assumption: cost centers per tenant are a small set, so the (TenantId, …) scan above the
    // minimum length is comfortably cheap. See project-foundation.md §12.8 / ADR-0002.
    public const int MinSearchLength = 2;

    // 00002 / B-03 — el texto es LITERAL, no interpolado. Con `$"…{MinSearchLength}…"` la clave que el
    // localizador deriva del mensaje se calcula en tiempo de ejecución, así que no se puede verificar de
    // forma estática que exista en el .resx — y no existía: los 37 sitios salían en inglés. Que el número
    // siga coincidiendo con la constante lo comprueba `CodeFormatMessageTests`.
    public const string SearchLengthMessage =
        "Search must be at least 2 characters when provided.";

    // Single source of truth for the (TenantId, NormalizedCode) unique-index name — referenced by
    // both the EF mapping (CostCenterConfiguration) and the command handlers' duplicate-code race
    // backstop, so a rename cannot silently degrade the 23505 → clean 409 mapping into an HTTP 500.
    public const string CodeUniqueConstraintName = "uq_cost_centers__tenant_code";

    // Same single-sourcing for the cost-center-type catalog's (TenantId, NormalizedCode) unique
    // index, shared by CostCenterTypeConfiguration and the type handlers' 23505 → 409 mapping
    // (mirrors WorkCenterTypeCodeUniqueConstraintName in LocationValidationRules).
    public const string CostCenterTypeCodeUniqueConstraintName = "uq_cost_center_types__tenant_code";

    // 00002 / B-03 — el texto vive JUNTO a la regla, no junto al validador. Antes decía «Code format is
    // invalid.» en los 34 sitios que lo usan, sin decir cuál es el formato, y las reglas NO son iguales
    // entre sí (hay de 50 y de 80 caracteres, con juegos de caracteres distintos): un texto compartido
    // habría sido falso en la mayoría. `CodeFormatMessageTests` verifica que lo que dice esta frase sea
    // exactamente lo que acepta la regex de abajo.
    public const string CodeFormatMessage =
        "Code must start with a letter or number and may contain only letters, numbers, hyphen and underscore, up to 50 characters.";

    public static bool IsValidCode(string code) =>
        CodeRegex().IsMatch(code.Trim());

    public static bool IsValidSearchLength(string? search) =>
        string.IsNullOrWhiteSpace(search) || search.Trim().Length >= MinSearchLength;

    public static bool IsValidAccountCode(string accountCode) =>
        AccountCodeRegex().IsMatch(accountCode.Trim());

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_-]{0,49}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodeRegex();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_.-]{0,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex AccountCodeRegex();
}

public static class CostCenterPermissionCodes
{
    public const string Read = "CostCenters.Read";
    public const string Admin = "CostCenters.Admin";
    public const string ManageAdministration = "iam.administration.manage";
    public const string ResourceKey = "COST_CENTERS";
}

public static class CostCenterErrors
{
    public static readonly Error Forbidden = new(
        "COST_CENTERS_FORBIDDEN",
        "You do not have permission to access cost center administration.",
        ErrorType.Forbidden);

    public static readonly Error CostCenterNotFound = new(
        "COST_CENTER_NOT_FOUND",
        "The cost center could not be found.",
        ErrorType.NotFound);

    public static readonly Error CodeConflict = new(
        "COST_CENTER_CODE_CONFLICT",
        "Another cost center already uses the requested code.",
        ErrorType.Conflict);

    public static readonly Error InUseConflict = new(
        "COST_CENTER_IN_USE",
        "The cost center cannot be inactivated because it is used by active organization units or position slots.",
        ErrorType.Conflict);

    public static readonly Error ConcurrencyConflict = new(
        "CONCURRENCY_CONFLICT",
        "The resource was modified by another request. Refresh and try again.",
        ErrorType.Conflict);

    public static readonly Error CostCenterTypeNotFound = new(
        "COST_CENTER_TYPE_NOT_FOUND",
        "The cost center type could not be found.",
        ErrorType.NotFound);

    public static readonly Error CostCenterTypeCodeConflict = new(
        "COST_CENTER_TYPE_CODE_CONFLICT",
        "Another cost center type already uses the requested code.",
        ErrorType.Conflict);

    public static readonly Error CostCenterTypeInUse = new(
        "COST_CENTER_TYPE_IN_USE",
        "The cost center type cannot be inactivated because active cost centers still use it.",
        ErrorType.Conflict);

    public static readonly Error CostCenterTypeInactive = new(
        "COST_CENTER_TYPE_INACTIVE",
        "The selected cost center type is inactive.",
        ErrorType.Conflict);

    public static readonly Error ExportFormatInvalid = new(
        "COST_CENTER_EXPORT_FORMAT_INVALID",
        "Unsupported export format.",
        ErrorType.Validation);

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch(CostCenterPermissionCodes.ResourceKey, action);
}

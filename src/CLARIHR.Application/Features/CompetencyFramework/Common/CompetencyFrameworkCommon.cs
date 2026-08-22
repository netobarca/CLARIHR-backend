using System.Text.RegularExpressions;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.Features.CompetencyFramework.Common;

public static partial class CompetencyFrameworkValidationRules
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    // Upper bounds for the collection-replace mutations (single source of truth, referenced by the
    // FluentValidation rules and their guardrail tests). The per-item N+1 was removed in F1/F4; these
    // also cap the *count* so one privileged request cannot submit an unbounded matrix/behavior set and
    // drive a huge in-memory build + bulk insert. Mirrors the `.Must(items.Count <= N)` convention used
    // by ReplaceCurrentUserSocialLinks. Generous for the domain, firm against abuse.
    public const int MaxMatrixItems = 200;
    public const int MaxConductsPerMatrixItem = 50;
    public const int MaxBehaviorsPerConduct = 50;

    // 00002 / B-03 — el texto vive JUNTO a la regla, no junto al validador. Antes decía «Code format is
    // invalid.» en los 34 sitios que lo usan, sin decir cuál es el formato, y las reglas NO son iguales
    // entre sí (hay de 50 y de 80 caracteres, con juegos de caracteres distintos): un texto compartido
    // habría sido falso en la mayoría. `CodeFormatMessageTests` verifica que lo que dice esta frase sea
    // exactamente lo que acepta la regex de abajo.
    public const string CodeFormatMessage =
        "Code must start with a letter or number and may contain only letters, numbers, hyphen and underscore, up to 50 characters.";

    public static bool IsValidCode(string code) =>
        CodeRegex().IsMatch(code.Trim());

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_-]{0,49}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodeRegex();
}

public static class CompetencyFrameworkPermissionCodes
{
    public const string Read = "CompetencyFramework.Read";
    public const string Admin = "CompetencyFramework.Admin";
    public const string ManageAdministration = "iam.administration.manage";
    public const string ResourceKey = "COMPETENCY_FRAMEWORK";
}

public static class CompetencyFrameworkErrors
{
    public static readonly Error Forbidden = new(
        "COMPETENCY_FRAMEWORK_FORBIDDEN",
        "You do not have permission to access competency framework administration.",
        ErrorType.Forbidden);

    public static readonly Error OccupationalPyramidLevelNotFound = new(
        "OCCUPATIONAL_PYRAMID_LEVEL_NOT_FOUND",
        "The occupational pyramid level could not be found.",
        ErrorType.NotFound);

    public static readonly Error OccupationalPyramidLevelCodeConflict = new(
        "OCCUPATIONAL_PYRAMID_LEVEL_CODE_CONFLICT",
        "Another occupational pyramid level already uses the requested code.",
        ErrorType.Conflict);

    public static readonly Error OccupationalPyramidLevelOrderConflict = new(
        "OCCUPATIONAL_PYRAMID_LEVEL_ORDER_CONFLICT",
        "Another occupational pyramid level already uses the requested level order.",
        ErrorType.Conflict);

    /// <summary>
    /// H-11 — the bulk reorder demands the COMPLETE set of levels, exactly once each. A partial or duplicated
    /// list is rejected rather than guessed at: whatever is omitted keeps its old rank, which can collide with
    /// the ranks just assigned, and on a strict ranking a half-applied order means nothing.
    /// </summary>
    public static readonly Error OccupationalPyramidLevelOrderSetIncomplete = new(
        "OCCUPATIONAL_PYRAMID_LEVEL_ORDER_SET_INCOMPLETE",
        "The reorder request must list every occupational pyramid level of the company exactly once.",
        ErrorType.UnprocessableEntity);

    public static readonly Error OccupationalPyramidLevelInUse = new(
        "OCCUPATIONAL_PYRAMID_LEVEL_IN_USE",
        "The occupational pyramid level cannot be inactivated while it has active usage.",
        ErrorType.Conflict);

    public static readonly Error CompetencyConductNotFound = new(
        "COMPETENCY_CONDUCT_NOT_FOUND",
        "The competency conduct could not be found.",
        ErrorType.NotFound);

    public static readonly Error CompetencyConductDuplicate = new(
        "COMPETENCY_CONDUCT_DUPLICATE",
        "A conduct already exists for the same competency, type, level and description.",
        ErrorType.Conflict);

    public static readonly Error CompetencyConductInUse = new(
        "COMPETENCY_CONDUCT_IN_USE",
        "The competency conduct cannot be inactivated while it is associated to active job profile expectations.",
        ErrorType.Conflict);

    public static readonly Error CompetencyConductBehaviorDuplicate = new(
        "COMPETENCY_CONDUCT_BEHAVIOR_DUPLICATE",
        "A behavior is referenced more than once in the request.",
        ErrorType.Conflict);

    public static readonly Error CompetencyNotFound = new(
        "COMPETENCY_NOT_FOUND",
        "The selected competency could not be found or is inactive.",
        ErrorType.NotFound);

    public static readonly Error CompetencyTypeNotFound = new(
        "COMPETENCY_TYPE_NOT_FOUND",
        "The selected competency type could not be found or is inactive.",
        ErrorType.NotFound);

    public static readonly Error BehaviorLevelNotFound = new(
        "BEHAVIOR_LEVEL_NOT_FOUND",
        "The selected behavior level could not be found or is inactive.",
        ErrorType.NotFound);

    public static readonly Error BehaviorNotFound = new(
        "BEHAVIOR_NOT_FOUND",
        "The selected behavior could not be found or is inactive.",
        ErrorType.NotFound);

    public static readonly Error JobProfileNotFound = new(
        "JOB_PROFILE_NOT_FOUND",
        "The job profile could not be found.",
        ErrorType.NotFound);

    public static readonly Error JobProfileCompetencyMatrixConflict = new(
        "JOB_PROFILE_COMPETENCY_MATRIX_CONFLICT",
        "The requested competency matrix change is not valid for the current state.",
        ErrorType.Conflict);

    /// <summary>
    /// H-06 — the conducts of one matrix item do not agree on their (competency, competency type, behaviour
    /// level) triple. A matrix item IS one cell of that triple, and the row's own
    /// <c>competencyCatalogItemId</c> / <c>competencyTypeCatalogItemId</c> / <c>behaviorLevelCatalogItemId</c>
    /// are derived from the conducts — so conducts that disagree have no single cell to define.
    /// <para>
    /// Split out of <see cref="JobProfileCompetencyMatrixConflict"/>, which covered several causes behind one
    /// message that distinguished none of them: a consumer reading the contract as "a list of conducts" got a
    /// 409 saying only that the change "is not valid for the current state". Same HTTP status on purpose — the
    /// delta for the frontend is one extra code, not a new status to handle.
    /// </para>
    /// </summary>
    public static readonly Error JobProfileCompetencyMatrixConductTripleMismatch = new(
        "JOB_PROFILE_COMPETENCY_MATRIX_CONDUCT_TRIPLE_MISMATCH",
        "All conducts of a competency matrix item must share the same competency, competency type and behaviour level. Split the conducts into one item per triple.",
        ErrorType.Conflict);

    public static readonly Error JobProfileCompetencyMatrixItemNotFound = new(
        "JOB_PROFILE_COMPETENCY_MATRIX_ITEM_NOT_FOUND",
        "The competency matrix item could not be found.",
        ErrorType.NotFound);

    public static readonly Error JobProfileCompetencyMatrixItemLimitReached = new(
        "JOB_PROFILE_COMPETENCY_MATRIX_ITEM_LIMIT_REACHED",
        "The job profile already has the maximum number of competency matrix items allowed.",
        ErrorType.Conflict);

    public static readonly Error ConcurrencyConflict = new(
        "CONCURRENCY_CONFLICT",
        "The resource was modified by another request. Refresh and try again.",
        ErrorType.Conflict);

    public static readonly Error ExportFormatInvalid = new(
        "COMPETENCY_FRAMEWORK_EXPORT_FORMAT_INVALID",
        "Unsupported export format.",
        ErrorType.Validation);

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch(CompetencyFrameworkPermissionCodes.ResourceKey, action);
}

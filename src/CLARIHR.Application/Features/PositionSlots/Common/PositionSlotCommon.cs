using System.Text.RegularExpressions;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.Features.PositionSlots.Common;

public static partial class PositionSlotValidationRules
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MaxGraphDepth = 15;

    // PS-C: single-sourced so the EF index name (PositionSlotConfiguration.HasDatabaseName) and the
    // UniqueConstraintViolationException guard (PositionSlotConstraintViolations) cannot drift apart.
    public const string CodeUniqueConstraintName = "uq_position_slots__tenant_code";

    // Free-text search guardrail (§PS2): the repository fans a non-sargable LIKE '%x%'
    // across 7+ Normalized* columns on a 6-table join, in both Search and Export.
    // Aligned with the PDC §P2 precedent (MinSearchLength: 2) — see
    // project-foundation.md §12.8 / ADR-0002.
    public const int MinSearchLength = 2;

    // 00002 / B-03 — el texto es LITERAL, no interpolado. Con `$"…{MinSearchLength}…"` la clave que el
    // localizador deriva del mensaje se calcula en tiempo de ejecución, así que no se puede verificar de
    // forma estática que exista en el .resx — y no existía: los 37 sitios salían en inglés. Que el número
    // siga coincidiendo con la constante lo comprueba `CodeFormatMessageTests`.
    public const string SearchLengthMessage =
        "Search must be at least 2 characters when provided.";
    public const int MaxSearchLength = 150;

    // Empty/whitespace search means "no filter" (the repository skips the predicate
    // via !string.IsNullOrWhiteSpace), so it is valid; otherwise enforce the minimum
    // length on the trimmed term.
    public static bool IsValidSearchLength(string? search) =>
        string.IsNullOrWhiteSpace(search) || search.Trim().Length >= MinSearchLength;

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

public static class PositionSlotPermissionCodes
{
    public const string Read = "PositionSlots.Read";
    public const string Admin = "PositionSlots.Admin";
    public const string ManageAdministration = "iam.administration.manage";
    public const string ResourceKey = "POSITION_SLOTS";
}

public static class PositionSlotErrors
{
    public static readonly Error Forbidden = new(
        "POSITION_SLOTS_FORBIDDEN",
        "You do not have permission to access position slot administration.",
        ErrorType.Forbidden);

    public static readonly Error PositionSlotNotFound = new(
        "POSITION_SLOT_NOT_FOUND",
        "The position slot could not be found.",
        ErrorType.NotFound);

    public static readonly Error JobProfileNotFound = new(
        "POSITION_SLOT_JOB_PROFILE_NOT_FOUND",
        "The selected job profile could not be found.",
        ErrorType.NotFound);

    public static readonly Error WorkCenterNotFound = new(
        "POSITION_SLOT_WORK_CENTER_NOT_FOUND",
        "The selected work center could not be found.",
        ErrorType.NotFound);

    public static readonly Error RoleNotFound = new(
        "POSITION_SLOT_ROLE_NOT_FOUND",
        "The selected role could not be found.",
        ErrorType.NotFound);

    public static readonly Error JobProfileOrgUnitNotConfigured = new(
        "POSITION_SLOT_JOB_PROFILE_ORG_UNIT_NOT_CONFIGURED",
        "The selected job profile does not have an organization unit configured.",
        ErrorType.UnprocessableEntity);

    public static readonly Error ContractTypeNotResolved = new(
        "POSITION_SLOT_CONTRACT_TYPE_NOT_RESOLVED",
        "The selected job profile does not resolve to an active contract type.",
        ErrorType.UnprocessableEntity);

    /// <summary>
    /// H-14 — the configured salary falls outside the plaza's own salary band. The band comes from the job
    /// profile's active tabulator line, which the company defined and <b>approved with two signatures</b>;
    /// letting the plaza sit outside it made that approval decorative and propagated a capture error to every
    /// occupant. The way to make an exception is to change the band through the tabulator, which has approval
    /// — not to bypass it here.
    /// </summary>
    public static readonly Error ConfiguredSalaryOutOfBand = new(
        "POSITION_SLOT_CONFIGURED_SALARY_OUT_OF_BAND",
        "The configured base salary falls outside the salary band of the position's job profile.",
        ErrorType.UnprocessableEntity);

    /// <summary>
    /// H-15 — the slot cannot be deleted because something still references it. This guard IS the feature:
    /// nothing has a foreign key to <c>position_slots</c> except its own self-references, so a raw delete does
    /// not fail — it silently orphans employment history. Blocked by ANY assignment (a historical row orphans
    /// just as badly as a live one), by contract history, authorization substitutions, exit-interview
    /// submissions, or by another slot depending on this one.
    /// </summary>
    public static readonly Error InUse = new(
        "POSITION_SLOT_IN_USE",
        "The position slot cannot be deleted because it is still referenced by employment records or by other position slots. Suspend it instead if the position no longer exists.",
        ErrorType.Conflict);

    /// <summary>
    /// H-15 — suspending a slot that still has active assignments was allowed and left the aggregate
    /// incoherent: <c>IsActive</c> went false while its occupants stayed assigned. Checked against the real
    /// assignments, NOT <c>OccupiedEmployees</c> — that counter is only written by the slot's own
    /// <c>/occupancy</c> endpoint, so trusting it would miss exactly this case.
    /// </summary>
    public static readonly Error SuspendWithOccupants = new(
        "POSITION_SLOT_SUSPEND_WITH_OCCUPANTS",
        "The position slot cannot be suspended while it still has active assignments. Release its occupants first.",
        ErrorType.UnprocessableEntity);

    /// <summary>
    /// H-14 — a base salary was supplied for a plaza whose job profile has no active salary band. With no
    /// bounds to compare against there is no way to assert the salary is within limits, so it cannot be
    /// configured. Leaving it through was the bypass this closes: the plaza kept a salary nobody had ever
    /// validated, and adding the tabulator line later did not revalidate it.
    /// <para>
    /// This does NOT make the profile's compensation a prerequisite for the plaza to exist — only for it to
    /// carry a configured salary. A plaza with no salary is still created normally.
    /// </para>
    /// </summary>
    public static readonly Error JobProfileHasNoSalaryBand = new(
        "POSITION_SLOT_JOB_PROFILE_HAS_NO_SALARY_BAND",
        "The position's job profile has no active salary band, so a base salary cannot be configured. Configure the job profile's compensation first.",
        ErrorType.UnprocessableEntity);

    /// <summary>
    /// H-14 — the configured salary is expressed in a different currency than the band. Comparing amounts
    /// across currencies is meaningless, so this is refused rather than silently compared or skipped.
    /// </summary>
    public static readonly Error ConfiguredSalaryCurrencyMismatch = new(
        "POSITION_SLOT_CONFIGURED_SALARY_CURRENCY_MISMATCH",
        "The configured base salary currency does not match the currency of the job profile's salary band.",
        ErrorType.UnprocessableEntity);

    /// <summary>
    /// H-01 — a plaza can only exist against an APPROVED job descriptor. 422 (not 404) on purpose: the
    /// profile exists and the caller can see it in the picker; what is unprocessable is its state, and the
    /// remedy is to publish it.
    /// </summary>
    public static readonly Error JobProfileNotPublished = new(
        "POSITION_SLOT_JOB_PROFILE_NOT_PUBLISHED",
        "The selected job profile is not published. Publish the job profile before creating or updating position slots.",
        ErrorType.UnprocessableEntity);

    /// <summary>
    /// 00950/B-02 — la unidad organizativa del perfil está de baja.
    /// </summary>
    /// <remarks>
    /// El bloqueo va aquí y no en la inactivación de la unidad porque los tres saltos de la cadena
    /// —inactivar la unidad, mantener el perfil, crear la plaza— no son igual de defendibles. Cerrar un
    /// departamento es legítimo y su histórico debe conservarse, así que inactivar no se toca y las plazas
    /// existentes siguen válidas. Lo que no tiene defensa es crear plazas NUEVAS bajo una unidad de baja:
    /// eso no preserva histórico, crea futuro. Decisión de producto del 2026-08-16.
    /// </remarks>
    // 00950 / B-02 (§3.6) — el otro padre de la plaza. La asimetria delataba la omision: el TIPO de
    // centro ya estaba protegido (WORK_CENTER_TYPE_INACTIVE) y el centro mismo no.
    public static readonly Error WorkCenterInactive = new(
        "POSITION_SLOT_WORK_CENTER_INACTIVE",
        "The selected work center is inactive. Reactivate it before creating or updating position slots against it.",
        ErrorType.UnprocessableEntity);

    public static readonly Error OrgUnitInactive = new(
        "POSITION_SLOT_ORG_UNIT_INACTIVE",
        "The job profile's organization unit is inactive. Reactivate the unit before creating or updating position slots under it.",
        ErrorType.UnprocessableEntity);

    public static readonly Error CostCenterInvalid = new(
        "POSITION_SLOT_COST_CENTER_INVALID",
        "The cost center inferred from the job profile organization unit does not exist or is inactive for the company.",
        ErrorType.UnprocessableEntity);

    public static readonly Error DependencyNotFound = new(
        "POSITION_SLOT_DEPENDENCY_NOT_FOUND",
        "The selected dependency position slot could not be found.",
        ErrorType.NotFound);

    public static readonly Error CodeConflict = new(
        "POSITION_SLOT_CODE_CONFLICT",
        "Another position slot already uses the requested code.",
        ErrorType.Conflict);

    // PS-D: covers both the direct and the functional dependency (the functional side previously
    // validated only self-reference); the message is dependency-type-agnostic.
    public static readonly Error DependencyCycle = new(
        "POSITION_SLOT_DEPENDENCY_CYCLE",
        "The requested dependency would create a cycle.",
        ErrorType.Conflict);

    public static readonly Error DependencySelfReference = new(
        "POSITION_SLOT_DEPENDENCY_SELF_REFERENCE",
        "A position slot cannot depend on itself.",
        ErrorType.Conflict);

    public static readonly Error StatusConflict = new(
        "POSITION_SLOT_STATUS_CONFLICT",
        "The requested status change is not allowed for the current occupancy.",
        ErrorType.Conflict);

    // §PS6: create rejects a contradictory status+occupancy pair instead of silently
    // coercing. Validation of the submitted payload → 422 (not a conflict with state).
    public static readonly Error StatusOccupancyMismatch = new(
        "POSITION_SLOT_STATUS_OCCUPANCY_MISMATCH",
        "Occupied employees must match the position slot status: vacant requires zero, occupied requires at least one.",
        ErrorType.UnprocessableEntity);

    public static readonly Error SuspendedOccupancyConflict = new(
        "POSITION_SLOT_SUSPENDED_OCCUPANCY_CONFLICT",
        "Suspended position slots cannot update occupancy.",
        ErrorType.Conflict);

    public static readonly Error CapacityRuleViolation = new(
        "POSITION_SLOT_CAPACITY_RULE_VIOLATION",
        "Occupied employees must be between zero and max employees.",
        ErrorType.UnprocessableEntity);

    public static readonly Error EffectiveDatesInvalid = new(
        "POSITION_SLOT_EFFECTIVE_DATES_INVALID",
        "Effective date range is invalid.",
        ErrorType.UnprocessableEntity);

    public static readonly Error ExportFormatInvalid = new(
        "POSITION_SLOT_EXPORT_FORMAT_INVALID",
        "Unsupported export format.",
        ErrorType.Validation);

    public static readonly Error DiagramFormatInvalid = new(
        "POSITION_SLOT_DIAGRAM_FORMAT_INVALID",
        "Unsupported diagram export format.",
        ErrorType.Validation);

    public static readonly Error ConcurrencyConflict = new(
        "CONCURRENCY_CONFLICT",
        "The resource was modified by another request. Refresh and try again.",
        ErrorType.Conflict);

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch(PositionSlotPermissionCodes.ResourceKey, action);
}

public static class PositionSlotConstraintViolations
{
    // PS-C: a concurrent create that loses the race to the (TenantId, NormalizedCode) unique index
    // surfaces as this Postgres constraint; mapping it to a clean 409 mirrors CostCenters R2 /
    // OrgUnits OU-004 / OrgStructureCatalogs OSC-005.
    public static bool IsCodeConflict(string? constraintName) =>
        string.Equals(constraintName, PositionSlotValidationRules.CodeUniqueConstraintName, StringComparison.Ordinal);
}

public static class PositionSlotContractTypeRules
{
    public static bool IsFixedTerm(string? contractTypeCode, string? contractTypeName)
    {
        var code = (contractTypeCode ?? string.Empty).Trim().ToUpperInvariant();
        var name = (contractTypeName ?? string.Empty).Trim().ToUpperInvariant();
        return code.Contains("TEMP", StringComparison.Ordinal) ||
               code.Contains("FIXED", StringComparison.Ordinal) ||
               name.Contains("TEMPORAL", StringComparison.Ordinal) ||
               name.Contains("PLAZO", StringComparison.Ordinal) ||
               name.Contains("FIJO", StringComparison.Ordinal) ||
               name.Contains("FIXED", StringComparison.Ordinal);
    }
}

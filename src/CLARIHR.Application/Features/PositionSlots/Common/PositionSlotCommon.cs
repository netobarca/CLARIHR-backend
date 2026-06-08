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
    public const int MaxSearchLength = 150;

    // Empty/whitespace search means "no filter" (the repository skips the predicate
    // via !string.IsNullOrWhiteSpace), so it is valid; otherwise enforce the minimum
    // length on the trimmed term.
    public static bool IsValidSearchLength(string? search) =>
        string.IsNullOrWhiteSpace(search) || search.Trim().Length >= MinSearchLength;

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

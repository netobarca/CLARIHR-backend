using System.Text.RegularExpressions;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.Features.PositionSlots.Common;

public static partial class PositionSlotValidationRules
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MaxGraphDepth = 15;

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
    public const string PlatformAdminRole = "platform_admin";
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

    public static readonly Error OrgUnitNotFound = new(
        "POSITION_SLOT_ORG_UNIT_NOT_FOUND",
        "The selected organization unit could not be found.",
        ErrorType.NotFound);

    public static readonly Error WorkCenterNotFound = new(
        "POSITION_SLOT_WORK_CENTER_NOT_FOUND",
        "The selected work center could not be found.",
        ErrorType.NotFound);

    public static readonly Error ContractTypeNotResolved = new(
        "POSITION_SLOT_CONTRACT_TYPE_NOT_RESOLVED",
        "The selected job profile does not resolve to an active contract type.",
        ErrorType.UnprocessableEntity);

    public static readonly Error CostCenterInvalid = new(
        "POSITION_SLOT_COST_CENTER_INVALID",
        "The selected cost center code does not exist or is inactive for the company.",
        ErrorType.UnprocessableEntity);

    public static readonly Error DependencyNotFound = new(
        "POSITION_SLOT_DEPENDENCY_NOT_FOUND",
        "The selected dependency position slot could not be found.",
        ErrorType.NotFound);

    public static readonly Error CodeConflict = new(
        "POSITION_SLOT_CODE_CONFLICT",
        "Another position slot already uses the requested code.",
        ErrorType.Conflict);

    public static readonly Error DependencyCycle = new(
        "POSITION_SLOT_DEPENDENCY_CYCLE",
        "The requested direct dependency would create a cycle.",
        ErrorType.Conflict);

    public static readonly Error DependencySelfReference = new(
        "POSITION_SLOT_DEPENDENCY_SELF_REFERENCE",
        "A position slot cannot depend on itself.",
        ErrorType.Conflict);

    public static readonly Error StatusConflict = new(
        "POSITION_SLOT_STATUS_CONFLICT",
        "The requested status change is not allowed for the current occupancy.",
        ErrorType.Conflict);

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

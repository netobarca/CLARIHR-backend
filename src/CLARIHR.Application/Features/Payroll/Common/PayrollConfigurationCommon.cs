using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.Features.Payroll.Common;

/// <summary>
/// Shared validation thresholds for the payroll configuration masters (payroll definitions — REQ-012 PR-1;
/// work schedules join in PR-3). Mirrors <c>LeaveConfigurationValidationRules</c>: the free-text search
/// (Normalized* Contains → non-sargable LIKE '%x%') enforces a minimum trimmed length in the validator so
/// it is rejected with 400 before hitting the database.
/// </summary>
public static class PayrollConfigurationValidationRules
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MinSearchLength = 2;

    /// <summary>
    /// Upper sanity bound for <c>totalPeriods</c>: the coherence with the canonical periods-per-year of the
    /// fixed frequencies is SOFT (<c>PayrollFrequencies</c> — a 13th aguinaldo run is deliberate), so the
    /// wire only rejects nonsense beyond one period per day of the year.
    /// </summary>
    public const int MaxTotalPeriods = 366;

    public static bool IsValidSearchLength(string? search) =>
        string.IsNullOrWhiteSpace(search) || search.Trim().Length >= MinSearchLength;
}

/// <summary>
/// RBAC permission codes and resource keys for the payroll configuration masters (REQ-012 §4). One shared
/// Read/Manage pair governs the family (payroll definitions now; work schedules and the template load join
/// in PR-3 — a single configuration surface); each master keeps its own resource key for
/// <c>[ResourceActions]</c> / allowed-actions evaluation. Mirrors <c>LeaveConfigurationPermissionCodes</c>
/// (whose manage-level code is named <c>.Admin</c> for legacy reasons; this family uses the ratified
/// <c>.Manage</c> naming of the plan).
/// </summary>
public static class PayrollConfigurationPermissionCodes
{
    public const string Read = "PayrollConfiguration.Read";
    public const string Manage = "PayrollConfiguration.Manage";
    public const string ManageAdministration = "iam.administration.manage";

    /// <summary>
    /// Module-level resource key used only for the shared authorization service's TENANT_MISMATCH error
    /// metadata (the service is master-agnostic).
    /// </summary>
    public const string ModuleResourceKey = "PAYROLL_CONFIGURATION";

    public const string PayrollDefinitionsResourceKey = "PAYROLL_DEFINITIONS";

    public const string WorkSchedulesResourceKey = "WORK_SCHEDULES";
}

/// <summary>
/// Errors shared by every payroll-configuration master; master-specific errors live next to their
/// contracts (e.g. <c>PayrollDefinitionErrors</c>).
/// </summary>
public static class PayrollConfigurationErrors
{
    public static readonly Error Forbidden = new(
        "PAYROLL_CONFIGURATION_FORBIDDEN",
        "You do not have permission to access payroll configuration administration.",
        ErrorType.Forbidden);

    public static readonly Error ConcurrencyConflict = new(
        "CONCURRENCY_CONFLICT",
        "The resource was modified by another request. Refresh and try again.",
        ErrorType.Conflict);

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch(PayrollConfigurationPermissionCodes.ModuleResourceKey, action);
}

/// <summary>
/// Single source of truth for the unique-constraint names of the payroll configuration masters. The EF
/// configurations reference these constants and the handlers match them when translating a database unique
/// violation into a 409 (mirrors <c>OvertimeMasterConstraintNames</c>). Filtered unique (WHERE is_active):
/// a code can be reused after inactivation.
/// </summary>
public static class PayrollMasterConstraintNames
{
    public const string PayrollDefinitionCodeUnique = "uq_payroll_definitions__tenant_code_active";

    public const string WorkScheduleCodeUnique = "uq_work_schedules__tenant_code_active";
}

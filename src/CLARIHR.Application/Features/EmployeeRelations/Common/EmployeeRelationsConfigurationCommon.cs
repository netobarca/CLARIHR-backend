using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.Features.EmployeeRelations.Common;

/// <summary>
/// Shared validation thresholds for the employee-relations configuration masters (recognition types,
/// disciplinary-action types, disciplinary-action causes). Mirrors <c>LeaveConfigurationValidationRules</c>:
/// the free-text search (Normalized* Contains → non-sargable LIKE '%x%') enforces a minimum trimmed
/// length in the validator so it is rejected with 400 before hitting the database.
/// </summary>
public static class EmployeeRelationsConfigurationValidationRules
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MinSearchLength = 2;

    public static bool IsValidSearchLength(string? search) =>
        string.IsNullOrWhiteSpace(search) || search.Trim().Length >= MinSearchLength;
}

/// <summary>
/// RBAC permission codes and resource keys for the employee-relations configuration masters. One shared
/// Read/Admin pair governs the three masters (they are a single configuration surface); each master keeps
/// its own resource key for <c>[ResourceActions]</c> / allowed-actions evaluation. Mirrors
/// <c>LeaveConfigurationPermissionCodes</c>.
/// </summary>
public static class EmployeeRelationsConfigurationPermissionCodes
{
    public const string Read = "EmployeeRelationsConfiguration.Read";
    public const string Admin = "EmployeeRelationsConfiguration.Admin";
    public const string ManageAdministration = "iam.administration.manage";

    /// <summary>
    /// Module-level resource key used only for the shared authorization service's TENANT_MISMATCH error
    /// metadata (the service is master-agnostic).
    /// </summary>
    public const string ModuleResourceKey = "EMPLOYEE_RELATIONS_CONFIGURATION";

    public const string RecognitionTypesResourceKey = "RECOGNITION_TYPES";
    public const string DisciplinaryActionTypesResourceKey = "DISCIPLINARY_ACTION_TYPES";
    public const string DisciplinaryActionCausesResourceKey = "DISCIPLINARY_ACTION_CAUSES";
}

/// <summary>
/// Errors shared by every employee-relations configuration master; master-specific errors live next to
/// their contracts (e.g. <c>RecognitionTypeErrors</c>).
/// </summary>
public static class EmployeeRelationsConfigurationErrors
{
    public static readonly Error Forbidden = new(
        "EMPLOYEE_RELATIONS_CONFIGURATION_FORBIDDEN",
        "You do not have permission to access employee-relations configuration administration.",
        ErrorType.Forbidden);

    public static readonly Error ConcurrencyConflict = new(
        "CONCURRENCY_CONFLICT",
        "The resource was modified by another request. Refresh and try again.",
        ErrorType.Conflict);

    public static readonly Error DeductionConceptInvalid = new(
        "DEDUCTION_CONCEPT_INVALID",
        "The deduction concept could not be found, is inactive, or is not an expense (egreso) concept.",
        ErrorType.UnprocessableEntity);

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch(EmployeeRelationsConfigurationPermissionCodes.ModuleResourceKey, action);
}

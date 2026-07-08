using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.Features.Leave.Common;

/// <summary>
/// Shared validation thresholds for the leave-configuration masters (medical clinics, incapacity
/// risks/types, company holidays, payroll periods). Mirrors <c>CostCenterValidationRules</c>: the
/// free-text search (Normalized* Contains → non-sargable LIKE '%x%') enforces a minimum trimmed
/// length in the validator so it is rejected with 400 before hitting the database.
/// </summary>
public static class LeaveConfigurationValidationRules
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MinSearchLength = 2;

    public static bool IsValidSearchLength(string? search) =>
        string.IsNullOrWhiteSpace(search) || search.Trim().Length >= MinSearchLength;
}

/// <summary>
/// RBAC permission codes and resource keys for the leave-configuration masters. One shared
/// Read/Admin pair governs the five masters (they are a single configuration surface); each
/// master keeps its own resource key for <c>[ResourceActions]</c> / allowed-actions evaluation.
/// </summary>
public static class LeaveConfigurationPermissionCodes
{
    public const string Read = "LeaveConfiguration.Read";
    public const string Admin = "LeaveConfiguration.Admin";
    public const string ManageAdministration = "iam.administration.manage";

    /// <summary>
    /// Module-level resource key used only for the shared authorization service's
    /// TENANT_MISMATCH error metadata (the service is master-agnostic).
    /// </summary>
    public const string ModuleResourceKey = "LEAVE_CONFIGURATION";

    public const string MedicalClinicsResourceKey = "MEDICAL_CLINICS";
    public const string IncapacityRisksResourceKey = "INCAPACITY_RISKS";
    public const string IncapacityTypesResourceKey = "INCAPACITY_TYPES";
    public const string CompanyHolidaysResourceKey = "COMPANY_HOLIDAYS";
    public const string PayrollPeriodsResourceKey = "PAYROLL_PERIODS";
}

/// <summary>
/// Errors shared by every leave-configuration master; master-specific errors live next to their
/// contracts (e.g. <c>MedicalClinicErrors</c>).
/// </summary>
public static class LeaveConfigurationErrors
{
    public static readonly Error Forbidden = new(
        "LEAVE_CONFIGURATION_FORBIDDEN",
        "You do not have permission to access leave configuration administration.",
        ErrorType.Forbidden);

    public static readonly Error ConcurrencyConflict = new(
        "CONCURRENCY_CONFLICT",
        "The resource was modified by another request. Refresh and try again.",
        ErrorType.Conflict);

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch(LeaveConfigurationPermissionCodes.ModuleResourceKey, action);
}

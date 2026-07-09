namespace CLARIHR.Application.Features.PersonnelFiles.Overtime.Common;

/// <summary>
/// Shared validation thresholds for the overtime configuration masters (overtime types, overtime
/// justification types). Mirrors <c>EmployeeRelationsConfigurationValidationRules</c>: the free-text search
/// (Normalized* Contains → non-sargable LIKE '%x%') enforces a minimum trimmed length in the validator so
/// it is rejected with 400 before hitting the database.
/// </summary>
public static class OvertimeConfigurationValidationRules
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MinSearchLength = 2;

    public static bool IsValidSearchLength(string? search) =>
        string.IsNullOrWhiteSpace(search) || search.Trim().Length >= MinSearchLength;
}

/// <summary>
/// Resource keys for the overtime configuration masters (used by <c>[ResourceActions]</c> /
/// allowed-actions evaluation and the TENANT_MISMATCH error metadata). The authorization codes they assert
/// are the shared <c>PersonnelFiles.ViewOvertimeRecords</c> / <c>PersonnelFiles.ManageOvertimeRecords</c>
/// permissions (the masters have no dedicated permission codes — they reuse the record permissions, gated
/// through <c>IPersonnelFileAuthorizationService</c>).
/// </summary>
public static class OvertimeConfigurationResourceKeys
{
    public const string OvertimeTypes = "OVERTIME_TYPES";
    public const string OvertimeJustificationTypes = "OVERTIME_JUSTIFICATION_TYPES";
}

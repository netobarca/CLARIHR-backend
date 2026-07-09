namespace CLARIHR.Application.Features.PersonnelFiles.Overtime.Common;

/// <summary>
/// Authorization policy names for the overtime configuration masters (overtime types, overtime
/// justification types — REQ-007), referenced by
/// <c>[AuthorizationPolicySet(OvertimeConfigurationPolicies.Read, OvertimeConfigurationPolicies.Manage)]</c>
/// on the governed master controllers. These are policy identifiers, not RBAC permission strings — they
/// are STRICT (RequireAssertion) declarative policies wired in <c>Program.cs</c> over the SAME
/// <c>PersonnelFiles.ViewOvertimeRecords</c> / <c>PersonnelFiles.ManageOvertimeRecords</c> permission codes
/// used by the overtime records (the masters have no self-service channel, so — unlike the authn-only
/// record policies — they gate declaratively). Kept a superset of the precise
/// <c>IPersonnelFileAuthorizationService.EnsureCanView/ManageOvertimeRecordsAsync</c> handler gate (Read ⊇
/// EnsureCanViewOvertimeRecordsAsync, Manage ⊇ EnsureCanManageOvertimeRecordsAsync) so a legitimate caller
/// is never falsely 403'd. Mirrors <c>CostCenterPolicies</c> / <c>EmployeeRelationsConfigurationPolicies</c>.
/// </summary>
public static class OvertimeConfigurationPolicies
{
    public const string Read = "OvertimeConfiguration.Read";
    public const string Manage = "OvertimeConfiguration.Manage";
}

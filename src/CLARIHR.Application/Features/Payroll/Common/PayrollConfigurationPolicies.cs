namespace CLARIHR.Application.Features.Payroll.Common;

/// <summary>
/// Authorization policy names for the payroll configuration masters (payroll definitions — REQ-012 PR-1;
/// work schedules join in PR-3), referenced by
/// <c>[AuthorizationPolicySet(PayrollConfigurationPolicies.Read, PayrollConfigurationPolicies.Manage)]</c>
/// on the governed master controllers. These are policy identifiers, not RBAC permission strings — the
/// permission codes they assert live in <see cref="PayrollConfigurationPermissionCodes"/> and are wired to
/// these policies in <c>Program.cs</c> as STRICT (RequireAssertion) declarative policies (the masters have
/// no self-service channel). Kept a superset of the precise
/// <c>IPayrollConfigurationAuthorizationService</c> handler gate (Read ⊇ EnsureCanReadAsync, Manage ⊇
/// EnsureCanManageAsync) so a legitimate caller is never falsely 403'd. Mirrors
/// <c>LeaveConfigurationPolicies</c> / <c>OvertimeConfigurationPolicies</c>.
/// </summary>
public static class PayrollConfigurationPolicies
{
    public const string Read = "PayrollConfiguration.Read";
    public const string Manage = "PayrollConfiguration.Manage";
}

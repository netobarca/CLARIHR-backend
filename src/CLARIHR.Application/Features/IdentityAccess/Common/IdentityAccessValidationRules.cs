namespace CLARIHR.Application.Features.IdentityAccess.Common;

/// <summary>
/// Shared validation bounds for the IAM role/user write surface. The collection caps (A-4) bound the
/// *count* of ids a single privileged request can submit so it cannot drive an unbounded
/// <c>WHERE PublicId IN (@p0…@pN)</c> against <c>iam_permissions</c>/<c>iam_roles</c> (Npgsql parameter
/// pressure / plan-cache churn). Mirrors the <c>.Must(items.Count &lt;= N)</c> convention used by
/// CompetencyFramework / ReplaceCurrentUserSocialLinks. Generous for the domain (far above a
/// full-permission role or any realistic per-user role set), firm against abuse.
/// </summary>
public static class IdentityAccessValidationRules
{
    // NOTE: the numeric cap embedded in each validator's localized message must match these constants
    // (the guardrail test pins behaviour to the constant; the message text is localized via
    // BackendMessages.resx). Mirrors CompetencyFrameworkValidationRules.
    public const int MaxPermissionIdsPerRole = 1000;
    public const int MaxRoleIdsPerUser = 200;
}

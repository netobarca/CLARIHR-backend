using System.Text.RegularExpressions;
using CLARIHR.Application.Common.Errors;

namespace CLARIHR.Application.Features.CommercialPlans.Common;

public static partial class CommercialPlanValidationRules
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static bool IsValidPlanCode(string code) =>
        PlanCodeRegex().IsMatch(code.Trim());

    public static bool IsValidLimitCode(string code) =>
        LimitCodeRegex().IsMatch(code.Trim());

    public static bool HasSupportedScale(decimal value) =>
        decimal.Round(value, 2) == value;

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_-]{0,39}$", RegexOptions.CultureInvariant)]
    private static partial Regex PlanCodeRegex();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_-]{0,79}$", RegexOptions.CultureInvariant)]
    private static partial Regex LimitCodeRegex();
}

public static class CommercialPlanErrors
{
    public static readonly Error Forbidden = new(
        "COMMERCIAL_PLAN_FORBIDDEN",
        "You do not have permission to access commercial plan administration.",
        ErrorType.Forbidden);

    public static readonly Error NotFound = new(
        "COMMERCIAL_PLAN_NOT_FOUND",
        "The requested commercial plan could not be found.",
        ErrorType.NotFound);

    public static readonly Error CodeConflict = new(
        "COMMERCIAL_PLAN_CODE_CONFLICT",
        "Another commercial plan already uses the requested code.",
        ErrorType.Conflict);

    public static readonly Error ConcurrencyConflict = new(
        "CONCURRENCY_CONFLICT",
        "The resource was modified by another request. Refresh and try again.",
        ErrorType.Conflict);

    public static readonly Error AlreadyActive = new(
        "COMMERCIAL_PLAN_ALREADY_ACTIVE",
        "The commercial plan is already active.",
        ErrorType.Conflict);

    public static readonly Error AlreadyInactive = new(
        "COMMERCIAL_PLAN_ALREADY_INACTIVE",
        "The commercial plan is already inactive.",
        ErrorType.Conflict);

    public static readonly Error SystemPlanRenameForbidden = new(
        "COMMERCIAL_PLAN_SYSTEM_RENAME_FORBIDDEN",
        "System plans cannot change code or name.",
        ErrorType.Conflict);

    public static readonly Error SystemPlanInactivationForbidden = new(
        "COMMERCIAL_PLAN_SYSTEM_INACTIVATION_FORBIDDEN",
        "System plans cannot be inactivated.",
        ErrorType.Conflict);
}

using System.Text.RegularExpressions;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.Features.SalaryTabulator.Common;

public static partial class SalaryTabulatorValidationRules
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static bool IsValidCode(string value) =>
        CodeRegex().IsMatch(value.Trim());

    public static bool IsValidCurrency(string value) =>
        CurrencyRegex().IsMatch(value.Trim());

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_-]{0,49}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodeRegex();

    [GeneratedRegex(@"^[A-Za-z]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex CurrencyRegex();
}

public static class SalaryTabulatorPermissionCodes
{
    public const string Read = "SalaryTabulator.Read";
    public const string Request = "SalaryTabulator.Request";
    public const string Approve = "SalaryTabulator.Approve";
    public const string Admin = "SalaryTabulator.Admin";
    public const string ManageAdministration = "iam.administration.manage";
    public const string ResourceKey = "SALARY_TABULATOR";
}

public static class SalaryTabulatorErrors
{
    public static readonly Error Forbidden = new(
        "SALARY_TABULATOR_FORBIDDEN",
        "You do not have permission to access salary tabulator administration.",
        ErrorType.Forbidden);

    public static readonly Error LineNotFound = new(
        "SALARY_TABULATOR_LINE_NOT_FOUND",
        "The requested salary tabulator line could not be found.",
        ErrorType.NotFound);

    public static readonly Error SalaryClassNotFound = new(
        "SALARY_TABULATOR_SALARY_CLASS_NOT_FOUND",
        "The requested salary class could not be found or is inactive.",
        ErrorType.NotFound);

    public static readonly Error ChangeRequestNotFound = new(
        "SALARY_TABULATOR_REQUEST_NOT_FOUND",
        "The requested salary tabulator change request could not be found.",
        ErrorType.NotFound);

    public static readonly Error ChangeRequestStateConflict = new(
        "SALARY_TABULATOR_REQUEST_STATE_CONFLICT",
        "The requested operation is invalid for the current request state.",
        ErrorType.Conflict);

    public static readonly Error EffectiveDateOverlap = new(
        "SALARY_TABULATOR_EFFECTIVE_DATE_OVERLAP",
        "Another salary tabulator line already covers the requested effective date range.",
        ErrorType.Conflict);

    public static readonly Error AmountRuleViolation = new(
        "SALARY_TABULATOR_AMOUNT_RULE_VIOLATION",
        "The salary amount rules are invalid for the requested operation.",
        ErrorType.UnprocessableEntity);

    public static readonly Error ApprovalPolicyViolation = new(
        "SALARY_TABULATOR_APPROVAL_POLICY_VIOLATION",
        "The approval policy does not allow this operation.",
        ErrorType.UnprocessableEntity);

    public static readonly Error EffectiveDatesInvalid = new(
        "SALARY_TABULATOR_EFFECTIVE_DATES_INVALID",
        "The effective date range is invalid.",
        ErrorType.UnprocessableEntity);

    public static readonly Error ExportFormatInvalid = new(
        "SALARY_TABULATOR_EXPORT_FORMAT_INVALID",
        "Unsupported export format.",
        ErrorType.Validation);

    public static readonly Error RequestItemRequired = new(
        "SALARY_TABULATOR_REQUEST_ITEM_REQUIRED",
        "At least one request item is required.",
        ErrorType.Validation);

    public static readonly Error ConcurrencyConflict = new(
        "CONCURRENCY_CONFLICT",
        "The resource was modified by another request. Refresh and try again.",
        ErrorType.Conflict);

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch(SalaryTabulatorPermissionCodes.ResourceKey, action);
}

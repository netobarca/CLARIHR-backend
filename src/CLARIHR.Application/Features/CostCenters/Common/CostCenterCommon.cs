using System.Text.RegularExpressions;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;

namespace CLARIHR.Application.Features.CostCenters.Common;

public static partial class CostCenterValidationRules
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    // §LR3 / §12.8 — free-text search (NormalizedCode/NormalizedName Contains → non-sargable
    // LIKE '%x%') must enforce a minimum trimmed length in the validator (rejected 400 before DB).
    // Threshold aligned with the LegalRepresentatives §LR3 / PositionSlots §PS2 precedent (2). Scale
    // assumption: cost centers per tenant are a small set, so the (TenantId, …) scan above the
    // minimum length is comfortably cheap. See project-foundation.md §12.8 / ADR-0002.
    public const int MinSearchLength = 2;

    // Single source of truth for the (TenantId, NormalizedCode) unique-index name — referenced by
    // both the EF mapping (CostCenterConfiguration) and the command handlers' duplicate-code race
    // backstop, so a rename cannot silently degrade the 23505 → clean 409 mapping into an HTTP 500.
    public const string CodeUniqueConstraintName = "uq_cost_centers__tenant_code";

    public static bool IsValidCode(string code) =>
        CodeRegex().IsMatch(code.Trim());

    public static bool IsValidSearchLength(string? search) =>
        string.IsNullOrWhiteSpace(search) || search.Trim().Length >= MinSearchLength;

    public static bool IsValidAccountCode(string accountCode) =>
        AccountCodeRegex().IsMatch(accountCode.Trim());

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_-]{0,49}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodeRegex();

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_.-]{0,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex AccountCodeRegex();
}

public static class CostCenterPermissionCodes
{
    public const string Read = "CostCenters.Read";
    public const string Admin = "CostCenters.Admin";
    public const string ManageAdministration = "iam.administration.manage";
    public const string ResourceKey = "COST_CENTERS";
}

public static class CostCenterErrors
{
    public static readonly Error Forbidden = new(
        "COST_CENTERS_FORBIDDEN",
        "You do not have permission to access cost center administration.",
        ErrorType.Forbidden);

    public static readonly Error CostCenterNotFound = new(
        "COST_CENTER_NOT_FOUND",
        "The cost center could not be found.",
        ErrorType.NotFound);

    public static readonly Error CodeConflict = new(
        "COST_CENTER_CODE_CONFLICT",
        "Another cost center already uses the requested code.",
        ErrorType.Conflict);

    public static readonly Error InUseConflict = new(
        "COST_CENTER_IN_USE",
        "The cost center cannot be inactivated because it is used by active organization units or position slots.",
        ErrorType.Conflict);

    public static readonly Error ConcurrencyConflict = new(
        "CONCURRENCY_CONFLICT",
        "The resource was modified by another request. Refresh and try again.",
        ErrorType.Conflict);

    public static readonly Error ExportFormatInvalid = new(
        "COST_CENTER_EXPORT_FORMAT_INVALID",
        "Unsupported export format.",
        ErrorType.Validation);

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch(CostCenterPermissionCodes.ResourceKey, action);
}

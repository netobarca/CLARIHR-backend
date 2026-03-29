using System.Text.RegularExpressions;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.PositionDescriptionCatalogs;

namespace CLARIHR.Application.Features.PositionDescriptionCatalogs.Common;

public static partial class PositionDescriptionCatalogValidationRules
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    public static bool IsValidCode(string code) =>
        CodeRegex().IsMatch(code.Trim());

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9_-]{0,49}$", RegexOptions.CultureInvariant)]
    private static partial Regex CodeRegex();
}

public static class PositionDescriptionCatalogPermissionCodes
{
    public const string Read = "PositionDescriptionCatalogs.Read";
    public const string Admin = "PositionDescriptionCatalogs.Admin";
    public const string ManageAdministration = "iam.administration.manage";
    public const string ResourceKey = "POSITION_DESCRIPTION_CATALOGS";
}

public static class PositionDescriptionCatalogErrors
{
    public static readonly Error Forbidden = new(
        "POSITION_DESCRIPTION_CATALOG_FORBIDDEN",
        "You do not have permission to access position description catalogs.",
        ErrorType.Forbidden);

    public static readonly Error CatalogItemNotFound = new(
        "POSITION_DESCRIPTION_CATALOG_ITEM_NOT_FOUND",
        "The requested position description catalog item could not be found.",
        ErrorType.NotFound);

    public static readonly Error ClassificationNotFound = new(
        "POSITION_CATEGORY_CLASSIFICATION_NOT_FOUND",
        "The requested position category classification could not be found.",
        ErrorType.NotFound);

    public static readonly Error CategoryNotFound = new(
        "POSITION_CATEGORY_NOT_FOUND",
        "The requested position category could not be found.",
        ErrorType.NotFound);

    public static readonly Error CatalogCodeConflict = new(
        "POSITION_DESCRIPTION_CATALOG_CODE_CONFLICT",
        "Another catalog item already uses the requested code.",
        ErrorType.Conflict);

    public static readonly Error ClassificationCodeConflict = new(
        "POSITION_CATEGORY_CLASSIFICATION_CODE_CONFLICT",
        "Another classification already uses the requested code.",
        ErrorType.Conflict);

    public static readonly Error CategoryCodeConflict = new(
        "POSITION_CATEGORY_CODE_CONFLICT",
        "Another position category already uses the requested code.",
        ErrorType.Conflict);

    public static readonly Error ClassificationDuplicateAxes = new(
        "POSITION_CATEGORY_CLASSIFICATION_DUPLICATE_AXES",
        "A classification with the same function, contract and hierarchy already exists.",
        ErrorType.Conflict);

    public static readonly Error CatalogInUse = new(
        "POSITION_DESCRIPTION_CATALOG_IN_USE",
        "The catalog item cannot be inactivated while it is in use.",
        ErrorType.Conflict);

    public static readonly Error ClassificationInUse = new(
        "POSITION_CATEGORY_CLASSIFICATION_IN_USE",
        "The classification cannot be inactivated while it is in use.",
        ErrorType.Conflict);

    public static readonly Error CategoryInUse = new(
        "POSITION_CATEGORY_IN_USE",
        "The category cannot be inactivated while it is in use.",
        ErrorType.Conflict);

    public static readonly Error InvalidCatalogType = new(
        "POSITION_DESCRIPTION_CATALOG_INVALID_TYPE",
        "Unsupported catalog type for the requested operation.",
        ErrorType.Validation);

    public static readonly Error RelatedCatalogItemNotFound = new(
        "POSITION_DESCRIPTION_CATALOG_RELATED_ITEM_NOT_FOUND",
        "A required related catalog item could not be found or is inactive.",
        ErrorType.NotFound);

    public static readonly Error OrgUnitTypeNotFound = new(
        "ORG_UNIT_TYPE_NOT_FOUND",
        "The selected organization unit type could not be found or is inactive.",
        ErrorType.NotFound);

    public static readonly Error ConcurrencyConflict = new(
        "CONCURRENCY_CONFLICT",
        "The resource was modified by another request. Refresh and try again.",
        ErrorType.Conflict);

    public static readonly Error SalaryClassNotFound = new(
        "SALARY_CLASS_NOT_FOUND",
        "The requested salary class could not be found.",
        ErrorType.NotFound);

    public static readonly Error RequirementTypeNotFound = new(
        "REQUIREMENT_TYPE_NOT_FOUND",
        "The requested requirement type could not be found.",
        ErrorType.NotFound);

    public static readonly Error FrequencyNotFound = new(
        "FREQUENCY_NOT_FOUND",
        "The requested frequency could not be found.",
        ErrorType.NotFound);

    public static readonly Error WorkConditionTypeNotFound = new(
        "WORK_CONDITION_TYPE_NOT_FOUND",
        "The requested work condition type could not be found.",
        ErrorType.NotFound);

    public static Error TenantMismatch(RbacPermissionAction action) =>
        AuthorizationErrors.TenantMismatch(PositionDescriptionCatalogPermissionCodes.ResourceKey, action);

    public static bool IsSimpleCatalogType(PositionDescriptionCatalogType type) =>
        type is
            PositionDescriptionCatalogType.PositionFunctionType or
            PositionDescriptionCatalogType.PositionContractType or
            PositionDescriptionCatalogType.StrategicObjective or
            PositionDescriptionCatalogType.Frequency or
            PositionDescriptionCatalogType.RequirementType or
            PositionDescriptionCatalogType.Requirement or
            PositionDescriptionCatalogType.GeneralFunction or
            PositionDescriptionCatalogType.SalaryClass or
            PositionDescriptionCatalogType.WorkEquipment or
            PositionDescriptionCatalogType.Responsibility or
            PositionDescriptionCatalogType.Benefit or
            PositionDescriptionCatalogType.WorkConditionType or
            PositionDescriptionCatalogType.WorkCondition;
}

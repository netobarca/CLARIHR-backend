using CLARIHR.Domain.PositionDescriptionCatalogs;

namespace CLARIHR.Api.Controllers;

internal static class PositionDescriptionCatalogRouteMap
{
    private static readonly IReadOnlyDictionary<string, PositionDescriptionCatalogType> CatalogTypes =
        new Dictionary<string, PositionDescriptionCatalogType>(StringComparer.OrdinalIgnoreCase)
        {
            ["position-function-types"] = PositionDescriptionCatalogType.PositionFunctionType,
            ["position-contract-types"] = PositionDescriptionCatalogType.PositionContractType,
            ["strategic-objectives"] = PositionDescriptionCatalogType.StrategicObjective,
            ["frequencies"] = PositionDescriptionCatalogType.Frequency,
            ["requirement-types"] = PositionDescriptionCatalogType.RequirementType,
            ["requirements"] = PositionDescriptionCatalogType.Requirement,
            ["general-functions"] = PositionDescriptionCatalogType.GeneralFunction,
            ["salary-classes"] = PositionDescriptionCatalogType.SalaryClass,
            ["work-equipments"] = PositionDescriptionCatalogType.WorkEquipment,
            ["responsibilities-catalog"] = PositionDescriptionCatalogType.Responsibility,
            ["benefits-catalog"] = PositionDescriptionCatalogType.Benefit,
            ["work-condition-types"] = PositionDescriptionCatalogType.WorkConditionType,
            ["work-conditions"] = PositionDescriptionCatalogType.WorkCondition
        };

    public static bool TryResolve(string? slug, out PositionDescriptionCatalogType catalogType)
    {
        catalogType = default;
        return !string.IsNullOrWhiteSpace(slug) &&
            CatalogTypes.TryGetValue(slug.Trim(), out catalogType);
    }
}

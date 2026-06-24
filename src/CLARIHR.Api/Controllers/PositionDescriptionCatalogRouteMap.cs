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
            ["work-conditions"] = PositionDescriptionCatalogType.WorkCondition,
            ["competency-domains"] = PositionDescriptionCatalogType.CompetencyDomain
        };

    private static readonly Lazy<IReadOnlyDictionary<PositionDescriptionCatalogType, string>> CatalogTypeSlugs =
        new(() => CatalogTypes.ToDictionary(kvp => kvp.Value, kvp => kvp.Key));

    /// <summary>
    /// The accepted <c>{catalogType}</c> route slugs, in canonical order. Single source
    /// of truth for the OpenAPI enum surfaced by <c>CatalogTypeSlugOperationFilter</c>
    /// so the documented contract cannot drift from the resolver (debt §3.6 / §6.2).
    /// </summary>
    internal static IReadOnlyList<string> Slugs { get; } = [.. CatalogTypes.Keys];

    public static bool TryResolve(string? slug, out PositionDescriptionCatalogType catalogType)
    {
        catalogType = default;
        return !string.IsNullOrWhiteSpace(slug) &&
            CatalogTypes.TryGetValue(slug.Trim(), out catalogType);
    }

    public static string ToSlug(PositionDescriptionCatalogType catalogType)
    {
        return CatalogTypeSlugs.Value.TryGetValue(catalogType, out var slug)
            ? slug
            : catalogType.ToString().ToLowerInvariant();
    }
}

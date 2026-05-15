using CLARIHR.Api.Controllers;
using CLARIHR.Domain.PositionDescriptionCatalogs;
using Xunit;

namespace CLARIHR.Application.UnitTests;

public class PositionDescriptionCatalogRouteMapTests
{
    [Theory]
    [InlineData("position-function-types", PositionDescriptionCatalogType.PositionFunctionType)]
    [InlineData("position-contract-types", PositionDescriptionCatalogType.PositionContractType)]
    [InlineData("strategic-objectives", PositionDescriptionCatalogType.StrategicObjective)]
    [InlineData("frequencies", PositionDescriptionCatalogType.Frequency)]
    [InlineData("requirement-types", PositionDescriptionCatalogType.RequirementType)]
    [InlineData("requirements", PositionDescriptionCatalogType.Requirement)]
    [InlineData("general-functions", PositionDescriptionCatalogType.GeneralFunction)]
    [InlineData("salary-classes", PositionDescriptionCatalogType.SalaryClass)]
    [InlineData("work-equipments", PositionDescriptionCatalogType.WorkEquipment)]
    [InlineData("responsibilities-catalog", PositionDescriptionCatalogType.Responsibility)]
    [InlineData("benefits-catalog", PositionDescriptionCatalogType.Benefit)]
    [InlineData("work-condition-types", PositionDescriptionCatalogType.WorkConditionType)]
    [InlineData("work-conditions", PositionDescriptionCatalogType.WorkCondition)]
    [InlineData("  position-function-types  ", PositionDescriptionCatalogType.PositionFunctionType)]
    [InlineData("POSITION-FUNCTION-TYPES", PositionDescriptionCatalogType.PositionFunctionType)]
    public void TryResolve_ShouldReturnTrueAndCorrectType_ForValidSlugs(string slug, PositionDescriptionCatalogType expectedType)
    {
        // Act
        var result = PositionDescriptionCatalogRouteMap.TryResolve(slug, out var resolvedType);

        // Assert
        Assert.True(result);
        Assert.Equal(expectedType, resolvedType);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("invalid-slug")]
    public void TryResolve_ShouldReturnFalse_ForInvalidSlugs(string? slug)
    {
        // Act
        var result = PositionDescriptionCatalogRouteMap.TryResolve(slug, out var resolvedType);

        // Assert
        Assert.False(result);
        Assert.Equal(default, resolvedType);
    }

    [Theory]
    [InlineData(PositionDescriptionCatalogType.PositionFunctionType, "position-function-types")]
    [InlineData(PositionDescriptionCatalogType.PositionContractType, "position-contract-types")]
    [InlineData(PositionDescriptionCatalogType.StrategicObjective, "strategic-objectives")]
    [InlineData(PositionDescriptionCatalogType.Frequency, "frequencies")]
    [InlineData(PositionDescriptionCatalogType.RequirementType, "requirement-types")]
    [InlineData(PositionDescriptionCatalogType.Requirement, "requirements")]
    [InlineData(PositionDescriptionCatalogType.GeneralFunction, "general-functions")]
    [InlineData(PositionDescriptionCatalogType.SalaryClass, "salary-classes")]
    [InlineData(PositionDescriptionCatalogType.WorkEquipment, "work-equipments")]
    [InlineData(PositionDescriptionCatalogType.Responsibility, "responsibilities-catalog")]
    [InlineData(PositionDescriptionCatalogType.Benefit, "benefits-catalog")]
    [InlineData(PositionDescriptionCatalogType.WorkConditionType, "work-condition-types")]
    [InlineData(PositionDescriptionCatalogType.WorkCondition, "work-conditions")]
    public void ToSlug_ShouldReturnCorrectSlug_ForValidTypes(PositionDescriptionCatalogType type, string expectedSlug)
    {
        // Act
        var result = PositionDescriptionCatalogRouteMap.ToSlug(type);

        // Assert
        Assert.Equal(expectedSlug, result);
    }

    [Fact]
    public void ToSlug_ShouldFallbackToLowerCase_ForUnknownTypes()
    {
        // Act
        var unknownType = (PositionDescriptionCatalogType)999;
        var result = PositionDescriptionCatalogRouteMap.ToSlug(unknownType);

        // Assert
        Assert.Equal("999", result);
    }
}

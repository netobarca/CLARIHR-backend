using System.Reflection;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// GC4 guardrail (GeneralCatalogs audit). <see cref="GeneralCatalogKeyMap"/> is the single source of
/// truth for the public <c>catalogKey</c> wire segment ⇄ catalog <c>Category</c> mapping that
/// <c>GeneralCatalogsController</c> consumes. These tests assert a <b>bijection</b> between each
/// category-constant set (<see cref="PersonnelCurriculumCatalogCategories"/> /
/// <see cref="PersonnelReferenceCatalogCategories"/>) and its map: every category constant is
/// reachable by exactly one wire key, and every map value is a defined constant (no orphan literal,
/// no typo). Adding a category constant without wiring its wire key — or renaming a category in only
/// one place — then fails loudly here instead of degrading into a silent <c>400 Bad Request</c>.
/// </summary>
public sealed class GeneralCatalogKeyMapGuardrailsTests
{
    private static IReadOnlyCollection<string> ConstStrings(Type type) =>
        type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(field => field is { IsLiteral: true, IsInitOnly: false } && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToArray();

    [Fact]
    public void GeneralCatalogKeys_AreBijectiveWithCurriculumCategoryConstants()
    {
        var constants = ConstStrings(typeof(PersonnelCurriculumCatalogCategories)).ToHashSet(StringComparer.Ordinal);
        var mapped = GeneralCatalogKeyMap.CatalogKeys.Values.ToHashSet(StringComparer.Ordinal);

        var missingWireKey = constants.Except(mapped).OrderBy(static c => c, StringComparer.Ordinal).ToArray();
        var orphanMapValue = mapped.Except(constants).OrderBy(static c => c, StringComparer.Ordinal).ToArray();

        Assert.True(
            missingWireKey.Length == 0,
            "GC4: every PersonnelCurriculumCatalogCategories constant must be exposed by a " +
            "general-catalogs wire key in GeneralCatalogKeyMap.CatalogKeys. Unmapped (supported but " +
            "unreachable): " + string.Join(", ", missingWireKey));
        Assert.True(
            orphanMapValue.Length == 0,
            "GC4: every GeneralCatalogKeyMap.CatalogKeys value must be a defined " +
            "PersonnelCurriculumCatalogCategories constant (no orphan literal / typo). Offending: " +
            string.Join(", ", orphanMapValue));
    }

    [Fact]
    public void ReferenceCatalogKeys_AreBijectiveWithReferenceCategoryConstants()
    {
        var constants = ConstStrings(typeof(PersonnelReferenceCatalogCategories)).ToHashSet(StringComparer.Ordinal);
        var mapped = GeneralCatalogKeyMap.ReferenceCatalogKeys.Values.ToHashSet(StringComparer.Ordinal);

        var missingWireKey = constants.Except(mapped).OrderBy(static c => c, StringComparer.Ordinal).ToArray();
        var orphanMapValue = mapped.Except(constants).OrderBy(static c => c, StringComparer.Ordinal).ToArray();

        Assert.True(
            missingWireKey.Length == 0,
            "GC4: every PersonnelReferenceCatalogCategories constant must be exposed by a " +
            "reference-catalogs wire key in GeneralCatalogKeyMap.ReferenceCatalogKeys. Unmapped " +
            "(supported but unreachable): " + string.Join(", ", missingWireKey));
        Assert.True(
            orphanMapValue.Length == 0,
            "GC4: every GeneralCatalogKeyMap.ReferenceCatalogKeys value must be a defined " +
            "PersonnelReferenceCatalogCategories constant (no orphan literal / typo). Offending: " +
            string.Join(", ", orphanMapValue));
    }

    [Fact]
    public void WireKeys_AreLowercaseKebabAndUnique()
    {
        var allKeys = GeneralCatalogKeyMap.CatalogKeys.Keys
            .Concat(GeneralCatalogKeyMap.ReferenceCatalogKeys.Keys)
            .ToArray();

        Assert.All(allKeys, key => Assert.Equal(key.ToLowerInvariant(), key));
        Assert.Equal(allKeys.Length, allKeys.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Theory]
    [InlineData("  countries  ", "Country")]
    [InlineData("COUNTRIES", "Country")]
    public void TryResolveCatalogCategory_TrimsAndIsCaseInsensitive(string key, string expected)
    {
        Assert.True(GeneralCatalogKeyMap.TryResolveCatalogCategory(key, out var category));
        Assert.Equal(expected, category);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-catalog")]
    public void TryResolveCatalogCategory_RejectsUnknownOrBlankKeys(string? key)
    {
        Assert.False(GeneralCatalogKeyMap.TryResolveCatalogCategory(key, out var category));
        Assert.Equal(string.Empty, category);
    }
}

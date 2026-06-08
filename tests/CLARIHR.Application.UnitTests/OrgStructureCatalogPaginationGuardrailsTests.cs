using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.RegularExpressions;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Features.OrgStructureCatalogs.Common;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// OSC-005 (defense-in-depth) for the Organization Structure Catalogs list endpoints; mirrors
/// <see cref="OrgUnitPaginationGuardrailsTests"/>. Every paginated <c>pageSize</c> on the family MUST
/// constrain its bounds at the controller boundary with
/// <c>[Range(1, OrgStructureCatalogValidationRules.MaxPageSize)]</c> — the same bounds as the handler
/// FluentValidation — instead of relying solely on the validator. Structural pattern (namespace + name
/// regex), so a new family controller cannot silently regress.
/// </summary>
public sealed class OrgStructureCatalogPaginationGuardrailsTests
{
    private static readonly Assembly ApiAssembly = typeof(AuthorizationPolicySetAttribute).Assembly;

    private static readonly Regex FamilyRegex =
        new(@"^OrganizationStructureCatalogs", RegexOptions.Compiled);

    private static IReadOnlyList<MethodInfo> FamilyActions() =>
        ApiAssembly.GetTypes()
            .Where(static type =>
                type.IsClass &&
                !type.IsAbstract &&
                type.Namespace == "CLARIHR.Api.Controllers" &&
                type.Name.EndsWith("Controller", StringComparison.Ordinal) &&
                FamilyRegex.IsMatch(type.Name))
            .SelectMany(static type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            .Where(static method => !method.IsSpecialName)
            .ToArray();

    [Fact]
    public void OrgStructureCatalogFamily_ExposesPaginatedActions()
    {
        var pageSizeParams = FamilyActions()
            .SelectMany(static action => action.GetParameters())
            .Count(static parameter => parameter.Name == "pageSize");

        Assert.True(
            pageSizeParams > 0,
            "Expected at least one 'pageSize' parameter across the Organization Structure Catalogs " +
            "controller family. Zero means the namespace/name filter drifted and the guardrail would " +
            "silently pass.");
    }

    [Fact]
    public void EveryPageSize_DeclaresRangeMatchingTheHandlerValidator()
    {
        var violations = new List<string>();

        foreach (var action in FamilyActions())
        {
            foreach (var parameter in action.GetParameters())
            {
                if (parameter.Name != "pageSize")
                {
                    continue;
                }

                var qualifiedName = $"{action.DeclaringType!.Name}.{action.Name}('{parameter.Name}')";
                var range = parameter.GetCustomAttribute<RangeAttribute>();

                if (range is null)
                {
                    violations.Add($"{qualifiedName}: missing [Range].");
                    continue;
                }

                if (Convert.ToInt32(range.Minimum) != 1)
                {
                    violations.Add($"{qualifiedName}: [Range] Minimum is {range.Minimum}, expected 1.");
                }

                if (Convert.ToInt32(range.Maximum) != OrgStructureCatalogValidationRules.MaxPageSize)
                {
                    violations.Add(
                        $"{qualifiedName}: [Range] Maximum is {range.Maximum}, expected " +
                        $"{OrgStructureCatalogValidationRules.MaxPageSize} (OrgStructureCatalogValidationRules.MaxPageSize).");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Finding OSC-005 (defense-in-depth): every paginated 'pageSize' on an Organization Structure " +
            "Catalogs controller must declare [Range(1, OrgStructureCatalogValidationRules.MaxPageSize)] " +
            "at the controller boundary. Offending:\n  " +
            string.Join("\n  ", violations.OrderBy(static v => v, StringComparer.Ordinal)));
    }
}

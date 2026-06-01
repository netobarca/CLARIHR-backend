using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.RegularExpressions;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Features.PersonnelFiles.Common;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// PersonnelFiles pagination-bounds guardrail (defense-in-depth, mirrors the §J2/§N5
/// JobProfile remediation in <see cref="PaginationRangeGuardrailsTests"/>). Closes the audit
/// finding where <c>SearchPayrollTransactions</c> and <c>SearchPersonnelActions</c> shipped
/// with an unbounded <c>pageSize</c> (no <c>[Range]</c>, no handler validator, no repo clamp),
/// silently bypassing the <c>MaxPageSize=100</c> invariant every other paginated endpoint
/// enforces. Every paginated <c>pageSize</c> on a PersonnelFile family controller MUST
/// constrain its bounds at the controller boundary with
/// <c>[Range(1, PersonnelFileValidationRules.MaxPageSize)]</c> — the same bounds as the handler
/// FluentValidation — instead of relying solely on the handler validator. Structural pattern
/// (namespace + name regex), not a hand-maintained list, so a new PersonnelFile controller
/// cannot silently regress.
/// </summary>
public sealed class PersonnelFilePaginationRangeGuardrailsTests
{
    private static readonly Assembly ApiAssembly = typeof(AuthorizationPolicySetAttribute).Assembly;

    private static readonly Regex PersonnelFileFamilyRegex =
        new("^PersonnelFile", RegexOptions.Compiled);

    private static IReadOnlyList<MethodInfo> FamilyActions() =>
        ApiAssembly.GetTypes()
            .Where(static type =>
                type.IsClass &&
                !type.IsAbstract &&
                type.Namespace == "CLARIHR.Api.Controllers" &&
                type.Name.EndsWith("Controller", StringComparison.Ordinal) &&
                PersonnelFileFamilyRegex.IsMatch(type.Name))
            .SelectMany(static type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            .Where(static method => !method.IsSpecialName)
            .ToArray();

    [Fact]
    public void PersonnelFileFamily_ExposesPaginatedActions()
    {
        var pageSizeParams = FamilyActions()
            .SelectMany(static action => action.GetParameters())
            .Count(static parameter => parameter.Name == "pageSize");

        Assert.True(
            pageSizeParams > 0,
            "Expected at least one 'pageSize' parameter across the PersonnelFile controller " +
            "family. Zero means the namespace/name filter drifted and this guardrail would " +
            "silently pass — fix the filter.");
    }

    [Fact]
    public void EveryPersonnelFileFamilyPageSize_DeclaresRangeMatchingTheHandlerValidator()
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

                if (Convert.ToInt32(range.Maximum) != PersonnelFileValidationRules.MaxPageSize)
                {
                    violations.Add(
                        $"{qualifiedName}: [Range] Maximum is {range.Maximum}, expected " +
                        $"{PersonnelFileValidationRules.MaxPageSize} (PersonnelFileValidationRules.MaxPageSize).");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "PersonnelFiles pagination guardrail (defense-in-depth, mirrors §J2/§N5): every " +
            "paginated 'pageSize' on a PersonnelFile controller must declare " +
            "[Range(1, PersonnelFileValidationRules.MaxPageSize)] at the controller boundary, " +
            "not rely solely on the handler FluentValidation rule. Offending:\n  " +
            string.Join("\n  ", violations.OrderBy(static v => v, StringComparer.Ordinal)));
    }
}

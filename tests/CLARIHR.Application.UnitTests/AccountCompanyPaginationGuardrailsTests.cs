using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.RegularExpressions;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Features.AccountCompanies.Common;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// AC-6 pagination guardrail for the AccountCompanies list endpoint; mirrors
/// <see cref="CostCenterPaginationGuardrailsTests"/>. Every paginated <c>pageSize</c> on an
/// AccountCompanies controller MUST constrain its bounds at the controller boundary with
/// <c>[Range(1, AccountCompanyValidationRules.MaxPageSize)]</c> — the same bound as the handler
/// FluentValidation (<c>GetOwnedCompaniesQueryValidator</c>) — instead of relying solely on the handler
/// validator. Structural pattern (namespace + name regex), not a hand-maintained list, so a new family
/// controller cannot silently regress. The <c>^AccountCompanies</c> (plural) prefix is disjoint from the
/// singular <c>AccountCompanyAccess</c>/<c>AccountCompanyCatalogs</c> siblings (which expose no paginated
/// reads).
/// </summary>
public sealed class AccountCompanyPaginationGuardrailsTests
{
    private static readonly Assembly ApiAssembly = typeof(AuthorizationPolicySetAttribute).Assembly;

    private static readonly Regex AccountCompanyFamilyRegex =
        new(@"^AccountCompanies", RegexOptions.Compiled);

    private static IReadOnlyList<MethodInfo> FamilyActions() =>
        ApiAssembly.GetTypes()
            .Where(static type =>
                type.IsClass &&
                !type.IsAbstract &&
                type.Namespace == "CLARIHR.Api.Controllers" &&
                type.Name.EndsWith("Controller", StringComparison.Ordinal) &&
                AccountCompanyFamilyRegex.IsMatch(type.Name))
            .SelectMany(static type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            .Where(static method => !method.IsSpecialName)
            .ToArray();

    [Fact]
    public void AccountCompanyFamily_ExposesPaginatedActions()
    {
        var pageSizeParams = FamilyActions()
            .SelectMany(static action => action.GetParameters())
            .Count(static parameter => parameter.Name == "pageSize");

        Assert.True(
            pageSizeParams > 0,
            "Expected at least one 'pageSize' parameter across the AccountCompanies controller family. " +
            "Zero means the namespace/name filter drifted and the guardrail would silently pass.");
    }

    [Fact]
    public void EveryAccountCompanyPageSize_DeclaresRangeMatchingTheHandlerValidator()
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

                if (Convert.ToInt32(range.Maximum) != AccountCompanyValidationRules.MaxPageSize)
                {
                    violations.Add(
                        $"{qualifiedName}: [Range] Maximum is {range.Maximum}, expected " +
                        $"{AccountCompanyValidationRules.MaxPageSize} (AccountCompanyValidationRules.MaxPageSize).");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "AC-6 (defense-in-depth): every paginated 'pageSize' on an AccountCompanies controller must " +
            "declare [Range(1, AccountCompanyValidationRules.MaxPageSize)] at the controller boundary, not " +
            "rely solely on the handler FluentValidation rule. Offending:\n  " +
            string.Join("\n  ", violations.OrderBy(static v => v, StringComparer.Ordinal)));
    }
}

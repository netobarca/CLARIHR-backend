using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.RegularExpressions;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Features.JobProfiles.Common;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// §J2 guardrail (defense-in-depth, 🟡 media-baja). Mirrors the §N5 remediation that added
/// <c>[Range(1, MaxPageSize)]</c> to the 9 JobProfile paginated GET endpoints but missed
/// <see cref="CLARIHR.Api.Controllers.JobCatalogsController"/>. Every paginated
/// <c>pageSize</c> on a JobProfile/JobCatalog family controller MUST constrain its bounds
/// at the controller boundary with <c>[Range(1, JobProfileValidationRules.MaxPageSize)]</c>
/// — the same bounds as the handler FluentValidation — instead of relying solely on the
/// handler validator. Structural pattern (namespace + name regex), not a hand-maintained
/// list, so a new family controller cannot silently regress. Scope is the JobProfile/
/// JobCatalog family only: PositionDescriptionCatalog is covered by
/// JsonPatchHardeningTests.PositionCatalogListEndpoints_ShouldDeclarePageSizeRange (§4.2);
/// ~17 unrelated controllers in other modules are out of scope for §J2.
/// </summary>
public sealed class PaginationRangeGuardrailsTests
{
    private static readonly Assembly ApiAssembly = typeof(AuthorizationPolicySetAttribute).Assembly;

    private static readonly Regex JobProfileFamilyRegex =
        new(@"^(JobProfile|JobCatalog)", RegexOptions.Compiled);

    private static IReadOnlyList<MethodInfo> FamilyActions() =>
        ApiAssembly.GetTypes()
            .Where(static type =>
                type.IsClass &&
                !type.IsAbstract &&
                type.Namespace == "CLARIHR.Api.Controllers" &&
                type.Name.EndsWith("Controller", StringComparison.Ordinal) &&
                JobProfileFamilyRegex.IsMatch(type.Name))
            .SelectMany(static type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            .Where(static method => !method.IsSpecialName)
            .ToArray();

    [Fact]
    public void JobProfileFamily_ExposesPaginatedActions()
    {
        var pageSizeParams = FamilyActions()
            .SelectMany(static action => action.GetParameters())
            .Count(static parameter => parameter.Name == "pageSize");

        Assert.True(
            pageSizeParams > 0,
            "Expected at least one 'pageSize' parameter across the JobProfile/JobCatalog " +
            "controller family. Zero means the namespace/name filter drifted and the §J2 " +
            "guardrail would silently pass — fix the filter.");
    }

    [Fact]
    public void EveryJobProfileFamilyPageSize_DeclaresRangeMatchingTheHandlerValidator()
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

                if (Convert.ToInt32(range.Maximum) != JobProfileValidationRules.MaxPageSize)
                {
                    violations.Add(
                        $"{qualifiedName}: [Range] Maximum is {range.Maximum}, expected " +
                        $"{JobProfileValidationRules.MaxPageSize} (JobProfileValidationRules.MaxPageSize).");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Finding §J2 (defense-in-depth, mirrors §N5): every paginated 'pageSize' on a " +
            "JobProfile/JobCatalog controller must declare " +
            "[Range(1, JobProfileValidationRules.MaxPageSize)] at the controller boundary, " +
            "not rely solely on the handler FluentValidation rule. Offending:\n  " +
            string.Join("\n  ", violations.OrderBy(static v => v, StringComparer.Ordinal)));
    }
}

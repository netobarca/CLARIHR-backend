using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using CLARIHR.Api.Common.Conventions;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// §J3 guardrail (OpenAPI contract, 🟡 media). Mirrors the §6.1/§6.2 remediation that
/// gave the 10 JobProfile controllers class-level [Tags("Job Profiles")] + a
/// [SwaggerOperation] on every action but missed JobCatalogsController (Swagger then
/// rendered an orphan "JobCatalogs" group with 5 undocumented endpoints). Structural
/// pattern (namespace + name regex), not a hand-maintained list, so a new family
/// controller cannot silently regress the contract.
///
/// <para>[SwaggerOperation] is the NuGet Swashbuckle type (transitive via CLARIHR.Api),
/// compile-referenced like §J2's RangeAttribute. [Tags] is the ASP.NET endpoint-metadata
/// <c>Microsoft.AspNetCore.Http.TagsAttribute</c> — resolved on the controllers via the
/// Web SDK's implicit usings, NOT compile-available in this non-Web unit project — so it
/// is matched by simple type name and its <c>Tags</c> list read via reflection (the
/// §J1 framework-type-avoidance technique; namespace-agnostic and compiles
/// unconditionally). Scope: JobProfile/JobCatalog family only.</para>
/// </summary>
public sealed class OpenApiContractGuardrailsTests
{
    private const string ExpectedTag = "Job Profiles";
    private const string TagsAttributeSimpleName = "TagsAttribute";

    private static readonly Assembly ApiAssembly = typeof(AuthorizationPolicySetAttribute).Assembly;

    private static readonly Regex JobProfileFamilyRegex =
        new(@"^(JobProfile|JobCatalog)", RegexOptions.Compiled);

    private static IReadOnlyList<Type> FamilyControllers() =>
        ApiAssembly.GetTypes()
            .Where(static type =>
                type.IsClass &&
                !type.IsAbstract &&
                type.Namespace == "CLARIHR.Api.Controllers" &&
                type.Name.EndsWith("Controller", StringComparison.Ordinal) &&
                JobProfileFamilyRegex.IsMatch(type.Name))
            .OrderBy(static type => type.Name, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<MethodInfo> Actions(Type controller) =>
        controller.GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => !method.IsSpecialName && method.DeclaringType == controller)
            .ToArray();

    private static IReadOnlyList<string>? ReadTags(Type controller)
    {
        var tagsAttribute = controller.GetCustomAttributes(inherit: true)
            .FirstOrDefault(attr => attr.GetType().Name == TagsAttributeSimpleName);
        if (tagsAttribute is null)
        {
            return null;
        }

        var tagsProperty = tagsAttribute.GetType()
            .GetProperty("Tags", BindingFlags.Public | BindingFlags.Instance);
        if (tagsProperty?.GetValue(tagsAttribute) is not IEnumerable values)
        {
            return Array.Empty<string>();
        }

        return values.Cast<object?>()
            .Select(static value => value?.ToString() ?? string.Empty)
            .ToArray();
    }

    [Fact]
    public void JobProfileFamily_ExposesControllersWithActions()
    {
        var actionCount = FamilyControllers().Sum(controller => Actions(controller).Count);
        Assert.True(
            actionCount > 0,
            "Expected actions across the JobProfile/JobCatalog family. Zero means the " +
            "namespace/name filter drifted and the §J3 guardrail would silently pass.");
    }

    [Fact]
    public void EveryJobProfileFamilyController_DeclaresJobProfilesTag()
    {
        var violations = new List<string>();
        foreach (var controller in FamilyControllers())
        {
            var tags = ReadTags(controller);
            if (tags is null)
            {
                violations.Add($"{controller.Name}: missing class-level [Tags].");
            }
            else if (!tags.Contains(ExpectedTag, StringComparer.Ordinal))
            {
                violations.Add(
                    $"{controller.Name}: [Tags] is [{string.Join(", ", tags)}], expected " +
                    $"to contain \"{ExpectedTag}\".");
            }
        }

        Assert.True(
            violations.Count == 0,
            "Finding §J3 (§6.1): every JobProfile/JobCatalog controller must carry " +
            "class-level [Tags(\"Job Profiles\")] so Swagger consolidates them into one " +
            "group instead of an orphan group. Offending:\n  " +
            string.Join("\n  ", violations.OrderBy(static v => v, StringComparer.Ordinal)));
    }

    [Fact]
    public void EveryJobProfileFamilyAction_DeclaresSwaggerOperationSummaryAndDescription()
    {
        var violations = new List<string>();
        foreach (var controller in FamilyControllers())
        {
            foreach (var action in Actions(controller))
            {
                var name = $"{controller.Name}.{action.Name}";
                var operation = action.GetCustomAttribute<SwaggerOperationAttribute>();
                if (operation is null)
                {
                    violations.Add($"{name}: missing [SwaggerOperation].");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(operation.Summary))
                {
                    violations.Add($"{name}: [SwaggerOperation] Summary is empty.");
                }

                if (string.IsNullOrWhiteSpace(operation.Description))
                {
                    violations.Add($"{name}: [SwaggerOperation] Description is empty.");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Finding §J3 (§6.2): every JobProfile/JobCatalog action must declare " +
            "[SwaggerOperation(Summary, Description)] so the OpenAPI contract documents " +
            "it. Offending:\n  " +
            string.Join("\n  ", violations.OrderBy(static v => v, StringComparer.Ordinal)));
    }
}

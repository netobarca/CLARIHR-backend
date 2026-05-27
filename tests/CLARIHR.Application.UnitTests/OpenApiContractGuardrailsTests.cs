using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using CLARIHR.Api.Common.Conventions;
using Swashbuckle.AspNetCore.Annotations;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// §J3 / §X-OPENAPI guardrail (OpenAPI contract, 🟠/🟡 media). Mirrors the §6.1/§6.2
/// remediation that gave the 10 JobProfile controllers class-level [Tags("Job Profiles")]
/// + a [SwaggerOperation] on every action but missed JobCatalogsController (Swagger then
/// rendered an orphan "JobCatalogs" group with 5 undocumented endpoints), and was widened
/// for §X-OPENAPI to also enforce the PositionSlots controller (the structural outlier of
/// the Position feature, which mirrored §X-AUTHZ/§X-RATE but had not yet received the
/// OpenAPI parity). Structural pattern (namespace + name regex per family → expected tag),
/// not a hand-maintained list, so a new family controller cannot silently regress the
/// contract. To enroll another family, add a row to <see cref="Families"/>.
///
/// <para>[SwaggerOperation] is the NuGet Swashbuckle type (transitive via CLARIHR.Api),
/// compile-referenced like §J2's RangeAttribute. [Tags] is the ASP.NET endpoint-metadata
/// <c>Microsoft.AspNetCore.Http.TagsAttribute</c> — resolved on the controllers via the
/// Web SDK's implicit usings, NOT compile-available in this non-Web unit project — so it
/// is matched by simple type name and its <c>Tags</c> list read via reflection (the
/// §J1 framework-type-avoidance technique; namespace-agnostic and compiles
/// unconditionally).</para>
/// </summary>
public sealed class OpenApiContractGuardrailsTests
{
    private const string TagsAttributeSimpleName = "TagsAttribute";

    private static readonly Assembly ApiAssembly = typeof(AuthorizationPolicySetAttribute).Assembly;

    /// <summary>
    /// Controller families enforced by this guardrail: a name-prefix regex paired with the
    /// single OpenAPI tag every controller in that family must declare (so Swagger
    /// consolidates them into one group instead of orphan per-controller groups).
    /// </summary>
    private static readonly (string Label, Regex Family, string ExpectedTag)[] Families =
    [
        ("JobProfile/JobCatalog", new Regex(@"^(JobProfile|JobCatalog)", RegexOptions.Compiled), "Job Profiles"),
        ("PositionSlot", new Regex(@"^PositionSlot", RegexOptions.Compiled), "Position Slots"),
        // PersonnelFileBackground, PersonnelFileInterests, PersonnelFilePersonalInfo and
        // PersonnelFileTalent are the PersonnelFiles sub-resource controllers brought fully canonical
        // (GET/POST/PUT/PATCH/DELETE per sub-entity with per-item concurrency tokens). Enrolled here so
        // an action that drops [SwaggerOperation] or a class that drops [Tags] fails loudly. The narrow
        // ^PersonnelFile(Background|Interests|PersonalInfo|Talent) regex matches only those controllers;
        // the shell + remaining PersonnelFile controllers stay out until they are canonicalised (then
        // broaden this to ^PersonnelFile).
        ("PersonnelFileBackground/Interests/PersonalInfo/Talent", new Regex(@"^PersonnelFile(Background|Interests|PersonalInfo|Talent)", RegexOptions.Compiled), "Personnel Files"),
    ];

    public static TheoryData<string> FamilyLabels()
    {
        var data = new TheoryData<string>();
        foreach (var family in Families)
        {
            data.Add(family.Label);
        }

        return data;
    }

    private static (string Label, Regex Family, string ExpectedTag) Family(string label) =>
        Families.Single(family => family.Label == label);

    private static IReadOnlyList<Type> FamilyControllers(Regex family) =>
        ApiAssembly.GetTypes()
            .Where(type =>
                type.IsClass &&
                !type.IsAbstract &&
                type.Namespace == "CLARIHR.Api.Controllers" &&
                type.Name.EndsWith("Controller", StringComparison.Ordinal) &&
                family.IsMatch(type.Name))
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

    [Theory]
    [MemberData(nameof(FamilyLabels))]
    public void Family_ExposesControllersWithActions(string label)
    {
        var family = Family(label);
        var actionCount = FamilyControllers(family.Family).Sum(controller => Actions(controller).Count);
        Assert.True(
            actionCount > 0,
            $"Expected actions across the {family.Label} family. Zero means the namespace/name " +
            "filter drifted (or the family was renamed) and the §J3/§X-OPENAPI guardrail would " +
            "silently pass — the zero-match sentinel.");
    }

    [Theory]
    [MemberData(nameof(FamilyLabels))]
    public void EveryFamilyController_DeclaresExpectedTag(string label)
    {
        var family = Family(label);
        var violations = new List<string>();
        foreach (var controller in FamilyControllers(family.Family))
        {
            var tags = ReadTags(controller);
            if (tags is null)
            {
                violations.Add($"{controller.Name}: missing class-level [Tags].");
            }
            else if (!tags.Contains(family.ExpectedTag, StringComparer.Ordinal))
            {
                violations.Add(
                    $"{controller.Name}: [Tags] is [{string.Join(", ", tags)}], expected " +
                    $"to contain \"{family.ExpectedTag}\".");
            }
        }

        Assert.True(
            violations.Count == 0,
            $"Finding §J3/§X-OPENAPI (§6.1): every {family.Label} controller must carry " +
            $"class-level [Tags(\"{family.ExpectedTag}\")] so Swagger consolidates them into one " +
            "group instead of an orphan group. Offending:\n  " +
            string.Join("\n  ", violations.OrderBy(static v => v, StringComparer.Ordinal)));
    }

    [Theory]
    [MemberData(nameof(FamilyLabels))]
    public void EveryFamilyAction_DeclaresSwaggerOperationSummaryAndDescription(string label)
    {
        var family = Family(label);
        var violations = new List<string>();
        foreach (var controller in FamilyControllers(family.Family))
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
            $"Finding §J3/§X-OPENAPI (§6.2): every {family.Label} action must declare " +
            "[SwaggerOperation(Summary, Description)] so the OpenAPI contract documents " +
            "it. Offending:\n  " +
            string.Join("\n  ", violations.OrderBy(static v => v, StringComparer.Ordinal)));
    }
}

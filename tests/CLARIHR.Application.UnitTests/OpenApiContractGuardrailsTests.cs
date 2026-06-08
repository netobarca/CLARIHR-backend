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
        // JobProfileCompetencyMatrixController is carved out via (?!CompetencyMatrix): it is a
        // CompetencyFramework controller (tagged "Competency Framework", handler-gated) that happens to
        // sit under /job-profiles/{}; it is enrolled in the CompetencyFramework family below instead.
        ("JobProfile/JobCatalog", new Regex(@"^(JobProfile(?!CompetencyMatrix)|JobCatalog)", RegexOptions.Compiled), "Job Profiles"),
        ("PositionSlot", new Regex(@"^PositionSlot", RegexOptions.Compiled), "Position Slots"),
        ("CostCenter", new Regex(@"^CostCenter", RegexOptions.Compiled), "Cost Centers"),
        ("WorkCenters", new Regex(@"^WorkCenters", RegexOptions.Compiled), "Work Centers"),
        ("WorkCenterTypes", new Regex(@"^WorkCenterTypes", RegexOptions.Compiled), "Work Center Types"),
        ("LocationGroups", new Regex(@"^LocationGroups", RegexOptions.Compiled), "Location Groups"),
        ("LocationLevels", new Regex(@"^LocationLevels", RegexOptions.Compiled), "Location Levels"),
        ("LocationHierarchy", new Regex(@"^LocationHierarchy", RegexOptions.Compiled), "Location Hierarchy"),
        ("LegalRepresentatives", new Regex(@"^LegalRepresentatives", RegexOptions.Compiled), "Legal Representatives"),
        ("OrganizationUnits", new Regex(@"^OrganizationUnits", RegexOptions.Compiled), "Organization Units"),
        ("OrganizationStructureCatalogs", new Regex(@"^OrganizationStructureCatalogs", RegexOptions.Compiled), "Organization Structure Catalogs"),
        // PersonnelFileBackground, PersonnelFileInterests, PersonnelFilePersonalInfo, PersonnelFileTalent,
        // PersonnelFileCompensation, PersonnelFileEmployment, PersonnelFileDocuments and
        // PersonnelFileReporting are the canonicalised PersonnelFiles sub-resource / reporting controllers.
        // Enrolled here so an action that drops [SwaggerOperation] or a class that drops [Tags] fails
        // loudly. The shell PersonnelFilesController is governed separately; add a controller to the
        // alternation only once it carries class-level [Tags] + per-action [SwaggerOperation].
        ("PersonnelFileBackground/Interests/PersonalInfo/Talent/Compensation/Employment/Documents/Reporting", new Regex(@"^PersonnelFile(Background|Interests|PersonalInfo|Talent|Compensation|Employment|Documents|Reporting)", RegexOptions.Compiled), "Personnel Files"),
        // OccupationalPyramidLevels, CompetencyConducts and JobProfileCompetencyMatrix are the three
        // CompetencyFramework controllers (split from the former single CompetencyFrameworkController),
        // all consolidated under the "Competency Framework" tag. JobProfileCompetencyMatrix is carved
        // out of the JobProfile family above by its (?!CompetencyMatrix) lookahead.
        ("CompetencyFramework", new Regex(@"^(OccupationalPyramidLevels|CompetencyConducts|JobProfileCompetencyMatrix)", RegexOptions.Compiled), "Competency Framework"),
        // CompanyUsersController carries [Tags("Company Users")] + per-action [SwaggerOperation]; its
        // handler-gated authz keeps it out of GovernedFamilyRegex by design, but it is still a public
        // contract family. `^CompanyUsers` matches only CompanyUsersController (not CompanyPreferences).
        ("CompanyUsers", new Regex(@"^CompanyUsers", RegexOptions.Compiled), "Company Users"),
        // FilesController is an infrastructure controller (handler-gated authz, kept out of
        // GovernedFamilyRegex by design) but still a public OpenAPI surface. `^Files` matches only
        // FilesController (PersonnelFile* controllers start with "PersonnelFile", not "Files").
        ("Files", new Regex(@"^Files", RegexOptions.Compiled), "Files"),
        // GeneralCatalogsController is the read-only general/reference catalog surface. Its read authz
        // is intentionally handler-gated via the personnel-files authorization service (GC2 by-design,
        // see the GeneralCatalogs audit), so it stays out of GovernedFamilyRegex but is still a public
        // OpenAPI surface. `^GeneralCatalogs` matches only GeneralCatalogsController.
        ("GeneralCatalogs", new Regex(@"^GeneralCatalogs", RegexOptions.Compiled), "General Catalogs"),
        // ReportExportJobsController is the async report-export queue — a technical/handler-gated
        // controller (authz delegated per-resource via ReportExportResourceAuthorizer, kept out of
        // GovernedFamilyRegex by design) but still a public OpenAPI surface. `^ReportExportJobs` matches
        // only ReportExportJobsController. Tagged "Reports" (REX-B).
        ("ReportExportJobs", new Regex(@"^ReportExportJobs", RegexOptions.Compiled), "Reports"),
        // SalaryTabulatorController administers salary PII via a maker-checker workflow. Its authz is
        // handler-gated (no single Read/Manage policy pair — Read/Request/Approve), so it stays out of
        // GovernedFamilyRegex by design (mirror AccountCompanies), but it is still a public OpenAPI surface.
        // Enrolled for ST-C so a GET that drops [SwaggerOperation] or a class that drops [Tags] fails CI.
        // `^SalaryTabulator` matches only SalaryTabulatorController.
        ("SalaryTabulator", new Regex(@"^SalaryTabulator", RegexOptions.Compiled), "Salary Tabulator"),
        // UserPreferencesController is the self-scoped "me" preferences resource (resolved from the JWT,
        // authn-only — intentionally NO [AuthorizationPolicySet], OUT of GovernedFamilyRegex by design),
        // but it is fully canonical ([Tags]/[SwaggerOperation]/[ProducesStandardErrors]) and a public
        // OpenAPI surface. Enrolled for UP-C so a regression of that canonicity fails CI. `^UserPreferences`
        // matches only UserPreferencesController (CompanyPreferences starts with "Company").
        ("UserPreferences", new Regex(@"^UserPreferences", RegexOptions.Compiled), "User Preferences"),
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

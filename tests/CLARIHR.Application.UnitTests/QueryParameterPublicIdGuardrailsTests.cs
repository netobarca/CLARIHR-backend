using System.Reflection;
using System.Text.RegularExpressions;
using CLARIHR.Api.Common.Conventions;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// §J4 guardrail. JobProfilesController.Search exposed `[FromQuery] Guid? salaryClass`
/// whose name did NOT end in "Id", so it escaped the single-source
/// PublicContractBindingMetadataProvider that auto-renames *Id Guid params to
/// *PublicId on the public wire (e.g. the sibling orgUnitId -> orgUnitPublicId). Every
/// query-bound Guid filter on a JobProfile/JobCatalog controller must be named *Id (so
/// the provider yields *PublicId) or already *PublicId — otherwise it silently breaks
/// foundation §10.3 the way salaryClass did. Structural pattern (namespace + name
/// regex), not a hand-maintained list. [FromQuery] is detected by simple attribute
/// type name (no framework-type compile dependency — see §J3 pitfall). Scope:
/// JobProfile/JobCatalog family only.
/// </summary>
public sealed class QueryParameterPublicIdGuardrailsTests
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
            .Where(method => !method.IsSpecialName && method.DeclaringType is not null &&
                method.DeclaringType.Namespace == "CLARIHR.Api.Controllers")
            .ToArray();

    private static bool IsQueryBound(ParameterInfo parameter) =>
        parameter.GetCustomAttributes(inherit: true)
            .Any(static attr => attr.GetType().Name == "FromQueryAttribute");

    private static bool IsGuid(Type type) =>
        (Nullable.GetUnderlyingType(type) ?? type) == typeof(Guid);

    private static IReadOnlyList<(string Owner, string Name)> QueryGuidParams() =>
        FamilyActions()
            .SelectMany(action => action.GetParameters()
                .Where(p => p.Name is not null && IsGuid(p.ParameterType) && IsQueryBound(p))
                .Select(p => ($"{action.DeclaringType!.Name}.{action.Name}", p.Name!)))
            .ToArray();

    [Fact]
    public void JobProfileFamily_ExposesQueryBoundGuidFilters()
    {
        Assert.True(
            QueryGuidParams().Count > 0,
            "Expected at least one [FromQuery] Guid filter across the JobProfile/JobCatalog " +
            "family. Zero means the filter drifted and the §J4 guardrail would silently pass.");
    }

    [Fact]
    public void EveryQueryBoundGuidFilter_IsNamedSoItNormalizesToPublicId()
    {
        var violations = QueryGuidParams()
            .Where(p => !p.Name.EndsWith("Id", StringComparison.Ordinal) &&
                        !p.Name.EndsWith("PublicId", StringComparison.Ordinal))
            .Select(p => $"{p.Owner}('{p.Name}')")
            .OrderBy(static v => v, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            violations.Length == 0,
            "Finding §J4: every [FromQuery] Guid filter on a JobProfile/JobCatalog " +
            "controller must be named *Id (so PublicContractBindingMetadataProvider " +
            "auto-exposes it as *PublicId per foundation §10.3) or already *PublicId. " +
            "These escape the normalizer and leak a non-*PublicId Guid contract:\n  " +
            string.Join("\n  ", violations));
    }
}

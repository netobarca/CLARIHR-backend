using System.Reflection;
using System.Text.RegularExpressions;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Locations.Common;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// §LG3 Locations rate-limit guardrail, feature-wide (drift-proof by construction; mirrors
/// <see cref="CompetencyFrameworkRateLimitingGovernanceTests"/>). Every costly Locations read MUST
/// declare an <c>[EnableRateLimiting]</c> policy: a paged search/list (any action returning
/// <see cref="PagedResponse{T}"/>) or the unpaginated full-hierarchy <c>/tree</c> graph. A costly read
/// shipping with no abuse guard fails CI. Identified by **return type** (not method name) so it covers
/// the `Search`/`List`/`Tree` naming variants across LocationGroups, WorkCenters, WorkCenterTypes,
/// LocationLevels and LocationHierarchy — and any future Locations controller. Tiny config reads
/// (`location-levels` list, hierarchy `Get`) are not <c>PagedResponse</c> and are intentionally excluded.
///
/// <para>The ASP.NET endpoint attributes (<c>EnableRateLimitingAttribute</c>, <c>HttpGetAttribute</c>)
/// and the MVC <c>ActionResult&lt;T&gt;</c> wrapper are detected by <b>simple type name</b> via
/// reflection — they are not cleanly compile-referenceable from this non-Web SDK test project (same
/// technique as <see cref="CompetencyFrameworkRateLimitingGovernanceTests"/>).</para>
/// </summary>
public sealed class LocationRateLimitingGovernanceTests
{
    private static readonly Assembly ApiAssembly = typeof(AuthorizationPolicySetAttribute).Assembly;

    private static readonly Regex LocationFamilyRegex =
        new(@"^(LocationGroups|LocationLevels|LocationHierarchy|WorkCenters|WorkCenterTypes)Controller$",
            RegexOptions.Compiled);

    private static readonly HashSet<string> CanonicalPolicies = new(StringComparer.Ordinal)
    {
        LocationRateLimitPolicies.Search,
        LocationRateLimitPolicies.Tree,
    };

    private static IReadOnlyList<MethodInfo> FamilyActions() =>
        ApiAssembly.GetTypes()
            .Where(static type =>
                type is { IsClass: true, IsAbstract: false } &&
                type.Namespace == "CLARIHR.Api.Controllers" &&
                LocationFamilyRegex.IsMatch(type.Name))
            .SelectMany(static type => type.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(static method => HttpRouteTemplate(method) is not null)
            .ToArray();

    private static string? HttpRouteTemplate(MethodInfo method) =>
        method.GetCustomAttributes()
            .Where(static attribute =>
                attribute.GetType().Name.StartsWith("Http", StringComparison.Ordinal) &&
                attribute.GetType().Name.EndsWith("Attribute", StringComparison.Ordinal))
            .Select(static attribute =>
                attribute.GetType().GetProperty("Template")?.GetValue(attribute) as string)
            .FirstOrDefault(static template => template is not null);

    private static string? RateLimitPolicy(MethodInfo method) =>
        method.GetCustomAttributes()
            .Where(static attribute => attribute.GetType().Name == "EnableRateLimitingAttribute")
            .Select(static attribute =>
                attribute.GetType().GetProperty("PolicyName")?.GetValue(attribute) as string)
            .FirstOrDefault(static policy => !string.IsNullOrWhiteSpace(policy));

    // Costly read = a paged search/list (return type unwraps to PagedResponse<T>) or the /tree graph.
    private static bool IsCostly(MethodInfo method) =>
        ReturnsPagedResponse(method) ||
        (HttpRouteTemplate(method)?.EndsWith("/tree", StringComparison.Ordinal) ?? false);

    private static bool ReturnsPagedResponse(MethodInfo method)
    {
        var type = method.ReturnType;

        // Unwrap Task<...> then ActionResult<...> by simple name (Web types are not referenceable here).
        if (type.IsGenericType && type.Name.StartsWith("Task", StringComparison.Ordinal))
        {
            type = type.GetGenericArguments()[0];
        }

        if (type.IsGenericType && type.Name.StartsWith("ActionResult", StringComparison.Ordinal))
        {
            type = type.GetGenericArguments()[0];
        }

        return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(PagedResponse<>);
    }

    /// <summary>
    /// Inv-R1 — every costly Locations read (paged search/list + the /tree graph) declares a canonical
    /// rate-limit policy. Catches a costly endpoint shipping with no abuse guard.
    /// </summary>
    [Fact]
    public void EveryCostlyLocationEndpoint_DeclaresRateLimitPolicy()
    {
        var costly = FamilyActions().Where(IsCostly).ToArray();

        // Sentinel: a refactor that makes the costly filter match zero actions must fail loudly.
        Assert.NotEmpty(costly);

        var unguarded = costly
            .Where(static method => RateLimitPolicy(method) is not { } policy ||
                !CanonicalPolicies.Contains(policy))
            .Select(static method => $"{method.DeclaringType!.Name}.{method.Name}")
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            unguarded.Length == 0,
            "§LG3: every costly Locations read (paged search/list + /tree) must declare " +
            "[EnableRateLimiting(LocationRateLimitPolicies.*)] or it has no per-tenant abuse guard. " +
            "Missing/invalid on:\n  " + string.Join("\n  ", unguarded));
    }

    /// <summary>
    /// Inv-R2 — every <c>[EnableRateLimiting]</c> references a canonical constant from
    /// <see cref="LocationRateLimitPolicies"/> (no typo / orphan policy).
    /// </summary>
    [Fact]
    public void EveryRateLimitMarker_ReferencesCanonicalPolicy()
    {
        var invalid = FamilyActions()
            .Select(static method => ($"{method.DeclaringType!.Name}.{method.Name}", Policy: RateLimitPolicy(method)))
            .Where(static pair => pair.Policy is not null && !CanonicalPolicies.Contains(pair.Policy))
            .Select(static pair => $"{pair.Item1} -> '{pair.Policy}'")
            .OrderBy(static entry => entry, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            invalid.Length == 0,
            "[EnableRateLimiting] must reference a constant from LocationRateLimitPolicies. " +
            "Offending:\n  " + string.Join("\n  ", invalid));
    }

    /// <summary>
    /// Inv-R3 — both registered limiters are actually wired to ≥1 endpoint, so deleting a whole limiter
    /// (tree or search) fails CI instead of silently leaving it dead.
    /// </summary>
    [Fact]
    public void BothRateLimitPolicies_AreAppliedToAtLeastOneEndpoint()
    {
        var applied = FamilyActions()
            .Select(RateLimitPolicy)
            .Where(static policy => policy is not null)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(LocationRateLimitPolicies.Tree, applied);
        Assert.Contains(LocationRateLimitPolicies.Search, applied);
    }
}

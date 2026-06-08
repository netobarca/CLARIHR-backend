using System.Reflection;
using System.Text.RegularExpressions;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.OrgUnits.Common;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// OU-001 Org Units rate-limit guardrail (drift-proof by construction; mirrors
/// <see cref="LocationRateLimitingGovernanceTests"/>). Every costly Org Units read MUST declare an
/// <c>[EnableRateLimiting]</c> policy: a paged search/list (any action returning
/// <see cref="PagedResponse{T}"/>), the unpaginated full-hierarchy <c>/tree</c> and <c>/graph</c>
/// projections, or a downloadable export (<c>/export</c>, <c>/diagram-export</c>). A costly read
/// shipping with no abuse guard fails CI. Costly endpoints are identified by **return type / route
/// suffix** (not method name) so the guardrail survives renames and covers any future Org Units read.
/// Tiny single-resource reads (<c>GetById</c>) and the id-routed mutations are not costly and are
/// intentionally excluded.
///
/// <para>The ASP.NET endpoint attributes (<c>EnableRateLimitingAttribute</c>, <c>HttpGetAttribute</c>)
/// and the MVC <c>ActionResult&lt;T&gt;</c> wrapper are detected by <b>simple type name</b> via
/// reflection — they are not cleanly compile-referenceable from this non-Web SDK test project (same
/// technique as <see cref="LocationRateLimitingGovernanceTests"/>).</para>
/// </summary>
public sealed class OrgUnitRateLimitingGovernanceTests
{
    private static readonly Assembly ApiAssembly = typeof(AuthorizationPolicySetAttribute).Assembly;

    private static readonly Regex OrgUnitFamilyRegex =
        new(@"^OrganizationUnitsController$", RegexOptions.Compiled);

    private static readonly string[] CostlyRouteSuffixes =
    {
        "/tree",
        "/graph",
        "/export",
        "/diagram-export",
    };

    private static readonly HashSet<string> CanonicalPolicies = new(StringComparer.Ordinal)
    {
        OrgUnitRateLimitPolicies.Search,
        OrgUnitRateLimitPolicies.Tree,
        OrgUnitRateLimitPolicies.Export,
    };

    private static IReadOnlyList<MethodInfo> FamilyActions() =>
        ApiAssembly.GetTypes()
            .Where(static type =>
                type is { IsClass: true, IsAbstract: false } &&
                type.Namespace == "CLARIHR.Api.Controllers" &&
                OrgUnitFamilyRegex.IsMatch(type.Name))
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

    // Costly read = a paged search/list (return type unwraps to PagedResponse<T>) or a full-hierarchy
    // graph / downloadable export (route suffix /tree, /graph, /export, /diagram-export).
    private static bool IsCostly(MethodInfo method) =>
        ReturnsPagedResponse(method) ||
        (HttpRouteTemplate(method) is { } template &&
         CostlyRouteSuffixes.Any(suffix => template.EndsWith(suffix, StringComparison.Ordinal)));

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
    /// Inv-R1 — every costly Org Units read (paged search/list + /tree + /graph + exports) declares a
    /// canonical rate-limit policy. Catches a costly endpoint shipping with no abuse guard.
    /// </summary>
    [Fact]
    public void EveryCostlyOrgUnitEndpoint_DeclaresRateLimitPolicy()
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
            "OU-001: every costly Org Units read (paged search/list + /tree + /graph + exports) must " +
            "declare [EnableRateLimiting(OrgUnitRateLimitPolicies.*)] or it has no per-tenant abuse " +
            "guard. Missing/invalid on:\n  " + string.Join("\n  ", unguarded));
    }

    /// <summary>
    /// Inv-R2 — every <c>[EnableRateLimiting]</c> references a canonical constant from
    /// <see cref="OrgUnitRateLimitPolicies"/> (no typo / orphan policy).
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
            "[EnableRateLimiting] must reference a constant from OrgUnitRateLimitPolicies. " +
            "Offending:\n  " + string.Join("\n  ", invalid));
    }

    /// <summary>
    /// Inv-R3 — all three registered limiters are actually wired to ≥1 endpoint, so deleting a whole
    /// limiter (search, tree or export) fails CI instead of silently leaving it dead.
    /// </summary>
    [Fact]
    public void AllRateLimitPolicies_AreAppliedToAtLeastOneEndpoint()
    {
        var applied = FamilyActions()
            .Select(RateLimitPolicy)
            .Where(static policy => policy is not null)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(OrgUnitRateLimitPolicies.Search, applied);
        Assert.Contains(OrgUnitRateLimitPolicies.Tree, applied);
        Assert.Contains(OrgUnitRateLimitPolicies.Export, applied);
    }
}

using System.Reflection;
using System.Text.RegularExpressions;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Features.PersonnelFiles.Common;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Personnel Files rate-limit guardrail (drift-proof by construction; mirrors the §X-RATE
/// <see cref="RateLimitingGovernanceTests"/> Position Slots template). The unbounded-cost
/// Personnel Files reads — row exports and full-tenant analytics aggregation — MUST declare an
/// <c>[EnableRateLimiting]</c> policy, so a newly added or renamed heavy endpoint cannot ship
/// without a per-tenant abuse guard (the gap this closes: the reporting/export/analytics
/// endpoints originally shipped unthrottled while the cheaper shell search was limited). Pure
/// reflection over the whole <c>PersonnelFile*</c> controller family — no hand-maintained
/// endpoint list — family-scoped, aggregated single assert, loud zero-match sentinel.
///
/// <para>The ASP.NET endpoint attributes (<c>EnableRateLimitingAttribute</c>,
/// <c>HttpGetAttribute</c>) are detected by <b>simple type name</b> and read via reflection —
/// they are not cleanly compile-referenceable from this non-Web SDK test project (same
/// framework-type-avoidance technique used by <see cref="RateLimitingGovernanceTests"/>).</para>
/// </summary>
public sealed class PersonnelFileRateLimitingGovernanceTests
{
    private static readonly Assembly ApiAssembly = typeof(AuthorizationPolicySetAttribute).Assembly;

    private static readonly Regex PersonnelFileFamilyRegex =
        new("^PersonnelFile", RegexOptions.Compiled);

    /// <summary>Unbounded-cost reads that must be rate-limited (name or route match).</summary>
    private static readonly Regex HeavyEndpointRegex =
        new(@"(export|analytics|graph)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly HashSet<string> CanonicalPolicies = new(StringComparer.Ordinal)
    {
        PersonnelFileRateLimitPolicies.Create,
        PersonnelFileRateLimitPolicies.Search,
        PersonnelFileRateLimitPolicies.Lifecycle,
        PersonnelFileRateLimitPolicies.Export,
    };

    private static IReadOnlyList<MethodInfo> PersonnelFileActions() =>
        ApiAssembly.GetTypes()
            .Where(static type =>
                type is { IsClass: true, IsAbstract: false } &&
                type.Namespace == "CLARIHR.Api.Controllers" &&
                type.Name.EndsWith("Controller", StringComparison.Ordinal) &&
                PersonnelFileFamilyRegex.IsMatch(type.Name))
            .SelectMany(static type => type.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(static method => HttpRouteTemplate(method) is not null)
            .ToArray();

    // Route template via simple-name reflection over Http{Get,Post,...}Attribute.Template.
    private static string? HttpRouteTemplate(MethodInfo method) =>
        method.GetCustomAttributes()
            .Where(static attribute =>
                attribute.GetType().Name.StartsWith("Http", StringComparison.Ordinal) &&
                attribute.GetType().Name.EndsWith("Attribute", StringComparison.Ordinal))
            .Select(static attribute =>
                attribute.GetType().GetProperty("Template")?.GetValue(attribute) as string)
            .FirstOrDefault(static template => template is not null);

    // Policy name via simple-name reflection over EnableRateLimitingAttribute.PolicyName.
    private static string? RateLimitPolicy(MethodInfo method) =>
        method.GetCustomAttributes()
            .Where(static attribute => attribute.GetType().Name == "EnableRateLimitingAttribute")
            .Select(static attribute =>
                attribute.GetType().GetProperty("PolicyName")?.GetValue(attribute) as string)
            .FirstOrDefault(static policy => !string.IsNullOrWhiteSpace(policy));

    private static bool IsHeavy(MethodInfo method) =>
        HeavyEndpointRegex.IsMatch(method.Name) ||
        HeavyEndpointRegex.IsMatch(HttpRouteTemplate(method) ?? string.Empty);

    /// <summary>
    /// Inv-R1 — every unbounded-cost read (export / analytics / graph) across the Personnel
    /// Files controller family declares a canonical rate-limit policy. Catches a heavy endpoint
    /// shipping with no abuse guard.
    /// </summary>
    [Fact]
    public void EveryHeavyPersonnelFileEndpoint_DeclaresRateLimitPolicy()
    {
        var heavy = PersonnelFileActions().Where(IsHeavy).ToArray();

        // Sentinel: a rename that makes the heavy regex match zero actions must fail loudly,
        // not pass vacuously.
        Assert.NotEmpty(heavy);

        var unguarded = heavy
            .Where(static method => RateLimitPolicy(method) is not { } policy ||
                !CanonicalPolicies.Contains(policy))
            .Select(static method => $"{method.DeclaringType!.Name}.{method.Name}")
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            unguarded.Length == 0,
            "Unbounded-cost Personnel Files reads (export / analytics) must declare " +
            "[EnableRateLimiting(PersonnelFileRateLimitPolicies.*)] or they have no per-tenant " +
            "abuse guard. Missing/invalid on:\n  " + string.Join("\n  ", unguarded));
    }

    /// <summary>
    /// Inv-R2 — every <c>[EnableRateLimiting]</c> across the family references a canonical
    /// constant from <see cref="PersonnelFileRateLimitPolicies"/> (no typo / orphan policy).
    /// </summary>
    [Fact]
    public void EveryRateLimitMarker_ReferencesCanonicalPolicy()
    {
        var invalid = PersonnelFileActions()
            .Select(static method => ($"{method.DeclaringType!.Name}.{method.Name}", Policy: RateLimitPolicy(method)))
            .Where(static pair => pair.Policy is not null && !CanonicalPolicies.Contains(pair.Policy))
            .Select(static pair => $"{pair.Item1} -> '{pair.Policy}'")
            .OrderBy(static entry => entry, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            invalid.Length == 0,
            "[EnableRateLimiting] must reference a constant from PersonnelFileRateLimitPolicies. " +
            "Offending:\n  " + string.Join("\n  ", invalid));
    }

    /// <summary>
    /// Inv-R3 — the search and export limiters are each wired to ≥1 endpoint, so deleting a
    /// whole limiter fails CI instead of silently leaving it dead.
    /// </summary>
    [Fact]
    public void SearchAndExportRateLimitPolicies_AreAppliedToAtLeastOneEndpoint()
    {
        var applied = PersonnelFileActions()
            .Select(RateLimitPolicy)
            .Where(static policy => policy is not null)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(PersonnelFileRateLimitPolicies.Search, applied);
        Assert.Contains(PersonnelFileRateLimitPolicies.Export, applied);
    }
}

using System.Reflection;
using System.Text.RegularExpressions;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Features.PositionSlots.Common;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// §X-RATE guardrail (drift-proof by construction): the unbounded-cost Position Slots
/// generators (export / diagram-export / full-tenant graph) MUST declare a
/// <c>[EnableRateLimiting]</c> policy, so a newly added or renamed heavy endpoint cannot
/// ship without an abuse guard the way it could before §X-RATE. Pure reflection — no
/// hand-maintained endpoint list. Mirrors <see cref="AuthorizationPolicyConventionGovernanceTests"/>
/// (the §X-AUTHZ template): family-scoped, aggregated single assert, loud zero-match sentinel.
///
/// <para>The ASP.NET endpoint attributes (<c>EnableRateLimitingAttribute</c>,
/// <c>HttpGetAttribute</c>) are detected by <b>simple type name</b> and read via
/// reflection — they are not cleanly compile-referenceable from this non-Web SDK test
/// project (same framework-type-avoidance technique used for <c>[Tags]</c>).</para>
/// </summary>
public sealed class RateLimitingGovernanceTests
{
    private static readonly Assembly ApiAssembly = typeof(AuthorizationPolicySetAttribute).Assembly;

    /// <summary>Unbounded-cost generators that must be rate-limited (name or route match).</summary>
    private static readonly Regex HeavyEndpointRegex =
        new(@"(export|diagram-export|graph)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly HashSet<string> CanonicalPolicies = new(StringComparer.Ordinal)
    {
        PositionSlotRateLimitPolicies.Export,
        PositionSlotRateLimitPolicies.Search,
    };

    private static IReadOnlyList<MethodInfo> PositionSlotActions() =>
        ApiAssembly.GetTypes()
            .Where(static type =>
                type is { IsClass: true, IsAbstract: false } &&
                type.Namespace == "CLARIHR.Api.Controllers" &&
                type.Name == "PositionSlotsController")
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
    /// Inv-R1 — every unbounded-cost generator (export / diagram-export / graph) declares a
    /// canonical rate-limit policy. Catches a heavy endpoint shipping with no abuse guard.
    /// </summary>
    [Fact]
    public void EveryHeavyPositionSlotEndpoint_DeclaresRateLimitPolicy()
    {
        var heavy = PositionSlotActions().Where(IsHeavy).ToArray();

        // Sentinel: a rename that makes the heavy regex match zero actions must fail
        // loudly, not pass vacuously (mirrors the §X-AUTHZ Inv-2 sentinel).
        Assert.NotEmpty(heavy);

        var unguarded = heavy
            .Where(static method => RateLimitPolicy(method) is not { } policy ||
                !CanonicalPolicies.Contains(policy))
            .Select(static method => method.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            unguarded.Length == 0,
            "Finding §X-RATE: unbounded-cost Position Slots generators must declare " +
            "[EnableRateLimiting(PositionSlotRateLimitPolicies.*)] or they have no per-tenant " +
            "abuse guard. Missing/invalid on:\n  " + string.Join("\n  ", unguarded));
    }

    /// <summary>
    /// Inv-R2 — every <c>[EnableRateLimiting]</c> on the controller references a canonical
    /// constant from <see cref="PositionSlotRateLimitPolicies"/> (no typo / orphan policy).
    /// </summary>
    [Fact]
    public void EveryRateLimitMarker_ReferencesCanonicalPolicy()
    {
        var invalid = PositionSlotActions()
            .Select(static method => (method.Name, Policy: RateLimitPolicy(method)))
            .Where(static pair => pair.Policy is not null && !CanonicalPolicies.Contains(pair.Policy))
            .Select(static pair => $"{pair.Name} -> '{pair.Policy}'")
            .OrderBy(static entry => entry, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            invalid.Length == 0,
            "[EnableRateLimiting] must reference a constant from PositionSlotRateLimitPolicies. " +
            "Offending:\n  " + string.Join("\n  ", invalid));
    }

    /// <summary>
    /// Inv-R3 — both registered limiters are actually wired to ≥1 endpoint, so deleting a
    /// whole limiter (export or search) fails CI instead of silently leaving it dead.
    /// </summary>
    [Fact]
    public void BothRateLimitPolicies_AreAppliedToAtLeastOneEndpoint()
    {
        var applied = PositionSlotActions()
            .Select(RateLimitPolicy)
            .Where(static policy => policy is not null)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(PositionSlotRateLimitPolicies.Export, applied);
        Assert.Contains(PositionSlotRateLimitPolicies.Search, applied);
    }
}

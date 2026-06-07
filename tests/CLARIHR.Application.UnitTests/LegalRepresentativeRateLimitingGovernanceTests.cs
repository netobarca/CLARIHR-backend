using System.Reflection;
using System.Text.RegularExpressions;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Features.LegalRepresentatives.Common;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Legal Representatives rate-limit guardrail (drift-proof by construction; mirrors the §X-RATE
/// <see cref="RateLimitingGovernanceTests"/> Position Slots template and
/// <see cref="CompetencyFrameworkRateLimitingGovernanceTests"/>). The unbounded-cost legal-
/// representatives report export MUST declare an <c>[EnableRateLimiting]</c> policy, so a newly
/// added or renamed heavy endpoint cannot ship without a per-tenant abuse guard. Pure reflection
/// over the autonomous LegalRepresentatives controller — family-scoped, aggregated single assert,
/// loud zero-match sentinel.
///
/// <para>The ASP.NET endpoint attributes (<c>EnableRateLimitingAttribute</c>,
/// <c>HttpGetAttribute</c>) are detected by <b>simple type name</b> and read via reflection —
/// they are not cleanly compile-referenceable from this non-Web SDK test project (same
/// framework-type-avoidance technique used by <see cref="RateLimitingGovernanceTests"/>).</para>
/// </summary>
public sealed class LegalRepresentativeRateLimitingGovernanceTests
{
    private static readonly Assembly ApiAssembly = typeof(AuthorizationPolicySetAttribute).Assembly;

    private static readonly Regex LegalRepresentativeFamilyRegex =
        new(@"^LegalRepresentatives", RegexOptions.Compiled);

    /// <summary>Unbounded-cost reads that must be rate-limited (name or route match).</summary>
    private static readonly Regex HeavyEndpointRegex =
        new(@"(export)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly HashSet<string> CanonicalPolicies = new(StringComparer.Ordinal)
    {
        LegalRepresentativeRateLimitPolicies.Search,
        LegalRepresentativeRateLimitPolicies.Export,
    };

    private static IReadOnlyList<MethodInfo> FamilyActions() =>
        ApiAssembly.GetTypes()
            .Where(static type =>
                type is { IsClass: true, IsAbstract: false } &&
                type.Namespace == "CLARIHR.Api.Controllers" &&
                type.Name.EndsWith("Controller", StringComparison.Ordinal) &&
                LegalRepresentativeFamilyRegex.IsMatch(type.Name))
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
    /// Inv-R1 — every unbounded-cost generator (the report export) declares a canonical rate-limit
    /// policy. Catches a heavy endpoint shipping with no abuse guard.
    /// </summary>
    [Fact]
    public void EveryHeavyLegalRepresentativeEndpoint_DeclaresRateLimitPolicy()
    {
        var heavy = FamilyActions().Where(IsHeavy).ToArray();

        // Sentinel: a rename that makes the heavy regex match zero actions must fail loudly.
        Assert.NotEmpty(heavy);

        var unguarded = heavy
            .Where(static method => RateLimitPolicy(method) is not { } policy ||
                !CanonicalPolicies.Contains(policy))
            .Select(static method => method.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            unguarded.Length == 0,
            "Finding §X-RATE: unbounded-cost Legal Representatives generators must declare " +
            "[EnableRateLimiting(LegalRepresentativeRateLimitPolicies.*)] or they have no per-tenant " +
            "abuse guard. Missing/invalid on:\n  " + string.Join("\n  ", unguarded));
    }

    /// <summary>
    /// Inv-R2 — every <c>[EnableRateLimiting]</c> references a canonical constant from
    /// <see cref="LegalRepresentativeRateLimitPolicies"/> (no typo / orphan policy).
    /// </summary>
    [Fact]
    public void EveryRateLimitMarker_ReferencesCanonicalPolicy()
    {
        var invalid = FamilyActions()
            .Select(static method => (method.Name, Policy: RateLimitPolicy(method)))
            .Where(static pair => pair.Policy is not null && !CanonicalPolicies.Contains(pair.Policy))
            .Select(static pair => $"{pair.Name} -> '{pair.Policy}'")
            .OrderBy(static entry => entry, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            invalid.Length == 0,
            "[EnableRateLimiting] must reference a constant from LegalRepresentativeRateLimitPolicies. " +
            "Offending:\n  " + string.Join("\n  ", invalid));
    }

    /// <summary>
    /// Inv-R3 — both registered limiters are actually wired to ≥1 endpoint, so deleting a whole
    /// limiter (export or search) fails CI instead of silently leaving it dead.
    /// </summary>
    [Fact]
    public void BothRateLimitPolicies_AreAppliedToAtLeastOneEndpoint()
    {
        var applied = FamilyActions()
            .Select(RateLimitPolicy)
            .Where(static policy => policy is not null)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(LegalRepresentativeRateLimitPolicies.Export, applied);
        Assert.Contains(LegalRepresentativeRateLimitPolicies.Search, applied);
    }
}

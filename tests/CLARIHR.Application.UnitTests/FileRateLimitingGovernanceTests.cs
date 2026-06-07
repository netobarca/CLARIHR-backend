using System.Reflection;
using System.Text.RegularExpressions;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Features.Files.Common;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Files rate-limit guardrail (drift-proof by construction; mirrors the §X-RATE
/// <see cref="LegalRepresentativeRateLimitingGovernanceTests"/> /
/// <see cref="CostCenterRateLimitingGovernanceTests"/> template). EVERY Files endpoint touches
/// storage — it reserves a row + mints a write SAS (upload-session), mints a read SAS (read-url), or
/// mutates the stored object (complete/delete) — so each MUST declare an <c>[EnableRateLimiting]</c>
/// policy, or a new/renamed endpoint ships with no per-tenant abuse guard. Pure reflection over the
/// FilesController — family-scoped, aggregated assert, loud zero-match sentinel.
///
/// <para>The ASP.NET endpoint attributes (<c>EnableRateLimitingAttribute</c>, <c>HttpGetAttribute</c>)
/// are detected by <b>simple type name</b> and read via reflection — they are not cleanly
/// compile-referenceable from this non-Web SDK test project (same framework-type-avoidance technique
/// used by <see cref="LegalRepresentativeRateLimitingGovernanceTests"/>).</para>
/// </summary>
public sealed class FileRateLimitingGovernanceTests
{
    private static readonly Assembly ApiAssembly = typeof(AuthorizationPolicySetAttribute).Assembly;

    private static readonly Regex FileFamilyRegex = new(@"^Files", RegexOptions.Compiled);

    private static readonly HashSet<string> CanonicalPolicies = new(StringComparer.Ordinal)
    {
        FileRateLimitPolicies.Upload,
        FileRateLimitPolicies.Read,
        FileRateLimitPolicies.Lifecycle,
    };

    private static IReadOnlyList<MethodInfo> FamilyActions() =>
        ApiAssembly.GetTypes()
            .Where(static type =>
                type is { IsClass: true, IsAbstract: false } &&
                type.Namespace == "CLARIHR.Api.Controllers" &&
                type.Name.EndsWith("Controller", StringComparison.Ordinal) &&
                FileFamilyRegex.IsMatch(type.Name))
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

    /// <summary>
    /// Inv-R1 — every Files endpoint (all mint a SAS or mutate storage) declares a canonical
    /// rate-limit policy. Catches a new/renamed endpoint shipping with no abuse guard.
    /// </summary>
    [Fact]
    public void EveryFileEndpoint_DeclaresRateLimitPolicy()
    {
        var actions = FamilyActions();

        // Sentinel: a rename that makes the family filter match zero actions must fail loudly.
        Assert.NotEmpty(actions);

        var unguarded = actions
            .Where(static method => RateLimitPolicy(method) is not { } policy || !CanonicalPolicies.Contains(policy))
            .Select(static method => method.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            unguarded.Length == 0,
            "Finding §X-RATE: every Files endpoint mints a SAS or mutates storage and MUST declare " +
            "[EnableRateLimiting(FileRateLimitPolicies.*)] or it has no per-tenant abuse guard. " +
            "Missing/invalid on:\n  " + string.Join("\n  ", unguarded));
    }

    /// <summary>Inv-R2 — every <c>[EnableRateLimiting]</c> references a canonical constant (no typo / orphan).</summary>
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
            "[EnableRateLimiting] must reference a constant from FileRateLimitPolicies. Offending:\n  " +
            string.Join("\n  ", invalid));
    }

    /// <summary>Inv-R3 — each registered limiter is wired to ≥1 endpoint, so deleting one fails CI.</summary>
    [Fact]
    public void EveryRegisteredFilePolicy_IsAppliedToAtLeastOneEndpoint()
    {
        var applied = FamilyActions()
            .Select(RateLimitPolicy)
            .Where(static policy => policy is not null)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(FileRateLimitPolicies.Upload, applied);
        Assert.Contains(FileRateLimitPolicies.Read, applied);
        Assert.Contains(FileRateLimitPolicies.Lifecycle, applied);
    }
}

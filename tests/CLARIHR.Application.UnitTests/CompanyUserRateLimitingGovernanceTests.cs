using System.Reflection;
using System.Text.RegularExpressions;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Features.CompanyUsers.Common;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Company Users rate-limit guardrail (drift-proof by construction; mirrors
/// <see cref="CompetencyFrameworkRateLimitingGovernanceTests"/>). The invitation e-mail senders —
/// every <c>[HttpPost]</c> in the family (invite + reset-invitation) — MUST declare an
/// <c>[EnableRateLimiting]</c> policy, so a newly added or renamed e-mail-sending endpoint cannot
/// ship without a per-tenant abuse guard (e-mail bomb / enumeration). Pure reflection over the
/// CompanyUsers controller — family-scoped, aggregated single assert, loud zero-match sentinel.
///
/// <para>The ASP.NET endpoint attributes (<c>EnableRateLimitingAttribute</c>,
/// <c>HttpPostAttribute</c>) are detected by <b>simple type name</b> and read via reflection — they
/// are not cleanly compile-referenceable from this non-Web SDK test project (same technique as
/// <see cref="CompetencyFrameworkRateLimitingGovernanceTests"/>).</para>
/// </summary>
public sealed class CompanyUserRateLimitingGovernanceTests
{
    private static readonly Assembly ApiAssembly = typeof(AuthorizationPolicySetAttribute).Assembly;

    private static readonly Regex CompanyUserFamilyRegex =
        new(@"^CompanyUsers", RegexOptions.Compiled);

    private static readonly HashSet<string> CanonicalPolicies = new(StringComparer.Ordinal)
    {
        CompanyUserRateLimitPolicies.Search,
        CompanyUserRateLimitPolicies.Invite,
    };

    private static IReadOnlyList<MethodInfo> FamilyActions() =>
        ApiAssembly.GetTypes()
            .Where(static type =>
                type is { IsClass: true, IsAbstract: false } &&
                type.Namespace == "CLARIHR.Api.Controllers" &&
                type.Name.EndsWith("Controller", StringComparison.Ordinal) &&
                CompanyUserFamilyRegex.IsMatch(type.Name))
            .SelectMany(static type => type.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(IsHttpAction)
            .ToArray();

    // Any ASP.NET HTTP verb action (HttpGet/HttpPost/HttpPut/HttpPatch/...), by simple type name —
    // includes the parameterless [HttpGet] list (no route template) so the search limiter is visible.
    private static bool IsHttpAction(MethodInfo method) =>
        method.GetCustomAttributes().Any(static attribute =>
            attribute.GetType().Name.StartsWith("Http", StringComparison.Ordinal) &&
            attribute.GetType().Name.EndsWith("Attribute", StringComparison.Ordinal));

    // The invitation e-mail senders are exactly the family's POST actions (invite + reset-invitation).
    private static bool IsHttpPost(MethodInfo method) =>
        method.GetCustomAttributes().Any(static attribute =>
            attribute.GetType().Name == "HttpPostAttribute");

    // Policy name via simple-name reflection over EnableRateLimitingAttribute.PolicyName.
    private static string? RateLimitPolicy(MethodInfo method) =>
        method.GetCustomAttributes()
            .Where(static attribute => attribute.GetType().Name == "EnableRateLimitingAttribute")
            .Select(static attribute =>
                attribute.GetType().GetProperty("PolicyName")?.GetValue(attribute) as string)
            .FirstOrDefault(static policy => !string.IsNullOrWhiteSpace(policy));

    /// <summary>
    /// Inv-R1 — every invitation e-mail sender (every [HttpPost] in the family) declares a canonical
    /// rate-limit policy. Catches an e-mail-sending endpoint shipping with no abuse guard.
    /// </summary>
    [Fact]
    public void EveryCompanyUserInvitationPost_DeclaresRateLimitPolicy()
    {
        var posts = FamilyActions().Where(IsHttpPost).ToArray();

        // Sentinel: a refactor that makes the POST filter match zero actions must fail loudly.
        Assert.NotEmpty(posts);

        var unguarded = posts
            .Where(static method => RateLimitPolicy(method) is not { } policy ||
                !CanonicalPolicies.Contains(policy))
            .Select(static method => method.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            unguarded.Length == 0,
            "Finding F2: the invitation e-mail senders (POST invite / reset-invitation) must declare " +
            "[EnableRateLimiting(CompanyUserRateLimitPolicies.*)] or they have no per-tenant abuse " +
            "guard. Missing/invalid on:\n  " + string.Join("\n  ", unguarded));
    }

    /// <summary>
    /// Inv-R2 — every <c>[EnableRateLimiting]</c> references a canonical constant from
    /// <see cref="CompanyUserRateLimitPolicies"/> (no typo / orphan policy).
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
            "[EnableRateLimiting] must reference a constant from CompanyUserRateLimitPolicies. " +
            "Offending:\n  " + string.Join("\n  ", invalid));
    }

    /// <summary>
    /// Inv-R3 — both registered limiters are actually wired to ≥1 endpoint, so deleting a whole
    /// limiter (invite or search) fails CI instead of silently leaving it dead.
    /// </summary>
    [Fact]
    public void BothRateLimitPolicies_AreAppliedToAtLeastOneEndpoint()
    {
        var applied = FamilyActions()
            .Select(RateLimitPolicy)
            .Where(static policy => policy is not null)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(CompanyUserRateLimitPolicies.Invite, applied);
        Assert.Contains(CompanyUserRateLimitPolicies.Search, applied);
    }
}

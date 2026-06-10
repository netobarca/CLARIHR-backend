using System.Reflection;
using System.Text.RegularExpressions;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Features.AccountCompanies.Common;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// AC-8 rate-limit guardrail for the AccountCompanies session-switch endpoint; mirrors
/// <see cref="CostCenterRateLimitingGovernanceTests"/>. <c>POST .../switch</c> mints a fresh
/// access+refresh token pair (the functional equivalent of login, which is limited at 5/min), so it MUST
/// declare an <c>[EnableRateLimiting]</c> policy — a renamed or newly added token-minting endpoint cannot
/// ship without a per-tenant abuse guard. Pure reflection over the autonomous AccountCompanies controller;
/// the ASP.NET attributes are read by simple type name (not compile-referenceable from this non-Web SDK
/// test project), same technique as the CostCenters template.
/// </summary>
public sealed class AccountCompanyRateLimitingGovernanceTests
{
    private static readonly Assembly ApiAssembly = typeof(AuthorizationPolicySetAttribute).Assembly;

    private static readonly Regex AccountCompanyFamilyRegex =
        new(@"^AccountCompanies", RegexOptions.Compiled);

    /// <summary>Token-minting action that must be rate-limited (name or route match).</summary>
    private static readonly Regex HeavyEndpointRegex =
        new(@"(switch)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly HashSet<string> CanonicalPolicies = new(StringComparer.Ordinal)
    {
        AccountCompanyRateLimitPolicies.Switch,
    };

    private static IReadOnlyList<MethodInfo> FamilyActions() =>
        ApiAssembly.GetTypes()
            .Where(static type =>
                type is { IsClass: true, IsAbstract: false } &&
                type.Namespace == "CLARIHR.Api.Controllers" &&
                type.Name.EndsWith("Controller", StringComparison.Ordinal) &&
                AccountCompanyFamilyRegex.IsMatch(type.Name))
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

    private static bool IsHeavy(MethodInfo method) =>
        HeavyEndpointRegex.IsMatch(method.Name) ||
        HeavyEndpointRegex.IsMatch(HttpRouteTemplate(method) ?? string.Empty);

    [Fact]
    public void EverySwitchEndpoint_DeclaresRateLimitPolicy()
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
            "AC-8: the token-minting switch endpoint must declare " +
            "[EnableRateLimiting(AccountCompanyRateLimitPolicies.Switch)] or it has no per-tenant abuse " +
            "guard. Missing/invalid on:\n  " + string.Join("\n  ", unguarded));
    }

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
            "[EnableRateLimiting] must reference a constant from AccountCompanyRateLimitPolicies. " +
            "Offending:\n  " + string.Join("\n  ", invalid));
    }

    [Fact]
    public void SwitchRateLimitPolicy_IsAppliedToAtLeastOneEndpoint()
    {
        var applied = FamilyActions()
            .Select(RateLimitPolicy)
            .Where(static policy => policy is not null)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(AccountCompanyRateLimitPolicies.Switch, applied);
    }
}

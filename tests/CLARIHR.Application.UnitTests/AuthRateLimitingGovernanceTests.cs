using System.Reflection;
using System.Text.RegularExpressions;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Features.Auth.Common;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Auth rate-limit guardrail (drift-proof by construction; mirrors <see cref="AuditRateLimitingGovernanceTests"/>).
/// Every anonymous (<c>[AllowAnonymous]</c>) action on <c>AuthController</c> — a credential / token-processing
/// endpoint reachable without authentication — MUST declare <c>[EnableRateLimiting]</c> with a canonical
/// <see cref="AuthRateLimitPolicies"/> policy (AU-2). Anonymous endpoints are identified by attribute (not by
/// name) so the guardrail survives renames and fails loudly if a new anonymous endpoint ships without a limit.
/// The authenticated <c>Logout</c> (<c>[Authorize]</c>) is intentionally out of scope.
/// </summary>
public sealed class AuthRateLimitingGovernanceTests
{
    private static readonly Assembly ApiAssembly = typeof(AuthorizationPolicySetAttribute).Assembly;

    private static readonly Regex FamilyRegex = new(@"^AuthController$", RegexOptions.Compiled);

    private static readonly HashSet<string> CanonicalPolicies = new(StringComparer.Ordinal)
    {
        AuthRateLimitPolicies.Login,
        AuthRateLimitPolicies.Register,
        AuthRateLimitPolicies.InviteAccept,
        AuthRateLimitPolicies.PasswordResetRequest,
        AuthRateLimitPolicies.PasswordResetSubmit,
        AuthRateLimitPolicies.Refresh,
        AuthRateLimitPolicies.EmailVerificationSubmit,
        AuthRateLimitPolicies.EmailVerificationResend,
    };

    private static IReadOnlyList<MethodInfo> FamilyActions() =>
        ApiAssembly.GetTypes()
            .Where(static type =>
                type is { IsClass: true, IsAbstract: false } &&
                type.Namespace == "CLARIHR.Api.Controllers" &&
                FamilyRegex.IsMatch(type.Name))
            .SelectMany(static type => type.GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(static method => IsHttpAction(method))
            .ToArray();

    private static bool IsHttpAction(MethodInfo method) =>
        method.GetCustomAttributes().Any(static attribute =>
            attribute.GetType().Name.StartsWith("Http", StringComparison.Ordinal) &&
            attribute.GetType().Name.EndsWith("Attribute", StringComparison.Ordinal));

    private static bool IsAnonymous(MethodInfo method) =>
        method.GetCustomAttributes().Any(static attribute =>
            attribute.GetType().Name == "AllowAnonymousAttribute");

    private static string? RateLimitPolicy(MethodInfo method) =>
        method.GetCustomAttributes()
            .Where(static attribute => attribute.GetType().Name == "EnableRateLimitingAttribute")
            .Select(static attribute =>
                attribute.GetType().GetProperty("PolicyName")?.GetValue(attribute) as string)
            .FirstOrDefault(static policy => !string.IsNullOrWhiteSpace(policy));

    [Fact]
    public void EveryAnonymousAuthEndpoint_DeclaresCanonicalRateLimitPolicy()
    {
        var anonymous = FamilyActions().Where(IsAnonymous).ToArray();

        Assert.NotEmpty(anonymous);

        var unguarded = anonymous
            .Where(static method => RateLimitPolicy(method) is not { } policy ||
                !CanonicalPolicies.Contains(policy))
            .Select(static method => $"{method.DeclaringType!.Name}.{method.Name}")
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            unguarded.Length == 0,
            "AU-2: every anonymous AuthController endpoint must declare [EnableRateLimiting] with a canonical " +
            "AuthRateLimitPolicies policy (anti-brute-force / DoS backstop). Missing or non-canonical on:\n  " +
            string.Join("\n  ", unguarded));
    }

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
            "[EnableRateLimiting] on AuthController must reference an AuthRateLimitPolicies constant. Offending:\n  " +
            string.Join("\n  ", invalid));
    }
}

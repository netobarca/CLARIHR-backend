using System.Reflection;
using System.Text.RegularExpressions;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Audit rate-limit guardrail (drift-proof by construction; mirrors
/// <see cref="ReportExportJobRateLimitingGovernanceTests"/>). The costly read on
/// <c>AuditController</c> — the paged audit-log search/list (any action returning
/// <see cref="PagedResponse{T}"/>) — MUST declare <c>[EnableRateLimiting(AuditRateLimitPolicies.Search)]</c>.
/// Costly endpoints are identified by return type (not method name) so the guardrail survives renames.
/// The tiny single-resource read (<c>GetById</c>) is not costly and is intentionally excluded.
/// </summary>
public sealed class AuditRateLimitingGovernanceTests
{
    private static readonly Assembly ApiAssembly = typeof(AuthorizationPolicySetAttribute).Assembly;

    private static readonly Regex FamilyRegex = new(@"^AuditController$", RegexOptions.Compiled);

    private static readonly HashSet<string> CanonicalPolicies = new(StringComparer.Ordinal)
    {
        AuditRateLimitPolicies.Search,
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

    // Detect actions by the presence of an Http*Attribute (NOT a route template): AuditController routes
    // the verb attributes off the class-level [Route], so [HttpGet] carries no Template — filtering on a
    // non-null template would silently drop the paged list and make this guardrail vacuously pass.
    private static bool IsHttpAction(MethodInfo method) =>
        method.GetCustomAttributes().Any(static attribute =>
            attribute.GetType().Name.StartsWith("Http", StringComparison.Ordinal) &&
            attribute.GetType().Name.EndsWith("Attribute", StringComparison.Ordinal));

    private static string? RateLimitPolicy(MethodInfo method) =>
        method.GetCustomAttributes()
            .Where(static attribute => attribute.GetType().Name == "EnableRateLimitingAttribute")
            .Select(static attribute =>
                attribute.GetType().GetProperty("PolicyName")?.GetValue(attribute) as string)
            .FirstOrDefault(static policy => !string.IsNullOrWhiteSpace(policy));

    private static bool ReturnsPagedResponse(MethodInfo method)
    {
        var type = method.ReturnType;

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

    [Fact]
    public void EveryCostlyAuditEndpoint_DeclaresRateLimitPolicy()
    {
        var costly = FamilyActions().Where(ReturnsPagedResponse).ToArray();

        Assert.NotEmpty(costly);

        var unguarded = costly
            .Where(static method => RateLimitPolicy(method) is not { } policy ||
                !CanonicalPolicies.Contains(policy))
            .Select(static method => $"{method.DeclaringType!.Name}.{method.Name}")
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            unguarded.Length == 0,
            "Every costly audit read (the paged search/list) must declare " +
            "[EnableRateLimiting(AuditRateLimitPolicies.Search)] or it has no per-tenant abuse guard. " +
            "Missing/invalid on:\n  " + string.Join("\n  ", unguarded));
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
            "[EnableRateLimiting] must reference a constant from AuditRateLimitPolicies. " +
            "Offending:\n  " + string.Join("\n  ", invalid));
    }

    [Fact]
    public void SearchPolicy_IsAppliedToAtLeastOneEndpoint()
    {
        var applied = FamilyActions()
            .Select(RateLimitPolicy)
            .Where(static policy => policy is not null)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(AuditRateLimitPolicies.Search, applied);
    }
}

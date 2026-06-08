using System.Reflection;
using System.Text.RegularExpressions;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.OrgStructureCatalogs.Common;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// OSC-007 Organization Structure Catalogs rate-limit guardrail (drift-proof by construction; mirrors
/// <see cref="OrgUnitRateLimitingGovernanceTests"/>). Every costly catalog read — a paged search/list
/// (any action returning <see cref="PagedResponse{T}"/>) — MUST declare an <c>[EnableRateLimiting]</c>
/// policy. A costly read shipping with no abuse guard fails CI. Identified by return type (not method
/// name). Tiny single-resource reads (GetById) are not <c>PagedResponse</c> and are intentionally
/// excluded.
/// </summary>
public sealed class OrgStructureCatalogRateLimitingGovernanceTests
{
    private static readonly Assembly ApiAssembly = typeof(AuthorizationPolicySetAttribute).Assembly;

    private static readonly Regex FamilyRegex =
        new(@"^OrganizationStructureCatalogsController$", RegexOptions.Compiled);

    private static readonly HashSet<string> CanonicalPolicies = new(StringComparer.Ordinal)
    {
        OrgStructureCatalogRateLimitPolicies.Search,
    };

    private static IReadOnlyList<MethodInfo> FamilyActions() =>
        ApiAssembly.GetTypes()
            .Where(static type =>
                type is { IsClass: true, IsAbstract: false } &&
                type.Namespace == "CLARIHR.Api.Controllers" &&
                FamilyRegex.IsMatch(type.Name))
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
    public void EveryCostlyCatalogSearch_DeclaresRateLimitPolicy()
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
            "OSC-007: every paged Organization Structure Catalogs search must declare " +
            "[EnableRateLimiting(OrgStructureCatalogRateLimitPolicies.Search)] or it has no per-tenant " +
            "abuse guard. Missing/invalid on:\n  " + string.Join("\n  ", unguarded));
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
            "[EnableRateLimiting] must reference a constant from OrgStructureCatalogRateLimitPolicies. " +
            "Offending:\n  " + string.Join("\n  ", invalid));
    }

    [Fact]
    public void SearchRateLimitPolicy_IsAppliedToAtLeastOneEndpoint()
    {
        var applied = FamilyActions()
            .Select(RateLimitPolicy)
            .Where(static policy => policy is not null)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(OrgStructureCatalogRateLimitPolicies.Search, applied);
    }
}

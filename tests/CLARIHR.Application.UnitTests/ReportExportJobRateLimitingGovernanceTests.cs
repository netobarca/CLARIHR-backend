using System.Reflection;
using System.Text.RegularExpressions;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Reports.Common;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// REX-E Report Export Jobs rate-limit guardrail (drift-proof by construction; mirrors
/// <see cref="OrgUnitRateLimitingGovernanceTests"/>). Every costly read on
/// <c>ReportExportJobsController</c> MUST declare an <c>[EnableRateLimiting]</c> policy: the paged
/// search/list (any action returning <see cref="PagedResponse{T}"/>) and the artifact
/// <c>/download</c> (streams a stored blob). Costly endpoints are identified by return type / route
/// suffix (not method name) so the guardrail survives renames. Tiny single-resource reads
/// (<c>GetById</c>) and the id-routed mutations (<c>/cancel</c>, create) are not costly and are
/// intentionally excluded.
/// </summary>
public sealed class ReportExportJobRateLimitingGovernanceTests
{
    private static readonly Assembly ApiAssembly = typeof(AuthorizationPolicySetAttribute).Assembly;

    private static readonly Regex FamilyRegex =
        new(@"^ReportExportJobsController$", RegexOptions.Compiled);

    private static readonly string[] CostlyRouteSuffixes =
    {
        "/download",
    };

    private static readonly HashSet<string> CanonicalPolicies = new(StringComparer.Ordinal)
    {
        ReportExportJobRateLimitPolicies.Search,
        ReportExportJobRateLimitPolicies.Download,
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

    private static bool IsCostly(MethodInfo method) =>
        ReturnsPagedResponse(method) ||
        (HttpRouteTemplate(method) is { } template &&
         CostlyRouteSuffixes.Any(suffix => template.EndsWith(suffix, StringComparison.Ordinal)));

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
    public void EveryCostlyReportExportJobEndpoint_DeclaresRateLimitPolicy()
    {
        var costly = FamilyActions().Where(IsCostly).ToArray();

        Assert.NotEmpty(costly);

        var unguarded = costly
            .Where(static method => RateLimitPolicy(method) is not { } policy ||
                !CanonicalPolicies.Contains(policy))
            .Select(static method => $"{method.DeclaringType!.Name}.{method.Name}")
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();

        Assert.True(
            unguarded.Length == 0,
            "REX-E: every costly report-export-jobs read (paged search/list + /download) must declare " +
            "[EnableRateLimiting(ReportExportJobRateLimitPolicies.*)] or it has no per-tenant abuse " +
            "guard. Missing/invalid on:\n  " + string.Join("\n  ", unguarded));
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
            "[EnableRateLimiting] must reference a constant from ReportExportJobRateLimitPolicies. " +
            "Offending:\n  " + string.Join("\n  ", invalid));
    }

    [Fact]
    public void AllRateLimitPolicies_AreAppliedToAtLeastOneEndpoint()
    {
        var applied = FamilyActions()
            .Select(RateLimitPolicy)
            .Where(static policy => policy is not null)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(ReportExportJobRateLimitPolicies.Search, applied);
        Assert.Contains(ReportExportJobRateLimitPolicies.Download, applied);
    }
}

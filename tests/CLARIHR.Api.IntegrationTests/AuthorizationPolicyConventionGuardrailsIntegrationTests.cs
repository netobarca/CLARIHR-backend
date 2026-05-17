using System.Reflection;
using CLARIHR.Api.Common.Conventions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// §S1 guardrail (finding §J1): verifies that on the fully-built pipeline the
/// <see cref="AuthorizationPolicyConvention"/> actually honors every controller's
/// <see cref="AuthorizationPolicySetAttribute"/> by HTTP verb, and that the declared
/// policy names resolve to real registered policies. Complements the reflection-only
/// governance unit test (which proves the right controllers are marked).
/// </summary>
public sealed class AuthorizationPolicyConventionGuardrailsIntegrationTests(IntegrationTestWebApplicationFactory factory)
    : IClassFixture<IntegrationTestWebApplicationFactory>
{
    private static bool IsReadVerb(IReadOnlyList<string> httpMethods) =>
        httpMethods.Count > 0 && httpMethods.All(static method =>
            string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Inv-4 — for every action of every marked controller: GET/HEAD-only endpoints carry
    /// the ReadPolicy, every other verb carries the ManagePolicy, and none is left with
    /// only the authenticated-only FallbackPolicy (the §J1 failure mode).
    /// </summary>
    [Fact]
    public void EveryMarkedControllerEndpoint_CarriesExpectedPolicyByVerb()
    {
        var endpointDataSource = factory.Services.GetRequiredService<EndpointDataSource>();

        var violations = new List<string>();

        foreach (var endpoint in endpointDataSource.Endpoints)
        {
            var actionDescriptor = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();
            var marker = actionDescriptor?.ControllerTypeInfo.GetCustomAttribute<AuthorizationPolicySetAttribute>();
            if (actionDescriptor is null || marker is null)
            {
                continue;
            }

            var httpMethods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [];
            var expectedPolicy = IsReadVerb(httpMethods) ? marker.ReadPolicy : marker.ManagePolicy;

            var appliedPolicies = endpoint.Metadata
                .GetOrderedMetadata<IAuthorizeData>()
                .Select(static data => data.Policy)
                .Where(static policy => !string.IsNullOrWhiteSpace(policy))
                .ToArray();

            if (!appliedPolicies.Contains(expectedPolicy, StringComparer.Ordinal))
            {
                violations.Add(
                    $"{actionDescriptor.ControllerName}.{actionDescriptor.ActionName} " +
                    $"[{string.Join(",", httpMethods)}] expected policy '{expectedPolicy}' but applied: " +
                    $"[{string.Join(", ", appliedPolicies.DefaultIfEmpty("<fallback-only>"))}]");
            }
        }

        Assert.True(
            violations.Count == 0,
            "Finding §J1/§S1: AuthorizationPolicyConvention did not apply the marker-declared " +
            "policy by verb on the live pipeline:\n  " + string.Join("\n  ", violations));
    }

    /// <summary>
    /// Inv-5 — every policy name declared by a marker resolves to a registered
    /// authorization policy (no typo, no policy missing its <c>AddPolicy</c> registration).
    /// </summary>
    [Fact]
    public async Task EveryMarkerPolicyName_ResolvesToARegisteredPolicy()
    {
        var endpointDataSource = factory.Services.GetRequiredService<EndpointDataSource>();
        var policyProvider = factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        var declaredPolicies = endpointDataSource.Endpoints
            .Select(static endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>())
            .Where(static actionDescriptor => actionDescriptor is not null)
            .Select(static actionDescriptor =>
                actionDescriptor!.ControllerTypeInfo.GetCustomAttribute<AuthorizationPolicySetAttribute>())
            .Where(static marker => marker is not null)
            .SelectMany(static marker => new[] { marker!.ReadPolicy, marker.ManagePolicy })
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(declaredPolicies);

        var unresolved = new List<string>();
        foreach (var policyName in declaredPolicies)
        {
            if (await policyProvider.GetPolicyAsync(policyName) is null)
            {
                unresolved.Add(policyName);
            }
        }

        Assert.True(
            unresolved.Count == 0,
            "[AuthorizationPolicySet] references policy names with no AddPolicy registration: " +
            string.Join(", ", unresolved));
    }
}

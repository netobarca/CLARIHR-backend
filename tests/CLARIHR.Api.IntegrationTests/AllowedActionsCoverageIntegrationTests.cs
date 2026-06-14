using System.Reflection;
using CLARIHR.Api.Authorization;
using CLARIHR.Application.Common.Policies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// Rollout guardrail for the centralized <c>allowedActions</c> feature. Asserts that any
/// controller opted-in via <see cref="ResourceActionsAttribute"/> is wired consistently:
/// every PUT/PATCH action returns a marker (<see cref="ISupportsAllowedActions"/>) type, and
/// its declared resource key is registered in <see cref="AllowedActionsRegistry"/>. The set
/// of asserted controllers grows automatically as resources are wired across rollout phases —
/// catching "added the attribute but forgot the marker/registry entry" (or vice versa).
/// </summary>
public sealed class AllowedActionsCoverageIntegrationTests(IntegrationTestWebApplicationFactory factory)
    : IClassFixture<IntegrationTestWebApplicationFactory>
{
    [Fact]
    public void OptedInControllers_ReturnMarkerTypeOnEveryPutPatchAction()
    {
        var endpointDataSource = factory.Services.GetRequiredService<EndpointDataSource>();
        var violations = new List<string>();

        foreach (var endpoint in endpointDataSource.Endpoints)
        {
            var actionDescriptor = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();
            var resourceActions = actionDescriptor?.ControllerTypeInfo.GetCustomAttribute<ResourceActionsAttribute>();
            if (actionDescriptor is null || resourceActions is null)
            {
                continue; // consistency is only required for controllers that opted in
            }

            var httpMethods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [];
            if (!IsPutOrPatch(httpMethods))
            {
                continue;
            }

            var responseType = UnwrapResponseType(actionDescriptor.MethodInfo.ReturnType);
            if (responseType is null || !typeof(ISupportsAllowedActions).IsAssignableFrom(responseType))
            {
                violations.Add(
                    $"{actionDescriptor.ControllerName}.{actionDescriptor.ActionName} " +
                    $"[{string.Join(",", httpMethods)}] returns '{responseType?.Name ?? "<unknown>"}' " +
                    "which does not implement ISupportsAllowedActions");
            }
        }

        Assert.True(
            violations.Count == 0,
            "Controllers with [ResourceActions] must return a marker type on every PUT/PATCH action:\n  "
            + string.Join("\n  ", violations));
    }

    [Fact]
    public void EveryDeclaredResourceKey_IsRegisteredInTheRegistry()
    {
        var endpointDataSource = factory.Services.GetRequiredService<EndpointDataSource>();

        var resourceKeys = endpointDataSource.Endpoints
            .Select(static endpoint => endpoint.Metadata.GetMetadata<ControllerActionDescriptor>())
            .Where(static actionDescriptor => actionDescriptor is not null)
            .Select(static actionDescriptor =>
                actionDescriptor!.ControllerTypeInfo.GetCustomAttribute<ResourceActionsAttribute>()?.ResourceKey)
            .Where(static resourceKey => !string.IsNullOrWhiteSpace(resourceKey))
            .Select(static resourceKey => resourceKey!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // The pilot controllers are wired, so at least one key must be present.
        Assert.NotEmpty(resourceKeys);

        var unregistered = resourceKeys
            .Where(static resourceKey => !AllowedActionsRegistry.TryGet(resourceKey, out _))
            .ToArray();

        Assert.True(
            unregistered.Length == 0,
            "[ResourceActions] keys with no AllowedActionsRegistry entry: " + string.Join(", ", unregistered));
    }

    private static bool IsPutOrPatch(IReadOnlyList<string> httpMethods) =>
        httpMethods.Any(static method =>
            string.Equals(method, "PUT", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(method, "PATCH", StringComparison.OrdinalIgnoreCase));

    private static Type? UnwrapResponseType(Type returnType)
    {
        var type = returnType;

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            if (definition == typeof(Task<>) || definition == typeof(ValueTask<>))
            {
                type = type.GetGenericArguments()[0];
            }
        }

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ActionResult<>))
        {
            type = type.GetGenericArguments()[0];
        }

        return type;
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Routing;

namespace CLARIHR.Api.Common.Conventions;

/// <summary>
/// Assigns the declarative authorization policy to every action of a controller that
/// carries <see cref="AuthorizationPolicySetAttribute"/>, by HTTP verb, as
/// defense-in-depth on top of the class-level <c>[Authorize]</c> (both compose with
/// AND — the handler remains the precise gate for tenant/entitlement/membership).
///
/// The endpoint→policy mapping is declared on the controller itself via the marker
/// attribute, so it cannot drift the way a remote hand-maintained dictionary could:
/// a newly added GET/POST inherits the correct policy automatically, and a new
/// in-scope controller that forgets the marker is caught by the guardrail tests
/// (<c>AuthorizationPolicyConventionGovernanceTests</c> /
/// <c>AuthorizationPolicyConventionGuardrailsIntegrationTests</c>) rather than
/// silently inheriting the bare authenticated <c>FallbackPolicy</c> (finding §J1 / §S1).
/// <list type="bullet">
///   <item>GET/HEAD only → <see cref="AuthorizationPolicySetAttribute.ReadPolicy"/></item>
///   <item>POST/PUT/PATCH/DELETE (any non-read verb) → <see cref="AuthorizationPolicySetAttribute.ManagePolicy"/></item>
/// </list>
/// Mirrors the project convention pattern (<see cref="ProducesStandardErrorsConvention"/>,
/// registered alongside it in <c>Program.cs</c>).
/// </summary>
public sealed class AuthorizationPolicyConvention : IActionModelConvention
{
    public void Apply(ActionModel action)
    {
        var marker = action.Controller.Attributes
            .OfType<AuthorizationPolicySetAttribute>()
            .FirstOrDefault();

        if (marker is null)
        {
            return;
        }

        var httpMethods = action.Attributes
            .OfType<HttpMethodAttribute>()
            .SelectMany(static attribute => attribute.HttpMethods)
            .ToArray();

        var isReadVerb = httpMethods.Length > 0 && httpMethods.All(static method =>
            string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(method, "HEAD", StringComparison.OrdinalIgnoreCase));

        var policyName = isReadVerb ? marker.ReadPolicy : marker.ManagePolicy;

        foreach (var selector in action.Selectors)
        {
            if (selector.EndpointMetadata
                .OfType<IAuthorizeData>()
                .Any(data => string.Equals(data.Policy, policyName, StringComparison.Ordinal)))
            {
                continue;
            }

            selector.EndpointMetadata.Add(new AuthorizeAttribute(policyName));
        }
    }
}

namespace CLARIHR.Api.Common.Conventions;

/// <summary>
/// Declares the declarative authorization policy pair a controller's actions inherit by
/// HTTP verb, as defense-in-depth on top of the class-level <c>[Authorize]</c> (both
/// compose with AND — the handler remains the precise gate for tenant/entitlement/
/// membership). <see cref="AuthorizationPolicyConvention"/> reads this marker at
/// application-model build time and assigns <see cref="ReadPolicy"/> to GET/HEAD-only
/// actions and <see cref="ManagePolicy"/> to every other verb.
///
/// This is the single, co-located source of truth for the endpoint→policy mapping:
/// because the policy travels with the controller, a newly added GET/POST inherits the
/// correct policy automatically and a new in-scope controller cannot silently fall back
/// to the bare authenticated <c>FallbackPolicy</c> the way a missing entry in a remote
/// dictionary could (the drift documented in JobProfiles debt §2.3 / finding §J1).
/// Mirrors the project convention pattern (<see cref="ProducesStandardErrorsAttribute"/>,
/// read by <see cref="ProducesStandardErrorsConvention"/> in the same way).
///
/// Pass policy-name constants from the canonical <c>*Policies</c> classes (e.g.
/// <c>JobProfilePolicies.Read</c>). The read and manage policies need not be symmetric:
/// <c>JobCatalogsController</c> uses <c>JobProfilePolicies.ManageCatalogs</c> for writes
/// (the CatalogAdmin gate), not the generic <c>JobProfilePolicies.Manage</c>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class AuthorizationPolicySetAttribute(string readPolicy, string managePolicy) : Attribute
{
    /// <summary>Policy assigned to GET/HEAD-only actions.</summary>
    public string ReadPolicy { get; } = readPolicy;

    /// <summary>Policy assigned to POST/PUT/PATCH/DELETE (and any non-read) actions.</summary>
    public string ManagePolicy { get; } = managePolicy;
}

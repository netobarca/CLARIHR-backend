namespace CLARIHR.Application.Features.PersonnelFiles.Common;

/// <summary>
/// Declarative authorization policy names for the Personnel Files shell controller,
/// assigned per HTTP verb by <c>AuthorizationPolicyConvention</c> via
/// <c>[AuthorizationPolicySet(Read, Manage)]</c> as defense-in-depth on top of the
/// class-level <c>[Authorize]</c>. The policies registered under these names in
/// <c>Program.cs</c> are kept a <b>superset</b> of the precise
/// <see cref="CLARIHR.Application.Abstractions.PersonnelFiles.IPersonnelFileAuthorizationService"/>
/// handler gate (<c>EnsureCanReadAsync</c> / <c>EnsureCanManageAsync</c>), so a legitimate
/// reader/manager is never falsely 403'd. The handler remains the precise gate for
/// tenant / entitlement / membership.
/// </summary>
public static class PersonnelFilePolicies
{
    public const string Read = "PersonnelFiles.Read";
    public const string Manage = "PersonnelFiles.Manage";

    /// <summary>
    /// Read policy for compensation sub-resources (D-16). Superset gate: the precise self-service /
    /// role check lives in the compensation read handlers.
    /// </summary>
    public const string ViewCompensation = "PersonnelFiles.ViewCompensation";

    /// <summary>
    /// Write policy for authorization substitutions (D-09): the dedicated
    /// <c>PersonnelFiles.ManageSubstitutions</c> permission, or Admin / IAM super-admin. Assigned to the
    /// write verbs of <c>PersonnelFileAuthorizationSubstitutionController</c>; reads use <see cref="Read"/>.
    /// Kept a superset of the precise <c>EnsureCanManageSubstitutionsAsync</c> handler gate so a legitimate
    /// manager is never falsely 403'd.
    /// </summary>
    public const string ManageSubstitutions = "PersonnelFiles.ManageSubstitutions";
}

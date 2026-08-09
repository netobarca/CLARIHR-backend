namespace CLARIHR.Application.Features.JobProfiles.Common;

public static class JobProfilePolicies
{
    public const string Read = "JobProfiles.Read";
    public const string Manage = "JobProfiles.Manage";

    /// <summary>
    /// H-01 — write policy of <c>JobProfileResolutionController</c> (publish / reopen / archive). Its
    /// codes are <c>{JobProfiles.Publish, iam.administration.manage}</c> and deliberately OMIT
    /// <c>JobProfiles.Admin</c>, so a profile administrator cannot approve their own descriptor.
    /// The transitions need their own controller precisely because <c>AuthorizationPolicySetAttribute</c>
    /// is class-level, so a single controller cannot map two different write policies.
    /// </summary>
    public const string Publish = "JobProfiles.Publish";

    /// <summary>
    /// Catalog-write policy for <c>JobCatalogsController</c>. Its codes mirror the
    /// <c>EnsureCanManageCatalogsAsync</c> (CatalogAdmin) handler gate exactly —
    /// <c>{JobCatalogs.Admin, iam.administration.manage}</c> — so the declarative
    /// (coarse) policy stays a superset of the precise handler gate and never
    /// produces a false 403 for a catalog-only admin. Distinct from <see cref="Manage"/>,
    /// whose codes require <c>JobProfiles.Admin</c> and omit <c>JobCatalogs.Admin</c>.
    /// </summary>
    public const string ManageCatalogs = "JobProfiles.ManageCatalogs";
}

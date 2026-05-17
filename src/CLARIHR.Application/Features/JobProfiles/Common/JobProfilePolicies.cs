namespace CLARIHR.Application.Features.JobProfiles.Common;

public static class JobProfilePolicies
{
    public const string Read = "JobProfiles.Read";
    public const string Manage = "JobProfiles.Manage";

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

namespace CLARIHR.Application.Abstractions.Tenancy;

/// <summary>
/// Every claim type the active tenant can arrive under, and the order to try them in.
///
/// The access token is minted with a plain <c>tid</c> claim, but that is NOT the name it carries on the
/// principal. The JWT bearer handler runs with inbound claim mapping enabled (the framework default), and
/// its default map renames <c>tid</c> to the Azure AD URI below. Reading only <c>"tid"</c> therefore finds
/// nothing on a real request while looking perfectly correct in a unit test, where no mapping happened.
///
/// Anything that reads the tenant off a principal must go through this list rather than hard-coding a
/// spelling — that mistake has silently denied every tenant-scoped request once already.
/// </summary>
public static class TenantClaimTypes
{
    public const string Short = "tid";

    public const string Mapped = "http://schemas.microsoft.com/identity/claims/tenantid";

    public static readonly string[] All = [Short, "tenantid", Mapped];
}

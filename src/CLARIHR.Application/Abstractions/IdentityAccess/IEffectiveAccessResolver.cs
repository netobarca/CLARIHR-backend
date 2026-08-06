namespace CLARIHR.Application.Abstractions.IdentityAccess;

/// <summary>
/// The roles and permission codes a user effectively holds inside one tenant, as of right now.
/// Both collections carry normalized (upper-case) values.
/// </summary>
public sealed record EffectiveAccess(
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions)
{
    public static readonly EffectiveAccess None = new([], []);
}

/// <summary>
/// Drops cached effective access so a revocation takes effect on the next request.
///
/// This is deliberately a separate interface from <see cref="IEffectiveAccessResolver"/> rather than a pair
/// of methods on it, and the split is load-bearing rather than cosmetic. The only consumer of invalidation
/// is the SaveChanges interceptor, and that interceptor is itself resolved while
/// <c>ApplicationDbContext</c>'s options are being built. Handing it the full resolver — which needs a
/// <c>DbContext</c> to run its query — closes the cycle
/// <c>DbContext → options → interceptor → resolver → DbContext</c>.
///
/// That cycle does not fail loudly. It is invisible to <c>ValidateOnBuild</c>, because it passes through the
/// <c>AddDbContext</c> options factory, which the container cannot analyse; at runtime one thread recurses
/// through it holding the scope lock while another blocks on that same lock, and the process deadlocks at 0%
/// CPU with no error. Keeping invalidation on a dependency that never touches the database is what makes the
/// cycle impossible to reintroduce by accident.
/// </summary>
public interface IEffectiveAccessInvalidator
{
    /// <summary>Drops the cached access of one user in one tenant (role assignment changed, user deactivated).</summary>
    void InvalidateUser(Guid userPublicId, Guid tenantId);

    /// <summary>
    /// Drops the cached access of EVERY user in a tenant. Required when a role's permissions change, because
    /// that silently alters the effective set of every user holding it.
    /// </summary>
    void InvalidateTenant(Guid tenantId);
}

/// <summary>
/// Single source of truth for "what may this user do in this tenant right now".
///
/// Authorization is deliberately NOT carried in the access token. The JWT proves identity (who the caller
/// is and which tenant they are acting in); the effective permission set is resolved here, per request,
/// from the current state of the database. That is what makes revocation take effect: stripping a role or
/// deactivating a user changes behaviour on the next request instead of when the token happens to expire.
///
/// Resolution is cached because it runs on every authenticated request. The cache is invalidated through
/// <see cref="IEffectiveAccessInvalidator"/>; the short TTL is only a safety net for multi-instance
/// deployments, where an invalidation raised on one instance does not reach the others.
/// </summary>
public interface IEffectiveAccessResolver
{
    Task<EffectiveAccess> ResolveAsync(Guid userPublicId, Guid tenantId, CancellationToken cancellationToken);
}

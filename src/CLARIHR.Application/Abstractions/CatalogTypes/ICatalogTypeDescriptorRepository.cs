using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.JobProfileCatalogTypes;
using CLARIHR.Domain.CatalogTypes;

namespace CLARIHR.Application.Abstractions.CatalogTypes;

public interface ICatalogTypeDescriptorRepository
{
    void Add(CatalogTypeDescriptor item);

    Task<CatalogTypeDescriptor?> GetByIdAsync(
        Guid publicId,
        CancellationToken cancellationToken);

    Task<bool> CodeExistsAsync(
        string normalizedCode,
        long? excludingId,
        CancellationToken cancellationToken);

    Task<PagedResponse<JobProfileCatalogTypeResponse>> SearchAsync(
        bool? isActive,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken);

    Task<JobProfileCatalogTypeResponse?> GetResponseByIdAsync(
        Guid publicId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns every registry row (active and inactive), cached. Used by the Job
    /// Profile catalog manifest, which exposes <c>isActive</c> so the frontend can
    /// hide/disable retired catalogs without a deploy.
    /// <para>
    /// <b>Freshness contract (§D4, doc technical-debt/07).</b> The snapshot is held
    /// in a process-local in-memory cache with a TTL of at most ~3 minutes. The
    /// registry is system-scoped, platform-managed, low-frequency reference data
    /// (see <see cref="CatalogTypeDescriptor"/>), so this is intentional and
    /// satisfies project-foundation.md §12.5 ("política clara de invalidación"):
    /// </para>
    /// <list type="bullet">
    /// <item>On the instance that performed a Backoffice mutation the change is
    /// visible <b>immediately</b> (the handler calls <see cref="Invalidate"/>).</item>
    /// <item>Across other instances in a multi-instance deployment coherency is
    /// <b>eventual, bounded by the TTL (≤ ~3 min)</b> — <see cref="Invalidate"/> is
    /// process-local and is not broadcast. Distributed invalidation is intentionally
    /// out of scope unless the business requires sub-TTL cross-instance immediacy.</item>
    /// </list>
    /// The TTL upper bound is pinned by
    /// <c>CatalogTypeDescriptorCacheFreshnessGuardrailsTests</c>; raising it past
    /// 3 minutes turns that guardrail red until this guarantee is updated.
    /// </summary>
    Task<IReadOnlyList<CatalogTypeDescriptorLookup>> GetAllAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Drops the cached registry snapshot. Called by the Backoffice mutation
    /// handlers after a create/update/activate/inactivate. Process-local: makes the
    /// change immediate on the editing instance only; other instances converge
    /// within the TTL (see the freshness contract on <see cref="GetAllAsync"/>).
    /// </summary>
    void Invalidate();
}

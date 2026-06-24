using CLARIHR.Application.Features.PositionDescriptionCatalogs;
using CLARIHR.Domain.PositionDescriptionCatalogs;

namespace CLARIHR.Application.Abstractions.PositionDescriptionCatalogs;

/// <summary>
/// §X-ISP (doc <c>05</c> §P5): narrow role-interface for the read-only catalog
/// reference lookups that **other** bounded contexts (JobProfiles, SalaryTabulator)
/// need to resolve and validate references to Position Description Catalog data.
/// These consumers depend on this 5-member role instead of the 30+ member
/// <see cref="IPositionDescriptionCatalogRepository"/> (CRUD + search + dependency
/// probes + cache invalidation), so they no longer take on members they never call.
/// The same repository class implements both; <see cref="IPositionDescriptionCatalogRepository"/>
/// extends this role so the catalog feature's own code is unaffected.
/// </summary>
public interface IPositionCatalogLookup
{
    /// <summary>Resolves the internal id of an active catalog item reference, or null if absent in the tenant.</summary>
    Task<CatalogReferenceInternal?> GetActiveCatalogReferenceAsync(
        Guid tenantId,
        PositionDescriptionCatalogType catalogType,
        Guid catalogItemId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Resolves an active catalog item reference by its (case-insensitive) code within the tenant, or null if
    /// absent/inactive. Mirrors <see cref="GetActiveCatalogReferenceAsync"/> but keys off the code instead of the
    /// public id — used by consumers that store a catalog code rather than a reference id (e.g. personnel-file
    /// curricular competencies). Backed by the unique <c>(tenant, type, normalized_code)</c> index.
    /// </summary>
    Task<CatalogReferenceInternal?> GetActiveCatalogReferenceByCodeAsync(
        Guid tenantId,
        PositionDescriptionCatalogType catalogType,
        string code,
        CancellationToken cancellationToken);

    /// <summary>True if the catalog item exists in another tenant (used to discriminate 404 vs tenant-mismatch).</summary>
    Task<bool> ExistsCatalogItemOutsideTenantAsync(Guid itemId, CancellationToken cancellationToken);

    /// <summary>True if the position category exists in another tenant (used to discriminate 404 vs tenant-mismatch).</summary>
    Task<bool> ExistsCategoryOutsideTenantAsync(Guid categoryId, CancellationToken cancellationToken);

    /// <summary>Resolves the internal id of a position category, or null if absent in the tenant.</summary>
    Task<long?> ResolvePositionCategoryIdAsync(Guid tenantId, Guid positionCategoryId, CancellationToken cancellationToken);

    /// <summary>Resolves the salary-class code from a salary-class catalog item id, or null if absent in the tenant.</summary>
    Task<string?> ResolveSalaryClassCodeByCatalogIdAsync(Guid tenantId, Guid salaryClassId, CancellationToken cancellationToken);
}

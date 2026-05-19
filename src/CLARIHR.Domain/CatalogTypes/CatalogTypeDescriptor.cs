using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.CatalogTypes;

/// <summary>
/// System-wide registry of the catalog types used by Job Profile and its sub-resources.
/// Managed globally by platform operators via Backoffice and surfaced to the frontend
/// through the Job Profile catalog manifest, so catalog keys are discovered instead of
/// hardcoded. The <see cref="SystemScopedCatalogItem.Code"/> is the immutable key;
/// <see cref="SystemScopedCatalogItem.Name"/>/<see cref="SystemScopedCatalogItem.SortOrder"/>
/// and the active flag are the editable metadata.
/// </summary>
/// <remarks>
/// <b>Naming map (§D7, doc technical-debt/07).</b> This entity is the canonical
/// source of truth. The two naming families for it are deliberate layering, not
/// drift — do <b>not</b> rename to unify (the API-facing names are serialized):
/// <list type="bullet">
/// <item><c>CatalogTypeDescriptor*</c> — the persistence/domain side:
/// this entity, <c>CatalogTypeDescriptorRepository</c>,
/// <c>CatalogTypeDescriptorSeedService</c>, <c>CatalogTypeDescriptorConfiguration</c>,
/// <c>ICatalogTypeDescriptorRepository</c>, and the read projection
/// <c>CatalogTypeDescriptorLookup</c>.</item>
/// <item><c>JobProfileCatalogType*</c> — the Job-Profile-feature/API surface over
/// it: <c>JobProfileCatalogTypeResponse</c>, the Backoffice
/// <c>JobProfileCatalogTypesController</c>, and the catalog-type commands/queries.
/// These names are bound to the wire contract / OpenAPI schema.</item>
/// </list>
/// </remarks>
public sealed class CatalogTypeDescriptor : SystemScopedCatalogItem
{
    private CatalogTypeDescriptor()
    {
    }

    private CatalogTypeDescriptor(Guid publicId, string code, string name, int sortOrder)
        : base(publicId, code, name, isActive: true, sortOrder)
    {
    }

    public static CatalogTypeDescriptor Create(string code, string name, int sortOrder) =>
        new(Guid.NewGuid(), code, name, sortOrder);
}

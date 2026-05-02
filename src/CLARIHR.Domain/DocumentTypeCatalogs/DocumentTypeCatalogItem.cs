using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.DocumentTypeCatalogs;

/// <summary>
/// System-wide catalog item for document types.
/// Managed globally by platform operators via Backoffice.
/// Used by PersonnelFileDocument to classify document kind.
/// </summary>
public sealed class DocumentTypeCatalogItem : SystemScopedCatalogItem
{
    private DocumentTypeCatalogItem()
    {
    }

    private DocumentTypeCatalogItem(Guid publicId, string code, string name, int sortOrder)
        : base(publicId, code, name, isActive: true, sortOrder)
    {
    }

    public static DocumentTypeCatalogItem Create(string code, string name, int sortOrder) =>
        new(Guid.NewGuid(), code, name, sortOrder);
}

using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.LegalRepresentatives;

public sealed class LegalRepresentativeDocumentTypeCatalogItem : Entity
{
    private LegalRepresentativeDocumentTypeCatalogItem()
    {
    }

    private LegalRepresentativeDocumentTypeCatalogItem(
        LegalRepresentativeDocumentType documentType,
        string name,
        int sortOrder)
    {
        if (!Enum.IsDefined(documentType))
        {
            throw new ArgumentOutOfRangeException(nameof(documentType), "Document type catalog item must use a defined enum value.");
        }

        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be greater than or equal to zero.");
        }

        Id = (long)documentType;
        Code = documentType.ToString();
        Name = Clean(name, nameof(name), 150);
        SortOrder = sortOrder;
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    public static LegalRepresentativeDocumentTypeCatalogItem Create(LegalRepresentativeDocumentTypeCatalogDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new LegalRepresentativeDocumentTypeCatalogItem(definition.DocumentType, definition.Name, definition.SortOrder);
    }

    private static string Clean(string value, string parameterName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be empty.", parameterName);
        }

        var cleaned = value.Trim();
        if (cleaned.Length > maxLength)
        {
            throw new ArgumentOutOfRangeException(parameterName, $"Value cannot exceed {maxLength} characters.");
        }

        return cleaned;
    }
}

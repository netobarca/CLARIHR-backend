using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.LegalRepresentatives;

public sealed class LegalRepresentativePositionTitleCatalogItem : Entity
{
    private LegalRepresentativePositionTitleCatalogItem()
    {
    }

    private LegalRepresentativePositionTitleCatalogItem(
        long id,
        string code,
        string name,
        int sortOrder)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Position title catalog item id must be greater than zero.");
        }

        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be greater than or equal to zero.");
        }

        Id = id;
        PublicId = CreateDeterministicPublicId($"legal-representative-position-title:{Id}");
        Code = Clean(code, nameof(code), 80).ToUpperInvariant();
        NormalizedCode = Code;
        Name = Clean(name, nameof(name), 150);
        SortOrder = sortOrder;
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    public static LegalRepresentativePositionTitleCatalogItem Create(LegalRepresentativePositionTitleCatalogDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new LegalRepresentativePositionTitleCatalogItem(definition.Id, definition.Code, definition.Name, definition.SortOrder);
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

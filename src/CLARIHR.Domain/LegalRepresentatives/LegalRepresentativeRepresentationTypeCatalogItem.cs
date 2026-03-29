using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.LegalRepresentatives;

public sealed class LegalRepresentativeRepresentationTypeCatalogItem : Entity
{
    private LegalRepresentativeRepresentationTypeCatalogItem()
    {
    }

    private LegalRepresentativeRepresentationTypeCatalogItem(
        LegalRepresentativeRepresentationType representationType,
        string name,
        int sortOrder)
    {
        if (!Enum.IsDefined(representationType))
        {
            throw new ArgumentOutOfRangeException(nameof(representationType), "Representation type catalog item must use a defined enum value.");
        }

        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be greater than or equal to zero.");
        }

        Id = (long)representationType;
        PublicId = CreateDeterministicPublicId($"legal-representative-representation-type:{Id}");
        Code = representationType.ToString().ToUpperInvariant();
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

    public static LegalRepresentativeRepresentationTypeCatalogItem Create(LegalRepresentativeRepresentationTypeCatalogDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new LegalRepresentativeRepresentationTypeCatalogItem(definition.RepresentationType, definition.Name, definition.SortOrder);
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

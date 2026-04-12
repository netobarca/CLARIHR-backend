using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.PersonnelFiles;

public sealed class PersonnelReferenceCatalogItem : Entity
{
    private PersonnelReferenceCatalogItem()
    {
    }

    private PersonnelReferenceCatalogItem(
        long id,
        string countryCode,
        string category,
        string code,
        string name,
        long? parentId,
        int sortOrder)
    {
        if (id == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Catalog item id cannot be zero.");
        }

        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be greater than or equal to zero.");
        }

        Id = id;
        CountryCode = Clean(countryCode, nameof(countryCode), 2).ToUpperInvariant();
        Category = Clean(category, nameof(category), 80);
        Code = PersonnelFileNormalization.NormalizeCode(code);
        NormalizedCode = Code;
        Name = Clean(name, nameof(name), 150);
        NormalizedName = PersonnelFileNormalization.NormalizeName(name);
        ParentId = parentId;
        SortOrder = sortOrder;
        IsActive = true;
        PublicId = CreateDeterministicPublicId($"personnel-reference-catalog:{CountryCode}:{Category}:{Code}");
    }

    public string CountryCode { get; private set; } = string.Empty;

    public string Category { get; private set; } = string.Empty;

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public long? ParentId { get; private set; }

    public PersonnelReferenceCatalogItem? Parent { get; private set; }

    public int SortOrder { get; private set; }

    public bool IsActive { get; private set; }

    public static PersonnelReferenceCatalogItem Create(PersonnelReferenceCatalogDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new PersonnelReferenceCatalogItem(
            definition.Id,
            definition.CountryCode,
            definition.Category,
            definition.Code,
            definition.Name,
            definition.ParentId,
            definition.SortOrder);
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

using CLARIHR.Domain.Common;

namespace CLARIHR.Domain.Locations;

public sealed class CountryCatalogItem : Entity
{
    private CountryCatalogItem()
    {
    }

    private CountryCatalogItem(
        long id,
        string code,
        string name,
        int sortOrder,
        string defaultLocale)
    {
        if (id == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "Country catalog item id cannot be zero.");
        }

        if (sortOrder < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sortOrder), "Sort order must be greater than or equal to zero.");
        }

        Id = id;
        Code = Clean(code, nameof(code), 2).ToUpperInvariant();
        PublicId = CreateDeterministicPublicId($"country-catalog:{Code}");
        NormalizedCode = Code;
        Name = Clean(name, nameof(name), 150);
        SortOrder = sortOrder;
        DefaultLocale = Clean(defaultLocale, nameof(defaultLocale), 16);
        IsActive = true;
    }

    public string Code { get; private set; } = string.Empty;

    public string NormalizedCode { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public string DefaultLocale { get; private set; } = "en";

    public bool IsActive { get; private set; }

    public static CountryCatalogItem Create(CountryCatalogDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new CountryCatalogItem(
            definition.Id,
            definition.Code,
            definition.Name,
            definition.SortOrder,
            ResolveDefaultLocale(definition.Code, definition.DefaultLocale));
    }

    private static string ResolveDefaultLocale(string code, string? configuredDefaultLocale)
    {
        if (!string.IsNullOrWhiteSpace(configuredDefaultLocale))
        {
            return configuredDefaultLocale.Trim();
        }

        var normalizedCode = Clean(code, nameof(code), 2).ToUpperInvariant();
        return normalizedCode switch
        {
            "SV" or "ES" or "AR" or "BO" or "CL" or "CO" or "CR" or "CU" or "DO" or "EC" or "GT" or "HN" or "MX" or "NI" or "PA" or "PE" or "PR" or "PY" or "UY" or "VE" => "es",
            "BR" or "PT" => "pt",
            _ => "en"
        };
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

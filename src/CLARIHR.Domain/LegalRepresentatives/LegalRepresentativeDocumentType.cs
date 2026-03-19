namespace CLARIHR.Domain.LegalRepresentatives;

public enum LegalRepresentativeDocumentType
{
    NationalId = 1,
    Passport = 2,
    TaxId = 3,
    Other = 4
}

public static class LegalRepresentativeDocumentTypeCatalog
{
    public static IReadOnlyList<LegalRepresentativeDocumentTypeCatalogDefinition> Items { get; } =
    [
        new(LegalRepresentativeDocumentType.NationalId, "National ID", 1),
        new(LegalRepresentativeDocumentType.Passport, "Passport", 2),
        new(LegalRepresentativeDocumentType.TaxId, "Tax ID", 3),
        new(LegalRepresentativeDocumentType.Other, "Other", 4)
    ];
}

public sealed record LegalRepresentativeDocumentTypeCatalogDefinition(
    LegalRepresentativeDocumentType DocumentType,
    string Name,
    int SortOrder)
{
    public long Id => (long)DocumentType;

    public string Code => DocumentType.ToString();
}

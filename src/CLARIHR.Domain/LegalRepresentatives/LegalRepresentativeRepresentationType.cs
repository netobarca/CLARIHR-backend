namespace CLARIHR.Domain.LegalRepresentatives;

public enum LegalRepresentativeRepresentationType
{
    PrimaryLegalRepresentative = 1,
    AlternateLegalRepresentative = 2,
    AttorneyInFact = 3
}

public static class LegalRepresentativeRepresentationTypeCatalog
{
    public static IReadOnlyList<LegalRepresentativeRepresentationTypeCatalogDefinition> Items { get; } =
    [
        new(LegalRepresentativeRepresentationType.PrimaryLegalRepresentative, "Primary Legal Representative", 1),
        new(LegalRepresentativeRepresentationType.AlternateLegalRepresentative, "Alternate Legal Representative", 2),
        new(LegalRepresentativeRepresentationType.AttorneyInFact, "Attorney in Fact", 3)
    ];
}

public sealed record LegalRepresentativeRepresentationTypeCatalogDefinition(
    LegalRepresentativeRepresentationType RepresentationType,
    string Name,
    int SortOrder)
{
    public long Id => (long)RepresentationType;

    public string Code => RepresentationType.ToString();
}

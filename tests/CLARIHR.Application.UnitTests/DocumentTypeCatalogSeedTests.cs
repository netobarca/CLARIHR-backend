using System.Reflection;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// H-22 — the platform baseline of document types, and the guard that used to reject it.
/// <para>
/// The catalog shipped empty while `personnel_file_documents` and `medical_claim_documents` carry a NOT NULL FK to
/// it, so a fresh environment could not attach one single document. The management surface already existed
/// (`api/platform/document-type-catalogs`, Backoffice API); what was missing was the seed.
/// </para>
/// </summary>
public sealed class DocumentTypeCatalogSeedTests
{
    private static readonly object[] SeededItems = ReadSeed();

    [Fact]
    public void Seed_ShipsTheTwelveBaselineTypes()
    {
        Assert.Equal(12, SeededItems.Length);
        var codes = SeededItems.Select(item => Read<string>(item, "Code")).ToArray();

        Assert.Contains("CONSTANCIA_MEDICA", codes);
        Assert.Contains("CONTRATO", codes);
        Assert.Contains("RESPALDO", codes);
        Assert.Contains("OTRO", codes);
    }

    [Fact]
    public void Seed_HasUniqueCodesIdsAndPublicIds()
    {
        var codes = SeededItems.Select(item => Read<string>(item, "Code")).ToArray();
        var ids = SeededItems.Select(item => Read<long>(item, "Id")).ToArray();
        var publicIds = SeededItems.Select(item => Read<Guid>(item, "PublicId")).ToArray();

        Assert.Equal(codes.Length, codes.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(ids.Length, ids.Distinct().Count());
        Assert.Equal(publicIds.Length, publicIds.Distinct().Count());
        Assert.All(publicIds, publicId => Assert.NotEqual(Guid.Empty, publicId));
    }

    /// <summary>
    /// The domain uppercases the code and derives `NormalizedCode`/`NormalizedName` from it, so a seeded row that
    /// did not follow the same shape would differ from any row the Backoffice writes later.
    /// </summary>
    [Fact]
    public void Seed_MatchesWhatTheDomainWouldWrite()
    {
        foreach (var item in SeededItems)
        {
            var code = Read<string>(item, "Code");
            var name = Read<string>(item, "Name");

            Assert.Equal(code.ToUpperInvariant(), code);
            Assert.Equal(code, Read<string>(item, "NormalizedCode"));
            Assert.Equal(name.ToUpperInvariant(), Read<string>(item, "NormalizedName"));
            Assert.True(Read<bool>(item, "IsActive"));
            Assert.True(Read<int>(item, "SortOrder") >= 0);
        }
    }

    /// <summary>
    /// The seed uses NEGATIVE ids (repo-wide `HasData` convention, so they never collide with the identity
    /// sequence) and the attachment aggregates guarded the FK with `<= 0` — which rejected precisely the rows that
    /// ship. The guard now rejects only the unset default.
    /// </summary>
    [Fact]
    public void Document_AcceptsASeededNegativeCatalogId()
    {
        var seededId = SeededItems.Select(item => Read<long>(item, "Id")).First();
        Assert.True(seededId < 0, "The baseline is expected to use the negative HasData id band.");

        var document = PersonnelFileDocument.Create(
            Guid.NewGuid(), seededId, Guid.NewGuid(), "contrato.pdf", "application/pdf", 2048, null);

        Assert.Equal(seededId, document.DocumentTypeCatalogItemId);
    }

    [Fact]
    public void Document_StillRejectsAnUnsetCatalogId()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PersonnelFileDocument.Create(
            Guid.NewGuid(), 0, Guid.NewGuid(), "contrato.pdf", "application/pdf", 2048, null));
    }

    private static object[] ReadSeed()
    {
        // GlobalCatalogSeedData is internal to the infrastructure assembly; the seed rows are anonymous objects.
        var type = typeof(CLARIHR.Infrastructure.Persistence.ApplicationDbContext).Assembly
            .GetType("CLARIHR.Infrastructure.Persistence.GlobalCatalogSeedData")
            ?? throw new InvalidOperationException("GlobalCatalogSeedData not found.");
        var method = type.GetMethod("GetDocumentTypeCatalogItems", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("GetDocumentTypeCatalogItems not found.");

        return ((IEnumerable<object>)method.Invoke(null, null)!).ToArray();
    }

    private static T Read<T>(object item, string propertyName) =>
        (T)item.GetType().GetProperty(propertyName)!.GetValue(item)!;
}

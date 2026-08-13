using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Domain.Files;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// H-22 — `document_type_catalog_items` shipped EMPTY, and two attachment families have a NOT NULL FK to it
/// (`personnel_file_documents`, `medical_claim_documents`), so a freshly provisioned environment could not attach
/// a single document to a personnel file. The management surface always existed —
/// `api/platform/document-type-catalogs` in the Backoffice API, with its own tests — what was missing was the
/// baseline: the catalog is global and platform-curated, so nobody per tenant could fill it either.
/// <para>
/// These tests assert the seeded baseline is enough on its own: no test creates a type, they use what ships.
/// </para>
/// </summary>
public sealed partial class ApiIntegrationTests
{
    [Fact]
    public async Task Catalogs_FileDocumentTypes_ShouldReturnTheSeededBaseline()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var response = await client.GetAsync("/api/v1/general-catalogs/file-document-types");

        response.EnsureSuccessStatusCode();
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var codes = document.RootElement.EnumerateArray()
            .Select(item => item.GetProperty("code").GetString())
            .ToArray();

        // The whole point of the finding: this used to be an empty list on every fresh environment.
        Assert.NotEmpty(codes);
        Assert.Contains("CONSTANCIA_MEDICA", codes);
        Assert.Contains("CONTRATO", codes);
        Assert.Contains("RESPALDO", codes);
        Assert.Contains("OTRO", codes);

        // Global catalog: it is the same with or without a country (H-21 keeps it in the system-scoped branch).
        var withCountry = await client.GetAsync("/api/v1/general-catalogs/file-document-types?countryCode=SV");
        withCountry.EnsureSuccessStatusCode();
        using var countryDocument = JsonDocument.Parse(await withCountry.Content.ReadAsStringAsync());
        Assert.Equal(
            codes,
            countryDocument.RootElement.EnumerateArray().Select(item => item.GetProperty("code").GetString()).ToArray());
    }

    /// <summary>
    /// The end-to-end proof: attach a personnel-file document picking a type from the SHIPPED catalog. Before the
    /// seed this was impossible on a fresh environment — the field is a non-nullable FK and no type existed.
    /// </summary>
    [Fact]
    public async Task PersonnelFiles_AddDocument_WithASeededType_ShouldSucceed()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var created = await CreatePersonnelFileAsync(client, scenario.TenantId, "Documenta", "Sembrada", "DUI", "07777777-8");

        var catalogResponse = await client.GetAsync("/api/v1/general-catalogs/file-document-types");
        catalogResponse.EnsureSuccessStatusCode();
        using var catalog = JsonDocument.Parse(await catalogResponse.Content.ReadAsStringAsync());
        var contractType = catalog.RootElement.EnumerateArray()
            .First(item => item.GetProperty("code").GetString() == "CONTRATO");
        // The public contract renames `Id` to `publicId` (PublicContractJsonTypeInfoResolver).
        var documentTypePublicId = contractType.GetProperty("publicId").GetGuid();

        var filePublicId = await SeedUploadedFileAsync(scenario, created.Id);

        var addResponse = await client.PostAsJsonAsync($"/api/v1/personnel-files/{created.Id}/documents", new
        {
            filePublicId,
            documentTypeCatalogItemPublicId = documentTypePublicId,
            observations = "Contrato firmado"
        });

        var payload = await addResponse.Content.ReadAsStringAsync();
        Assert.True(HttpStatusCode.Created == addResponse.StatusCode, $"Add document failed: {(int)addResponse.StatusCode} {payload}");
        using var document = JsonDocument.Parse(payload);
        Assert.Equal("CONTRATO", document.RootElement.GetProperty("documentTypeCode").GetString());
    }

    /// <summary>Non-regression: the type still has to exist — the seed must not soften the guard.</summary>
    [Fact]
    public async Task PersonnelFiles_AddDocument_WithUnknownType_ShouldReturn400()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(CreatePersonnelFileAdminContext(scenario));

        var created = await CreatePersonnelFileAsync(client, scenario.TenantId, "Documenta", "Invalida", "DUI", "07777777-9");
        var filePublicId = await SeedUploadedFileAsync(scenario, created.Id);

        var addResponse = await client.PostAsJsonAsync($"/api/v1/personnel-files/{created.Id}/documents", new
        {
            filePublicId,
            documentTypeCatalogItemPublicId = Guid.NewGuid(),
            observations = (string?)null
        });

        Assert.Equal(HttpStatusCode.BadRequest, addResponse.StatusCode);
    }

    /// <summary>An uploaded file WITHOUT creating a document type — the catalog must come from the seed.</summary>
    private async Task<Guid> SeedUploadedFileAsync(IntegrationTestScenario scenario, Guid personnelFilePublicId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        const string fileName = "contrato.pdf";
        const string contentType = "application/pdf";
        const int sizeBytes = 2048;

        var file = StoredFile.Create(
            fileName,
            contentType,
            sizeBytes,
            ".pdf",
            StorageProvider.AzureBlob,
            "clarihr-personnel-documents",
            $"personnel-documents/{Guid.NewGuid():N}.pdf",
            FilePurpose.PersonnelDocument,
            FileUploadType.DirectUpload,
            scenario.ActorUserId.ToString(),
            personnelFilePublicId);
        file.SetTenantId(scenario.TenantId);
        file.MarkActive(sizeBytes, contentType);
        dbContext.Set<StoredFile>().Add(file);

        await dbContext.SaveChangesAsync();
        return file.PublicId;
    }
}

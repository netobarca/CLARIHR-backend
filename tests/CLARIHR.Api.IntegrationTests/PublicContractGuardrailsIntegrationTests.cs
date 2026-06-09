using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using CLARIHR.Domain.Common;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

public sealed class PublicContractGuardrailsIntegrationTests(IntegrationTestWebApplicationFactory factory)
    : IClassFixture<IntegrationTestWebApplicationFactory>
{
    private static readonly Regex RoutePlaceholderRegex = new(@"\{(?<name>[^}:]+)(?::[^}]+)?\}", RegexOptions.Compiled);

    [Fact]
    public async Task Swagger_ShouldExposeOnlyPublicIdentifiers_AndNormalizedCodes()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        var schemas = root.GetProperty("components").GetProperty("schemas");
        foreach (var schema in schemas.EnumerateObject())
        {
            if (!schema.Value.TryGetProperty("properties", out var properties))
            {
                continue;
            }

            var propertyNames = properties.EnumerateObject()
                .Select(property => property.Name)
                .ToArray();

            Assert.DoesNotContain("internalId", propertyNames, StringComparer.OrdinalIgnoreCase);

            // §S6: the legacy checks below are NAME-pattern only. PublicContractNaming
            // renames/suppresses Guid ids exclusively, so a surrogate integer
            // (`long Id`, `int CategoryId`) would be neither renamed nor caught by
            // name — a raw enumeration/IDOR leak (foundation §10.3). Make the
            // guardrail TYPE-aware: no public schema property may be an integer
            // (int32/int64, nullable or not) AND carry an identifier-shaped name.
            // A non-enum integer named like an id is itself the defect.
            foreach (var property in properties.EnumerateObject())
            {
                if (!IsIdentifierLikeName(property.Name) || IsEnumSchema(property.Value))
                {
                    continue;
                }

                Assert.False(
                    IsIntegerSchema(property.Value),
                    $"Schema '{schema.Name}' exposes integer identifier-shaped property " +
                    $"'{property.Name}' (foundation §10.3 / doc `06` §S6: surrogate " +
                    "integer id leak — keep the BIGINT internal, expose the Guid publicId).");
            }

            foreach (var propertyName in propertyNames.Where(IsIdentifierName))
            {
                Assert.True(
                    IsPublicIdentifierName(propertyName),
                    $"Schema '{schema.Name}' exposes legacy identifier property '{propertyName}'.");

                Assert.False(
                    HasDuplicatedPublicSegment(propertyName),
                    $"Schema '{schema.Name}' exposes duplicated public identifier property '{propertyName}'.");
            }

            if (propertyNames.Contains("code", StringComparer.Ordinal))
            {
                Assert.Contains("normalizedCode", propertyNames, StringComparer.Ordinal);
            }
        }

        var paths = root.GetProperty("paths");
        foreach (var path in paths.EnumerateObject())
        {
            foreach (Match match in RoutePlaceholderRegex.Matches(path.Name))
            {
                var placeholder = match.Groups["name"].Value;
                if (!IsIdentifierName(placeholder))
                {
                    continue;
                }

                Assert.True(
                    IsPublicIdentifierName(placeholder),
                    $"Path '{path.Name}' exposes legacy route placeholder '{placeholder}'.");

                Assert.False(
                    HasDuplicatedPublicSegment(placeholder),
                    $"Path '{path.Name}' exposes duplicated public route placeholder '{placeholder}'.");
            }

            foreach (var operation in path.Value.EnumerateObject())
            {
                if (!operation.Value.TryGetProperty("parameters", out var parameters))
                {
                    continue;
                }

                foreach (var parameter in parameters.EnumerateArray())
                {
                    var name = parameter.GetProperty("name").GetString();
                    var location = parameter.GetProperty("in").GetString();
                    if (!IsIdentifierName(name))
                    {
                        continue;
                    }

                    Assert.True(
                        IsPublicIdentifierName(name!),
                        $"Operation '{operation.Name}' on path '{path.Name}' exposes legacy parameter '{name}'.");

                    Assert.False(
                        HasDuplicatedPublicSegment(name!),
                        $"Operation '{operation.Name}' on path '{path.Name}' exposes duplicated public parameter '{name}'.");
                }
            }
        }
    }

    [Fact]
    public async Task Swagger_ShouldUsePublicIdentifiers_ForDirectResourceRoutes_AndCompanyPublicId_ForCompanyContextRoutes()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var pathNames = document.RootElement.GetProperty("paths")
            .EnumerateObject()
            .Select(static path => path.Name)
            .ToArray();

        Assert.Contains("/api/v1/account/companies/{companyPublicId}", pathNames);
        Assert.Contains("/api/v1/account/companies/{companyPublicId}/switch", pathNames);
        Assert.Contains("/api/v1/companies/{companyPublicId}/cost-centers", pathNames);
        Assert.Contains("/api/v1/companies/{companyPublicId}/job-profiles", pathNames);
        Assert.Contains("/api/v1/personnel-files/{publicId}/identifications", pathNames);
        Assert.Contains("/api/v1/personnel-files/{publicId}/addresses", pathNames);
        Assert.Contains("/api/v1/personnel-files/{publicId}/addresses/{addressPublicId}", pathNames);
        Assert.Contains("/api/v1/personnel-files/{publicId}/emergency-contacts", pathNames);
        Assert.Contains("/api/v1/personnel-files/{publicId}/emergency-contacts/{emergencyContactPublicId}", pathNames);
        Assert.Contains("/api/v1/personnel-files/{publicId}/family-members", pathNames);
        Assert.Contains("/api/v1/personnel-files/{publicId}/family-members/{familyMemberPublicId}", pathNames);
        Assert.Contains("/api/v1/personnel-files/{publicId}/documents", pathNames);

        Assert.DoesNotContain("/api/v1/account/companies/{publicId}", pathNames);
        Assert.DoesNotContain("/api/v1/account/companies/{publicId}/switch", pathNames);
        Assert.DoesNotContain("/api/v1/job-profiles/{id}", pathNames);
        Assert.DoesNotContain(pathNames, static path => path.Contains("{companyId}", StringComparison.Ordinal));
        Assert.DoesNotContain("/api/v1/personnel-file-documents/{publicId}/file", pathNames);
        Assert.DoesNotContain("/api/v1/personnel-file-documents/{publicId}/inactivate", pathNames);

        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.GetProperty("/api/v1/personnel-files/{publicId}/addresses").TryGetProperty("get", out _));
        Assert.True(paths.GetProperty("/api/v1/personnel-files/{publicId}/addresses").TryGetProperty("post", out _));
        Assert.False(paths.GetProperty("/api/v1/personnel-files/{publicId}/addresses").TryGetProperty("put", out _));
        Assert.True(paths.GetProperty("/api/v1/personnel-files/{publicId}/addresses/{addressPublicId}").TryGetProperty("put", out _));
        Assert.True(paths.GetProperty("/api/v1/personnel-files/{publicId}/addresses/{addressPublicId}").TryGetProperty("delete", out _));

        Assert.True(paths.GetProperty("/api/v1/personnel-files/{publicId}/emergency-contacts").TryGetProperty("get", out _));
        Assert.True(paths.GetProperty("/api/v1/personnel-files/{publicId}/emergency-contacts").TryGetProperty("post", out _));
        Assert.False(paths.GetProperty("/api/v1/personnel-files/{publicId}/emergency-contacts").TryGetProperty("put", out _));
        Assert.True(paths.GetProperty("/api/v1/personnel-files/{publicId}/emergency-contacts/{emergencyContactPublicId}").TryGetProperty("put", out _));
        Assert.True(paths.GetProperty("/api/v1/personnel-files/{publicId}/emergency-contacts/{emergencyContactPublicId}").TryGetProperty("delete", out _));

        Assert.True(paths.GetProperty("/api/v1/personnel-files/{publicId}/family-members").TryGetProperty("get", out _));
        Assert.True(paths.GetProperty("/api/v1/personnel-files/{publicId}/family-members").TryGetProperty("post", out _));
        Assert.False(paths.GetProperty("/api/v1/personnel-files/{publicId}/family-members").TryGetProperty("put", out _));
        Assert.True(paths.GetProperty("/api/v1/personnel-files/{publicId}/family-members/{familyMemberPublicId}").TryGetProperty("put", out _));
        Assert.True(paths.GetProperty("/api/v1/personnel-files/{publicId}/family-members/{familyMemberPublicId}").TryGetProperty("delete", out _));
    }

    [Fact]
    public async Task Swagger_ShouldNotExposeHybridParentChildActionRoutes()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var pathNames = document.RootElement.GetProperty("paths")
            .EnumerateObject()
            .Select(static path => path.Name)
            .ToArray();

        var nestedCollections = pathNames
            .Select(TryParseNestedCollectionRoute)
            .Where(static route => route is not null)
            .Cast<NestedCollectionRoute>()
            .ToArray();

        foreach (var collection in nestedCollections)
        {
            var hybridPrefix = $"/api/v1/{collection.ParentSegment}/{collection.ChildSegment}/{{";
            var hybridRoutes = pathNames
                .Where(path => path.StartsWith(hybridPrefix, StringComparison.Ordinal))
                .ToArray();

            Assert.True(
                hybridRoutes.Length == 0,
                $"Swagger exposes hybrid parent/child action routes for nested collection '/api/v1/{collection.ParentSegment}/{{{collection.ParentIdentifier}}}/{collection.ChildSegment}'. " +
                $"Move direct child actions to a flat child resource route instead of keeping '/api/v1/{collection.ParentSegment}/{collection.ChildSegment}/{{...}}/...'. " +
                $"Found: {string.Join(", ", hybridRoutes)}");
        }
    }

    [Fact]
    public async Task Swagger_ShouldExposeTypedCreatePersonnelFileRequest_AndShellResponse()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var postOperation = document.RootElement.GetProperty("paths")
            .GetProperty("/api/v1/companies/{companyPublicId}/personnel-files")
            .GetProperty("post");

        var requestSchemaReference = postOperation.GetProperty("requestBody")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema")
            .GetProperty("$ref")
            .GetString();
        Assert.Contains("CreatePersonnelFileRequest", requestSchemaReference, StringComparison.Ordinal);

        var responseSchemaReference = postOperation.GetProperty("responses")
            .GetProperty("201")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema")
            .GetProperty("$ref")
            .GetString();
        Assert.Contains("PersonnelFileShellResponse", responseSchemaReference, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Swagger_ShouldExposePersonnelFilesCoreContracts_WithShellLifecycleAndLimitedSearchProjection()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");

        Assert.True(paths.TryGetProperty("/api/v1/companies/{companyPublicId}/personnel-files", out var collectionPath));
        Assert.True(paths.TryGetProperty("/api/v1/personnel-files/{publicId}", out var shellPath));

        // The former state-only PATCH endpoints were folded into the unified shell PATCH.
        Assert.False(paths.TryGetProperty("/api/v1/personnel-files/{publicId}/activate", out _));
        Assert.False(paths.TryGetProperty("/api/v1/personnel-files/{publicId}/inactivate", out _));

        var searchOperation = collectionPath.GetProperty("get");
        Assert.True(searchOperation.GetProperty("responses").TryGetProperty("429", out _));

        var searchResponseSchemaReference = searchOperation.GetProperty("responses")
            .GetProperty("200")
            .GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema")
            .GetProperty("$ref")
            .GetString();
        Assert.NotNull(searchResponseSchemaReference);
        Assert.Contains("PagedResponse", searchResponseSchemaReference, StringComparison.Ordinal);

        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var searchSchemaName = searchResponseSchemaReference[(searchResponseSchemaReference.LastIndexOf('/') + 1)..];
        var searchSchema = schemas.GetProperty(searchSchemaName);
        var listItemSchemaReference = searchSchema.GetProperty("properties")
            .GetProperty("items")
            .GetProperty("items")
            .GetProperty("$ref")
            .GetString();
        Assert.NotNull(listItemSchemaReference);

        var listItemSchemaName = listItemSchemaReference[(listItemSchemaReference.LastIndexOf('/') + 1)..];
        var listItemSchema = schemas.GetProperty(listItemSchemaName);
        var listItemPropertyNames = listItemSchema.GetProperty("properties")
            .EnumerateObject()
            .Select(static property => property.Name)
            .ToArray();
        Assert.DoesNotContain("birthDate", listItemPropertyNames, StringComparer.Ordinal);
        Assert.DoesNotContain("concurrencyToken", listItemPropertyNames, StringComparer.Ordinal);

        Assert.True(shellPath.GetProperty("get").GetProperty("responses").TryGetProperty("200", out var shellResponse));
        var shellResponseSchemaReference = shellResponse.GetProperty("content")
            .GetProperty("application/json")
            .GetProperty("schema")
            .GetProperty("$ref")
            .GetString();
        Assert.Contains("PersonnelFileShellResponse", shellResponseSchemaReference, StringComparison.Ordinal);

        // The unified shell PATCH replaces /activate and /inactivate: it is rate-limited
        // (429) and, like PUT, returns the updated personal information.
        var shellPatchOperation = shellPath.GetProperty("patch");
        Assert.True(shellPatchOperation.GetProperty("responses").TryGetProperty("429", out _));

        foreach (var writeVerb in new[] { "put", "patch" })
        {
            var writeResponseSchemaReference = shellPath.GetProperty(writeVerb)
                .GetProperty("responses")
                .GetProperty("200")
                .GetProperty("content")
                .GetProperty("application/json")
                .GetProperty("schema")
                .GetProperty("$ref")
                .GetString();
            Assert.Contains("PersonnelFilePersonalInfoResponse", writeResponseSchemaReference, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ApplicationDbContext_Model_ShouldRequirePublicId_ForEveryPersistedEntity()
    {
        _ = await factory.ResetDatabaseAsync();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var missingPublicIds = dbContext.Model.GetEntityTypes()
            .Where(static entityType => !entityType.IsOwned())
            .Where(static entityType => entityType.ClrType is not null && typeof(Entity).IsAssignableFrom(entityType.ClrType))
            .Where(static entityType => entityType.FindProperty(nameof(Entity.PublicId)) is null)
            .Select(static entityType => entityType.ClrType.FullName ?? entityType.Name)
            .OrderBy(static name => name)
            .ToArray();

        Assert.True(
            missingPublicIds.Length == 0,
            $"Persisted entities missing PublicId: {string.Join(", ", missingPublicIds)}");
    }

    private static bool IsIdentifierName(string? name) =>
        !string.IsNullOrWhiteSpace(name) &&
        name.EndsWith("Id", StringComparison.Ordinal);

    // §S6: covers both scalar (`*Id`/`id`) and collection (`*Ids`/`ids`)
    // identifier shapes — the surrogate-integer leak vector for either form.
    private static bool IsIdentifierLikeName(string? name) =>
        !string.IsNullOrWhiteSpace(name) &&
        (name.EndsWith("Id", StringComparison.Ordinal) ||
         name.EndsWith("Ids", StringComparison.Ordinal) ||
         name.Equals("id", StringComparison.Ordinal) ||
         name.Equals("ids", StringComparison.Ordinal));

    private static bool IsIntegerSchema(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        // Direct integer property, or a nullable wrapper (OpenAPI 3.1 style:
        // "type": ["integer","null"]) / array-of-integer collection id.
        if (schema.TryGetProperty("type", out var type))
        {
            if (type.ValueKind == JsonValueKind.String &&
                type.GetString() == "integer")
            {
                return true;
            }

            if (type.ValueKind == JsonValueKind.Array &&
                type.EnumerateArray().Any(entry =>
                    entry.ValueKind == JsonValueKind.String &&
                    entry.GetString() == "integer"))
            {
                return true;
            }

            if (type.ValueKind == JsonValueKind.String &&
                type.GetString() == "array" &&
                schema.TryGetProperty("items", out var items))
            {
                return IsIntegerSchema(items);
            }
        }

        return false;
    }

    private static bool IsEnumSchema(JsonElement schema) =>
        schema.ValueKind == JsonValueKind.Object &&
        schema.TryGetProperty("enum", out _);

    private static bool IsPublicIdentifierName(string name) =>
        name.Equals("publicId", StringComparison.Ordinal) ||
        name.EndsWith("PublicId", StringComparison.Ordinal);

    private static bool HasDuplicatedPublicSegment(string name) =>
        name.Contains("PublicPublic", StringComparison.Ordinal);

    private static NestedCollectionRoute? TryParseNestedCollectionRoute(string path)
    {
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length != 5)
        {
            return null;
        }

        if (!segments[0].Equals("api", StringComparison.Ordinal) ||
            !segments[1].Equals("v1", StringComparison.Ordinal))
        {
            return null;
        }

        if (!TryGetPlaceholderName(segments[3], out var parentIdentifier) || !IsPublicIdentifierName(parentIdentifier))
        {
            return null;
        }

        return new NestedCollectionRoute(segments[2], parentIdentifier, segments[4]);
    }

    private static bool TryGetPlaceholderName(string segment, out string placeholderName)
    {
        var match = RoutePlaceholderRegex.Match(segment);
        if (!match.Success)
        {
            placeholderName = string.Empty;
            return false;
        }

        placeholderName = match.Groups["name"].Value;
        return true;
    }

    private sealed record NestedCollectionRoute(string ParentSegment, string ParentIdentifier, string ChildSegment);
}

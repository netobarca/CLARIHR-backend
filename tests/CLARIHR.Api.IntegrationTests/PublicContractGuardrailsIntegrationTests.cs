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

        Assert.Contains("/api/account/companies/{companyPublicId}", pathNames);
        Assert.Contains("/api/account/companies/{companyPublicId}/switch", pathNames);
        Assert.Contains("/api/v1/companies/{companyPublicId}/cost-centers", pathNames);
        Assert.Contains("/api/v1/companies/{companyPublicId}/job-profiles", pathNames);

        Assert.DoesNotContain("/api/account/companies/{publicId}", pathNames);
        Assert.DoesNotContain("/api/account/companies/{publicId}/switch", pathNames);
        Assert.DoesNotContain("/api/v1/job-profiles/{id}", pathNames);
        Assert.DoesNotContain(pathNames, static path => path.Contains("{companyId}", StringComparison.Ordinal));
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

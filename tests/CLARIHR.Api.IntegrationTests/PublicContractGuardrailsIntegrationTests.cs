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

                if (!placeholder.Equals("companyPublicId", StringComparison.Ordinal))
                {
                    Assert.Equal(
                        "publicId",
                        placeholder);
                }
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

                    if (string.Equals(location, "path", StringComparison.Ordinal) &&
                        !name!.Equals("companyPublicId", StringComparison.Ordinal))
                    {
                        Assert.Equal(
                            "publicId",
                            name);
                    }
                }
            }
        }
    }

    [Fact]
    public async Task Swagger_ShouldUsePublicId_ForDirectResourceRoutes_AndCompanyPublicId_ForCompanyContextRoutes()
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

        Assert.Contains("/api/iam/roles/{publicId}", pathNames);
        Assert.Contains("/api/iam/permissions/{publicId}", pathNames);
        Assert.Contains("/api/iam/users/{publicId}", pathNames);
        Assert.Contains("/api/company/users/{publicId}", pathNames);
        Assert.Contains("/api/audit/logs/{publicId}", pathNames);
        Assert.Contains("/api/account/companies/{publicId}", pathNames);
        Assert.Contains("/api/account/companies/{publicId}/switch", pathNames);
        Assert.Contains("/api/v1/companies/{companyPublicId}/cost-centers", pathNames);
        Assert.Contains("/api/v1/companies/{companyPublicId}/job-profiles", pathNames);
        Assert.Contains("/api/v1/job-profiles/{publicId}", pathNames);
        Assert.Contains("/api/v1/job-profiles/{publicId}/print", pathNames);

        Assert.DoesNotContain("/api/iam/roles/{rolePublicId}", pathNames);
        Assert.DoesNotContain("/api/iam/permissions/{permissionPublicId}", pathNames);
        Assert.DoesNotContain("/api/iam/users/{userPublicId}", pathNames);
        Assert.DoesNotContain("/api/company/users/{userPublicId}", pathNames);
        Assert.DoesNotContain("/api/audit/logs/{auditLogPublicId}", pathNames);
        Assert.DoesNotContain("/api/account/companies/{companyPublicId}", pathNames);
        Assert.DoesNotContain("/api/account/companies/{companyPublicId}/switch", pathNames);
        Assert.DoesNotContain("/api/v1/job-profiles/{id}", pathNames);
        Assert.DoesNotContain("/api/v1/job-profiles/{jobProfilePublicId}", pathNames);
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
}

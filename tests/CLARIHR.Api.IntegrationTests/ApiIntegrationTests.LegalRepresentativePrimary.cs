using System.Net;
using System.Net.Http.Json;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// Regression coverage for the primary-representative switch. `ux_legal_representatives__tenant_primary_active`
/// is a PARTIAL UNIQUE INDEX (`WHERE is_primary = true AND is_active = true`), evaluated per statement and not
/// deferrable. The three handlers that move the flag used to demote the current primary and promote the new one
/// in a SINGLE SaveChanges; EF is free to order the promotion first, which leaves two (primary, active) rows for
/// an instant and makes Postgres reject the batch — the caller got a `500`.
///
/// These cases only fail against a real database: a unit test with a fake repository never issues the SQL, so
/// nothing catches the ordering. Two of the three paths were reproducibly broken in production.
/// </summary>
public sealed partial class ApiIntegrationTests
{
    private sealed record PrimaryRepItem(Guid PublicId, bool? IsPrimary, bool IsActive, Guid ConcurrencyToken);

    private static object BuildRepresentativeBody(string firstName, string documentNumber, bool isPrimary) =>
        new
        {
            firstName,
            lastName = "Prueba",
            documentType = "DUI",
            documentNumber,
            positionTitle = "Representante Legal",
            representationType = "AttorneyInFact",
            authorityDescription = (string?)null,
            appointmentInstrument = (string?)null,
            appointmentDateUtc = (DateTime?)null,
            effectiveFromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            effectiveToUtc = (DateTime?)null,
            email = (string?)null,
            phone = (string?)null,
            isPrimary
        };

    private async Task<PrimaryRepItem> CreateRepresentativeAsync(
        HttpClient client, Guid tenantId, string firstName, string documentNumber, bool isPrimary)
    {
        var response = await client.PostJsonAsync(
            $"/api/v1/companies/{tenantId}/legal-representatives",
            BuildRepresentativeBody(firstName, documentNumber, isPrimary));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<PrimaryRepItem>(JsonOptions);
        Assert.NotNull(created);
        return created!;
    }

    /// <summary>Exactly one active primary must remain, and it must be the expected one.</summary>
    private async Task AssertSingleActivePrimaryAsync(Guid tenantId, Guid expectedPublicId)
    {
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var primaries = await dbContext.LegalRepresentatives
            .IgnoreQueryFilters()
            .Where(item => item.TenantId == tenantId && item.IsPrimary == true && item.IsActive)
            .Select(item => item.PublicId)
            .ToListAsync();

        Assert.Single(primaries);
        Assert.Equal(expectedPublicId, primaries[0]);
    }

    private static HttpRequestMessage IfMatch(HttpMethod method, string uri, Guid concurrencyToken, object? body = null)
    {
        var request = new HttpRequestMessage(method, uri);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        request.Headers.TryAddWithoutValidation("If-Match", concurrencyToken.ToString("D"));
        return request;
    }

    [Fact]
    public async Task SetPrimary_WhenAnotherRepresentativeIsAlreadyPrimary_ShouldPromoteAndDemote()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId, "LegalRepresentatives.Admin"));

        await CreateRepresentativeAsync(client, scenario.TenantId, "Primero", "10000000-1", isPrimary: true);
        var second = await CreateRepresentativeAsync(client, scenario.TenantId, "Segundo", "20000000-2", isPrimary: false);

        var response = await client.SendAsync(IfMatch(
            HttpMethod.Patch,
            $"/api/v1/legal-representatives/{second.PublicId}/set-primary",
            second.ConcurrencyToken));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var promoted = await response.Content.ReadFromJsonAsync<PrimaryRepItem>(JsonOptions);
        Assert.NotNull(promoted);
        Assert.True(promoted!.IsPrimary);
        Assert.NotEqual(second.ConcurrencyToken, promoted.ConcurrencyToken);

        await AssertSingleActivePrimaryAsync(scenario.TenantId, second.PublicId);
    }

    [Fact]
    public async Task Update_WithIsPrimaryTrue_WhenAnotherRepresentativeIsAlreadyPrimary_ShouldPromoteAndDemote()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId, "LegalRepresentatives.Admin"));

        await CreateRepresentativeAsync(client, scenario.TenantId, "Primero", "10000000-1", isPrimary: true);
        var second = await CreateRepresentativeAsync(client, scenario.TenantId, "Segundo", "20000000-2", isPrimary: false);

        var response = await client.SendAsync(IfMatch(
            HttpMethod.Put,
            $"/api/v1/legal-representatives/{second.PublicId}",
            second.ConcurrencyToken,
            BuildRepresentativeBody("Segundo", "20000000-2", isPrimary: true)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AssertSingleActivePrimaryAsync(scenario.TenantId, second.PublicId);
    }

    [Fact]
    public async Task Create_WithIsPrimaryTrue_WhenAnotherRepresentativeIsAlreadyPrimary_ShouldDemoteTheIncumbent()
    {
        var scenario = await factory.ResetDatabaseAsync();
        using var client = factory.CreateClientFor(
            TestUserContext.Authenticated(scenario.ActorUserId, scenario.TenantId, "LegalRepresentatives.Admin"));

        await CreateRepresentativeAsync(client, scenario.TenantId, "Primero", "10000000-1", isPrimary: true);
        var second = await CreateRepresentativeAsync(client, scenario.TenantId, "Segundo", "20000000-2", isPrimary: true);

        await AssertSingleActivePrimaryAsync(scenario.TenantId, second.PublicId);
    }
}

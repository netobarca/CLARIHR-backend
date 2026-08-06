using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Domain.Auth;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

/// <summary>
/// Covers the guarantee the permissions rework exists for: authorization is resolved from the database on
/// every request, so revoking access takes effect on the NEXT request instead of when the token expires.
///
/// These run against the real JWT factory on purpose. The main integration factory substitutes
/// <c>IEffectiveAccessResolver</c> with a header-driven stand-in, which is the right trade for the hundreds
/// of tests that assert authorization DECISIONS — but it means those tests never exercise the resolver
/// itself. This file does, end to end: real token, real IAM rows, real cache invalidation.
/// </summary>
public sealed class EffectiveAccessRevocationIntegrationTests(CoreJwtIntegrationTestWebApplicationFactory factory)
    : IClassFixture<CoreJwtIntegrationTestWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private const string Email = "revocation@clarihr.local";
    private const string Password = "StrongPass123!";

    [Fact]
    public async Task AccessToken_ShouldNotCarryAuthorizationClaims()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var token = await RegisterActivateAndLoginAsync(client);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        // Authorization must be resolved per request, never carried. A token that lists permissions cannot
        // be revoked before it expires, and for an owner it grew to ~5 KB on every single request.
        Assert.DoesNotContain(jwt.Claims, static claim =>
            claim.Type is "permission" or "permissions" or "role" or System.Security.Claims.ClaimTypes.Role);
    }

    /// <summary>
    /// Splits the pipeline in half. If this passes and the revocation test still 403s, the rows and the
    /// query are right and the fault is downstream (claims transformation, principal, policy); if this
    /// fails, nothing downstream can work.
    /// </summary>
    [Fact]
    public async Task Resolver_ShouldReturnTheOwnersAccess_FromTheDatabase()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var token = await RegisterActivateAndLoginAsync(client);
        var companyPublicId = await CreateCompanyAsync(client, token);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userPublicId = await dbContext.AuthUsers
            .Where(user => user.NormalizedEmail == Email.ToLowerInvariant())
            .Select(user => user.PublicId)
            .SingleAsync();

        var resolver = scope.ServiceProvider.GetRequiredService<IEffectiveAccessResolver>();
        var access = await resolver.ResolveAsync(userPublicId, companyPublicId, CancellationToken.None);

        Assert.NotEmpty(access.Roles);
        Assert.Contains("ORGSTRUCTURECATALOGS.READ", access.Permissions);
    }

    [Fact]
    public async Task RevokingRoleAssignments_ShouldDenyOnTheVeryNextRequest_WithTheSameToken()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

        var token = await RegisterActivateAndLoginAsync(client);
        var companyPublicId = await CreateCompanyAsync(client, token);

        // Switching mints a token scoped to the new tenant; the caller is its owner and holds everything.
        var tenantToken = await SwitchAsync(client, token, companyPublicId);
        var probeUrl = $"/api/v1/companies/{companyPublicId}/organization-structure-catalogs/unit-types";

        var granted = await GetAsync(client, tenantToken, probeUrl);
        await AssertStatusAsync(granted, HttpStatusCode.OK);

        await RemoveAllRoleAssignmentsAsync(companyPublicId);

        // Same token, unexpired. Under the old design the JWT carried the permissions and this would still
        // return 200 for up to 15 minutes.
        var denied = await GetAsync(client, tenantToken, probeUrl);
        await AssertStatusAsync(denied, HttpStatusCode.Forbidden);
    }

    // A bare status comparison on an authorization probe says "403" and nothing about which gate fired.
    // The body carries the error code, and that is the whole difference between a five-minute diagnosis
    // and an afternoon of guessing.
    private static async Task AssertStatusAsync(HttpResponseMessage response, HttpStatusCode expected)
    {
        if (response.StatusCode == expected)
        {
            return;
        }

        Assert.Fail(
            $"Expected {expected} from {response.RequestMessage?.RequestUri}, got {response.StatusCode}. " +
            $"Body: {await response.Content.ReadAsStringAsync()}");
    }

    private async Task<string> RegisterActivateAndLoginAsync(HttpClient client)
    {
        var registerResponse = await client.PostJsonAsync("/api/v1/auth/register", new
        {
            firstName = "Revo",
            lastName = "Cation",
            email = Email,
            password = Password,
            country = "SV",
            source = "integration-tests"
        });
        Assert.Equal(HttpStatusCode.Accepted, registerResponse.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await dbContext.AuthUsers.SingleAsync(item => item.NormalizedEmail == Email.ToLowerInvariant());
            user.ConfirmEmail();
            await dbContext.SaveChangesAsync();
        }

        var loginResponse = await client.PostJsonAsync("/api/v1/auth/login", new { email = Email, password = Password });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var payload = await loginResponse.Content.ReadFromJsonAsync<AuthEnvelope>(JsonOptions);
        Assert.NotNull(payload);
        return payload!.AccessToken;
    }

    private static async Task<Guid> CreateCompanyAsync(HttpClient client, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/account/companies")
        {
            Content = JsonContent.Create(new
            {
                name = "Revocacion S.A. de C.V.",
                countryCode = "SV",
                companyTypePublicId = (Guid?)null,
                initialLegalRepresentative = new
                {
                    firstName = "Ana",
                    lastName = "Portillo",
                    documentType = "DUI",
                    documentNumber = "01234567-8",
                    positionTitle = "Representante Legal",
                    representationType = "PrimaryLegalRepresentative",
                    authorityDescription = (string?)null,
                    appointmentInstrument = (string?)null,
                    appointmentDateUtc = (DateTime?)null,
                    effectiveFromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                    effectiveToUtc = (DateTime?)null,
                    email = (string?)null,
                    phone = (string?)null,
                    isPrimary = true
                }
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("publicId").GetGuid();
    }

    private static async Task<string> SwitchAsync(HttpClient client, string token, Guid companyPublicId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/account/companies/{companyPublicId}/switch");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("accessToken").GetString()!;
    }

    private static async Task<HttpResponseMessage> GetAsync(HttpClient client, string token, string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private async Task RemoveAllRoleAssignmentsAsync(Guid tenantId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var assignments = await dbContext.IamUserRoleAssignments
            .IgnoreQueryFilters()
            .Where(assignment => assignment.TenantId == tenantId)
            .ToListAsync();

        dbContext.IamUserRoleAssignments.RemoveRange(assignments);

        // SaveChanges is what drops the tenant's cached access (EffectiveAccessInvalidationInterceptor).
        await dbContext.SaveChangesAsync();
    }

    private sealed record AuthEnvelope(string AccessToken);
}

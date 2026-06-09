using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Domain.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace CLARIHR.Api.IntegrationTests;

public sealed class PlatformAuthenticationIntegrationTests(
    CoreJwtIntegrationTestWebApplicationFactory coreFactory,
    BackofficeJwtIntegrationTestWebApplicationFactory backofficeFactory)
    : IClassFixture<CoreJwtIntegrationTestWebApplicationFactory>,
      IClassFixture<BackofficeJwtIntegrationTestWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = IntegrationTestJson.CreateOptions();
    private static readonly Guid PlatformUserPublicId = Guid.Parse("90000000-0000-0000-0000-000000000003");

    [Fact]
    public async Task CoreLogin_ShouldEmitCoreClientType_WithoutPlatformRole()
    {
        await coreFactory.ResetDatabaseAsync();
        using var client = coreFactory.CreateClient();

        var registerResponse = await client.PostJsonAsync("/api/auth/register", new
        {
            firstName = "Dev",
            lastName = "Platform",
            email = "dev@clarihr.local",
            password = "StrongPass123!",
            country = "SV",
            source = "integration-tests"
        });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var loginResponse = await client.PostJsonAsync("/api/auth/login", new
        {
            email = "dev@clarihr.local",
            password = "StrongPass123!"
        });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var authPayload = await loginResponse.Content.ReadFromJsonAsync<AuthEnvelope>(JsonOptions);
        Assert.NotNull(authPayload);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(authPayload!.AccessToken);
        Assert.Equal(AuthClientType.Core.ToClaimValue(), jwt.Claims.Single(claim => claim.Type == "client_type").Value);
        Assert.DoesNotContain(jwt.Claims, claim => claim.Type == System.Security.Claims.ClaimTypes.Role && claim.Value == "platform_admin");
    }

    [Fact]
    public async Task PlatformLogin_WithoutPlatformOperator_ShouldReturnForbidden()
    {
        const string password = "StrongPass123!";

        await backofficeFactory.ResetDatabaseWithServicesAsync(async (services, dbContext) =>
        {
            var hasher = services.GetRequiredService<IPasswordHasher>();
            await PlatformTestSeed.SeedLocalUserAsync(
                dbContext,
                PlatformUserPublicId,
                "platform.user@clarihr.test",
                hasher.Hash(password));
        });

        using var client = backofficeFactory.CreateClient();

        var response = await client.PostJsonAsync("/api/platform/auth/login", new
        {
            email = "platform.user@clarihr.test",
            password
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        Assert.Equal("PLATFORM_ACCESS_FORBIDDEN", document.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task PlatformLogin_WithPlatformOperator_ShouldEmitPlatformClientTypeWithoutTenant()
    {
        const string password = "StrongPass123!";

        await backofficeFactory.ResetDatabaseWithServicesAsync(async (services, dbContext) =>
        {
            var hasher = services.GetRequiredService<IPasswordHasher>();
            await PlatformTestSeed.SeedPlatformOperatorAsync(
                dbContext,
                PlatformUserPublicId,
                "platform.user@clarihr.test",
                hasher.Hash(password));
        });

        using var client = backofficeFactory.CreateClient();

        var response = await client.PostJsonAsync("/api/platform/auth/login", new
        {
            email = "platform.user@clarihr.test",
            password
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var authPayload = await response.Content.ReadFromJsonAsync<AuthEnvelope>(JsonOptions);
        Assert.NotNull(authPayload);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(authPayload!.AccessToken);
        Assert.Equal(AuthClientType.Platform.ToClaimValue(), jwt.Claims.Single(claim => claim.Type == "client_type").Value);
        Assert.DoesNotContain(jwt.Claims, static claim => claim.Type == "tid");
    }

    [Fact]
    public async Task CoreAndPlatformTokens_ShouldNotCrossApis()
    {
        const string corePassword = "StrongPass123!";
        const string platformPassword = "StrongPass123!";

        await coreFactory.ResetDatabaseAsync();
        await backofficeFactory.ResetDatabaseWithServicesAsync(async (services, dbContext) =>
        {
            var hasher = services.GetRequiredService<IPasswordHasher>();
            await PlatformTestSeed.SeedPlatformOperatorAsync(
                dbContext,
                PlatformUserPublicId,
                "platform.user@clarihr.test",
                hasher.Hash(platformPassword));
        });

        using var coreClient = coreFactory.CreateClient();
        _ = await coreClient.PostJsonAsync("/api/auth/register", new
        {
            firstName = "Core",
            lastName = "User",
            email = "core.user@clarihr.test",
            password = corePassword,
            country = "SV",
            source = "integration-tests"
        });
        var coreLoginResponse = await coreClient.PostJsonAsync("/api/auth/login", new
        {
            email = "core.user@clarihr.test",
            password = corePassword
        });
        coreLoginResponse.EnsureSuccessStatusCode();
        var coreAuth = await coreLoginResponse.Content.ReadFromJsonAsync<AuthEnvelope>(JsonOptions);

        using var backofficeClient = backofficeFactory.CreateClient();
        var platformLoginResponse = await backofficeClient.PostJsonAsync("/api/platform/auth/login", new
        {
            email = "platform.user@clarihr.test",
            password = platformPassword
        });
        platformLoginResponse.EnsureSuccessStatusCode();
        var platformAuth = await platformLoginResponse.Content.ReadFromJsonAsync<AuthEnvelope>(JsonOptions);

        using var backofficeProtectedClient = backofficeFactory.CreateClient();
        backofficeProtectedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", coreAuth!.AccessToken);
        var coreTokenAgainstBackoffice = await backofficeProtectedClient.GetAsync("/api/platform/commercial-plans");
        Assert.Equal(HttpStatusCode.Unauthorized, coreTokenAgainstBackoffice.StatusCode);

        using var coreProtectedClient = coreFactory.CreateClient();
        coreProtectedClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", platformAuth!.AccessToken);
        var platformTokenAgainstCore = await coreProtectedClient.GetAsync("/api/v1/account/companies/countries");
        Assert.Equal(HttpStatusCode.Unauthorized, platformTokenAgainstCore.StatusCode);
    }

    [Fact]
    public async Task CoreLoginAndRefreshTokens_ShouldAccessCompaniesList()
    {
        await coreFactory.ResetDatabaseAsync();

        var email = $"jwt.login.{Guid.NewGuid():N}@clarihr.test";
        const string password = "StrongPass123!";

        using var anonymousClient = coreFactory.CreateClient();
        var registerResponse = await anonymousClient.PostJsonAsync("/api/auth/register", new
        {
            firstName = "Jwt",
            lastName = "Regression",
            email,
            password,
            country = "SV",
            source = "integration-tests"
        });
        Assert.Equal(HttpStatusCode.Created, registerResponse.StatusCode);

        var registerPayload = await registerResponse.Content.ReadFromJsonAsync<AuthEnvelope>(JsonOptions);
        Assert.NotNull(registerPayload);

        using var companyProvisioningClient = coreFactory.CreateClient();
        companyProvisioningClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", registerPayload!.AccessToken);

        var createCompanyResponse = await companyProvisioningClient.PostJsonAsync("/api/v1/account/companies", new
        {
            name = "JWT Regression Company",
            countryCode = "SV",
            initialLegalRepresentative = CreateInitialLegalRepresentativePayload()
        });
        Assert.Equal(HttpStatusCode.Created, createCompanyResponse.StatusCode);

        using var createCompanyDocument = JsonDocument.Parse(await createCompanyResponse.Content.ReadAsStringAsync());
        var companyPublicId = createCompanyDocument.RootElement.GetProperty("publicId").GetGuid();

        var switchResponse = await companyProvisioningClient.PostAsync(
            $"/api/v1/account/companies/{companyPublicId}/switch",
            content: null);
        Assert.Equal(HttpStatusCode.OK, switchResponse.StatusCode);

        var loginResponse = await anonymousClient.PostJsonAsync("/api/auth/login", new
        {
            email,
            password
        });
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);

        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<AuthEnvelope>(JsonOptions);
        Assert.NotNull(loginPayload);

        var loginJwt = new JwtSecurityTokenHandler().ReadJwtToken(loginPayload!.AccessToken);
        Assert.Equal(AuthClientType.Core.ToClaimValue(), loginJwt.Claims.Single(claim => claim.Type == "client_type").Value);
        Assert.Contains(loginJwt.Claims, static claim => claim.Type == "tid");
        Assert.DoesNotContain(loginJwt.Claims, static claim => claim.Type is System.Security.Claims.ClaimTypes.Role or "role" or "permission" or "permissions");

        using var companiesClient = coreFactory.CreateClient();
        companiesClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginPayload.AccessToken);

        var companiesResponse = await companiesClient.GetAsync("/api/v1/account/companies?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, companiesResponse.StatusCode);
        await AssertContainsCompanyAsync(companiesResponse, "JWT Regression Company");

        var refreshResponse = await anonymousClient.PostJsonAsync("/api/auth/refresh", new
        {
            refreshToken = loginPayload.RefreshToken
        });
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        var refreshPayload = await refreshResponse.Content.ReadFromJsonAsync<AuthEnvelope>(JsonOptions);
        Assert.NotNull(refreshPayload);

        using var refreshedCompaniesClient = coreFactory.CreateClient();
        refreshedCompaniesClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", refreshPayload!.AccessToken);

        var refreshedCompaniesResponse = await refreshedCompaniesClient.GetAsync("/api/v1/account/companies?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, refreshedCompaniesResponse.StatusCode);
        await AssertContainsCompanyAsync(refreshedCompaniesResponse, "JWT Regression Company");
    }

    private static object CreateInitialLegalRepresentativePayload() => new
    {
        firstName = "Ana",
        lastName = "Mendoza",
        documentType = "TaxId",
        documentNumber = "0614-290190-102-3",
        positionTitle = "Representante Legal",
        representationType = "PrimaryLegalRepresentative",
        authorityDescription = "Representacion general",
        appointmentInstrument = "Acta de nombramiento",
        appointmentDateUtc = DateTime.UtcNow.Date,
        effectiveFromUtc = DateTime.UtcNow.Date,
        effectiveToUtc = (DateTime?)null,
        email = "ana.mendoza@test.com",
        phone = "+50370000000",
        isPrimary = true
    };

    private static async Task AssertContainsCompanyAsync(HttpResponseMessage response, string companyName)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var items = document.RootElement.GetProperty("items");

        Assert.Contains(
            items.EnumerateArray(),
            item => string.Equals(item.GetProperty("name").GetString(), companyName, StringComparison.Ordinal));
    }

    private sealed record AuthEnvelope(
        string AccessToken,
        string? RefreshToken,
        int ExpiresIn);
}

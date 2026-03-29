using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace CLARIHR.Api.IntegrationTests;

public sealed class PlatformAdminAuthenticationIntegrationTests(IntegrationTestWebApplicationFactory factory)
    : IClassFixture<IntegrationTestWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = IntegrationTestJson.CreateOptions();

    [Fact]
    public async Task Login_WithConfiguredPlatformAdminEmail_ShouldEmitPlatformAdminClaim()
    {
        await factory.ResetDatabaseAsync();
        using var client = factory.CreateClient();

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
        Assert.Equal("platform_admin", jwt.Claims.Single(claim => claim.Type == System.Security.Claims.ClaimTypes.Role).Value);
    }

    private sealed record AuthEnvelope(
        string AccessToken,
        string RefreshToken,
        int ExpiresIn);
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CLARIHR.Application.Features.Auth.RegisterUser;
using Xunit;

namespace CLARIHR.Api.IntegrationTests;

internal static class AuthFlowTestExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = IntegrationTestJson.CreateOptions();

    // Registers a local user and redeems the captured verification link, returning the issued session.
    // Mirrors the real AU-1 flow (register -> 202 Accepted -> email-verification/confirm -> session) for
    // tests that need an authenticated, verified local user.
    public static async Task<AuthResponse> RegisterAndVerifyAsync(
        this IntegrationTestWebApplicationFactory factory,
        HttpClient client,
        string email,
        string password,
        string firstName = "Test",
        string lastName = "User")
    {
        var registerResponse = await client.PostJsonAsync("/api/v1/auth/register", new
        {
            firstName,
            lastName,
            email,
            password,
            country = "SV",
            source = "integration-tests"
        });
        Assert.Equal(HttpStatusCode.Accepted, registerResponse.StatusCode);

        var token = factory.AuthEmails.LatestVerificationTokenFor(email);
        Assert.False(string.IsNullOrWhiteSpace(token));

        var confirmResponse = await client.PostJsonAsync(
            "/api/v1/auth/email-verification/confirm",
            new { token });
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);

        var payload = await confirmResponse.Content.ReadFromJsonAsync<AuthResponse>(JsonOptions);
        Assert.NotNull(payload);
        return payload!;
    }
}
